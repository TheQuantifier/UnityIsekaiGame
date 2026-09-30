using UnityEngine;
using UnityIsekaiGame.Dialogue;
using UnityIsekaiGame.Input;
using UnityIsekaiGame.Presentation;
using UnityIsekaiGame.PrototypeIntegration;
using UnityIsekaiGame.WorldLocations;

namespace UnityIsekaiGame.Gameplay
{
    public sealed class PrototypeGuildDeskPanel : MonoBehaviour
    {
        private enum DeskKind
        {
            AdventurersGuild,
            MerchantGuild
        }

        private PrototypePersistenceServiceBehaviour services;
        private PlayerInputReader input;
        private DeskKind deskKind;
        private string interactionPointId;
        private string questSourceId;
        private string conversationDefinitionId;
        private string providerPersonId;
        private string locationId;
        private string title;
        private string status;
        private GameObject interactor;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            services = GetComponent<PrototypePersistenceServiceBehaviour>();
        }

        public void OpenAdventurersGuildDesk(string pointId, GameObject interactionOwner)
        {
            Open(
                DeskKind.AdventurersGuild,
                string.IsNullOrWhiteSpace(pointId) ? PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterPointId : pointId,
                PrototypeSceneIntegrationIds.AdventurerGuildCounterSourceId,
                PrototypeConversationDefinitionFactory.AdventurerGuildCounterDefinitionId,
                "person.prototype.adventurers-guild-receptionist",
                "location.prototype.adventurers-guild",
                "Adventurers Guild Desk",
                interactionOwner);
        }

        public void OpenMerchantGuildDesk(string pointId, GameObject interactionOwner)
        {
            Open(
                DeskKind.MerchantGuild,
                string.IsNullOrWhiteSpace(pointId) ? PrototypeInteractionPointDefinitionFactory.MerchantGuildCounterPointId : pointId,
                PrototypeSceneIntegrationIds.MerchantGuildCounterSourceId,
                PrototypeConversationDefinitionFactory.MerchantGuildCounterDefinitionId,
                "person.prototype.merchant-guild-receptionist",
                "location.prototype.merchant-counter",
                "Merchant Guild Desk",
                interactionOwner);
        }

        public void Open(
            string pointId,
            string sourceId,
            string conversationId,
            string providerId,
            string hostLocationId,
            string displayName,
            GameObject interactionOwner)
        {
            if (string.Equals(pointId, PrototypeInteractionPointDefinitionFactory.MerchantGuildCounterPointId, System.StringComparison.Ordinal))
                Open(DeskKind.MerchantGuild, pointId, sourceId, conversationId, providerId, hostLocationId, displayName, interactionOwner);
            else
                Open(DeskKind.AdventurersGuild, pointId, sourceId, conversationId, providerId, hostLocationId, displayName, interactionOwner);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            if (input != null) input.SetMenuInputBlocked(this, false);
            else PlayerCursorMode.SetMenuOpen(this, false);
        }

        private void Open(
            DeskKind kind,
            string pointId,
            string sourceId,
            string conversationId,
            string providerId,
            string hostLocationId,
            string displayName,
            GameObject interactionOwner)
        {
            services ??= GetComponent<PrototypePersistenceServiceBehaviour>();
            if (services == null || !services.OwnsPlayerInteractor(interactionOwner))
            {
                GameHudMessageBus.Show("This desk is not available to that character.");
                return;
            }

            input = interactionOwner == null ? null : interactionOwner.GetComponentInParent<PlayerInputReader>();
            if (input == null || input.GameplayInputBlocked)
            {
                GameHudMessageBus.Show("The interacting character is not available to use this desk.");
                return;
            }

            deskKind = kind;
            interactionPointId = pointId?.Trim() ?? string.Empty;
            questSourceId = sourceId?.Trim() ?? string.Empty;
            conversationDefinitionId = conversationId?.Trim() ?? string.Empty;
            providerPersonId = providerId?.Trim() ?? string.Empty;
            locationId = hostLocationId?.Trim() ?? string.Empty;
            title = kind == DeskKind.MerchantGuild ? "Merchant Guild Desk" : "Adventurers Guild Desk";
            interactor = interactionOwner;
            status = string.Empty;
            IsOpen = true;
            if (input != null) input.SetMenuInputBlocked(this, true, Close);
            else PlayerCursorMode.SetMenuOpen(this, true, Close);
        }

