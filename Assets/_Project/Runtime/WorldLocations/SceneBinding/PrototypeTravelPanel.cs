using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityIsekaiGame.Gameplay;

namespace UnityIsekaiGame.WorldLocations.SceneBinding
{
    public sealed class PrototypeTravelPanel : MonoBehaviour
    {
        [SerializeField] private bool visible;
        private PrototypePersistenceServiceBehaviour persistence;
        private string status = "Select a destination.";
        private Vector2 scroll;

        private void Awake()
        {
            persistence = GetComponent<PrototypePersistenceServiceBehaviour>();
        }

        private void Update()
        {
            if (Keyboard.current?.tKey.wasPressedThisFrame == true) visible = !visible;
        }

        private void OnGUI()
        {
            if (!visible || persistence?.WorldLocations == null || persistence.WorldTravel == null) return;
            GUILayout.BeginArea(new Rect(20f, 80f, 360f, 520f), GUI.skin.window);
            GUILayout.Label("Travel (T to close)");
            EntityLocationReferenceData player = PrototypeEntityLocationFactory.Body(PrototypeEntityLocationFactory.PlayerBodyId, persistence.WorldLocations.WorldId);
            string current = persistence.WorldEntityLocations != null && persistence.WorldEntityLocations.TryGetActivePlacement(player, out EntityPlacementSnapshot placement)
                ? placement.ExactLocationId
                : string.Empty;
            GUILayout.Label($"Current: {current}");
            GUILayout.Label(status);
            scroll = GUILayout.BeginScrollView(scroll);
            WorldSceneBindingRuntime sceneBindings = WorldSceneBindingRuntime.Default;
            foreach (LocationSnapshot destination in persistence.WorldLocations.Snapshots
                         .Where(value => value.Visibility == LocationVisibility.Public
                             && value.LifecycleState == LocationLifecycleState.Active
                             && value.LocationId != current
                             && sceneBindings.CanMaterializeAtLocation(value.LocationId))
                         .OrderBy(value => value.OfficialName, StringComparer.Ordinal))
            {
                if (!GUILayout.Button(destination.OfficialName)) continue;
                double worldTime = persistence.PlayTime?.CumulativeSeconds ?? Time.unscaledTimeAsDouble;
                WorldTravelResult result = persistence.WorldTravel.StartTravel(new WorldTravelRequest
                {
                    Traveler = player,
                    TravelerPersonId = PrototypeEntityLocationFactory.PlayerPersonId,
                    DestinationLocationId = destination.LocationId,
                    TravelModeDefinitionId = PrototypeLocationRouteDefinitionFactory.WalkingModeDefinitionId,
                    AccessContext = persistence.BuildWorldLocationAccessContext(player, worldTime),
                    LegalComplianceMode = TravelLegalComplianceMode.RequireLegalTravel,
                    WorldTime = worldTime
                });
                status = result.Message;
                PrototypeHudMessageBus.Show(status);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
