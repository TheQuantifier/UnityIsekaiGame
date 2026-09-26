#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.Economy;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Organizations;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.Progression;

namespace UnityIsekaiGame.Tests
{
    public sealed class OrganizationGoalsPoliciesDecisionsTests
    {
        private const string GuildId = "organization.prototype.adventurers-guild";
        private const string ActorId = PersistenceService.LocalPlayerId;

        [Test]
        public void PrototypeDecisionDefinitionsValidateAndAuthorityRolesExposeGovernancePermissions()
        {
            Fixture fixture = Fixture.Create();
            DefinitionValidationReport report = new DefinitionValidationReport();

            foreach (IDefinitionCatalogValidationParticipant definition in fixture.Registry.DefinitionsById.Values.OfType<IDefinitionCatalogValidationParticipant>())
            {
                if (definition is OrganizationGoalDefinition or OrganizationPolicyDefinition or OrganizationDecisionProcedureDefinition or OrganizationProposalDefinition)
                {
                    definition.ValidateCatalogDefinition(fixture.Registry.DefinitionsById, report);
                }
            }

            Assert.That(report.ErrorCount, Is.Zero, report.ToString());
            Assert.That(fixture.Registry.TryGet(PrototypeOrganizationDecisionDefinitionFactory.RecruitmentGoalId, out OrganizationGoalDefinition goal), Is.True);
            Assert.That(goal.ProgressSourceKind, Is.EqualTo(OrganizationGoalProgressSourceKind.ActiveMembershipCount));
            Assert.That(fixture.Registry.TryGet(PrototypeOrganizationDecisionDefinitionFactory.SimpleMajorityProcedureId, out OrganizationDecisionProcedureDefinition procedure), Is.True);
            Assert.That(procedure.VoterEligibility, Is.EqualTo(OrganizationVoterEligibilityKind.ActiveMembers));
            Assert.That(fixture.Registry.TryGet(PrototypeOrganizationDecisionDefinitionFactory.ApproveBudgetProposalId, out OrganizationProposalDefinition budget), Is.True);
            Assert.That(budget.SupportedExecutionOperations, Does.Contain(OrganizationDecisionExecutionOperationKind.ApproveBudget));
            Assert.That(fixture.Registry.TryGet(PrototypeOrganizationAuthorityDefinitionFactory.GuildmasterRoleId, out OrganizationAuthorityRoleDefinition guildmaster), Is.True);
            Assert.That(guildmaster.GrantedPermissionIds, Does.Contain(PrototypeOrganizationAuthorityDefinitionFactory.ExecuteOrganizationResolutionPermissionId));
        }

