using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Quests;

namespace UnityIsekaiGame.Dialogue
{
    public static class PrototypeDialogueGraphDefinitionFactory
    {
        public const string AdventurerGuildCounterGraphId = "dialogue-graph.prototype.adventurer-guild-counter";
        public const string MerchantGuildCounterGraphId = "dialogue-graph.prototype.merchant-guild-counter";
        public const string MayorDeskGraphId = "dialogue-graph.prototype.mayor-desk";
        public const string GuildHeadOfficeGraphId = "dialogue-graph.prototype.guild-head-office";
        public const string RecordsDeskGraphId = "dialogue-graph.prototype.records-desk";
        public const string PrisonerInterviewGraphId = "dialogue-graph.prototype.prisoner-interview";

        public static readonly string[] PrototypeDefinitionIds =
        {
            AdventurerGuildCounterGraphId,
            MerchantGuildCounterGraphId,
            MayorDeskGraphId,
            GuildHeadOfficeGraphId,
            RecordsDeskGraphId,
            PrisonerInterviewGraphId
        };

        public static DefinitionRegistry AddMissingPrototypeDialogueGraphDefinitions(DefinitionRegistry baseRegistry)
        {
            HashSet<string> ids = new HashSet<string>(baseRegistry?.DefinitionsById.Keys ?? Array.Empty<string>(), StringComparer.Ordinal);
            List<IGameDefinition> definitions = new List<IGameDefinition>();
            if (baseRegistry != null) definitions.AddRange(baseRegistry.DefinitionsById.Values.Where(definition => definition != null));
            foreach (DialogueGraphDefinition definition in CreateMissingDialogueGraphDefinitions(ids)) definitions.Add(definition);
            return new DefinitionRegistry(definitions);
        }

