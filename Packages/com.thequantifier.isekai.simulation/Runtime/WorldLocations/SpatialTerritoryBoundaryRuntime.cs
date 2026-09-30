using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Governments;
using UnityIsekaiGame.Places;

namespace UnityIsekaiGame.WorldLocations
{
    public sealed class SpatialTerritoryResolution
    {
        public SpatialTerritoryResolution(
            Vector3 worldPosition,
            PlaceDefinition primaryPlace,
            string locationId,
            IEnumerable<PlaceDefinition> containingPlaces,
            IEnumerable<SpatialTerritoryBoundaryDefinition> matchedBoundaries,
            IEnumerable<PoliticalTerritoryRecordData> territoryHierarchy)
        {
            WorldPosition = worldPosition;
            PrimaryPlace = primaryPlace;
            LocationId = N(locationId);
            ContainingPlaces = (containingPlaces ?? Array.Empty<PlaceDefinition>()).Where(value => value != null).Distinct().ToArray();
            MatchedBoundaries = (matchedBoundaries ?? Array.Empty<SpatialTerritoryBoundaryDefinition>()).Where(value => value != null).Distinct().ToArray();
            TerritoryHierarchy = (territoryHierarchy ?? Array.Empty<PoliticalTerritoryRecordData>()).Where(value => value != null).Select(value => value.Clone()).ToArray();
        }

        public Vector3 WorldPosition { get; }
        public PlaceDefinition PrimaryPlace { get; }
        public string PrimaryPlaceId => PrimaryPlace == null ? string.Empty : PrimaryPlace.Id;
        public string LocationId { get; }
        public IReadOnlyList<PlaceDefinition> ContainingPlaces { get; }
        public IReadOnlyList<SpatialTerritoryBoundaryDefinition> MatchedBoundaries { get; }
        public IReadOnlyList<PoliticalTerritoryRecordData> TerritoryHierarchy { get; }
        public PoliticalTerritoryRecordData PrimaryTerritory => TerritoryHierarchy.FirstOrDefault()?.Clone();
        public string PrimaryTerritoryId => TerritoryHierarchy.FirstOrDefault()?.territoryId ?? string.Empty;
        public string PolityId => TerritoryHierarchy.Select(value => value.polityId).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        public bool IsResolved => PrimaryPlace != null;

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public sealed class SpatialTerritoryTransition
    {
        public SpatialTerritoryTransition(
            SpatialTerritoryResolution previous,
            SpatialTerritoryResolution current,
            IEnumerable<string> exitedTerritoryIds,
            IEnumerable<string> enteredTerritoryIds,
            IEnumerable<PoliticalTerritoryCategory> crossedCategories,
            bool crossesNationalBorder)
        {
            Previous = previous;
            Current = current;
            ExitedTerritoryIds = Clean(exitedTerritoryIds);
            EnteredTerritoryIds = Clean(enteredTerritoryIds);
            CrossedCategories = (crossedCategories ?? Array.Empty<PoliticalTerritoryCategory>()).Where(value => value != PoliticalTerritoryCategory.Unknown).Distinct().ToArray();
            CrossesNationalBorder = crossesNationalBorder;
        }

        public SpatialTerritoryResolution Previous { get; }
        public SpatialTerritoryResolution Current { get; }
        public IReadOnlyList<string> ExitedTerritoryIds { get; }
        public IReadOnlyList<string> EnteredTerritoryIds { get; }
        public IReadOnlyList<PoliticalTerritoryCategory> CrossedCategories { get; }
        public bool CrossesNationalBorder { get; }
        public bool CrossesPoliticalBorder => ExitedTerritoryIds.Count > 0 || EnteredTerritoryIds.Count > 0;
        public bool CrossesSettlementBorder => CrossedCategories.Any(value => value is PoliticalTerritoryCategory.City or PoliticalTerritoryCategory.Town or PoliticalTerritoryCategory.Village or PoliticalTerritoryCategory.District);

        private static string[] Clean(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).ToArray();
    }

    /// <summary>Stateless position resolver backed by authored boundary definitions and live government ownership.</summary>
    public sealed class SpatialTerritoryBoundaryRuntime
    {
        private DefinitionRegistry registry;
        private GovernmentRuntime governments;
        private SpatialTerritoryBoundaryDefinition[] boundaries = Array.Empty<SpatialTerritoryBoundaryDefinition>();

        public IReadOnlyList<SpatialTerritoryBoundaryDefinition> Boundaries => boundaries.ToArray();

        public void Configure(DefinitionRegistry definitionRegistry, GovernmentRuntime governmentRuntime)
        {
            registry = definitionRegistry;
            governments = governmentRuntime;
            boundaries = registry?.DefinitionsById.Values
                .OfType<SpatialTerritoryBoundaryDefinition>()
                .Where(value => value != null && value.EnabledForResolution)
                .OrderByDescending(value => value.Priority)
                .ThenBy(value => value.ApproximateHorizontalArea())
                .ThenBy(value => value.Id, StringComparer.Ordinal)
                .ToArray() ?? Array.Empty<SpatialTerritoryBoundaryDefinition>();
        }

