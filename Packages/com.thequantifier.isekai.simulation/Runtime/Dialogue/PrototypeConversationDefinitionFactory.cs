using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Quests;

namespace UnityIsekaiGame.Dialogue
{
    public static class PrototypeConversationDefinitionFactory
    {
        public const string AdventurerGuildCounterDefinitionId = "conversation-definition.prototype.adventurer-guild-counter";
        public const string MerchantGuildCounterDefinitionId = "conversation-definition.prototype.merchant-guild-counter";
        public const string MayorDeskDefinitionId = "conversation-definition.prototype.mayor-desk";
        public const string GuildHeadOfficeDefinitionId = "conversation-definition.prototype.guild-head-office";
        public const string RecordsDeskDefinitionId = "conversation-definition.prototype.records-desk";
        public const string PrisonerInterviewDefinitionId = "conversation-definition.prototype.prisoner-interview";
        public const string PrivateAudienceDefinitionId = "conversation-definition.prototype.private-audience";
        public const string GroupBriefingDefinitionId = "conversation-definition.prototype.group-briefing";
        public const string MissingProviderDiagnosticDefinitionId = "conversation-definition.prototype.missing-provider-diagnostic";

        public static readonly string[] PrototypeDefinitionIds =
        {
            AdventurerGuildCounterDefinitionId,
            MerchantGuildCounterDefinitionId,
            MayorDeskDefinitionId,
            GuildHeadOfficeDefinitionId,
            RecordsDeskDefinitionId,
            PrisonerInterviewDefinitionId,
            PrivateAudienceDefinitionId,
            GroupBriefingDefinitionId,
            MissingProviderDiagnosticDefinitionId
        };

        public static DefinitionRegistry AddMissingPrototypeConversationDefinitions(DefinitionRegistry baseRegistry)
        {
            HashSet<string> ids = new HashSet<string>(baseRegistry?.DefinitionsById.Keys ?? Array.Empty<string>(), StringComparer.Ordinal);
            List<IGameDefinition> definitions = new List<IGameDefinition>();
            if (baseRegistry != null) definitions.AddRange(baseRegistry.DefinitionsById.Values.Where(definition => definition != null));
            foreach (ConversationDefinition definition in CreateMissingConversationDefinitions(ids)) definitions.Add(definition);
            return new DefinitionRegistry(definitions);
        }