        public static IReadOnlyList<DialogueGraphDefinition> CreateMissingDialogueGraphDefinitions(IEnumerable<string> existingDefinitionIds)
        {
            HashSet<string> ids = existingDefinitionIds == null ? new HashSet<string>(StringComparer.Ordinal) : new HashSet<string>(existingDefinitionIds, StringComparer.Ordinal);
            List<DialogueGraphDefinition> definitions = new List<DialogueGraphDefinition>();

            Add(definitions, ids, AdventurerGuildCounterGraphId, "Adventurer Guild Counter Dialogue", PrototypeConversationDefinitionFactory.AdventurerGuildCounterDefinitionId, new[]
            {
                Node("guild.entry", "Welcome to the Adventurers Guild. Looking for work, records, or rank services?", DialogueNodeCategory.ChoicePrompt,
                    speaker: Provider(), choices: new[]
                    {
                        Choice("guild.choice.ask-work", "Ask for available work", "guild.work", DialogueChoiceCategory.Question, effects: new[] { Flag("effect.guild.asked-work", "flag.guild.asked-work") }),
                        Choice("guild.choice.accept-posting", "Accept the counter posting", "guild.accepted", DialogueChoiceCategory.QuestAccept, repeat: DialogueChoiceRepeatPolicy.OneShotPerActor, effects: new[] { OptionalOwner("effect.guild.accept-posting", DialogueEffectKind.AcceptQuestOffer, PrototypeQuestDefinitionFactory.GuildPostingDefinitionId) }),
                        Choice("guild.choice.silver-rank", "Ask about silver-rank contracts", "guild.silver", DialogueChoiceCategory.InformationRequest, visibility: ConversationVisibility.Hidden, conditions: new[] { Condition("condition.guild.silver-rank", DialogueConditionKind.OrganizationRank, "rank.prototype.adventurers.silver", hidden: true) }),
                        EndChoice("guild.choice.leave")
                    }),
                Node("guild.work", "The public board has monster work, escort work, and delivery requests. The clerk can turn a posting into an assignment when you accept it.", DialogueNodeCategory.Information, speaker: Provider(), transitions: new[] { Transition("guild.return-from-work", "guild.entry") }),
                Node("guild.accepted", "Your acceptance was recorded. The quest system owns the assignment; this dialogue only records that you selected the branch.", DialogueNodeCategory.QuestOffer, speaker: Provider(), transitions: new[] { Transition("guild.return-from-accepted", "guild.entry") }),
                Node("guild.silver", "Silver-rank work is restricted to trusted adventurers and is hidden from ordinary visitors.", DialogueNodeCategory.Information, speaker: Provider(), transitions: new[] { Transition("guild.return-from-silver", "guild.entry") }),
                EndNode("guild.end")
            }, tags: new[] { "prototype", "guild", "quest" });

            Add(definitions, ids, MerchantGuildCounterGraphId, "Merchant Guild Counter Dialogue", PrototypeConversationDefinitionFactory.MerchantGuildCounterDefinitionId, new[]
            {
                Node("merchant.entry", "The Merchant Guild handles delivery contracts and registered trade services.", DialogueNodeCategory.ChoicePrompt, speaker: Provider(), choices: new[]
                {
                    Choice("merchant.choice.delivery", "Ask for delivery work", "merchant.delivery", DialogueChoiceCategory.Question, effects: new[] { Flag("effect.merchant.asked-delivery", "flag.merchant.asked-delivery") }),
                    Choice("merchant.choice.trade", "Ask about trade services", "merchant.trade", DialogueChoiceCategory.ServiceRequest),
                    EndChoice("merchant.choice.leave")
                }),
                Node("merchant.delivery", "Delivery contracts are offered through the quest-source runtime and remain owned by that system.", DialogueNodeCategory.Information, speaker: Provider(), transitions: new[] { Transition("merchant.return-delivery", "merchant.entry") }),
                Node("merchant.trade", "Trade availability is presented here, but inventory and economy ownership remain outside dialogue.", DialogueNodeCategory.InstitutionalService, speaker: Provider(), transitions: new[] { Transition("merchant.return-trade", "merchant.entry") }),
                EndNode("merchant.end")
            }, tags: new[] { "prototype", "merchant", "trade" });

            Add(definitions, ids, MayorDeskGraphId, "Mayor Desk Dialogue", PrototypeConversationDefinitionFactory.MayorDeskDefinitionId, new[]
            {
                Node("mayor.entry", "The mayor's office can discuss civic notices, permits, and city authority.", DialogueNodeCategory.ChoicePrompt, speaker: OfficeHolder(), choices: new[]
                {
                    Choice("mayor.choice.notice", "Ask about civic notices", "mayor.notice", DialogueChoiceCategory.InformationRequest),
                    Choice("mayor.choice.permit", "Request permit review", "mayor.permit", DialogueChoiceCategory.ServiceRequest, conditions: new[] { Condition("condition.mayor.authority", DialogueConditionKind.Authority, "authority.prototype.city.quest-assign") }),
                    EndChoice("mayor.choice.leave")
                }),
                Node("mayor.notice", "Public notices are safe to disclose. Restricted government details still require Step 8 access.", DialogueNodeCategory.Information, speaker: OfficeHolder(), transitions: new[] { Transition("mayor.return-notice", "mayor.entry") }),
                Node("mayor.permit", "Permit review is delegated to the legal and government runtimes when those services execute the effect.", DialogueNodeCategory.InstitutionalService, speaker: OfficeHolder(), transitions: new[] { Transition("mayor.return-permit", "mayor.entry") }),
                EndNode("mayor.end")
            }, tags: new[] { "prototype", "mayor", "government" });

            Add(definitions, ids, GuildHeadOfficeGraphId, "Guild Head Office Dialogue", PrototypeConversationDefinitionFactory.GuildHeadOfficeDefinitionId, new[]
            {
                Node("head.entry", "The guild head only discusses private strategy with authorized members.", DialogueNodeCategory.ChoicePrompt, speaker: OfficeHolder(), choices: new[]
                {
                    Choice("head.choice.private", "Request private briefing", "head.private", DialogueChoiceCategory.InformationRequest, visibility: ConversationVisibility.Hidden, conditions: new[] { Condition("condition.head.member", DialogueConditionKind.OrganizationMembership, "organization.prototype.adventurers-guild", hidden: true) }),
                    Choice("head.choice.public", "Ask for public guidance", "head.public", DialogueChoiceCategory.Question),
                    EndChoice("head.choice.leave")
                }),
                Node("head.private", "Private guild strategy stays hidden unless membership and access policy both allow it.", DialogueNodeCategory.Information, speaker: OfficeHolder(), transitions: new[] { Transition("head.return-private", "head.entry") }),
                Node("head.public", "Train, complete public contracts, and build reliable history before requesting restricted work.", DialogueNodeCategory.Information, speaker: OfficeHolder(), transitions: new[] { Transition("head.return-public", "head.entry") }),
                EndNode("head.end")
            }, tags: new[] { "prototype", "guild", "private" });

            Add(definitions, ids, RecordsDeskGraphId, "Records Desk Dialogue", PrototypeConversationDefinitionFactory.RecordsDeskDefinitionId, new[]
            {
                Node("records.entry", "Records access depends on office authority and information-access policy.", DialogueNodeCategory.ChoicePrompt, speaker: Provider(), choices: new[]
                {
                    Choice("records.choice.public", "Request public record index", "records.public", DialogueChoiceCategory.InformationRequest),
                    Choice("records.choice.restricted", "Request restricted records", "records.restricted", DialogueChoiceCategory.InformationRequest, visibility: ConversationVisibility.Hidden, conditions: new[] { Condition("condition.records.authority", DialogueConditionKind.Authority, "authority.prototype.records.read", hidden: true) }),
                    EndChoice("records.choice.leave")
                }),
                Node("records.public", "The public index can be previewed without mutating knowledge or history.", DialogueNodeCategory.Information, speaker: Provider(), transitions: new[] { Transition("records.return-public", "records.entry") }),
                Node("records.restricted", "Restricted records require an authorized read projection; dialogue does not bypass privacy.", DialogueNodeCategory.Information, speaker: Provider(), transitions: new[] { Transition("records.return-restricted", "records.entry") }),
                EndNode("records.end")
            }, tags: new[] { "prototype", "records", "access" });

            Add(definitions, ids, PrisonerInterviewGraphId, "Prisoner Interview Dialogue", PrototypeConversationDefinitionFactory.PrisonerInterviewDefinitionId, new[]
            {
                Node("prisoner.entry", "The prisoner watches the guard before answering.", DialogueNodeCategory.ChoicePrompt, speaker: new DialogueSpeakerSelectorData { kind = DialogueSpeakerSelectorKind.ParticipantRole, role = ConversationParticipantRole.Prisoner }, choices: new[]
                {
                    Choice("prisoner.choice.ask-rumor", "Ask about the hidden route", "prisoner.rumor", DialogueChoiceCategory.Question, conditions: new[] { Condition("condition.prisoner.knows-rumor", DialogueConditionKind.Knowledge, "subject.prototype.hidden-dungeon") }),
                    Choice("prisoner.choice.press", "Press without enough knowledge", "prisoner.refusal", DialogueChoiceCategory.Question),
                    EndChoice("prisoner.choice.leave")
                }),
                Node("prisoner.rumor", "The prisoner confirms a rumor but the belief remains distinct from authoritative truth.", DialogueNodeCategory.Information, speaker: new DialogueSpeakerSelectorData { kind = DialogueSpeakerSelectorKind.ParticipantRole, role = ConversationParticipantRole.Prisoner }, transitions: new[] { Transition("prisoner.return-rumor", "prisoner.entry") }),
                Node("prisoner.refusal", "The prisoner refuses to volunteer useful details.", DialogueNodeCategory.Information, speaker: new DialogueSpeakerSelectorData { kind = DialogueSpeakerSelectorKind.ParticipantRole, role = ConversationParticipantRole.Prisoner }, transitions: new[] { Transition("prisoner.return-refusal", "prisoner.entry") }),
                EndNode("prisoner.end")
            }, tags: new[] { "prototype", "prison", "interview" });

            return definitions;
        }

