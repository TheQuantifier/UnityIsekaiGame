#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Organizations;
using UnityIsekaiGame.Persistence;

namespace UnityIsekaiGame.Tests
{
    public sealed class OrganizationMembershipRankOfficeTests
    {
        private const string CatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";
        private const string PersonId = PersistenceService.LocalPlayerId;
        private static readonly string[] KnownPersons =
        {
            PersonId,
            "person.prototype.friend",
            "person.prototype.rival",
            "person.prototype.student",
            "person.prototype.mentor"
        };

        [Test]
        public void PrototypeMembershipRankAndOfficeDefinitionsValidate()
        {
            DefinitionRegistry registry = CreateRegistry();
            DefinitionValidationReport report = new DefinitionValidationReport();
            foreach (IGameDefinition definition in PrototypeOrganizationMembershipDefinitionFactory.CreateMissingMembershipDefinitions(Array.Empty<string>())
                .Cast<IGameDefinition>()
                .Concat(PrototypeOrganizationMembershipDefinitionFactory.CreateMissingRankTrackDefinitions(Array.Empty<string>()))
                .Concat(PrototypeOrganizationMembershipDefinitionFactory.CreateMissingRankDefinitions(Array.Empty<string>()))
                .Concat(PrototypeOrganizationMembershipDefinitionFactory.CreateMissingOfficeDefinitions(Array.Empty<string>())))
            {
                Assert.That(definition, Is.InstanceOf<IDefinitionCatalogValidationParticipant>());
                ((IDefinitionCatalogValidationParticipant)definition).ValidateCatalogDefinition(registry.DefinitionsById, report);
            }

            Assert.That(registry.TryGet(PrototypeOrganizationMembershipDefinitionFactory.GuildFullMemberId, out OrganizationMembershipDefinition member), Is.True);
            Assert.That(member.SupportsRanks, Is.True);
            Assert.That(member.SupportsOffices, Is.True);
            Assert.That(registry.TryGet(PrototypeOrganizationMembershipDefinitionFactory.GuildmasterOfficeId, out OrganizationOfficeDefinition office), Is.True);
            Assert.That(office.RequiredRankDefinitionIds, Does.Contain(PrototypeOrganizationMembershipDefinitionFactory.GuildSSSRankId));
            Assert.That(registry.TryGet(PrototypeOrganizationMembershipDefinitionFactory.AdventurerGuildEntryRankId, out OrganizationRankDefinition entryRank), Is.True);
            Assert.That(registry.TryGet(PrototypeOrganizationMembershipDefinitionFactory.GuildFRankId, out OrganizationRankDefinition fRank), Is.True);
            Assert.That(entryRank.DisplayName, Is.EqualTo("G Rank"));
            Assert.That(entryRank.RankOrder, Is.LessThan(fRank.RankOrder));
            Assert.That(entryRank.AppliesToOrganization(PrototypeInstitutionalContentIds.AdventurersGuild), Is.True);
            Assert.That(entryRank.AppliesToOrganization(PrototypeInstitutionalContentIds.MerchantGuild), Is.False);
            Assert.That(PrototypeOrganizationMembershipDefinitionFactory.GuildRatingRankIds.Count, Is.EqualTo(9));
            Assert.That(PrototypeOrganizationMembershipDefinitionFactory.GuildRatingRankIds.First(), Is.EqualTo(PrototypeOrganizationMembershipDefinitionFactory.GuildFRankId));
            Assert.That(PrototypeOrganizationMembershipDefinitionFactory.GuildRatingRankIds.Last(), Is.EqualTo(PrototypeOrganizationMembershipDefinitionFactory.GuildSSSRankId));
            Assert.That(report.ErrorCount, Is.EqualTo(0), report.ToString());
        }

