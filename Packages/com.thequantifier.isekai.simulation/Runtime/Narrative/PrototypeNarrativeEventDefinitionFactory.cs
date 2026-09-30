using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Dialogue;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Quests;

namespace UnityIsekaiGame.Narrative
{
    public static class PrototypeNarrativeEventDefinitionFactory
    {
        public const string DungeonEntryQuestDefinitionId = "narrative-event-definition.prototype.dungeon-entry-quest";
        public const string FollowUpQuestDefinitionId = "narrative-event-definition.prototype.quest-completion-follow-up";
        public const string DialogueChoiceWorldEventDefinitionId = "narrative-event-definition.prototype.dialogue-choice-world-event";
        public const string KnowledgeUnlockConversationDefinitionId = "narrative-event-definition.prototype.knowledge-unlocks-conversation";
        public const string HiddenFactionOfferDefinitionId = "narrative-event-definition.prototype.hidden-faction-offer";
        public const string DelayedPublicationDefinitionId = "narrative-event-definition.prototype.delayed-publication";
        public const string CascadeSignalDefinitionId = "narrative-event-definition.prototype.cascade-signal";

        public const string DungeonEntrySignalId = "narrative-signal-definition.prototype.location.dungeon-entry";
        public const string QuestCompletedSignalId = "narrative-signal-definition.prototype.quest.guild-completed";
        public const string DialogueChoiceSignalId = "narrative-signal-definition.prototype.dialogue.guild-choice";
        public const string KnowledgeLearnedSignalId = "narrative-signal-definition.prototype.knowledge.hidden-dungeon";
        public const string HiddenFactionSignalId = "narrative-signal-definition.prototype.hidden-faction";
        public const string DelayedSignalId = "narrative-signal-definition.prototype.delayed-follow-up";
        public const string CascadeStartSignalId = "narrative-signal-definition.prototype.cascade.start";
        public const string CascadeFollowUpSignalId = "narrative-signal-definition.prototype.cascade.follow-up";

        public static readonly string[] PrototypeDefinitionIds =
        {
            DungeonEntryQuestDefinitionId,
            FollowUpQuestDefinitionId,
            DialogueChoiceWorldEventDefinitionId,
            KnowledgeUnlockConversationDefinitionId,
            HiddenFactionOfferDefinitionId,
            DelayedPublicationDefinitionId,
            CascadeSignalDefinitionId
        };

        public static DefinitionRegistry AddMissingPrototypeNarrativeEventDefinitions(DefinitionRegistry baseRegistry)
        {
            HashSet<string> ids = new HashSet<string>(baseRegistry?.DefinitionsById.Keys ?? Array.Empty<string>(), StringComparer.Ordinal);
            List<IGameDefinition> definitions = new List<IGameDefinition>();
            if (baseRegistry != null) definitions.AddRange(baseRegistry.DefinitionsById.Values.Where(definition => definition != null));
            foreach (NarrativeEventDefinition definition in CreateMissingNarrativeEventDefinitions(ids)) definitions.Add(definition);
            return new DefinitionRegistry(definitions);
        }