        private static void Add(ICollection<DialogueGraphDefinition> definitions, ISet<string> existingIds, string id, string displayName, string conversationDefinitionId, IEnumerable<DialogueNodeDefinitionData> nodes, IEnumerable<string> tags)
        {
            if (existingIds.Contains(id)) return;
            DialogueNodeDefinitionData[] nodeArray = (nodes ?? Array.Empty<DialogueNodeDefinitionData>()).ToArray();
            DialogueGraphDefinition definition = ScriptableObject.CreateInstance<DialogueGraphDefinition>();
            definition.name = displayName;
            definition.DevelopmentConfigure(id, displayName, conversationDefinitionId, nodeArray.FirstOrDefault()?.nodeId ?? string.Empty, nodeArray.LastOrDefault(value => value.category == DialogueNodeCategory.End)?.nodeId ?? string.Empty, nodeArray, tags: tags);
            definitions.Add(definition);
            existingIds.Add(id);
        }

        private static DialogueNodeDefinitionData Node(string id, string text, DialogueNodeCategory category, DialogueSpeakerSelectorData speaker = null, IEnumerable<DialogueChoiceDefinitionData> choices = null, IEnumerable<DialogueTransitionDefinitionData> transitions = null)
        {
            return new DialogueNodeDefinitionData
            {
                nodeId = id,
                authoredText = text,
                category = category,
                speaker = speaker ?? new DialogueSpeakerSelectorData { kind = DialogueSpeakerSelectorKind.ConversationInitiator },
                listener = new DialogueListenerSelectorData { kind = DialogueListenerSelectorKind.AllParticipants },
                choices = (choices ?? Array.Empty<DialogueChoiceDefinitionData>()).ToArray(),
                transitions = (transitions ?? Array.Empty<DialogueTransitionDefinitionData>()).ToArray()
            };
        }

