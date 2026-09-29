using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Knowledge.History;
using UnityIsekaiGame.Knowledge.Sharing;
using UnityIsekaiGame.Knowledge.Sources;
using UnityIsekaiGame.Persistence;

namespace UnityIsekaiGame.Tests
{
    public sealed class InformationAccessTests
    {
        [Test]
        public void PolicyDefinitionValidationRequiresCanonicalAccessIdAndDetails()
        {
            InformationAccessPolicyDefinition valid = PolicyDefinition(
                "information-access.test.secret",
                InformationSubjectType.Memory,
                InformationVisibilityClassification.Secret,
                InformationDisclosurePolicy.RedactedOnly,
                InformationResharingPolicy.NoResharing,
                InformationSourceVisibilityPolicy.HideOriginal,
                InformationDetailVisibilityPolicy.Selected,
                InformationAuditPolicy.AuditDeniedAndGranted,
                new[] { "detail.summary" },
                new[] { "detail.source" },
                new[] { "detail.body" });
            DefinitionValidationReport validReport = Validate(valid);

            InformationAccessPolicyDefinition invalid = PolicyDefinition(
                "test.secret",
                InformationSubjectType.Memory,
                InformationVisibilityClassification.Secret,
                InformationDisclosurePolicy.FreelyDisclose,
                InformationResharingPolicy.NoResharing,
                InformationSourceVisibilityPolicy.Reveal,
                InformationDetailVisibilityPolicy.Selected,
                InformationAuditPolicy.None,
                Array.Empty<string>(),
                null,
                null);
            DefinitionValidationReport invalidReport = Validate(invalid);

            Assert.That(validReport.ErrorCount, Is.EqualTo(0), validReport.GetSummary());
            Assert.That(validReport.WarningCount, Is.EqualTo(0), validReport.GetSummary());
            Assert.That(invalidReport.WarningCount, Is.EqualTo(1), invalidReport.GetSummary());
            Assert.That(invalidReport.ErrorCount, Is.EqualTo(2), invalidReport.GetSummary());
        }

        [Test]
        public void PublicAndSecretPoliciesEvaluateWithoutMutatingKnowledge()
        {
            InformationAccessRuntime runtime = Runtime();
            Register(runtime, PublicPolicy());
            Register(runtime, SecretPolicy());

            InformationAccessDecision publicDecision = runtime.EvaluateAccess(Context(PublicPolicyId, PublicSubjectId, InformationSubjectType.FactInstance, "person.visitor", InformationAccessMode.Inspect, discovered: true));
            InformationAccessDecision secretDecision = runtime.EvaluateAccess(Context(SecretPolicyId, SecretSubjectId, InformationSubjectType.Memory, "person.visitor", InformationAccessMode.Inspect, discovered: true, revealDenial: true));

            Assert.That(publicDecision.FullAccess, Is.True);
            Assert.That(secretDecision.Denied, Is.True);
            Assert.That(secretDecision.DenialCode, Is.EqualTo(InformationAccessDenialCode.MissingAuthorization));
            Assert.That(runtime.CreateSnapshot().Policies.Count, Is.EqualTo(2));
        }

        [Test]
        public void GrantControlsDetailsSourceVisibilityAndResharing()
        {
            InformationAccessRuntime runtime = Runtime();
            Register(runtime, SecretPolicy());
            Grant(runtime, "grant.inspect", "person.listener", new[] { InformationAccessMode.Inspect }, sourceVisibility: InformationSourceVisibilityPolicy.PrivilegedOnly, permitsResharing: false);
            Grant(runtime, "grant.source", "person.source", new[] { InformationAccessMode.RevealSource }, sourceVisibility: InformationSourceVisibilityPolicy.Reveal, permitsResharing: true);

            InformationAccessDecision inspect = runtime.EvaluateAccess(Context(SecretPolicyId, SecretSubjectId, InformationSubjectType.Memory, "person.listener", InformationAccessMode.Inspect, discovered: true));
            InformationAccessDecision source = runtime.EvaluateAccess(Context(SecretPolicyId, SecretSubjectId, InformationSubjectType.Memory, "person.source", InformationAccessMode.RevealSource, discovered: true));

            Assert.That(inspect.RedactedAccess, Is.True);
            Assert.That(inspect.AllowedDetails, Does.Contain("detail.summary"));
            Assert.That(inspect.RedactedDetails, Does.Contain("detail.source"));
            Assert.That(inspect.HiddenDetails, Does.Contain("detail.body"));
            Assert.That(inspect.SourceVisible, Is.False);
            Assert.That(inspect.ResharingOutcome, Is.EqualTo(InformationResharingPolicy.NoResharing));
            Assert.That(source.SourceVisible, Is.True);
        }

        [Test]
        public void ExplicitDenialOverridesGrantAndGrantRevocationBlocksFutureAccess()
        {
            InformationAccessRuntime runtime = Runtime();
            Register(runtime, SecretPolicy());
            Grant(runtime, "grant.inspect", "person.listener", new[] { InformationAccessMode.Inspect }, InformationSourceVisibilityPolicy.PrivilegedOnly, permitsResharing: false);
            InformationAccessOperationResult denial = runtime.AddDenial(new InformationAccessDenialData
            {
                denialId = "denial.listener.inspect",
                policyId = SecretPolicyId,
                subject = Subject(InformationSubjectType.Memory, SecretSubjectId, OwnerId),
                deniedKind = InformationGranteeKind.Person,
                deniedId = "person.listener",
                accessModes = new[] { InformationAccessMode.Inspect },
                reason = "test denial"
            }, "tx.denial");
            InformationAccessDecision denied = runtime.EvaluateAccess(Context(SecretPolicyId, SecretSubjectId, InformationSubjectType.Memory, "person.listener", InformationAccessMode.Inspect, discovered: true));
            InformationAccessOperationResult revoke = runtime.RevokeGrant("grant.inspect", "tx.revoke", 12d);
            InformationAccessDecision revoked = runtime.EvaluateAccess(Context(SecretPolicyId, SecretSubjectId, InformationSubjectType.Memory, "person.listener", InformationAccessMode.Inspect, discovered: true));

            Assert.That(denial.Succeeded, Is.True, denial.Message);
            Assert.That(denied.DenialCode, Is.EqualTo(InformationAccessDenialCode.ExplicitDenial));
            Assert.That(revoke.Succeeded, Is.True, revoke.Message);
            Assert.That(revoked.Denied, Is.True);
            Assert.That(runtime.CreateSnapshot().Grants.Single().Revoked, Is.True);
        }