        public SpatialTerritoryResolution Resolve(Vector3 worldPosition, string sceneKey, double worldTime = 0d)
        {
            string scene = N(sceneKey);
            SpatialTerritoryBoundaryDefinition[] sceneBoundaries = boundaries
                .Where(value => string.IsNullOrWhiteSpace(value.SceneKey) || string.Equals(value.SceneKey, scene, StringComparison.Ordinal))
                .ToArray();
            List<SpatialTerritoryBoundaryDefinition> included = new List<SpatialTerritoryBoundaryDefinition>();
            foreach (IGrouping<string, SpatialTerritoryBoundaryDefinition> group in sceneBoundaries.GroupBy(value => value.PlaceId, StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(group.Key)) continue;
                bool insideInclude = group.Any(value => value.Operation == SpatialBoundaryOperation.Include && value.Contains(worldPosition));
                bool insideExclusion = group.Any(value => value.Operation == SpatialBoundaryOperation.Exclude && value.Contains(worldPosition));
                if (!insideInclude || insideExclusion) continue;
                included.AddRange(group.Where(value => value.Operation == SpatialBoundaryOperation.Include && value.Contains(worldPosition)));
            }

            SpatialTerritoryBoundaryDefinition primaryBoundary = included
                .OrderByDescending(value => PlaceDepth(value.Place))
                .ThenByDescending(value => value.Priority)
                .ThenBy(value => value.ApproximateHorizontalArea())
                .ThenBy(value => value.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            PlaceDefinition primaryPlace = primaryBoundary?.Place;
            // Only the winning boundary's Place hierarchy is active. Overlapping sibling shapes are
            // candidates, not simultaneous places; priority and specificity decide the winner.
            PlaceDefinition[] places = BuildPlaceHierarchy(primaryPlace).ToArray();
            PoliticalTerritoryRecordData[] territories = ResolveTerritoryHierarchy(places, worldTime);
            return new SpatialTerritoryResolution(worldPosition, primaryPlace, primaryBoundary?.LocationId, places, included, territories);
        }

        public SpatialTerritoryTransition Compare(SpatialTerritoryResolution previous, SpatialTerritoryResolution current)
        {
            string[] before = previous?.TerritoryHierarchy.Select(value => value.territoryId).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToArray() ?? Array.Empty<string>();
            string[] after = current?.TerritoryHierarchy.Select(value => value.territoryId).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToArray() ?? Array.Empty<string>();
            string[] exited = before.Except(after, StringComparer.Ordinal).ToArray();
            string[] entered = after.Except(before, StringComparer.Ordinal).ToArray();
            PoliticalTerritoryCategory[] categories = exited.Concat(entered).Select(TerritoryCategory).Where(value => value != PoliticalTerritoryCategory.Unknown).Distinct().ToArray();
            string oldPolity = previous?.PolityId ?? string.Empty;
            string newPolity = current?.PolityId ?? string.Empty;
            bool national = !string.Equals(oldPolity, newPolity, StringComparison.Ordinal) && (!string.IsNullOrWhiteSpace(oldPolity) || !string.IsNullOrWhiteSpace(newPolity));
            return new SpatialTerritoryTransition(previous, current, exited, entered, categories, national);
        }

        private PoliticalTerritoryRecordData[] ResolveTerritoryHierarchy(IEnumerable<PlaceDefinition> placeHierarchy, double worldTime)
        {
            if (governments == null) return Array.Empty<PoliticalTerritoryRecordData>();
            string[] placeIds = (placeHierarchy ?? Array.Empty<PlaceDefinition>()).Where(value => value != null).Select(value => value.Id).Distinct(StringComparer.Ordinal).ToArray();
            TerritoryPlaceMembershipRecordData membership = governments.TerritoryPlaceMemberships
                .Where(value => placeIds.Contains(value.placeId, StringComparer.Ordinal) && Active(value.effectiveWorldTime, value.endedWorldTime, worldTime))
                .OrderBy(value => Array.IndexOf(placeIds, value.placeId))
                .ThenBy(value => value.membershipId, StringComparer.Ordinal)
                .FirstOrDefault();
            PoliticalTerritoryRecordData direct = null;
            if (membership != null) governments.TryGetTerritory(membership.territoryId, out direct);
            direct ??= governments.Territories
                .Where(value => value.lifecycleState == TerritoryLifecycleState.Active && (value.placeIds ?? Array.Empty<string>()).Any(placeIds.Contains))
                .OrderBy(value => placeIds.Select((id, index) => new { id, index }).Where(entry => value.placeIds.Contains(entry.id, StringComparer.Ordinal)).Select(entry => entry.index).DefaultIfEmpty(int.MaxValue).Min())
                .ThenBy(value => value.territoryId, StringComparer.Ordinal)
                .FirstOrDefault();
            if (direct == null) return Array.Empty<PoliticalTerritoryRecordData>();

            List<PoliticalTerritoryRecordData> result = new List<PoliticalTerritoryRecordData>();
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            PoliticalTerritoryRecordData cursor = direct;
            while (cursor != null && !string.IsNullOrWhiteSpace(cursor.territoryId) && visited.Add(cursor.territoryId))
            {
                result.Add(cursor.Clone());
                if (string.IsNullOrWhiteSpace(cursor.parentTerritoryId) || !governments.TryGetTerritory(cursor.parentTerritoryId, out cursor)) break;
            }
            return result.ToArray();
        }

        private PoliticalTerritoryCategory TerritoryCategory(string territoryId)
        {
            if (governments == null || registry == null || !governments.TryGetTerritory(territoryId, out PoliticalTerritoryRecordData territory)) return PoliticalTerritoryCategory.Unknown;
            return registry.TryGet(territory.territoryDefinitionId, out PoliticalTerritoryDefinition definition) ? definition.Category : PoliticalTerritoryCategory.Unknown;
        }

        private static IEnumerable<PlaceDefinition> BuildPlaceHierarchy(PlaceDefinition place)
        {
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            PlaceDefinition cursor = place;
            while (cursor != null && !string.IsNullOrWhiteSpace(cursor.Id) && visited.Add(cursor.Id))
            {
                yield return cursor;
                cursor = cursor.ParentPlace;
            }
        }

        private static int PlaceDepth(PlaceDefinition place) => BuildPlaceHierarchy(place).Count();
        private static bool Active(double start, double end, double time) => time >= start && (end < 0d || time <= end);
        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
