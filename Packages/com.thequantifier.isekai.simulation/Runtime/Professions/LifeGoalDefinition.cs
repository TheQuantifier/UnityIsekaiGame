using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Professions
{
    [CreateAssetMenu(menuName = "Unity Isekai/Professions/Life Goal Definition")]
    public sealed class LifeGoalDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private string debugName;
        [SerializeField] private LifeGoalCategory category = LifeGoalCategory.Custom;
        [SerializeField] private LifePathTargetSubjectType targetSubjectType = LifePathTargetSubjectType.Custom;
        [SerializeField] private string[] requiredProfessionIds = Array.Empty<string>();
        [SerializeField] private string[] requiredTrainingProgramIds = Array.Empty<string>();
        [SerializeField] private string[] requiredCredentialDefinitionIds = Array.Empty<string>();
        [SerializeField] private string[] requiredRankDefinitionIds = Array.Empty<string>();
        [SerializeField] private string[] requiredPositionDefinitionIds = Array.Empty<string>();
        [SerializeField] private string[] requiredActivityDefinitionIds = Array.Empty<string>();
        [SerializeField] private string[] requirementIds = Array.Empty<string>();
        [SerializeField] private LifePathCompletionPolicy completionPolicy = LifePathCompletionPolicy.AuthoritativeTargetExists;
        [SerializeField] private LifePathFailurePolicy failurePolicy = LifePathFailurePolicy.Manual;
        [SerializeField] private string deadlineFoundationId;
        [SerializeField] private string[] compatibleAspirationDefinitionIds = Array.Empty<string>();
        [SerializeField] private string[] dependencyGoalDefinitionIds = Array.Empty<string>();
        [SerializeField] private string[] alternativeGoalDefinitionIds = Array.Empty<string>();
        [SerializeField] private string[] conflictTags = Array.Empty<string>();
        [SerializeField] private string accessPolicyId;
        [SerializeField] private int version = 1;

        public string Id => id ?? string.Empty;
        public string DisplayName => displayName ?? string.Empty;
        public string DebugName => debugName ?? string.Empty;
        public LifeGoalCategory Category => category;
        public LifePathTargetSubjectType TargetSubjectType => targetSubjectType;
        public IReadOnlyList<string> RequiredProfessionIds => AspirationDefinition.Clean(requiredProfessionIds);
        public IReadOnlyList<string> RequiredTrainingProgramIds => AspirationDefinition.Clean(requiredTrainingProgramIds);
        public IReadOnlyList<string> RequiredCredentialDefinitionIds => AspirationDefinition.Clean(requiredCredentialDefinitionIds);
        public IReadOnlyList<string> RequiredRankDefinitionIds => AspirationDefinition.Clean(requiredRankDefinitionIds);
        public IReadOnlyList<string> RequiredPositionDefinitionIds => AspirationDefinition.Clean(requiredPositionDefinitionIds);
        public IReadOnlyList<string> RequiredActivityDefinitionIds => AspirationDefinition.Clean(requiredActivityDefinitionIds);
        public IReadOnlyList<string> RequirementIds => AspirationDefinition.Clean(requirementIds);
        public LifePathCompletionPolicy CompletionPolicy => completionPolicy;
        public LifePathFailurePolicy FailurePolicy => failurePolicy;
        public string DeadlineFoundationId => deadlineFoundationId ?? string.Empty;
        public IReadOnlyList<string> CompatibleAspirationDefinitionIds => AspirationDefinition.Clean(compatibleAspirationDefinitionIds);
        public IReadOnlyList<string> DependencyGoalDefinitionIds => AspirationDefinition.Clean(dependencyGoalDefinitionIds);
        public IReadOnlyList<string> AlternativeGoalDefinitionIds => AspirationDefinition.Clean(alternativeGoalDefinitionIds);
        public IReadOnlyList<string> ConflictTags => AspirationDefinition.Clean(conflictTags);
        public string AccessPolicyId => accessPolicyId ?? string.Empty;
        public int Version => Math.Max(1, version);

        public void DevelopmentConfigure(
            string definitionId,
            string name,
            LifeGoalCategory goalCategory,
            LifePathTargetSubjectType targetType,
            IEnumerable<string> professions = null,
            IEnumerable<string> trainingPrograms = null,
            IEnumerable<string> credentials = null,
            IEnumerable<string> ranks = null,
            IEnumerable<string> positions = null,
            IEnumerable<string> activities = null,
            IEnumerable<string> requirements = null,
            LifePathCompletionPolicy completion = LifePathCompletionPolicy.AuthoritativeTargetExists,
            LifePathFailurePolicy failure = LifePathFailurePolicy.Manual,
            string deadline = "",
            IEnumerable<string> compatibleAspirations = null,
            IEnumerable<string> dependencies = null,
            IEnumerable<string> alternatives = null,
            IEnumerable<string> conflicts = null,
            string policyId = "",
            int definitionVersion = 1)
        {
            id = definitionId ?? string.Empty;
            displayName = name ?? string.Empty;
            debugName = string.IsNullOrWhiteSpace(name) ? definitionId ?? string.Empty : name;
            category = goalCategory;
            targetSubjectType = targetType;
            requiredProfessionIds = AspirationDefinition.Clean(professions).ToArray();
            requiredTrainingProgramIds = AspirationDefinition.Clean(trainingPrograms).ToArray();
            requiredCredentialDefinitionIds = AspirationDefinition.Clean(credentials).ToArray();
            requiredRankDefinitionIds = AspirationDefinition.Clean(ranks).ToArray();
            requiredPositionDefinitionIds = AspirationDefinition.Clean(positions).ToArray();
            requiredActivityDefinitionIds = AspirationDefinition.Clean(activities).ToArray();
            requirementIds = AspirationDefinition.Clean(requirements).ToArray();
            completionPolicy = completion;
            failurePolicy = failure;
            deadlineFoundationId = deadline ?? string.Empty;
            compatibleAspirationDefinitionIds = AspirationDefinition.Clean(compatibleAspirations).ToArray();
            dependencyGoalDefinitionIds = AspirationDefinition.Clean(dependencies).ToArray();
            alternativeGoalDefinitionIds = AspirationDefinition.Clean(alternatives).ToArray();
            conflictTags = AspirationDefinition.Clean(conflicts).ToArray();
            accessPolicyId = policyId ?? string.Empty;
            version = Math.Max(1, definitionVersion);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(Id) || !Id.StartsWith("goal.", StringComparison.Ordinal))
            {
                report.AddError($"Goal definition '{DisplayName}' must use the 'goal.' namespace.");
            }

            if (string.IsNullOrWhiteSpace(DisplayName))
            {
                report.AddError($"Goal definition '{Id}' must declare a display name.");
            }

            ValidateReferences<ProfessionDefinition>(RequiredProfessionIds, "profession", definitionsById, report);
            ValidateReferences<TrainingProgramDefinition>(RequiredTrainingProgramIds, "training program", definitionsById, report);
            ValidateReferences<CredentialDefinition>(RequiredCredentialDefinitionIds, "credential", definitionsById, report);
            ValidateReferences<ProfessionalRankDefinition>(RequiredRankDefinitionIds, "rank", definitionsById, report);
            ValidateReferences<PositionDefinition>(RequiredPositionDefinitionIds, "position", definitionsById, report);
            ValidateReferences<ProfessionalActivityDefinition>(RequiredActivityDefinitionIds, "activity", definitionsById, report);
            foreach (string aspirationId in CompatibleAspirationDefinitionIds)
            {
                if (definitionsById == null || !definitionsById.TryGetValue(aspirationId, out IGameDefinition definition) || definition is not AspirationDefinition)
                {
                    report.AddError($"Goal definition '{DisplayName}' references missing Aspiration definition '{aspirationId}'.");
                }
            }

            foreach (string goalId in DependencyGoalDefinitionIds.Concat(AlternativeGoalDefinitionIds))
            {
                if (definitionsById == null || !definitionsById.TryGetValue(goalId, out IGameDefinition definition) || definition is not LifeGoalDefinition)
                {
                    report.AddError($"Goal definition '{DisplayName}' references missing Goal definition '{goalId}'.");
                }
            }

            if (DependencyGoalDefinitionIds.Contains(Id, StringComparer.Ordinal) || AlternativeGoalDefinitionIds.Contains(Id, StringComparer.Ordinal))
            {
                report.AddError($"Goal definition '{DisplayName}' cannot depend on or alternate with itself.");
            }
        }

        private void ValidateReferences<TDefinition>(IEnumerable<string> ids, string label, IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report) where TDefinition : class, IGameDefinition
        {
            foreach (string value in ids ?? Array.Empty<string>())
            {
                if (definitionsById == null || !definitionsById.TryGetValue(value, out IGameDefinition definition) || definition is not TDefinition)
                {
                    report.AddError($"Goal definition '{DisplayName}' references missing {label} definition '{value}'.");
                }
            }
        }
    }
}
