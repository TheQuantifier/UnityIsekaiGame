#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Organizations;
using UnityIsekaiGame.Persistence;

namespace UnityIsekaiGame.Tests
{
    public sealed class OrganizationAuthorityPermissionTests
    {
        private const string CatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";
        private const string PersonId = PersistenceService.LocalPlayerId;
        private static readonly string[] KnownPersons =
        {
            PersonId,
            "person.prototype.friend",
            "person.prototype.rival",
            "person.prototype.student",
            "person.prototype.mentor",
            "person.prototype.partner"
        };

        [Test]
        public void PrototypeAuthorityDefinitionsValidateAgainstOrganizationMemberships()
        {
            DefinitionRegistry registry = CreateRegistry();
            DefinitionValidationReport report = new DefinitionValidationReport();
            foreach (IGameDefinition definition in PrototypeOrganizationAuthorityDefinitionFactory.CreateMissingPermissionDefinitions(Array.Empty<string>())
                .Cast<IGameDefinition>()
                .Concat(PrototypeOrganizationAuthorityDefinitionFactory.CreateMissingActionDefinitions(Array.Empty<string>()))
                .Concat(PrototypeOrganizationAuthorityDefinitionFactory.CreateMissingRoleDefinitions(Array.Empty<string>()))
                .Concat(PrototypeOrganizationAuthorityDefinitionFactory.CreateMissingBindingDefinitions(Array.Empty<string>())))
            {
                Assert.That(definition, Is.InstanceOf<IDefinitionCatalogValidationParticipant>());
                ((IDefinitionCatalogValidationParticipant)definition).ValidateCatalogDefinition(registry.DefinitionsById, report);
            }

            Assert.That(report.ErrorCount, Is.EqualTo(0), report.ToString());
            Assert.That(registry.TryGet(PrototypeOrganizationAuthorityDefinitionFactory.GuildmasterRoleId, out OrganizationAuthorityRoleDefinition role), Is.True);
            Assert.That(role.GrantedPermissionIds, Does.Contain(PrototypeOrganizationAuthorityDefinitionFactory.AppointOfficeholdersPermissionId));
            Assert.That(registry.TryGet(PrototypeOrganizationAuthorityDefinitionFactory.ChangeHeadquartersActionId, out InstitutionalActionDefinition action), Is.True);
            Assert.That(action.PermissionPolicy, Is.EqualTo(OrganizationPermissionCombinationPolicy.JointApproval));
        }

        [Test]
        public void MembershipRankAndOfficeBindingsProduceEffectiveAuthorityWithoutDuplicatingMembershipState()
        {
            RuntimeFixture fixture = CreateFixture();
            CreateGuildmaster(fixture, PersonId, "master");
            fixture.Memberships.ApplyMembership(MembershipRequest("organization-membership.test.general", "organization.prototype.adventurers-guild", "person.prototype.friend", PrototypeOrganizationMembershipDefinitionFactory.GuildFullMemberId, "tx.member.general"));

            OrganizationAuthorizationResult guildmaster = fixture.Authority.EvaluateAuthorization(AuthRequest(PersonId, "organization.prototype.adventurers-guild", PrototypeOrganizationAuthorityDefinitionFactory.AppointOfficeholderActionId, "auth.guildmaster.appoint"));
            OrganizationAuthorizationResult generalMember = fixture.Authority.EvaluateAuthorization(AuthRequest("person.prototype.friend", "organization.prototype.adventurers-guild", PrototypeOrganizationAuthorityDefinitionFactory.AppointOfficeholderActionId, "auth.general.appoint"));
            OrganizationEffectiveAuthoritySnapshot effective = fixture.Authority.QueryEffectiveAuthority(PersonId, "organization.prototype.adventurers-guild", 100d);

            Assert.That(guildmaster.Succeeded, Is.True, guildmaster.Message);
            Assert.That(generalMember.Status, Is.EqualTo(OrganizationAuthorizationStatus.MissingPermission));
            Assert.That(effective.Sources.Any(source => source.permissionDefinitionId == PrototypeOrganizationAuthorityDefinitionFactory.PromoteMembersPermissionId), Is.True);
            Assert.That(fixture.Memberships.MembershipCount, Is.EqualTo(2));
        }

