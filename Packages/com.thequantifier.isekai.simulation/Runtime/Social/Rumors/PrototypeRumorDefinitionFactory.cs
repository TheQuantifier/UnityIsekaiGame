using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Social.Rumors
{
    public static class PrototypeRumorDefinitionFactory
    {
        public const string PersonalConductRumorId = "rumor.prototype.personal-conduct";
        public const string PublicNewsRumorId = "rumor.prototype.public-news";
        public const string FabricatedAccusationRumorId = "rumor.prototype.fabricated-accusation";
        public const string SecretLeakRumorId = "rumor.prototype.secret-leak";
        public const string ReputationRumorId = "rumor.prototype.reputation";

        public const string ConversationChannelId = "rumor.channel.prototype.conversation";
        public const string TavernGossipChannelId = "rumor.channel.prototype.tavern-gossip";
        public const string PublicSpeechChannelId = "rumor.channel.prototype.public-speech";
        public const string DevelopmentFixtureChannelId = "rumor.channel.prototype.development-fixture";

        public static IReadOnlyList<ScriptableObject> CreateDefinitions()
        {
            return new ScriptableObject[]
            {
                Rumor(PersonalConductRumorId, "Prototype Personal Conduct Rumor", RumorCategory.PersonalConduct, RumorDisclosure.Shareable, RumorDistortionPolicy.DeterministicMetadataOnly, true, true, true, "A social claim about a person's conduct."),
                Rumor(PublicNewsRumorId, "Prototype Public News Rumor", RumorCategory.PublicNews, RumorDisclosure.Public, RumorDistortionPolicy.None, true, true, true, "A public event report that can be transmitted as testimony."),
                Rumor(FabricatedAccusationRumorId, "Prototype Fabricated Accusation", RumorCategory.CrimeOrAccusation, RumorDisclosure.Shareable, RumorDistortionPolicy.ForcedConfidenceDecrease, true, true, true, "An explicit fabrication that must never become authoritative truth."),
                Rumor(SecretLeakRumorId, "Prototype Secret Leak", RumorCategory.Secret, RumorDisclosure.Secret, RumorDistortionPolicy.ForcedAnonymousSource, true, true, true, "A restricted rumor used to prove disclosure boundaries."),
                Rumor(ReputationRumorId, "Prototype Reputation Rumor", RumorCategory.Reputation, RumorDisclosure.Shareable, RumorDistortionPolicy.DeterministicMetadataOnly, true, true, true, "A rumor eligible for explicit reputation integration."),
                Channel(ConversationChannelId, "Prototype Conversation", RumorCommunicationChannelCategory.Conversation, true, false, 1, 0),
                Channel(TavernGossipChannelId, "Prototype Tavern Gossip", RumorCommunicationChannelCategory.TavernGossip, false, true, 8, 50),
                Channel(PublicSpeechChannelId, "Prototype Public Speech", RumorCommunicationChannelCategory.PublicSpeech, false, true, 32, 25),
                Channel(DevelopmentFixtureChannelId, "Development Fixture", RumorCommunicationChannelCategory.DevelopmentFixture, true, true, 64, 0)
            };
        }

        public static DefinitionRegistry AddMissingPrototypeRumorDefinitions(DefinitionRegistry baseRegistry)
        {
            IGameDefinition[] existing = baseRegistry == null
                ? new IGameDefinition[0]
                : baseRegistry.DefinitionsById.Values.ToArray();
            IGameDefinition[] additions = CreateDefinitions()
                .OfType<IGameDefinition>()
                .Where(definition => baseRegistry == null || !baseRegistry.Contains(definition.Id))
                .ToArray();
            return new DefinitionRegistry(existing.Concat(additions));
        }

        private static RumorDefinition Rumor(string id, string name, RumorCategory category, RumorDisclosure disclosure, RumorDistortionPolicy distortion, bool retransmission, bool anonymous, bool concealment, string text)
        {
            RumorDefinition definition = ScriptableObject.CreateInstance<RumorDefinition>();
            definition.name = name.Replace(" ", string.Empty);
            definition.DevelopmentConfigure(id, name, category, disclosure, distortion, retransmission, anonymous, concealment, text: text, tagIds: new[] { "prototype", "alpha", "social" });
            return definition;
        }

        private static RumorCommunicationChannelDefinition Channel(string id, string name, RumorCommunicationChannelCategory category, bool privateRumors, bool broadcast, int maxListeners, int credibilityModifier)
        {
            RumorCommunicationChannelDefinition definition = ScriptableObject.CreateInstance<RumorCommunicationChannelDefinition>();
            definition.name = name.Replace(" ", string.Empty);
            definition.DevelopmentConfigure(id, name, category, privateRumors, broadcast, maxListeners, credibilityModifier);
            return definition;
        }
    }
}
