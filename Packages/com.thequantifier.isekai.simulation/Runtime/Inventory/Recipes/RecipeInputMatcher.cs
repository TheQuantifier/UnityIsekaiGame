using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory.Composition;

namespace UnityIsekaiGame.Inventory.Recipes
{
    public static class RecipeInputMatcher
    {
        private const string ResourceItemCategoryId = "category.item.material";

        public static bool HasSelector(RecipeInputSpecificationData input)
        {
            return input != null
                && (!string.IsNullOrWhiteSpace(input.itemDefinitionId)
                    || !string.IsNullOrWhiteSpace(input.materialDefinitionId)
                    || (input.itemCategoryIds?.Length ?? 0) > 0
                    || (input.materialTagIds?.Length ?? 0) > 0);
        }

        public static bool Matches(RecipeInputSpecificationData input, ItemDefinition item, DefinitionRegistry registry)
        {
            if (input == null || item == null || registry == null || !HasSelector(input))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(input.itemDefinitionId)
                && !string.Equals(input.itemDefinitionId, item.Id, StringComparison.Ordinal))
            {
                return false;
            }

            string[] categoryIds = Normalize(input.itemCategoryIds);
            if (categoryIds.Length > 0
                && (item.PrimaryCategory == null || !categoryIds.Contains(item.PrimaryCategory.Id, StringComparer.Ordinal)))
            {
                return false;
            }

            // A material-family selector describes a crafting resource, not every
            // finished item that happens to contain that material.
            if ((input.materialTagIds?.Length ?? 0) > 0
                && string.IsNullOrWhiteSpace(input.itemDefinitionId)
                && categoryIds.Length == 0
                && !string.Equals(item.PrimaryCategory?.Id, ResourceItemCategoryId, StringComparison.Ordinal))
            {
                return false;
            }

            HashSet<string> materialIdsAndTags = ResolveMaterialIdsAndTags(item, registry);
            if (!string.IsNullOrWhiteSpace(input.materialDefinitionId)
                && !materialIdsAndTags.Contains(input.materialDefinitionId))
            {
                return false;
            }

            string[] requiredMaterialTags = Normalize(input.materialTagIds);
            return requiredMaterialTags.All(materialIdsAndTags.Contains);
        }

        public static IReadOnlyList<ItemDefinition> FindMatchingItems(RecipeInputSpecificationData input, DefinitionRegistry registry)
        {
            if (registry == null)
            {
                return Array.Empty<ItemDefinition>();
            }

            return registry.DefinitionsById.Values
                .OfType<ItemDefinition>()
                .Where(item => Matches(input, item, registry))
                .OrderBy(item => item.Id, StringComparer.Ordinal)
                .ToArray();
        }

        public static string Describe(RecipeInputSpecificationData input, DefinitionRegistry registry)
        {
            if (input == null)
            {
                return "Unknown input";
            }

            if (!string.IsNullOrWhiteSpace(input.itemDefinitionId)
                && registry != null
                && registry.TryGet(input.itemDefinitionId, out ItemDefinition item))
            {
                return item.DisplayName;
            }

            if ((input.materialTagIds?.Length ?? 0) > 0)
            {
                return string.Join(" + ", Normalize(input.materialTagIds).Select(tag => $"Any {DisplayId(tag)}"));
            }

            if ((input.itemCategoryIds?.Length ?? 0) > 0)
            {
                return string.Join(" + ", Normalize(input.itemCategoryIds).Select(category => $"Any {DisplayId(category)}"));
            }

            if (!string.IsNullOrWhiteSpace(input.materialDefinitionId)
                && registry != null
                && registry.TryGet(input.materialDefinitionId, out MaterialDefinition material))
            {
                return material.DisplayName;
            }

            return FirstNonEmpty(input.itemDefinitionId, input.materialDefinitionId, "Unspecified input");
        }

        public static IReadOnlyList<string> GetResourceCategoryIds(ItemDefinition item, DefinitionRegistry registry)
        {
            return ResolveMaterialIdsAndTags(item, registry)
                .Where(value => value.StartsWith("material.", StringComparison.Ordinal))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        private static HashSet<string> ResolveMaterialIdsAndTags(ItemDefinition item, DefinitionRegistry registry)
        {
            HashSet<string> values = new HashSet<string>(StringComparer.Ordinal);
            foreach (ItemMaterialEntryData entry in item?.DefaultCompositionTemplate?.materials ?? new List<ItemMaterialEntryData>())
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.materialDefinitionId))
                {
                    continue;
                }

                values.Add(entry.materialDefinitionId);
                if (!registry.TryGet(entry.materialDefinitionId, out MaterialDefinition material))
                {
                    continue;
                }

                values.Add($"material.{material.Category.ToString().ToLowerInvariant()}");
                foreach (string tag in material.MaterialTags)
                {
                    if (!string.IsNullOrWhiteSpace(tag)) values.Add(tag);
                }
            }

            return values;
        }

        private static string[] Normalize(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        private static string DisplayId(string value)
        {
            string suffix = value?.Split('.').LastOrDefault() ?? string.Empty;
            return string.Join(" ", suffix.Split('-', '_').Where(part => part.Length > 0).Select(part => char.ToUpperInvariant(part[0]) + part.Substring(1)));
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }
    }
}
