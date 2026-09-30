using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Beings.Biology
{
    [CreateAssetMenu(fileName = "BodyForm", menuName = "Unity Isekai Game/Beings/Biology/Body Form")]
    public sealed class BodyFormDefinition : ScriptableObject, IGameDefinition, ICategorizableDefinition, ITaggedDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string bodyFormId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 5)] private string description;
        [SerializeField] private CategoryDefinition primaryCategory;
        [SerializeField] private TagDefinition[] tags;
        [SerializeField] private BodyLocomotionCategory locomotionCategory = BodyLocomotionCategory.Unknown;
        [SerializeField] private BodyManipulationCategory manipulationCategory = BodyManipulationCategory.Unknown;
        [SerializeField] private TagDefinition[] generalEquipmentCompatibilityTags;
        [SerializeField] private bool conventionalHeadTorsoLimbAnatomyExpected;
        [SerializeField] private bool physicalEquipmentCompatible = true;
        [SerializeField] private bool physicalBody = true;
        [SerializeField] private bool corporeal = true;

        public string Id => bodyFormId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public CategoryDefinition PrimaryCategory => primaryCategory;
        public CategoryDomain ClassificationDomain => CategoryDomain.Being;
        public IReadOnlyList<TagDefinition> Tags => tags ?? Array.Empty<TagDefinition>();
        public BodyLocomotionCategory LocomotionCategory => locomotionCategory;
        public BodyManipulationCategory ManipulationCategory => manipulationCategory;
        public IReadOnlyList<TagDefinition> GeneralEquipmentCompatibilityTags => generalEquipmentCompatibilityTags ?? Array.Empty<TagDefinition>();
        public bool ConventionalHeadTorsoLimbAnatomyExpected => conventionalHeadTorsoLimbAnatomyExpected;
        public bool PhysicalEquipmentCompatible => physicalEquipmentCompatible;
        public bool PhysicalBody => physicalBody;
        public bool Corporeal => corporeal;

        private void OnValidate()
        {
            bodyFormId = bodyFormId?.Trim();
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"Body form '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("body-form.", StringComparison.Ordinal))
            {
                report.AddWarning($"Body form '{Id}' should use the 'body-form.' namespace prefix.");
            }

            if (string.IsNullOrWhiteSpace(DisplayName))
            {
                report.AddError($"Body form '{Id}' is missing a display name.");
            }

            if (!physicalBody && physicalEquipmentCompatible)
            {
                report.AddError($"Body form '{DisplayName}' cannot be physical-equipment compatible without a physical body.");
            }

            if (!corporeal && physicalBody)
            {
                report.AddError($"Body form '{DisplayName}' cannot be physical while marked incorporeal.");
            }

            if (!Enum.IsDefined(typeof(BodyLocomotionCategory), locomotionCategory))
            {
                report.AddError($"Body form '{DisplayName}' has an invalid locomotion category.");
            }

            if (!Enum.IsDefined(typeof(BodyManipulationCategory), manipulationCategory))
            {
                report.AddError($"Body form '{DisplayName}' has an invalid manipulation category.");
            }
        }
    }
}
