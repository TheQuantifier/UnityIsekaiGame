using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Progression
{
    public interface IProgressionRandomSource
    {
        float Next01();
        int NextInclusive(int minimumInclusive, int maximumInclusive);
    }

    public sealed class SeededProgressionRandomSource : IProgressionRandomSource
    {
        private readonly System.Random random;

        public SeededProgressionRandomSource(int seed)
        {
            random = new System.Random(seed);
        }

        public float Next01()
        {
            return (float)random.NextDouble();
        }

        public int NextInclusive(int minimumInclusive, int maximumInclusive)
        {
            if (maximumInclusive <= minimumInclusive)
            {
                return minimumInclusive;
            }

            return random.Next(minimumInclusive, maximumInclusive + 1);
        }
    }

    public sealed class CharacterOriginGenerationResult
    {
        private CharacterOriginGenerationResult(bool succeeded, string message, OriginFamilyDefinition family, OriginDefinition origin, BirthGiftDefinition birthGift, long startingGold)
        {
            Succeeded = succeeded;
            Message = message;
            Family = family;
            Origin = origin;
            BirthGift = birthGift;
            StartingGold = startingGold;
        }

        public bool Succeeded { get; }
        public string Message { get; }
        public OriginFamilyDefinition Family { get; }
        public OriginDefinition Origin { get; }
        public BirthGiftDefinition BirthGift { get; }
        public long StartingGold { get; }

        public static CharacterOriginGenerationResult Success(OriginFamilyDefinition family, OriginDefinition origin, BirthGiftDefinition birthGift, long startingGold)
        {
            return new CharacterOriginGenerationResult(true, "Origin generated.", family, origin, birthGift, startingGold);
        }

        public static CharacterOriginGenerationResult Failure(string message)
        {
            return new CharacterOriginGenerationResult(false, message, null, null, null, 0L);
        }
    }

    public sealed class BirthGiftWeightEntry
    {
        public BirthGiftWeightEntry(
            BirthGiftDefinition gift,
            float baseWeight,
            float affinityMultiplier,
            float giftMultiplier,
            float familyRarityMultiplier,
            float originRarityMultiplier,
            float finalWeight,
            float probability)
        {
            Gift = gift;
            BaseWeight = baseWeight;
            AffinityMultiplier = affinityMultiplier;
            GiftMultiplier = giftMultiplier;
            FamilyRarityMultiplier = familyRarityMultiplier;
            OriginRarityMultiplier = originRarityMultiplier;
            FinalWeight = finalWeight;
            Probability = probability;
        }

        public BirthGiftDefinition Gift { get; }
        public float BaseWeight { get; }
        public float AffinityMultiplier { get; }
        public float GiftMultiplier { get; }
        public float FamilyRarityMultiplier { get; }
        public float OriginRarityMultiplier { get; }
        public float FinalWeight { get; }
        public float Probability { get; }
    }

    public sealed class CharacterOriginGenerator
    {
        private readonly DefinitionRegistry registry;
        private readonly IProgressionRandomSource random;

        public CharacterOriginGenerator(DefinitionRegistry registry, IProgressionRandomSource random)
        {
            this.registry = registry;
            this.random = random;
        }

        public CharacterOriginGenerationResult Generate()
        {
            if (registry == null)
            {
                return CharacterOriginGenerationResult.Failure("Definition registry is missing.");
            }

            if (random == null)
            {
                return CharacterOriginGenerationResult.Failure("Random source is missing.");
            }

            List<OriginFamilyDefinition> families = registry.DefinitionsById.Values
                .OfType<OriginFamilyDefinition>()
                .Where(family => family.EnabledForAlpha && family.SelectionWeight > 0f)
                .OrderBy(family => family.Id, StringComparer.Ordinal)
                .ToList();
            OriginFamilyDefinition selectedFamily = SelectWeighted(families, family => family.SelectionWeight);
            if (selectedFamily == null)
            {
                return CharacterOriginGenerationResult.Failure("No enabled origin families are available.");
            }

            List<OriginDefinition> origins = selectedFamily.AllowedOrigins
                .Where(origin => origin != null && origin.EnabledForAlpha && origin.Family == selectedFamily && origin.SelectionWeight > 0f)
                .ToList();
            OriginDefinition selectedOrigin = SelectWeighted(origins, origin => origin.SelectionWeight);
            if (selectedOrigin == null)
            {
                return CharacterOriginGenerationResult.Failure($"No enabled origins are available for family '{selectedFamily.DisplayName}'.");
            }

            IReadOnlyList<BirthGiftWeightEntry> giftWeights = BuildBirthGiftWeights(selectedFamily, selectedOrigin, out string weightFailureReason);
            BirthGiftDefinition gift = SelectWeighted(giftWeights, entry => entry.FinalWeight)?.Gift;
            if (gift == null)
            {
                return CharacterOriginGenerationResult.Failure(string.IsNullOrWhiteSpace(weightFailureReason) ? "No enabled birth gifts are available." : weightFailureReason);
            }

            long startingGold = RollStartingGold(selectedOrigin);
            return CharacterOriginGenerationResult.Success(selectedFamily, selectedOrigin, gift, startingGold);
        }

        public IReadOnlyList<BirthGiftWeightEntry> BuildBirthGiftWeights(OriginFamilyDefinition family, OriginDefinition origin, out string failureReason)
        {
            failureReason = string.Empty;
            if (registry == null || family == null || origin == null)
            {
                failureReason = "A registry, origin family, and origin are required to calculate birth-gift weights.";
                return Array.Empty<BirthGiftWeightEntry>();
            }

            if (origin.Family != family || !family.AllowedOrigins.Contains(origin))
            {
                failureReason = $"Origin '{origin.Id}' is not an allowed member of family '{family.Id}'.";
                return Array.Empty<BirthGiftWeightEntry>();
            }

            if (!registry.TryGet(OriginGenerationPolicyDefinition.DefaultPolicyId, out OriginGenerationPolicyDefinition policy))
            {
                failureReason = $"Origin generation policy '{OriginGenerationPolicyDefinition.DefaultPolicyId}' is missing.";
                return Array.Empty<BirthGiftWeightEntry>();
            }

            List<BirthGiftDefinition> gifts = registry.DefinitionsById.Values
                .OfType<BirthGiftDefinition>()
                .Where(gift => gift.EnabledForAlpha && gift.SelectionWeight > 0f)
                .OrderBy(gift => gift.Id, StringComparer.Ordinal)
                .ToList();

            if (gifts.Count == 0)
            {
                failureReason = "No enabled birth gifts are available.";
                return Array.Empty<BirthGiftWeightEntry>();
            }

            HashSet<string> favoredGiftIds = new HashSet<string>(origin.FavoredGiftPool.Where(gift => gift != null).Select(gift => gift.Id), StringComparer.Ordinal);
            List<BirthGiftWeightEntry> entries = new List<BirthGiftWeightEntry>(gifts.Count);
            float totalWeight = 0f;
            for (int i = 0; i < gifts.Count; i++)
            {
                BirthGiftDefinition gift = gifts[i];
                float weight = gift.SelectionWeight;
                float affinityMultiplier = favoredGiftIds.Contains(gift.Id)
                    ? policy.FavoredGiftWeightMultiplier
                    : policy.UnfavoredGiftWeightMultiplier;
                float giftMultiplier = ResolveGiftModifier(origin.GiftWeightModifiers, gift.Id);
                float familyRarityMultiplier = gift.Rarity == null ? 1f : ResolveRarityModifier(family.GiftRarityWeightModifiers, gift.Rarity.Id);
                float originRarityMultiplier = gift.Rarity == null ? 1f : ResolveRarityModifier(origin.GiftRarityWeightModifiers, gift.Rarity.Id);
                weight *= affinityMultiplier;
                weight *= giftMultiplier;
                weight *= familyRarityMultiplier;
                weight *= originRarityMultiplier;
                weight = Mathf.Max(0f, weight);
                totalWeight += weight;
                entries.Add(new BirthGiftWeightEntry(
                    gift,
                    gift.SelectionWeight,
                    affinityMultiplier,
                    giftMultiplier,
                    familyRarityMultiplier,
                    originRarityMultiplier,
                    weight,
                    0f));
            }

            if (totalWeight <= 0f || float.IsNaN(totalWeight) || float.IsInfinity(totalWeight))
            {
                failureReason = $"Birth-gift weights for origin '{origin.Id}' do not produce a finite positive total.";
                return Array.Empty<BirthGiftWeightEntry>();
            }

            return entries
                .Select(entry => new BirthGiftWeightEntry(
                    entry.Gift,
                    entry.BaseWeight,
                    entry.AffinityMultiplier,
                    entry.GiftMultiplier,
                    entry.FamilyRarityMultiplier,
                    entry.OriginRarityMultiplier,
                    entry.FinalWeight,
                    entry.FinalWeight / totalWeight))
                .ToList();
        }

        private long RollStartingGold(OriginDefinition origin)
        {
            ProgressionCurrencyGrantDefinition grant = origin.StartingGold ?? origin.Family?.DefaultStartingMoney;
            if (grant == null)
            {
                return 0L;
            }

            long variation = grant.RandomVariation <= 0L ? 0L : random.NextInclusive(0, (int)Math.Min(int.MaxValue, grant.RandomVariation));
            return Math.Max(0L, grant.BaseAmount + variation);
        }

        private T SelectWeighted<T>(IReadOnlyList<T> values, Func<T, float> weightProvider)
            where T : class
        {
            if (values == null || values.Count == 0)
            {
                return null;
            }

            float total = 0f;
            for (int i = 0; i < values.Count; i++)
            {
                total += Mathf.Max(0f, weightProvider(values[i]));
            }

            if (total <= 0f)
            {
                return null;
            }

            float roll = random.Next01() * total;
            float cumulative = 0f;
            for (int i = 0; i < values.Count; i++)
            {
                cumulative += Mathf.Max(0f, weightProvider(values[i]));
                if (roll < cumulative)
                {
                    return values[i];
                }
            }

            return values[values.Count - 1];
        }

        private static float ResolveGiftModifier(IReadOnlyList<BirthGiftWeightModifierDefinition> modifiers, string giftId)
        {
            if (modifiers == null)
            {
                return 1f;
            }

            float multiplier = 1f;
            for (int i = 0; i < modifiers.Count; i++)
            {
                if (modifiers[i]?.Gift != null && string.Equals(modifiers[i].Gift.Id, giftId, StringComparison.Ordinal))
                {
                    multiplier *= modifiers[i].WeightMultiplier;
                }
            }

            return multiplier;
        }

        private static float ResolveRarityModifier(IReadOnlyList<RarityWeightModifierDefinition> modifiers, string rarityId)
        {
            if (modifiers == null)
            {
                return 1f;
            }

            float multiplier = 1f;
            for (int i = 0; i < modifiers.Count; i++)
            {
                if (modifiers[i]?.Rarity != null && string.Equals(modifiers[i].Rarity.Id, rarityId, StringComparison.Ordinal))
                {
                    multiplier *= modifiers[i].WeightMultiplier;
                }
            }

            return multiplier;
        }
    }
}
