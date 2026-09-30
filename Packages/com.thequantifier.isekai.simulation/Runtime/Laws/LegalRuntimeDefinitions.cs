using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Crimes;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Governments;

namespace UnityIsekaiGame.Laws
{
    public abstract class LegalDefinitionBase : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string definitionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private PoliticalVisibility visibility = PoliticalVisibility.Public;
        [SerializeField] private string[] tags = Array.Empty<string>();
        [SerializeField] private int version = 1;
        public string Id => definitionId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public string Description => description ?? string.Empty;
        public PoliticalVisibility Visibility => visibility;
        public IReadOnlyList<string> Tags => PoliticalModelUtility.Clean(tags);
        public int Version => Math.Max(1, version);
        protected void ConfigureBase(string id, string name, PoliticalVisibility access, IEnumerable<string> tagIds) { definitionId = PoliticalModelUtility.Normalize(id); displayName = string.IsNullOrWhiteSpace(name) ? definitionId : name.Trim(); description = string.Empty; visibility = access; tags = PoliticalModelUtility.Clean(tagIds); version = 1; }
        public virtual void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report) { if (report == null) return; if (string.IsNullOrWhiteSpace(Id)) report.AddError($"{GetType().Name} has no stable ID."); }
    }

    [CreateAssetMenu(fileName = "LawRuleDefinition", menuName = "Unity Isekai Game/Laws/Law Rule Definition")]
    public class LawRuleDefinition : LegalDefinitionBase
    {
        [Header("Legal rule")]
        [SerializeField] private LegalEffectCategory effect = LegalEffectCategory.Prohibition;
        [SerializeField] private string actionId;
        [SerializeField] private string offenseDefinitionId;
        [SerializeField] private string subjectMatterId;

        [Header("Instrument and authority")]
        [SerializeField] private string instrumentId;
        [SerializeField] private string instrumentTitle;
        [SerializeField] private string instrumentShortTitle;
        [SerializeField] private string citation;
        [SerializeField] private string instrumentDefinitionId;
        [SerializeField] private string authorityDefinitionId;
        [SerializeField] private string issuingGovernmentId;
        [SerializeField] private string issuingOrganizationId;
        [SerializeField] private string issuingOfficeId;
        [SerializeField] private string[] jurisdictionIds = Array.Empty<string>();

        [Header("Scope")]
        [SerializeField] private LawScopeKind scopeKind = LawScopeKind.Worldwide;
        [SerializeField] private string[] territoryIds = Array.Empty<string>();
        [SerializeField] private string[] placeIds = Array.Empty<string>();
        [SerializeField] private string[] propertyIds = Array.Empty<string>();
        [SerializeField] private string[] organizationIds = Array.Empty<string>();

        [Header("Discovery and enforcement")]
        [SerializeField, Range(0, 10000)] private int credibleReportReliabilityBasisPoints = 6000;
        [SerializeField] private string reportedWantedDefinitionId;
        [SerializeField] private string authorityDiscoveredWantedDefinitionId;

        public LegalEffectCategory Effect => effect;
        public string ActionId => PoliticalModelUtility.Normalize(actionId);
        public string OffenseDefinitionId => PoliticalModelUtility.Normalize(offenseDefinitionId);
        public string SubjectMatterId => PoliticalModelUtility.Normalize(subjectMatterId);
        public string InstrumentId => PoliticalModelUtility.Normalize(instrumentId);
        public string InstrumentTitle => string.IsNullOrWhiteSpace(instrumentTitle) ? DisplayName : instrumentTitle.Trim();
        public string InstrumentShortTitle => string.IsNullOrWhiteSpace(instrumentShortTitle) ? InstrumentTitle : instrumentShortTitle.Trim();
        public string Citation => citation?.Trim() ?? string.Empty;
        public string InstrumentDefinitionId => PoliticalModelUtility.Normalize(instrumentDefinitionId);
        public string AuthorityDefinitionId => PoliticalModelUtility.Normalize(authorityDefinitionId);
        public string IssuingGovernmentId => PoliticalModelUtility.Normalize(issuingGovernmentId);
        public string IssuingOrganizationId => PoliticalModelUtility.Normalize(issuingOrganizationId);
        public string IssuingOfficeId => PoliticalModelUtility.Normalize(issuingOfficeId);
        public IReadOnlyList<string> JurisdictionIds => PoliticalModelUtility.Clean(jurisdictionIds);
        public LawScopeKind ScopeKind => scopeKind;
        public IReadOnlyList<string> TerritoryIds => PoliticalModelUtility.Clean(territoryIds);
        public IReadOnlyList<string> PlaceIds => PoliticalModelUtility.Clean(placeIds);
        public IReadOnlyList<string> PropertyIds => PoliticalModelUtility.Clean(propertyIds);
        public IReadOnlyList<string> OrganizationIds => PoliticalModelUtility.Clean(organizationIds);
        public int CredibleReportReliabilityBasisPoints => Mathf.Clamp(credibleReportReliabilityBasisPoints, 0, 10000);
        public string ReportedWantedDefinitionId => PoliticalModelUtility.Normalize(reportedWantedDefinitionId);
        public string AuthorityDiscoveredWantedDefinitionId => PoliticalModelUtility.Normalize(authorityDiscoveredWantedDefinitionId);
        public string ProvisionId => $"legal-provision.{Id}";

        public void DevelopmentConfigure(
            string id,
            string name,
            string prohibitedActionId,
            string legalOffenseDefinitionId,
            string legalInstrumentId,
            string title,
            string shortTitle,
            string legalCitation,
            string legalInstrumentDefinitionId,
            string legalAuthorityDefinitionId,
            string governmentId,
            string organizationId,
            IEnumerable<string> legalJurisdictionIds,
            LawScopeKind scope,
            IEnumerable<string> scopedTerritoryIds = null,
            IEnumerable<string> scopedPlaceIds = null,
            IEnumerable<string> scopedPropertyIds = null,
            IEnumerable<string> scopedOrganizationIds = null,
            int minimumCredibleReportReliabilityBasisPoints = 6000,
            string reportWantedDefinitionId = "",
            string authorityWantedDefinitionId = "",
            string officeId = "")
        {
            ConfigureBase(id, name, PoliticalVisibility.Public, new[] { "law", "rule", "crime" });
            effect = LegalEffectCategory.Prohibition;
            actionId = PoliticalModelUtility.Normalize(prohibitedActionId);
            offenseDefinitionId = PoliticalModelUtility.Normalize(legalOffenseDefinitionId);
            subjectMatterId = string.Empty;
            instrumentId = PoliticalModelUtility.Normalize(legalInstrumentId);
            instrumentTitle = title?.Trim() ?? string.Empty;
            instrumentShortTitle = shortTitle?.Trim() ?? string.Empty;
            citation = legalCitation?.Trim() ?? string.Empty;
            instrumentDefinitionId = PoliticalModelUtility.Normalize(legalInstrumentDefinitionId);
            authorityDefinitionId = PoliticalModelUtility.Normalize(legalAuthorityDefinitionId);
            issuingGovernmentId = PoliticalModelUtility.Normalize(governmentId);
            issuingOrganizationId = PoliticalModelUtility.Normalize(organizationId);
            issuingOfficeId = PoliticalModelUtility.Normalize(officeId);
            jurisdictionIds = PoliticalModelUtility.Clean(legalJurisdictionIds);
            scopeKind = scope;
            territoryIds = PoliticalModelUtility.Clean(scopedTerritoryIds);
            placeIds = PoliticalModelUtility.Clean(scopedPlaceIds);
            propertyIds = PoliticalModelUtility.Clean(scopedPropertyIds);
            organizationIds = PoliticalModelUtility.Clean(scopedOrganizationIds);
            credibleReportReliabilityBasisPoints = Mathf.Clamp(minimumCredibleReportReliabilityBasisPoints, 0, 10000);
            reportedWantedDefinitionId = PoliticalModelUtility.Normalize(reportWantedDefinitionId);
            authorityDiscoveredWantedDefinitionId = PoliticalModelUtility.Normalize(authorityWantedDefinitionId);
        }

        public override void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitions, DefinitionValidationReport report)
        {
            base.ValidateCatalogDefinition(definitions, report);
            if (effect == LegalEffectCategory.Unknown) report?.AddError($"Law rule '{DisplayName}' has no legal effect.");
            if (string.IsNullOrWhiteSpace(ActionId)) report?.AddError($"Law rule '{DisplayName}' has no action ID.");
            if (scopeKind == LawScopeKind.Unknown) report?.AddError($"Law rule '{DisplayName}' has no scope.");
            if (string.IsNullOrWhiteSpace(InstrumentId) || string.IsNullOrWhiteSpace(InstrumentDefinitionId) || string.IsNullOrWhiteSpace(AuthorityDefinitionId)) report?.AddError($"Law rule '{DisplayName}' has incomplete instrument metadata.");
            if (string.IsNullOrWhiteSpace(IssuingGovernmentId) || JurisdictionIds.Count == 0) report?.AddError($"Law rule '{DisplayName}' has no issuing government or jurisdiction.");
            if (scopeKind == LawScopeKind.Worldwide && (TerritoryIds.Count > 0 || PlaceIds.Count > 0 || PropertyIds.Count > 0 || OrganizationIds.Count > 0)) report?.AddError($"Worldwide law rule '{DisplayName}' cannot also contain a local scope.");
            if (scopeKind == LawScopeKind.Territory && TerritoryIds.Count == 0) report?.AddError($"Territory law rule '{DisplayName}' has no territory.");
            if (scopeKind == LawScopeKind.Place && PlaceIds.Count == 0) report?.AddError($"Place law rule '{DisplayName}' has no place.");
            if (scopeKind == LawScopeKind.Property && PropertyIds.Count == 0) report?.AddError($"Property law rule '{DisplayName}' has no property.");
            if (scopeKind == LawScopeKind.Organization && OrganizationIds.Count == 0) report?.AddError($"Organization law rule '{DisplayName}' has no organization.");
            ValidateReference<LegalOffenseDefinition>(OffenseDefinitionId, "offense", definitions, report);
            ValidateReference<LegalInstrumentDefinition>(InstrumentDefinitionId, "instrument definition", definitions, report);
            ValidateReference<LegalAuthorityDefinition>(AuthorityDefinitionId, "authority definition", definitions, report);
            ValidateReference<WantedStatusDefinition>(ReportedWantedDefinitionId, "reported wanted status", definitions, report);
            ValidateReference<WantedStatusDefinition>(AuthorityDiscoveredWantedDefinitionId, "authority wanted status", definitions, report);
        }

        private void ValidateReference<T>(string id, string label, IReadOnlyDictionary<string, IGameDefinition> definitions, DefinitionValidationReport report) where T : class, IGameDefinition
        {
            if (string.IsNullOrWhiteSpace(id) || definitions == null || !definitions.TryGetValue(id, out IGameDefinition definition) || definition is not T)
            {
                report?.AddError($"Law rule '{DisplayName}' references a missing {label} '{id}'.");
            }
        }
    }

    [CreateAssetMenu(fileName = "LegalAuthorityDefinition", menuName = "Unity Isekai Game/Laws/Legal Authority Definition")]
    public class LegalAuthorityDefinition : LegalDefinitionBase
    {
        [SerializeField] private LegalAuthorityCategory category;
        [SerializeField] private GovernmentLevel[] governmentLevels = Array.Empty<GovernmentLevel>();
        [SerializeField] private JurisdictionCategory[] jurisdictionCategories = Array.Empty<JurisdictionCategory>();
        [SerializeField] private LegalInstrumentCategory[] instrumentCategories = Array.Empty<LegalInstrumentCategory>();
        [SerializeField] private string[] requiredPermissionIds = Array.Empty<string>();
        [SerializeField] private bool allowsDelegation;
        [SerializeField] private bool allowsEmergencyLaw;
        public LegalAuthorityCategory Category => category;
        public IReadOnlyList<GovernmentLevel> GovernmentLevels => governmentLevels ?? Array.Empty<GovernmentLevel>();
        public IReadOnlyList<JurisdictionCategory> JurisdictionCategories => jurisdictionCategories ?? Array.Empty<JurisdictionCategory>();
        public IReadOnlyList<LegalInstrumentCategory> InstrumentCategories => instrumentCategories ?? Array.Empty<LegalInstrumentCategory>();
        public IReadOnlyList<string> RequiredPermissionIds => PoliticalModelUtility.Clean(requiredPermissionIds);
        public bool AllowsDelegation => allowsDelegation;
        public bool AllowsEmergencyLaw => allowsEmergencyLaw;
        public void DevelopmentConfigure(string id, string name, LegalAuthorityCategory authorityCategory, IEnumerable<GovernmentLevel> levels, IEnumerable<JurisdictionCategory> jurisdictions, IEnumerable<LegalInstrumentCategory> instruments, IEnumerable<string> permissions = null, bool delegation = true, bool emergency = false) { ConfigureBase(id, name, PoliticalVisibility.Public, new[] { "law", "authority" }); category = authorityCategory; governmentLevels = (levels ?? Array.Empty<GovernmentLevel>()).Distinct().ToArray(); jurisdictionCategories = (jurisdictions ?? Array.Empty<JurisdictionCategory>()).Distinct().ToArray(); instrumentCategories = (instruments ?? Array.Empty<LegalInstrumentCategory>()).Distinct().ToArray(); requiredPermissionIds = PoliticalModelUtility.Clean(permissions); allowsDelegation = delegation; allowsEmergencyLaw = emergency; }
        public override void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitions, DefinitionValidationReport report) { base.ValidateCatalogDefinition(definitions, report); if (category == LegalAuthorityCategory.Unknown) report?.AddError($"Legal Authority '{DisplayName}' has no category."); if (InstrumentCategories.Count == 0) report?.AddError($"Legal Authority '{DisplayName}' supports no instruments."); }
    }

    [CreateAssetMenu(fileName = "LegalInstrumentDefinition", menuName = "Unity Isekai Game/Laws/Legal Instrument Definition")]
    public class LegalInstrumentDefinition : LegalDefinitionBase
    {
        [SerializeField] private LegalInstrumentCategory category;
        [SerializeField] private int precedence;
        [SerializeField] private LegalConflictPolicy conflictPolicy = LegalConflictPolicy.HigherPrecedenceWins;
        [SerializeField] private bool requiresPublication = true;
        [SerializeField] private bool allowsSuspension = true;
        [SerializeField] private bool allowsAmendment = true;
        [SerializeField] private bool allowsRepeal = true;
        [SerializeField] private double maximumEmergencyDuration = -1d;
        public LegalInstrumentCategory Category => category; public int Precedence => precedence; public LegalConflictPolicy ConflictPolicy => conflictPolicy; public bool RequiresPublication => requiresPublication; public bool AllowsSuspension => allowsSuspension; public bool AllowsAmendment => allowsAmendment; public bool AllowsRepeal => allowsRepeal; public double MaximumEmergencyDuration => maximumEmergencyDuration;
        public void DevelopmentConfigure(string id, string name, LegalInstrumentCategory instrumentCategory, int legalPrecedence, LegalConflictPolicy policy, bool publication = true, double emergencyDuration = -1d) { ConfigureBase(id, name, PoliticalVisibility.Public, new[] { "law", "instrument" }); category = instrumentCategory; precedence = legalPrecedence; conflictPolicy = policy; requiresPublication = publication; allowsSuspension = true; allowsAmendment = true; allowsRepeal = true; maximumEmergencyDuration = emergencyDuration; }
        public override void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitions, DefinitionValidationReport report) { base.ValidateCatalogDefinition(definitions, report); if (category == LegalInstrumentCategory.Unknown) report?.AddError($"Legal Instrument '{DisplayName}' has no category."); }
    }

    [CreateAssetMenu(fileName = "LegalProvisionDefinition", menuName = "Unity Isekai Game/Laws/Legal Provision Definition")]
    public class LegalProvisionDefinition : LegalDefinitionBase
    {
        [SerializeField] private LegalEffectCategory effectCategory;
        [SerializeField] private LegalInstrumentCategory[] supportedInstruments = Array.Empty<LegalInstrumentCategory>();
        public LegalEffectCategory EffectCategory => effectCategory; public IReadOnlyList<LegalInstrumentCategory> SupportedInstruments => supportedInstruments ?? Array.Empty<LegalInstrumentCategory>();
        public void DevelopmentConfigure(string id, string name, LegalEffectCategory effect, IEnumerable<LegalInstrumentCategory> instruments) { ConfigureBase(id, name, PoliticalVisibility.Public, new[] { "law", "provision" }); effectCategory = effect; supportedInstruments = (instruments ?? Array.Empty<LegalInstrumentCategory>()).Distinct().ToArray(); }
        public override void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitions, DefinitionValidationReport report) { base.ValidateCatalogDefinition(definitions, report); if (effectCategory == LegalEffectCategory.Unknown) report?.AddError($"Legal Provision '{DisplayName}' has no effect category."); }
    }

    [CreateAssetMenu(fileName = "LegalStatusDefinition", menuName = "Unity Isekai Game/Laws/Legal Status Definition")]
    public class LegalStatusDefinition : LegalDefinitionBase
    {
        [SerializeField] private LegalStatusCategory category;
        [SerializeField] private bool requiresPolity;
        [SerializeField] private bool allowsMultiple = true;
        [SerializeField] private string[] rightDefinitionIds = Array.Empty<string>();
        [SerializeField] private string[] dutyDefinitionIds = Array.Empty<string>();
        public LegalStatusCategory Category => category; public bool RequiresPolity => requiresPolity; public bool AllowsMultiple => allowsMultiple; public IReadOnlyList<string> RightDefinitionIds => PoliticalModelUtility.Clean(rightDefinitionIds); public IReadOnlyList<string> DutyDefinitionIds => PoliticalModelUtility.Clean(dutyDefinitionIds);
        public void DevelopmentConfigure(string id, string name, LegalStatusCategory statusCategory, bool polityRequired, bool multiple = true) { ConfigureBase(id, name, PoliticalVisibility.Restricted, new[] { "law", "status" }); category = statusCategory; requiresPolity = polityRequired; allowsMultiple = multiple; }
        public override void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitions, DefinitionValidationReport report) { base.ValidateCatalogDefinition(definitions, report); if (category == LegalStatusCategory.Unknown) report?.AddError($"Legal Status '{DisplayName}' has no category."); }
    }

    [CreateAssetMenu(fileName = "CitizenshipDefinition", menuName = "Unity Isekai Game/Laws/Citizenship Definition")]
    public class CitizenshipDefinition : LegalDefinitionBase
    {
        [SerializeField] private CitizenshipAcquisitionRoute[] routes = Array.Empty<CitizenshipAcquisitionRoute>();
        [SerializeField] private bool requiresConsent = true;
        [SerializeField] private bool allowsMultiple = true;
        public IReadOnlyList<CitizenshipAcquisitionRoute> Routes => routes ?? Array.Empty<CitizenshipAcquisitionRoute>(); public bool RequiresConsent => requiresConsent; public bool AllowsMultiple => allowsMultiple;
        public void DevelopmentConfigure(string id, string name, IEnumerable<CitizenshipAcquisitionRoute> acquisitionRoutes, bool consent, bool multiple) { ConfigureBase(id, name, PoliticalVisibility.Restricted, new[] { "law", "citizenship" }); routes = (acquisitionRoutes ?? Array.Empty<CitizenshipAcquisitionRoute>()).Distinct().ToArray(); requiresConsent = consent; allowsMultiple = multiple; }
        public override void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitions, DefinitionValidationReport report) { base.ValidateCatalogDefinition(definitions, report); if (Routes.Count == 0) report?.AddError($"Citizenship Definition '{DisplayName}' has no acquisition route."); }
    }
}
