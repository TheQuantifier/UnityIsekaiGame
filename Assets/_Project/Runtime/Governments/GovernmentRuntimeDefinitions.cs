using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Governments
{
    [CreateAssetMenu(fileName = "PolityDefinition", menuName = "Unity Isekai Game/Governments/Polity Definition")]
    public sealed class PolityDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string polityDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private PolityCategory category = PolityCategory.Kingdom;
        [SerializeField] private GovernmentCategory[] supportedGovernmentCategories = Array.Empty<GovernmentCategory>();
        [SerializeField] private bool supportsTerritorialSovereignty = true;
        [SerializeField] private bool supportsNonTerritorialIdentity;
        [SerializeField] private bool allowsCompetingGovernments = true;
        [SerializeField] private bool supportsSubordinateGovernments = true;
        [SerializeField] private bool supportsSuccession = true;
        [SerializeField] private bool supportsDissolution = true;
        [SerializeField] private bool requiresRecognitionForExternalClaims;
        [SerializeField] private PoliticalVisibility defaultVisibility = PoliticalVisibility.Public;
        [SerializeField] private string[] tags = Array.Empty<string>();
        [SerializeField] private int version = 1;

        public string Id => polityDefinitionId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public string Description => description ?? string.Empty;
        public PolityCategory Category => category;
        public IReadOnlyList<GovernmentCategory> SupportedGovernmentCategories => supportedGovernmentCategories ?? Array.Empty<GovernmentCategory>();
        public bool SupportsTerritorialSovereignty => supportsTerritorialSovereignty;
        public bool SupportsNonTerritorialIdentity => supportsNonTerritorialIdentity;
        public bool AllowsCompetingGovernments => allowsCompetingGovernments;
        public bool SupportsSubordinateGovernments => supportsSubordinateGovernments;
        public bool SupportsSuccession => supportsSuccession;
        public bool SupportsDissolution => supportsDissolution;
        public bool RequiresRecognitionForExternalClaims => requiresRecognitionForExternalClaims;
        public PoliticalVisibility DefaultVisibility => defaultVisibility;
        public IReadOnlyList<string> TagIds => PoliticalModelUtility.Clean(tags);
        public int Version => Math.Max(1, version);

        public void DevelopmentConfigure(string id, string name, PolityCategory polityCategory, IEnumerable<GovernmentCategory> governmentCategories = null, bool territorial = true, bool nonTerritorial = false, bool competingGovernments = true, bool subordinateGovernments = true, bool succession = true, bool dissolution = true, bool recognitionRequired = false, PoliticalVisibility visibility = PoliticalVisibility.Public, IEnumerable<string> tagIds = null)
        {
            polityDefinitionId = PoliticalModelUtility.Normalize(id);
            displayName = string.IsNullOrWhiteSpace(name) ? polityDefinitionId : name.Trim();
            description = string.Empty;
            category = polityCategory;
            supportedGovernmentCategories = (governmentCategories ?? Array.Empty<GovernmentCategory>()).Where(item => item != GovernmentCategory.Unknown).Distinct().ToArray();
            supportsTerritorialSovereignty = territorial;
            supportsNonTerritorialIdentity = nonTerritorial;
            allowsCompetingGovernments = competingGovernments;
            supportsSubordinateGovernments = subordinateGovernments;
            supportsSuccession = succession;
            supportsDissolution = dissolution;
            requiresRecognitionForExternalClaims = recognitionRequired;
            defaultVisibility = visibility;
            tags = PoliticalModelUtility.Clean(tagIds);
            version = 1;
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null) return;
            if (string.IsNullOrWhiteSpace(Id)) report.AddError("Polity definition has no stable ID.");
            else if (!Id.StartsWith("polity.", StringComparison.Ordinal)) report.AddWarning($"Polity definition '{DisplayName}' should use the 'polity.' namespace prefix.");
            ValidateEnum(category, DisplayName, "category", report);
            ValidateEnum(defaultVisibility, DisplayName, "visibility", report);
            foreach (GovernmentCategory governmentCategory in SupportedGovernmentCategories) ValidateEnum(governmentCategory, DisplayName, "supported government category", report);
            if (!supportsTerritorialSovereignty && !supportsNonTerritorialIdentity) report.AddError($"Polity definition '{DisplayName}' supports neither territorial nor nonterritorial identity.");
        }

        private static void ValidateEnum<T>(T value, string name, string field, DefinitionValidationReport report) where T : struct, Enum
        {
            bool rejectsDefault = typeof(T) != typeof(PoliticalVisibility);
            if (!Enum.IsDefined(typeof(T), value) || (rejectsDefault && value.Equals(default(T)))) report.AddError($"Polity definition '{name}' has invalid {field}.");
        }
    }

    [CreateAssetMenu(fileName = "GovernmentDefinition", menuName = "Unity Isekai Game/Governments/Government Definition")]
    public class GovernmentDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string governmentDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private GovernmentCategory category = GovernmentCategory.CouncilGovernment;
        [SerializeField] private PolityCategory[] supportedPolityCategories = Array.Empty<PolityCategory>();
        [SerializeField] private GovernmentLevel defaultLevel = GovernmentLevel.Central;
        [SerializeField] private bool allowsSeveralGoverningOrganizations;
        [SerializeField] private bool requiresTerritorialJurisdiction = true;
        [SerializeField] private bool supportsGovernmentInExile;
        [SerializeField] private bool supportsProvisionalState = true;
        [SerializeField] private bool supportsOccupationAdministration;
        [SerializeField] private bool supportsSubordinateGovernments = true;
        [SerializeField] private bool canClaimLegitimacy = true;
        [SerializeField] private PoliticalVisibility defaultVisibility = PoliticalVisibility.Public;
        [SerializeField] private string[] requiredAuthorityPermissionIds = Array.Empty<string>();
        [SerializeField] private string[] tags = Array.Empty<string>();
        [SerializeField] private int version = 1;

        public string Id => governmentDefinitionId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public string Description => description ?? string.Empty;
        public GovernmentCategory Category => category;
        public IReadOnlyList<PolityCategory> SupportedPolityCategories => supportedPolityCategories ?? Array.Empty<PolityCategory>();
        public GovernmentLevel DefaultLevel => defaultLevel;
        public bool AllowsSeveralGoverningOrganizations => allowsSeveralGoverningOrganizations;
        public bool RequiresTerritorialJurisdiction => requiresTerritorialJurisdiction;
        public bool SupportsGovernmentInExile => supportsGovernmentInExile;
        public bool SupportsProvisionalState => supportsProvisionalState;
        public bool SupportsOccupationAdministration => supportsOccupationAdministration;
        public bool SupportsSubordinateGovernments => supportsSubordinateGovernments;
        public bool CanClaimLegitimacy => canClaimLegitimacy;
        public PoliticalVisibility DefaultVisibility => defaultVisibility;
        public IReadOnlyList<string> RequiredAuthorityPermissionIds => PoliticalModelUtility.Clean(requiredAuthorityPermissionIds);
        public IReadOnlyList<string> TagIds => PoliticalModelUtility.Clean(tags);
        public int Version => Math.Max(1, version);

        public void DevelopmentConfigure(string id, string name, GovernmentCategory governmentCategory, GovernmentLevel level, IEnumerable<PolityCategory> polityCategories = null, bool severalOrganizations = false, bool territorialRequired = true, bool exile = false, bool provisional = true, bool occupation = false, bool subordinate = true, bool legitimacy = true, PoliticalVisibility visibility = PoliticalVisibility.Public, IEnumerable<string> permissionIds = null, IEnumerable<string> tagIds = null)
        {
            governmentDefinitionId = PoliticalModelUtility.Normalize(id);
            displayName = string.IsNullOrWhiteSpace(name) ? governmentDefinitionId : name.Trim();
            description = string.Empty;
            category = governmentCategory;
            defaultLevel = level;
            supportedPolityCategories = (polityCategories ?? Array.Empty<PolityCategory>()).Where(item => item != PolityCategory.Unknown).Distinct().ToArray();
            allowsSeveralGoverningOrganizations = severalOrganizations;
            requiresTerritorialJurisdiction = territorialRequired;
            supportsGovernmentInExile = exile;
            supportsProvisionalState = provisional;
            supportsOccupationAdministration = occupation;
            supportsSubordinateGovernments = subordinate;
            canClaimLegitimacy = legitimacy;
            defaultVisibility = visibility;
            requiredAuthorityPermissionIds = PoliticalModelUtility.Clean(permissionIds);
            tags = PoliticalModelUtility.Clean(tagIds);
            version = 1;
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null) return;
            if (string.IsNullOrWhiteSpace(Id)) report.AddError("Government definition has no stable ID.");
            else if (!Id.StartsWith("government.", StringComparison.Ordinal)) report.AddWarning($"Government definition '{DisplayName}' should use the 'government.' namespace prefix.");
            ValidateEnum(category, DisplayName, "category", report);
            ValidateEnum(defaultLevel, DisplayName, "level", report);
            ValidateEnum(defaultVisibility, DisplayName, "visibility", report);
            foreach (PolityCategory polityCategory in SupportedPolityCategories) ValidateEnum(polityCategory, DisplayName, "supported polity category", report);
            foreach (string id in RequiredAuthorityPermissionIds) if (definitionsById != null && !definitionsById.ContainsKey(id)) report.AddError($"Government definition '{DisplayName}' references missing authority permission definition '{id}'.");
        }

        private static void ValidateEnum<T>(T value, string name, string field, DefinitionValidationReport report) where T : struct, Enum
        {
            bool rejectsDefault = typeof(T) != typeof(PoliticalVisibility);
            if (!Enum.IsDefined(typeof(T), value) || (rejectsDefault && value.Equals(default(T)))) report.AddError($"Government definition '{name}' has invalid {field}.");
        }
    }

    [CreateAssetMenu(fileName = "PoliticalTerritoryDefinition", menuName = "Unity Isekai Game/Governments/Territory Definition")]
    public class PoliticalTerritoryDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string territoryDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField] private PoliticalTerritoryCategory category = PoliticalTerritoryCategory.Region;
        [SerializeField] private bool requiresAtLeastOnePlace = true;
        [SerializeField] private bool allowsSubordinateTerritories = true;
        [SerializeField] private bool mayBeNonContiguous;
        [SerializeField] private bool mayBeNonTerritorial;
        [SerializeField] private PoliticalVisibility defaultVisibility = PoliticalVisibility.Public;
        [SerializeField] private string[] tags = Array.Empty<string>();

        public string Id => territoryDefinitionId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public PoliticalTerritoryCategory Category => category;
        public bool RequiresAtLeastOnePlace => requiresAtLeastOnePlace;
        public bool AllowsSubordinateTerritories => allowsSubordinateTerritories;
        public bool MayBeNonContiguous => mayBeNonContiguous;
        public bool MayBeNonTerritorial => mayBeNonTerritorial;
        public PoliticalVisibility DefaultVisibility => defaultVisibility;
        public IReadOnlyList<string> TagIds => PoliticalModelUtility.Clean(tags);

        public void DevelopmentConfigure(string id, string name, PoliticalTerritoryCategory territoryCategory, bool requiresPlace = true, bool subordinate = true, bool nonContiguous = false, bool nonTerritorial = false, PoliticalVisibility visibility = PoliticalVisibility.Public, IEnumerable<string> tagIds = null)
        {
            territoryDefinitionId = PoliticalModelUtility.Normalize(id);
            displayName = string.IsNullOrWhiteSpace(name) ? territoryDefinitionId : name.Trim();
            category = territoryCategory;
            requiresAtLeastOnePlace = requiresPlace;
            allowsSubordinateTerritories = subordinate;
            mayBeNonContiguous = nonContiguous;
            mayBeNonTerritorial = nonTerritorial;
            defaultVisibility = visibility;
            tags = PoliticalModelUtility.Clean(tagIds);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null) return;
            if (string.IsNullOrWhiteSpace(Id)) report.AddError("Political Territory definition has no stable ID.");
            else if (!Id.StartsWith("political-territory.", StringComparison.Ordinal)) report.AddWarning($"Political Territory definition '{DisplayName}' should use the 'political-territory.' namespace prefix.");
            if (!Enum.IsDefined(typeof(PoliticalTerritoryCategory), category) || category == PoliticalTerritoryCategory.Unknown) report.AddError($"Political Territory definition '{DisplayName}' has invalid category.");
            if (!Enum.IsDefined(typeof(PoliticalVisibility), defaultVisibility)) report.AddError($"Political Territory definition '{DisplayName}' has invalid visibility.");
        }
    }

    [CreateAssetMenu(fileName = "TerritorialClaimDefinition", menuName = "Unity Isekai Game/Governments/Territorial Claim Definition")]
    public class TerritorialClaimDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string claimDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField] private TerritorialClaimCategory category = TerritorialClaimCategory.Sovereignty;
        [SerializeField] private bool requiresGovernment;
        [SerializeField] private bool requiresPolity = true;
        [SerializeField] private bool allowsDispute = true;
        [SerializeField] private bool allowsRecognition = true;
        [SerializeField] private PoliticalVisibility defaultVisibility = PoliticalVisibility.Public;
        [SerializeField] private string[] tags = Array.Empty<string>();

        public string Id => claimDefinitionId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public TerritorialClaimCategory Category => category;
        public bool RequiresGovernment => requiresGovernment;
        public bool RequiresPolity => requiresPolity;
        public bool AllowsDispute => allowsDispute;
        public bool AllowsRecognition => allowsRecognition;
        public PoliticalVisibility DefaultVisibility => defaultVisibility;
        public IReadOnlyList<string> TagIds => PoliticalModelUtility.Clean(tags);

        public void DevelopmentConfigure(string id, string name, TerritorialClaimCategory claimCategory, bool governmentRequired = false, bool polityRequired = true, bool dispute = true, bool recognition = true, PoliticalVisibility visibility = PoliticalVisibility.Public, IEnumerable<string> tagIds = null)
        {
            claimDefinitionId = PoliticalModelUtility.Normalize(id);
            displayName = string.IsNullOrWhiteSpace(name) ? claimDefinitionId : name.Trim();
            category = claimCategory;
            requiresGovernment = governmentRequired;
            requiresPolity = polityRequired;
            allowsDispute = dispute;
            allowsRecognition = recognition;
            defaultVisibility = visibility;
            tags = PoliticalModelUtility.Clean(tagIds);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null) return;
            if (string.IsNullOrWhiteSpace(Id)) report.AddError("Territorial Claim definition has no stable ID.");
            else if (!Id.StartsWith("territorial-claim.", StringComparison.Ordinal)) report.AddWarning($"Territorial Claim definition '{DisplayName}' should use the 'territorial-claim.' namespace prefix.");
            if (!Enum.IsDefined(typeof(TerritorialClaimCategory), category) || category == TerritorialClaimCategory.Unknown) report.AddError($"Territorial Claim definition '{DisplayName}' has invalid category.");
            if (!Enum.IsDefined(typeof(PoliticalVisibility), defaultVisibility)) report.AddError($"Territorial Claim definition '{DisplayName}' has invalid visibility.");
        }
    }

    [CreateAssetMenu(fileName = "JurisdictionDefinition", menuName = "Unity Isekai Game/Governments/Jurisdiction Definition")]
    public class JurisdictionDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string jurisdictionDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField] private JurisdictionCategory category = JurisdictionCategory.GeneralGovernment;
        [SerializeField] private JurisdictionScopeDimension allowedDimensions = JurisdictionScopeDimension.Territory | JurisdictionScopeDimension.SubjectMatter;
        [SerializeField] private JurisdictionSubjectMatter[] allowedSubjectMatters = Array.Empty<JurisdictionSubjectMatter>();
        [SerializeField] private JurisdictionConflictPolicy defaultConflictPolicy = JurisdictionConflictPolicy.SpecificOverridesGeneral;
        [SerializeField] private bool allowsDelegation = true;
        [SerializeField] private bool exclusiveByDefault;
        [SerializeField] private PoliticalVisibility defaultVisibility = PoliticalVisibility.Public;
        [SerializeField] private string[] requiredAuthorityPermissionIds = Array.Empty<string>();
        [SerializeField] private string[] tags = Array.Empty<string>();

        public string Id => jurisdictionDefinitionId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public JurisdictionCategory Category => category;
        public JurisdictionScopeDimension AllowedDimensions => allowedDimensions;
        public IReadOnlyList<JurisdictionSubjectMatter> AllowedSubjectMatters => allowedSubjectMatters ?? Array.Empty<JurisdictionSubjectMatter>();
        public JurisdictionConflictPolicy DefaultConflictPolicy => defaultConflictPolicy;
        public bool AllowsDelegation => allowsDelegation;
        public bool ExclusiveByDefault => exclusiveByDefault;
        public PoliticalVisibility DefaultVisibility => defaultVisibility;
        public IReadOnlyList<string> RequiredAuthorityPermissionIds => PoliticalModelUtility.Clean(requiredAuthorityPermissionIds);
        public IReadOnlyList<string> TagIds => PoliticalModelUtility.Clean(tags);

        public void DevelopmentConfigure(string id, string name, JurisdictionCategory jurisdictionCategory, JurisdictionScopeDimension dimensions, IEnumerable<JurisdictionSubjectMatter> subjectMatters = null, JurisdictionConflictPolicy conflictPolicy = JurisdictionConflictPolicy.SpecificOverridesGeneral, bool delegation = true, bool exclusive = false, PoliticalVisibility visibility = PoliticalVisibility.Public, IEnumerable<string> permissionIds = null, IEnumerable<string> tagIds = null)
        {
            jurisdictionDefinitionId = PoliticalModelUtility.Normalize(id);
            displayName = string.IsNullOrWhiteSpace(name) ? jurisdictionDefinitionId : name.Trim();
            category = jurisdictionCategory;
            allowedDimensions = dimensions;
            allowedSubjectMatters = (subjectMatters ?? Array.Empty<JurisdictionSubjectMatter>()).Where(item => item != JurisdictionSubjectMatter.Unknown).Distinct().ToArray();
            defaultConflictPolicy = conflictPolicy;
            allowsDelegation = delegation;
            exclusiveByDefault = exclusive;
            defaultVisibility = visibility;
            requiredAuthorityPermissionIds = PoliticalModelUtility.Clean(permissionIds);
            tags = PoliticalModelUtility.Clean(tagIds);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null) return;
            if (string.IsNullOrWhiteSpace(Id)) report.AddError("Jurisdiction definition has no stable ID.");
            else if (!Id.StartsWith("jurisdiction.", StringComparison.Ordinal)) report.AddWarning($"Jurisdiction definition '{DisplayName}' should use the 'jurisdiction.' namespace prefix.");
            if (!Enum.IsDefined(typeof(JurisdictionCategory), category) || category == JurisdictionCategory.Unknown) report.AddError($"Jurisdiction definition '{DisplayName}' has invalid category.");
            if (allowedDimensions == JurisdictionScopeDimension.None) report.AddError($"Jurisdiction definition '{DisplayName}' has no allowed scope dimensions.");
            if (!Enum.IsDefined(typeof(JurisdictionConflictPolicy), defaultConflictPolicy) || defaultConflictPolicy == JurisdictionConflictPolicy.Unknown) report.AddError($"Jurisdiction definition '{DisplayName}' has invalid conflict policy.");
            foreach (JurisdictionSubjectMatter subject in AllowedSubjectMatters) if (!Enum.IsDefined(typeof(JurisdictionSubjectMatter), subject) || subject == JurisdictionSubjectMatter.Unknown) report.AddError($"Jurisdiction definition '{DisplayName}' has invalid subject matter.");
            foreach (string id in RequiredAuthorityPermissionIds) if (definitionsById != null && !definitionsById.ContainsKey(id)) report.AddError($"Jurisdiction definition '{DisplayName}' references missing authority permission definition '{id}'.");
        }
    }

    [CreateAssetMenu(fileName = "GovernmentOrganizationCharterDefinition", menuName = "Unity Isekai Game/Governments/Organization Charter Definition")]
    public class GovernmentOrganizationCharterDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string charterDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private GovernmentOrganizationCharterCategory category = GovernmentOrganizationCharterCategory.RecognizedInstitution;
        [SerializeField] private string governmentId;
        [SerializeField] private string organizationId;
        [SerializeField] private string supervisingOrganizationId;
        [SerializeField] private string legalInstrumentId;
        [SerializeField] private string[] jurisdictionIds = Array.Empty<string>();
        [SerializeField] private string[] grantedPowerIds = Array.Empty<string>();
        [SerializeField] private string[] dutyIds = Array.Empty<string>();
        [SerializeField] private bool revocable = true;
        [SerializeField] private PoliticalVisibility defaultVisibility = PoliticalVisibility.Public;
        [SerializeField] private string[] tags = Array.Empty<string>();
        [SerializeField] private int version = 1;

        public string Id => charterDefinitionId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public string Description => description ?? string.Empty;
        public GovernmentOrganizationCharterCategory Category => category;
        public string GovernmentId => PoliticalModelUtility.Normalize(governmentId);
        public string OrganizationId => PoliticalModelUtility.Normalize(organizationId);
        public string SupervisingOrganizationId => PoliticalModelUtility.Normalize(supervisingOrganizationId);
        public string LegalInstrumentId => PoliticalModelUtility.Normalize(legalInstrumentId);
        public IReadOnlyList<string> JurisdictionIds => PoliticalModelUtility.Clean(jurisdictionIds);
        public IReadOnlyList<string> GrantedPowerIds => PoliticalModelUtility.Clean(grantedPowerIds);
        public IReadOnlyList<string> DutyIds => PoliticalModelUtility.Clean(dutyIds);
        public bool Revocable => revocable;
        public PoliticalVisibility DefaultVisibility => defaultVisibility;
        public IReadOnlyList<string> TagIds => PoliticalModelUtility.Clean(tags);
        public int Version => Math.Max(1, version);

        public void DevelopmentConfigure(string id, string name, GovernmentOrganizationCharterCategory charterCategory, string issuingGovernmentId, string targetOrganizationId, string supervisorOrganizationId, string instrumentId, IEnumerable<string> jurisdictions, IEnumerable<string> grantedPowers, IEnumerable<string> duties, bool canRevoke = true, PoliticalVisibility visibility = PoliticalVisibility.Public, IEnumerable<string> tagIds = null)
        {
            charterDefinitionId = PoliticalModelUtility.Normalize(id);
            displayName = string.IsNullOrWhiteSpace(name) ? charterDefinitionId : name.Trim();
            description = string.Empty;
            category = charterCategory;
            governmentId = PoliticalModelUtility.Normalize(issuingGovernmentId);
            organizationId = PoliticalModelUtility.Normalize(targetOrganizationId);
            supervisingOrganizationId = PoliticalModelUtility.Normalize(supervisorOrganizationId);
            legalInstrumentId = PoliticalModelUtility.Normalize(instrumentId);
            jurisdictionIds = PoliticalModelUtility.Clean(jurisdictions);
            grantedPowerIds = PoliticalModelUtility.Clean(grantedPowers);
            dutyIds = PoliticalModelUtility.Clean(duties);
            revocable = canRevoke;
            defaultVisibility = visibility;
            tags = PoliticalModelUtility.Clean(tagIds);
            version = 1;
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null) return;
            if (string.IsNullOrWhiteSpace(Id)) report.AddError("Government organization charter definition has no stable ID.");
            else if (!Id.StartsWith("government-charter.", StringComparison.Ordinal)) report.AddWarning($"Government organization charter definition '{DisplayName}' should use the 'government-charter.' namespace prefix.");
            if (!Enum.IsDefined(typeof(GovernmentOrganizationCharterCategory), category) || category == GovernmentOrganizationCharterCategory.Unknown) report.AddError($"Government organization charter definition '{DisplayName}' has an invalid category.");
            if (string.IsNullOrWhiteSpace(GovernmentId)) report.AddError($"Government organization charter definition '{DisplayName}' has no issuing government.");
            if (string.IsNullOrWhiteSpace(OrganizationId)) report.AddError($"Government organization charter definition '{DisplayName}' has no target organization.");
            if (string.IsNullOrWhiteSpace(LegalInstrumentId)) report.AddError($"Government organization charter definition '{DisplayName}' has no legal instrument ID.");
            if (GrantedPowerIds.Count == 0 && DutyIds.Count == 0) report.AddError($"Government organization charter definition '{DisplayName}' grants no powers and imposes no duties.");
            if (!Enum.IsDefined(typeof(PoliticalVisibility), defaultVisibility)) report.AddError($"Government organization charter definition '{DisplayName}' has invalid visibility.");
        }
    }
}