        [Test]
        public void PolicyPrecedenceIsDeterministicAcrossDenialsGrantsAndContext()
        {
            InformationAccessRuntime runtime = Runtime();
            InformationAccessPolicyData policy = SecretPolicy();
            policy.allowedOrganizationIds = new[] { "organization.archive" };
            policy.allowedRoleIds = new[] { "role.medic" };
            Register(runtime, policy);
            Grant(runtime, "grant.person", "person.listener", new[] { InformationAccessMode.Inspect }, InformationSourceVisibilityPolicy.PrivilegedOnly, permitsResharing: false);
            runtime.AddDenial(new InformationAccessDenialData
            {
                denialId = "denial.organization",
                policyId = SecretPolicyId,
                subject = Subject(InformationSubjectType.Memory, SecretSubjectId, OwnerId),
                deniedKind = InformationGranteeKind.Organization,
                deniedId = "organization.archive",
                accessModes = new[] { InformationAccessMode.Inspect }
            }, "tx.denial.organization");
            runtime.AddDenial(new InformationAccessDenialData
            {
                denialId = "denial.expired",
                policyId = SecretPolicyId,
                subject = Subject(InformationSubjectType.Memory, SecretSubjectId, OwnerId),
                deniedKind = InformationGranteeKind.Person,
                deniedId = "person.expired",
                accessModes = new[] { InformationAccessMode.Inspect },
                effectiveStartTime = 0d,
                expirationTime = 2d
            }, "tx.denial.expired");
            Grant(runtime, "grant.expired-denial", "person.expired", new[] { InformationAccessMode.Inspect }, InformationSourceVisibilityPolicy.PrivilegedOnly, permitsResharing: false);

            InformationAccessContext organizationDenied = Context(SecretPolicyId, SecretSubjectId, InformationSubjectType.Memory, "person.listener", InformationAccessMode.Inspect, discovered: true);
            organizationDenied.OrganizationIds = new[] { "organization.archive" };
            InformationAccessContext roleContext = Context(SecretPolicyId, SecretSubjectId, InformationSubjectType.Memory, "person.role", InformationAccessMode.Inspect, discovered: true);
            roleContext.RoleIds = new[] { "role.medic" };
            InformationAccessDecision denied = runtime.EvaluateAccess(organizationDenied);
            InformationAccessDecision role = runtime.EvaluateAccess(roleContext);
            InformationAccessDecision expiredDenial = runtime.EvaluateAccess(ContextAt(SecretPolicyId, SecretSubjectId, InformationSubjectType.Memory, "person.expired", InformationAccessMode.Inspect, 3d));
            InformationAccessDecision privileged = runtime.EvaluateAccess(new InformationAccessContext
            {
                RequestingPersonId = "person.any",
                Subject = Subject(InformationSubjectType.Memory, SecretSubjectId, OwnerId),
                AccessMode = InformationAccessMode.Inspect,
                ContextKind = InformationContextKind.Validation,
                DeterministicPolicyId = SecretPolicyId,
                WorldTimeSeconds = 1d
            });

            Assert.That(privileged.FullAccess, Is.True);
            Assert.That(denied.DenialCode, Is.EqualTo(InformationAccessDenialCode.ExplicitDenial));
            Assert.That(role.Denied, Is.False);
            Assert.That(expiredDenial.Denied, Is.False);
        }

        [Test]
        public void TimeLimitedGrantExpiresDeterministicallyByWorldTime()
        {
            InformationAccessRuntime runtime = Runtime();
            Register(runtime, SecretPolicy());
            InformationAccessOperationResult grant = runtime.GrantAccess(new InformationAccessGrantData
            {
                grantId = "grant.timed",
                policyId = SecretPolicyId,
                subject = Subject(InformationSubjectType.Memory, SecretSubjectId, OwnerId),
                granteeKind = InformationGranteeKind.Person,
                granteeId = "person.listener",
                grantorId = OwnerId,
                accessModes = new[] { InformationAccessMode.Inspect },
                detailIds = new[] { "detail.summary" },
                effectiveStartTime = 5d,
                expirationTime = 10d
            }, "tx.grant.timed");

            InformationAccessDecision before = runtime.EvaluateAccess(ContextAt(SecretPolicyId, SecretSubjectId, InformationSubjectType.Memory, "person.listener", InformationAccessMode.Inspect, 4d));
            InformationAccessDecision during = runtime.EvaluateAccess(ContextAt(SecretPolicyId, SecretSubjectId, InformationSubjectType.Memory, "person.listener", InformationAccessMode.Inspect, 10d));
            InformationAccessDecision after = runtime.EvaluateAccess(ContextAt(SecretPolicyId, SecretSubjectId, InformationSubjectType.Memory, "person.listener", InformationAccessMode.Inspect, 10.001d));

            Assert.That(grant.Succeeded, Is.True, grant.Message);
            Assert.That(before.Denied, Is.True);
            Assert.That(during.Denied, Is.False);
            Assert.That(after.Denied, Is.True);
        }

