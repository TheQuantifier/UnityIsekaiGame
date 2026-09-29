using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Inventory.Durability
{
    public static class ItemDurabilityInformationSubject
    {
        public const string DurabilitySubjectTag = "subject-type:item-durability";

        public static readonly string[] ProtectedFields =
        {
            "hidden-damage",
            "structural-weakness",
            "break-check-history",
            "repair-history",
            "item-recovery-yield",
            "maintenance-source",
            "access-policy"
        };

        public static InformationSubjectReferenceData Create(string itemInstanceId, string durabilityRecordId, string itemDefinitionId, IEnumerable<string> tags = null)
        {
            string[] subjectTags = (tags ?? Array.Empty<string>())
                .Concat(new[] { "domain.item", "item.durability", DurabilitySubjectTag })
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(tag => tag, StringComparer.Ordinal)
                .ToArray();

            return new InformationSubjectReferenceData
            {
                subjectType = InformationSubjectType.Custom,
                subjectId = string.IsNullOrWhiteSpace(durabilityRecordId) ? $"item-durability.{itemInstanceId}" : durabilityRecordId,
                parentSubjectId = itemDefinitionId ?? string.Empty,
                tags = subjectTags
            };
        }
    }
}
