using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Governments;
using UnityIsekaiGame.Laws;

namespace UnityIsekaiGame.Crimes
{
    public abstract class CrimeDefinitionBase : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
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
        public IReadOnlyList<string> Tags => CrimeModelUtility.C(tags);
        public int Version => Math.Max(1, version);

        protected void ConfigureBase(string id, string name, string text, PoliticalVisibility access, IEnumerable<string> tagIds)
        {
            definitionId = CrimeModelUtility.N(id);
            displayName = string.IsNullOrWhiteSpace(name) ? definitionId : name.Trim();
            description = text ?? string.Empty;
            visibility = access;
            tags = CrimeModelUtility.C(tagIds);
            version = 1;
        }

        public virtual void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null) return;
            if (string.IsNullOrWhiteSpace(Id)) report.AddError($"{GetType().Name} has no stable ID.");
        }
    }

    [Serializable]
    public sealed class OffenseElementDefinitionData
    {
        [SerializeField] private OffenseElementKind kind;
        [SerializeField] private string key;
        [SerializeField] private string expectedValue;
        [SerializeField] private bool required = true;
        [SerializeField] private bool negated;

        public OffenseElementKind Kind => kind;
        public string Key => key ?? string.Empty;
        public string ExpectedValue => expectedValue ?? string.Empty;
        public bool Required => required;
        public bool Negated => negated;

        public OffenseElementDefinitionData Clone() => new OffenseElementDefinitionData { kind = kind, key = CrimeModelUtility.N(key), expectedValue = CrimeModelUtility.N(expectedValue), required = required, negated = negated };
        public static OffenseElementDefinitionData Create(OffenseElementKind elementKind, string elementKey, string value = "", bool isRequired = true, bool isNegated = false) => new OffenseElementDefinitionData { kind = elementKind, key = CrimeModelUtility.N(elementKey), expectedValue = CrimeModelUtility.N(value), required = isRequired, negated = isNegated };
    }

    [CreateAssetMenu(fileName = "LegalOffenseDefinition", menuName = "Unity Isekai Game/Crimes/Legal Offense Definition")]
    public class LegalOffenseDefinition : CrimeDefinitionBase
    {
        [SerializeField] private OffenseCategory category;
        [SerializeField] private OffenseSeverityCategory severity;
        [SerializeField] private LegalEffectCategory[] legalProvisionEffects = Array.Empty<LegalEffectCategory>();
        [SerializeField] private string legalActionId;
        [SerializeField] private OffenseElementDefinitionData[] requiredElements = Array.Empty<OffenseElementDefinitionData>();
        [SerializeField] private CrimeMentalState mentalStatePolicy = CrimeMentalState.Unknown;
        [SerializeField] private OffenseStage[] supportedStages = Array.Empty<OffenseStage>();
        [SerializeField] private ParticipationCategory[] supportedParticipation = Array.Empty<ParticipationCategory>();
        [SerializeField] private bool continuingOffense;
        [SerializeField] private bool reportable = true;
        [SerializeField] private bool warrantEligible = true;
        [SerializeField] private EvidenceSufficiencyState minimumChargeThreshold = EvidenceSufficiencyState.Partial;
        [SerializeField] private EvidenceSufficiencyState minimumWarrantThreshold = EvidenceSufficiencyState.Substantial;
        [SerializeField] private double limitationPeriodWorldTime = -1d;

        public OffenseCategory Category => category;
        public OffenseSeverityCategory Severity => severity;
        public IReadOnlyList<LegalEffectCategory> LegalProvisionEffects => legalProvisionEffects ?? Array.Empty<LegalEffectCategory>();
        public string LegalActionId => legalActionId ?? string.Empty;
        public IReadOnlyList<OffenseElementDefinitionData> RequiredElements => (requiredElements ?? Array.Empty<OffenseElementDefinitionData>()).Where(item => item != null).Select(item => item.Clone()).ToArray();
        public CrimeMentalState MentalStatePolicy => mentalStatePolicy;
        public IReadOnlyList<OffenseStage> SupportedStages => supportedStages ?? Array.Empty<OffenseStage>();
        public IReadOnlyList<ParticipationCategory> SupportedParticipation => supportedParticipation ?? Array.Empty<ParticipationCategory>();
        public bool ContinuingOffense => continuingOffense;
        public bool Reportable => reportable;
        public bool WarrantEligible => warrantEligible;
        public EvidenceSufficiencyState MinimumChargeThreshold => minimumChargeThreshold;
        public EvidenceSufficiencyState MinimumWarrantThreshold => minimumWarrantThreshold;
        public double LimitationPeriodWorldTime => limitationPeriodWorldTime;

        public void DevelopmentConfigure(string id, string name, OffenseCategory offenseCategory, OffenseSeverityCategory severityCategory, IEnumerable<LegalEffectCategory> effects, IEnumerable<OffenseElementDefinitionData> elements, CrimeMentalState mentalState, IEnumerable<OffenseStage> stages, IEnumerable<ParticipationCategory> participation, bool continuing = false, bool canReport = true, bool canWarrant = true, EvidenceSufficiencyState threshold = EvidenceSufficiencyState.Substantial, PoliticalVisibility access = PoliticalVisibility.Public, EvidenceSufficiencyState chargeThreshold = EvidenceSufficiencyState.Partial, string actionId = "")
        {
            ConfigureBase(id, name, string.Empty, access, new[] { "crime", "offense" });
            category = offenseCategory;
            severity = severityCategory;
            legalProvisionEffects = (effects ?? new[] { LegalEffectCategory.Prohibition }).Where(item => item != LegalEffectCategory.Unknown).Distinct().ToArray();
            requiredElements = (elements ?? Array.Empty<OffenseElementDefinitionData>()).Where(item => item != null).Select(item => item.Clone()).ToArray();
            legalActionId = string.IsNullOrWhiteSpace(actionId)
                ? RequiredElements.FirstOrDefault(item => item.Kind == OffenseElementKind.ActorConduct)?.ExpectedValue ?? string.Empty
                : CrimeModelUtility.N(actionId);
            mentalStatePolicy = mentalState == CrimeMentalState.Unknown ? CrimeMentalState.NotRequired : mentalState;
            supportedStages = (stages ?? new[] { OffenseStage.Completed }).Where(item => item != OffenseStage.Unknown).Distinct().ToArray();
            supportedParticipation = (participation ?? new[] { ParticipationCategory.PrincipalActor }).Where(item => item != ParticipationCategory.Unknown).Distinct().ToArray();
            continuingOffense = continuing;
            reportable = canReport;
            warrantEligible = canWarrant;
            minimumChargeThreshold = chargeThreshold == EvidenceSufficiencyState.Unknown ? EvidenceSufficiencyState.Partial : chargeThreshold;
            minimumWarrantThreshold = threshold == EvidenceSufficiencyState.Unknown ? EvidenceSufficiencyState.Substantial : threshold;
            limitationPeriodWorldTime = -1d;
        }

        public override void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitions, DefinitionValidationReport report)
        {
            base.ValidateCatalogDefinition(definitions, report);
            if (category == OffenseCategory.Unknown) report?.AddError($"Legal Offense '{DisplayName}' has no category.");
            if (severity == OffenseSeverityCategory.Unknown) report?.AddError($"Legal Offense '{DisplayName}' has no severity.");
            if (LegalProvisionEffects.Count == 0) report?.AddError($"Legal Offense '{DisplayName}' references no legal provision effect.");
            if (string.IsNullOrWhiteSpace(LegalActionId)) report?.AddError($"Legal Offense '{DisplayName}' has no legal action ID.");
            if (RequiredElements.Count == 0) report?.AddError($"Legal Offense '{DisplayName}' has no structured elements.");
            if (SupportedStages.Count == 0) report?.AddError($"Legal Offense '{DisplayName}' supports no offense stage.");
            if (SupportedParticipation.Count == 0) report?.AddError($"Legal Offense '{DisplayName}' supports no participation category.");
        }
    }

    [CreateAssetMenu(fileName = "WarrantDefinition", menuName = "Unity Isekai Game/Crimes/Warrant Definition")]
    public class WarrantDefinition : CrimeDefinitionBase
    {
        [SerializeField] private WarrantCategory category;
        [SerializeField] private WarrantScopeKind[] allowedScopes = Array.Empty<WarrantScopeKind>();
        [SerializeField] private EvidenceSufficiencyState minimumThreshold = EvidenceSufficiencyState.ThresholdMet;
        [SerializeField] private string requiredInstitutionalActionId;
        [SerializeField] private bool requiresActivePotentialOffense = true;
        [SerializeField] private bool createsDerivedWantedStatus;

        public WarrantCategory Category => category;
        public IReadOnlyList<WarrantScopeKind> AllowedScopes => allowedScopes ?? Array.Empty<WarrantScopeKind>();
        public EvidenceSufficiencyState MinimumThreshold => minimumThreshold;
        public string RequiredInstitutionalActionId => requiredInstitutionalActionId ?? string.Empty;
        public bool RequiresActivePotentialOffense => requiresActivePotentialOffense;
        public bool CreatesDerivedWantedStatus => createsDerivedWantedStatus;

        public void DevelopmentConfigure(string id, string name, WarrantCategory warrantCategory, IEnumerable<WarrantScopeKind> scopes, EvidenceSufficiencyState threshold, bool createsWanted, string requiredActionId = "")
        {
            ConfigureBase(id, name, string.Empty, PoliticalVisibility.Restricted, new[] { "crime", "warrant" });
            category = warrantCategory;
            allowedScopes = (scopes ?? Array.Empty<WarrantScopeKind>()).Where(item => item != WarrantScopeKind.Unknown).Distinct().ToArray();
            minimumThreshold = threshold == EvidenceSufficiencyState.Unknown ? EvidenceSufficiencyState.ThresholdMet : threshold;
            createsDerivedWantedStatus = createsWanted;
            requiresActivePotentialOffense = true;
            requiredInstitutionalActionId = CrimeModelUtility.N(requiredActionId);
        }

        public override void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitions, DefinitionValidationReport report)
        {
            base.ValidateCatalogDefinition(definitions, report);
            if (category == WarrantCategory.Unknown) report?.AddError($"Warrant Definition '{DisplayName}' has no category.");
            if (AllowedScopes.Count == 0) report?.AddError($"Warrant Definition '{DisplayName}' has no allowed scope.");
        }
    }

    [CreateAssetMenu(fileName = "WantedStatusDefinition", menuName = "Unity Isekai Game/Crimes/Wanted Status Definition")]
    public class WantedStatusDefinition : CrimeDefinitionBase
    {
        [SerializeField] private WantedPurposeCategory purpose;
        [SerializeField] private bool mayBePublic = true;
        [SerializeField] private bool mayBeRestricted = true;
        [SerializeField] private bool mayBeSecret = true;
        [SerializeField] private bool mayDeriveFromWarrant = true;

        public WantedPurposeCategory Purpose => purpose;
        public bool MayBePublic => mayBePublic;
        public bool MayBeRestricted => mayBeRestricted;
        public bool MayBeSecret => mayBeSecret;
        public bool MayDeriveFromWarrant => mayDeriveFromWarrant;

        public void DevelopmentConfigure(string id, string name, WantedPurposeCategory wantedPurpose, bool derive = true, bool publicAllowed = true, bool restrictedAllowed = true, bool secretAllowed = true)
        {
            ConfigureBase(id, name, string.Empty, PoliticalVisibility.Restricted, new[] { "crime", "wanted" });
            purpose = wantedPurpose;
            mayDeriveFromWarrant = derive;
            mayBePublic = publicAllowed;
            mayBeRestricted = restrictedAllowed;
            mayBeSecret = secretAllowed;
        }

        public override void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitions, DefinitionValidationReport report)
        {
            base.ValidateCatalogDefinition(definitions, report);
            if (purpose == WantedPurposeCategory.Unknown) report?.AddError($"Wanted Status Definition '{DisplayName}' has no purpose.");
            if (!mayBePublic && !mayBeRestricted && !mayBeSecret) report?.AddError($"Wanted Status Definition '{DisplayName}' has no visibility mode.");
        }
    }
}
