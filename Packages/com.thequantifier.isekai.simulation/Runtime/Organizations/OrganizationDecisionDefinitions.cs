using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Organizations
{
    [Serializable]
    public sealed class OrganizationPolicyParameterSchemaData
    {
        public string parameterId;
        public OrganizationPolicyParameterType type = OrganizationPolicyParameterType.StringIdentifier;
        public bool required;
        public string defaultValue;
        public string[] allowedValues = Array.Empty<string>();

        public OrganizationPolicyParameterSchemaData Clone() => new OrganizationPolicyParameterSchemaData
        {
            parameterId = Normalize(parameterId),
            type = type,
            required = required,
            defaultValue = defaultValue ?? string.Empty,
            allowedValues = OrganizationModelUtility.Clean(allowedValues)
        };

        internal static string Normalize(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    [CreateAssetMenu(fileName = "OrganizationGoalDefinition", menuName = "Unity Isekai Game/Organizations/Goal Definition")]
    public class OrganizationGoalDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string goalDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private OrganizationGoalCategory category = OrganizationGoalCategory.Custom;
        [SerializeField] private string[] validOrganizationDefinitionIds = Array.Empty<string>();
        [SerializeField] private OrganizationCategory[] validOrganizationCategories = Array.Empty<OrganizationCategory>();
        [SerializeField] private string[] targetSubjectTypes = Array.Empty<string>();
        [SerializeField] private OrganizationGoalProgressSourceKind progressSourceKind = OrganizationGoalProgressSourceKind.ExplicitContribution;
        [SerializeField] private long targetValue = 1L;
        [SerializeField] private OrganizationGoalCompletionPolicy completionPolicy = OrganizationGoalCompletionPolicy.ExplicitConfirmation;
        [SerializeField] private bool allowMultipleActiveInstances;
        [SerializeField] private bool suspensionAllowed = true;
        [SerializeField] private bool deadlineAllowed = true;
        [SerializeField] private int minimumPriority;
        [SerializeField] private int maximumPriority = 100;
        [SerializeField] private OrganizationVisibility visibility = OrganizationVisibility.Restricted;
        [SerializeField] private OrganizationAuthorityAuditPolicy auditPolicy = OrganizationAuthorityAuditPolicy.SuccessfulActions;
        [SerializeField] private string[] tags = Array.Empty<string>();
        [SerializeField] private int version = 1;

        public string Id => goalDefinitionId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public string Description => description ?? string.Empty;
        public OrganizationGoalCategory Category => category;
        public IReadOnlyList<string> ValidOrganizationDefinitionIds => OrganizationModelUtility.Clean(validOrganizationDefinitionIds);
        public IReadOnlyList<OrganizationCategory> ValidOrganizationCategories => CleanCategories(validOrganizationCategories);
        public IReadOnlyList<string> TargetSubjectTypes => OrganizationModelUtility.Clean(targetSubjectTypes);
        public OrganizationGoalProgressSourceKind ProgressSourceKind => progressSourceKind;
        public long TargetValue => Math.Max(0L, targetValue);
        public OrganizationGoalCompletionPolicy CompletionPolicy => completionPolicy;
        public bool AllowMultipleActiveInstances => allowMultipleActiveInstances;
        public bool SuspensionAllowed => suspensionAllowed;
        public bool DeadlineAllowed => deadlineAllowed;
        public int MinimumPriority => minimumPriority;
        public int MaximumPriority => Math.Max(minimumPriority, maximumPriority);
        public OrganizationVisibility Visibility => visibility;
        public OrganizationAuthorityAuditPolicy AuditPolicy => auditPolicy;
        public IReadOnlyList<string> TagIds => OrganizationModelUtility.Clean(tags);
        public int Version => Math.Max(1, version);

        public void DevelopmentConfigure(string id, string name, OrganizationGoalCategory goalCategory, OrganizationGoalProgressSourceKind sourceKind, long target, OrganizationGoalCompletionPolicy completion = OrganizationGoalCompletionPolicy.ExplicitConfirmation, IEnumerable<string> targetTypes = null, IEnumerable<string> organizationDefinitions = null, IEnumerable<OrganizationCategory> organizationCategories = null, bool multipleActive = false, bool allowSuspension = true, bool allowDeadline = true, OrganizationVisibility goalVisibility = OrganizationVisibility.Restricted, IEnumerable<string> tagIds = null)
        {
            goalDefinitionId = Normalize(id);
            displayName = string.IsNullOrWhiteSpace(name) ? id : name.Trim();
            description = string.Empty;
            category = goalCategory;
            progressSourceKind = sourceKind;
            targetValue = Math.Max(0L, target);
            completionPolicy = completion;
            targetSubjectTypes = OrganizationModelUtility.Clean(targetTypes).ToArray();
            validOrganizationDefinitionIds = OrganizationModelUtility.Clean(organizationDefinitions).ToArray();
            validOrganizationCategories = CleanCategories(organizationCategories).ToArray();
            allowMultipleActiveInstances = multipleActive;
            suspensionAllowed = allowSuspension;
            deadlineAllowed = allowDeadline;
            visibility = goalVisibility;
            auditPolicy = OrganizationAuthorityAuditPolicy.SuccessfulActions;
            tags = OrganizationModelUtility.Clean(tagIds).ToArray();
            version = 1;
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null) return;
            if (string.IsNullOrWhiteSpace(Id)) report.AddError("Organization Goal definition has no stable ID.");
            else if (!Id.StartsWith("organization-goal.", StringComparison.Ordinal)) report.AddWarning($"Organization Goal definition '{DisplayName}' should use the 'organization-goal.' namespace prefix.");
            if (!Enum.IsDefined(typeof(OrganizationGoalCategory), category) || category == OrganizationGoalCategory.Unknown) report.AddError($"Organization Goal definition '{DisplayName}' has invalid category.");
            if (!Enum.IsDefined(typeof(OrganizationGoalProgressSourceKind), progressSourceKind) || progressSourceKind == OrganizationGoalProgressSourceKind.Unknown) report.AddError($"Organization Goal definition '{DisplayName}' has invalid progress source.");
            if (!Enum.IsDefined(typeof(OrganizationGoalCompletionPolicy), completionPolicy) || completionPolicy == OrganizationGoalCompletionPolicy.Unknown) report.AddError($"Organization Goal definition '{DisplayName}' has invalid completion policy.");
            if (TargetValue <= 0L) report.AddError($"Organization Goal definition '{DisplayName}' must declare a positive target value.");
            if (MinimumPriority > MaximumPriority) report.AddError($"Organization Goal definition '{DisplayName}' has an invalid priority range.");
            ValidateOrganizationReferences(DisplayName, "Organization Goal", ValidOrganizationDefinitionIds, definitionsById, report);
        }

        private static string Normalize(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        private static OrganizationCategory[] CleanCategories(IEnumerable<OrganizationCategory> values) => (values ?? Array.Empty<OrganizationCategory>()).Where(value => value != OrganizationCategory.Unknown).Distinct().OrderBy(value => value.ToString(), StringComparer.Ordinal).ToArray();
        private static void ValidateOrganizationReferences(string name, string label, IEnumerable<string> ids, IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            foreach (string id in ids ?? Array.Empty<string>())
            {
                if (definitionsById == null || !definitionsById.TryGetValue(id, out IGameDefinition definition) || definition is not OrganizationDefinition) report.AddError($"{label} definition '{name}' references missing Organization Definition '{id}'.");
            }
        }
    }

    [CreateAssetMenu(fileName = "OrganizationPolicyDefinition", menuName = "Unity Isekai Game/Organizations/Policy Definition")]
    public class OrganizationPolicyDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string policyDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private OrganizationPolicyCategory category = OrganizationPolicyCategory.Custom;
        [SerializeField] private string[] validOrganizationDefinitionIds = Array.Empty<string>();
        [SerializeField] private OrganizationCategory[] validOrganizationCategories = Array.Empty<OrganizationCategory>();
        [SerializeField] private OrganizationPolicyParameterSchemaData[] parameterSchema = Array.Empty<OrganizationPolicyParameterSchemaData>();
        [SerializeField] private OrganizationPolicyScopeType[] allowedScopes = Array.Empty<OrganizationPolicyScopeType>();
        [SerializeField] private bool allowMultipleScopedInstances = true;
        [SerializeField] private bool amendmentAllowed = true;
        [SerializeField] private bool revocationAllowed = true;
        [SerializeField] private bool expirationAllowed = true;
        [SerializeField] private int priority = 100;
        [SerializeField] private OrganizationVisibility visibility = OrganizationVisibility.Restricted;
        [SerializeField] private string[] tags = Array.Empty<string>();
        [SerializeField] private int version = 1;

        public string Id => policyDefinitionId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public string Description => description ?? string.Empty;
        public OrganizationPolicyCategory Category => category;
        public IReadOnlyList<string> ValidOrganizationDefinitionIds => OrganizationModelUtility.Clean(validOrganizationDefinitionIds);
        public IReadOnlyList<OrganizationCategory> ValidOrganizationCategories => CleanCategories(validOrganizationCategories);
        public IReadOnlyList<OrganizationPolicyParameterSchemaData> ParameterSchema => (parameterSchema ?? Array.Empty<OrganizationPolicyParameterSchemaData>()).Where(item => item != null).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<OrganizationPolicyScopeType> AllowedScopes => CleanScopes(allowedScopes);
        public bool AllowMultipleScopedInstances => allowMultipleScopedInstances;
        public bool AmendmentAllowed => amendmentAllowed;
        public bool RevocationAllowed => revocationAllowed;
        public bool ExpirationAllowed => expirationAllowed;
        public int Priority => priority;
        public OrganizationVisibility Visibility => visibility;
        public IReadOnlyList<string> TagIds => OrganizationModelUtility.Clean(tags);
        public int Version => Math.Max(1, version);

        public void DevelopmentConfigure(string id, string name, OrganizationPolicyCategory policyCategory, IEnumerable<OrganizationPolicyParameterSchemaData> schema = null, IEnumerable<OrganizationPolicyScopeType> scopes = null, int policyPriority = 100, bool multipleScoped = true, bool canAmend = true, bool canRevoke = true, bool canExpire = true, OrganizationVisibility policyVisibility = OrganizationVisibility.Restricted, IEnumerable<string> tagIds = null)
        {
            policyDefinitionId = Normalize(id);
            displayName = string.IsNullOrWhiteSpace(name) ? id : name.Trim();
            description = string.Empty;
            category = policyCategory;
            parameterSchema = (schema ?? Array.Empty<OrganizationPolicyParameterSchemaData>()).Where(item => item != null).Select(item => item.Clone()).ToArray();
            allowedScopes = CleanScopes(scopes).ToArray();
            priority = policyPriority;
            allowMultipleScopedInstances = multipleScoped;
            amendmentAllowed = canAmend;
            revocationAllowed = canRevoke;
            expirationAllowed = canExpire;
            visibility = policyVisibility;
            tags = OrganizationModelUtility.Clean(tagIds).ToArray();
            version = 1;
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null) return;
            if (string.IsNullOrWhiteSpace(Id)) report.AddError("Organization Policy definition has no stable ID.");
            else if (!Id.StartsWith("organization-policy.", StringComparison.Ordinal)) report.AddWarning($"Organization Policy definition '{DisplayName}' should use the 'organization-policy.' namespace prefix.");
            if (!Enum.IsDefined(typeof(OrganizationPolicyCategory), category) || category == OrganizationPolicyCategory.Unknown) report.AddError($"Organization Policy definition '{DisplayName}' has invalid category.");
            if (AllowedScopes.Count == 0) report.AddError($"Organization Policy definition '{DisplayName}' must declare at least one allowed scope.");
            string duplicate = ParameterSchema.GroupBy(item => item.parameterId, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1)?.Key;
            if (!string.IsNullOrWhiteSpace(duplicate)) report.AddError($"Organization Policy definition '{DisplayName}' has duplicate parameter '{duplicate}'.");
            foreach (OrganizationPolicyParameterSchemaData parameter in ParameterSchema)
            {
                if (string.IsNullOrWhiteSpace(parameter.parameterId)) report.AddError($"Organization Policy definition '{DisplayName}' has a parameter without a stable ID.");
                if (!Enum.IsDefined(typeof(OrganizationPolicyParameterType), parameter.type) || parameter.type == OrganizationPolicyParameterType.Unknown) report.AddError($"Organization Policy definition '{DisplayName}' parameter '{parameter.parameterId}' has invalid type.");
            }
            ValidateOrganizationReferences(DisplayName, "Organization Policy", ValidOrganizationDefinitionIds, definitionsById, report);
        }

        private static string Normalize(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        private static OrganizationCategory[] CleanCategories(IEnumerable<OrganizationCategory> values) => (values ?? Array.Empty<OrganizationCategory>()).Where(value => value != OrganizationCategory.Unknown).Distinct().OrderBy(value => value.ToString(), StringComparer.Ordinal).ToArray();
        private static OrganizationPolicyScopeType[] CleanScopes(IEnumerable<OrganizationPolicyScopeType> values) => (values ?? Array.Empty<OrganizationPolicyScopeType>()).Where(value => value != OrganizationPolicyScopeType.Unknown).Distinct().OrderBy(value => value.ToString(), StringComparer.Ordinal).ToArray();
        private static void ValidateOrganizationReferences(string name, string label, IEnumerable<string> ids, IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            foreach (string id in ids ?? Array.Empty<string>())
            {
                if (definitionsById == null || !definitionsById.TryGetValue(id, out IGameDefinition definition) || definition is not OrganizationDefinition) report.AddError($"{label} definition '{name}' references missing Organization Definition '{id}'.");
            }
        }
    }

    [CreateAssetMenu(fileName = "OrganizationDecisionProcedureDefinition", menuName = "Unity Isekai Game/Organizations/Decision Procedure Definition")]
    public class OrganizationDecisionProcedureDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string procedureDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private OrganizationDecisionProcedureKind kind = OrganizationDecisionProcedureKind.SimpleMajority;
        [SerializeField] private OrganizationVoterEligibilityKind voterEligibility = OrganizationVoterEligibilityKind.ActiveMembers;
        [SerializeField] private OrganizationVoteWeightKind voteWeight = OrganizationVoteWeightKind.OnePersonOneVote;
        [SerializeField] private OrganizationQuorumKind quorumKind = OrganizationQuorumKind.MinimumCount;
        [SerializeField] private int quorumCount = 1;
        [SerializeField] private int quorumPercentageBasisPoints = 5000;
        [SerializeField] private OrganizationPassageThresholdKind thresholdKind = OrganizationPassageThresholdKind.SimpleMajorityVotesCast;
        [SerializeField] private int thresholdBasisPoints = 5000;
        [SerializeField] private long fixedWeightThreshold;
        [SerializeField] private OrganizationTiePolicy tiePolicy = OrganizationTiePolicy.Fail;
        [SerializeField] private bool snapshotVoterRoll = true;
        [SerializeField] private bool secretBallot;
        [SerializeField] private bool allowVoteReplacement;
        [SerializeField] private bool allowVeto;
        [SerializeField] private bool allowOverride;
        [SerializeField] private int overrideThresholdBasisPoints = 6667;
        [SerializeField] private double vetoWindowDuration = -1d;
        [SerializeField] private double emergencyDurationLimit = -1d;
        [SerializeField] private string[] eligibleOfficeDefinitionIds = Array.Empty<string>();
        [SerializeField] private string[] eligibleRankDefinitionIds = Array.Empty<string>();
        [SerializeField] private string[] eligiblePermissionDefinitionIds = Array.Empty<string>();
        [SerializeField] private OrganizationVisibility visibility = OrganizationVisibility.Restricted;
        [SerializeField] private string[] tags = Array.Empty<string>();
        [SerializeField] private int version = 1;

        public string Id => procedureDefinitionId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public string Description => description ?? string.Empty;
        public OrganizationDecisionProcedureKind Kind => kind;
        public OrganizationVoterEligibilityKind VoterEligibility => voterEligibility;
        public OrganizationVoteWeightKind VoteWeight => voteWeight;
        public OrganizationQuorumKind QuorumKind => quorumKind;
        public int QuorumCount => Math.Max(0, quorumCount);
        public int QuorumPercentageBasisPoints => Math.Max(0, Math.Min(10000, quorumPercentageBasisPoints));
        public OrganizationPassageThresholdKind ThresholdKind => thresholdKind;
        public int ThresholdBasisPoints => Math.Max(0, Math.Min(10000, thresholdBasisPoints));
        public long FixedWeightThreshold => Math.Max(0L, fixedWeightThreshold);
        public OrganizationTiePolicy TiePolicy => tiePolicy;
        public bool SnapshotVoterRoll => snapshotVoterRoll;
        public bool SecretBallot => secretBallot;
        public bool AllowVoteReplacement => allowVoteReplacement;
        public bool AllowVeto => allowVeto;
        public bool AllowOverride => allowOverride;
        public int OverrideThresholdBasisPoints => Math.Max(0, Math.Min(10000, overrideThresholdBasisPoints));
        public double VetoWindowDuration => vetoWindowDuration;
        public double EmergencyDurationLimit => emergencyDurationLimit;
        public IReadOnlyList<string> EligibleOfficeDefinitionIds => OrganizationModelUtility.Clean(eligibleOfficeDefinitionIds);
        public IReadOnlyList<string> EligibleRankDefinitionIds => OrganizationModelUtility.Clean(eligibleRankDefinitionIds);
        public IReadOnlyList<string> EligiblePermissionDefinitionIds => OrganizationModelUtility.Clean(eligiblePermissionDefinitionIds);
        public OrganizationVisibility Visibility => visibility;
        public IReadOnlyList<string> TagIds => OrganizationModelUtility.Clean(tags);
        public int Version => Math.Max(1, version);

        public void DevelopmentConfigure(string id, string name, OrganizationDecisionProcedureKind procedureKind, OrganizationVoterEligibilityKind eligibility, OrganizationPassageThresholdKind passage, OrganizationQuorumKind quorum = OrganizationQuorumKind.MinimumCount, int minimumQuorum = 1, int thresholdBps = 5000, bool snapshotRoll = true, bool isSecretBallot = false, bool voteReplacement = false, bool veto = false, bool vetoOverride = false, IEnumerable<string> permissions = null, IEnumerable<string> offices = null, IEnumerable<string> ranks = null, OrganizationVisibility procedureVisibility = OrganizationVisibility.Restricted, IEnumerable<string> tagIds = null)
        {
            procedureDefinitionId = Normalize(id);
            displayName = string.IsNullOrWhiteSpace(name) ? id : name.Trim();
            description = string.Empty;
            kind = procedureKind;
            voterEligibility = eligibility;
            voteWeight = OrganizationVoteWeightKind.OnePersonOneVote;
            quorumKind = quorum;
            quorumCount = Math.Max(0, minimumQuorum);
            quorumPercentageBasisPoints = 5000;
            thresholdKind = passage;
            thresholdBasisPoints = Math.Max(0, Math.Min(10000, thresholdBps));
            fixedWeightThreshold = 0L;
            tiePolicy = OrganizationTiePolicy.Fail;
            snapshotVoterRoll = snapshotRoll;
            secretBallot = isSecretBallot;
            allowVoteReplacement = voteReplacement;
            allowVeto = veto;
            allowOverride = vetoOverride;
            eligiblePermissionDefinitionIds = OrganizationModelUtility.Clean(permissions).ToArray();
            eligibleOfficeDefinitionIds = OrganizationModelUtility.Clean(offices).ToArray();
            eligibleRankDefinitionIds = OrganizationModelUtility.Clean(ranks).ToArray();
            visibility = procedureVisibility;
            tags = OrganizationModelUtility.Clean(tagIds).ToArray();
            version = 1;
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null) return;
            if (string.IsNullOrWhiteSpace(Id)) report.AddError("Organization Decision Procedure definition has no stable ID.");
            else if (!Id.StartsWith("organization-decision-procedure.", StringComparison.Ordinal)) report.AddWarning($"Organization Decision Procedure definition '{DisplayName}' should use the 'organization-decision-procedure.' namespace prefix.");
            if (!Enum.IsDefined(typeof(OrganizationDecisionProcedureKind), kind) || kind == OrganizationDecisionProcedureKind.Unknown) report.AddError($"Organization Decision Procedure definition '{DisplayName}' has invalid kind.");
            if (!Enum.IsDefined(typeof(OrganizationVoterEligibilityKind), voterEligibility) || voterEligibility == OrganizationVoterEligibilityKind.Unknown) report.AddError($"Organization Decision Procedure definition '{DisplayName}' has invalid voter eligibility.");
            if (!Enum.IsDefined(typeof(OrganizationPassageThresholdKind), thresholdKind) || thresholdKind == OrganizationPassageThresholdKind.Unknown) report.AddError($"Organization Decision Procedure definition '{DisplayName}' has invalid passage threshold.");
            if (quorumKind != OrganizationQuorumKind.None && QuorumCount == 0 && QuorumPercentageBasisPoints == 0) report.AddError($"Organization Decision Procedure definition '{DisplayName}' has an invalid quorum.");
            if ((thresholdKind == OrganizationPassageThresholdKind.FixedWeightedThreshold && FixedWeightThreshold <= 0L) || ThresholdBasisPoints > 10000) report.AddError($"Organization Decision Procedure definition '{DisplayName}' has an invalid threshold.");
            foreach (string permissionId in EligiblePermissionDefinitionIds)
            {
                if (definitionsById == null || !definitionsById.TryGetValue(permissionId, out IGameDefinition definition) || definition is not OrganizationPermissionDefinition) report.AddError($"Organization Decision Procedure definition '{DisplayName}' references missing Permission '{permissionId}'.");
            }
        }

        private static string Normalize(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    [CreateAssetMenu(fileName = "OrganizationProposalDefinition", menuName = "Unity Isekai Game/Organizations/Proposal Definition")]
    public class OrganizationProposalDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string proposalDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private OrganizationProposalCategory category = OrganizationProposalCategory.Custom;
        [SerializeField] private string requiredSubmitActionDefinitionId;
        [SerializeField] private string decisionProcedureDefinitionId;
        [SerializeField] private bool amendmentAllowed = true;
        [SerializeField] private bool withdrawalAllowed = true;
        [SerializeField] private double reviewDuration = -1d;
        [SerializeField] private double amendmentDuration = -1d;
        [SerializeField] private double votingDuration = 10d;
        [SerializeField] private double expirationDuration = -1d;
        [SerializeField] private bool duplicateActiveProposalsAllowed;
        [SerializeField] private OrganizationDecisionExecutionOperationKind[] supportedExecutionOperations = Array.Empty<OrganizationDecisionExecutionOperationKind>();
        [SerializeField] private OrganizationVisibility visibility = OrganizationVisibility.Restricted;
        [SerializeField] private string[] tags = Array.Empty<string>();
        [SerializeField] private int version = 1;

        public string Id => proposalDefinitionId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public string Description => description ?? string.Empty;
        public OrganizationProposalCategory Category => category;
        public string RequiredSubmitActionDefinitionId => requiredSubmitActionDefinitionId ?? string.Empty;
        public string DecisionProcedureDefinitionId => decisionProcedureDefinitionId ?? string.Empty;
        public bool AmendmentAllowed => amendmentAllowed;
        public bool WithdrawalAllowed => withdrawalAllowed;
        public double ReviewDuration => reviewDuration;
        public double AmendmentDuration => amendmentDuration;
        public double VotingDuration => votingDuration;
        public double ExpirationDuration => expirationDuration;
        public bool DuplicateActiveProposalsAllowed => duplicateActiveProposalsAllowed;
        public IReadOnlyList<OrganizationDecisionExecutionOperationKind> SupportedExecutionOperations => (supportedExecutionOperations ?? Array.Empty<OrganizationDecisionExecutionOperationKind>()).Where(value => value != OrganizationDecisionExecutionOperationKind.Unknown).Distinct().OrderBy(value => (int)value).ToArray();
        public OrganizationVisibility Visibility => visibility;
        public IReadOnlyList<string> TagIds => OrganizationModelUtility.Clean(tags);
        public int Version => Math.Max(1, version);

        public void DevelopmentConfigure(string id, string name, OrganizationProposalCategory proposalCategory, string submitActionDefinitionId, string procedureDefinitionId, IEnumerable<OrganizationDecisionExecutionOperationKind> executionOperations = null, bool canAmend = true, bool canWithdraw = true, double review = -1d, double amendment = -1d, double voting = 10d, double expiration = -1d, bool duplicateActive = false, OrganizationVisibility proposalVisibility = OrganizationVisibility.Restricted, IEnumerable<string> tagIds = null)
        {
            proposalDefinitionId = Normalize(id);
            displayName = string.IsNullOrWhiteSpace(name) ? id : name.Trim();
            description = string.Empty;
            category = proposalCategory;
            requiredSubmitActionDefinitionId = Normalize(submitActionDefinitionId);
            decisionProcedureDefinitionId = Normalize(procedureDefinitionId);
            supportedExecutionOperations = (executionOperations ?? Array.Empty<OrganizationDecisionExecutionOperationKind>()).Where(value => value != OrganizationDecisionExecutionOperationKind.Unknown).Distinct().OrderBy(value => (int)value).ToArray();
            amendmentAllowed = canAmend;
            withdrawalAllowed = canWithdraw;
            reviewDuration = review;
            amendmentDuration = amendment;
            votingDuration = voting;
            expirationDuration = expiration;
            duplicateActiveProposalsAllowed = duplicateActive;
            visibility = proposalVisibility;
            tags = OrganizationModelUtility.Clean(tagIds).ToArray();
            version = 1;
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null) return;
            if (string.IsNullOrWhiteSpace(Id)) report.AddError("Organization Proposal definition has no stable ID.");
            else if (!Id.StartsWith("organization-proposal.", StringComparison.Ordinal)) report.AddWarning($"Organization Proposal definition '{DisplayName}' should use the 'organization-proposal.' namespace prefix.");
            if (!Enum.IsDefined(typeof(OrganizationProposalCategory), category) || category == OrganizationProposalCategory.Unknown) report.AddError($"Organization Proposal definition '{DisplayName}' has invalid category.");
            if (string.IsNullOrWhiteSpace(DecisionProcedureDefinitionId) || definitionsById == null || !definitionsById.TryGetValue(DecisionProcedureDefinitionId, out IGameDefinition procedure) || procedure is not OrganizationDecisionProcedureDefinition) report.AddError($"Organization Proposal definition '{DisplayName}' references missing decision procedure '{DecisionProcedureDefinitionId}'.");
            if (!string.IsNullOrWhiteSpace(RequiredSubmitActionDefinitionId) && (definitionsById == null || !definitionsById.TryGetValue(RequiredSubmitActionDefinitionId, out IGameDefinition action) || action is not InstitutionalActionDefinition)) report.AddError($"Organization Proposal definition '{DisplayName}' references missing submit action '{RequiredSubmitActionDefinitionId}'.");
            if (VotingDuration == 0d) report.AddError($"Organization Proposal definition '{DisplayName}' has a zero-length voting window.");
        }

        private static string Normalize(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