        [Test]
        public void MembershipLifecycleRequiresConsentAndPreservesImmutableSnapshots()
        {
            OrganizationMembershipRuntime runtime = CreateMembershipRuntime();
            OrganizationMembershipOperationResult invited = runtime.ApplyMembership(MembershipRequest(
                "organization-membership.test.invited",
                "organization.prototype.adventurers-guild",
                "person.prototype.friend",
                PrototypeOrganizationMembershipDefinitionFactory.GuildInviteeId,
                OrganizationMembershipStatus.Invited,
                OrganizationMembershipSourceKind.Invitation,
                "tx.membership.invited"));
            OrganizationMembershipSnapshot invitedSnapshot = invited.Membership;
            invitedSnapshot.Data.status = OrganizationMembershipStatus.Active;
            Assert.That(runtime.TryGetMembership(invited.Membership.MembershipId, out OrganizationMembershipSnapshot preAcceptanceStored), Is.True);
            Assert.That(preAcceptanceStored.Status, Is.EqualTo(OrganizationMembershipStatus.Invited));

            OrganizationMembershipOperationResult denied = runtime.ApplyMembership(MembershipRequest(
                invited.Membership.MembershipId,
                "organization.prototype.adventurers-guild",
                "person.prototype.friend",
                PrototypeOrganizationMembershipDefinitionFactory.GuildInviteeId,
                OrganizationMembershipStatus.Active,
                OrganizationMembershipSourceKind.Invitation,
                "tx.membership.invited.denied"));
            OrganizationMembershipRequest acceptedRequest = MembershipRequest(
                invited.Membership.MembershipId,
                "organization.prototype.adventurers-guild",
                "person.prototype.friend",
                PrototypeOrganizationMembershipDefinitionFactory.GuildInviteeId,
                OrganizationMembershipStatus.Active,
                OrganizationMembershipSourceKind.Invitation,
                "tx.membership.invited.accepted");
            acceptedRequest.explicitConsent = true;
            OrganizationMembershipOperationResult accepted = runtime.ApplyMembership(acceptedRequest);
            OrganizationMembershipOperationResult duplicate = runtime.ApplyMembership(acceptedRequest);

            Assert.That(invited.Succeeded, Is.True, invited.Message);
            Assert.That(runtime.TryGetMembership(invited.Membership.MembershipId, out OrganizationMembershipSnapshot stored), Is.True);
            Assert.That(stored.Status, Is.EqualTo(OrganizationMembershipStatus.Active));
            Assert.That(denied.Status, Is.EqualTo(OrganizationMembershipOperationStatus.ConsentRequired));
            Assert.That(accepted.Succeeded, Is.True, accepted.Message);
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(runtime.MembershipCount, Is.EqualTo(1));
        }

        [Test]
        public void BranchMembershipRankAndOfficeRulesUseAuthoritativeOrganizationRuntime()
        {
            DefinitionRegistry registry = CreateRegistry();
            OrganizationRuntime organizations = CreateOrganizationRuntime(registry);
            OrganizationMembershipRuntime runtime = CreateMembershipRuntime(registry, organizations);
            organizations.CreateOrganization(new OrganizationCreateRequest
            {
                organizationId = "organization.test.branch",
                organizationDefinitionId = PrototypeOrganizationDefinitionFactory.BranchDefinitionId,
                officialName = "Branch",
                initialLifecycleState = OrganizationLifecycleState.Active,
                transactionId = "tx.organization.branch"
            });
            organizations.LinkOrganizations(new OrganizationLinkRequest
            {
                sourceOrganizationId = "organization.test.branch",
                targetOrganizationId = "organization.prototype.adventurers-guild",
                kind = OrganizationLinkKind.Parent,
                transactionId = "tx.organization.branch.parent"
            });
            runtime.Configure(registry, organizations, PersistenceService.LocalWorldId, KnownPersons, organizations.Snapshots.Select(snapshot => snapshot.OrganizationId));

            OrganizationMembershipOperationResult missingParent = runtime.ApplyMembership(MembershipRequest("organization-membership.test.branch.missing", "organization.test.branch", "person.prototype.student", PrototypeOrganizationMembershipDefinitionFactory.BranchMemberId, OrganizationMembershipStatus.Active, OrganizationMembershipSourceKind.WorldSetup, "tx.membership.branch.missing", consent: true));
            OrganizationMembershipOperationResult parent = runtime.ApplyMembership(MembershipRequest("organization-membership.test.guild.parent", "organization.prototype.adventurers-guild", "person.prototype.student", PrototypeOrganizationMembershipDefinitionFactory.GuildFullMemberId, OrganizationMembershipStatus.Active, OrganizationMembershipSourceKind.WorldSetup, "tx.membership.guild.parent", consent: true));
            OrganizationMembershipRequest branchRequest = MembershipRequest("organization-membership.test.branch", "organization.test.branch", "person.prototype.student", PrototypeOrganizationMembershipDefinitionFactory.BranchMemberId, OrganizationMembershipStatus.Active, OrganizationMembershipSourceKind.WorldSetup, "tx.membership.branch", consent: true);
            branchRequest.parentMembershipId = parent.Membership.MembershipId;
            OrganizationMembershipOperationResult branch = runtime.ApplyMembership(branchRequest);
            OrganizationMembershipOperationResult rank = runtime.AssignRank(RankRequest("organization-rank-assignment.test.f", parent.Membership.MembershipId, PrototypeOrganizationMembershipDefinitionFactory.GuildFRankId, "tx.rank.f"));
            OrganizationMembershipOperationResult office = runtime.CreateOffice(OfficeRequest("organization-office-record.test.treasurer", "organization.prototype.adventurers-guild", PrototypeOrganizationMembershipDefinitionFactory.GuildTreasurerOfficeId, "tx.office.treasurer", 2));
            OrganizationMembershipOperationResult appointment = runtime.AssignOffice(OfficeAssignmentRequest("organization-office-assignment.test.treasurer", office.Office.OfficeId, parent.Membership.MembershipId, "tx.office.treasurer.assign", acting: true));

            Assert.That(missingParent.Status, Is.EqualTo(OrganizationMembershipOperationStatus.InvalidDependency));
            Assert.That(parent.Succeeded, Is.True, parent.Message);
            Assert.That(branch.Succeeded, Is.True, branch.Message);
            Assert.That(branch.Membership.Data.parentMembershipId, Is.EqualTo(parent.Membership.MembershipId));
            Assert.That(rank.Succeeded, Is.True, rank.Message);
            Assert.That(office.Succeeded, Is.True, office.Message);
            Assert.That(appointment.Succeeded, Is.True, appointment.Message);
            Assert.That(appointment.OfficeAssignment.acting, Is.True);
        }

