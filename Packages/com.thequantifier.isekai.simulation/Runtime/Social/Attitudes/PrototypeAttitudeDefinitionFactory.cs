using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Social.Attitudes
{
    public static class PrototypeAttitudeDefinitionFactory
    {
        public const string TrustId = "attitude.trust";
        public const string AffectionId = "attitude.affection";
        public const string RespectId = "attitude.respect";
        public const string FearId = "attitude.fear";
        public const string LoyaltyId = "attitude.loyalty";
        public const string HostilityId = "attitude.hostility";
        public const string RomanticAttractionId = "attitude.romantic-attraction";

        public static IReadOnlyList<ScriptableObject> CreateDefinitions()
        {
            return new ScriptableObject[]
            {
                Definition(TrustId, "Trust", AttitudeDimensionCategory.Regard, -100, 100, 0, true, "Negative values represent distrust; positive values represent trust."),
                Definition(AffectionId, "Affection", AttitudeDimensionCategory.Attachment, -100, 100, 0, true, "Negative values represent dislike or aversion; positive values represent fondness."),
                Definition(RespectId, "Respect", AttitudeDimensionCategory.Regard, -100, 100, 0, true, "Negative values represent contempt; positive values represent respect or admiration."),
                Definition(FearId, "Fear", AttitudeDimensionCategory.Threat, 0, 100, 0, false, "Higher values represent greater fear."),
                Definition(LoyaltyId, "Loyalty", AttitudeDimensionCategory.Commitment, -100, 100, 0, true, "Negative values represent disloyalty; positive values represent commitment."),
                Definition(HostilityId, "Hostility", AttitudeDimensionCategory.Conflict, 0, 100, 0, false, "Higher values represent greater hostility."),
                Definition(RomanticAttractionId, "Romantic Attraction", AttitudeDimensionCategory.Attachment, 0, 100, 0, false, "Directional romantic interest. This never implies consent, compatibility, or a formal relationship.")
            };
        }

        public static DefinitionRegistry AddMissingPrototypeAttitudeDefinitions(DefinitionRegistry baseRegistry)
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

        private static AttitudeDimensionDefinition Definition(string id, string name, AttitudeDimensionCategory category, int minimum, int maximum, int neutral, bool negativeValuesAllowed, string description)
        {
            AttitudeDimensionDefinition definition = ScriptableObject.CreateInstance<AttitudeDimensionDefinition>();
            definition.name = name.Replace(" ", string.Empty);
            definition.DevelopmentConfigure(id, name, category, minimum, maximum, neutral, negativeValuesAllowed, description, new[] { "prototype", "alpha" });
            return definition;
        }
    }
}