        [Test]
        public void GoalsPoliciesProposalsVotesAndExecutionUseOwningRuntimes()
        {
            Fixture fixture = Fixture.Create();
            fixture.CreateTreasuryAndAccounts(250L);

            OrganizationDecisionOperationResult goal = fixture.Decisions.CreateGoal(new OrganizationGoalRequest
            {
                transactionId = "tx.decisions.goal",
                goalId = "organization-goal-record.test.recruitment",
                organizationId = GuildId,
                goalDefinitionId = PrototypeOrganizationDecisionDefinitionFactory.RecruitmentGoalId,
                targetValue = 3L,
                priority = 25,
                actorPersonId = ActorId,
                worldTime = 10d
            });
            OrganizationDecisionOperationResult policy = fixture.Decisions.CreatePolicy(fixture.Policy("tx.decisions.policy", "organization-policy-record.test.confidentiality", ActorId, 11d));
            OrganizationDecisionOperationResult conflict = fixture.Decisions.CreatePolicy(fixture.Policy("tx.decisions.policy.conflict", "organization-policy-record.test.confidentiality.conflict", ActorId, 12d));
            OrganizationDecisionOperationResult submit = fixture.SubmitBudgetProposal();
            OrganizationDecisionOperationResult voteActor = fixture.Decisions.CastVote(fixture.Vote("tx.decisions.vote.actor", "organization-vote-record.test.actor", ActorId, OrganizationVoteChoice.Approve));
            OrganizationDecisionOperationResult voteFriend = fixture.Decisions.CastVote(fixture.Vote("tx.decisions.vote.friend", "organization-vote-record.test.friend", "person.prototype.friend", OrganizationVoteChoice.Approve));
            OrganizationDecisionOperationResult close = fixture.Decisions.CloseVote(new OrganizationCloseVoteRequest
            {
                transactionId = "tx.decisions.close",
                proposalId = "organization-proposal-record.test.budget",
                resolutionId = "organization-resolution-record.test.budget",
                actorPersonId = ActorId,
                worldTime = 21d
            });
            OrganizationDecisionOperationResult preview = fixture.Decisions.ExecuteResolution(new OrganizationDecisionExecutionRequest
            {
                transactionId = "tx.decisions.execute.preview",
                executionId = "organization-execution-record.test.preview",
                resolutionId = "organization-resolution-record.test.budget",
                actorPersonId = ActorId,
                worldTime = 22d,
                preview = true
            });
            int budgetCountAfterPreview = fixture.Resources.BudgetCount;
            OrganizationDecisionOperationResult execute = fixture.Decisions.ExecuteResolution(new OrganizationDecisionExecutionRequest
            {
                transactionId = "tx.decisions.execute",
                executionId = "organization-execution-record.test.budget",
                resolutionId = "organization-resolution-record.test.budget",
                actorPersonId = ActorId,
                worldTime = 23d
            });

            Assert.That(goal.Succeeded, Is.True, goal.Message);
            Assert.That(goal.Goal.lifecycleState, Is.EqualTo(OrganizationGoalLifecycleState.Completed));
            Assert.That(policy.Succeeded, Is.True, policy.Message);
            Assert.That(conflict.Code, Is.EqualTo(OrganizationDecisionOperationCode.InvalidConflict));
            Assert.That(submit.Succeeded, Is.True, submit.Message);
            Assert.That(voteActor.Succeeded, Is.True, voteActor.Message);
            Assert.That(voteFriend.Succeeded, Is.True, voteFriend.Message);
            Assert.That(close.Succeeded, Is.True, close.Message);
            Assert.That(close.Resolution.outcome, Is.EqualTo(OrganizationResolutionOutcome.Adopted));
            Assert.That(preview.Succeeded, Is.True, preview.Message);
            Assert.That(budgetCountAfterPreview, Is.Zero);
            Assert.That(execute.Succeeded, Is.True, execute.Message);
            Assert.That(fixture.Resources.BudgetCount, Is.EqualTo(1));
            Assert.That(fixture.Decisions.ExecutionCount, Is.EqualTo(1));
        }

        [Test]
        public void ProjectionRedactsDecisionPayloadWithoutMutatingAuthoritativeProposal()
        {
            Fixture fixture = Fixture.Create();
            fixture.CreateTreasuryAndAccounts(100L);
            Assert.That(fixture.SubmitBudgetProposal().Succeeded, Is.True);

            OrganizationDecisionProjection redacted = fixture.Decisions.GetProposalProjection("organization-proposal-record.test.budget", OrganizationDecisionProjectionAccess.Redacted);
            OrganizationDecisionProjection full = fixture.Decisions.GetProposalProjection("organization-proposal-record.test.budget", OrganizationDecisionProjectionAccess.Full);

            Assert.That(redacted.Succeeded, Is.True);
            Assert.That(redacted.Redacted, Is.True);
            Assert.That(redacted.Proposal.requestedExecutionOperations, Is.Empty);
            Assert.That(full.Succeeded, Is.True);
            Assert.That(full.Proposal.requestedExecutionOperations, Has.Length.EqualTo(1));
        }

        [Test]
        public void PersistencePrepareRejectsCorruptDecisionGraphWithoutMutatingLiveState()
        {
            Fixture fixture = Fixture.Create();
            fixture.CreateTreasuryAndAccounts(100L);
            Assert.That(fixture.SubmitBudgetProposal().Succeeded, Is.True);
            OrganizationDecisionPersistenceParticipant participant = fixture.Participant();
            PersistenceParticipantSaveResult captured = participant.CapturePayload();
            OrganizationDecisionRuntimeSaveData corrupt = JsonUtility.FromJson<OrganizationDecisionRuntimeSaveData>(captured.PayloadJson);
            corrupt.proposals[0].organizationId = "organization.missing";
            long revisionBefore = fixture.Decisions.Revision;
            int proposalCountBefore = fixture.Decisions.ProposalCount;

            PersistenceParticipantPrepareResult rejected = participant.PreparePayload(JsonUtility.ToJson(corrupt), OrganizationDecisionPersistenceParticipant.CurrentParticipantSchemaVersion);

            Assert.That(captured.Succeeded, Is.True, captured.Message);
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(fixture.Decisions.Revision, Is.EqualTo(revisionBefore));
            Assert.That(fixture.Decisions.ProposalCount, Is.EqualTo(proposalCountBefore));
        }

        private sealed class Fixture
        {
            private static readonly string[] Persons = { ActorId, "person.prototype.friend", "person.prototype.mentor" };

