using System.Collections.Generic;

namespace UnityIsekaiGame.GameData
{
    public static class ClassificationUtility
    {
        public static bool HasTag(ITaggedDefinition definition, TagDefinition tag)
        {
            if (definition == null || tag == null)
            {
                return false;
            }

            IReadOnlyList<TagDefinition> tags = definition.Tags;
            for (int i = 0; i < tags.Count; i++)
            {
                if (HasSameId(tags[i], tag))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool HasTag(ITaggedDefinition definition, string tagId)
        {
            if (definition == null || string.IsNullOrWhiteSpace(tagId))
            {
                return false;
            }

            IReadOnlyList<TagDefinition> tags = definition.Tags;
            for (int i = 0; i < tags.Count; i++)
            {
                if (tags[i] != null && tags[i].Id == tagId)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsInCategory(ICategorizableDefinition definition, CategoryDefinition category)
        {
            return definition != null && IsInCategory(definition.PrimaryCategory, category);
        }

        public static bool IsInCategory(ICategorizableDefinition definition, string categoryId)
        {
            return definition != null && IsInCategory(definition.PrimaryCategory, categoryId);
        }

        public static bool IsInCategory(CategoryDefinition assignedCategory, CategoryDefinition category)
        {
            if (assignedCategory == null || category == null)
            {
                return false;
            }

            return IsInCategory(assignedCategory, category.Id);
        }

        public static bool IsInCategory(CategoryDefinition assignedCategory, string categoryId)
        {
            if (assignedCategory == null || string.IsNullOrWhiteSpace(categoryId))
            {
                return false;
            }

            HashSet<string> visitedIds = new HashSet<string>();
            CategoryDefinition current = assignedCategory;

            while (current != null && visitedIds.Add(current.Id))
            {
                if (current.Id == categoryId)
                {
                    return true;
                }

                current = current.ParentCategory;
            }

            return false;
        }

        public static IReadOnlyList<CategoryDefinition> GetAncestors(CategoryDefinition category)
        {
            List<CategoryDefinition> ancestors = new List<CategoryDefinition>();

            if (category == null)
            {
                return ancestors;
            }

            HashSet<string> visitedIds = new HashSet<string>();
            CategoryDefinition current = category.ParentCategory;

            while (current != null && visitedIds.Add(current.Id))
            {
                ancestors.Add(current);
                current = current.ParentCategory;
            }

            return ancestors;
        }

        public static bool HasCategoryCycle(CategoryDefinition category)
        {
            if (category == null)
            {
                return false;
            }

            HashSet<string> visitedIds = new HashSet<string>();
            CategoryDefinition current = category;

            while (current != null)
            {
                if (!visitedIds.Add(current.Id))
                {
                    return true;
                }

                current = current.ParentCategory;
            }

            return false;
        }

        private static bool HasSameId(IGameDefinition left, IGameDefinition right)
        {
            return left != null
                && right != null
                && !string.IsNullOrWhiteSpace(left.Id)
                && left.Id == right.Id;
        }
    }
}
