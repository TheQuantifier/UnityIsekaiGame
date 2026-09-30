using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Beings.Biology.Compatibility;
using UnityIsekaiGame.Beings.Biology.Hazards;
using UnityIsekaiGame.Beings.Biology.VitalProcesses;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Beings.Biology.BiologicalConditions
{
    [Serializable]
    public sealed class BiologicalConditionStageRule
    {
        [SerializeField] private BiologicalConditionStage stage = BiologicalConditionStage.Active;
        [SerializeField] private float minimumLoad;
        [SerializeField] private BiologicalConditionSeverity severity = BiologicalConditionSeverity.Minor;
        [SerializeField] private bool terminal;

        public BiologicalConditionStage Stage => stage;
        public float MinimumLoad => Mathf.Max(0f, minimumLoad);
        public BiologicalConditionSeverity Severity => severity;
        public bool Terminal => terminal;
    }

    [Serializable]
    public sealed class BiologicalConditionSymptomDefinition
    {
        [SerializeField] private string symptomId;
        [SerializeField] private string displayName;
        [SerializeField] private BiologicalConditionStage minimumStage = BiologicalConditionStage.Active;
        [SerializeField] private BiologicalConditionSeverity minimumSeverity = BiologicalConditionSeverity.Minor;
        [SerializeField] private string sourceContributionId;

        public string SymptomId => symptomId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? SymptomId : displayName;
        public BiologicalConditionStage MinimumStage => minimumStage;
        public BiologicalConditionSeverity MinimumSeverity => minimumSeverity;
        public string SourceContributionId => sourceContributionId ?? string.Empty;
    }

    [CreateAssetMenu(fileName = "BiologicalCondition", menuName = "Unity Isekai Game/Beings/Biology/Biological Condition")]
    public sealed class BiologicalConditionDefinition : ScriptableObject, IGameDefinition, ITaggedDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string conditionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 5)] private string description;
        [SerializeField] private BiologicalConditionFamily family = BiologicalConditionFamily.Disease;
        [SerializeField] private string strainId = "strain.alpha.default";
        [SerializeField] private BiologicalInteractionDefinition biologicalInteractionDefinition;
        [SerializeField] private BiologicalExposureRoute[] allowedRoutes = Array.Empty<BiologicalExposureRoute>();
        [SerializeField] private string[] targetAnatomyTagIds = Array.Empty<string>();
        [SerializeField] private string requiredAnatomyNodeId;
        [SerializeField] private bool requiresActiveInjury;
        [SerializeField] private BiologicalConditionStackingPolicy stackingPolicy = BiologicalConditionStackingPolicy.MergeByDefinitionAndStrain;
        [SerializeField] private float establishmentThreshold = 10f;
        [SerializeField] private float baseProgressionRate = 1f;
        [SerializeField] private float baseRegressionRate = 0.25f;
        [SerializeField] private float immuneClearanceRate = 0.5f;
        [SerializeField] private float symptomThreshold = 10f;
        [SerializeField] private BiologicalResourceDefinition vitalResource;
        [SerializeField] private float vitalPressurePerTick;
        [SerializeField] private BiologicalHazardDefinition hazardDefinition;
        [SerializeField] private float hazardRateMultiplier = 1f;
        [SerializeField] private DamageTypeDefinition damageType;
        [SerializeField] private float step6DamagePerTick;
        [SerializeField] private float recoveryRateMultiplier = 1f;
        [SerializeField] private bool grantsImmunityMemoryOnClear;
        [SerializeField] private BiologicalConditionTransmissionMode transmissionMode = BiologicalConditionTransmissionMode.None;
        [SerializeField] private float transmissionDose = 1f;
        [SerializeField] private BiologicalConditionReconciliationPolicy transformationPolicy = BiologicalConditionReconciliationPolicy.PreserveIfCompatible;
        [SerializeField] private BiologicalConditionReconciliationPolicy bodyReplacementPolicy = BiologicalConditionReconciliationPolicy.Clear;
        [SerializeField] private BiologicalConditionStageRule[] stageRules = Array.Empty<BiologicalConditionStageRule>();
        [SerializeField] private BiologicalConditionSymptomDefinition[] symptoms = Array.Empty<BiologicalConditionSymptomDefinition>();
        [SerializeField] private TagDefinition[] tags = Array.Empty<TagDefinition>();
        [SerializeField] private bool alphaEnabled = true;
        [SerializeField, TextArea(1, 3)] private string validationMetadata;

        public string Id => conditionId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public BiologicalConditionFamily Family => family;
        public string StrainId => string.IsNullOrWhiteSpace(strainId) ? "strain.alpha.default" : strainId;
        public BiologicalInteractionDefinition BiologicalInteractionDefinition => biologicalInteractionDefinition;
        public string BiologicalInteractionDefinitionId => biologicalInteractionDefinition == null ? string.Empty : biologicalInteractionDefinition.Id;
        public IReadOnlyList<BiologicalExposureRoute> AllowedRoutes => allowedRoutes ?? Array.Empty<BiologicalExposureRoute>();
        public IReadOnlyList<string> TargetAnatomyTagIds => targetAnatomyTagIds ?? Array.Empty<string>();
        public string RequiredAnatomyNodeId => requiredAnatomyNodeId ?? string.Empty;
        public bool RequiresActiveInjury => requiresActiveInjury;
        public BiologicalConditionStackingPolicy StackingPolicy => stackingPolicy;
        public float EstablishmentThreshold => Mathf.Max(0.001f, establishmentThreshold);
        public float BaseProgressionRate => Mathf.Max(0f, baseProgressionRate);
        public float BaseRegressionRate => Mathf.Max(0f, baseRegressionRate);
        public float ImmuneClearanceRate => Mathf.Max(0f, immuneClearanceRate);
        public float SymptomThreshold => Mathf.Max(0f, symptomThreshold);
        public BiologicalResourceDefinition VitalResource => vitalResource;
        public string VitalResourceId => vitalResource == null ? string.Empty : vitalResource.Id;
        public float VitalPressurePerTick => Mathf.Max(0f, vitalPressurePerTick);
        public BiologicalHazardDefinition HazardDefinition => hazardDefinition;
        public string HazardDefinitionId => hazardDefinition == null ? string.Empty : hazardDefinition.Id;
        public float HazardRateMultiplier => Mathf.Max(0f, hazardRateMultiplier);
        public DamageTypeDefinition DamageType => damageType;
        public float Step6DamagePerTick => Mathf.Max(0f, step6DamagePerTick);
        public float RecoveryRateMultiplier => Mathf.Max(0f, recoveryRateMultiplier);
        public bool GrantsImmunityMemoryOnClear => grantsImmunityMemoryOnClear;
        public BiologicalConditionTransmissionMode TransmissionMode => transmissionMode;
        public float TransmissionDose => Mathf.Max(0f, transmissionDose);
        public BiologicalConditionReconciliationPolicy TransformationPolicy => transformationPolicy;
        public BiologicalConditionReconciliationPolicy BodyReplacementPolicy => bodyReplacementPolicy;
        public IReadOnlyList<BiologicalConditionStageRule> StageRules => stageRules ?? Array.Empty<BiologicalConditionStageRule>();
        public IReadOnlyList<BiologicalConditionSymptomDefinition> Symptoms => symptoms ?? Array.Empty<BiologicalConditionSymptomDefinition>();
        public IReadOnlyList<TagDefinition> Tags => tags ?? Array.Empty<TagDefinition>();
        public bool AlphaEnabled => alphaEnabled;
        public string ValidationMetadata => validationMetadata ?? string.Empty;

        private void OnValidate()
        {
            conditionId = conditionId?.Trim();
            strainId = string.IsNullOrWhiteSpace(strainId) ? "strain.alpha.default" : strainId.Trim();
            requiredAnatomyNodeId = requiredAnatomyNodeId?.Trim();
            allowedRoutes = allowedRoutes == null ? Array.Empty<BiologicalExposureRoute>() : allowedRoutes.Distinct().OrderBy(route => route).ToArray();
            targetAnatomyTagIds = targetAnatomyTagIds == null ? Array.Empty<string>() : targetAnatomyTagIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            tags = tags == null ? Array.Empty<TagDefinition>() : tags.Where(tag => tag != null).Distinct().OrderBy(tag => tag.Id, StringComparer.Ordinal).ToArray();
        }

        public BiologicalConditionStageRule ResolveStage(float load)
        {
            BiologicalConditionStageRule[] rules = StageRules.Where(rule => rule != null).OrderBy(rule => rule.MinimumLoad).ToArray();
            return rules.LastOrDefault(rule => load >= rule.MinimumLoad) ?? rules.FirstOrDefault();
        }

        public bool AllowsRoute(BiologicalExposureRoute route)
        {
            return AllowedRoutes.Count == 0 || AllowedRoutes.Contains(route);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"BiologicalConditionDefinition '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("condition.biology.", StringComparison.Ordinal))
            {
                report.AddWarning($"BiologicalConditionDefinition '{Id}' should use the 'condition.biology.' namespace prefix.");
            }

            if (family == BiologicalConditionFamily.Unknown)
            {
                report.AddError($"BiologicalConditionDefinition '{DisplayName}' has an Unknown family.");
            }

            if (biologicalInteractionDefinition == null
                || definitionsById == null
                || !definitionsById.TryGetValue(BiologicalInteractionDefinitionId, out IGameDefinition interaction)
                || !ReferenceEquals(interaction, biologicalInteractionDefinition))
            {
                report.AddError($"BiologicalConditionDefinition '{DisplayName}' references missing Biological Interaction '{BiologicalInteractionDefinitionId}'.");
            }

            if (EstablishmentThreshold <= 0f)
            {
                report.AddError($"BiologicalConditionDefinition '{DisplayName}' has an invalid establishment threshold.");
            }

            if (vitalResource != null
                && (definitionsById == null || !definitionsById.TryGetValue(VitalResourceId, out IGameDefinition resource) || !ReferenceEquals(resource, vitalResource)))
            {
                report.AddError($"BiologicalConditionDefinition '{DisplayName}' references missing Biological Resource '{VitalResourceId}'.");
            }

            if (hazardDefinition != null
                && (definitionsById == null || !definitionsById.TryGetValue(HazardDefinitionId, out IGameDefinition hazard) || !ReferenceEquals(hazard, hazardDefinition)))
            {
                report.AddError($"BiologicalConditionDefinition '{DisplayName}' references missing Biological Hazard '{HazardDefinitionId}'.");
            }

            if (StageRules.Count == 0)
            {
                report.AddError($"BiologicalConditionDefinition '{DisplayName}' must author at least one stage rule.");
            }

            string[] symptomIds = Symptoms.Where(symptom => symptom != null).Select(symptom => symptom.SymptomId).Where(id => !string.IsNullOrWhiteSpace(id)).ToArray();
            foreach (string duplicate in symptomIds.GroupBy(id => id, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => group.Key))
            {
                report.AddError($"BiologicalConditionDefinition '{DisplayName}' has duplicate symptom ID '{duplicate}'.");
            }
        }
    }
}
