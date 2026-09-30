using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Input;
using UnityIsekaiGame.Presentation;

namespace UnityIsekaiGame.WorldLocations.SceneBinding
{
    public sealed class PrototypeTravelPanel : MonoBehaviour
    {
        private const float DestinationRefreshIntervalSeconds = 1f;

        [SerializeField] private bool visible;
        private PrototypePersistenceServiceBehaviour persistence;
        private PlayerInputReader input;
        private string status = "Select a destination.";
        private Vector2 scroll;
        private readonly List<LocationSnapshot> visibleDestinations = new List<LocationSnapshot>();
        private string currentLocationId = string.Empty;
        private float nextDestinationRefreshAt;

        private void Awake()
        {
            persistence = GetComponent<PrototypePersistenceServiceBehaviour>();
            input = persistence == null ? null : persistence.PlayerInput;
            SetVisible(visible);
        }

        private void Update()
        {
            if (input != null && input.TravelTogglePressedThisFrame) SetVisible(!visible);

            if (visible && Time.unscaledTime >= nextDestinationRefreshAt)
            {
                RefreshDestinations();
            }
        }

        private void OnDisable() => SetVisible(false);

        private void OnGUI()
        {
            if (!visible || persistence?.WorldLocations == null || persistence.WorldTravel == null) return;
            float width = Mathf.Min(420f, Screen.width - 30f);
            float height = Mathf.Min(580f, Screen.height - 100f);
            Rect window = new Rect(20f, 72f, width, height);
            GameUiTheme.DrawPanelFrame(window);
            GUILayout.BeginArea(new Rect(window.x + 14f, window.y + 14f, window.width - 28f, window.height - 28f));
            GUILayout.BeginHorizontal();
            GUILayout.Label("TRAVEL", GameUiTheme.TitleStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close  [T]", GameUiTheme.DangerButtonStyle, GUILayout.Width(100f), GUILayout.Height(34f)))
            {
                SetVisible(false);
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
                return;
            }
            GUILayout.EndHorizontal();
            EntityLocationReferenceData player = PrototypeEntityLocationFactory.Body(PrototypeEntityLocationFactory.PlayerBodyId, persistence.WorldLocations.WorldId);
            GUILayout.Label($"CURRENT LOCATION\n{(string.IsNullOrWhiteSpace(currentLocationId) ? "Unknown" : currentLocationId)}", GameUiTheme.HeadingStyle);
            GUILayout.Label(status, GameUiTheme.StatusStyle);
            GUILayout.Space(6f);
            GUILayout.Label("AVAILABLE DESTINATIONS", GameUiTheme.HeadingStyle);
            scroll = GUILayout.BeginScrollView(scroll);
            for (int i = 0; i < visibleDestinations.Count; i++)
            {
                LocationSnapshot destination = visibleDestinations[i];
                if (!GUILayout.Button(destination.OfficialName, GameUiTheme.PrimaryButtonStyle, GUILayout.Height(38f))) continue;
                if (!persistence.PartyTravel.CanTravel(persistence.PlayerPersonId, requireAllReady: true, out string partyTravelMessage))
                {
                    status = partyTravelMessage;
                    GameHudMessageBus.Show(status);
                    continue;
                }
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
                GameHudMessageBus.Show(status);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void SetVisible(bool value)
        {
            visible = value;
            if (visible)
            {
                RefreshDestinations();
            }
            if (input == null && persistence != null) input = persistence.PlayerInput;
            if (input != null) input.SetMenuInputBlocked(this, visible, visible ? CloseFromCancel : null);
            else PlayerCursorMode.SetMenuOpen(this, visible, visible ? CloseFromCancel : null);
        }

        private void CloseFromCancel()
        {
            SetVisible(false);
        }

        private void RefreshDestinations()
        {
            nextDestinationRefreshAt = Time.unscaledTime + DestinationRefreshIntervalSeconds;
            visibleDestinations.Clear();
            currentLocationId = string.Empty;
            if (persistence?.WorldLocations == null) return;

            EntityLocationReferenceData player = PrototypeEntityLocationFactory.Body(PrototypeEntityLocationFactory.PlayerBodyId, persistence.WorldLocations.WorldId);
            if (persistence.WorldEntityLocations != null
                && persistence.WorldEntityLocations.TryGetActivePlacement(player, out EntityPlacementSnapshot placement))
            {
                currentLocationId = placement.ExactLocationId;
            }

            WorldSceneBindingRuntime sceneBindings = WorldSceneBindingRuntime.Default;
            IReadOnlyList<LocationSnapshot> snapshots = persistence.WorldLocations.Snapshots;
            for (int i = 0; i < snapshots.Count; i++)
            {
                LocationSnapshot destination = snapshots[i];
                if (destination.Visibility == LocationVisibility.Public
                    && destination.LifecycleState == LocationLifecycleState.Active
                    && !string.Equals(destination.LocationId, currentLocationId, StringComparison.Ordinal)
                    && sceneBindings.CanMaterializeAtLocation(destination.LocationId))
                {
                    visibleDestinations.Add(destination);
                }
            }

            visibleDestinations.Sort((left, right) => string.Compare(left.OfficialName, right.OfficialName, StringComparison.Ordinal));
        }
    }
}