        [Test]
        public void ConcealmentDiscoveryClassificationAndAuditAreSeparate()
        {
            InformationAccessRuntime runtime = Runtime();
            Register(runtime, SecretPolicy());
            Register(runtime, DiscoveryPolicy());

            InformationAccessOperationResult conceal = runtime.AddConcealment(new InformationConcealmentData
            {
                concealmentId = "conceal.secret",
                policyId = SecretPolicyId,
                subject = Subject(InformationSubjectType.Memory, SecretSubjectId, OwnerId),
                concealingEntityId = "person.secret-keeper",
                concealmentKind = InformationConcealmentKind.Existence,
                authorizedExceptionIds = new[] { "authorization.reveal-existence" },
                active = true
            }, "tx.conceal");
            InformationAccessDecision concealed = runtime.EvaluateAccess(Context(SecretPolicyId, SecretSubjectId, InformationSubjectType.Memory, "person.visitor", InformationAccessMode.Inspect, discovered: true, revealDenial: true));
            InformationAccessDecision undiscovered = runtime.EvaluateAccess(Context(DiscoveryPolicyId, DiscoverySubjectId, InformationSubjectType.HistoricalEvent, "person.visitor", InformationAccessMode.Query, discovered: false));
            InformationAccessDecision discovered = runtime.EvaluateAccess(Context(DiscoveryPolicyId, DiscoverySubjectId, InformationSubjectType.HistoricalEvent, "person.visitor", InformationAccessMode.Query, discovered: true));
            InformationAccessOperationResult audit = runtime.RecordAudit(concealed, Context(SecretPolicyId, SecretSubjectId, InformationSubjectType.Memory, "person.visitor", InformationAccessMode.Inspect, discovered: true, revealDenial: true));
            InformationAccessOperationResult declassify = runtime.ChangeClassification(SecretPolicyId, InformationVisibilityClassification.Public, OwnerId, "tx.declassify", 20d, "test");
            InformationAccessDecision publicAfter = runtime.EvaluateAccess(Context(SecretPolicyId, SecretSubjectId, InformationSubjectType.Memory, "person.visitor", InformationAccessMode.Inspect, discovered: true, authorizationIds: new[] { "authorization.reveal-existence" }));

            Assert.That(conceal.Succeeded, Is.True);
            Assert.That(concealed.DenialCode, Is.EqualTo(InformationAccessDenialCode.Concealed));
            Assert.That(concealed.VisibleReason, Is.Empty);
            Assert.That(undiscovered.Decision, Is.EqualTo(InformationAccessDecisionKind.NotDiscovered));
            Assert.That(discovered.FullAccess, Is.True);
            Assert.That(audit.Succeeded, Is.True);
            Assert.That(runtime.CreateSnapshot().Audits.Single().unauthorized, Is.True);
            Assert.That(declassify.Succeeded, Is.True);
            Assert.That(publicAfter.FullAccess, Is.True);
        }

        [Test]
        public void RestoreRejectsCorruptPayloadWithoutPartialMutation()
        {
            InformationAccessRuntime runtime = Runtime();
            Register(runtime, SecretPolicy());
            Grant(runtime, "grant.inspect", "person.listener", new[] { InformationAccessMode.Inspect }, InformationSourceVisibilityPolicy.PrivilegedOnly, permitsResharing: false);
            InformationAccessSaveData valid = runtime.CreateSaveData();
            InformationAccessSnapshot before = runtime.CreateSnapshot();

            InformationAccessSaveData corrupt = runtime.CreateSaveData();
            corrupt.policies = corrupt.policies.Concat(corrupt.policies.Take(1).Select(policy => policy.Clone())).ToArray();
            InformationAccessOperationResult rejected = runtime.RestoreFromSaveData(corrupt, Registry(), OwnerId, restoring: true);

            InformationAccessRuntime restored = Runtime();
            InformationAccessOperationResult accepted = restored.RestoreFromSaveData(valid, Registry(), OwnerId, restoring: true);
            InformationAccessSnapshot after = runtime.CreateSnapshot();

            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(rejected.Code, Is.EqualTo(InformationAccessResultCode.RestoreFailed));
            Assert.That(after.Revision, Is.EqualTo(before.Revision));
            Assert.That(after.Policies.Select(policy => policy.PolicyId), Is.EqualTo(before.Policies.Select(policy => policy.PolicyId)));
            Assert.That(after.Grants.Select(grant => grant.GrantId), Is.EqualTo(before.Grants.Select(grant => grant.GrantId)));
            Assert.That(accepted.Succeeded, Is.True);
            Assert.That(restored.CreateSnapshot().Policies.Count, Is.EqualTo(valid.policies.Length));
        }

        [Test]
        public void SnapshotsDecisionsAndProjectionsAreImmutableBoundaries()
        {
            InformationAccessRuntime runtime = Runtime();
            Register(runtime, SecretPolicy());
            Grant(runtime, "grant.inspect", "person.listener", new[] { InformationAccessMode.Inspect }, InformationSourceVisibilityPolicy.PrivilegedOnly, permitsResharing: false);
            InformationAccessSnapshot snapshot = runtime.CreateSnapshot();
            InformationAccessDecision decision = runtime.EvaluateAccess(Context(SecretPolicyId, SecretSubjectId, InformationSubjectType.Memory, "person.listener", InformationAccessMode.Inspect, discovered: true));
            RedactedInformationProjection projection = runtime.Project(Context(SecretPolicyId, SecretSubjectId, InformationSubjectType.Memory, "person.listener", InformationAccessMode.Inspect, discovered: true), new[] { "detail.summary", "detail.source", "detail.body" });

            Assert.That(snapshot.Policies, Is.AssignableTo<IReadOnlyList<InformationAccessPolicyRecord>>());
            Assert.Throws<NotSupportedException>(() => ((IList<InformationAccessPolicyRecord>)snapshot.Policies).Add(new InformationAccessPolicyRecord(SecretPolicy())));
            Assert.Throws<NotSupportedException>(() => ((IList<string>)decision.AllowedDetails).Add("detail.injected"));
            Assert.Throws<NotSupportedException>(() => ((IDictionary<string, InformationRedactionState>)projection.Details).Add("detail.injected", InformationRedactionState.Visible));

            snapshot.Policies.Single().Data.policyId = "mutated";
            projection.Details.TryGetValue("detail.summary", out InformationRedactionState state);

            Assert.That(runtime.CreateSnapshot().Policies.Single().PolicyId, Is.EqualTo(SecretPolicyId));
            Assert.That(state, Is.EqualTo(InformationRedactionState.Visible));
        }