        [Test]
        public void DirectGrantAndDelegationAreScopedExpiringAndIdempotent()
        {
            RuntimeFixture fixture = CreateFixture();
            CreateGuildmaster(fixture, PersonId, "master");
            fixture.Memberships.ApplyMembership(MembershipRequest("organization-membership.test.friend", "organization.prototype.adventurers-guild", "person.prototype.friend", PrototypeOrganizationMembershipDefinitionFactory.GuildFullMemberId, "tx.member.friend"));

            OrganizationAuthorityOperationResult direct = fixture.Authority.CreateDirectGrant(new OrganizationAuthorityGrantRequest
            {
                grantId = "organization-authority-grant.test.friend.records",
                organizationId = "organization.prototype.adventurers-guild",
                granteePersonId = "person.prototype.friend",
                grantorPersonId = PersonId,
                permissionDefinitionIds = new[] { PrototypeOrganizationAuthorityDefinitionFactory.IssueOrdersPermissionId },
                scope = OrganizationAuthorityScopeData.ForOrganization("organization.prototype.adventurers-guild"),
                startWorldTime = 20d,
                expirationWorldTime = 40d,
                delegationPolicy = OrganizationAuthorityDelegationPolicy.DelegableNoRedelegation,
                transactionId = "tx.authority.direct"
            });
            OrganizationAuthorityOperationResult duplicate = fixture.Authority.CreateDirectGrant(new OrganizationAuthorityGrantRequest
            {
                grantId = "organization-authority-grant.test.friend.records",
                organizationId = "organization.prototype.adventurers-guild",
                granteePersonId = "person.prototype.friend",
                grantorPersonId = PersonId,
                permissionDefinitionIds = new[] { PrototypeOrganizationAuthorityDefinitionFactory.IssueOrdersPermissionId },
                scope = OrganizationAuthorityScopeData.ForOrganization("organization.prototype.adventurers-guild"),
                startWorldTime = 20d,
                expirationWorldTime = 40d,
                transactionId = "tx.authority.direct"
            });
            OrganizationAuthorizationResult authorized = fixture.Authority.EvaluateAuthorization(AuthRequest("person.prototype.friend", "organization.prototype.adventurers-guild", PrototypeOrganizationAuthorityDefinitionFactory.IssueOrderActionId, "auth.friend.orders", 30d));
            OrganizationAuthorizationResult expired = fixture.Authority.EvaluateAuthorization(AuthRequest("person.prototype.friend", "organization.prototype.adventurers-guild", PrototypeOrganizationAuthorityDefinitionFactory.IssueOrderActionId, "auth.friend.orders.expired", 50d));
            OrganizationAuthorityOperationResult delegated = fixture.Authority.DelegateAuthority(new OrganizationDelegationRequest
            {
                delegationGrantId = "organization-authority-grant.test.friend.delegate",
                organizationId = "organization.prototype.adventurers-guild",
                delegatorPersonId = "person.prototype.friend",
                recipientPersonId = "person.prototype.student",
                sourceAuthorityId = direct.Grant.GrantId,
                permissionDefinitionIds = new[] { PrototypeOrganizationAuthorityDefinitionFactory.IssueOrdersPermissionId },
                scope = OrganizationAuthorityScopeData.ForOrganization("organization.prototype.adventurers-guild"),
                startWorldTime = 25d,
                expirationWorldTime = 35d,
                transactionId = "tx.authority.delegate"
            });
            OrganizationAuthorityOperationResult redelegated = fixture.Authority.DelegateAuthority(new OrganizationDelegationRequest
            {
                delegationGrantId = "organization-authority-grant.test.student.redelegate",
                organizationId = "organization.prototype.adventurers-guild",
                delegatorPersonId = "person.prototype.student",
                recipientPersonId = "person.prototype.rival",
                sourceAuthorityId = delegated.Grant?.GrantId,
                permissionDefinitionIds = new[] { PrototypeOrganizationAuthorityDefinitionFactory.IssueOrdersPermissionId },
                scope = OrganizationAuthorityScopeData.ForOrganization("organization.prototype.adventurers-guild"),
                startWorldTime = 26d,
                expirationWorldTime = 30d,
                transactionId = "tx.authority.redelegate"
            });

            Assert.That(direct.Succeeded, Is.True, direct.Message);
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(authorized.Succeeded, Is.True, authorized.Message);
            Assert.That(expired.Status, Is.EqualTo(OrganizationAuthorizationStatus.MissingPermission));
            Assert.That(delegated.Succeeded, Is.True, delegated.Message);
            Assert.That(redelegated.Succeeded, Is.False);
            Assert.That(redelegated.Status, Is.EqualTo(OrganizationAuthorizationStatus.InvalidDependency));
        }

