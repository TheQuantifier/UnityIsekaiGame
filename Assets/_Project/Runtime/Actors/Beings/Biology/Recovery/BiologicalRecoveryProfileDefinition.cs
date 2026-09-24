using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Beings.Biology.Recovery
{
    [Serializable]
    public sealed class RecoveryProfileMethodEntry
    {
        [SerializeField] private RecoveryMethodDefinition recoveryMethod;
        [SerializeField] private bool enabled = true;
        [SerializeField, Min(0f)] private float rateMultiplier = 1f;
        [SerializeField, Min(0)] private int priority;
        [SerializeField] private bool autoStart;
        [SerializeField] private RecoveryAllocationPolicy allocationPolicy = RecoveryAllocationPolicy.ProfileDefined;
        [SerializeField] private RecoveryLimit recoveryLimit = RecoveryLimit.MethodSpecific;

        public RecoveryMethodDefinition RecoveryMethod => recoveryMethod;
        public string RecoveryMethodId => recoveryMethod == null ? string.Empty : recoveryMethod.Id;
        public bool Enabled => enabled;
        public float RateMultiplier => Mathf.Max(0f, rateMultiplier);
        public int Priority => priority;
        public bool AutoStart => autoStart;
        public RecoveryAllocationPolicy AllocationPolicy => allocationPolicy;
        public RecoveryLimit RecoveryLimit => recoveryLimit;
    }

    [CreateAssetMenu(fileName = "BiologicalRecoveryProfile", menuName = "Unity Isekai Game/Beings/Biology/Biological Recovery Profile")]
    public sealed class BiologicalRecoveryProfileDefinition : ScriptableObject, IGameDefinition, ITaggedDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string profileId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 5)] private string description;
        [SerializeField] private SpeciesDefinition speciesDefinition;
        [SerializeField] private BodyFormDefinition bodyFormDefinition;
        [SerializeField] private BiologicalClassificationDefinition biologicalClassification;
        [SerializeField, Min(0f)] private float baseRecoveryCapacityPerHour = 1f;
        [SerializeField] private RecoveryAllocationPolicy allocationPolicy = RecoveryAllocationPolicy.VitalStructuresFirst;
        [SerializeField] private RecoveryProfileMethodEntry[] methods = Array.Empty<RecoveryProfileMethodEntry>();
        [SerializeField] private Hazards.BiologicalHazardDefinition[] blockingHazards = Array.Empty<Hazards.BiologicalHazardDefinition>();
        [SerializeField] private TagDefinition[] tags = Array.Empty<TagDefinition>();
        [SerializeField, TextArea(1, 3)] private string validationMetadata;

        public string Id => profileId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public SpeciesDefinition SpeciesDefinition => speciesDefinition;
        public BodyFormDefinition BodyFormDefinition => bodyFormDefinition;
        public BiologicalClassificationDefinition BiologicalClassification => biologicalClassification;
        public string SpeciesDefinitionId => speciesDefinition == null ? string.Empty : speciesDefinition.Id;
        public string BodyFormDefinitionId => bodyFormDefinition == null ? string.Empty : bodyFormDefinition.Id;
        public string BiologicalClassificationId => biologicalClassification == null ? string.Empty : biologicalClassification.Id;
        public float BaseRecoveryCapacityPerHour => Mathf.Max(0f, baseRecoveryCapacityPerHour);
        public RecoveryAllocationPolicy AllocationPolicy => allocationPolicy;
        public IReadOnlyList<RecoveryProfileMethodEntry> Methods => methods ?? Array.Empty<RecoveryProfileMethodEntry>();
        public IReadOnlyList<Hazards.BiologicalHazardDefinition> BlockingHazards => blockingHazards ?? Array.Empty<Hazards.BiologicalHazardDefinition>();
        public IReadOnlyList<string> BlockingHazardIds => BlockingHazards.Select(definition => definition.Id).ToArray();
        public IReadOnlyList<TagDefinition> Tags => tags ?? Array.Empty<TagDefinition>();
        public string ValidationMetadata => validationMetadata ?? string.Empty;

        public bool IsCompatibleWith(BodySnapshot body)
        {
            if (body == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(SpeciesDefinitionId) && !string.Equals(SpeciesDefinitionId, body.SpeciesId, StringComparison.Ordinal))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(BodyFormDefinitionId) && !string.Equals(BodyFormDefinitionId, body.BodyFormId, StringComparison.Ordinal))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(BiologicalClassificationId) && !string.Equals(BiologicalClassificationId, body.BiologicalClassificationId, StringComparison.Ordinal))
            {
                return false;
            }

            return true;
        }

        public RecoveryProfileMethodEntry GetMethodEntry(string methodId)
        {
            return Methods
                .Where(entry => entry != null && entry.Enabled)
                .OrderBy(entry => entry.Priority)
                .ThenBy(entry => entry.RecoveryMethodId, StringComparer.Ordinal)
                .FirstOrDefault(entry => string.Equals(entry.RecoveryMethodId, methodId, StringComparison.Ordinal));
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"BiologicalRecoveryProfileDefinition '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("recovery-profile.", StringComparison.Ordinal))
            {
                report.AddWarning($"BiologicalRecoveryProfileDefinition '{Id}' should use the 'recovery-profile.' namespace prefix.");
            }

            if (string.IsNullOrWhiteSpace(SpeciesDefinitionId) && string.IsNullOrWhiteSpace(BodyFormDefinitionId) && string.IsNullOrWhiteSpace(BiologicalClassificationId))
            {
                report.AddError($"BiologicalRecoveryProfileDefinition '{DisplayName}' must map to a Species, body form, or biological classification.");
            }

            HashSet<string> methodIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (RecoveryProfileMethodEntry entry in Methods)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.RecoveryMethodId) || !methodIds.Add(entry.RecoveryMethodId))
                {
                    report.AddError($"BiologicalRecoveryProfileDefinition '{DisplayName}' contains a missing or duplicate Recovery Method entry.");
                    continue;
                }

                if (definitionsById == null || !definitionsById.TryGetValue(entry.RecoveryMethodId, out IGameDefinition method) || !ReferenceEquals(method, entry.RecoveryMethod))
                {
                    report.AddError($"BiologicalRecoveryProfileDefinition '{DisplayName}' references missing Recovery Method '{entry.RecoveryMethodId}'.");
                }
            }

            if (definitionsById == null)
            {
                return;
            }

            if (speciesDefinition != null && (!definitionsById.TryGetValue(SpeciesDefinitionId, out IGameDefinition species) || !ReferenceEquals(species, speciesDefinition)))
            {
                report.AddError($"BiologicalRecoveryProfileDefinition '{DisplayName}' references missing Species '{SpeciesDefinitionId}'.");
            }

            if (bodyFormDefinition != null && (!definitionsById.TryGetValue(BodyFormDefinitionId, out IGameDefinition bodyForm) || !ReferenceEquals(bodyForm, bodyFormDefinition)))
            {
                report.AddError($"BiologicalRecoveryProfileDefinition '{DisplayName}' references missing Body Form '{BodyFormDefinitionId}'.");
            }

            if (biologicalClassification != null && (!definitionsById.TryGetValue(BiologicalClassificationId, out IGameDefinition classification) || !ReferenceEquals(classification, biologicalClassification)))
            {
                report.AddError($"BiologicalRecoveryProfileDefinition '{DisplayName}' references missing Biological Classification '{BiologicalClassificationId}'.");
            }

            foreach (Hazards.BiologicalHazardDefinition hazard in BlockingHazards)
            {
                if (!definitionsById.TryGetValue(hazard.Id, out IGameDefinition registered) || !ReferenceEquals(registered, hazard))
                {
                    report.AddError($"BiologicalRecoveryProfileDefinition '{DisplayName}' references missing blocking Hazard '{hazard.Id}'.");
                }
            }
        }
    }
}