        [Test]
        public void PersistenceParticipantPrepareCommitRestoresAccessStateSilently()
        {
            InformationAccessRuntime runtime = Runtime();
            Register(runtime, SecretPolicy());
            Grant(runtime, "grant.inspect", "person.listener", new[] { InformationAccessMode.Inspect }, InformationSourceVisibilityPolicy.PrivilegedOnly, permitsResharing: false);
            InformationAccessPersistenceParticipant participant = new InformationAccessPersistenceParticipant(runtime, Registry, OwnerId);
            PersistenceParticipantSaveResult captured = participant.CapturePayload();

            InformationAccessRuntime restored = Runtime();
            InformationAccessPersistenceParticipant restoredParticipant = new InformationAccessPersistenceParticipant(restored, Registry, OwnerId);
            PersistenceParticipantPrepareResult prepared = restoredParticipant.PreparePayload(captured.PayloadJson, InformationAccessPersistenceParticipant.CurrentParticipantSchemaVersion);
            PersistenceParticipantCommitResult committed = restoredParticipant.CommitPreparedPayload(prepared.PreparedPayload);

            Assert.That(captured.Succeeded, Is.True, captured.Message);
            Assert.That(prepared.Succeeded, Is.True, prepared.Message);
            Assert.That(committed.Succeeded, Is.True, committed.Message);
            Assert.That(restored.CreateSnapshot().Grants.Single().GrantId, Is.EqualTo("grant.inspect"));
        }

        [Test]
        public void TransferRuntimeConsultsAccessPolicyWhenProvided()
        {
            InformationAccessRuntime access = Runtime();
            Register(access, SecretPolicy());
            InformationTransferRuntime transfers = new InformationTransferRuntime();
            transfers.Configure(Registry(), OwnerId);
            InformationTransferRequest deniedRequest = TransferRequest(access, "tx.transfer.denied");

            InformationTransferResult denied = transfers.ExecuteTransfer(deniedRequest);
            Grant(access, "grant.share", "person.sender", new[] { InformationAccessMode.Share }, InformationSourceVisibilityPolicy.PrivilegedOnly, permitsResharing: false);
            InformationTransferResult allowed = transfers.ExecuteTransfer(TransferRequest(access, "tx.transfer.allowed"));

            Assert.That(denied.Succeeded, Is.False);
            Assert.That(denied.Status, Is.EqualTo(InformationTransferStatus.PrivacyBlocked));
            Assert.That(allowed.Succeeded, Is.True, allowed.Message);
            Assert.That(transfers.CreateSnapshot().Transfers.Count, Is.EqualTo(1));
        }

        [Test]
        public void NonTransferProjectionAdaptersRespectAccessWithoutMutatingOwners()
        {
            using AccessFixture fixture = AccessFixture.Create();
            fixture.RegisterAllPolicies();
            long historyRevision = fixture.History.HistoryRevision;
            long memoryRevision = fixture.Memory.MemoryRevision;
            long knowledgeRevision = fixture.Knowledge.KnowledgeRevision;
            long sourceRevision = fixture.Sources.SourceRevision;

            InformationAccessContext visitor = fixture.Context("person.access.visitor", InformationAccessMode.Query);
            InformationAccessProjection<HistoricalEventRecord> deniedHistory = fixture.History.GetHistoryProjection(fixture.HistoryEventId, fixture.Access, visitor, fixture.HistoryPolicyId);
            IReadOnlyList<InformationAccessProjection<BiographyTimelineEntry>> deniedBiography = fixture.History.GetBiographyProjection(fixture.PersonId, fixture.Access, visitor, fixture.Memory, policyId: fixture.LifePolicyId);
            InformationAccessProjection<HistoryMemoryRecord> deniedMemory = fixture.Memory.GetMemoryProjection(fixture.MemoryId, fixture.Access, visitor, fixture.MemoryPolicyId);
            InformationAccessProjection<KnowledgeBeliefRecord> deniedKnowledge = fixture.Knowledge.GetKnowledgeProjection(fixture.Proposition, fixture.Access, visitor, fixture.KnowledgePolicyId);
            InformationAccessProjection<InformationSourceRecord> deniedSource = fixture.Sources.GetSourceProjection(fixture.SourceId, fixture.Access, visitor, fixture.SourcePolicyId);

            fixture.GrantAll("person.access.listener", revealSource: false);
            InformationAccessContext listener = fixture.Context("person.access.listener", InformationAccessMode.Query);
            InformationAccessProjection<HistoricalEventRecord> history = fixture.History.GetHistoryProjection(fixture.HistoryEventId, fixture.Access, listener, fixture.HistoryPolicyId);
            IReadOnlyList<InformationAccessProjection<BiographyTimelineEntry>> biography = fixture.History.GetBiographyProjection(fixture.PersonId, fixture.Access, listener, fixture.Memory, policyId: fixture.LifePolicyId);
            InformationAccessProjection<HistoryMemoryRecord> memory = fixture.Memory.GetMemoryProjection(fixture.MemoryId, fixture.Access, listener, fixture.MemoryPolicyId);
            InformationAccessProjection<KnowledgeBeliefRecord> knowledge = fixture.Knowledge.GetKnowledgeProjection(fixture.Proposition, fixture.Access, listener, fixture.KnowledgePolicyId);
            InformationAccessProjection<InformationSourceRecord> source = fixture.Sources.GetSourceProjection(fixture.SourceId, fixture.Access, listener, fixture.SourcePolicyId);
            InformationAccessProjection<SourceChainSnapshot> chain = fixture.Sources.GetSourceChainProjection(fixture.SourceId, fixture.Access, listener, fixture.SourceChainPolicyId);

            Assert.That(deniedHistory.Record, Is.Null);
            Assert.That(deniedBiography.Count, Is.EqualTo(0));
            Assert.That(deniedMemory.Record, Is.Null);
            Assert.That(deniedKnowledge.Record, Is.Null);
            Assert.That(deniedSource.Record, Is.Null);
            Assert.That(history.Record, Is.Not.Null);
            Assert.That(history.Record.PrimaryPersonId, Is.Empty);
            Assert.That(biography.Count, Is.EqualTo(1));
            Assert.That(memory.Record, Is.Not.Null);
            Assert.That(memory.Record.EvidenceIds, Is.Empty);
            Assert.That(knowledge.Record, Is.Not.Null);
            Assert.That(knowledge.Record.SupportingEvidenceIds, Is.Empty);
            Assert.That(source.Record, Is.Not.Null);
            Assert.That(source.Record.Data.originalCreatorPersonId, Is.Empty);
            Assert.That(chain.Record, Is.Not.Null);
            Assert.That(chain.Record.OriginalHidden, Is.True);
            Assert.That(fixture.History.HistoryRevision, Is.EqualTo(historyRevision));
            Assert.That(fixture.Memory.MemoryRevision, Is.EqualTo(memoryRevision));
            Assert.That(fixture.Knowledge.KnowledgeRevision, Is.EqualTo(knowledgeRevision));
            Assert.That(fixture.Sources.SourceRevision, Is.EqualTo(sourceRevision));
        }

