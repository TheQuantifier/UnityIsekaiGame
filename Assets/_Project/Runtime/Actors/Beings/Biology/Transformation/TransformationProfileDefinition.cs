using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Beings.Biology.Transformation
{
    [CreateAssetMenu(fileName = "TransformationProfile", menuName = "Unity Isekai Game/Beings/Biology/Transformation Profile")]
    public sealed class TransformationProfileDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string profileId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 5)] private string description;
        [SerializeField] private SpeciesDefinition[] speciesDefinitions = Array.Empty<SpeciesDefinition>();
        [SerializeField] private BodyFormDefinition[] bodyFormDefinitions = Array.Empty<BodyFormDefinition>();
        [SerializeField] private TransformationMethodDefinition[] enabledMethods = Array.Empty<TransformationMethodDefinition>();
        [SerializeField] private TransformationTransferPolicy defaultTransferPolicy = TransformationTransferPolicy.TransferPersonOwnedOnly;
        [SerializeField] private TransformationReconciliationPolicy defaultConditionPolicy = TransformationReconciliationPolicy.Clear;
        [SerializeField] private TransformationReconciliationPolicy defaultVitalPolicy = TransformationReconciliationPolicy.InitializeClean;
        [SerializeField] private TransformationReconciliationPolicy defaultHazardPolicy = TransformationReconciliationPolicy.Clear;
        [SerializeField] private TransformationReconciliationPolicy defaultRecoveryPolicy = TransformationReconciliationPolicy.Cancel;
        [SerializeField, TextArea(1, 3)] private string validationMetadata;

        public string Id => profileId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public IReadOnlyList<SpeciesDefinition> SpeciesDefinitions => speciesDefinitions ?? Array.Empty<SpeciesDefinition>();
        public IReadOnlyList<BodyFormDefinition> BodyFormDefinitions => bodyFormDefinitions ?? Array.Empty<BodyFormDefinition>();
        public IReadOnlyList<TransformationMethodDefinition> EnabledMethods => enabledMethods ?? Array.Empty<TransformationMethodDefinition>();
        public IReadOnlyList<string> SpeciesDefinitionIds => SpeciesDefinitions.Select(definition => definition.Id).ToArray();
        public IReadOnlyList<string> BodyFormDefinitionIds => BodyFormDefinitions.Select(definition => definition.Id).ToArray();
        public IReadOnlyList<string> EnabledMethodIds => EnabledMethods.Select(definition => definition.Id).ToArray();
        public TransformationTransferPolicy DefaultTransferPolicy => defaultTransferPolicy;
        public TransformationReconciliationPolicy DefaultConditionPolicy => defaultConditionPolicy;
        public TransformationReconciliationPolicy DefaultVitalPolicy => defaultVitalPolicy;
        public TransformationReconciliationPolicy DefaultHazardPolicy => defaultHazardPolicy;
        public TransformationReconciliationPolicy DefaultRecoveryPolicy => defaultRecoveryPolicy;
        public string ValidationMetadata => validationMetadata ?? string.Empty;

        private void OnValidate()
        {
            profileId = profileId?.Trim();
            speciesDefinitions = Normalize(speciesDefinitions);
            bodyFormDefinitions = Normalize(bodyFormDefinitions);
            enabledMethods = Normalize(enabledMethods);
        }

        public bool AppliesTo(BodySnapshot body)
        {
            if (body == null)
            {
                return false;
            }

            bool speciesMatches = SpeciesDefinitionIds.Count == 0 || SpeciesDefinitionIds.Contains(body.SpeciesId ?? string.Empty, StringComparer.Ordinal);
            bool formMatches = BodyFormDefinitionIds.Count == 0 || BodyFormDefinitionIds.Contains(body.BodyFormId ?? string.Empty, StringComparer.Ordinal);
            return speciesMatches && formMatches;
        }

        public bool EnablesMethod(string methodId)
        {
            return EnabledMethodIds.Count == 0 || EnabledMethodIds.Contains(methodId ?? string.Empty, StringComparer.Ordinal);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"TransformationProfileDefinition '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("transformation-profile.", StringComparison.Ordinal))
            {
                report.AddWarning($"TransformationProfileDefinition '{Id}' should use the 'transformation-profile.' namespace prefix.");
            }

            foreach (string speciesId in SpeciesDefinitionIds)
            {
                if (definitionsById == null || !definitionsById.TryGetValue(speciesId, out IGameDefinition species) || species is not SpeciesDefinition)
                {
                    report.AddError($"TransformationProfileDefinition '{DisplayName}' references missing Species '{speciesId}'.");
                }
            }

            foreach (string bodyFormId in BodyFormDefinitionIds)
            {
                if (definitionsById == null || !definitionsById.TryGetValue(bodyFormId, out IGameDefinition bodyForm) || bodyForm is not BodyFormDefinition)
                {
                    report.AddError($"TransformationProfileDefinition '{DisplayName}' references missing body form '{bodyFormId}'.");
                }
            }

            foreach (string methodId in EnabledMethodIds)
            {
                if (definitionsById == null || !definitionsById.TryGetValue(methodId, out IGameDefinition method) || method is not TransformationMethodDefinition)
                {
                    report.AddError($"TransformationProfileDefinition '{DisplayName}' references missing Transformation Method '{methodId}'.");
                }
            }

            ValidateCanonicalAlphaSet(definitionsById, report);
        }

        private static void ValidateCanonicalAlphaSet(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (definitionsById == null || !definitionsById.ContainsKey("species.human"))
            {
                return;
            }

            TransformationProfileDefinition first = definitionsById.Values.OfType<TransformationProfileDefinition>().OrderBy(definition => definition.Id, StringComparer.Ordinal).FirstOrDefault();
            if (first == null || !ReferenceEquals(first, definitionsById.Values.OfType<TransformationProfileDefinition>().FirstOrDefault(definition => definition.Id == first.Id)))
            {
                return;
            }

            foreach (string id in new[] { "transformation-profile.alpha.default", "transformation-profile.species.human", "transformation-profile.species.basic-construct", "transformation-profile.species.basic-spirit" })
            {
                if (!definitionsById.TryGetValue(id, out IGameDefinition definition) || definition is not TransformationProfileDefinition)
                {
                    report.AddError($"Canonical TransformationProfileDefinition '{id}' must be registered in the alpha definition catalog.");
                }
            }
        }

        private static T[] Normalize<T>(T[] values) where T : UnityEngine.Object, IGameDefinition
        {
            return values == null ? Array.Empty<T>() : values.Where(value => value != null).Distinct().OrderBy(value => value.Id, StringComparer.Ordinal).ToArray();
        }
    }
}
