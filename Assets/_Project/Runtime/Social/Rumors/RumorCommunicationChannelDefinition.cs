using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Social.Rumors
{
    [CreateAssetMenu(fileName = "RumorCommunicationChannelDefinition", menuName = "Unity Isekai Game/Social/Rumor Communication Channel")]
    public sealed class RumorCommunicationChannelDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string channelId = "rumor.channel.prototype.conversation";
        [SerializeField] private string displayName = "Prototype Conversation";
        [SerializeField, TextArea] private string description;
        [SerializeField] private RumorCommunicationChannelCategory category = RumorCommunicationChannelCategory.Conversation;
        [SerializeField] private bool supportsPrivateRumors = true;
        [SerializeField] private bool supportsBroadcast = false;
        [SerializeField, Min(1)] private int defaultMaxListeners = 1;
        [SerializeField, Range(0, 1000)] private int defaultCredibilityModifier = 0;
        [SerializeField, Min(1)] private int version = 1;

        public string Id => ChannelId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? ChannelId : displayName;
        public string ChannelId => channelId ?? string.Empty;
        public RumorCommunicationChannelCategory Category => category;
        public bool SupportsPrivateRumors => supportsPrivateRumors;
        public bool SupportsBroadcast => supportsBroadcast;
        public int DefaultMaxListeners => Math.Max(1, defaultMaxListeners);
        public int DefaultCredibilityModifier => Math.Max(0, Math.Min(1000, defaultCredibilityModifier));
        public int Version => Math.Max(1, version);

        // Prototype definitions are also created by the runtime registry in release players,
        // so this configurator must remain available outside editor/development builds.
        public void DevelopmentConfigure(
            string id,
            string name,
            RumorCommunicationChannelCategory channelCategory,
            bool allowsPrivateRumors,
            bool allowsBroadcast,
            int maxListeners,
            int credibilityModifier = 0,
            string text = "")
        {
            channelId = id ?? string.Empty;
            displayName = name ?? string.Empty;
            description = text ?? string.Empty;
            category = channelCategory;
            supportsPrivateRumors = allowsPrivateRumors;
            supportsBroadcast = allowsBroadcast;
            defaultMaxListeners = Math.Max(1, maxListeners);
            defaultCredibilityModifier = Math.Max(0, Math.Min(1000, credibilityModifier));
        }
        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(ChannelId))
            {
                report.AddError("Rumor communication channel must declare a stable ID.");
            }
            else if (!ChannelId.StartsWith("rumor.channel.", StringComparison.Ordinal))
            {
                report.AddError($"Rumor communication channel '{DisplayName}' ID '{ChannelId}' must start with 'rumor.channel.'.");
            }

            if (!Enum.IsDefined(typeof(RumorCommunicationChannelCategory), category) || category == RumorCommunicationChannelCategory.Unknown)
            {
                report.AddError($"Rumor communication channel '{DisplayName}' must declare a concrete category.");
            }

            if (defaultMaxListeners < 1)
            {
                report.AddError($"Rumor communication channel '{DisplayName}' must support at least one listener.");
            }

            if (defaultCredibilityModifier < 0 || defaultCredibilityModifier > 1000)
            {
                report.AddError($"Rumor communication channel '{DisplayName}' credibility modifier is outside 0..1000.");
            }
        }
    }
}