        private const string OwnerId = "person.access.owner";
        private const string PublicPolicyId = "information-access.test.public";
        private const string SecretPolicyId = "information-access.test.secret";
        private const string DiscoveryPolicyId = "information-access.test.discovery";
        private const string PublicSubjectId = "fact.access.public";
        private const string SecretSubjectId = "memory.access.secret";
        private const string DiscoverySubjectId = "event.access.discovery";

        private static DefinitionRegistry Registry()
        {
            return KnowledgeTestDefinitionFactory.AddSourceDefinitions(
                KnowledgeTestDefinitionFactory.TransferDefinition("information-transfer.test.direct", InformationTransferMode.DirectTestimony));
        }

        private static InformationAccessRuntime Runtime()
        {
            InformationAccessRuntime runtime = new InformationAccessRuntime();
            runtime.Configure(Registry(), OwnerId);
            return runtime;
        }

        private static void Register(InformationAccessRuntime runtime, InformationAccessPolicyData policy)
        {
            InformationAccessOperationResult result = runtime.RegisterPolicy(policy, $"tx.policy.{policy.policyId}");
            Assert.That(result.Succeeded, Is.True, result.Message);
        }

        private static void Grant(InformationAccessRuntime runtime, string grantId, string personId, InformationAccessMode[] modes, InformationSourceVisibilityPolicy sourceVisibility, bool permitsResharing)
        {
            InformationAccessOperationResult result = runtime.GrantAccess(new InformationAccessGrantData
            {
                grantId = grantId,
                policyId = SecretPolicyId,
                subject = Subject(InformationSubjectType.Memory, SecretSubjectId, OwnerId),
                granteeKind = InformationGranteeKind.Person,
                granteeId = personId,
                grantorId = OwnerId,
                accessModes = modes,
                detailIds = new[] { "detail.summary" },
                sourceVisibility = sourceVisibility,
                permitsDisclosure = true,
                permitsResharing = permitsResharing
            }, $"tx.{grantId}");
            Assert.That(result.Succeeded, Is.True, result.Message);
        }

        private static InformationAccessPolicyData PublicPolicy()
        {
            return new InformationAccessPolicyData
            {
                policyId = PublicPolicyId,
                subject = Subject(InformationSubjectType.FactInstance, PublicSubjectId),
                classification = InformationVisibilityClassification.Public,
                disclosurePolicy = InformationDisclosurePolicy.FreelyDisclose,
                resharingPolicy = InformationResharingPolicy.FreelyReshareable,
                sourceVisibilityPolicy = InformationSourceVisibilityPolicy.Reveal,
                detailVisibilityPolicy = InformationDetailVisibilityPolicy.All
            };
        }

        private static InformationAccessPolicyData SecretPolicy()
        {
            return new InformationAccessPolicyData
            {
                policyId = SecretPolicyId,
                subject = Subject(InformationSubjectType.Memory, SecretSubjectId, OwnerId),
                classification = InformationVisibilityClassification.Secret,
                disclosurePolicy = InformationDisclosurePolicy.RedactedOnly,
                resharingPolicy = InformationResharingPolicy.NoResharing,
                sourceVisibilityPolicy = InformationSourceVisibilityPolicy.HideOriginal,
                detailVisibilityPolicy = InformationDetailVisibilityPolicy.Selected,
                auditPolicy = InformationAuditPolicy.AuditDeniedAndGranted,
                defaultVisibleDetails = new[] { "detail.summary" },
                defaultRedactedDetails = new[] { "detail.source" },
                defaultHiddenDetails = new[] { "detail.body" },
                needToKnowTags = new[] { "need.secret" }
            };
        }

        private static InformationAccessPolicyData ProjectionPolicy(string policyId, InformationSubjectType type, string subjectId, string owner)
        {
            return new InformationAccessPolicyData
            {
                policyId = policyId,
                subject = Subject(type, subjectId, owner),
                classification = InformationVisibilityClassification.Secret,
                disclosurePolicy = InformationDisclosurePolicy.RedactedOnly,
                resharingPolicy = InformationResharingPolicy.NoResharing,
                sourceVisibilityPolicy = InformationSourceVisibilityPolicy.HideFullProvenance,
                detailVisibilityPolicy = InformationDetailVisibilityPolicy.Selected,
                defaultVisibleDetails = new[] { "detail.summary", "detail.event", "detail.memory", "detail.belief", "detail.source" },
                defaultRedactedDetails = new[] { "detail.primary-person", "detail.evidence", "detail.sources", "detail.creator", "detail.original-source" },
                defaultHiddenDetails = new[] { "detail.participants", "detail.provenance", "detail.suppressions", "detail.revisions", "detail.source-identity" },
                auditPolicy = InformationAuditPolicy.None
            };
        }

