using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Skills
{
    public sealed class SkillActionExecutionEvent
    {
        public string EventId { get; set; }
        public string ActorId { get; set; }
        public string ActionDefinitionId { get; set; }
        public SkillActionEventCategory ActionCategory { get; set; }
        public string EquipmentItemInstanceId { get; set; }
        public string ItemDefinitionId { get; set; }
        public CategoryDefinition ItemCategory { get; set; }
        public IReadOnlyList<TagDefinition> ItemTags { get; set; } = Array.Empty<TagDefinition>();
        public IReadOnlyList<TagDefinition> ActionTags { get; set; } = Array.Empty<TagDefinition>();
        public IReadOnlyList<TagDefinition> MagicTags { get; set; } = Array.Empty<TagDefinition>();
        public bool Executed { get; set; }
        public bool IntendedResultSucceeded { get; set; }
        public double PlaytimeSeconds { get; set; }
        public string SourceSystem { get; set; }
        public bool Restoring { get; set; }
        public bool ServerAuthoritative { get; set; }

        public static SkillActionExecutionEvent Development(
            string eventId,
            SkillActionEventCategory category,
            string qualifyingActionId,
            bool executed = true,
            bool succeeded = true)
        {
            return new SkillActionExecutionEvent
            {
                EventId = eventId,
                ActionCategory = category,
                ActionDefinitionId = qualifyingActionId,
                Executed = executed,
                IntendedResultSucceeded = succeeded,
                SourceSystem = "development"
            };
        }
    }
}