        private static DialogueNodeDefinitionData EndNode(string id)
        {
            return Node(id, "Conversation ended.", DialogueNodeCategory.End, new DialogueSpeakerSelectorData { kind = DialogueSpeakerSelectorKind.None });
        }

        private static DialogueChoiceDefinitionData Choice(string id, string text, string target, DialogueChoiceCategory category, ConversationVisibility visibility = ConversationVisibility.Public, DialogueChoiceRepeatPolicy repeat = DialogueChoiceRepeatPolicy.Repeatable, IEnumerable<DialogueConditionData> conditions = null, IEnumerable<DialogueEffectData> effects = null)
        {
            return new DialogueChoiceDefinitionData
            {
                choiceId = id,
                displayText = text,
                category = category,
                targetNodeId = target,
                visibility = visibility,
                repeatPolicy = repeat,
                conditions = (conditions ?? Array.Empty<DialogueConditionData>()).ToArray(),
                effects = (effects ?? Array.Empty<DialogueEffectData>()).ToArray()
            };
        }

        private static DialogueChoiceDefinitionData EndChoice(string id)
        {
            string prefix = string.IsNullOrWhiteSpace(id) ? string.Empty : id.Split('.')[0];
            return Choice(id, "End conversation", string.IsNullOrWhiteSpace(prefix) ? string.Empty : $"{prefix}.end", DialogueChoiceCategory.EndConversation);
        }

        private static DialogueTransitionDefinitionData Transition(string id, string target)
        {
            return new DialogueTransitionDefinitionData { transitionId = id, targetNodeId = target, category = DialogueTransitionCategory.Redirect };
        }

        private static DialogueConditionData Condition(string id, DialogueConditionKind kind, string requiredId, bool hidden = false)
        {
            return new DialogueConditionData { conditionId = id, kind = kind, requiredId = requiredId, hidden = hidden, revealFailure = !hidden };
        }

        private static DialogueEffectData Flag(string id, string variableId)
        {
            return new DialogueEffectData { effectId = id, kind = DialogueEffectKind.SetLocalFlag, targetId = variableId };
        }

        private static DialogueEffectData OptionalOwner(string id, DialogueEffectKind kind, string targetId)
        {
            return new DialogueEffectData { effectId = id, kind = kind, targetId = targetId, requirement = DialogueEffectRequirement.Optional };
        }

        private static DialogueSpeakerSelectorData Provider() => new DialogueSpeakerSelectorData { kind = DialogueSpeakerSelectorKind.Provider };
        private static DialogueSpeakerSelectorData OfficeHolder() => new DialogueSpeakerSelectorData { kind = DialogueSpeakerSelectorKind.OfficeRepresentative };
    }
}