        private static InformationAccessPolicyData DiscoveryPolicy()
        {
            return new InformationAccessPolicyData
            {
                policyId = DiscoveryPolicyId,
                subject = Subject(InformationSubjectType.HistoricalEvent, DiscoverySubjectId),
                classification = InformationVisibilityClassification.Public,
                disclosurePolicy = InformationDisclosurePolicy.SameAsAccess,
                resharingPolicy = InformationResharingPolicy.FreelyReshareable,
                sourceVisibilityPolicy = InformationSourceVisibilityPolicy.Reveal,
                detailVisibilityPolicy = InformationDetailVisibilityPolicy.ExistenceOnly,
                discoveryRequired = true,
                defaultVisibleDetails = new[] { "detail.summary" }
            };
        }

        private static InformationAccessContext Context(string policyId, string subjectId, InformationSubjectType type, string requester, InformationAccessMode mode, bool discovered, bool revealDenial = false, string[] authorizationIds = null)
        {
            return ContextAt(policyId, subjectId, type, requester, mode, 10d, discovered, revealDenial, authorizationIds);
        }

        private static InformationAccessContext ContextAt(string policyId, string subjectId, InformationSubjectType type, string requester, InformationAccessMode mode, double worldTimeSeconds, bool discovered = true, bool revealDenial = false, string[] authorizationIds = null)
        {
            return new InformationAccessContext
            {
                RequestingPersonId = requester,
                ActingEntityId = requester,
                Subject = Subject(type, subjectId, type == InformationSubjectType.Memory ? OwnerId : string.Empty),
                AccessMode = mode,
                Purpose = mode == InformationAccessMode.Share || mode == InformationAccessMode.Reshare ? InformationAccessPurpose.Transfer : InformationAccessPurpose.Gameplay,
                RequestedDetailIds = new[] { "detail.summary", "detail.source", "detail.body" },
                HasDiscoveredSubject = discovered,
                RevealDenialReasons = revealDenial,
                AuthorizationIds = authorizationIds ?? Array.Empty<string>(),
                DeterministicPolicyId = policyId,
                WorldTimeSeconds = worldTimeSeconds
            };
        }

        private static InformationSubjectReferenceData Subject(InformationSubjectType type, string subjectId, string owner = "")
        {
            return new InformationSubjectReferenceData
            {
                subjectType = type,
                subjectId = subjectId,
                ownerPersonId = owner,
                parentSubjectId = "test.parent"
            };
        }

        private static InformationAccessPolicyDefinition PolicyDefinition(string id, InformationSubjectType subjectType, InformationVisibilityClassification classification, InformationDisclosurePolicy disclosure, InformationResharingPolicy resharing, InformationSourceVisibilityPolicy sourceVisibility, InformationDetailVisibilityPolicy detailVisibility, InformationAuditPolicy audit, string[] visible, string[] redacted, string[] hidden)
        {
            InformationAccessPolicyDefinition definition = ScriptableObject.CreateInstance<InformationAccessPolicyDefinition>();
            definition.DevelopmentConfigure(id, id, subjectType, classification, disclosure, resharing, sourceVisibility, detailVisibility, audit, visible, redacted, hidden);
            return definition;
        }

        private static DefinitionValidationReport Validate(InformationAccessPolicyDefinition definition)
        {
            DefinitionValidationReport report = new DefinitionValidationReport();
            definition.ValidateCatalogDefinition(new Dictionary<string, IGameDefinition> { [definition.Id] = definition }, report);
            return report;
        }

        private static InformationTransferRequest TransferRequest(InformationAccessRuntime access, string transactionId)
        {
            return new InformationTransferRequest
            {
                TransactionId = transactionId,
                TransferId = transactionId.Replace("tx.", "transfer."),
                SenderPersonId = "person.sender",
                RecipientPersonIds = new[] { "person.recipient" },
                Mode = InformationTransferMode.DirectTestimony,
                TransferDefinitionId = "information-transfer.test.direct",
                PrivacyScope = TransferPrivacyScope.Private,
                DeliberateFalsehoodAuthorized = true,
                AccessRuntime = access,
                AccessPolicyId = SecretPolicyId,
                AccessSubject = Subject(InformationSubjectType.Memory, SecretSubjectId, OwnerId),
                ContentItems = new[]
                {
                    new TransferContentItemData
                    {
                        contentItemId = "content.secret-memory",
                        contentType = InformationTransferContentType.MemoryStatement,
                        senderMemoryId = SecretSubjectId,
                        privacyClassification = KnowledgeVisibility.Secret,
                        requiredRecipientAccessId = SecretPolicyId,
                        includedDetailIds = new[] { "detail.summary" },
                        deliberateFalsehood = true,
                        rawEvidenceStrength = 500
                    }
                }
            };
        }

        private sealed class AccessFixture : IDisposable
        {
            private readonly GameObject gameObject;

            private AccessFixture(GameObject gameObject, DefinitionRegistry registry, AuthoritativeHistoryRuntime history, PersonMemoryRuntime memory, PersonKnowledgeRuntime knowledge, InformationSourceRuntime sources, InformationAccessRuntime access)
            {
                this.gameObject = gameObject;
                Registry = registry;
                History = history;
                Memory = memory;
                Knowledge = knowledge;
                Sources = sources;
                Access = access;
            }

            public string PersonId => "person.access.owner";
            public string BodyId => "body.access.owner";
            public string HistoryEventId => "event.access.secret";
            public string LifeEventId => "event.access.life.secret";
            public string MemoryId => "memory.access.secret.runtime";
            public string SourceId => "information-source.access.secret";
            public string HistoryPolicyId => "information-access.test.history";
            public string LifePolicyId => "information-access.test.life";
            public string MemoryPolicyId => "information-access.test.memory";
            public string KnowledgePolicyId => "information-access.test.knowledge";
            public string SourcePolicyId => "information-access.test.source";
            public string SourceChainPolicyId => "information-access.test.source-chain";
            public DefinitionRegistry Registry { get; }
            public AuthoritativeHistoryRuntime History { get; }
            public PersonMemoryRuntime Memory { get; }
            public PersonKnowledgeRuntime Knowledge { get; }
            public InformationSourceRuntime Sources { get; }
            public InformationAccessRuntime Access { get; }
            public KnowledgePropositionData Proposition => new KnowledgePropositionData
            {
                factDefinitionId = BuiltInKnowledgeFacts.EventOccurred,
                subjectType = KnowledgeSubjectType.Event,
                subjectId = HistoryEventId,
                valueType = KnowledgeValueType.Boolean,
                booleanValue = true
            };

