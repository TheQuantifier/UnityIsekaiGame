using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Inventory.Quality
{
    public static class ItemQualityAffixInformationSubject
    {
        public const string QualitySubjectTag = "subject-type:item-quality";
        public const string AffixSubjectTag = "subject-type:item-affix";

        public static readonly string[] ProtectedFields =
        {
            "quality-record-id",
            "affix-instance-id",
            "hidden-affix",
            "hidden-defect",
            "rolled-values",
            "modifier-source",
            "generation-seed",
            "generation-policy",
            "provenance",
            "access-policy",
            "revision-history"
        };

        public static InformationSubjectReferenceData Quality(string itemInstanceId, string qualityRecordId, string itemDefinitionId, IEnumerable<string> tags = null)
        {
            return Create(qualityRecordId, itemInstanceId, itemDefinitionId, QualitySubjectTag, tags);
        }

        public static InformationSubjectReferenceData Affix(string itemInstanceId, string affixInstanceId, string affixDefinitionId, IEnumerable<string> tags = null)
        {
            return Create(affixInstanceId, itemInstanceId, affixDefinitionId, AffixSubjectTag, tags);
        }

        private static InformationSubjectReferenceData Create(string subjectId, string parentSubjectId, string definitionId, string typeTag, IEnumerable<string> tags)
        {
            string[] subjectTags = (tags ?? Array.Empty<string>())
                .Concat(new[] { "domain.item", "item.instance", typeTag, definitionId })
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(tag => tag, StringComparer.Ordinal)
                .ToArray();

            return new InformationSubjectReferenceData
            {
                subjectType = InformationSubjectType.Custom,
                subjectId = subjectId ?? string.Empty,
                parentSubjectId = parentSubjectId ?? string.Empty,
                tags = subjectTags
            };
        }
    }
}
