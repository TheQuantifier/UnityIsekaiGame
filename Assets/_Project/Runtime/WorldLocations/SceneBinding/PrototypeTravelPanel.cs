using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Input;
using UnityIsekaiGame.Presentation;

namespace UnityIsekaiGame.WorldLocations.SceneBinding
{
    public sealed class PrototypeTravelPanel : MonoBehaviour
    {
        [SerializeField] private bool visible;
        private PrototypePersistenceServiceBehaviour persistence;
        private PlayerInputReader input;
        private string status = "Select a destination.";
        private Vector2 scroll;

        private void Awake()
        {
            persistence = GetComponent<PrototypePersistenceServiceBehaviour>();
            input = FindAnyObjectByType<PlayerInputReader>(FindObjectsInactive.Include);
            SetVisible(visible);
        }

        private void Update()
        {
            if (Keyboard.current?.tKey.wasPressedThisFrame == true) SetVisible(!visible);
            else if (visible && Keyboard.current?.escapeKey.wasPressedThisFrame == true) SetVisible(false);
        }

        private void OnDisable() => SetVisible(false);

        private void OnGUI()
        {
            if (!visible || persistence?.WorldLocations == null || persistence.WorldTravel == null) return;
            float width = Mathf.Min(420f, Screen.width - 30f);
            float height = Mathf.Min(580f, Screen.height - 100f);
            Rect window = new Rect(20f, 72f, width, height);
            PrototypeUiTheme.DrawPanelFrame(window);
            GUILayout.BeginArea(new Rect(window.x + 14f, window.y + 14f, window.width - 28f, window.height - 28f));
            GUILayout.BeginHorizontal();
            GUILayout.Label("TRAVEL", PrototypeUiTheme.TitleStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close  [T]", PrototypeUiTheme.DangerButtonStyle, GUILayout.Width(100f), GUILayout.Height(34f)))
            {
                SetVisible(false);
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
                return;
            }
            GUILayout.EndHorizontal();
            EntityLocationReferenceData player = PrototypeEntityLocationFactory.Body(PrototypeEntityLocationFactory.PlayerBodyId, persistence.WorldLocations.WorldId);
            string current = persistence.WorldEntityLocations != null && persistence.WorldEntityLocations.TryGetActivePlacement(player, out EntityPlacementSnapshot placement)
                ? placement.ExactLocationId
                : string.Empty;
            GUILayout.Label($"CURRENT LOCATION\n{(string.IsNullOrWhiteSpace(current) ? "Unknown" : current)}", PrototypeUiTheme.HeadingStyle);
            GUILayout.Label(status, PrototypeUiTheme.StatusStyle);
            GUILayout.Space(6f);
            GUILayout.Label("AVAILABLE DESTINATIONS", PrototypeUiTheme.HeadingStyle);
            scroll = GUILayout.BeginScrollView(scroll);
            WorldSceneBindingRuntime sceneBindings = WorldSceneBindingRuntime.Default;
            foreach (LocationSnapshot destination in persistence.WorldLocations.Snapshots
                         .Where(value => value.Visibility == LocationVisibility.Public
                             && value.LifecycleState == LocationLifecycleState.Active
                             && value.LocationId != current
                             && sceneBindings.CanMaterializeAtLocation(value.LocationId))
                         .OrderBy(value => value.OfficialName, StringComparer.Ordinal))
            {
                if (!GUILayout.Button(destination.OfficialName, PrototypeUiTheme.PrimaryButtonStyle, GUILayout.Height(38f))) continue;
                if (!persistence.PartyTravel.CanTravel(persistence.PlayerPersonId, requireAllReady: true, out string partyTravelMessage))
                {
                    status = partyTravelMessage;
                    PrototypeHudMessageBus.Show(status);
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
                PrototypeHudMessageBus.Show(status);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void SetVisible(bool value)
        {
            visible = value;
            if (input == null) input = FindAnyObjectByType<PlayerInputReader>(FindObjectsInactive.Include);
            if (input != null) input.SetMenuInputBlocked(this, visible);
            else PlayerCursorMode.SetMenuOpen(this, visible);
        }
    }
}
