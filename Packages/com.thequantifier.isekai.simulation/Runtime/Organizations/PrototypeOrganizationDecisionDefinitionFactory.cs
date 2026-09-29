using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Organizations
{
    public static class PrototypeOrganizationDecisionDefinitionFactory
    {
        public const string RecruitmentGoalId = "organization-goal.prototype.guild.recruitment";
        public const string ReserveFundGoalId = "organization-goal.prototype.guild.reserve-fund";
        public const string ConfidentialityPolicyId = "organization-policy.prototype.guild.confidentiality";
        public const string BudgetLimitPolicyId = "organization-policy.prototype.guild.budget-limit";
        public const string SimpleMajorityProcedureId = "organization-decision-procedure.prototype.simple-majority";
        public const string SecretBallotProcedureId = "organization-decision-procedure.prototype.secret-ballot";
        public const string EmergencyProcedureId = "organization-decision-procedure.prototype.emergency-executive";
        public const string AdoptPolicyProposalId = "organization-proposal.prototype.adopt-policy";
        public const string EstablishGoalProposalId = "organization-proposal.prototype.establish-goal";
        public const string ApproveBudgetProposalId = "organization-proposal.prototype.approve-budget";
        public const string EmergencyDecisionProposalId = "organization-proposal.prototype.emergency-decision";

        public static DefinitionRegistry AddMissingPrototypeOrganizationDecisionDefinitions(DefinitionRegistry baseRegistry)
        {
            HashSet<string> ids = new HashSet<string>(baseRegistry?.DefinitionsById.Keys ?? Array.Empty<string>(), StringComparer.Ordinal);
            List<IGameDefinition> definitions = new List<IGameDefinition>();
            if (baseRegistry != null)
            {
                definitions.AddRange(baseRegistry.DefinitionsById.Values.Where(definition => definition != null));
            }

            definitions.AddRange(CreateMissingGoalDefinitions(ids));
            definitions.AddRange(CreateMissingPolicyDefinitions(ids));
            definitions.AddRange(CreateMissingProcedureDefinitions(ids));
            definitions.AddRange(CreateMissingProposalDefinitions(ids));
            return new DefinitionRegistry(definitions);
        }

        public static IReadOnlyList<OrganizationGoalDefinition> CreateMissingGoalDefinitions(IEnumerable<string> existingIds)
        {
            HashSet<string> ids = Set(existingIds);
            List<OrganizationGoalDefinition> definitions = new List<OrganizationGoalDefinition>();
            AddGoal(definitions, ids, RecruitmentGoalId, "Guild Recruitment Goal", OrganizationGoalCategory.Recruitment, OrganizationGoalProgressSourceKind.ActiveMembershipCount, 3L, OrganizationGoalCompletionPolicy.Automatic, new[] { "person" });
            AddGoal(definitions, ids, ReserveFundGoalId, "Guild Reserve Fund Goal", OrganizationGoalCategory.FinancialReserve, OrganizationGoalProgressSourceKind.TreasuryBalance, 100L, OrganizationGoalCompletionPolicy.Automatic, new[] { PrototypeOrganizationResourceDefinitionFactory.CurrencyResourceTypeId });
            return definitions;
        }

        public static IReadOnlyList<OrganizationPolicyDefinition> CreateMissingPolicyDefinitions(IEnumerable<string> existingIds)
        {
            HashSet<string> ids = Set(existingIds);
            List<OrganizationPolicyDefinition> definitions = new List<OrganizationPolicyDefinition>();
            AddPolicy(
                definitions,
                ids,
                ConfidentialityPolicyId,
                "Guild Confidentiality Policy",
                OrganizationPolicyCategory.Confidentiality,
                new[]
                {
                    Parameter("visibility", OrganizationPolicyParameterType.EnumValue, required: true, defaultValue: OrganizationVisibility.Restricted.ToString(), allowedValues: new[] { OrganizationVisibility.Public.ToString(), OrganizationVisibility.Restricted.ToString(), OrganizationVisibility.Secret.ToString(), OrganizationVisibility.Hidden.ToString() }),
                    Parameter("reshare_allowed", OrganizationPolicyParameterType.Boolean, required: false, defaultValue: "false")
                },
                new[] { OrganizationPolicyScopeType.EntireOrganization, OrganizationPolicyScopeType.SpecificMembershipType, OrganizationPolicyScopeType.SpecificOffice, OrganizationPolicyScopeType.SpecificSubject },
                OrganizationVisibility.Restricted,
                priority: 200);
            AddPolicy(
                definitions,
                ids,
                BudgetLimitPolicyId,
                "Guild Budget Limit Policy",
                OrganizationPolicyCategory.Budgeting,
                new[]
                {
                    Parameter("currency", OrganizationPolicyParameterType.CurrencyId, required: true, defaultValue: "currency.gold"),
                    Parameter("limit_units", OrganizationPolicyParameterType.Amount, required: true, defaultValue: "100")
                },
                new[] { OrganizationPolicyScopeType.EntireOrganization, OrganizationPolicyScopeType.SpecificTreasury, OrganizationPolicyScopeType.SpecificAccount, OrganizationPolicyScopeType.SpecificAction },
                OrganizationVisibility.Restricted,
                priority: 180);
            return definitions;
        }

        public static IReadOnlyList<OrganizationDecisionProcedureDefinition> CreateMissingProcedureDefinitions(IEnumerable<string> existingIds)
        {
            HashSet<string> ids = Set(existingIds);
            List<OrganizationDecisionProcedureDefinition> definitions = new List<OrganizationDecisionProcedureDefinition>();
            AddProcedure(definitions, ids, SimpleMajorityProcedureId, "Simple Majority Procedure", OrganizationDecisionProcedureKind.SimpleMajority, OrganizationVoterEligibilityKind.ActiveMembers, OrganizationPassageThresholdKind.SimpleMajorityVotesCast, quorum: OrganizationQuorumKind.MinimumCount, quorumCount: 1, thresholdBps: 5000, secret: false, replacement: true);
            AddProcedure(definitions, ids, SecretBallotProcedureId, "Secret Ballot Majority Procedure", OrganizationDecisionProcedureKind.SimpleMajority, OrganizationVoterEligibilityKind.ActiveMembers, OrganizationPassageThresholdKind.SimpleMajorityVotesCast, quorum: OrganizationQuorumKind.MinimumCount, quorumCount: 1, thresholdBps: 5000, secret: true, replacement: true);
            AddProcedure(definitions, ids, EmergencyProcedureId, "Emergency Executive Procedure", OrganizationDecisionProcedureKind.SingleAuthorizedDecision, OrganizationVoterEligibilityKind.AuthorityPermissionHolders, OrganizationPassageThresholdKind.SimpleMajorityVotesCast, quorum: OrganizationQuorumKind.MinimumCount, quorumCount: 1, thresholdBps: 10000, secret: false, replacement: false, veto: true, vetoOverride: true, permissions: new[] { PrototypeOrganizationAuthorityDefinitionFactory.IssueEmergencyDecisionPermissionId });
            return definitions;
        }

        public static IReadOnlyList<OrganizationProposalDefinition> CreateMissingProposalDefinitions(IEnumerable<string> existingIds)
        {
            HashSet<string> ids = Set(existingIds);
            List<OrganizationProposalDefinition> definitions = new List<OrganizationProposalDefinition>();
            AddProposal(definitions, ids, AdoptPolicyProposalId, "Adopt Policy Proposal", OrganizationProposalCategory.AdoptPolicy, PrototypeOrganizationAuthorityDefinitionFactory.SubmitDecisionProposalActionId, SimpleMajorityProcedureId, new[] { OrganizationDecisionExecutionOperationKind.AdoptPolicy });
            AddProposal(definitions, ids, EstablishGoalProposalId, "Establish Goal Proposal", OrganizationProposalCategory.EstablishGoal, PrototypeOrganizationAuthorityDefinitionFactory.SubmitDecisionProposalActionId, SimpleMajorityProcedureId, new[] { OrganizationDecisionExecutionOperationKind.EstablishGoal });
            AddProposal(definitions, ids, ApproveBudgetProposalId, "Approve Budget Proposal", OrganizationProposalCategory.ApproveBudget, PrototypeOrganizationAuthorityDefinitionFactory.SubmitDecisionProposalActionId, SecretBallotProcedureId, new[] { OrganizationDecisionExecutionOperationKind.ApproveBudget, OrganizationDecisionExecutionOperationKind.AuthorizeExpense });
            AddProposal(definitions, ids, EmergencyDecisionProposalId, "Emergency Decision Proposal", OrganizationProposalCategory.Custom, PrototypeOrganizationAuthorityDefinitionFactory.IssueEmergencyDecisionActionId, EmergencyProcedureId, new[] { OrganizationDecisionExecutionOperationKind.AdoptPolicy, OrganizationDecisionExecutionOperationKind.EstablishGoal, OrganizationDecisionExecutionOperationKind.ApproveBudget, OrganizationDecisionExecutionOperationKind.AuthorizeExpense }, duplicateActive: true, visibility: OrganizationVisibility.Secret);
            return definitions;
        }

        private static HashSet<string> Set(IEnumerable<string> ids) => new HashSet<string>((ids ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);

        private static OrganizationPolicyParameterSchemaData Parameter(string id, OrganizationPolicyParameterType type, bool required, string defaultValue = "", IEnumerable<string> allowedValues = null) => new OrganizationPolicyParameterSchemaData
        {
            parameterId = id,
            type = type,
            required = required,
            defaultValue = defaultValue ?? string.Empty,
            allowedValues = OrganizationModelUtility.Clean(allowedValues)
        };

        private static void AddGoal(ICollection<OrganizationGoalDefinition> definitions, ISet<string> ids, string id, string name, OrganizationGoalCategory category, OrganizationGoalProgressSourceKind source, long target, OrganizationGoalCompletionPolicy completion, IEnumerable<string> targetTypes)
        {
            if (ids.Contains(id)) return;
            OrganizationGoalDefinition definition = ScriptableObject.CreateInstance<OrganizationGoalDefinition>();
            definition.name = name;
            definition.DevelopmentConfigure(id, name, category, source, target, completion, targetTypes: targetTypes, organizationCategories: new[] { OrganizationCategory.Guild }, multipleActive: false, goalVisibility: OrganizationVisibility.Restricted, tagIds: Tags());
            definitions.Add(definition);
            ids.Add(id);
        }

        private static void AddPolicy(ICollection<OrganizationPolicyDefinition> definitions, ISet<string> ids, string id, string name, OrganizationPolicyCategory category, IEnumerable<OrganizationPolicyParameterSchemaData> schema, IEnumerable<OrganizationPolicyScopeType> scopes, OrganizationVisibility visibility, int priority)
        {
            if (ids.Contains(id)) return;
            OrganizationPolicyDefinition definition = ScriptableObject.CreateInstance<OrganizationPolicyDefinition>();
            definition.name = name;
            definition.DevelopmentConfigure(id, name, category, schema, scopes, priority, multipleScoped: true, policyVisibility: visibility, tagIds: Tags());
            definitions.Add(definition);
            ids.Add(id);
        }

        private static void AddProcedure(ICollection<OrganizationDecisionProcedureDefinition> definitions, ISet<string> ids, string id, string name, OrganizationDecisionProcedureKind kind, OrganizationVoterEligibilityKind eligibility, OrganizationPassageThresholdKind threshold, OrganizationQuorumKind quorum, int quorumCount, int thresholdBps, bool secret, bool replacement, bool veto = false, bool vetoOverride = false, IEnumerable<string> permissions = null)
        {
            if (ids.Contains(id)) return;
            OrganizationDecisionProcedureDefinition definition = ScriptableObject.CreateInstance<OrganizationDecisionProcedureDefinition>();
            definition.name = name;
            definition.DevelopmentConfigure(id, name, kind, eligibility, threshold, quorum, quorumCount, thresholdBps, snapshotRoll: true, isSecretBallot: secret, voteReplacement: replacement, veto: veto, vetoOverride: vetoOverride, permissions: permissions, procedureVisibility: secret ? OrganizationVisibility.Secret : OrganizationVisibility.Restricted, tagIds: Tags());
            definitions.Add(definition);
            ids.Add(id);
        }

        private static void AddProposal(ICollection<OrganizationProposalDefinition> definitions, ISet<string> ids, string id, string name, OrganizationProposalCategory category, string submitActionId, string procedureId, IEnumerable<OrganizationDecisionExecutionOperationKind> operations, bool duplicateActive = false, OrganizationVisibility visibility = OrganizationVisibility.Restricted)
        {
            if (ids.Contains(id)) return;
            OrganizationProposalDefinition definition = ScriptableObject.CreateInstance<OrganizationProposalDefinition>();
            definition.name = name;
            definition.DevelopmentConfigure(id, name, category, submitActionId, procedureId, operations, canAmend: true, canWithdraw: true, review: -1d, amendment: -1d, voting: 10d, expiration: -1d, duplicateActive: duplicateActive, proposalVisibility: visibility, tagIds: Tags());
            definitions.Add(definition);
            ids.Add(id);
        }

        private static string[] Tags() => new[] { "prototype", "organization", "decisions" };
    }
}
