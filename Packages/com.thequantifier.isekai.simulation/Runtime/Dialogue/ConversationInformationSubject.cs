using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Quests;

namespace UnityIsekaiGame.Dialogue
{
    public static class ConversationInformationSubject
    {
        public const string ConversationTag = "conversation";
        public const string ConversationDefinitionTag = "conversation-definition";

        public static InformationSubjectReferenceData Conversation(string conversationId, string definitionId, string ownerPersonId = "", string controllingEntityId = "", IEnumerable<string> tags = null)
        {
            return Create(conversationId, definitionId, ownerPersonId, controllingEntityId, ConversationTag, tags);
        }

        public static InformationSubjectReferenceData Definition(string definitionId, IEnumerable<string> tags = null)
        {
            return Create(definitionId, string.Empty, string.Empty, string.Empty, ConversationDefinitionTag, tags);
        }

        private static InformationSubjectReferenceData Create(string subjectId, string parentSubjectId, string ownerPersonId, string controllingEntityId, string primaryTag, IEnumerable<string> tags)
        {
            return new InformationSubjectReferenceData
            {
                subjectType = InformationSubjectType.Custom,
                subjectId = subjectId ?? string.Empty,
                parentSubjectId = parentSubjectId ?? string.Empty,
                ownerPersonId = ownerPersonId ?? string.Empty,
                controllingEntityId = controllingEntityId ?? string.Empty,
                tags = QuestRuntimeModelUtility.Clean((tags ?? Enumerable.Empty<string>()).Concat(new[] { primaryTag }))
            };
        }
    }
}
