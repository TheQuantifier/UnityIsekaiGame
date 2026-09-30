using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Social.Attitudes;
using UnityIsekaiGame.Social.Relationships;

namespace UnityIsekaiGame.Social.Family
{
    public static class PrototypeFamilyRelationshipDefinitionFactory
    {
        public const string StrictAdultRomancePolicyId = "romance-policy.prototype.strict-adult";
        public const string FamilyHouseholdDefinitionId = "household.prototype.family";
        public const string SharedResidenceHouseholdDefinitionId = "household.prototype.shared-residence";

        public static IReadOnlyList<ScriptableObject> CreateDefinitions()
        {
            return new ScriptableObject[]
            {
                StrictPolicy(),
                Household(FamilyHouseholdDefinitionId, "Prototype Family Household", 1, 32, 1, 2, false, true, "family", "household"),
                Household(SharedResidenceHouseholdDefinitionId, "Prototype Shared Residence", 1, 24, 0, 4, false, false, "shared-residence", "household")
            };
        }

        public static DefinitionRegistry AddMissingPrototypeFamilyRelationshipDefinitions(DefinitionRegistry baseRegistry)
        {
            DefinitionRegistry withRelationships = PrototypeRelationshipDefinitionFactory.AddMissingPrototypeRelationshipDefinitions(baseRegistry);
            DefinitionRegistry withAttitudes = PrototypeAttitudeDefinitionFactory.AddMissingPrototypeAttitudeDefinitions(withRelationships);
            IGameDefinition[] existing = withAttitudes.DefinitionsById.Values.ToArray();
            IGameDefinition[] additions = CreateDefinitions()
                .OfType<IGameDefinition>()
                .Where(definition => !withAttitudes.Contains(definition.Id))
                .ToArray();
            return new DefinitionRegistry(existing.Concat(additions));
        }

        private static RomanticEligibilityPolicyDefinition StrictPolicy()
        {
            RomanticEligibilityPolicyDefinition definition = ScriptableObject.CreateInstance<RomanticEligibilityPolicyDefinition>();
            definition.name = "PrototypeStrictAdultRomancePolicy";
            definition.DevelopmentConfigure(
                StrictAdultRomancePolicyId,
                "Prototype Strict Adult Romance Policy",
                adultRequired: true,
                consentRequired: true,
                guardianDependentProhibited: true,
                exclusive: true,
                maximumPartners: 1,
                prohibitedKinship: new[]
                {
                    KinshipClassification.Parent,
                    KinshipClassification.Child,
                    KinshipClassification.BiologicalParent,
                    KinshipClassification.BiologicalChild,
                    KinshipClassification.AdoptiveParent,
                    KinshipClassification.AdoptiveChild,
                    KinshipClassification.FullSibling,
                    KinshipClassification.HalfSibling,
                    KinshipClassification.AdoptiveSibling,
                    KinshipClassification.Ancestor,
                    KinshipClassification.Descendant
                },
                tagIds: new[] { "prototype", "strict", "adult-romance" });
            return definition;
        }

        private static HouseholdDefinition Household(string id, string name, int minimumMembers, int maximumMembers, int minimumHeads, int maximumHeads, bool residenceRequired, bool adultHeadRequired, params string[] tags)
        {
            HouseholdDefinition definition = ScriptableObject.CreateInstance<HouseholdDefinition>();
            definition.name = name.Replace(" ", string.Empty);
            definition.DevelopmentConfigure(
                id,
                name,
                Enum.GetValues(typeof(HouseholdRole)).Cast<HouseholdRole>().Where(role => role != HouseholdRole.Custom),
                minimumMembers,
                maximumMembers,
                minimumHeads,
                maximumHeads,
                residenceRequired,
                adultHeadRequired,
                tagIds: tags);
            return definition;
        }
    }
}
