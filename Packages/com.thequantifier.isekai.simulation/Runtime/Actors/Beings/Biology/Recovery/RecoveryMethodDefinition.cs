using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Beings.Biology.Compatibility;
using UnityIsekaiGame.Beings.Biology.VitalProcesses;
using UnityIsekaiGame.Capabilities;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Beings.Biology.Recovery
{
    [CreateAssetMenu(fileName = "RecoveryMethod", menuName = "Unity Isekai Game/Beings/Biology/Recovery Method")]
    public sealed class RecoveryMethodDefinition : ScriptableObject, IGameDefinition, ITaggedDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string methodId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 5)] private string description;
        [SerializeField] private BiologicalInteractionDefinition biologicalInteractionDefinition;
        [SerializeField] private RecoveryCategory category = RecoveryCategory.Unknown;
        [SerializeField] private RecoveryTargetCategory[] supportedTargets = Array.Empty<RecoveryTargetCategory>();
        [SerializeField, Min(0f)] private float baseProgressPerHour = 1f;
        [SerializeField, Min(0f)] private float structuralIntegrityPerHour;
        [SerializeField, Min(0f)] private float vitalResourcePerHour;
        [SerializeField] private RecoveryAllocationPolicy allocationPolicy = RecoveryAllocationPolicy.ProfileDefined;
        [SerializeField] private RecoveryLimit recoveryLimit = RecoveryLimit.RestorePartialIntegrity;
        [SerializeField, Range(0f, 1f)] private float maximumRecoverableIntegrityPercent = 1f;
        [SerializeField] private bool canRestoreDestroyedStructure;
        [SerializeField] private bool canRestoreMissingStructure;
        [SerializeField] private bool canRestoreSeveredStructure;
        [SerializeField] private bool resolvesInjuryOnCompletion;
        [SerializeField] private bool requiresRestContext;
        [SerializeField] private RecoveryRestType[] allowedRestTypes = Array.Empty<RecoveryRestType>();
        [SerializeField] private CapabilityDefinition[] requiredCapabilities = Array.Empty<CapabilityDefinition>();
        [SerializeField] private CapabilityDefinition[] blockingCapabilities = Array.Empty<CapabilityDefinition>();
        [SerializeField] private string[] compatibleInjuryTypeIds = Array.Empty<string>();
        [SerializeField] private string[] compatibleAnatomyTagIds = Array.Empty<string>();
        [SerializeField] private BiologicalResourceDefinition[] restoredResources = Array.Empty<BiologicalResourceDefinition>();
        [SerializeField] private RecoveryPermanentOutcome permanentOutcome = RecoveryPermanentOutcome.None;
        [SerializeField] private RecoveryInterruptionPolicy interruptionPolicy = RecoveryInterruptionPolicy.PauseAndPreserveProgress;
        [SerializeField] private bool alphaExecutionEnabled = true;
        [SerializeField] private TagDefinition[] tags = Array.Empty<TagDefinition>();
        [SerializeField, TextArea(1, 3)] private string validationMetadata;

        public string Id => methodId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public BiologicalInteractionDefinition BiologicalInteractionDefinition => biologicalInteractionDefinition;
        public string BiologicalInteractionDefinitionId => biologicalInteractionDefinition == null ? string.Empty : biologicalInteractionDefinition.Id;
        public RecoveryCategory Category => category;
        public IReadOnlyList<RecoveryTargetCategory> SupportedTargets => supportedTargets ?? Array.Empty<RecoveryTargetCategory>();
        public float BaseProgressPerHour => Mathf.Max(0f, baseProgressPerHour);
        public float StructuralIntegrityPerHour => Mathf.Max(0f, structuralIntegrityPerHour);
        public float VitalResourcePerHour => Mathf.Max(0f, vitalResourcePerHour);
        public RecoveryAllocationPolicy AllocationPolicy => allocationPolicy;
        public RecoveryLimit RecoveryLimit => recoveryLimit;
        public float MaximumRecoverableIntegrityPercent => Mathf.Clamp01(maximumRecoverableIntegrityPercent);
        public bool CanRestoreDestroyedStructure => canRestoreDestroyedStructure;
        public bool CanRestoreMissingStructure => canRestoreMissingStructure;
        public bool CanRestoreSeveredStructure => canRestoreSeveredStructure;
        public bool ResolvesInjuryOnCompletion => resolvesInjuryOnCompletion;
        public bool RequiresRestContext => requiresRestContext;
        public IReadOnlyList<RecoveryRestType> AllowedRestTypes => allowedRestTypes ?? Array.Empty<RecoveryRestType>();
        public IReadOnlyList<CapabilityDefinition> RequiredCapabilities => requiredCapabilities ?? Array.Empty<CapabilityDefinition>();
        public IReadOnlyList<CapabilityDefinition> BlockingCapabilities => blockingCapabilities ?? Array.Empty<CapabilityDefinition>();
        public IReadOnlyList<string> RequiredRuntimeCapabilityKeys => RequiredCapabilities.Select(definition => definition.Id).ToArray();
        public IReadOnlyList<string> BlockingRuntimeCapabilityKeys => BlockingCapabilities.Select(definition => definition.Id).ToArray();
        public IReadOnlyList<string> CompatibleInjuryTypeIds => compatibleInjuryTypeIds ?? Array.Empty<string>();
        public IReadOnlyList<string> CompatibleAnatomyTagIds => compatibleAnatomyTagIds ?? Array.Empty<string>();
        public IReadOnlyList<BiologicalResourceDefinition> RestoredResources => restoredResources ?? Array.Empty<BiologicalResourceDefinition>();
        public IReadOnlyList<string> RestoredResourceIds => RestoredResources.Select(definition => definition.Id).ToArray();
        public RecoveryPermanentOutcome PermanentOutcome => permanentOutcome;
        public RecoveryInterruptionPolicy InterruptionPolicy => interruptionPolicy;
        public bool AlphaExecutionEnabled => alphaExecutionEnabled;
        public IReadOnlyList<TagDefinition> Tags => tags ?? Array.Empty<TagDefinition>();
        public string ValidationMetadata => validationMetadata ?? string.Empty;

        private void OnValidate()
        {
            methodId = methodId?.Trim();
            displayName = displayName?.Trim();
            baseProgressPerHour = Mathf.Max(0f, baseProgressPerHour);
            structuralIntegrityPerHour = Mathf.Max(0f, structuralIntegrityPerHour);
            vitalResourcePerHour = Mathf.Max(0f, vitalResourcePerHour);
            maximumRecoverableIntegrityPercent = Mathf.Clamp01(maximumRecoverableIntegrityPercent);
            requiredCapabilities = Normalize(requiredCapabilities);
            blockingCapabilities = Normalize(blockingCapabilities);
            compatibleInjuryTypeIds = Normalize(compatibleInjuryTypeIds);
            compatibleAnatomyTagIds = Normalize(compatibleAnatomyTagIds);
            restoredResources = Normalize(restoredResources);
        }

        public bool SupportsTarget(RecoveryTargetCategory target)
        {
            return SupportedTargets.Contains(target);
        }

        public bool RestoresResource(string resourceId)
        {
            return RestoredResourceIds.Count == 0 || RestoredResourceIds.Contains(resourceId ?? string.Empty, StringComparer.Ordinal);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"RecoveryMethodDefinition '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("recovery.", StringComparison.Ordinal))
            {
                report.AddWarning($"RecoveryMethodDefinition '{Id}' should use the 'recovery.' namespace prefix.");
            }

            if (string.IsNullOrWhiteSpace(DisplayName))
            {
                report.AddError($"RecoveryMethodDefinition '{Id}' is missing a display name.");
            }

            if (!Enum.IsDefined(typeof(RecoveryCategory), category) || category == RecoveryCategory.Unknown)
            {
                report.AddError($"RecoveryMethodDefinition '{DisplayName}' has an invalid category.");
            }

            if (biologicalInteractionDefinition == null
                || definitionsById == null
                || !definitionsById.TryGetValue(BiologicalInteractionDefinitionId, out IGameDefinition interaction)
                || !ReferenceEquals(interaction, biologicalInteractionDefinition))
            {
                report.AddError($"RecoveryMethodDefinition '{DisplayName}' references missing Biological Interaction '{BiologicalInteractionDefinitionId}'.");
            }

            foreach (CapabilityDefinition capability in RequiredCapabilities.Concat(BlockingCapabilities))
            {
                if (definitionsById == null || !definitionsById.TryGetValue(capability.Id, out IGameDefinition registered) || !ReferenceEquals(registered, capability))
                {
                    report.AddError($"RecoveryMethodDefinition '{DisplayName}' references missing Capability '{capability.Id}'.");
                }
            }

            foreach (BiologicalResourceDefinition resourceDefinition in RestoredResources)
            {
                if (definitionsById == null || !definitionsById.TryGetValue(resourceDefinition.Id, out IGameDefinition resource) || !ReferenceEquals(resource, resourceDefinition))
                {
                    report.AddError($"RecoveryMethodDefinition '{DisplayName}' references missing Biological Resource '{resourceDefinition.Id}'.");
                }
            }

            if (BaseProgressPerHour <= 0f && StructuralIntegrityPerHour <= 0f && VitalResourcePerHour <= 0f)
            {
                report.AddError($"RecoveryMethodDefinition '{DisplayName}' has no recovery rate.");
            }
        }

        private static string[] Normalize(string[] values)
        {
            return values == null ? Array.Empty<string>() : values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        private static T[] Normalize<T>(T[] values) where T : UnityEngine.Object, IGameDefinition
        {
            return values == null ? Array.Empty<T>() : values.Where(value => value != null).Distinct().OrderBy(value => value.Id, StringComparer.Ordinal).ToArray();
        }
    }
}