        [Test]
        public void BranchAuthorityRequiresExplicitBranchBindingAndDoesNotInferFromParent()
        {
            RuntimeFixture fixture = CreateFixture();
            fixture.Organizations.CreateOrganization(new OrganizationCreateRequest
            {
                organizationId = "organization.test.branch",
                organizationDefinitionId = PrototypeOrganizationDefinitionFactory.BranchDefinitionId,
                officialName = "Test Branch",
                initialLifecycleState = OrganizationLifecycleState.Active,
                transactionId = "tx.org.branch"
            });
            fixture.Organizations.LinkOrganizations(new OrganizationLinkRequest
            {
                sourceOrganizationId = "organization.test.branch",
                targetOrganizationId = "organization.prototype.adventurers-guild",
                kind = OrganizationLinkKind.Parent,
                transactionId = "tx.org.branch.parent"
            });
            fixture.Authority.Configure(fixture.Registry, fixture.Organizations, fixture.Memberships, PersistenceService.LocalWorldId, KnownPersons, fixture.Organizations.Snapshots.Select(snapshot => snapshot.OrganizationId));
            OrganizationMembershipOperationResult parentMembership = fixture.Memberships.ApplyMembership(MembershipRequest("organization-membership.test.parent.master", "organization.prototype.adventurers-guild", PersonId, PrototypeOrganizationMembershipDefinitionFactory.GuildFullMemberId, "tx.parent.master"));
            RankToSSS(fixture.Memberships, parentMembership.Membership.MembershipId, "parent");
            OrganizationMembershipOperationResult branchMembership = fixture.Memberships.ApplyMembership(MembershipRequest("organization-membership.test.branch.master", "organization.test.branch", "person.prototype.friend", PrototypeOrganizationMembershipDefinitionFactory.BranchMemberId, "tx.branch.master", parentMembership.Membership.MembershipId));
            OrganizationMembershipOperationResult branchOffice = fixture.Memberships.CreateOffice(OfficeRequest("organization-office-record.test.branch.master", "organization.test.branch", PrototypeOrganizationMembershipDefinitionFactory.BranchChapterMasterOfficeId, "tx.branch.office"));
            fixture.Memberships.AssignOffice(OfficeAssignmentRequest("organization-office-assignment.test.branch.master", branchOffice.Office.OfficeId, branchMembership.Membership.MembershipId, "tx.branch.office.assign"));

            OrganizationAuthorizationResult parentOnBranch = fixture.Authority.EvaluateAuthorization(AuthRequest(PersonId, "organization.test.branch", PrototypeOrganizationAuthorityDefinitionFactory.IssueOrderActionId, "auth.parent.branch"));
            OrganizationAuthorizationResult branchOnBranch = fixture.Authority.EvaluateAuthorization(AuthRequest("person.prototype.friend", "organization.test.branch", PrototypeOrganizationAuthorityDefinitionFactory.IssueOrderActionId, "auth.branch.branch"));
            OrganizationAuthorizationResult branchOnParent = fixture.Authority.EvaluateAuthorization(AuthRequest("person.prototype.friend", "organization.prototype.adventurers-guild", PrototypeOrganizationAuthorityDefinitionFactory.IssueOrderActionId, "auth.branch.parent"));

            Assert.That(parentOnBranch.Status, Is.EqualTo(OrganizationAuthorizationStatus.MissingPermission));
            Assert.That(branchOnBranch.Succeeded, Is.True, branchOnBranch.Message);
            Assert.That(branchOnParent.Status, Is.EqualTo(OrganizationAuthorizationStatus.MissingPermission));
        }

