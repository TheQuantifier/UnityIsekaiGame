#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.Development.Automation;
using UnityIsekaiGame.Factions;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Organizations;
using UnityIsekaiGame.Persistence;

namespace UnityIsekaiGame.Tests
{
    public sealed class FactionIdentityAlignmentInternalPoliticsTests
    {
        private const string PersonId = PersistenceService.LocalPlayerId;
        private const string GuildId = "organization.prototype.adventurers-guild";
        private static readonly string[] KnownPersons = { PersonId, "person.prototype.friend", "person.prototype.mentor" };

        [Test]
        public void PrototypeFactionDefinitionsValidateAndExposePoliticalMetadata()
        {
            DefinitionRegistry registry = CreateRegistry();
            DefinitionValidationReport report = new DefinitionValidationReport();
            foreach (IDefinitionCatalogValidationParticipant definition in registry.DefinitionsById.Values.OfType<IDefinitionCatalogValidationParticipant>())
            {
                if (definition is FactionDefinition or FactionAffiliationDefinition or FactionRoleDefinition or FactionPositionDefinition or FactionAlignmentAxisDefinition)
                {
                    definition.ValidateCatalogDefinition(registry.DefinitionsById, report);
                }
            }

            Assert.That(report.ErrorCount, Is.Zero, report.ToString());
            Assert.That(registry.TryGet(PrototypeFactionDefinitionFactory.ReformFactionId, out FactionDefinition reform), Is.True);
            Assert.That(reform.PoliticalCategory, Is.EqualTo(PoliticalFactionCategory.ReformMovement));
            Assert.That(reform.OrganizationMembershipRequired, Is.True);
            Assert.That(registry.TryGet(PrototypeFactionDefinitionFactory.SecretMemberAffiliationId, out FactionAffiliationDefinition secret), Is.True);
            Assert.That(FactionModelUtility.IsSecret(secret.Visibility), Is.True);
            Assert.That(registry.TryGet(PrototypeFactionDefinitionFactory.ReformTraditionAxisId, out FactionAlignmentAxisDefinition axis), Is.True);
            Assert.That(axis.MinimumValue, Is.LessThan(axis.MaximumValue));
        }

        [Test]
        public void FactionAffiliationRequiresOrganizationMembershipButDoesNotGrantIt()
        {
            using TestLabRuntimeBundle bundle = CreateBundle();
            FactionRuntime factions = bundle.Factions;
            FactionOperationResult create = factions.CreateFaction(CreateFaction("membership", PrototypeFactionDefinitionFactory.ReformFactionId, "Guild Reform Bloc"));
            FactionOperationResult denied = factions.ApplyAffiliation(Affiliation("denied", create.Faction.factionId, "person.prototype.friend", PrototypeFactionDefinitionFactory.FormalMemberAffiliationId, consent: true));
            OrganizationMembershipOperationResult membership = bundle.OrganizationMemberships.ApplyMembership(Membership("member", PersonId));
            FactionOperationResult affiliated = factions.ApplyAffiliation(Affiliation("member", create.Faction.factionId, PersonId, PrototypeFactionDefinitionFactory.FormalMemberAffiliationId, consent: true));
            FactionOperationResult supporter = factions.ApplyAffiliation(Affiliation("supporter", create.Faction.factionId, "person.prototype.friend", PrototypeFactionDefinitionFactory.SupporterAffiliationId, consent: false));

            Assert.That(create.Succeeded, Is.True, create.Message);
            Assert.That(denied.Code, Is.EqualTo(FactionOperationCode.InvalidEligibility));
            Assert.That(membership.Succeeded, Is.True, membership.Message);
            Assert.That(affiliated.Succeeded, Is.True, affiliated.Message);
            Assert.That(supporter.Succeeded, Is.True, supporter.Message);
            Assert.That(bundle.OrganizationMemberships.Memberships.Any(item => item.PersonId == "person.prototype.friend" && item.OrganizationId == GuildId && item.IsActive), Is.False);
            Assert.That(factions.AffiliationCount, Is.EqualTo(2));
        }