        [Test]
        public void PersistenceParticipantRejectsCorruptMembershipGraphWithoutMutation()
        {
            DefinitionRegistry registry = CreateRegistry();
            OrganizationRuntime organizations = CreateOrganizationRuntime(registry);
            OrganizationMembershipRuntime runtime = CreateMembershipRuntime(registry, organizations);
            runtime.ApplyMembership(MembershipRequest("organization-membership.test.persist", "organization.prototype.adventurers-guild", "person.prototype.mentor", PrototypeOrganizationMembershipDefinitionFactory.GuildFullMemberId, OrganizationMembershipStatus.Active, OrganizationMembershipSourceKind.WorldSetup, "tx.membership.persist", consent: true));
            OrganizationMembershipPersistenceParticipant participant = new OrganizationMembershipPersistenceParticipant(runtime, () => registry, () => organizations, PersistenceService.LocalWorldId, () => KnownPersons, () => organizations.Snapshots.Select(snapshot => snapshot.OrganizationId).ToArray());
            PersistenceParticipantSaveResult save = participant.CapturePayload();
            OrganizationMembershipRuntimeSaveData corrupt = JsonUtility.FromJson<OrganizationMembershipRuntimeSaveData>(save.PayloadJson);
            corrupt.memberships[0].membershipDefinitionId = "organization-membership.missing";

            PersistenceParticipantPrepareResult rejected = participant.PreparePayload(JsonUtility.ToJson(corrupt), OrganizationMembershipPersistenceParticipant.CurrentParticipantSchemaVersion);

            Assert.That(save.Succeeded, Is.True, save.Message);
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(runtime.TryGetMembership("organization-membership.test.persist", out OrganizationMembershipSnapshot live), Is.True);
            Assert.That(live.Status, Is.EqualTo(OrganizationMembershipStatus.Active));
            Assert.That(runtime.MembershipCount, Is.EqualTo(1));
        }

