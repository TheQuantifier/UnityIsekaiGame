using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Beings.Biology.VitalProcesses
{
    [CreateAssetMenu(fileName = "BiologicalResource", menuName = "Unity Isekai Game/Beings/Biology/Biological Resource")]
    public sealed class BiologicalResourceDefinition : ScriptableObject, IGameDefinition, ITaggedDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string resourceId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 5)] private string description;
        [SerializeField] private BiologicalResourceModelType modelType;
        [SerializeField] private string unit;
        [SerializeField] private bool alphaEnabled = true;
        [SerializeField] private TagDefinition[] tags;

        public string Id => resourceId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public BiologicalResourceModelType ModelType => modelType;
        public string Unit => unit ?? string.Empty;
        public bool AlphaEnabled => alphaEnabled;
        public IReadOnlyList<TagDefinition> Tags => tags ?? Array.Empty<TagDefinition>();

        private void OnValidate()
        {
            resourceId = resourceId?.Trim();
            displayName = displayName?.Trim();
            unit = unit?.Trim();
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"BiologicalResourceDefinition '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("resource.biology.", StringComparison.Ordinal))
            {
                report.AddWarning($"BiologicalResourceDefinition '{Id}' should use the 'resource.biology.' namespace prefix.");
            }

            if (!Enum.IsDefined(typeof(BiologicalResourceModelType), modelType))
            {
                report.AddError($"BiologicalResourceDefinition '{DisplayName}' has an invalid model type.");
            }

            foreach (TagDefinition tag in Tags)
            {
                if (tag == null)
                {
                    report.AddError($"BiologicalResourceDefinition '{DisplayName}' has a missing tag reference.");
                }
                else if (definitionsById != null && (!definitionsById.TryGetValue(tag.Id, out IGameDefinition found) || found is not TagDefinition))
                {
                    report.AddError($"BiologicalResourceDefinition '{DisplayName}' references tag '{tag.Id}', which is not in the configured catalog.");
                }
            }

            ValidateCanonicalAlphaSet(definitionsById, report);
        }

        private void ValidateCanonicalAlphaSet(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (definitionsById == null || !definitionsById.ContainsKey("species.human"))
            {
                return;
            }

            string[] required =
            {
                BiologicalResourceIds.Blood,
                BiologicalResourceIds.Breath,
                BiologicalResourceIds.Temperature,
                BiologicalResourceIds.Nutrition,
                BiologicalResourceIds.Hydration,
                BiologicalResourceIds.SleepNeed,
                BiologicalResourceIds.Fatigue
            };

            foreach (string id in required)
            {
                if (!definitionsById.TryGetValue(id, out IGameDefinition definition) || definition is not BiologicalResourceDefinition)
                {
                    report.AddError($"Canonical BiologicalResourceDefinition '{id}' must be registered in the alpha definition catalog.");
                }
            }
        }
    }
}