            private Fixture(DefinitionRegistry registry, CurrencyDefinition currency)
            {
                Registry = registry;
                Currency = currency;
                Organizations = new OrganizationRuntime();
                PrototypeOrganizationDefinitionFactory.SeedPrototypeOrganizations(Organizations, registry, PersistenceService.LocalWorldId);
                Organizations.Configure(registry, PersistenceService.LocalWorldId, Persons, Array.Empty<string>());
                Memberships = new OrganizationMembershipRuntime();
                Memberships.Configure(registry, Organizations, PersistenceService.LocalWorldId, Persons, Organizations.Snapshots.Select(snapshot => snapshot.OrganizationId));
                Authority = new OrganizationAuthorityRuntime();
                Authority.Configure(registry, Organizations, Memberships, PersistenceService.LocalWorldId, Persons, Organizations.Snapshots.Select(snapshot => snapshot.OrganizationId));
                Economy = new EconomyRuntime();
                Economy.Configure(registry, PersistenceService.LocalWorldId);
                Resources = new OrganizationResourceRuntime();
                Resources.Configure(registry, Organizations, Authority, Economy, PersistenceService.LocalWorldId, null, null, new ItemInstanceIdentityRuntime());
                Decisions = new OrganizationDecisionRuntime();
                Decisions.Configure(registry, Organizations, Memberships, Authority, Resources, PersistenceService.LocalWorldId, Persons, Economy);
                CreateGuildmaster();
                CreateVotingMember("person.prototype.friend", "friend");
                CreateVotingMember("person.prototype.mentor", "mentor");
            }

            public DefinitionRegistry Registry { get; }
            public CurrencyDefinition Currency { get; }
            public OrganizationRuntime Organizations { get; }
            public OrganizationMembershipRuntime Memberships { get; }
            public OrganizationAuthorityRuntime Authority { get; }
            public EconomyRuntime Economy { get; }
            public OrganizationResourceRuntime Resources { get; }
            public OrganizationDecisionRuntime Decisions { get; }

            public static Fixture Create()
            {
                CurrencyDefinition currency = ScriptableObject.CreateInstance<CurrencyDefinition>();
                currency.Initialize("currency.test.organization-decision-gold", "Organization Decision Gold", "ODG");
                DefinitionRegistry baseRegistry = new DefinitionRegistry(new IGameDefinition[] { currency });
                DefinitionRegistry registry = PrototypeOrganizationDecisionDefinitionFactory.AddMissingPrototypeOrganizationDecisionDefinitions(
                    PrototypeOrganizationResourceDefinitionFactory.AddMissingPrototypeOrganizationResourceDefinitions(
                        PrototypeOrganizationAuthorityDefinitionFactory.AddMissingPrototypeOrganizationAuthorityDefinitions(
                            PrototypeOrganizationMembershipDefinitionFactory.AddMissingPrototypeOrganizationMembershipDefinitions(
                                PrototypeOrganizationDefinitionFactory.AddMissingPrototypeOrganizationDefinitions(baseRegistry)))));
                return new Fixture(registry, currency);
            }

            public void CreateTreasuryAndAccounts(long openingBalance)
            {
                Assert.That(Resources.CreateTreasury(new OrganizationTreasuryRequest
                {
                    transactionId = "tx.decisions.resources.treasury",
                    treasuryId = "organization-treasury.test.decisions",
                    organizationId = GuildId,
                    resourceTypeDefinitionId = PrototypeOrganizationResourceDefinitionFactory.CurrencyResourceTypeId,
                    officialName = "Decision Treasury",
                    actorPersonId = ActorId,
                    worldTime = 1d
                }).Succeeded, Is.True);
                Assert.That(Resources.CreateAccount(new OrganizationAccountRequest
                {
                    transactionId = "tx.decisions.resources.account",
                    accountId = "organization-account.test.decisions.operating",
                    treasuryId = "organization-treasury.test.decisions",
                    organizationId = GuildId,
                    economyAccountId = "economy-account.test.decisions.operating",
                    officialName = "Decision Operating Account",
                    currencyDefinitionId = Currency.Id,
                    openingBalanceUnits = openingBalance,
                    actorPersonId = ActorId,
                    worldTime = 2d
                }).Succeeded, Is.True);
            }

