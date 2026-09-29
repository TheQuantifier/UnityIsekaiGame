using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityIsekaiGame.Places
{
    public sealed class CurrentPlaceTracker : MonoBehaviour
    {
        private readonly Dictionary<string, PlaceDefinition> activePlaces = new Dictionary<string, PlaceDefinition>(StringComparer.Ordinal);
        private PlaceDefinition currentPlace;

        public event Action<PlaceDefinition, bool> CurrentPlaceChanged;

        public PlaceDefinition CurrentPlace => currentPlace;
        public string CurrentPlaceId => currentPlace == null ? string.Empty : currentPlace.Id;

        public void NotifyEntered(PlaceDefinition place, bool isRestoration = false)
        {
            if (place == null || string.IsNullOrWhiteSpace(place.Id))
            {
                return;
            }

            activePlaces[place.Id] = place;
            RefreshCurrentPlace(isRestoration);
        }

        public void NotifyExited(PlaceDefinition place, bool isRestoration = false)
        {
            if (place == null || string.IsNullOrWhiteSpace(place.Id))
            {
                return;
            }

            activePlaces.Remove(place.Id);
            RefreshCurrentPlace(isRestoration);
        }

        public void Clear(bool isRestoration = false)
        {
            activePlaces.Clear();
            SetCurrentPlace(null, isRestoration);
        }

        public void ForceCurrentPlace(PlaceDefinition place, bool isRestoration)
        {
            activePlaces.Clear();
            if (place != null && !string.IsNullOrWhiteSpace(place.Id))
            {
                activePlaces[place.Id] = place;
            }

            SetCurrentPlace(place, isRestoration);
        }

        private void RefreshCurrentPlace(bool isRestoration)
        {
            PlaceDefinition deepest = activePlaces.Values
                .OrderByDescending(GetDepth)
                .ThenBy(place => place.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            SetCurrentPlace(deepest, isRestoration);
        }

        private void SetCurrentPlace(PlaceDefinition next, bool isRestoration)
        {
            if (ReferenceEquals(currentPlace, next) || string.Equals(CurrentPlaceId, next == null ? string.Empty : next.Id, StringComparison.Ordinal))
            {
                currentPlace = next;
                return;
            }

            currentPlace = next;
            CurrentPlaceChanged?.Invoke(currentPlace, isRestoration);
        }

        private static int GetDepth(PlaceDefinition place)
        {
            int depth = 0;
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            PlaceDefinition current = place;
            while (current != null && !string.IsNullOrWhiteSpace(current.Id) && visited.Add(current.Id))
            {
                depth++;
                current = current.ParentPlace;
            }

            return depth;
        }
    }
}
