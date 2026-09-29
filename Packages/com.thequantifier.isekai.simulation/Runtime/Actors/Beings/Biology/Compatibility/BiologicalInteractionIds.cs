using System;
using UnityIsekaiGame.Beings.Biology.Hazards;

namespace UnityIsekaiGame.Beings.Biology.Compatibility
{
    public static class BiologicalInteractionIds
    {
        public const string Bleeding = "interaction.biology.bleeding";
        public const string Suffocation = "interaction.biology.suffocation";
        public const string Overheating = "interaction.biology.overheating";
        public const string Hypothermia = "interaction.biology.hypothermia";
        public const string Starvation = "interaction.biology.starvation";
        public const string Dehydration = "interaction.biology.dehydration";
        public const string ExtremeFatigue = "interaction.biology.extreme-fatigue";
        public const string SleepDeprivation = "interaction.biology.sleep-deprivation";

        public const string BluntTrauma = "interaction.injury.blunt-trauma";
        public const string Laceration = "interaction.injury.laceration";
        public const string Puncture = "interaction.injury.puncture";
        public const string Fracture = "interaction.injury.fracture";
        public const string Burn = "interaction.injury.burn";
        public const string Crush = "interaction.injury.crush";
        public const string Severing = "interaction.injury.severing";
        public const string OrganTrauma = "interaction.injury.organ-trauma";
        public const string CoreDamage = "interaction.injury.core-damage";
        public const string IncorporealDisruption = "interaction.injury.incorporeal-disruption";

        public const string Blood = "interaction.resource.blood";
        public const string Breath = "interaction.resource.breath";
        public const string Temperature = "interaction.resource.temperature";
        public const string Nutrition = "interaction.resource.nutrition";
        public const string Hydration = "interaction.resource.hydration";
        public const string Sleep = "interaction.resource.sleep";
        public const string Fatigue = "interaction.resource.fatigue";
        public const string Digestion = "interaction.metabolism.digestion";
        public const string Intoxication = "interaction.metabolism.intoxication";

        public const string NaturalHealing = "interaction.recovery.natural-healing";
        public const string Regeneration = "interaction.recovery.regeneration";
        public const string BiologicalHealing = "interaction.recovery.biological-healing";
        public const string ConstructRepair = "interaction.recovery.construct-repair";
        public const string SpiritRestoration = "interaction.recovery.spirit-restoration";
        public const string HolyHealing = "interaction.recovery.holy-healing";
        public const string NecroticRestoration = "interaction.recovery.necrotic-restoration";

        public const string Disease = "interaction.condition.disease";
        public const string Infection = "interaction.condition.infection";
        public const string Parasite = "interaction.condition.parasite";
        public const string Poison = "interaction.condition.poison";
        public const string Venom = "interaction.condition.venom";
        public const string Toxin = "interaction.condition.toxin";
        public const string Alcohol = "interaction.condition.alcohol";

        public const string Polymorph = "interaction.transformation.polymorph";
        public const string SpeciesChange = "interaction.transformation.species-change";
        public const string Possession = "interaction.transformation.possession";
        public const string BodyReplacement = "interaction.transformation.body-replacement";
        public const string Reincarnation = "interaction.transformation.reincarnation";

        public const string Holy = "interaction.energy.holy";
        public const string Necrotic = "interaction.energy.necrotic";
        public const string Fire = "interaction.energy.fire";
        public const string Cold = "interaction.energy.cold";
        public const string Radiant = "interaction.energy.radiant";
        public const string Corruption = "interaction.energy.corruption";

        public static string FromHazardId(string hazardDefinitionId)
        {
            return hazardDefinitionId switch
            {
                BiologicalHazardIds.Bleeding => Bleeding,
                BiologicalHazardIds.Suffocation => Suffocation,
                BiologicalHazardIds.Overheating => Overheating,
                BiologicalHazardIds.Hypothermia => Hypothermia,
                BiologicalHazardIds.Starvation => Starvation,
                BiologicalHazardIds.Dehydration => Dehydration,
                BiologicalHazardIds.ExtremeFatigue => ExtremeFatigue,
                BiologicalHazardIds.SleepDeprivation => SleepDeprivation,
                BiologicalHazardIds.EnvironmentalExposure => "interaction.environment.exposure",
                _ => string.Empty
            };
        }

        public static string FromInjuryTypeId(string injuryTypeId)
        {
            return injuryTypeId switch
            {
                "injury.blunt-trauma" => BluntTrauma,
                "injury.laceration" => Laceration,
                "injury.puncture" => Puncture,
                "injury.penetrating" => Puncture,
                "injury.fracture" => Fracture,
                "injury.burn" => Burn,
                "injury.crush" => Crush,
                "injury.severing" => Severing,
                "injury.organ-trauma" => OrganTrauma,
                "injury.core-damage" => CoreDamage,
                "injury.incorporeal-disruption" => IncorporealDisruption,
                _ => string.Empty
            };
        }

        public static bool IsCanonicalAlphaInteraction(string interactionId)
        {
            if (string.IsNullOrWhiteSpace(interactionId))
            {
                return false;
            }

            return interactionId.StartsWith("interaction.", StringComparison.Ordinal);
        }
    }
}