        [Test]
        public void JointApprovalConsumesApprovalsAndWritesExplicitAudit()
        {
            RuntimeFixture fixture = CreateFixture();
            CreateGuildmaster(fixture, PersonId, "master");
            fixture.Memberships.ApplyMembership(MembershipRequest("organization-membership.test.mentor.approver", "organization.prototype.adventurers-guild", "person.prototype.mentor", PrototypeOrganizationMembershipDefinitionFactory.GuildFullMemberId, "tx.member.mentor.approver"));
            fixture.Memberships.ApplyMembership(MembershipRequest("organization-membership.test.partner.approver", "organization.prototype.adventurers-guild", "person.prototype.partner", PrototypeOrganizationMembershipDefinitionFactory.GuildFullMemberId, "tx.member.partner.approver"));
            OrganizationAuthorityOperationResult mentorGrant = fixture.Authority.CreateDirectGrant(new OrganizationAuthorityGrantRequest
            {
                grantId = "organization-authority-grant.test.mentor.guildmaster",
                organizationId = "organization.prototype.adventurers-guild",
                granteePersonId = "person.prototype.mentor",
                grantorPersonId = PersonId,
                authorityRoleDefinitionId = PrototypeOrganizationAuthorityDefinitionFactory.GuildmasterRoleId,
                scope = OrganizationAuthorityScopeData.ForOrganization("organization.prototype.adventurers-guild"),
                transactionId = "tx.authority.approval.mentor"
            });
            OrganizationAuthorityOperationResult partnerGrant = fixture.Authority.CreateDirectGrant(new OrganizationAuthorityGrantRequest
            {
                grantId = "organization-authority-grant.test.partner.guildmaster",
                organizationId = "organization.prototype.adventurers-guild",
                granteePersonId = "person.prototype.partner",
                grantorPersonId = PersonId,
                authorityRoleDefinitionId = PrototypeOrganizationAuthorityDefinitionFactory.GuildmasterRoleId,
                scope = OrganizationAuthorityScopeData.ForOrganization("organization.prototype.adventurers-guild"),
                transactionId = "tx.authority.approval.partner"
            });
            OrganizationAuthorityOperationResult approvalOne = fixture.Authority.RecordApproval(ApprovalRequest("organization-authority-approval.test.one", "operation.headquarters", "person.prototype.mentor"));
            OrganizationAuthorityOperationResult approvalTwo = fixture.Authority.RecordApproval(ApprovalRequest("organization-authority-approval.test.two", "operation.headquarters", "person.prototype.partner"));
            OrganizationAuthorizationRequest request = AuthRequest("person.prototype.friend", "organization.prototype.adventurers-guild", PrototypeOrganizationAuthorityDefinitionFactory.ChangeHeadquartersActionId, "operation.headquarters");
            request.consumeApprovals = true;

            OrganizationAuthorizationResult denied = fixture.Authority.EvaluateAuthorization(request);
            OrganizationAuthorizationRequest actorRequest = AuthRequest(PersonId, "organization.prototype.adventurers-guild", PrototypeOrganizationAuthorityDefinitionFactory.ChangeHeadquartersActionId, "operation.headquarters");
            actorRequest.consumeApprovals = true;
            OrganizationAuthorizationResult authorized = fixture.Authority.EvaluateAuthorization(actorRequest);
            OrganizationAuthorityOperationResult audit = fixture.Authority.RecordAuthorizationAudit(authorized, "organization-authority-audit.test.headquarters", 120d);

            Assert.That(mentorGrant.Succeeded, Is.True, mentorGrant.Message);
            Assert.That(partnerGrant.Succeeded, Is.True, partnerGrant.Message);
            Assert.That(approvalOne.Succeeded, Is.True, approvalOne.Message);
            Assert.That(approvalTwo.Succeeded, Is.True, approvalTwo.Message);
            Assert.That(denied.Succeeded, Is.False);
            Assert.That(authorized.Succeeded, Is.True, authorized.Message);
            Assert.That(authorized.ApprovalIds.Count, Is.EqualTo(2));
            Assert.That(fixture.Authority.Approvals.All(approval => approval.LifecycleState == OrganizationApprovalLifecycleState.Consumed), Is.True);
            Assert.That(audit.Succeeded, Is.True, audit.Message);
            Assert.That(fixture.Authority.Audits.Single().Status, Is.EqualTo(OrganizationAuthorizationStatus.Authorized));
        }