            public static AccessFixture Create()
            {
                DefinitionRegistry registry = KnowledgeTestDefinitionFactory.AddSourceDefinitions(new IGameDefinition[]
                {
                    Fact(BuiltInKnowledgeFacts.EventOccurred, "Event Occurred", KnowledgeDomain.Historical, KnowledgePropositionType.Event, KnowledgeSubjectType.Event, KnowledgeValueType.Boolean),
                    EventDefinition("history-event.access.secret", HistoricalEventCategory.Discovery, KnowledgeVisibility.Hidden, HistoricalEventPayloadKind.Generic),
                    LifeEventDefinition("history-event.access.life", LifeEventCategory.Discovery, LifeEventPayloadKind.Discovery, LifeEventSignificance.Notable, LifeEventBiographyRelevance.MajorBiographyEvent, LifeEventPublicRecordRelevance.PersonalOnly, LifeEventParticipantRole.Subject)
                });
                AuthoritativeHistoryRuntime history = new AuthoritativeHistoryRuntime();
                history.Configure(registry, "world.access.test", new[] { "person.access.owner" }, new[] { "body.access.owner" });
                PersonMemoryRuntime memory = new PersonMemoryRuntime();
                memory.Configure("person.access.owner", registry, history, new[] { "person.access.owner" });
                GameObject gameObject = new GameObject("Information Access Projection Tests");
                PersonKnowledgeRuntime knowledge = gameObject.AddComponent<PersonKnowledgeRuntime>();
                knowledge.Configure(registry, "person.access.owner", "actor.access.owner", "body.access.owner");
                InformationSourceRuntime sources = new InformationSourceRuntime();
                sources.Configure(registry, "person.access.owner");
                InformationAccessRuntime access = new InformationAccessRuntime();
                access.Configure(registry, "person.access.owner");
                AccessFixture fixture = new AccessFixture(gameObject, registry, history, memory, knowledge, sources, access);
                fixture.Seed();
                return fixture;
            }

            public InformationAccessContext Context(string requester, InformationAccessMode mode)
            {
                return new InformationAccessContext
                {
                    RequestingPersonId = requester,
                    ActingEntityId = requester,
                    AccessMode = mode,
                    Purpose = InformationAccessPurpose.Debug,
                    WorldTimeSeconds = 12d,
                    HasDiscoveredSubject = true,
                    RedactedAccessAcceptable = true
                };
            }

            public void RegisterAllPolicies()
            {
                Register(Access, ProjectionPolicy(HistoryPolicyId, InformationSubjectType.HistoricalEvent, HistoryEventId, PersonId));
                Register(Access, ProjectionPolicy(LifePolicyId, InformationSubjectType.LifeEvent, LifeEventId, PersonId));
                Register(Access, ProjectionPolicy(MemoryPolicyId, InformationSubjectType.Memory, MemoryId, PersonId));
                Register(Access, ProjectionPolicy(KnowledgePolicyId, InformationSubjectType.Belief, Knowledge.TryGetBelief(Proposition, out KnowledgeBeliefRecord belief) ? belief.BeliefId : string.Empty, PersonId));
                Register(Access, ProjectionPolicy(SourcePolicyId, InformationSubjectType.Source, SourceId, PersonId));
                Register(Access, ProjectionPolicy(SourceChainPolicyId, InformationSubjectType.SourceChain, SourceId, PersonId));
            }

            public void GrantAll(string personId, bool revealSource)
            {
                foreach (string policyId in new[] { HistoryPolicyId, LifePolicyId, MemoryPolicyId, KnowledgePolicyId, SourcePolicyId, SourceChainPolicyId })
                {
                    InformationAccessOperationResult result = Access.GrantAccess(new InformationAccessGrantData
                    {
                        grantId = $"grant.{policyId}.{personId}",
                        policyId = policyId,
                        granteeKind = InformationGranteeKind.Person,
                        granteeId = personId,
                        grantorId = PersonId,
                        accessModes = new[] { InformationAccessMode.Query, InformationAccessMode.Inspect, InformationAccessMode.RevealProvenance },
                        detailIds = new[] { "detail.summary", "detail.event", "detail.memory", "detail.belief", "detail.source" },
                        sourceVisibility = revealSource ? InformationSourceVisibilityPolicy.Reveal : InformationSourceVisibilityPolicy.PrivilegedOnly,
                        permitsDisclosure = false,
                        permitsResharing = false
                    }, $"tx.{policyId}.grant");
                    Assert.That(result.Succeeded, Is.True, result.Message);
                }
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }

