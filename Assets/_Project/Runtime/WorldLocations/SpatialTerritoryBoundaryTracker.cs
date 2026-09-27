using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Places;

namespace UnityIsekaiGame.WorldLocations
{
    /// <summary>Projects the tracked Transform into authored place boundaries without requiring giant trigger colliders.</summary>
    public sealed class SpatialTerritoryBoundaryTracker : MonoBehaviour
    {
        [SerializeField, Min(0.02f)] private float sampleIntervalSeconds = 0.2f;
        [SerializeField] private string sceneKey = "scene.prototype";

        private readonly Dictionary<string, PlaceDefinition> activeBoundaryPlaces = new Dictionary<string, PlaceDefinition>(StringComparer.Ordinal);
        private SpatialTerritoryBoundaryRuntime runtime;
        private CurrentPlaceTracker placeTracker;
        private Func<double> worldTimeProvider;
        private double nextSampleTime;

        public event Action<SpatialTerritoryTransition> BoundaryCrossed;
        public SpatialTerritoryResolution CurrentResolution { get; private set; }
        public float SampleIntervalSeconds => sampleIntervalSeconds;
        public string SceneKey => sceneKey ?? string.Empty;

        public void Configure(SpatialTerritoryBoundaryRuntime boundaryRuntime, CurrentPlaceTracker currentPlaceTracker, string currentSceneKey, Func<double> currentWorldTimeProvider = null)
        {
            runtime = boundaryRuntime;
            placeTracker = currentPlaceTracker;
            sceneKey = string.IsNullOrWhiteSpace(currentSceneKey) ? sceneKey : currentSceneKey.Trim();
            worldTimeProvider = currentWorldTimeProvider;
            SampleNow(isRestoration: true);
        }

        public SpatialTerritoryResolution SampleNow(bool isRestoration = false)
        {
            if (runtime == null) return CurrentResolution;
            SpatialTerritoryResolution previous = CurrentResolution;
            SpatialTerritoryResolution current = runtime.Resolve(transform.position, sceneKey, worldTimeProvider?.Invoke() ?? 0d);
            SynchronizePlaces(current, isRestoration);
            CurrentResolution = current;
            SpatialTerritoryTransition transition = runtime.Compare(previous, current);
            if (!isRestoration && previous != null && (transition.CrossesPoliticalBorder || !string.Equals(previous.PrimaryPlaceId, current.PrimaryPlaceId, StringComparison.Ordinal)))
            {
                BoundaryCrossed?.Invoke(transition);
            }
            return current;
        }

        private void Update()
        {
            if (runtime == null || Time.unscaledTimeAsDouble < nextSampleTime) return;
            nextSampleTime = Time.unscaledTimeAsDouble + Math.Max(0.02d, sampleIntervalSeconds);
            SampleNow();
        }

        private void OnDisable()
        {
            foreach (PlaceDefinition place in activeBoundaryPlaces.Values.OrderByDescending(Depth).ToArray()) placeTracker?.NotifyExited(place);
            activeBoundaryPlaces.Clear();
            CurrentResolution = null;
        }

        private void SynchronizePlaces(SpatialTerritoryResolution resolution, bool isRestoration)
        {
            Dictionary<string, PlaceDefinition> next = (resolution?.ContainingPlaces ?? Array.Empty<PlaceDefinition>())
                .Where(value => value != null && !string.IsNullOrWhiteSpace(value.Id))
                .GroupBy(value => value.Id, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            foreach (PlaceDefinition leaving in activeBoundaryPlaces.Where(pair => !next.ContainsKey(pair.Key)).Select(pair => pair.Value).OrderByDescending(Depth).ToArray())
                placeTracker?.NotifyExited(leaving, isRestoration);
            foreach (PlaceDefinition entering in next.Where(pair => !activeBoundaryPlaces.ContainsKey(pair.Key)).Select(pair => pair.Value).OrderBy(Depth).ToArray())
                placeTracker?.NotifyEntered(entering, isRestoration);
            activeBoundaryPlaces.Clear();
            foreach (KeyValuePair<string, PlaceDefinition> pair in next) activeBoundaryPlaces[pair.Key] = pair.Value;
        }

        private static int Depth(PlaceDefinition place)
        {
            int depth = 0;
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            for (PlaceDefinition cursor = place; cursor != null && visited.Add(cursor.Id); cursor = cursor.ParentPlace) depth++;
            return depth;
        }
    }
}