        public static IReadOnlyList<ConversationDefinition> CreateMissingConversationDefinitions(IEnumerable<string> existingDefinitionIds)
        {
            HashSet<string> ids = existingDefinitionIds == null ? new HashSet<string>(StringComparer.Ordinal) : new HashSet<string>(existingDefinitionIds, StringComparer.Ordinal);
            List<ConversationDefinition> definitions = new List<ConversationDefinition>();

            Add(definitions, ids, AdventurerGuildCounterDefinitionId, "Adventurer Guild Counter Conversation", ConversationCategory.QuestOffer, ConversationVisibility.LocallyKnown, ConversationCoLocationPolicy.SameInteractionPoint, ConversationOverlapPolicy.PreventParticipantOverlap,
                providers: new[] { Requirement(ConversationProviderRequirementKind.OrganizationMembership, "organization.prototype.adventurers-guild") },
                roles: new[] { ConversationParticipantRole.Initiator, ConversationParticipantRole.Provider, ConversationParticipantRole.QuestRecipient },
                questSources: new[] { PrototypeQuestSourceDefinitionFactory.AdventurerGuildCounterDefinitionId },
                questTags: new[] { "guild" },
                authority: new[] { "authority.prototype.guild.quest-offer" },
                tags: new[] { "guild", "counter", "quest", "prototype" });

            Add(definitions, ids, MerchantGuildCounterDefinitionId, "Merchant Guild Counter Conversation", ConversationCategory.MerchantService, ConversationVisibility.LocallyKnown, ConversationCoLocationPolicy.SameInteractionPoint, ConversationOverlapPolicy.PreventParticipantOverlap,
                providers: new[] { Requirement(ConversationProviderRequirementKind.Organization, "organization.prototype.merchant-guild") },
                roles: new[] { ConversationParticipantRole.Initiator, ConversationParticipantRole.Provider, ConversationParticipantRole.Merchant },
                questSources: new[] { PrototypeQuestSourceDefinitionFactory.MerchantGuildCounterDefinitionId },
                questTags: new[] { "merchant" },
                tags: new[] { "merchant", "counter", "prototype" });

            Add(definitions, ids, MayorDeskDefinitionId, "Mayor Desk Conversation", ConversationCategory.GovernmentOffice, ConversationVisibility.GovernmentOfficial, ConversationCoLocationPolicy.SameInteractionPoint, ConversationOverlapPolicy.PreventParticipantOverlap,
                providers: new[] { Requirement(ConversationProviderRequirementKind.Office, "office.prototype.mayor") },
                roles: new[] { ConversationParticipantRole.Initiator, ConversationParticipantRole.OfficeHolder, ConversationParticipantRole.Listener },
                questSources: new[] { PrototypeQuestSourceDefinitionFactory.MayorOfficeDeskDefinitionId },
                questTags: new[] { "civic" },
                authority: new[] { "authority.prototype.city.quest-assign" },
                tags: new[] { "mayor", "office", "government", "prototype" });

            Add(definitions, ids, GuildHeadOfficeDefinitionId, "Guild Head Office Conversation", ConversationCategory.GuildOffice, ConversationVisibility.OrganizationMembers, ConversationCoLocationPolicy.SameInteractionPoint, ConversationOverlapPolicy.PreventParticipantOverlap,
                providers: new[] { Requirement(ConversationProviderRequirementKind.Office, "office.prototype.guild-head") },
                roles: new[] { ConversationParticipantRole.Initiator, ConversationParticipantRole.OfficeHolder, ConversationParticipantRole.OrganizationRepresentative },
                tags: new[] { "guild", "office", "private", "prototype" });

            Add(definitions, ids, RecordsDeskDefinitionId, "Records Desk Conversation", ConversationCategory.RecordsInquiry, ConversationVisibility.OfficeRestricted, ConversationCoLocationPolicy.SameInteractionPoint, ConversationOverlapPolicy.PreventParticipantOverlap,
                providers: new[] { Requirement(ConversationProviderRequirementKind.Authority, "authority.prototype.records.read") },
                roles: new[] { ConversationParticipantRole.Initiator, ConversationParticipantRole.Provider, ConversationParticipantRole.Listener },
                tags: new[] { "records", "access", "prototype" });

            Add(definitions, ids, PrisonerInterviewDefinitionId, "Prisoner Interview Conversation", ConversationCategory.PrisonerInterview, ConversationVisibility.Private, ConversationCoLocationPolicy.SameLocation, ConversationOverlapPolicy.PreventParticipantOverlap,
                providers: new[] { Requirement(ConversationProviderRequirementKind.Authority, "authority.prototype.prison.interview") },
                roles: new[] { ConversationParticipantRole.Initiator, ConversationParticipantRole.Prisoner, ConversationParticipantRole.Guard },
                tags: new[] { "prison", "interview", "private", "prototype" });

            Add(definitions, ids, PrivateAudienceDefinitionId, "Private Audience Conversation", ConversationCategory.PrivateAudience, ConversationVisibility.Private, ConversationCoLocationPolicy.SameInteractionPoint, ConversationOverlapPolicy.PreventParticipantOverlap,
                roles: new[] { ConversationParticipantRole.Initiator, ConversationParticipantRole.Addressee },
                tags: new[] { "private", "audience", "prototype" });

            Add(definitions, ids, GroupBriefingDefinitionId, "Group Briefing Conversation", ConversationCategory.GroupDiscussion, ConversationVisibility.ParticipantKnown, ConversationCoLocationPolicy.SameLocation, ConversationOverlapPolicy.AllowConcurrent,
                roles: new[] { ConversationParticipantRole.Initiator, ConversationParticipantRole.Speaker, ConversationParticipantRole.Listener, ConversationParticipantRole.Witness },
                tags: new[] { "group", "briefing", "prototype" });

            Add(definitions, ids, MissingProviderDiagnosticDefinitionId, "Missing Provider Diagnostic Conversation", ConversationCategory.Diagnostic, ConversationVisibility.Diagnostic, ConversationCoLocationPolicy.NotRequired, ConversationOverlapPolicy.AllowConcurrent,
                providers: new[] { Requirement(ConversationProviderRequirementKind.Person, "person.prototype.required-provider") },
                roles: new[] { ConversationParticipantRole.Initiator, ConversationParticipantRole.Provider },
                tags: new[] { "diagnostic", "provider", "prototype" });

            return definitions;
        }

        private static void Add(
            ICollection<ConversationDefinition> definitions,
            ISet<string> existingIds,
            string id,
            string displayName,
            ConversationCategory category,
            ConversationVisibility visibility,
            ConversationCoLocationPolicy coLocationPolicy,
            ConversationOverlapPolicy overlapPolicy,
            IEnumerable<ConversationProviderRequirementData> providers = null,
            IEnumerable<ConversationParticipantRole> roles = null,
            IEnumerable<string> questSources = null,
            IEnumerable<string> questTags = null,
            IEnumerable<string> authority = null,
            IEnumerable<string> tags = null)
        {
            if (existingIds.Contains(id)) return;
            ConversationDefinition definition = ScriptableObject.CreateInstance<ConversationDefinition>();
            definition.name = displayName;
            definition.DevelopmentConfigure(id, displayName, category, visibility, coLocationPolicy, overlapPolicy, providers, roles, questSources, questTags, authority, tags);
            definitions.Add(definition);
            existingIds.Add(id);
        }

        private static ConversationProviderRequirementData Requirement(ConversationProviderRequirementKind kind, string id, bool hidden = false)
        {
            return new ConversationProviderRequirementData { kind = kind, requirementId = id, hidden = hidden };
        }
    }
}
