using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Beings.Biology.Compatibility;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Beings.Biology.BiologicalConditions
{
    [CreateAssetMenu(fileName = "BiologicalTreatment", menuName = "Unity Isekai Game/Beings/Biology/Biological Treatment")]
    public sealed class BiologicalConditionTreatmentDefinition : ScriptableObject, IGameDefinition, ITaggedDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string treatmentId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 5)] private string description;
        [SerializeField] private BiologicalConditionTreatmentKind treatmentKind = BiologicalConditionTreatmentKind.GeneralMedicine;
        [SerializeField] private BiologicalInteractionDefinition biologicalInteractionDefinition;
        [SerializeField] private BiologicalConditionDefinition[] compatibleConditions = Array.Empty<BiologicalConditionDefinition>();
        [SerializeField] private BiologicalConditionFamily[] compatibleFamilies = Array.Empty<BiologicalConditionFamily>();
        [SerializeField] private float loadReduction = 5f;
        [SerializeField] private float progressionMultiplier = 0.75f;
        [SerializeField] private bool canClearCondition;
        [SerializeField] private bool grantsImmunityMemory;
        [SerializeField] private TagDefinition[] tags = Array.Empty<TagDefinition>();
        [SerializeField] private bool alphaEnabled = true;
        [SerializeField, TextArea(1, 3)] private string validationMetadata;

        public string Id => treatmentId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public BiologicalConditionTreatmentKind TreatmentKind => treatmentKind;
        public BiologicalInteractionDefinition BiologicalInteractionDefinition => biologicalInteractionDefinition;
        public string BiologicalInteractionDefinitionId => biologicalInteractionDefinition == null ? string.Empty : biologicalInteractionDefinition.Id;
        public IReadOnlyList<BiologicalConditionDefinition> CompatibleConditions => compatibleConditions ?? Array.Empty<BiologicalConditionDefinition>();
        public IReadOnlyList<string> CompatibleConditionIds => CompatibleConditions.Select(definition => definition.Id).ToArray();
        public IReadOnlyList<BiologicalConditionFamily> CompatibleFamilies => compatibleFamilies ?? Array.Empty<BiologicalConditionFamily>();
        public float LoadReduction => Mathf.Max(0f, loadReduction);
        public float ProgressionMultiplier => Mathf.Max(0f, progressionMultiplier);
        public bool CanClearCondition => canClearCondition;
        public bool GrantsImmunityMemory => grantsImmunityMemory;
        public IReadOnlyList<TagDefinition> Tags => tags ?? Array.Empty<TagDefinition>();
        public bool AlphaEnabled => alphaEnabled;
        public string ValidationMetadata => validationMetadata ?? string.Empty;

        private void OnValidate()
        {
            treatmentId = treatmentId?.Trim();
            compatibleConditions = compatibleConditions == null ? Array.Empty<BiologicalConditionDefinition>() : compatibleConditions.Where(definition => definition != null).Distinct().OrderBy(definition => definition.Id, StringComparer.Ordinal).ToArray();
            compatibleFamilies = compatibleFamilies == null ? Array.Empty<BiologicalConditionFamily>() : compatibleFamilies.Where(family => family != BiologicalConditionFamily.Unknown).Distinct().OrderBy(family => family).ToArray();
            tags = tags == null ? Array.Empty<TagDefinition>() : tags.Where(tag => tag != null).Distinct().OrderBy(tag => tag.Id, StringComparer.Ordinal).ToArray();
        }

        public bool CanTreat(BiologicalConditionDefinition definition)
        {
            if (definition == null)
            {
                return false;
            }

            return CompatibleConditions.Contains(definition)
                || CompatibleFamilies.Contains(definition.Family);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"BiologicalConditionTreatmentDefinition '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("treatment.biology.", StringComparison.Ordinal))
            {
                report.AddWarning($"BiologicalConditionTreatmentDefinition '{Id}' should use the 'treatment.biology.' namespace prefix.");
            }

            if (biologicalInteractionDefinition == null
                || definitionsById == null
                || !definitionsById.TryGetValue(BiologicalInteractionDefinitionId, out IGameDefinition interaction)
                || !ReferenceEquals(interaction, biologicalInteractionDefinition))
            {
                report.AddError($"BiologicalConditionTreatmentDefinition '{DisplayName}' references missing Biological Interaction '{BiologicalInteractionDefinitionId}'.");
            }

            foreach (BiologicalConditionDefinition conditionDefinition in CompatibleConditions)
            {
                if (definitionsById == null || !definitionsById.TryGetValue(conditionDefinition.Id, out IGameDefinition condition) || !ReferenceEquals(condition, conditionDefinition))
                {
                    report.AddError($"BiologicalConditionTreatmentDefinition '{DisplayName}' references missing Biological Condition '{conditionDefinition.Id}'.");
                }
            }

            if (CompatibleConditionIds.Count == 0 && CompatibleFamilies.Count == 0)
            {
                report.AddError($"BiologicalConditionTreatmentDefinition '{DisplayName}' must target at least one condition or family.");
            }
        }
    }
}
