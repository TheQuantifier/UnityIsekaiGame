using System.Collections.Generic;
using System.Text;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Factions
{
    public static class FactionHierarchyUtility
    {
        public static bool HasSameId(FactionDefinition left, FactionDefinition right)
        {
            return left != null
                && right != null
                && !string.IsNullOrWhiteSpace(left.Id)
                && left.Id == right.Id;
        }

        public static bool IsDescendantOf(FactionDefinition faction, FactionDefinition ancestor)
        {
            if (faction == null || ancestor == null)
            {
                return false;
            }

            HashSet<string> visitedIds = new HashSet<string>();
            FactionDefinition current = faction.ParentFaction;
            while (current != null && visitedIds.Add(current.Id))
            {
                if (HasSameId(current, ancestor))
                {
                    return true;
                }

                current = current.ParentFaction;
            }

            return false;
        }

        public static IReadOnlyList<FactionDefinition> GetAncestors(FactionDefinition faction)
        {
            List<FactionDefinition> ancestors = new List<FactionDefinition>();
            if (faction == null)
            {
                return ancestors;
            }

            HashSet<string> visitedIds = new HashSet<string>();
            FactionDefinition current = faction.ParentFaction;
            while (current != null && visitedIds.Add(current.Id))
            {
                ancestors.Add(current);
                current = current.ParentFaction;
            }

            return ancestors;
        }

        public static FactionDefinition GetRootFaction(FactionDefinition faction)
        {
            if (faction == null)
            {
                return null;
            }

            FactionDefinition root = faction;
            HashSet<string> visitedIds = new HashSet<string>();
            while (root.ParentFaction != null && visitedIds.Add(root.Id))
            {
                root = root.ParentFaction;
            }

            return root;
        }

        public static FactionDefinition FindNearestAncestorOfKind(FactionDefinition faction, FactionKind kind, bool includeSelf = true)
        {
            if (faction == null)
            {
                return null;
            }

            HashSet<string> visitedIds = new HashSet<string>();
            FactionDefinition current = includeSelf ? faction : faction.ParentFaction;
            while (current != null && visitedIds.Add(current.Id))
            {
                if (current.Kind == kind)
                {
                    return current;
                }

                current = current.ParentFaction;
            }

            return null;
        }

        public static FactionDefinition FindNearestAncestorInCategory(FactionDefinition faction, string categoryId, bool includeSelf = true)
        {
            if (faction == null || string.IsNullOrWhiteSpace(categoryId))
            {
                return null;
            }

            HashSet<string> visitedIds = new HashSet<string>();
            FactionDefinition current = includeSelf ? faction : faction.ParentFaction;
            while (current != null && visitedIds.Add(current.Id))
            {
                if (ClassificationUtility.IsInCategory(current, categoryId))
                {
                    return current;
                }

                current = current.ParentFaction;
            }

            return null;
        }

        public static bool HasParentCycle(FactionDefinition faction)
        {
            if (faction == null)
            {
                return false;
            }

            HashSet<string> visitedIds = new HashSet<string>();
            FactionDefinition current = faction;
            while (current != null)
            {
                if (!visitedIds.Add(current.Id))
                {
                    return true;
                }

                current = current.ParentFaction;
            }

            return false;
        }

        public static bool ShareCommonParent(FactionDefinition left, FactionDefinition right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            return HasSameId(left.ParentFaction, right.ParentFaction);
        }

        public static string GetHierarchyPath(FactionDefinition faction)
        {
            if (faction == null)
            {
                return string.Empty;
            }

            List<FactionDefinition> hierarchy = new List<FactionDefinition> { faction };
            hierarchy.AddRange(GetAncestors(faction));
            hierarchy.Reverse();

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < hierarchy.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(" / ");
                }

                builder.Append(hierarchy[i].DisplayName);
            }

            return builder.ToString();
        }

        public static string GetExpectedCategoryId(FactionKind kind)
        {
            switch (kind)
            {
                case FactionKind.Nation:
                    return "category.faction.nation";
                case FactionKind.Government:
                    return "category.faction.government";
                case FactionKind.Guild:
                    return "category.faction.guild";
                case FactionKind.Company:
                    return "category.faction.company";
                case FactionKind.Military:
                    return "category.faction.military";
                case FactionKind.Religious:
                    return "category.faction.religious";
                case FactionKind.Criminal:
                    return "category.faction.criminal";
                case FactionKind.NobleHouse:
                    return "category.faction.noble-house";
                case FactionKind.SettlementAuthority:
                    return "category.faction.settlement-authority";
                case FactionKind.InformalGroup:
                    return "category.faction.informal";
                default:
                    return string.Empty;
            }
        }
    }
}