        private void OnGUI()
        {
            if (!IsOpen || services == null) return;

            float width = Mathf.Min(540f, Screen.width - 30f);
            float height = Mathf.Min(480f, Screen.height - 30f);
            Rect window = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GameUiTheme.DrawPanelFrame(window, modal: true);
            GUILayout.BeginArea(new Rect(window.x + 18f, window.y + 16f, window.width - 36f, window.height - 32f));

            GUILayout.BeginHorizontal();
            GUILayout.Label(title, GameUiTheme.TitleStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", GameUiTheme.DangerButtonStyle, GUILayout.Width(90f), GUILayout.Height(34f))) Close();
            GUILayout.EndHorizontal();
            GUILayout.Space(10f);

            GUILayout.BeginVertical(GameUiTheme.CardStyle);
            if (deskKind == DeskKind.AdventurersGuild) DrawAdventurersGuildOptions();
            else DrawMerchantGuildOptions();
            GUILayout.EndVertical();

            if (!string.IsNullOrWhiteSpace(status))
            {
                GUILayout.Space(10f);
                GUILayout.Label(status, GameUiTheme.StatusStyle);
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label("Select a service. Press Escape to close.", GameUiTheme.MutedStyle);
            GUILayout.EndArea();
        }

        private void DrawAdventurersGuildOptions()
        {
            bool registered = services.IsPlayerRegisteredAsAdventurer;
            GUI.enabled = !registered;
            if (GUILayout.Button(registered ? "Registered Adventurer (G Rank)" : "Register as an Adventurer", registered ? GameUiTheme.ButtonStyle : GameUiTheme.PrimaryButtonStyle, GUILayout.Height(42f)))
            {
                PrototypeAdventurerRegistrationResult result = services.RegisterPlayerAsAdventurerAtGuildDesk(interactionPointId);
                status = result.Message;
                GameHudMessageBus.Show(status);
                if (!result.Succeeded) Debug.LogWarning(result.Message);
            }
            GUI.enabled = true;

            if (GUILayout.Button("Browse Guild Quests", GameUiTheme.PrimaryButtonStyle, GUILayout.Height(42f)))
            {
                OpenQuestSource(PrototypeSceneIntegrationIds.AdventurerGuildBoardSourceId, "Adventurers Guild Quests");
            }
            if (GUILayout.Button("Speak with the Receptionist", GameUiTheme.ButtonStyle, GUILayout.Height(42f))) OpenConversation();
        }

        private void DrawMerchantGuildOptions()
        {
            bool registered = services.IsPlayerRegisteredWithMerchantGuild;
            GUI.enabled = !registered;
            if (GUILayout.Button(registered ? "Merchant Guild Membership Active" : "Register with the Merchant Guild", registered ? GameUiTheme.ButtonStyle : GameUiTheme.PrimaryButtonStyle, GUILayout.Height(42f)))
            {
                PrototypeMerchantRegistrationResult result = services.RegisterPlayerWithMerchantGuildAtDesk(interactionPointId);
                status = result.Message;
                GameHudMessageBus.Show(status);
                if (!result.Succeeded) Debug.LogWarning(result.Message);
            }
            GUI.enabled = true;

            if (GUILayout.Button("Browse Delivery Contracts", GameUiTheme.PrimaryButtonStyle, GUILayout.Height(42f)))
            {
                OpenQuestSource(questSourceId, "Merchant Guild Contracts");
            }
            if (GUILayout.Button("Speak with the Receptionist", GameUiTheme.ButtonStyle, GUILayout.Height(42f))) OpenConversation();
        }

        private void OpenQuestSource(string sourceId, string displayName)
        {
            PrototypeQuestSourcePanel panel = FindAnyObjectByType<PrototypeQuestSourcePanel>(FindObjectsInactive.Include);
            if (panel == null)
            {
                status = "Quest services are unavailable.";
                return;
            }

            Close();
            panel.Open(sourceId, interactionPointId, displayName, interactor);
        }

        private void OpenConversation()
        {
            PrototypeDialoguePanel panel = FindAnyObjectByType<PrototypeDialoguePanel>(FindObjectsInactive.Include);
            if (panel == null)
            {
                status = "Receptionist dialogue is unavailable.";
                return;
            }

            Close();
            DialogueFlowOperationResult result = panel.Open(conversationDefinitionId, providerPersonId, locationId, interactionPointId, questSourceId, title, interactor);
            if (!result.Succeeded) GameHudMessageBus.Show(result.Message);
        }

        private void OnDisable() => Close();
    }
}
