using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Professions
{
    [CreateAssetMenu(menuName = "Unity Isekai/Professions/Professional Activity Definition")]
    public sealed class ProfessionalActivityDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private string debugName;
        [SerializeField] private string description;
        [SerializeField] private ProfessionalActivityCategory category = ProfessionalActivityCategory.Custom;
        [SerializeField] private string[] applicableProfessionIds = Array.Empty<string>();
        [SerializeField] private string[] applicableSpecializationIds = Array.Empty<string>();
        [SerializeField] private ProfessionalActivitySourceType[] acceptedSourceTypes = Array.Empty<ProfessionalActivitySourceType>();
        [SerializeField] private string[] requiredActivityTags = Array.Empty<string>();
        [SerializeField] private ProfessionalActivityOutcomeState minimumSuccessState = ProfessionalActivityOutcomeState.Successful;
        [SerializeField] private int minimumQuality;
        [SerializeField] private ProfessionalActivityDifficulty minimumDifficulty = ProfessionalActivityDifficulty.Unknown;
        [SerializeField] private ProfessionalSupervisionPolicy supervisionPolicy = ProfessionalSupervisionPolicy.Any;
        [SerializeField] private ProfessionalIndependentWorkPolicy independentWorkPolicy = ProfessionalIndependentWorkPolicy.Any;
        [SerializeField] private string quantityOrDurationInterpretation;
        [SerializeField] private ProfessionalActivityDifficulty defaultDifficulty = ProfessionalActivityDifficulty.Routine;
        [SerializeField] private ProfessionalRepetitionPolicy repetitionPolicy = ProfessionalRepetitionPolicy.PreserveAll;
        [SerializeField] private ProfessionalFailureCreditPolicy failureCreditPolicy = ProfessionalFailureCreditPolicy.NoCredit;
        [SerializeField] private ProfessionalCreditPolicy creditPolicy = ProfessionalCreditPolicy.Exclusive;
        [SerializeField] private string[] evidenceRequirementIds = Array.Empty<string>();
        [SerializeField] private string accessPolicyId;
        [SerializeField] private int version = 1;

        public string Id => id ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public string DebugName => debugName ?? string.Empty;
        public string Description => description ?? string.Empty;
        public ProfessionalActivityCategory Category => category;
        public IReadOnlyList<string> ApplicableProfessionIds => Clean(applicableProfessionIds);
        public IReadOnlyList<string> ApplicableSpecializationIds => Clean(applicableSpecializationIds);
        public IReadOnlyList<ProfessionalActivitySourceType> AcceptedSourceTypes => (acceptedSourceTypes ?? Array.Empty<ProfessionalActivitySourceType>()).Distinct().OrderBy(value => value).ToArray();
        public IReadOnlyList<string> RequiredActivityTags => Clean(requiredActivityTags);
        public ProfessionalActivityOutcomeState MinimumSuccessState => minimumSuccessState;
        public int MinimumQuality => Math.Max(0, minimumQuality);
        public ProfessionalActivityDifficulty MinimumDifficulty => minimumDifficulty;
        public ProfessionalSupervisionPolicy SupervisionPolicy => supervisionPolicy;
        public ProfessionalIndependentWorkPolicy IndependentWorkPolicy => independentWorkPolicy;
        public string QuantityOrDurationInterpretation => quantityOrDurationInterpretation ?? string.Empty;
        public ProfessionalActivityDifficulty DefaultDifficulty => defaultDifficulty;
        public ProfessionalRepetitionPolicy RepetitionPolicy => repetitionPolicy;
        public ProfessionalFailureCreditPolicy FailureCreditPolicy => failureCreditPolicy;
        public ProfessionalCreditPolicy CreditPolicy => creditPolicy;
        public IReadOnlyList<string> EvidenceRequirementIds => Clean(evidenceRequirementIds);
        public string AccessPolicyId => accessPolicyId ?? string.Empty;
        public int Version => Math.Max(1, version);

        public void DevelopmentConfigure(
            string stableId,
            string name,
            ProfessionalActivityCategory activityCategory,
            IEnumerable<string> professionIds,
            IEnumerable<ProfessionalActivitySourceType> sourceTypes,
            IEnumerable<string> specializationIds = null,
            ProfessionalSupervisionPolicy supervision = ProfessionalSupervisionPolicy.Any,
            ProfessionalIndependentWorkPolicy independent = ProfessionalIndependentWorkPolicy.Any,
            ProfessionalCreditPolicy credit = ProfessionalCreditPolicy.Exclusive,
            ProfessionalFailureCreditPolicy failureCredit = ProfessionalFailureCreditPolicy.NoCredit,
            ProfessionalRepetitionPolicy repetition = ProfessionalRepetitionPolicy.PreserveAll,
            int minQuality = 0,
            ProfessionalActivityDifficulty minDifficulty = ProfessionalActivityDifficulty.Unknown,
            IEnumerable<string> tags = null,
            string policyId = "",
            int definitionVersion = 1)
        {
            id = stableId ?? string.Empty;
            displayName = name ?? stableId ?? string.Empty;
            debugName = displayName;
            category = activityCategory;
            applicableProfessionIds = Clean(professionIds);
            applicableSpecializationIds = Clean(specializationIds);
            acceptedSourceTypes = (sourceTypes ?? Array.Empty<ProfessionalActivitySourceType>()).Distinct().OrderBy(value => value).ToArray();
            requiredActivityTags = Clean(tags);
            supervisionPolicy = supervision;
            independentWorkPolicy = independent;
            creditPolicy = credit;
            failureCreditPolicy = failureCredit;
            repetitionPolicy = repetition;
            minimumQuality = Math.Max(0, minQuality);
            minimumDifficulty = minDifficulty;
            defaultDifficulty = minDifficulty == ProfessionalActivityDifficulty.Unknown ? ProfessionalActivityDifficulty.Routine : minDifficulty;
            accessPolicyId = policyId ?? string.Empty;
            version = Math.Max(1, definitionVersion);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError("Professional Activity definition has no stable ID.");
            }
            else if (!Id.StartsWith("profession.activity.", StringComparison.Ordinal))
            {
                report.AddWarning($"Professional Activity definition '{DisplayName}' should use the 'profession.activity.' namespace prefix.");
            }

            if (ApplicableProfessionIds.Count == 0)
            {
                report.AddError($"Professional Activity definition '{DisplayName}' must reference at least one profession.");
            }

            foreach (string professionId in ApplicableProfessionIds)
            {
                if (definitionsById == null || !definitionsById.TryGetValue(professionId, out IGameDefinition definition) || definition is not ProfessionDefinition)
                {
                    report.AddError($"Professional Activity definition '{DisplayName}' references missing Profession '{professionId}'.");
                }
            }

            foreach (string specializationId in ApplicableSpecializationIds)
            {
                if (definitionsById == null || !definitionsById.TryGetValue(specializationId, out IGameDefinition definition) || definition is not ProfessionSpecializationDefinition)
                {
                    report.AddError($"Professional Activity definition '{DisplayName}' references missing Profession Specialization '{specializationId}'.");
                }
            }

            if (AcceptedSourceTypes.Count == 0)
            {
                report.AddError($"Professional Activity definition '{DisplayName}' must accept at least one source-record type.");
            }

            if (MinimumQuality < 0)
            {
                report.AddError($"Professional Activity definition '{DisplayName}' has invalid minimum quality.");
            }

            if (Version <= 0)
            {
                report.AddError($"Professional Activity definition '{DisplayName}' has invalid version.");
            }
        }

        private static string[] Clean(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }
    }
}