        [Test]
        public void ProjectionsExposeAffiliationSubjectWithoutLeakingHiddenMembership()
        {
            OrganizationMembershipRuntime runtime = CreateMembershipRuntime();
            OrganizationMembershipRequest hiddenRequest = MembershipRequest("organization-membership.test.hidden", "organization.prototype.adventurers-guild", "person.prototype.rival", PrototypeOrganizationMembershipDefinitionFactory.GuildFullMemberId, OrganizationMembershipStatus.Active, OrganizationMembershipSourceKind.WorldSetup, "tx.membership.hidden", consent: true);
            hiddenRequest.visibility = OrganizationVisibility.Hidden;
            runtime.ApplyMembership(hiddenRequest);

            OrganizationMembershipProjection denied = runtime.ProjectMembership("organization-membership.missing", "person.prototype.friend");
            OrganizationMembershipProjection concealed = runtime.ProjectMembership("organization-membership.test.hidden", "person.prototype.friend");
            OrganizationMembershipProjection privileged = runtime.ProjectMembership("organization-membership.test.hidden", "person.prototype.friend", privileged: true);

            Assert.That(denied.Access, Is.EqualTo(OrganizationMembershipProjectionAccess.Denied));
            Assert.That(concealed.Access, Is.EqualTo(OrganizationMembershipProjectionAccess.Concealed));
            Assert.That(concealed.Subject.subjectType, Is.EqualTo(InformationSubjectType.Affiliation));
            Assert.That(concealed.Snapshot, Is.Null);
            Assert.That(privileged.Access, Is.EqualTo(OrganizationMembershipProjectionAccess.Full));
            Assert.That(privileged.Snapshot.PersonId, Is.EqualTo("person.prototype.rival"));
        }

        private static OrganizationMembershipRequest MembershipRequest(string membershipId, string organizationId, string personId, string definitionId, OrganizationMembershipStatus target, OrganizationMembershipSourceKind source, string transactionId, bool consent = false)
        {
            return new OrganizationMembershipRequest
            {
                membershipId = membershipId,
                organizationId = organizationId,
                personId = personId,
                membershipDefinitionId = definitionId,
                targetStatus = target,
                sourceKind = source,
                explicitConsent = consent,
                worldTime = 10d,
                transactionId = transactionId
            };
        }

        private static OrganizationRankAssignmentRequest RankRequest(string assignmentId, string membershipId, string rankId, string transactionId)
        {
            return new OrganizationRankAssignmentRequest
            {
                rankAssignmentId = assignmentId,
                membershipId = membershipId,
                rankDefinitionId = rankId,
                worldTime = 20d,
                assignedById = PersonId,
                transactionId = transactionId
            };
        }

        private static OrganizationOfficeRequest OfficeRequest(string officeId, string organizationId, string officeDefinitionId, string transactionId, int maximumHolders = 0)
        {
            return new OrganizationOfficeRequest
            {
                officeId = officeId,
                organizationId = organizationId,
                officeDefinitionId = officeDefinitionId,
                maximumActiveHolders = maximumHolders,
                worldTime = 30d,
                transactionId = transactionId
            };
        }

        private static OrganizationOfficeAssignmentRequest OfficeAssignmentRequest(string assignmentId, string officeId, string membershipId, string transactionId, bool acting = false)
        {
            return new OrganizationOfficeAssignmentRequest
            {
                officeAssignmentId = assignmentId,
                officeId = officeId,
                membershipId = membershipId,
                acting = acting,
                worldTime = 40d,
                appointedById = PersonId,
                transactionId = transactionId
            };
        }

        private static OrganizationMembershipRuntime CreateMembershipRuntime(DefinitionRegistry registry = null, OrganizationRuntime organizations = null)
        {
            registry ??= CreateRegistry();
            organizations ??= CreateOrganizationRuntime(registry);
            OrganizationMembershipRuntime runtime = new OrganizationMembershipRuntime();
            runtime.Configure(registry, organizations, PersistenceService.LocalWorldId, KnownPersons, organizations.Snapshots.Select(snapshot => snapshot.OrganizationId));
            return runtime;
        }

        private static OrganizationRuntime CreateOrganizationRuntime(DefinitionRegistry registry)
        {
            OrganizationRuntime runtime = new OrganizationRuntime();
            PrototypeOrganizationDefinitionFactory.SeedPrototypeOrganizations(runtime, registry, PersistenceService.LocalWorldId);
            runtime.Configure(registry, PersistenceService.LocalWorldId, KnownPersons, Array.Empty<string>());
            return runtime;
        }

        private static DefinitionRegistry CreateRegistry()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            return PrototypeOrganizationMembershipDefinitionFactory.AddMissingPrototypeOrganizationMembershipDefinitions(
                PrototypeOrganizationDefinitionFactory.AddMissingPrototypeOrganizationDefinitions(catalog.CreateRegistry()));
        }
    }
}
#endif
