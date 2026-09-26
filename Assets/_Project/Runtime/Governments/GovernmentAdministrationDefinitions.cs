using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Economy.InstitutionalRevenue;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Governments
{
    [Serializable]
    public struct GovernmentLegitimacyWeight
    {
        public GovernmentLegitimacyComponent component;
        [Range(0, 10000)] public int weightBasisPoints;
    }

    [CreateAssetMenu(fileName = "GovernmentOfficeDefinition", menuName = "Unity Isekai Game/Governments/Office Definition")]
    public class GovernmentOfficeDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string officeDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private string organizationOfficeDefinitionId;
        [SerializeField] private GovernmentInstitutionRoleCategory institutionRole = GovernmentInstitutionRoleCategory.Executive;
        [SerializeField] private GovernmentAuthorityTier authorityTier;
        [SerializeField] private string superiorOfficeDefinitionId;
        [SerializeField] private GovernmentOfficeEligibilityRule eligibilityRule;
        [SerializeField] private GovernmentOfficeSelectionMethod[] allowedSelectionMethods = Array.Empty<GovernmentOfficeSelectionMethod>();
        [SerializeField] private bool headOfGovernment;
        [SerializeField] private bool requiresConfirmation;
        [SerializeField] private bool permitsActingHolder = true;
        [SerializeField] private double defaultTermDuration = -1d;
        [SerializeField] private string authorityRoleDefinitionId;
        [SerializeField] private string[] tags = Array.Empty<string>();
        [SerializeField] private int version = 1;

        public string Id => officeDefinitionId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public string OrganizationOfficeDefinitionId => organizationOfficeDefinitionId ?? string.Empty;
        public GovernmentInstitutionRoleCategory InstitutionRole => institutionRole;
        public GovernmentAuthorityTier AuthorityTier => authorityTier;
        public string SuperiorOfficeDefinitionId => superiorOfficeDefinitionId ?? string.Empty;
        public GovernmentOfficeEligibilityRule EligibilityRule => eligibilityRule;
        public IReadOnlyList<GovernmentOfficeSelectionMethod> AllowedSelectionMethods => allowedSelectionMethods ?? Array.Empty<GovernmentOfficeSelectionMethod>();
        public bool HeadOfGovernment => headOfGovernment;
        public bool RequiresConfirmation => requiresConfirmation;
        public bool PermitsActingHolder => permitsActingHolder;
        public double DefaultTermDuration => defaultTermDuration;
        public string AuthorityRoleDefinitionId => authorityRoleDefinitionId ?? string.Empty;
        public int Version => Math.Max(1, version);

        public void DevelopmentConfigure(string id, string name, string organizationOfficeId, GovernmentInstitutionRoleCategory role, IEnumerable<GovernmentOfficeSelectionMethod> methods, bool isHead = false, bool confirmation = false, bool acting = true, double termDuration = -1d, string authorityRoleId = "", IEnumerable<string> tagIds = null, GovernmentAuthorityTier tier = GovernmentAuthorityTier.Unknown, string superiorOfficeId = "", GovernmentOfficeEligibilityRule eligibility = GovernmentOfficeEligibilityRule.None)
        {
            officeDefinitionId = PoliticalModelUtility.Normalize(id);
            displayName = string.IsNullOrWhiteSpace(name) ? officeDefinitionId : name.Trim();
            description = string.Empty;
            organizationOfficeDefinitionId = PoliticalModelUtility.Normalize(organizationOfficeId);
            institutionRole = role;
            authorityTier = tier;
            superiorOfficeDefinitionId = PoliticalModelUtility.Normalize(superiorOfficeId);
            eligibilityRule = eligibility;
            allowedSelectionMethods = (methods ?? Array.Empty<GovernmentOfficeSelectionMethod>()).Where(value => value != GovernmentOfficeSelectionMethod.Unknown).Distinct().ToArray();
            headOfGovernment = isHead;
            requiresConfirmation = confirmation;
            permitsActingHolder = acting;
            defaultTermDuration = termDuration;
            authorityRoleDefinitionId = PoliticalModelUtility.Normalize(authorityRoleId);
            tags = PoliticalModelUtility.Clean(tagIds);
            version = 1;
        }

        public bool Allows(GovernmentOfficeSelectionMethod method) => AllowedSelectionMethods.Contains(method);

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null) return;
            if (string.IsNullOrWhiteSpace(Id)) report.AddError("Government office definition has no stable ID.");
            if (string.IsNullOrWhiteSpace(OrganizationOfficeDefinitionId) || !definitionsById.ContainsKey(OrganizationOfficeDefinitionId)) report.AddError($"Government office definition '{DisplayName}' references missing organization office definition '{OrganizationOfficeDefinitionId}'.");
            if (AllowedSelectionMethods.Count == 0) report.AddError($"Government office definition '{DisplayName}' has no permitted selection method.");
            if (authorityTier == GovernmentAuthorityTier.Unknown) report.AddError($"Government office definition '{DisplayName}' has no authority tier.");
            if (!string.IsNullOrWhiteSpace(SuperiorOfficeDefinitionId))
            {
                if (!definitionsById.TryGetValue(SuperiorOfficeDefinitionId, out IGameDefinition superior) || superior is not GovernmentOfficeDefinition superiorOffice) report.AddError($"Government office definition '{DisplayName}' references missing superior office '{SuperiorOfficeDefinitionId}'.");
                else if (superiorOffice.AuthorityTier < AuthorityTier) report.AddError($"Government office definition '{DisplayName}' cannot report to the lower-tier office '{superiorOffice.DisplayName}'.");
            }
            if (!string.IsNullOrWhiteSpace(AuthorityRoleDefinitionId) && !definitionsById.ContainsKey(AuthorityRoleDefinitionId)) report.AddError($"Government office definition '{DisplayName}' references missing authority role '{AuthorityRoleDefinitionId}'.");
        }
    }

    [CreateAssetMenu(fileName = "GovernmentPermitDefinition", menuName = "Unity Isekai Game/Governments/Permit Definition")]
    public class GovernmentPermitDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string permitDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private GovernmentPermitCategory category = GovernmentPermitCategory.TradeLicense;
        [SerializeField] private GovernmentPermitHolderCategory[] allowedHolderCategories = Array.Empty<GovernmentPermitHolderCategory>();
        [SerializeField] private string[] permittedActionIds = Array.Empty<string>();
        [SerializeField] private bool renewable = true;
        [SerializeField] private bool revocable = true;
        [SerializeField] private double defaultDuration = -1d;
        [SerializeField] private string feeRevenueDefinitionId;
        [SerializeField] private string[] tags = Array.Empty<string>();
        [SerializeField] private int version = 1;

        public string Id => permitDefinitionId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public GovernmentPermitCategory Category => category;
        public IReadOnlyList<GovernmentPermitHolderCategory> AllowedHolderCategories => allowedHolderCategories ?? Array.Empty<GovernmentPermitHolderCategory>();
        public IReadOnlyList<string> PermittedActionIds => PoliticalModelUtility.Clean(permittedActionIds);
        public bool Renewable => renewable;
        public bool Revocable => revocable;
        public double DefaultDuration => defaultDuration;
        public string FeeRevenueDefinitionId => feeRevenueDefinitionId ?? string.Empty;
        public int Version => Math.Max(1, version);

        public void DevelopmentConfigure(string id, string name, GovernmentPermitCategory permitCategory, IEnumerable<GovernmentPermitHolderCategory> holderCategories, IEnumerable<string> actionIds, double duration = -1d, bool canRenew = true, bool canRevoke = true, string feeDefinitionId = "", IEnumerable<string> tagIds = null)
        {
            permitDefinitionId = PoliticalModelUtility.Normalize(id);
            displayName = string.IsNullOrWhiteSpace(name) ? permitDefinitionId : name.Trim();
            description = string.Empty;
            category = permitCategory;
            allowedHolderCategories = (holderCategories ?? Array.Empty<GovernmentPermitHolderCategory>()).Where(value => value != GovernmentPermitHolderCategory.Unknown).Distinct().ToArray();
            permittedActionIds = PoliticalModelUtility.Clean(actionIds);
            defaultDuration = duration;
            renewable = canRenew;
            revocable = canRevoke;
            feeRevenueDefinitionId = PoliticalModelUtility.Normalize(feeDefinitionId);
            tags = PoliticalModelUtility.Clean(tagIds);
            version = 1;
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null) return;
            if (string.IsNullOrWhiteSpace(Id)) report.AddError("Government permit definition has no stable ID.");
            if (category == GovernmentPermitCategory.Unknown) report.AddError($"Government permit definition '{DisplayName}' has no category.");
            if (AllowedHolderCategories.Count == 0) report.AddError($"Government permit definition '{DisplayName}' has no permitted holder category.");
            if (PermittedActionIds.Count == 0) report.AddError($"Government permit definition '{DisplayName}' grants no action.");
            if (!string.IsNullOrWhiteSpace(FeeRevenueDefinitionId) && !definitionsById.ContainsKey(FeeRevenueDefinitionId)) report.AddError($"Government permit definition '{DisplayName}' references missing fee definition '{FeeRevenueDefinitionId}'.");
        }
    }

    [CreateAssetMenu(fileName = "GovernmentLegitimacyPolicyDefinition", menuName = "Unity Isekai Game/Governments/Legitimacy Policy Definition")]
    public class GovernmentLegitimacyPolicyDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string policyDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField] private GovernmentCategory[] governmentCategories = Array.Empty<GovernmentCategory>();
        [SerializeField] private GovernmentLegitimacyWeight[] weights = Array.Empty<GovernmentLegitimacyWeight>();
        [SerializeField] private int contestedThreshold = 2000;
        [SerializeField] private int fragileThreshold = 4000;
        [SerializeField] private int acceptedThreshold = 6000;
        [SerializeField] private int strongThreshold = 8000;
        [SerializeField] private int reveredThreshold = 9500;
        [SerializeField] private int version = 1;

        public string Id => policyDefinitionId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public IReadOnlyList<GovernmentCategory> GovernmentCategories => governmentCategories ?? Array.Empty<GovernmentCategory>();
        public IReadOnlyList<GovernmentLegitimacyWeight> Weights => weights ?? Array.Empty<GovernmentLegitimacyWeight>();
        public int Version => Math.Max(1, version);

        public void DevelopmentConfigure(string id, string name, IEnumerable<GovernmentCategory> categories, IEnumerable<GovernmentLegitimacyWeight> componentWeights)
        {
            policyDefinitionId = PoliticalModelUtility.Normalize(id);
            displayName = string.IsNullOrWhiteSpace(name) ? policyDefinitionId : name.Trim();
            governmentCategories = (categories ?? Array.Empty<GovernmentCategory>()).Where(value => value != GovernmentCategory.Unknown).Distinct().ToArray();
            weights = (componentWeights ?? Array.Empty<GovernmentLegitimacyWeight>()).Where(value => value.component != GovernmentLegitimacyComponent.Unknown && value.weightBasisPoints > 0).ToArray();
            version = 1;
        }

        public GovernmentLegitimacyBand GetBand(int score)
        {
            score = Math.Max(0, Math.Min(10000, score));
            if (score >= reveredThreshold) return GovernmentLegitimacyBand.Revered;
            if (score >= strongThreshold) return GovernmentLegitimacyBand.Strong;
            if (score >= acceptedThreshold) return GovernmentLegitimacyBand.Accepted;
            if (score >= fragileThreshold) return GovernmentLegitimacyBand.Fragile;
            if (score >= contestedThreshold) return GovernmentLegitimacyBand.Contested;
            return GovernmentLegitimacyBand.Rejected;
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null) return;
            if (string.IsNullOrWhiteSpace(Id)) report.AddError("Government legitimacy policy has no stable ID.");
            if (GovernmentCategories.Count == 0) report.AddError($"Government legitimacy policy '{DisplayName}' has no government category.");
            if (Weights.Sum(value => value.weightBasisPoints) != 10000) report.AddError($"Government legitimacy policy '{DisplayName}' weights must total 10000 basis points.");
            if (Weights.GroupBy(value => value.component).Any(group => group.Count() > 1)) report.AddError($"Government legitimacy policy '{DisplayName}' repeats a component.");
            if (!(0 <= contestedThreshold && contestedThreshold < fragileThreshold && fragileThreshold < acceptedThreshold && acceptedThreshold < strongThreshold && strongThreshold < reveredThreshold && reveredThreshold <= 10000)) report.AddError($"Government legitimacy policy '{DisplayName}' has invalid band thresholds.");
        }
    }

    [CreateAssetMenu(fileName = "GovernmentFiscalPolicyDefinition", menuName = "Unity Isekai Game/Governments/Fiscal Policy Definition")]
    public class GovernmentFiscalPolicyDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string fiscalPolicyDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField] private string[] revenueDefinitionIds = Array.Empty<string>();
        [SerializeField] private string defaultTreasuryAccountId;
        [SerializeField] private bool requiresApprovedBudget = true;
        [SerializeField] private int version = 1;

        public string Id => fiscalPolicyDefinitionId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public IReadOnlyList<string> RevenueDefinitionIds => PoliticalModelUtility.Clean(revenueDefinitionIds);
        public string DefaultTreasuryAccountId => defaultTreasuryAccountId ?? string.Empty;
        public bool RequiresApprovedBudget => requiresApprovedBudget;
        public int Version => Math.Max(1, version);

        public void DevelopmentConfigure(string id, string name, IEnumerable<string> revenueIds, string treasuryAccountId, bool budgetRequired = true)
        {
            fiscalPolicyDefinitionId = PoliticalModelUtility.Normalize(id);
            displayName = string.IsNullOrWhiteSpace(name) ? fiscalPolicyDefinitionId : name.Trim();
            revenueDefinitionIds = PoliticalModelUtility.Clean(revenueIds);
            defaultTreasuryAccountId = PoliticalModelUtility.Normalize(treasuryAccountId);
            requiresApprovedBudget = budgetRequired;
            version = 1;
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null) return;
            if (string.IsNullOrWhiteSpace(Id)) report.AddError("Government fiscal policy has no stable ID.");
            foreach (string revenueId in RevenueDefinitionIds)
            {
                if (definitionsById.TryGetValue(revenueId, out IGameDefinition referenced) && referenced is not InstitutionalRevenueDefinition) report.AddError($"Government fiscal policy '{DisplayName}' references '{revenueId}', but it is not a revenue definition.");
            }
            if (string.IsNullOrWhiteSpace(DefaultTreasuryAccountId)) report.AddError($"Government fiscal policy '{DisplayName}' has no treasury account ID.");
        }
    }
}
