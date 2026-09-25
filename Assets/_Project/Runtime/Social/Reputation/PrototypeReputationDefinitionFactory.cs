using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Social.Reputation
{
    public static class PrototypeReputationDefinitionFactory
    {
        public const string GlobalPublicAudienceId = "reputation.audience.global-public";
        public const string PrototypeTownAudienceId = "reputation.audience.place.prototype-town";
        public const string AdventurersGuildAudienceId = "reputation.audience.organization.adventurers-guild";
        public const string AdventurersGuildVeteransAudienceId = "reputation.audience.organization.adventurers-guild.veterans";
        public const string RoyalJurisdictionAudienceId = "reputation.audience.jurisdiction.prototype-kingdom";
        public const string CityGuardAudienceId = "reputation.audience.organization.prototype-city-guard";
        public const string HiddenInvestigatorsAudienceId = "reputation.audience.custom.hidden-investigators";

        public const string RenownId = "reputation.renown";
        public const string EsteemId = "reputation.esteem";
        public const string NotorietyId = "reputation.notoriety";
        public const string CredibilityId = "reputation.credibility";
        public const string PerceivedDangerId = "reputation.perceived-danger";
        public const string HonorId = "reputation.honor";

        public static IReadOnlyList<ScriptableObject> CreateDefinitions()
        {
            return new ScriptableObject[]
            {
                Audience(GlobalPublicAudienceId, "Global Public", ReputationAudienceCategory.GlobalPublic, ReputationAudienceScope.Global, text: "General public reputation that is not tied to a specific place."),
                Audience(PrototypeTownAudienceId, "Prototype Town Residents", ReputationAudienceCategory.PlacePopulation, ReputationAudienceScope.Contextual, "place.prototype-town", GlobalPublicAudienceId, hierarchy: true, text: "Local town-population reputation."),
                Audience(AdventurersGuildAudienceId, "Adventurers Guild", ReputationAudienceCategory.Organization, ReputationAudienceScope.Contextual, "organization.prototype.adventurers-guild", GlobalPublicAudienceId, hierarchy: true, text: "Institutional guild reputation."),
                Audience(AdventurersGuildVeteransAudienceId, "Adventurers Guild Veterans", ReputationAudienceCategory.Organization, ReputationAudienceScope.Contextual, "organization.prototype.adventurers-guild.veterans", AdventurersGuildAudienceId, hierarchy: true, text: "Child audience used to prove deterministic inheritance."),
                Audience(RoyalJurisdictionAudienceId, "Prototype Kingdom Jurisdiction", ReputationAudienceCategory.Jurisdiction, ReputationAudienceScope.Contextual, "jurisdiction.prototype-kingdom", GlobalPublicAudienceId, hierarchy: true, text: "Jurisdictional reputation suitable for notoriety requirements."),
                Audience(CityGuardAudienceId, "Prototype City Guard", ReputationAudienceCategory.Organization, ReputationAudienceScope.Contextual, "organization.prototype.city-guard", RoyalJurisdictionAudienceId, hierarchy: true, text: "Reputation with the prototype city's guard organization."),
                Audience(HiddenInvestigatorsAudienceId, "Hidden Investigators", ReputationAudienceCategory.CustomGroup, ReputationAudienceScope.Contextual, "group.prototype.hidden-investigators", GlobalPublicAudienceId, hierarchy: true, restricted: true, text: "Restricted prototype audience for visibility boundary tests."),
                Dimension(RenownId, "Renown", ReputationDimensionCategory.Recognition, 0, 100, 0, false, "How widely recognized the subject is within the audience."),
                Dimension(EsteemId, "Esteem", ReputationDimensionCategory.Regard, -100, 100, 0, true, "General favorable or unfavorable public regard."),
                Dimension(NotorietyId, "Notoriety", ReputationDimensionCategory.Infamy, 0, 100, 0, false, "Recognition associated with scandal, crime, controversy, or feared conduct."),
                Dimension(CredibilityId, "Credibility", ReputationDimensionCategory.Credibility, -100, 100, 0, true, "Whether the audience considers the subject reliable or believable."),
                Dimension(PerceivedDangerId, "Perceived Danger", ReputationDimensionCategory.Threat, 0, 100, 0, false, "How dangerous the audience considers the subject."),
                Dimension(HonorId, "Honor", ReputationDimensionCategory.Honor, -100, 100, 0, true, "Audience-relative perception of honorable conduct.")
            };
        }

        public static DefinitionRegistry AddMissingPrototypeReputationDefinitions(DefinitionRegistry baseRegistry)
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

        private static ReputationAudienceDefinition Audience(string id, string name, ReputationAudienceCategory category, ReputationAudienceScope scope, string context = "", string parent = "", bool hierarchy = false, bool restricted = false, string text = "")
        {
            ReputationAudienceDefinition definition = ScriptableObject.CreateInstance<ReputationAudienceDefinition>();
            definition.name = name.Replace(" ", string.Empty);
            definition.DevelopmentConfigure(id, name, category, scope, context, parent, hierarchy, available: true, restricted: restricted, text: text, tagIds: new[] { "prototype", "alpha" });
            return definition;
        }

        private static ReputationDimensionDefinition Dimension(string id, string name, ReputationDimensionCategory category, int minimum, int maximum, int neutral, bool negativeValuesAllowed, string description)
        {
            ReputationDimensionDefinition definition = ScriptableObject.CreateInstance<ReputationDimensionDefinition>();
            definition.name = name.Replace(" ", string.Empty);
            definition.DevelopmentConfigure(id, name, category, minimum, maximum, neutral, negativeValuesAllowed, description, new[] { "prototype", "alpha" });
            return definition;
        }
    }
}
