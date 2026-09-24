using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Beings.Biology.Compatibility;
using UnityIsekaiGame.Capabilities;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Beings.Biology.Transformation
{
    [CreateAssetMenu(fileName = "TransformationMethod", menuName = "Unity Isekai Game/Beings/Biology/Transformation Method")]
    public sealed class TransformationMethodDefinition : ScriptableObject, IGameDefinition, ITaggedDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string methodId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 5)] private string description;
        [SerializeField] private BiologicalInteractionDefinition biologicalInteractionDefinition;
        [SerializeField] private TransformationCategory category = TransformationCategory.Unknown;
        [SerializeField] private bool temporary;
        [SerializeField, Min(0f)] private float defaultDurationSeconds;
        [SerializeField] private SpeciesDefinition[] allowedTargetSpecies = Array.Empty<SpeciesDefinition>();
        [SerializeField] private BodyFormDefinition[] allowedTargetBodyForms = Array.Empty<BodyFormDefinition>();
        [SerializeField] private CapabilityDefinition[] requiredCapabilities = Array.Empty<CapabilityDefinition>();
        [SerializeField] private CapabilityDefinition[] blockingCapabilities = Array.Empty<CapabilityDefinition>();
        [SerializeField] private TransformationTransferPolicy transferPolicy = TransformationTransferPolicy.TransferPersonOwnedOnly;
        [SerializeField] private TransformationReconciliationPolicy anatomyPolicy = TransformationReconciliationPolicy.Rebuild;
        [SerializeField] private TransformationReconciliationPolicy conditionPolicy = TransformationReconciliationPolicy.Clear;
        [SerializeField] private TransformationReconciliationPolicy vitalPolicy = TransformationReconciliationPolicy.InitializeClean;
        [SerializeField] private TransformationReconciliationPolicy hazardPolicy = TransformationReconciliationPolicy.Clear;
        [SerializeField] private TransformationReconciliationPolicy recoveryPolicy = TransformationReconciliationPolicy.Cancel;
        [SerializeField] private TransformationEquipmentPolicy equipmentPolicy = TransformationEquipmentPolicy.PreserveIfCompatible;
        [SerializeField] private TransformationLifecyclePolicy lifecyclePolicy = TransformationLifecyclePolicy.Preserve;
        [SerializeField] private TransformationControllerPolicy controllerPolicy = TransformationControllerPolicy.PreserveController;
        [SerializeField] private TransformationAssociationPolicy associationPolicy = TransformationAssociationPolicy.PreserveBody;
        [SerializeField] private TransformationReversionPolicy reversionPolicy = TransformationReversionPolicy.None;
        [SerializeField] private bool allowSameSpecies;
        [SerializeField] private bool alphaExecutionEnabled = true;
        [SerializeField] private TagDefinition[] tags = Array.Empty<TagDefinition>();
        [SerializeField, TextArea(1, 3)] private string validationMetadata;

        public string Id => methodId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public BiologicalInteractionDefinition BiologicalInteractionDefinition => biologicalInteractionDefinition;
        public string BiologicalInteractionDefinitionId => biologicalInteractionDefinition == null ? string.Empty : biologicalInteractionDefinition.Id;
        public TransformationCategory Category => category;
        public bool Temporary => temporary;
        public float DefaultDurationSeconds => Mathf.Max(0f, defaultDurationSeconds);
        public IReadOnlyList<SpeciesDefinition> AllowedTargetSpecies => allowedTargetSpecies ?? Array.Empty<SpeciesDefinition>();
        public IReadOnlyList<BodyFormDefinition> AllowedTargetBodyForms => allowedTargetBodyForms ?? Array.Empty<BodyFormDefinition>();
        public IReadOnlyList<CapabilityDefinition> RequiredCapabilities => requiredCapabilities ?? Array.Empty<CapabilityDefinition>();
        public IReadOnlyList<CapabilityDefinition> BlockingCapabilities => blockingCapabilities ?? Array.Empty<CapabilityDefinition>();
        public IReadOnlyList<string> AllowedTargetSpeciesIds => AllowedTargetSpecies.Select(definition => definition.Id).ToArray();
        public IReadOnlyList<string> AllowedTargetBodyFormIds => AllowedTargetBodyForms.Select(definition => definition.Id).ToArray();
        public IReadOnlyList<string> RequiredRuntimeCapabilityKeys => RequiredCapabilities.Select(definition => definition.Id).ToArray();
        public IReadOnlyList<string> BlockingRuntimeCapabilityKeys => BlockingCapabilities.Select(definition => definition.Id).ToArray();
        public TransformationTransferPolicy TransferPolicy => transferPolicy;
        public TransformationReconciliationPolicy AnatomyPolicy => anatomyPolicy;
        public TransformationReconciliationPolicy ConditionPolicy => conditionPolicy;
        public TransformationReconciliationPolicy VitalPolicy => vitalPolicy;
        public TransformationReconciliationPolicy HazardPolicy => hazardPolicy;
        public TransformationReconciliationPolicy RecoveryPolicy => recoveryPolicy;
        public TransformationEquipmentPolicy EquipmentPolicy => equipmentPolicy;
        public TransformationLifecyclePolicy LifecyclePolicy => lifecyclePolicy;
        public TransformationControllerPolicy ControllerPolicy => controllerPolicy;
        public TransformationAssociationPolicy AssociationPolicy => associationPolicy;
        public TransformationReversionPolicy ReversionPolicy => reversionPolicy;
        public bool AllowSameSpecies => allowSameSpecies;
        public bool AlphaExecutionEnabled => alphaExecutionEnabled;
        public IReadOnlyList<TagDefinition> Tags => tags ?? Array.Empty<TagDefinition>();
        public string ValidationMetadata => validationMetadata ?? string.Empty;

        private void OnValidate()
        {
            methodId = methodId?.Trim();
            displayName = displayName?.Trim();
            defaultDurationSeconds = Mathf.Max(0f, defaultDurationSeconds);
            allowedTargetSpecies = Normalize(allowedTargetSpecies);
            allowedTargetBodyForms = Normalize(allowedTargetBodyForms);
            requiredCapabilities = Normalize(requiredCapabilities);
            blockingCapabilities = Normalize(blockingCapabilities);
        }

        public bool AllowsTargetSpecies(string speciesId)
        {
            return AllowedTargetSpeciesIds.Count == 0 || AllowedTargetSpeciesIds.Contains(speciesId ?? string.Empty, StringComparer.Ordinal);
        }

        public bool AllowsTargetBodyForm(string bodyFormId)
        {
            return AllowedTargetBodyFormIds.Count == 0 || AllowedTargetBodyFormIds.Contains(bodyFormId ?? string.Empty, StringComparer.Ordinal);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"TransformationMethodDefinition '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("transformation.", StringComparison.Ordinal))
            {
                report.AddWarning($"TransformationMethodDefinition '{Id}' should use the 'transformation.' namespace prefix.");
            }

            if (!Enum.IsDefined(typeof(TransformationCategory), category) || category == TransformationCategory.Unknown)
            {
                report.AddError($"TransformationMethodDefinition '{DisplayName}' has an invalid category.");
            }

            if (biologicalInteractionDefinition == null
                || definitionsById == null
                || !definitionsById.TryGetValue(BiologicalInteractionDefinitionId, out IGameDefinition interaction)
                || !ReferenceEquals(interaction, biologicalInteractionDefinition))
            {
                report.AddError($"TransformationMethodDefinition '{DisplayName}' references missing Biological Interaction '{BiologicalInteractionDefinitionId}'.");
            }

            foreach (string speciesId in AllowedTargetSpeciesIds)
            {
                if (definitionsById == null || !definitionsById.TryGetValue(speciesId, out IGameDefinition species) || species is not SpeciesDefinition)
                {
                    report.AddError($"TransformationMethodDefinition '{DisplayName}' references missing target Species '{speciesId}'.");
                }
            }

            foreach (string bodyFormId in AllowedTargetBodyFormIds)
            {
                if (definitionsById == null || !definitionsById.TryGetValue(bodyFormId, out IGameDefinition bodyForm) || bodyForm is not BodyFormDefinition)
                {
                    report.AddError($"TransformationMethodDefinition '{DisplayName}' references missing target body form '{bodyFormId}'.");
                }
            }

            foreach (CapabilityDefinition capability in RequiredCapabilities.Concat(BlockingCapabilities))
            {
                if (definitionsById == null || !definitionsById.TryGetValue(capability.Id, out IGameDefinition registered) || !ReferenceEquals(registered, capability))
                {
                    report.AddError($"TransformationMethodDefinition '{DisplayName}' references missing Capability '{capability.Id}'.");
                }
            }

            if (temporary && reversionPolicy == TransformationReversionPolicy.None)
            {
                report.AddError($"TransformationMethodDefinition '{DisplayName}' is temporary but has no reversion policy.");
            }

            ValidateCanonicalAlphaSet(definitionsById, report);
        }

        private static void ValidateCanonicalAlphaSet(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (definitionsById == null || !definitionsById.ContainsKey("species.human"))
            {
                return;
            }

            TransformationMethodDefinition first = definitionsById.Values.OfType<TransformationMethodDefinition>().OrderBy(definition => definition.Id, StringComparer.Ordinal).FirstOrDefault();
            if (first == null || !ReferenceEquals(first, definitionsById.Values.OfType<TransformationMethodDefinition>().FirstOrDefault(definition => definition.Id == first.Id)))
            {
                return;
            }

            string[] required =
            {
                "transformation.polymorph.temporary",
                "transformation.species-change.permanent",
                "transformation.body-form-change",
                "transformation.body-replacement",
                "transformation.body-swap",
                "transformation.possession",
                "transformation.reincarnation",
                "transformation.resurrection-body",
                "transformation.spirit-embodiment",
                "transformation.structure-replacement",
                "transformation.organ-replacement",
                "transformation.limb-replacement",
                "transformation.construct-component-replacement"
            };

            foreach (string id in required)
            {
                if (!definitionsById.TryGetValue(id, out IGameDefinition definition) || definition is not TransformationMethodDefinition)
                {
                    report.AddError($"Canonical TransformationMethodDefinition '{id}' must be registered in the alpha definition catalog.");
                }
            }
        }

        private static T[] Normalize<T>(T[] values) where T : UnityEngine.Object, IGameDefinition
        {
            return values == null ? Array.Empty<T>() : values.Where(value => value != null).Distinct().OrderBy(value => value.Id, StringComparer.Ordinal).ToArray();
        }
    }
}