        [Test]
        public void RolesPositionsRecommendationsAndCohesionReadOwningOrganizationState()
        {
            using TestLabRuntimeBundle bundle = CreateBundle();
            PrepareVotingFixture(bundle);
            FactionRuntime factions = bundle.Factions;
            FactionOperationResult create = factions.CreateFaction(CreateFaction("cohesion", PrototypeFactionDefinitionFactory.ReformFactionId, "Cohesion Bloc"));
            FactionOperationResult actorAffiliation = factions.ApplyAffiliation(Affiliation("actor", create.Faction.factionId, PersonId, PrototypeFactionDefinitionFactory.FormalMemberAffiliationId, consent: true));
            FactionOperationResult friendAffiliation = factions.ApplyAffiliation(Affiliation("friend", create.Faction.factionId, "person.prototype.friend", PrototypeFactionDefinitionFactory.FormalMemberAffiliationId, consent: true));
            FactionOperationResult role = factions.AssignRole(new FactionRoleAssignmentRequest { transactionId = "tx.faction.role", roleAssignmentId = "faction-role-assignment.test.organizer", affiliationId = actorAffiliation.Affiliation.affiliationId, roleDefinitionId = PrototypeFactionDefinitionFactory.OrganizerRoleId, worldTime = 3d });
            OrganizationDecisionOperationResult submit = SubmitProposal(bundle);
            FactionOperationResult position = factions.SetPosition(new FactionPositionRequest { transactionId = "tx.faction.position", positionId = "faction-position.test.proposal", factionId = create.Faction.factionId, positionDefinitionId = PrototypeFactionDefinitionFactory.ProposalPositionId, targetKind = FactionPositionTargetKind.OrganizationProposal, targetId = "organization-proposal-record.test.faction", stance = FactionPositionStance.Supports, weight = 5, worldTime = 4d });
            FactionOperationResult recommendation = factions.RecommendVote(new FactionRecommendationRequest { transactionId = "tx.faction.recommend", recommendationId = "faction-recommendation.test", factionId = create.Faction.factionId, proposalId = "organization-proposal-record.test.faction", recommendation = FactionVoteRecommendationKind.Support, issuedByPersonId = PersonId, worldTime = 5d });
            OrganizationDecisionOperationResult voteActor = bundle.OrganizationDecisions.CastVote(Vote("tx.vote.actor", "organization-vote-record.test.actor", PersonId, OrganizationVoteChoice.Approve));
            OrganizationDecisionOperationResult voteFriend = bundle.OrganizationDecisions.CastVote(Vote("tx.vote.friend", "organization-vote-record.test.friend", "person.prototype.friend", OrganizationVoteChoice.Reject));
            FactionVoteCohesionReport cohesion = factions.CreateVoteCohesionReport(create.Faction.factionId, "organization-proposal-record.test.faction", 6d);
            FactionInfluenceReport influence = factions.CreateInfluenceReport(create.Faction.factionId, GuildId, 6d);

            Assert.That(actorAffiliation.Succeeded, Is.True, actorAffiliation.Message);
            Assert.That(friendAffiliation.Succeeded, Is.True, friendAffiliation.Message);
            Assert.That(role.Succeeded, Is.True, role.Message);
            Assert.That(submit.Succeeded, Is.True, submit.Message);
            Assert.That(position.Succeeded, Is.True, position.Message);
            Assert.That(recommendation.Succeeded, Is.True, recommendation.Message);
            Assert.That(voteActor.Succeeded, Is.True, voteActor.Message);
            Assert.That(voteFriend.Succeeded, Is.True, voteFriend.Message);
            Assert.That(cohesion.AlignedVotes, Is.EqualTo(1));
            Assert.That(cohesion.OpposedVotes, Is.EqualTo(1));
            Assert.That(influence.InfluenceScore, Is.GreaterThan(0));
            Assert.That(bundle.OrganizationDecisions.VoteCount, Is.EqualTo(2));
        }

