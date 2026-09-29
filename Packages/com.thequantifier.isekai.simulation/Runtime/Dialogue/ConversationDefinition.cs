using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Quests;

namespace UnityIsekaiGame.Dialogue
{
    [CreateAssetMenu(fileName = "ConversationDefinition", menuName = "Unity Isekai Game/Dialogue/Conversation Definition")]
    public sealed class ConversationDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string definitionId;
        [SerializeField] private string displayName;
        [SerializeField] private ConversationCategory category = ConversationCategory.General;
        [SerializeField] private ConversationVisibility defaultVisibility = ConversationVisibility.Public;
        [SerializeField] private ConversationCoLocationPolicy coLocationPolicy = ConversationCoLocationPolicy.SameInteractionPoint;
        [SerializeField] private ConversationOverlapPolicy overlapPolicy = ConversationOverlapPolicy.PreventParticipantOverlap;
        [SerializeField] private ConversationProviderRequirementData[] providerRequirements = Array.Empty<ConversationProviderRequirementData>();
        [SerializeField] private ConversationParticipantRole[] requiredRoles = Array.Empty<ConversationParticipantRole>();
        [SerializeField] private string[] supportedQuestSourceDefinitionIds = Array.Empty<string>();
        [SerializeField] private string[] supportedQuestTagIds = Array.Empty<string>();
        [SerializeField] private string[] authorityRequirementIds = Array.Empty<string>();
        [SerializeField] private string[] tagIds = Array.Empty<string>();

        public string Id => definitionId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public ConversationCategory Category => category;
        public ConversationVisibility DefaultVisibility => defaultVisibility;
        public ConversationCoLocationPolicy CoLocationPolicy => coLocationPolicy;
        public ConversationOverlapPolicy OverlapPolicy => overlapPolicy;
        public IReadOnlyList<ConversationProviderRequirementData> ProviderRequirements => (providerRequirements ?? Array.Empty<ConversationProviderRequirementData>()).Where(value => value != null).Select(value => value.Clone()).ToArray();
        public IReadOnlyList<ConversationParticipantRole> RequiredRoles => (requiredRoles ?? Array.Empty<ConversationParticipantRole>()).Where(value => value != ConversationParticipantRole.Unknown).Distinct().OrderBy(value => value.ToString(), StringComparer.Ordinal).ToArray();
        public IReadOnlyList<string> SupportedQuestSourceDefinitionIds => QuestRuntimeModelUtility.Clean(supportedQuestSourceDefinitionIds);
        public IReadOnlyList<string> SupportedQuestTagIds => QuestRuntimeModelUtility.Clean(supportedQuestTagIds);
        public IReadOnlyList<string> AuthorityRequirementIds => QuestRuntimeModelUtility.Clean(authorityRequirementIds);
        public IReadOnlyList<string> TagIds => QuestRuntimeModelUtility.Clean(tagIds);

        public ConversationDefinitionRecordData ToRecordData()
        {
            return new ConversationDefinitionRecordData
            {
                definitionId = Id,
                displayName = DisplayName,
                category = Category,
                defaultVisibility = DefaultVisibility,
                coLocationPolicy = CoLocationPolicy,
                overlapPolicy = OverlapPolicy,
                providerRequirements = ProviderRequirements.ToArray(),
                requiredRoles = RequiredRoles.ToArray(),
                supportedQuestSourceDefinitionIds = SupportedQuestSourceDefinitionIds.ToArray(),
                supportedQuestTagIds = SupportedQuestTagIds.ToArray(),
                authorityRequirementIds = AuthorityRequirementIds.ToArray(),
                tagIds = TagIds.ToArray()
            };
        }

        public void DevelopmentConfigure(
            string id,
            string name,
            ConversationCategory conversationCategory,
            ConversationVisibility visibility,
            ConversationCoLocationPolicy locationPolicy,
            ConversationOverlapPolicy participantOverlapPolicy,
            IEnumerable<ConversationProviderRequirementData> providers = null,
            IEnumerable<ConversationParticipantRole> roles = null,
            IEnumerable<string> questSourceDefinitionIds = null,
            IEnumerable<string> questTags = null,
            IEnumerable<string> authority = null,
            IEnumerable<string> tags = null)
        {
            definitionId = id ?? string.Empty;
            displayName = string.IsNullOrWhiteSpace(name) ? definitionId : name;
            category = conversationCategory == ConversationCategory.Unknown ? ConversationCategory.General : conversationCategory;
            defaultVisibility = visibility == ConversationVisibility.Unknown ? ConversationVisibility.Public : visibility;
            coLocationPolicy = locationPolicy == ConversationCoLocationPolicy.Unknown ? ConversationCoLocationPolicy.SameInteractionPoint : locationPolicy;
            overlapPolicy = participantOverlapPolicy == ConversationOverlapPolicy.Unknown ? ConversationOverlapPolicy.PreventParticipantOverlap : participantOverlapPolicy;
            providerRequirements = (providers ?? Array.Empty<ConversationProviderRequirementData>()).Where(value => value != null).Select(value => value.Clone()).ToArray();
            requiredRoles = (roles ?? Array.Empty<ConversationParticipantRole>()).Where(value => value != ConversationParticipantRole.Unknown).Distinct().OrderBy(value => value.ToString(), StringComparer.Ordinal).ToArray();
            supportedQuestSourceDefinitionIds = QuestRuntimeModelUtility.Clean(questSourceDefinitionIds);
            supportedQuestTagIds = QuestRuntimeModelUtility.Clean(questTags);
            authorityRequirementIds = QuestRuntimeModelUtility.Clean(authority);
            tagIds = QuestRuntimeModelUtility.Clean(tags);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null) return;

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError("Conversation definition is missing a stable ID.");
            }
            else if (!Id.StartsWith("conversation-definition.", StringComparison.Ordinal))
            {
                report.AddWarning($"Conversation definition '{DisplayName}' should use the 'conversation-definition.' namespace prefix.");
            }

            if (Category == ConversationCategory.Unknown) report.AddError($"Conversation definition '{DisplayName}' must declare a concrete category.");
            if (DefaultVisibility == ConversationVisibility.Unknown) report.AddError($"Conversation definition '{DisplayName}' must declare a concrete default visibility.");
            if (CoLocationPolicy == ConversationCoLocationPolicy.Unknown) report.AddError($"Conversation definition '{DisplayName}' must declare a co-location policy.");
            if (OverlapPolicy == ConversationOverlapPolicy.Unknown) report.AddError($"Conversation definition '{DisplayName}' must declare an overlap policy.");

            foreach (ConversationProviderRequirementData requirement in ProviderRequirements)
            {
                if (requirement.kind == ConversationProviderRequirementKind.Unknown)
                {
                    report.AddError($"Conversation definition '{DisplayName}' has an unknown provider requirement.");
                }
            }

            foreach (string questSourceDefinitionId in SupportedQuestSourceDefinitionIds)
            {
                if (definitionsById != null && !definitionsById.ContainsKey(questSourceDefinitionId))
                {
                    report.AddWarning($"Conversation definition '{DisplayName}' references missing Quest Source definition '{questSourceDefinitionId}'.");
                }
            }
        }
    }
}