        public static IReadOnlyList<NarrativeEventDefinition> CreateMissingNarrativeEventDefinitions(IEnumerable<string> existingDefinitionIds)
        {
            HashSet<string> ids = existingDefinitionIds == null ? new HashSet<string>(StringComparer.Ordinal) : new HashSet<string>(existingDefinitionIds, StringComparer.Ordinal);
            List<NarrativeEventDefinition> definitions = new List<NarrativeEventDefinition>();

            Add(definitions, ids, new NarrativeEventDefinitionData
            {
                eventDefinitionId = DungeonEntryQuestDefinitionId,
                displayName = "Dungeon Entry Quest Unlock",
                category = NarrativeEventCategory.Location,
                scope = NarrativeEventScope.OncePerPerson,
                scopeSelectorId = "actorPersonId",
                repeatPolicy = NarrativeRepeatPolicy.OncePerScope,
                armingPolicy = NarrativeArmingPolicy.OnWorldInitialization,
                priority = 10,
                visibility = NarrativeEventVisibility.ParticipantKnown,
                triggers = new[] { Trigger("trigger.dungeon-entry", NarrativeTriggerCategory.LocationEntered, DungeonEntrySignalId, "location.prototype.dungeon-entry") },
                conditions = new[] { Condition("condition.guild-member", NarrativeConditionCategory.OrganizationState, "organization.prototype.adventurers-guild") },
                actions = new[]
                {
                    Action("action.create-guild-quest", NarrativeActionCategory.InstantiateQuest, PrototypeQuestDefinitionFactory.GuildPostingDefinitionId, outputSlot: "createdQuest", secondaryTargetId: "organization.prototype.adventurers-guild", order: 10),
                    Action("action.publish-guild-quest", NarrativeActionCategory.PublishQuestListing, "quest-source.prototype.guild-board", inputSlot: "createdQuest", secondaryTargetId: "authority.prototype.guild.board-post", requirement: NarrativeActionRequirement.OptionalBestEffort, order: 20)
                },
                tagIds = new[] { "prototype", "location", "quest" }
            });

            Add(definitions, ids, new NarrativeEventDefinitionData
            {
                eventDefinitionId = FollowUpQuestDefinitionId,
                displayName = "Quest Completion Follow Up",
                category = NarrativeEventCategory.Quest,
                scope = NarrativeEventScope.OncePerQuest,
                scopeSelectorId = "questId",
                repeatPolicy = NarrativeRepeatPolicy.OncePerScope,
                armingPolicy = NarrativeArmingPolicy.OnWorldInitialization,
                priority = 20,
                triggers = new[] { Trigger("trigger.guild-complete", NarrativeTriggerCategory.QuestOutcome, QuestCompletedSignalId, PrototypeQuestDefinitionFactory.GuildPostingDefinitionId) },
                conditions = new[] { Condition("condition.quest-complete", NarrativeConditionCategory.QuestState, "quest-state.completed") },
                actions = new[]
                {
                    Action("action.create-hidden-rumor", NarrativeActionCategory.InstantiateQuest, PrototypeQuestDefinitionFactory.HiddenDungeonRumorDefinitionId, outputSlot: "rumorQuest", order: 10),
                    Action("action.publish-hidden-rumor", NarrativeActionCategory.PublishQuestListing, "quest-source.prototype.guild-board", inputSlot: "rumorQuest", secondaryTargetId: "authority.prototype.guild.board-post", requirement: NarrativeActionRequirement.OptionalBestEffort, order: 20)
                },
                tagIds = new[] { "prototype", "quest", "follow-up" }
            });

            Add(definitions, ids, new NarrativeEventDefinitionData
            {
                eventDefinitionId = DialogueChoiceWorldEventDefinitionId,
                displayName = "Dialogue Choice World Event",
                category = NarrativeEventCategory.Dialogue,
                scope = NarrativeEventScope.OncePerConversation,
                scopeSelectorId = "conversationId",
                armingPolicy = NarrativeArmingPolicy.OnWorldInitialization,
                visibility = NarrativeEventVisibility.Restricted,
                triggers = new[] { Trigger("trigger.guild-choice", NarrativeTriggerCategory.DialogueChoice, DialogueChoiceSignalId, "guild.choice.ask-work") },
                conditions = new[] { Condition("condition.dialogue-selected", NarrativeConditionCategory.DialogueState, "dialogue-choice.guild.choice.ask-work") },
                actions = new[] { Action("action.emit-follow-up", NarrativeActionCategory.EmitNarrativeSignal, CascadeStartSignalId, outputSlot: "cascadeSignal", order: 10) },
                tagIds = new[] { "prototype", "dialogue", "signal" }
            });

            Add(definitions, ids, new NarrativeEventDefinitionData
            {
                eventDefinitionId = KnowledgeUnlockConversationDefinitionId,
                displayName = "Knowledge Unlocks Conversation",
                category = NarrativeEventCategory.Discovery,
                scope = NarrativeEventScope.OncePerPerson,
                scopeSelectorId = "actorPersonId",
                armingPolicy = NarrativeArmingPolicy.OnWorldInitialization,
                triggers = new[] { Trigger("trigger.knowledge", NarrativeTriggerCategory.ExplicitSignal, KnowledgeLearnedSignalId, "subject.prototype.hidden-dungeon") },
                conditions = new[] { Condition("condition.knows-hidden", NarrativeConditionCategory.ActorKnowledge, "subject.prototype.hidden-dungeon") },
                actions = new[] { Action("action.start-prisoner-conversation", NarrativeActionCategory.StartConversation, PrototypeConversationDefinitionFactory.PrisonerInterviewDefinitionId, requirement: NarrativeActionRequirement.OptionalBestEffort, order: 10) },
                tagIds = new[] { "prototype", "knowledge", "conversation" }
            });

            Add(definitions, ids, new NarrativeEventDefinitionData
            {
                eventDefinitionId = HiddenFactionOfferDefinitionId,
                displayName = "Hidden Faction Offer",
                category = NarrativeEventCategory.Social,
                scope = NarrativeEventScope.OncePerPerson,
                scopeSelectorId = "actorPersonId",
                visibility = NarrativeEventVisibility.Hidden,
                armingPolicy = NarrativeArmingPolicy.OnWorldInitialization,
                triggers = new[] { Trigger("trigger.hidden-faction", NarrativeTriggerCategory.SocialState, HiddenFactionSignalId, "faction.prototype.hidden") },
                conditions = new[] { Condition("condition.hidden-faction", NarrativeConditionCategory.SocialState, "faction.prototype.hidden", hidden: true) },
                actions = new[] { Action("action.hidden-offer", NarrativeActionCategory.InstantiateQuest, PrototypeQuestDefinitionFactory.HiddenDungeonRumorDefinitionId, order: 10) },
                tagIds = new[] { "prototype", "hidden", "faction" }
            });

            Add(definitions, ids, new NarrativeEventDefinitionData
            {
                eventDefinitionId = DelayedPublicationDefinitionId,
                displayName = "Delayed Quest Publication",
                category = NarrativeEventCategory.Quest,
                scope = NarrativeEventScope.OncePerWorld,
                triggerMode = NarrativeTriggerMode.TriggerAfterDelay,
                delayDuration = 12d,
                delayedRevalidationPolicy = NarrativeDelayedRevalidationPolicy.Revalidate,
                armingPolicy = NarrativeArmingPolicy.OnWorldInitialization,
                triggers = new[] { Trigger("trigger.delayed", NarrativeTriggerCategory.AuthoritativeTime, DelayedSignalId) },
                conditions = new[] { Condition("condition.time", NarrativeConditionCategory.TimeState, string.Empty, min: 10) },
                actions = new[] { Action("action.delayed-signal", NarrativeActionCategory.EmitNarrativeSignal, CascadeStartSignalId, order: 10) },
                tagIds = new[] { "prototype", "time", "delay" }
            });

            Add(definitions, ids, new NarrativeEventDefinitionData
            {
                eventDefinitionId = CascadeSignalDefinitionId,
                displayName = "Cascade Signal Follow Up",
                category = NarrativeEventCategory.Scripted,
                scope = NarrativeEventScope.OncePerWorld,
                armingPolicy = NarrativeArmingPolicy.OnWorldInitialization,
                triggers = new[] { Trigger("trigger.cascade", NarrativeTriggerCategory.ExplicitSignal, CascadeStartSignalId) },
                actions = new[] { Action("action.emit-cascade-follow-up", NarrativeActionCategory.EmitNarrativeSignal, CascadeFollowUpSignalId, requirement: NarrativeActionRequirement.OptionalBestEffort, order: 10) },
                tagIds = new[] { "prototype", "cascade" }
            });

            return definitions;
        }

