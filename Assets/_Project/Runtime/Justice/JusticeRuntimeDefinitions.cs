using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Crimes;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Governments;

namespace UnityIsekaiGame.Justice
{
    public abstract class JusticeDefinitionBase : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
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
        public IReadOnlyList<string> Tags => JusticeModelUtility.C(tags);
        public int Version => Math.Max(1, version);

        protected void ConfigureBase(string id, string name, string text, PoliticalVisibility access, IEnumerable<string> tagIds)
        {
            definitionId = JusticeModelUtility.N(id);
            displayName = string.IsNullOrWhiteSpace(name) ? definitionId : name.Trim();
            description = text ?? string.Empty;
            visibility = access;
            tags = JusticeModelUtility.C(tagIds);
            version = 1;
        }

        public virtual void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null) return;
            if (string.IsNullOrWhiteSpace(Id)) report.AddError($"{GetType().Name} has no stable ID.");
        }
    }

    [CreateAssetMenu(fileName = "JusticeInstitutionDefinition", menuName = "Unity Isekai Game/Justice/Justice Institution Definition")]
    public class JusticeInstitutionDefinition : JusticeDefinitionBase
    {
        [SerializeField] private JusticeInstitutionCategory category;
        [SerializeField] private JurisdictionCategory[] supportedJurisdictions = Array.Empty<JurisdictionCategory>();
        [SerializeField] private JusticeCaseCategory[] supportedCases = Array.Empty<JusticeCaseCategory>();
        [SerializeField] private JusticeDecisionProcedure[] supportedProcedures = Array.Empty<JusticeDecisionProcedure>();
        [SerializeField] private bool canHoldCustody;
        [SerializeField] private bool canSentence;
        [SerializeField] private bool appellate;
        [SerializeField] private string[] requiredAuthorityActionIds = Array.Empty<string>();

        public JusticeInstitutionCategory Category => category;
        public IReadOnlyList<JurisdictionCategory> SupportedJurisdictions => supportedJurisdictions ?? Array.Empty<JurisdictionCategory>();
        public IReadOnlyList<JusticeCaseCategory> SupportedCases => supportedCases ?? Array.Empty<JusticeCaseCategory>();
        public IReadOnlyList<JusticeDecisionProcedure> SupportedProcedures => supportedProcedures ?? Array.Empty<JusticeDecisionProcedure>();
        public bool CanHoldCustody => canHoldCustody;
        public bool CanSentence => canSentence;
        public bool Appellate => appellate;
        public IReadOnlyList<string> RequiredAuthorityActionIds => JusticeModelUtility.C(requiredAuthorityActionIds);

        public void DevelopmentConfigure(string id, string name, JusticeInstitutionCategory institutionCategory, IEnumerable<JurisdictionCategory> jurisdictions, IEnumerable<JusticeCaseCategory> cases, IEnumerable<JusticeDecisionProcedure> procedures, bool holdsCustody, bool sentences, bool isAppellate, IEnumerable<string> authorityActions = null)
        {
            ConfigureBase(id, name, string.Empty, PoliticalVisibility.Public, new[] { "justice", "institution" });
            category = institutionCategory;
            supportedJurisdictions = (jurisdictions ?? Array.Empty<JurisdictionCategory>()).Where(item => item != JurisdictionCategory.Unknown).Distinct().ToArray();
            supportedCases = (cases ?? Array.Empty<JusticeCaseCategory>()).Where(item => item != JusticeCaseCategory.Unknown).Distinct().ToArray();
            supportedProcedures = (procedures ?? Array.Empty<JusticeDecisionProcedure>()).Where(item => item != JusticeDecisionProcedure.Unknown).Distinct().ToArray();
            canHoldCustody = holdsCustody;
            canSentence = sentences;
            appellate = isAppellate;
            requiredAuthorityActionIds = JusticeModelUtility.C(authorityActions);
        }

        public override void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            base.ValidateCatalogDefinition(definitionsById, report);
            if (category == JusticeInstitutionCategory.Unknown) report?.AddError($"Justice Institution '{DisplayName}' has no category.");
            if (SupportedJurisdictions.Count == 0) report?.AddError($"Justice Institution '{DisplayName}' supports no jurisdiction category.");
            if (SupportedCases.Count == 0) report?.AddError($"Justice Institution '{DisplayName}' supports no case category.");
            if (SupportedProcedures.Count == 0) report?.AddError($"Justice Institution '{DisplayName}' supports no decision procedure.");
        }
    }

    [CreateAssetMenu(fileName = "CourtDefinition", menuName = "Unity Isekai Game/Justice/Court Definition")]
    public class CourtDefinition : JusticeDefinitionBase
    {
        [SerializeField] private JusticeInstitutionCategory category;
        [SerializeField] private JusticeCaseCategory[] supportedCases = Array.Empty<JusticeCaseCategory>();
        [SerializeField] private ChargeCategory[] supportedCharges = Array.Empty<ChargeCategory>();
        [SerializeField] private bool firstInstance = true;
        [SerializeField] private bool appellate;
        [SerializeField] private int requiredJudgeCount = 1;
        [SerializeField] private StandardOfProofCategory defaultStandard = StandardOfProofCategory.BeyondReasonableDoubt;
        [SerializeField] private JudgmentOutcome[] availableOutcomes = Array.Empty<JudgmentOutcome>();
        [SerializeField] private SentenceCategory[] availableSentences = Array.Empty<SentenceCategory>();

        public JusticeInstitutionCategory Category => category;
        public IReadOnlyList<JusticeCaseCategory> SupportedCases => supportedCases ?? Array.Empty<JusticeCaseCategory>();
        public IReadOnlyList<ChargeCategory> SupportedCharges => supportedCharges ?? Array.Empty<ChargeCategory>();
        public bool FirstInstance => firstInstance;
        public bool Appellate => appellate;
        public int RequiredJudgeCount => Math.Max(0, requiredJudgeCount);
        public StandardOfProofCategory DefaultStandard => defaultStandard;
        public IReadOnlyList<JudgmentOutcome> AvailableOutcomes => availableOutcomes ?? Array.Empty<JudgmentOutcome>();
        public IReadOnlyList<SentenceCategory> AvailableSentences => availableSentences ?? Array.Empty<SentenceCategory>();

        public void DevelopmentConfigure(string id, string name, JusticeInstitutionCategory courtCategory, IEnumerable<JusticeCaseCategory> cases, IEnumerable<ChargeCategory> charges, bool isFirstInstance, bool isAppellate, int judges, StandardOfProofCategory standard, IEnumerable<JudgmentOutcome> outcomes, IEnumerable<SentenceCategory> sentences)
        {
            ConfigureBase(id, name, string.Empty, PoliticalVisibility.Public, new[] { "justice", "court" });
            category = courtCategory;
            supportedCases = (cases ?? Array.Empty<JusticeCaseCategory>()).Where(item => item != JusticeCaseCategory.Unknown).Distinct().ToArray();
            supportedCharges = (charges ?? Array.Empty<ChargeCategory>()).Where(item => item != ChargeCategory.Unknown).Distinct().ToArray();
            firstInstance = isFirstInstance;
            appellate = isAppellate;
            requiredJudgeCount = Math.Max(0, judges);
            defaultStandard = standard == StandardOfProofCategory.Unknown ? StandardOfProofCategory.BeyondReasonableDoubt : standard;
            availableOutcomes = (outcomes ?? Array.Empty<JudgmentOutcome>()).Where(item => item != JudgmentOutcome.Unknown).Distinct().ToArray();
            availableSentences = (sentences ?? Array.Empty<SentenceCategory>()).Where(item => item != SentenceCategory.Unknown).Distinct().ToArray();
        }

        public override void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            base.ValidateCatalogDefinition(definitionsById, report);
            if (category == JusticeInstitutionCategory.Unknown) report?.AddError($"Court Definition '{DisplayName}' has no category.");
            if (SupportedCases.Count == 0) report?.AddError($"Court Definition '{DisplayName}' supports no case category.");
            if (!firstInstance && !appellate) report?.AddError($"Court Definition '{DisplayName}' is neither first-instance nor appellate.");
            if (AvailableOutcomes.Count == 0) report?.AddError($"Court Definition '{DisplayName}' has no judgment outcomes.");
        }
    }

    [CreateAssetMenu(fileName = "ArrestDefinition", menuName = "Unity Isekai Game/Justice/Arrest Definition")]
    public class ArrestDefinition : JusticeDefinitionBase
    {
        [SerializeField] private ArrestCategory category;
        [SerializeField] private ArrestLegalBasisKind[] validLegalBases = Array.Empty<ArrestLegalBasisKind>();
        [SerializeField] private WarrantCategory[] requiredWarrantCategories = Array.Empty<WarrantCategory>();
        [SerializeField] private bool permitsWarrantlessArrest;
        [SerializeField] private bool createsCustody = true;
        [SerializeField] private double defaultDetentionReviewInterval = 24d;

        public ArrestCategory Category => category;
        public IReadOnlyList<ArrestLegalBasisKind> ValidLegalBases => validLegalBases ?? Array.Empty<ArrestLegalBasisKind>();
        public IReadOnlyList<WarrantCategory> RequiredWarrantCategories => requiredWarrantCategories ?? Array.Empty<WarrantCategory>();
        public bool PermitsWarrantlessArrest => permitsWarrantlessArrest;
        public bool CreatesCustody => createsCustody;
        public double DefaultDetentionReviewInterval => defaultDetentionReviewInterval;

        public void DevelopmentConfigure(string id, string name, ArrestCategory arrestCategory, IEnumerable<ArrestLegalBasisKind> bases, IEnumerable<WarrantCategory> warrants, bool warrantless, bool custody, double reviewInterval)
        {
            ConfigureBase(id, name, string.Empty, PoliticalVisibility.Restricted, new[] { "justice", "arrest" });
            category = arrestCategory;
            validLegalBases = (bases ?? Array.Empty<ArrestLegalBasisKind>()).Where(item => item != ArrestLegalBasisKind.Unknown).Distinct().ToArray();
            requiredWarrantCategories = (warrants ?? Array.Empty<WarrantCategory>()).Where(item => item != WarrantCategory.Unknown).Distinct().ToArray();
            permitsWarrantlessArrest = warrantless;
            createsCustody = custody;
            defaultDetentionReviewInterval = Math.Max(0d, reviewInterval);
        }

        public override void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            base.ValidateCatalogDefinition(definitionsById, report);
            if (category == ArrestCategory.Unknown) report?.AddError($"Arrest Definition '{DisplayName}' has no category.");
            if (ValidLegalBases.Count == 0) report?.AddError($"Arrest Definition '{DisplayName}' has no valid legal basis.");
        }
    }

    [CreateAssetMenu(fileName = "ChargeDefinition", menuName = "Unity Isekai Game/Justice/Charge Definition")]
    public class ChargeDefinition : JusticeDefinitionBase
    {
        [SerializeField] private ChargeCategory category;
        [SerializeField] private EvidenceSufficiencyState minimumFilingThreshold = EvidenceSufficiencyState.Substantial;
        [SerializeField] private StandardOfProofCategory trialStandard = StandardOfProofCategory.BeyondReasonableDoubt;
        [SerializeField] private OffenseCategory[] supportedOffenseCategories = Array.Empty<OffenseCategory>();

        public ChargeCategory Category => category;
        public EvidenceSufficiencyState MinimumFilingThreshold => minimumFilingThreshold;
        public StandardOfProofCategory TrialStandard => trialStandard;
        public IReadOnlyList<OffenseCategory> SupportedOffenseCategories => supportedOffenseCategories ?? Array.Empty<OffenseCategory>();

        public void DevelopmentConfigure(string id, string name, ChargeCategory chargeCategory, EvidenceSufficiencyState threshold, StandardOfProofCategory standard, IEnumerable<OffenseCategory> offenses)
        {
            ConfigureBase(id, name, string.Empty, PoliticalVisibility.Restricted, new[] { "justice", "charge" });
            category = chargeCategory;
            minimumFilingThreshold = threshold == EvidenceSufficiencyState.Unknown ? EvidenceSufficiencyState.Substantial : threshold;
            trialStandard = standard == StandardOfProofCategory.Unknown ? StandardOfProofCategory.BeyondReasonableDoubt : standard;
            supportedOffenseCategories = (offenses ?? Array.Empty<OffenseCategory>()).Where(item => item != OffenseCategory.Unknown).Distinct().ToArray();
        }

        public override void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            base.ValidateCatalogDefinition(definitionsById, report);
            if (category == ChargeCategory.Unknown) report?.AddError($"Charge Definition '{DisplayName}' has no category.");
            if (SupportedOffenseCategories.Count == 0) report?.AddError($"Charge Definition '{DisplayName}' supports no offense category.");
        }
    }

    [CreateAssetMenu(fileName = "HearingDefinition", menuName = "Unity Isekai Game/Justice/Hearing Definition")]
    public class HearingDefinition : JusticeDefinitionBase
    {
        [SerializeField] private HearingCategory category;
        [SerializeField] private bool permitsEvidenceRulings;
        [SerializeField] private bool permitsFindings;

        public HearingCategory Category => category;
        public bool PermitsEvidenceRulings => permitsEvidenceRulings;
        public bool PermitsFindings => permitsFindings;

        public void DevelopmentConfigure(string id, string name, HearingCategory hearingCategory, bool evidence, bool findings)
        {
            ConfigureBase(id, name, string.Empty, PoliticalVisibility.Restricted, new[] { "justice", "hearing" });
            category = hearingCategory;
            permitsEvidenceRulings = evidence;
            permitsFindings = findings;
        }

        public override void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            base.ValidateCatalogDefinition(definitionsById, report);
            if (category == HearingCategory.Unknown) report?.AddError($"Hearing Definition '{DisplayName}' has no category.");
        }
    }

    [CreateAssetMenu(fileName = "SentenceDefinition", menuName = "Unity Isekai Game/Justice/Sentence Definition")]
    public class SentenceDefinition : JusticeDefinitionBase
    {
        [SerializeField] private SentenceCategory category;
        [SerializeField] private bool requiresGuiltyOrLiableOutcome = true;
        [SerializeField] private bool createsCustody;
        [SerializeField] private bool usesEconomy;

        public SentenceCategory Category => category;
        public bool RequiresGuiltyOrLiableOutcome => requiresGuiltyOrLiableOutcome;
        public bool CreatesCustody => createsCustody;
        public bool UsesEconomy => usesEconomy;

        public void DevelopmentConfigure(string id, string name, SentenceCategory sentenceCategory, bool requiresLiability, bool custody, bool economy)
        {
            ConfigureBase(id, name, string.Empty, PoliticalVisibility.Restricted, new[] { "justice", "sentence" });
            category = sentenceCategory;
            requiresGuiltyOrLiableOutcome = requiresLiability;
            createsCustody = custody;
            usesEconomy = economy;
        }

        public override void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            base.ValidateCatalogDefinition(definitionsById, report);
            if (category == SentenceCategory.Unknown) report?.AddError($"Sentence Definition '{DisplayName}' has no category.");
        }
    }

    [CreateAssetMenu(fileName = "RemedyDefinition", menuName = "Unity Isekai Game/Justice/Remedy Definition")]
    public class RemedyDefinition : JusticeDefinitionBase
    {
        [SerializeField] private RemedyCategory category;
        public RemedyCategory Category => category;
        public void DevelopmentConfigure(string id, string name, RemedyCategory remedyCategory) { ConfigureBase(id, name, string.Empty, PoliticalVisibility.Restricted, new[] { "justice", "remedy" }); category = remedyCategory; }
        public override void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report) { base.ValidateCatalogDefinition(definitionsById, report); if (category == RemedyCategory.Unknown) report?.AddError($"Remedy Definition '{DisplayName}' has no category."); }
    }

    [CreateAssetMenu(fileName = "AppealDefinition", menuName = "Unity Isekai Game/Justice/Appeal Definition")]
    public class AppealDefinition : JusticeDefinitionBase
    {
        [SerializeField] private AppealCategory category;
        [SerializeField] private bool mayStayJudgment = true;
        [SerializeField] private bool mayStaySentence = true;
        public AppealCategory Category => category;
        public bool MayStayJudgment => mayStayJudgment;
        public bool MayStaySentence => mayStaySentence;
        public void DevelopmentConfigure(string id, string name, AppealCategory appealCategory, bool judgmentStay, bool sentenceStay) { ConfigureBase(id, name, string.Empty, PoliticalVisibility.Restricted, new[] { "justice", "appeal" }); category = appealCategory; mayStayJudgment = judgmentStay; mayStaySentence = sentenceStay; }
        public override void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report) { base.ValidateCatalogDefinition(definitionsById, report); if (category == AppealCategory.Unknown) report?.AddError($"Appeal Definition '{DisplayName}' has no category."); }
    }

    [CreateAssetMenu(fileName = "ClemencyDefinition", menuName = "Unity Isekai Game/Justice/Clemency Definition")]
    public class ClemencyDefinition : JusticeDefinitionBase
    {
        [SerializeField] private ClemencyCategory category;
        public ClemencyCategory Category => category;
        public void DevelopmentConfigure(string id, string name, ClemencyCategory clemencyCategory) { ConfigureBase(id, name, string.Empty, PoliticalVisibility.Restricted, new[] { "justice", "clemency" }); category = clemencyCategory; }
        public override void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report) { base.ValidateCatalogDefinition(definitionsById, report); if (category == ClemencyCategory.Unknown) report?.AddError($"Clemency Definition '{DisplayName}' has no category."); }
    }
}
