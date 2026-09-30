using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Factions
{
    [CreateAssetMenu(fileName = "FactionAffiliationDefinition", menuName = "Unity Isekai Game/Factions/Affiliation Definition")]
    public sealed class FactionAffiliationDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string affiliationDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private FactionAffiliationCategory category = FactionAffiliationCategory.FormalMember;
        [SerializeField] private FactionAffiliationConsentPolicy consentPolicy = FactionAffiliationConsentPolicy.ExplicitConsentRequired;
        [SerializeField] private bool publicByDefault = true;
        [SerializeField] private bool simultaneousAllowed;
        [SerializeField] private bool organizationMembershipRequired;
        [SerializeField] private bool votingEligibilitySupported = true;
        [SerializeField] private bool internalRoleEligibilitySupported = true;
        [SerializeField] private bool infiltrationSupported;
        [SerializeField] private bool supportWithoutMembership;
        [SerializeField] private FactionVisibility visibility = FactionVisibility.Public;
        [SerializeField] private string[] requiredCapabilityIds = Array.Empty<string>();
        [SerializeField] private string[] tags = Array.Empty<string>();
        [SerializeField] private int version = 1;

        public string Id => affiliationDefinitionId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public string Description => description ?? string.Empty;
        public FactionAffiliationCategory Category => category;
        public FactionAffiliationConsentPolicy ConsentPolicy => consentPolicy;
        public bool PublicByDefault => publicByDefault;
        public bool SimultaneousAllowed => simultaneousAllowed;
        public bool OrganizationMembershipRequired => organizationMembershipRequired;
        public bool VotingEligibilitySupported => votingEligibilitySupported;
        public bool InternalRoleEligibilitySupported => internalRoleEligibilitySupported;
        public bool InfiltrationSupported => infiltrationSupported;
        public bool SupportWithoutMembership => supportWithoutMembership;
        public FactionVisibility Visibility => visibility;
        public IReadOnlyList<string> RequiredCapabilityIds => FactionModelUtility.Clean(requiredCapabilityIds);
        public IReadOnlyList<string> TagIds => FactionModelUtility.Clean(tags);
        public int Version => Math.Max(1, version);

        public void DevelopmentConfigure(string id, string name, FactionAffiliationCategory affiliationCategory, FactionAffiliationConsentPolicy consent, bool isPublic = true, bool simultaneous = false, bool requiresOrganizationMembership = false, bool voteEligible = true, bool roleEligible = true, bool infiltration = false, bool supportOnly = false, FactionVisibility affiliationVisibility = FactionVisibility.Public, IEnumerable<string> tagIds = null)
        {
            affiliationDefinitionId = Normalize(id);
            displayName = string.IsNullOrWhiteSpace(name) ? affiliationDefinitionId : name.Trim();
            description = string.Empty;
            category = affiliationCategory;
            consentPolicy = consent;
            publicByDefault = isPublic;
            simultaneousAllowed = simultaneous;
            organizationMembershipRequired = requiresOrganizationMembership;
            votingEligibilitySupported = voteEligible;
            internalRoleEligibilitySupported = roleEligible;
            infiltrationSupported = infiltration;
            supportWithoutMembership = supportOnly;
            visibility = affiliationVisibility;
            tags = FactionModelUtility.Clean(tagIds);
            version = 1;
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null) return;
            if (string.IsNullOrWhiteSpace(Id)) report.AddError("Faction Affiliation definition has no stable ID.");
            else if (!Id.StartsWith("faction-affiliation.", StringComparison.Ordinal)) report.AddWarning($"Faction Affiliation definition '{DisplayName}' should use the 'faction-affiliation.' namespace prefix.");
            if (!Enum.IsDefined(typeof(FactionAffiliationCategory), category)) report.AddError($"Faction Affiliation definition '{DisplayName}' has invalid category.");
            if (!Enum.IsDefined(typeof(FactionAffiliationConsentPolicy), consentPolicy)) report.AddError($"Faction Affiliation definition '{DisplayName}' has invalid consent policy.");
            if (!Enum.IsDefined(typeof(FactionVisibility), visibility)) report.AddError($"Faction Affiliation definition '{DisplayName}' has invalid visibility.");
            if (category == FactionAffiliationCategory.Infiltrator && !infiltrationSupported) report.AddError($"Faction Affiliation definition '{DisplayName}' is an infiltrator category but does not support infiltration.");
            if (supportWithoutMembership && consentPolicy == FactionAffiliationConsentPolicy.InvitationThenAcceptance) report.AddWarning($"Faction Affiliation definition '{DisplayName}' is support-only but uses invitation acceptance.");
        }

        private static string Normalize(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    [CreateAssetMenu(fileName = "FactionRoleDefinition", menuName = "Unity Isekai Game/Factions/Role Definition")]
    public class FactionRoleDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string roleDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private FactionRoleCategory category = FactionRoleCategory.Member;
        [SerializeField] private bool leadershipRole;
        [SerializeField] private bool allowsMultipleActiveHolders = true;
        [SerializeField] private bool requiresActiveAffiliation = true;
        [SerializeField] private FactionVisibility visibility = FactionVisibility.Public;
        [SerializeField] private string[] tags = Array.Empty<string>();
        [SerializeField] private int version = 1;

        public string Id => roleDefinitionId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public string Description => description ?? string.Empty;
        public FactionRoleCategory Category => category;
        public bool LeadershipRole => leadershipRole;
        public bool AllowsMultipleActiveHolders => allowsMultipleActiveHolders;
        public bool RequiresActiveAffiliation => requiresActiveAffiliation;
        public FactionVisibility Visibility => visibility;
        public IReadOnlyList<string> TagIds => FactionModelUtility.Clean(tags);
        public int Version => Math.Max(1, version);

        public void DevelopmentConfigure(string id, string name, FactionRoleCategory roleCategory, bool leadership = false, bool multiple = true, bool activeAffiliation = true, FactionVisibility roleVisibility = FactionVisibility.Public, IEnumerable<string> tagIds = null)
        {
            roleDefinitionId = Normalize(id);
            displayName = string.IsNullOrWhiteSpace(name) ? roleDefinitionId : name.Trim();
            description = string.Empty;
            category = roleCategory;
            leadershipRole = leadership;
            allowsMultipleActiveHolders = multiple;
            requiresActiveAffiliation = activeAffiliation;
            visibility = roleVisibility;
            tags = FactionModelUtility.Clean(tagIds);
            version = 1;
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null) return;
            if (string.IsNullOrWhiteSpace(Id)) report.AddError("Faction Role definition has no stable ID.");
            else if (!Id.StartsWith("faction-role.", StringComparison.Ordinal)) report.AddWarning($"Faction Role definition '{DisplayName}' should use the 'faction-role.' namespace prefix.");
            if (!Enum.IsDefined(typeof(FactionRoleCategory), category)) report.AddError($"Faction Role definition '{DisplayName}' has invalid category.");
            if (!Enum.IsDefined(typeof(FactionVisibility), visibility)) report.AddError($"Faction Role definition '{DisplayName}' has invalid visibility.");
        }

        private static string Normalize(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    [CreateAssetMenu(fileName = "FactionPositionDefinition", menuName = "Unity Isekai Game/Factions/Position Definition")]
    public class FactionPositionDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string positionDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField] private FactionPositionTargetKind targetKind = FactionPositionTargetKind.Custom;
        [SerializeField] private FactionPositionStance defaultStance = FactionPositionStance.Neutral;
        [SerializeField] private bool temporaryAllowed = true;
        [SerializeField] private bool internalDisputeAllowed = true;
        [SerializeField] private FactionVisibility visibility = FactionVisibility.Public;
        [SerializeField] private string[] tags = Array.Empty<string>();

        public string Id => positionDefinitionId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public FactionPositionTargetKind TargetKind => targetKind;
        public FactionPositionStance DefaultStance => defaultStance;
        public bool TemporaryAllowed => temporaryAllowed;
        public bool InternalDisputeAllowed => internalDisputeAllowed;
        public FactionVisibility Visibility => visibility;
        public IReadOnlyList<string> TagIds => FactionModelUtility.Clean(tags);

        public void DevelopmentConfigure(string id, string name, FactionPositionTargetKind target, FactionPositionStance stance, bool temporary = true, bool disputes = true, FactionVisibility positionVisibility = FactionVisibility.Public, IEnumerable<string> tagIds = null)
        {
            positionDefinitionId = Normalize(id);
            displayName = string.IsNullOrWhiteSpace(name) ? positionDefinitionId : name.Trim();
            targetKind = target;
            defaultStance = stance;
            temporaryAllowed = temporary;
            internalDisputeAllowed = disputes;
            visibility = positionVisibility;
            tags = FactionModelUtility.Clean(tagIds);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null) return;
            if (string.IsNullOrWhiteSpace(Id)) report.AddError("Faction Position definition has no stable ID.");
            else if (!Id.StartsWith("faction-position.", StringComparison.Ordinal)) report.AddWarning($"Faction Position definition '{DisplayName}' should use the 'faction-position.' namespace prefix.");
            if (!Enum.IsDefined(typeof(FactionPositionTargetKind), targetKind)) report.AddError($"Faction Position definition '{DisplayName}' has invalid target kind.");
            if (!Enum.IsDefined(typeof(FactionPositionStance), defaultStance)) report.AddError($"Faction Position definition '{DisplayName}' has invalid stance.");
        }

        private static string Normalize(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    [CreateAssetMenu(fileName = "FactionAlignmentAxisDefinition", menuName = "Unity Isekai Game/Factions/Alignment Axis Definition")]
    public class FactionAlignmentAxisDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string axisDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField] private int minimumValue = -100;
        [SerializeField] private int maximumValue = 100;
        [SerializeField] private int neutralValue;
        [SerializeField] private string[] tags = Array.Empty<string>();

        public string Id => axisDefinitionId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public int MinimumValue => minimumValue;
        public int MaximumValue => Math.Max(minimumValue, maximumValue);
        public int NeutralValue => Math.Max(MinimumValue, Math.Min(MaximumValue, neutralValue));
        public IReadOnlyList<string> TagIds => FactionModelUtility.Clean(tags);

        public void DevelopmentConfigure(string id, string name, int min = -100, int max = 100, int neutral = 0, IEnumerable<string> tagIds = null)
        {
            axisDefinitionId = Normalize(id);
            displayName = string.IsNullOrWhiteSpace(name) ? axisDefinitionId : name.Trim();
            minimumValue = min;
            maximumValue = Math.Max(min, max);
            neutralValue = Math.Max(MinimumValue, Math.Min(MaximumValue, neutral));
            tags = FactionModelUtility.Clean(tagIds);
        }

        public int Clamp(int value) => Math.Max(MinimumValue, Math.Min(MaximumValue, value));

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null) return;
            if (string.IsNullOrWhiteSpace(Id)) report.AddError("Faction Alignment Axis definition has no stable ID.");
            else if (!Id.StartsWith("faction-axis.", StringComparison.Ordinal)) report.AddWarning($"Faction Alignment Axis definition '{DisplayName}' should use the 'faction-axis.' namespace prefix.");
            if (minimumValue >= maximumValue) report.AddError($"Faction Alignment Axis definition '{DisplayName}' has invalid min/max.");
            if (neutralValue < minimumValue || neutralValue > maximumValue) report.AddError($"Faction Alignment Axis definition '{DisplayName}' has neutral value outside bounds.");
        }

        private static string Normalize(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