        [Test]
        public void PersistenceParticipantRejectsCorruptAuthorityGraphWithoutMutation()
        {
            RuntimeFixture fixture = CreateFixture();
            OrganizationAuthorityOperationResult grant = fixture.Authority.CreateDirectGrant(new OrganizationAuthorityGrantRequest
            {
                grantId = "organization-authority-grant.test.persist",
                organizationId = "organization.prototype.adventurers-guild",
                granteePersonId = "person.prototype.friend",
                grantorPersonId = PersonId,
                permissionDefinitionIds = new[] { PrototypeOrganizationAuthorityDefinitionFactory.ViewRestrictedInformationPermissionId },
                scope = OrganizationAuthorityScopeData.ForOrganization("organization.prototype.adventurers-guild"),
                startWorldTime = 0d,
                transactionId = "tx.authority.persist"
            });
            OrganizationAuthorityPersistenceParticipant participant = new OrganizationAuthorityPersistenceParticipant(fixture.Authority, () => fixture.Registry, () => fixture.Organizations, () => fixture.Memberships, PersistenceService.LocalWorldId, () => KnownPersons, () => fixture.Organizations.Snapshots.Select(snapshot => snapshot.OrganizationId).ToArray());
            PersistenceParticipantSaveResult save = participant.CapturePayload();
            OrganizationAuthorityRuntimeSaveData corrupt = JsonUtility.FromJson<OrganizationAuthorityRuntimeSaveData>(save.PayloadJson);
            corrupt.grants[0].permissionDefinitionIds = new[] { "organization-permission.missing" };

            PersistenceParticipantPrepareResult rejected = participant.PreparePayload(JsonUtility.ToJson(corrupt), OrganizationAuthorityPersistenceParticipant.CurrentParticipantSchemaVersion);

            Assert.That(grant.Succeeded, Is.True, grant.Message);
            Assert.That(save.Succeeded, Is.True, save.Message);
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(fixture.Authority.TryGetGrant("organization-authority-grant.test.persist", out OrganizationAuthoritySnapshot live), Is.True);
            Assert.That(live.Data.permissionDefinitionIds, Does.Contain(PrototypeOrganizationAuthorityDefinitionFactory.ViewRestrictedInformationPermissionId));
            Assert.That(fixture.Authority.GrantCount, Is.EqualTo(1));
        }