        [Test]
        public void ProjectionPersistenceAndCorruptRestorePreserveLiveFactionState()
        {
            using TestLabRuntimeBundle bundle = CreateBundle();
            FactionRuntime factions = bundle.Factions;
            FactionOperationResult secret = factions.CreateFaction(CreateFaction("secret", PrototypeFactionDefinitionFactory.SecretFactionId, "Hidden Lantern", FactionVisibility.Secret));
            FactionProjection concealed = factions.GetFactionProjection(secret.Faction.factionId, new FactionProjectionContext());
            FactionProjection development = factions.GetFactionProjection(secret.Faction.factionId, new FactionProjectionContext { developmentView = true, privileged = true });
            FactionRuntimeSaveData save = factions.CreateSaveData();
            FactionPersistenceParticipant participant = new FactionPersistenceParticipant(factions, () => bundle.DefinitionRegistry, () => bundle.Organizations, () => bundle.OrganizationMemberships, () => bundle.OrganizationAuthority, () => bundle.OrganizationResources, () => bundle.OrganizationDecisions, bundle.WorldId, () => bundle.KnownPersonIds.ToArray());
            PersistenceParticipantPrepareResult prepared = participant.PreparePayload(JsonUtility.ToJson(save), FactionPersistenceParticipant.CurrentParticipantSchemaVersion);
            FactionRuntimeSaveData corrupt = save.Clone();
            corrupt.factions[0].factionDefinitionId = "faction.missing";
            long beforeRevision = factions.Revision;
            PersistenceParticipantPrepareResult rejected = participant.PreparePayload(JsonUtility.ToJson(corrupt), FactionPersistenceParticipant.CurrentParticipantSchemaVersion);

            Assert.That(secret.Succeeded, Is.True, secret.Message);
            Assert.That(concealed.Access, Is.EqualTo(FactionProjectionAccess.Concealed));
            Assert.That(development.Access, Is.EqualTo(FactionProjectionAccess.Development));
            Assert.That(prepared.Succeeded, Is.True, prepared.Message);
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(factions.Revision, Is.EqualTo(beforeRevision));
            Assert.That(factions.FactionCount, Is.EqualTo(1));
        }

        private static TestLabRuntimeBundle CreateBundle()
        {
            return TestLabRuntimeBundle.CreateFresh(CreateRegistry(), PersonId, PersistenceService.LocalWorldId, KnownPersons, Array.Empty<string>(), "Faction Tests");
        }

        private static DefinitionRegistry CreateRegistry()
        {
            return PrototypeFactionDefinitionFactory.AddMissingPrototypeFactionDefinitions(
                PrototypeOrganizationDecisionDefinitionFactory.AddMissingPrototypeOrganizationDecisionDefinitions(
                    PrototypeOrganizationResourceDefinitionFactory.AddMissingPrototypeOrganizationResourceDefinitions(
                        PrototypeOrganizationAuthorityDefinitionFactory.AddMissingPrototypeOrganizationAuthorityDefinitions(
                            PrototypeOrganizationMembershipDefinitionFactory.AddMissingPrototypeOrganizationMembershipDefinitions(
                                PrototypeOrganizationDefinitionFactory.AddMissingPrototypeOrganizationDefinitions(new DefinitionRegistry(Array.Empty<IGameDefinition>())))))));
        }

        private static FactionCreateRequest CreateFaction(string suffix, string definitionId, string name, FactionVisibility visibility = FactionVisibility.Public)
        {
            return CreateFaction(suffix, definitionId, name, visibility, FactionHostContextData.ForOrganization(GuildId));
        }

        private static FactionCreateRequest CreateFaction(string suffix, string definitionId, string name, FactionHostContextData host)
        {
            return CreateFaction(suffix, definitionId, name, FactionVisibility.Public, host);
        }

        private static FactionCreateRequest CreateFaction(string suffix, string definitionId, string name, FactionVisibility visibility, FactionHostContextData host)
        {
            return new FactionCreateRequest
            {
                transactionId = $"tx.faction.create.{suffix}",
                factionId = $"faction.test.{suffix}",
                factionDefinitionId = definitionId,
                officialName = name,
                publicDescription = $"{name} test faction.",
                hostContext = host,
                founderPersonId = PersonId,
                founderOrganizationId = host?.primaryOrganizationId ?? string.Empty,
                worldTime = 1d,
                initialState = FactionLifecycleState.Active,
                visibility = visibility
            };
        }