            public OrganizationDecisionOperationResult SubmitBudgetProposal() => Decisions.SubmitProposal(new OrganizationProposalRequest
            {
                transactionId = "tx.decisions.proposal",
                proposalId = "organization-proposal-record.test.budget",
                organizationId = GuildId,
                proposalDefinitionId = PrototypeOrganizationDecisionDefinitionFactory.ApproveBudgetProposalId,
                title = "Approve a training budget",
                proposerPersonId = ActorId,
                requestedExecutionOperations = new[]
                {
                    new OrganizationDecisionExecutionOperationData
                    {
                        operationId = "operation.test.budget",
                        kind = OrganizationDecisionExecutionOperationKind.ApproveBudget,
                        targetId = "organization-budget.test.decisions.training",
                        treasuryId = "organization-treasury.test.decisions",
                        accountId = "organization-account.test.decisions.operating",
                        currencyDefinitionId = Currency.Id,
                        units = 40L,
                        purpose = "training",
                        required = true
                    }
                },
                submittedWorldTime = 10d,
                votingStartWorldTime = 10d,
                votingEndWorldTime = 20d
            });

            public OrganizationPolicyRequest Policy(string transactionId, string policyId, string actorId, double worldTime) => new OrganizationPolicyRequest
            {
                transactionId = transactionId,
                policyId = policyId,
                organizationId = GuildId,
                policyDefinitionId = PrototypeOrganizationDecisionDefinitionFactory.ConfidentialityPolicyId,
                scope = OrganizationPolicyScopeData.EntireOrganization(GuildId),
                parameters = new[]
                {
                    new OrganizationPolicyParameterValueData { parameterId = "visibility", type = OrganizationPolicyParameterType.EnumValue, stringValue = OrganizationVisibility.Restricted.ToString() },
                    new OrganizationPolicyParameterValueData { parameterId = "reshare_allowed", type = OrganizationPolicyParameterType.Boolean, boolValue = false }
                },
                priority = 100,
                actorPersonId = actorId,
                adoptedWorldTime = worldTime,
                effectiveStartWorldTime = worldTime
            };

            public OrganizationVoteRequest Vote(string transactionId, string voteId, string voterId, OrganizationVoteChoice choice) => new OrganizationVoteRequest
            {
                transactionId = transactionId,
                voteId = voteId,
                proposalId = "organization-proposal-record.test.budget",
                voterPersonId = voterId,
                choice = choice,
                worldTime = 12d
            };

            public OrganizationDecisionPersistenceParticipant Participant() => new OrganizationDecisionPersistenceParticipant(Decisions, () => Registry, () => Organizations, () => Memberships, () => Authority, () => Resources, PersistenceService.LocalWorldId, () => Persons);

            private void CreateGuildmaster()
            {
                OrganizationMembershipOperationResult member = CreateVotingMember(ActorId, "guildmaster");
                string[] ranks = PrototypeOrganizationMembershipDefinitionFactory.GuildRatingRankIds.ToArray();
                for (int index = 0; index < ranks.Length; index++) Memberships.AssignRank(new OrganizationRankAssignmentRequest
                {
                    rankAssignmentId = $"organization-rank-assignment.test.decisions.{index}",
                    membershipId = member.Membership.MembershipId,
                    rankDefinitionId = ranks[index],
                    worldTime = index + 1d,
                    assignedById = ActorId,
                    transactionId = $"tx.decisions.rank.{index}"
                });
                OrganizationMembershipOperationResult office = Memberships.CreateOffice(new OrganizationOfficeRequest
                {
                    officeId = "organization-office-record.test.decisions.guildmaster",
                    organizationId = GuildId,
                    officeDefinitionId = PrototypeOrganizationMembershipDefinitionFactory.GuildmasterOfficeId,
                    worldTime = 4d,
                    transactionId = "tx.decisions.office"
                });
                Memberships.AssignOffice(new OrganizationOfficeAssignmentRequest
                {
                    officeAssignmentId = "organization-office-assignment.test.decisions.guildmaster",
                    officeId = office.Office.OfficeId,
                    membershipId = member.Membership.MembershipId,
                    worldTime = 5d,
                    appointedById = ActorId,
                    transactionId = "tx.decisions.office.assign"
                });
            }

            private OrganizationMembershipOperationResult CreateVotingMember(string personId, string suffix)
            {
                return Memberships.ApplyMembership(new OrganizationMembershipRequest
                {
                    membershipId = $"organization-membership.test.decisions.{suffix}",
                    organizationId = GuildId,
                    personId = personId,
                    membershipDefinitionId = PrototypeOrganizationMembershipDefinitionFactory.GuildFullMemberId,
                    targetStatus = OrganizationMembershipStatus.Active,
                    sourceKind = OrganizationMembershipSourceKind.WorldSetup,
                    explicitConsent = true,
                    worldTime = 0d,
                    transactionId = $"tx.decisions.member.{suffix}"
                });
            }
        }
    }
}
#endif