        [Test]
        public void SnapshotsAndRedactedProjectionsDoNotMutateAuthority()
        {
            RuntimeFixture fixture = CreateFixture();
            OrganizationAuthorityOperationResult grant = fixture.Authority.CreateDirectGrant(new OrganizationAuthorityGrantRequest
            {
                grantId = "organization-authority-grant.test.hidden",
                organizationId = "organization.prototype.adventurers-guild",
                granteePersonId = "person.prototype.friend",
                grantorPersonId = PersonId,
                permissionDefinitionIds = new[] { PrototypeOrganizationAuthorityDefinitionFactory.ViewSecretInformationPermissionId },
                scope = OrganizationAuthorityScopeData.ForOrganization("organization.prototype.adventurers-guild"),
                visibility = OrganizationVisibility.Hidden,
                startWorldTime = 0d,
                transactionId = "tx.authority.hidden"
            });

            OrganizationAuthoritySnapshot snapshot = grant.Grant;
            snapshot.Data.permissionDefinitionIds = new[] { PrototypeOrganizationAuthorityDefinitionFactory.RemoveMembersPermissionId };
            OrganizationAuthorityProjection concealed = fixture.Authority.ProjectGrant(grant.Grant.GrantId, "person.prototype.rival");
            OrganizationAuthorityProjection full = fixture.Authority.ProjectGrant(grant.Grant.GrantId, "person.prototype.rival", privileged: true);

            Assert.That(concealed.Access, Is.EqualTo(OrganizationAuthorityProjectionAccess.Concealed));
            Assert.That(concealed.Snapshot, Is.Null);
            Assert.That(full.Access, Is.EqualTo(OrganizationAuthorityProjectionAccess.Full));
            Assert.That(full.Snapshot.Data.permissionDefinitionIds, Does.Contain(PrototypeOrganizationAuthorityDefinitionFactory.ViewSecretInformationPermissionId));
            Assert.That(full.Snapshot.Data.permissionDefinitionIds, Does.Not.Contain(PrototypeOrganizationAuthorityDefinitionFactory.RemoveMembersPermissionId));
        }

        private static void CreateGuildmaster(RuntimeFixture fixture, string personId, string suffix)
        {
            OrganizationMembershipOperationResult member = fixture.Memberships.ApplyMembership(MembershipRequest($"organization-membership.test.guildmaster.{suffix}", "organization.prototype.adventurers-guild", personId, PrototypeOrganizationMembershipDefinitionFactory.GuildFullMemberId, $"tx.member.guildmaster.{suffix}"));
            RankToSSS(fixture.Memberships, member.Membership.MembershipId, suffix);
            OrganizationMembershipOperationResult office = fixture.Memberships.CreateOffice(OfficeRequest($"organization-office-record.test.guildmaster.{suffix}", "organization.prototype.adventurers-guild", PrototypeOrganizationMembershipDefinitionFactory.GuildmasterOfficeId, $"tx.office.guildmaster.{suffix}"));
            fixture.Memberships.AssignOffice(OfficeAssignmentRequest($"organization-office-assignment.test.guildmaster.{suffix}", office.Office.OfficeId, member.Membership.MembershipId, $"tx.office.guildmaster.assign.{suffix}"));
        }

        private static void RankToSSS(OrganizationMembershipRuntime memberships, string membershipId, string suffix)
        {
            for (int index = 0; index < PrototypeOrganizationMembershipDefinitionFactory.GuildRatingRankIds.Count; index++)
            {
                memberships.AssignRank(RankRequest($"organization-rank-assignment.test.guild-rating.{index}.{suffix}", membershipId, PrototypeOrganizationMembershipDefinitionFactory.GuildRatingRankIds[index], $"tx.rank.guild-rating.{index}.{suffix}"));
            }
        }

        private static OrganizationAuthorizationRequest AuthRequest(string actorId, string organizationId, string actionId, string operationId, double worldTime = 100d)
        {
            return new OrganizationAuthorizationRequest
            {
                operationId = operationId,
                actorPersonId = actorId,
                organizationId = organizationId,
                actionDefinitionId = actionId,
                scope = OrganizationAuthorityScopeData.ForOrganization(organizationId),
                worldTime = worldTime
            };
        }