        private static void Add(ICollection<NarrativeEventDefinition> definitions, ISet<string> existingIds, NarrativeEventDefinitionData data)
        {
            if (data == null || existingIds.Contains(data.eventDefinitionId)) return;
            NarrativeEventDefinition definition = ScriptableObject.CreateInstance<NarrativeEventDefinition>();
            definition.DevelopmentConfigure(data);
            definitions.Add(definition);
            existingIds.Add(data.eventDefinitionId);
        }

        private static NarrativeTriggerDefinitionData Trigger(string id, NarrativeTriggerCategory category, string sourceId, string subjectId = "")
        {
            return new NarrativeTriggerDefinitionData
            {
                triggerDefinitionId = id,
                category = category,
                requiredSourceId = sourceId,
                requiredSubjectId = subjectId,
                committedOnly = true,
                ignoreRestoreReplay = true
            };
        }

        private static NarrativeConditionDefinitionData Condition(string id, NarrativeConditionCategory category, string requiredId, bool hidden = false, int min = 1)
        {
            return new NarrativeConditionDefinitionData
            {
                conditionDefinitionId = id,
                category = category,
                requiredId = requiredId,
                hidden = hidden,
                revealFailure = !hidden,
                minimumValue = min
            };
        }

        private static NarrativeActionDefinitionData Action(
            string id,
            NarrativeActionCategory category,
            string targetId,
            string inputSlot = "",
            string outputSlot = "",
            string secondaryTargetId = "",
            NarrativeActionRequirement requirement = NarrativeActionRequirement.Required,
            int order = 0)
        {
            return new NarrativeActionDefinitionData
            {
                actionDefinitionId = id,
                category = category,
                targetId = targetId,
                secondaryTargetId = secondaryTargetId,
                inputSlotId = inputSlot,
                outputSlotId = outputSlot,
                requirement = requirement,
                order = order
            };
        }
    }
}