        private static FactionAffiliationRequest Affiliation(string suffix, string factionId, string personId, string definitionId, bool consent)
        {
            return new FactionAffiliationRequest
            {
                transactionId = $"tx.faction.affiliation.{suffix}",
                affiliationId = $"faction-affiliation.test.{suffix}",
                factionId = factionId,
                personId = personId,
                affiliationDefinitionId = definitionId,
                explicitConsent = consent,
                organizationContextId = GuildId,
                worldTime = 2d
            };
        }

        private static OrganizationMembershipRequest Membership(string suffix, string personId)
        {
            return new OrganizationMembershipRequest
            {
                transactionId = $"tx.membership.{suffix}",
                membershipId = $"organization-membership.test.{suffix}",
                organizationId = GuildId,
                personId = personId,
                membershipDefinitionId = PrototypeOrganizationMembershipDefinitionFactory.GuildFullMemberId,
                targetStatus = OrganizationMembershipStatus.Active,
                sourceKind = OrganizationMembershipSourceKind.WorldSetup,
                explicitConsent = true,
                worldTime = 1d
            };
        }

        private static void PrepareVotingFixture(TestLabRuntimeBundle bundle)
        {
            bundle.OrganizationMemberships.ApplyMembership(Membership("actor", PersonId));
            bundle.OrganizationMemberships.ApplyMembership(Membership("friend", "person.prototype.friend"));
            bundle.OrganizationAuthority.CreateDirectGrant(new OrganizationAuthorityGrantRequest
            {
                transactionId = "tx.authority.actor",
                grantId = "organization-authority-grant.test.actor",
                organizationId = GuildId,
                granteePersonId = PersonId,
                grantorPersonId = PersonId,
                authorityRoleDefinitionId = PrototypeOrganizationAuthorityDefinitionFactory.GuildmasterRoleId,
                scope = OrganizationAuthorityScopeData.ForOrganization(GuildId)
            });
        }

        private static OrganizationDecisionOperationResult SubmitProposal(TestLabRuntimeBundle bundle)
        {
            return bundle.OrganizationDecisions.SubmitProposal(new OrganizationProposalRequest
            {
                transactionId = "tx.proposal.submit",
                proposalId = "organization-proposal-record.test.faction",
                organizationId = GuildId,
                proposalDefinitionId = PrototypeOrganizationDecisionDefinitionFactory.EstablishGoalProposalId,
                title = "Faction-backed proposal",
                proposerPersonId = PersonId,
                requestedExecutionOperations = new[]
                {
                    new OrganizationDecisionExecutionOperationData
                    {
                        operationId = "decision-operation.goal.test.faction",
                        kind = OrganizationDecisionExecutionOperationKind.EstablishGoal,
                        targetId = "organization-goal-record.test.faction",
                        definitionId = PrototypeOrganizationDecisionDefinitionFactory.RecruitmentGoalId,
                        goalPayload = new OrganizationGoalRecordData
                        {
                            goalId = "organization-goal-record.test.faction",
                            organizationId = GuildId,
                            goalDefinitionId = PrototypeOrganizationDecisionDefinitionFactory.RecruitmentGoalId,
                            displayName = "Faction-backed recruitment goal",
                            targetValue = 3L,
                            priority = 50,
                            visibility = OrganizationVisibility.Restricted
                        },
                        required = true
                    }
                },
                submittedWorldTime = 4d,
                votingStartWorldTime = 4d,
                votingEndWorldTime = 10d
            });
        }

        private static OrganizationVoteRequest Vote(string transactionId, string voteId, string voter, OrganizationVoteChoice choice)
        {
            return new OrganizationVoteRequest
            {
                transactionId = transactionId,
                voteId = voteId,
                proposalId = "organization-proposal-record.test.faction",
                voterPersonId = voter,
                choice = choice,
                worldTime = 5d
            };
        }
    }
}
#endif