        private static OrganizationApprovalRequest ApprovalRequest(string approvalId, string operationId, string approverId)
        {
            return new OrganizationApprovalRequest
            {
                approvalId = approvalId,
                operationId = operationId,
                actionDefinitionId = PrototypeOrganizationAuthorityDefinitionFactory.ChangeHeadquartersActionId,
                organizationId = "organization.prototype.adventurers-guild",
                approverPersonId = approverId,
                targetPersonId = "person.prototype.friend",
                scope = OrganizationAuthorityScopeData.ForOrganization("organization.prototype.adventurers-guild"),
                approvedWorldTime = 90d,
                expirationWorldTime = 130d,
                transactionId = $"tx.approval.{approvalId}"
            };
        }

        private static OrganizationMembershipRequest MembershipRequest(string membershipId, string organizationId, string personId, string definitionId, string transactionId, string parentMembershipId = "")
        {
            return new OrganizationMembershipRequest
            {
                membershipId = membershipId,
                organizationId = organizationId,
                personId = personId,
                membershipDefinitionId = definitionId,
                targetStatus = OrganizationMembershipStatus.Active,
                sourceKind = OrganizationMembershipSourceKind.WorldSetup,
                explicitConsent = true,
                parentMembershipId = parentMembershipId,
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

        private static OrganizationOfficeRequest OfficeRequest(string officeId, string organizationId, string officeDefinitionId, string transactionId)
        {
            return new OrganizationOfficeRequest
            {
                officeId = officeId,
                organizationId = organizationId,
                officeDefinitionId = officeDefinitionId,
                worldTime = 30d,
                transactionId = transactionId
            };
        }

        private static OrganizationOfficeAssignmentRequest OfficeAssignmentRequest(string assignmentId, string officeId, string membershipId, string transactionId)
        {
            return new OrganizationOfficeAssignmentRequest
            {
                officeAssignmentId = assignmentId,
                officeId = officeId,
                membershipId = membershipId,
                worldTime = 40d,
                appointedById = PersonId,
                transactionId = transactionId
            };
        }

        private static RuntimeFixture CreateFixture()
        {
            DefinitionRegistry registry = CreateRegistry();
            OrganizationRuntime organizations = new OrganizationRuntime();
            PrototypeOrganizationDefinitionFactory.SeedPrototypeOrganizations(organizations, registry, PersistenceService.LocalWorldId);
            organizations.Configure(registry, PersistenceService.LocalWorldId, KnownPersons, Array.Empty<string>());
            OrganizationMembershipRuntime memberships = new OrganizationMembershipRuntime();
            memberships.Configure(registry, organizations, PersistenceService.LocalWorldId, KnownPersons, organizations.Snapshots.Select(snapshot => snapshot.OrganizationId));
            OrganizationAuthorityRuntime authority = new OrganizationAuthorityRuntime();
            authority.Configure(registry, organizations, memberships, PersistenceService.LocalWorldId, KnownPersons, organizations.Snapshots.Select(snapshot => snapshot.OrganizationId));
            return new RuntimeFixture(registry, organizations, memberships, authority);
        }

        private static DefinitionRegistry CreateRegistry()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            return PrototypeOrganizationAuthorityDefinitionFactory.AddMissingPrototypeOrganizationAuthorityDefinitions(
                PrototypeOrganizationMembershipDefinitionFactory.AddMissingPrototypeOrganizationMembershipDefinitions(
                    PrototypeOrganizationDefinitionFactory.AddMissingPrototypeOrganizationDefinitions(catalog.CreateRegistry())));
        }

        private sealed class RuntimeFixture
        {
            public RuntimeFixture(DefinitionRegistry registry, OrganizationRuntime organizations, OrganizationMembershipRuntime memberships, OrganizationAuthorityRuntime authority)
            {
                Registry = registry;
                Organizations = organizations;
                Memberships = memberships;
                Authority = authority;
            }

            public DefinitionRegistry Registry { get; }
            public OrganizationRuntime Organizations { get; }
            public OrganizationMembershipRuntime Memberships { get; }
            public OrganizationAuthorityRuntime Authority { get; }
        }
    }
}
#endif
