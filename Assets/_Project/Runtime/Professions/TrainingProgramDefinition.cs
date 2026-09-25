using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Professions
{
    [CreateAssetMenu(fileName = "TrainingProgramDefinition", menuName = "Unity Isekai Game/Professions/Training Program")]
    public sealed class TrainingProgramDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string programId;
        [SerializeField] private string displayName;
        [SerializeField] private string debugName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private TrainingProgramCategory category = TrainingProgramCategory.VocationalTraining;
        [SerializeField] private TrainingProgramFormality formality = TrainingProgramFormality.Informal;
        [SerializeField] private string[] relatedProfessionIds;
        [SerializeField] private string[] relatedSpecializationIds;
        [SerializeField] private double durationFoundationHours;
        [SerializeField] private string[] entryPathIds;
        [SerializeField] private string curriculumId;
        [SerializeField] private TrainingInstructorRequirementData[] instructorRequirements;
        [SerializeField, Min(0)] private int minimumLearners = 1;
        [SerializeField, Min(0)] private int maximumLearners = 1;
        [SerializeField] private string requiredOrganizationId;
        [SerializeField] private string[] requiredLocationIds;
        [SerializeField] private string[] requiredToolIds;
        [SerializeField] private string[] requiredStationIds;
        [SerializeField] private string[] requiredResourceIds;
        [SerializeField] private string[] completionRequirementIds;
        [SerializeField] private bool withdrawalAllowed = true;
        [SerializeField] private bool dismissalAllowed = true;
        [SerializeField] private bool failureAllowed = true;
        [SerializeField] private string defaultAccessPolicyId;
        [SerializeField, Min(1)] private int version = 1;
        [SerializeField] private string validationMetadata;

        public string Id => programId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string DebugName => string.IsNullOrWhiteSpace(debugName) ? DisplayName : debugName;
        public string Description => description ?? string.Empty;
        public TrainingProgramCategory Category => category;
        public TrainingProgramFormality Formality => formality;
        public IReadOnlyList<string> RelatedProfessionIds => relatedProfessionIds ?? Array.Empty<string>();
        public IReadOnlyList<string> RelatedSpecializationIds => relatedSpecializationIds ?? Array.Empty<string>();
        public double DurationFoundationHours => Math.Max(0d, durationFoundationHours);
        public IReadOnlyList<string> EntryPathIds => entryPathIds ?? Array.Empty<string>();
        public string CurriculumId => curriculumId ?? string.Empty;
        public IReadOnlyList<TrainingInstructorRequirementData> InstructorRequirements => (instructorRequirements ?? Array.Empty<TrainingInstructorRequirementData>()).Select(requirement => requirement?.Clone()).Where(requirement => requirement != null).ToArray();
        public int MinimumLearners => Math.Max(0, minimumLearners);
        public int MaximumLearners => Math.Max(MinimumLearners, maximumLearners);
        public string RequiredOrganizationId => requiredOrganizationId ?? string.Empty;
        public IReadOnlyList<string> RequiredLocationIds => requiredLocationIds ?? Array.Empty<string>();
        public IReadOnlyList<string> RequiredToolIds => requiredToolIds ?? Array.Empty<string>();
        public IReadOnlyList<string> RequiredStationIds => requiredStationIds ?? Array.Empty<string>();
        public IReadOnlyList<string> RequiredResourceIds => requiredResourceIds ?? Array.Empty<string>();
        public IReadOnlyList<string> CompletionRequirementIds => completionRequirementIds ?? Array.Empty<string>();
        public bool WithdrawalAllowed => withdrawalAllowed;
        public bool DismissalAllowed => dismissalAllowed;
        public bool FailureAllowed => failureAllowed;
        public string DefaultAccessPolicyId => defaultAccessPolicyId ?? string.Empty;
        public int Version => Math.Max(1, version);
        public string ValidationMetadata => validationMetadata ?? string.Empty;

        private void OnValidate()
        {
            programId = programId?.Trim();
            curriculumId = curriculumId?.Trim();
            durationFoundationHours = Math.Max(0d, durationFoundationHours);
            minimumLearners = Math.Max(0, minimumLearners);
            maximumLearners = Math.Max(minimumLearners, maximumLearners);
            version = Math.Max(1, version);
        }

        public void DevelopmentConfigure(
            string id,
            string label,
            TrainingProgramCategory programCategory,
            TrainingProgramFormality programFormality,
            string curriculum,
            string[] professions = null,
            string[] specializations = null,
            string[] entryPaths = null,
            TrainingInstructorRequirementData[] instructors = null,
            double durationHours = 0d,
            int minLearners = 1,
            int maxLearners = 1,
            string organization = "",
            string[] locations = null,
            string[] tools = null,
            string[] stations = null,
            string[] resources = null,
            string[] completionRequirements = null,
            bool allowWithdrawal = true,
            bool allowDismissal = true,
            bool allowFailure = true,
            string accessPolicy = "")
        {
            programId = id?.Trim();
            displayName = string.IsNullOrWhiteSpace(label) ? id : label;
            debugName = displayName;
            category = programCategory;
            formality = programFormality;
            curriculumId = curriculum?.Trim();
            relatedProfessionIds = Clean(professions);
            relatedSpecializationIds = Clean(specializations);
            entryPathIds = Clean(entryPaths);
            instructorRequirements = (instructors ?? Array.Empty<TrainingInstructorRequirementData>()).Select(requirement => requirement?.Clone()).Where(requirement => requirement != null).ToArray();
            durationFoundationHours = Math.Max(0d, durationHours);
            minimumLearners = Math.Max(0, minLearners);
            maximumLearners = Math.Max(minimumLearners, maxLearners);
            requiredOrganizationId = organization ?? string.Empty;
            requiredLocationIds = Clean(locations);
            requiredToolIds = Clean(tools);
            requiredStationIds = Clean(stations);
            requiredResourceIds = Clean(resources);
            completionRequirementIds = Clean(completionRequirements);
            withdrawalAllowed = allowWithdrawal;
            dismissalAllowed = allowDismissal;
            failureAllowed = allowFailure;
            defaultAccessPolicyId = accessPolicy ?? string.Empty;
            version = 1;
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"Training Program '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("training.program.", StringComparison.Ordinal))
            {
                report.AddWarning($"Training Program '{Id}' should use the 'training.program.' namespace prefix.");
            }

            ValidateEnum(category, nameof(TrainingProgramCategory), report);
            ValidateEnum(formality, nameof(TrainingProgramFormality), report);
            ValidateUnique(RelatedProfessionIds, "relatedProfessionIds", report);
            ValidateUnique(RelatedSpecializationIds, "relatedSpecializationIds", report);
            ValidateUnique(EntryPathIds, "entryPathIds", report);
            ValidateUnique(RequiredLocationIds, "requiredLocationIds", report);
            ValidateUnique(RequiredToolIds, "requiredToolIds", report);
            ValidateUnique(RequiredStationIds, "requiredStationIds", report);
            ValidateUnique(RequiredResourceIds, "requiredResourceIds", report);
            ValidateUnique(CompletionRequirementIds, "completionRequirementIds", report);

            ValidateReferences<ProfessionDefinition>(RelatedProfessionIds, "relatedProfessionIds", definitionsById, report);
            ValidateReferences<ProfessionSpecializationDefinition>(RelatedSpecializationIds, "relatedSpecializationIds", definitionsById, report);
            ValidateReferences<ProfessionEntryPathDefinition>(EntryPathIds, "entryPathIds", definitionsById, report);
            ValidateReference<TrainingCurriculumDefinition>(CurriculumId, "curriculumId", definitionsById, report);
            if (!string.IsNullOrWhiteSpace(DefaultAccessPolicyId))
            {
                ValidateReference<InformationAccessPolicyDefinition>(DefaultAccessPolicyId, "defaultAccessPolicyId", definitionsById, report);
            }

            foreach (TrainingInstructorRequirementData requirement in InstructorRequirements)
            {
                requirement.Validate($"Training Program '{DisplayName}' instructor requirement", definitionsById, report);
            }

            if (MinimumLearners > MaximumLearners)
            {
                report.AddError($"Training Program '{DisplayName}' has learner bounds {MinimumLearners}>{MaximumLearners}.");
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

        private static void ValidateEnum<T>(T value, string enumName, DefinitionValidationReport report)
            where T : struct, Enum
        {
            if (!Enum.IsDefined(typeof(T), value))
            {
                report.AddError($"Training Program has an invalid {enumName} value.");
            }
        }

        private void ValidateUnique(IReadOnlyList<string> ids, string field, DefinitionValidationReport report)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in ids ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(id) || !seen.Add(id))
                {
                    report.AddError($"Training Program '{DisplayName}' field '{field}' has duplicate or blank value '{id}'.");
                }
            }
        }

        private void ValidateReferences<TDefinition>(IReadOnlyList<string> ids, string field, IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
            where TDefinition : class, IGameDefinition
        {
            foreach (string id in ids ?? Array.Empty<string>())
            {
                ValidateReference<TDefinition>(id, field, definitionsById, report);
            }
        }

        private void ValidateReference<TDefinition>(string id, string field, IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
            where TDefinition : class, IGameDefinition
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                report.AddError($"Training Program '{DisplayName}' field '{field}' is missing.");
                return;
            }

            if (definitionsById == null || !definitionsById.TryGetValue(id, out IGameDefinition definition))
            {
                report.AddError($"Training Program '{DisplayName}' field '{field}' references missing definition '{id}'.");
                return;
            }

            if (definition is not TDefinition)
            {
                report.AddError($"Training Program '{DisplayName}' field '{field}' references '{id}' as {typeof(TDefinition).Name}, but found {definition.GetType().Name}.");
            }
        }
    }
}