            private void Seed()
            {
                HistoryOperationResult eventResult = History.RecordEvent(new RecordHistoricalEventRequest
                {
                    TransactionId = "tx.history.secret",
                    EventId = HistoryEventId,
                    EventDefinitionId = "history-event.access.secret",
                    OccurredAtWorldTime = 1d,
                    RecordedAtWorldTime = 1d,
                    PrimaryPersonId = PersonId,
                    ParticipantPersonIds = new[] { PersonId },
                    BodyIds = new[] { BodyId },
                    Visibility = KnowledgeVisibility.Hidden,
                    SourceSystem = "EditModeTest",
                    Provenance = "Secret projection test",
                    Payload = new HistoricalEventPayloadData { kind = HistoricalEventPayloadKind.Generic, note = "secret" },
                    Tags = new[] { "projection-test" }
                });
                HistoryOperationResult lifeResult = History.RecordLifeEvent(new RecordLifeEventRequest
                {
                    TransactionId = "tx.life.secret",
                    EventId = LifeEventId,
                    EventDefinitionId = "history-event.access.life",
                    Category = LifeEventCategory.Discovery,
                    PayloadKind = LifeEventPayloadKind.Discovery,
                    OccurredAtWorldTime = 2d,
                    RecordedAtWorldTime = 2d,
                    PrimaryPersonId = PersonId,
                    Participants = new[] { new LifeEventParticipantData { personId = PersonId, role = LifeEventParticipantRole.Subject, bodyId = BodyId } },
                    BodyIds = new[] { BodyId },
                    Visibility = KnowledgeVisibility.Hidden,
                    Outcome = LifeEventOutcome.Confirmed,
                    Significance = LifeEventSignificance.Notable,
                    BiographyRelevance = LifeEventBiographyRelevance.MajorBiographyEvent,
                    SourceSystem = "EditModeTest",
                    Provenance = "Secret biography test",
                    LifeEventPayload = new LifeEventPayloadData { kind = LifeEventPayloadKind.Discovery, subjectPersonId = PersonId, note = "secret-life" },
                    Tags = new[] { "projection-test" }
                });
                HistoryOperationResult memoryResult = Memory.FormMemory(new FormMemoryRequest
                {
                    TransactionId = "tx.memory.secret",
                    MemoryId = MemoryId,
                    OwnerPersonId = PersonId,
                    HistoricalEventId = HistoryEventId,
                    Source = HistoryMemorySource.DirectObservation,
                    FormedAtWorldTime = 3d,
                    RememberedOccurredAtWorldTime = 1d,
                    Confidence = 800,
                    Clarity = 800,
                    Salience = 800,
                    FirstHand = true,
                    BodyAtTimeId = BodyId,
                    Visibility = KnowledgeVisibility.Hidden,
                    EvidenceIds = new[] { "evidence.secret" },
                    Tags = new[] { "projection-test" }
                });
                KnowledgeOperationResult knowledgeResult = Knowledge.RecordObservation(new KnowledgeObservationRequest
                {
                    PersonId = PersonId,
                    TransactionId = "tx.knowledge.secret",
                    Proposition = Proposition,
                    Strength = 900,
                    Credibility = 900,
                    Visibility = KnowledgeVisibility.Hidden,
                    PrivateAccessAuthorized = true,
                    EvidenceId = "evidence.knowledge.secret",
                    SourceId = SourceId
                });
                InformationSourceOperationResult sourceResult = Sources.RegisterSource(new InformationSourceRegistrationRequest
                {
                    TransactionId = "tx.source.secret",
                    SourceInstanceId = SourceId,
                    Category = InformationSourceCategory.DirectObservation,
                    ReferenceType = InformationSourceReferenceType.HistoricalEvent,
                    ReferencedId = HistoryEventId,
                    OriginalCreatorPersonId = PersonId,
                    ObserverPersonId = PersonId,
                    HolderPersonId = PersonId,
                    Privacy = SourcePrivacyLevel.Hidden,
                    Domain = KnowledgeDomain.Historical,
                    SubjectId = HistoryEventId
                });

                Assert.That(eventResult.Succeeded, Is.True, eventResult.Message);
                Assert.That(lifeResult.Succeeded, Is.True, lifeResult.Message);
                Assert.That(memoryResult.Succeeded, Is.True, memoryResult.Message);
                Assert.That(knowledgeResult.Succeeded, Is.True, knowledgeResult.Message);
                Assert.That(sourceResult.Succeeded, Is.True, sourceResult.Message);
            }

            private static HistoricalEventDefinition EventDefinition(string id, HistoricalEventCategory category, KnowledgeVisibility visibility, HistoricalEventPayloadKind payloadKind)
            {
                HistoricalEventDefinition definition = ScriptableObject.CreateInstance<HistoricalEventDefinition>();
                definition.name = id;
                Set(definition, "eventDefinitionId", id);
                Set(definition, "displayName", id);
                Set(definition, "category", category);
                Set(definition, "defaultVisibility", visibility);
                Set(definition, "payloadKind", payloadKind);
                return definition;
            }

            private static HistoricalEventDefinition LifeEventDefinition(string id, LifeEventCategory category, LifeEventPayloadKind payloadKind, LifeEventSignificance significance, LifeEventBiographyRelevance biography, LifeEventPublicRecordRelevance publicRecord, LifeEventParticipantRole requiredRole)
            {
                HistoricalEventDefinition definition = EventDefinition(id, HistoricalEventCategory.CustomWorldEvent, KnowledgeVisibility.Private, HistoricalEventPayloadKind.Generic);
                Set(definition, "lifeEventDefinition", true);
                Set(definition, "lifeEventCategory", category);
                Set(definition, "lifeEventPayloadKind", payloadKind);
                Set(definition, "defaultSignificance", significance);
                Set(definition, "defaultBiographyRelevance", biography);
                Set(definition, "defaultPublicRecordRelevance", publicRecord);
                Set(definition, "requiredParticipantRoles", new[] { requiredRole });
                Set(definition, "optionalParticipantRoles", new[] { LifeEventParticipantRole.Witness });
                Set(definition, "mayBePrivate", true);
                Set(definition, "mayBeSecret", true);
                return definition;
            }

            private static KnowledgeFactDefinition Fact(string id, string displayName, KnowledgeDomain domain, KnowledgePropositionType propositionType, KnowledgeSubjectType subjectType, KnowledgeValueType valueType)
            {
                KnowledgeFactDefinition definition = ScriptableObject.CreateInstance<KnowledgeFactDefinition>();
                definition.name = displayName;
                Set(definition, "factId", id);
                Set(definition, "displayName", displayName);
                Set(definition, "domain", domain);
                Set(definition, "propositionType", propositionType);
                Set(definition, "subjectType", subjectType);
                Set(definition, "valueType", valueType);
                Set(definition, "certaintyThreshold", 700);
                Set(definition, "requiredEvidenceCount", 1);
                return definition;
            }

            private static void Set(object target, string fieldName, object value)
            {
                FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null, $"Missing field {fieldName} on {target.GetType().Name}");
                field.SetValue(target, value);
            }
        }
    }
}
