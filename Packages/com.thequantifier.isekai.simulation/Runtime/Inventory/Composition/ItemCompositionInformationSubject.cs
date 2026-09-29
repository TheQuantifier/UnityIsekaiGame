using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Inventory.Composition
{
    public static class ItemCompositionInformationSubject
    {
        public const string ItemCompositionSubjectTag = "subject-type:item-composition";
        public const string ItemMaterialSubjectTag = "subject-type:item-material";

        public static readonly string[] ProtectedFields =
        {
            "material-purity",
            "material-source",
            "hidden-component",
            "recipe",
            "provenance",
            "access-policy"
        };

        public static InformationSubjectReferenceData Create(string itemInstanceId, string compositionId, string itemDefinitionId, IEnumerable<string> tags = null)
        {
            string subjectId = string.IsNullOrWhiteSpace(compositionId) ? $"item-composition.{itemInstanceId}" : compositionId;
            return new InformationSubjectReferenceData
            {
                subjectType = InformationSubjectType.Custom,
                subjectId = subjectId,
                parentSubjectId = itemDefinitionId ?? string.Empty,
                tags = Normalize(tags, "domain.item", "item.composition", ItemCompositionSubjectTag)
            };
        }

        public static InformationSubjectReferenceData CreateMaterial(string itemInstanceId, string compositionId, string materialEntryId, string materialDefinitionId, IEnumerable<string> tags = null)
        {
            string parent = string.IsNullOrWhiteSpace(compositionId) ? $"item-composition.{itemInstanceId}" : compositionId;
            return new InformationSubjectReferenceData
            {
                subjectType = InformationSubjectType.Custom,
                subjectId = string.IsNullOrWhiteSpace(materialEntryId) ? $"{parent}.material" : materialEntryId,
                parentSubjectId = parent,
                tags = Normalize(tags, "domain.item", "category.item.material", ItemMaterialSubjectTag, materialDefinitionId)
            };
        }

        private static string[] Normalize(IEnumerable<string> tags, params string[] required)
        {
            return (tags ?? Array.Empty<string>())
                .Concat(required ?? Array.Empty<string>())
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => tag.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(tag => tag, StringComparer.Ordinal)
                .ToArray();
        }
    }
}
