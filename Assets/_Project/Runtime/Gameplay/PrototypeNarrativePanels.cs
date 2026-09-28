using System;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Dialogue;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Input;
using UnityIsekaiGame.Quests;
using UnityIsekaiGame.Presentation;

namespace UnityIsekaiGame.Gameplay
{
    public abstract class PrototypeNarrativeModalPanel : MonoBehaviour
    {
        protected PrototypePersistenceServiceBehaviour Services { get; private set; }
        protected PlayerInputReader Input { get; private set; }
        protected bool IsOpen { get; private set; }

        protected virtual void Awake() => Services = GetComponent<PrototypePersistenceServiceBehaviour>();

        protected void OpenModal(GameObject interactor)
        {
            if (IsOpen) return;
            Input = interactor == null ? null : interactor.GetComponentInParent<PlayerInputReader>();
            if (Input == null) Input = FindAnyObjectByType<PlayerInputReader>(FindObjectsInactive.Include);
            IsOpen = true;
            GameUiModalState.SetNarrativeActive(true);
            if (Input != null) Input.SetMenuInputBlocked(this, true, Close);
            else PlayerCursorMode.SetMenuOpen(this, true, Close);
        }

        public virtual void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            GameUiModalState.SetNarrativeActive(false);
            if (Input != null) Input.SetMenuInputBlocked(this, false);
            else PlayerCursorMode.SetMenuOpen(this, false);
        }

        protected virtual void OnDisable() => Close();
    }

    public sealed class PrototypeQuestSourcePanel : PrototypeNarrativeModalPanel
    {
        private string sourceId;
        private string interactionPointId;
        private string title;
        private string status;
        private Vector2 scroll;
        private QuestSourceBrowseResult browseResult;

        public void Open(string questSourceId, string pointId, string displayName, GameObject interactor)
        {
            if (Services?.NarrativeCoordinator == null) return;
            sourceId = questSourceId?.Trim() ?? string.Empty;
            interactionPointId = pointId?.Trim() ?? string.Empty;
            title = string.IsNullOrWhiteSpace(displayName) ? "Available Quests" : displayName;
            OpenModal(interactor);
            Refresh();
        }

        private void Refresh()
        {
            browseResult = Services?.NarrativeCoordinator?.BrowseSource(sourceId, interactionPointId);
            status = browseResult?.Message ?? "Quest source is unavailable.";
        }

        private void OnGUI()
        {
            if (!IsOpen || Services?.NarrativeCoordinator == null) return;
            float width = Mathf.Min(680f, Screen.width - 30f);
            float height = Mathf.Min(620f, Screen.height - 30f);
            Rect window = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GameUiTheme.DrawPanelFrame(window, modal: true);
            GUILayout.BeginArea(new Rect(window.x + 18f, window.y + 16f, window.width - 36f, window.height - 32f));
            GUILayout.BeginHorizontal();
            GUILayout.Label(title, GameUiTheme.TitleStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", GameUiTheme.DangerButtonStyle, GUILayout.Width(90f), GUILayout.Height(34f)))
            {
                Close();
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
                return;
            }
            GUILayout.EndHorizontal();
            GUILayout.Label(status ?? string.Empty, GameUiTheme.StatusStyle);
            GUILayout.Space(8f);
            scroll = GUILayout.BeginScrollView(scroll);
            if (browseResult?.Listings == null || browseResult.Listings.Count == 0)
            {
                GUILayout.Label("No postings are currently available. Check another quest source or return later.", GameUiTheme.MutedStyle);
            }
            else
            {
                foreach (QuestVisibleListingSnapshot visible in browseResult.Listings)
                {
                    DrawListing(visible);
                    GUILayout.Space(8f);
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawListing(QuestVisibleListingSnapshot visible)
        {
            QuestDefinition definition = null;
            if (visible?.Quest != null) Services.RuntimeDefinitionRegistry.TryGet(visible.Quest.QuestDefinitionId, out definition);
            GUILayout.BeginVertical(GameUiTheme.CardStyle);
            GUILayout.Label(definition?.Title ?? visible?.Quest?.QuestDefinitionId ?? "Unknown Quest", GameUiTheme.HeadingStyle);
            if (!string.IsNullOrWhiteSpace(definition?.Summary)) GUILayout.Label(definition.Summary, GameUiTheme.BodyStyle);
            if (definition != null)
            {
                int currentPartySize = Services.NarrativeCoordinator.Parties.GetPartyForPerson(Services.PlayerPersonId)?.MemberCount ?? 1;
                string requirement = definition.RequiresParty
                    ? $"Requires a party of at least {definition.RequiredPartySize}. Current party: {currentPartySize}."
                    : $"Solo or party quest. Current party: {currentPartySize}.";
                GUILayout.Label(requirement, definition.RequiresParty ? GameUiTheme.StatusStyle : GameUiTheme.MutedStyle);
            }
            bool canAccept = visible != null && visible.Eligible && !visible.Taken && visible.Listing?.LifecycleState == QuestListingLifecycleState.Published;
            if (!canAccept)
            {
                string reason = visible?.Taken == true
                    ? "Already taken."
                    : string.Join(" ", visible?.Eligibility?.VisibleFailureReasons ?? Array.Empty<string>());
                GUILayout.Label(string.IsNullOrWhiteSpace(reason) ? "Currently unavailable." : reason, GameUiTheme.MutedStyle);
            }
            GUI.enabled = canAccept;
            if (GUILayout.Button("Accept Quest", GameUiTheme.PrimaryButtonStyle, GUILayout.Height(36f)))
            {
                QuestSourceOperationResult result = Services.NarrativeCoordinator.AcceptListing(visible.Listing.QuestListingId, interactionPointId);
                status = result.Message;
                PrototypeHudMessageBus.Show(status);
                Refresh();
            }
            GUI.enabled = true;
            GUILayout.EndVertical();
        }

    }

    public sealed class PrototypeDialoguePanel : PrototypeNarrativeModalPanel
    {
        private DialogueFlowSnapshot flow;
        private string speakerName;
        private string status;
        private Vector2 scroll;

        public DialogueFlowOperationResult Open(string conversationDefinitionId, string providerPersonId, string locationId, string interactionPointId, string questSourceId, string displayName, GameObject interactor)
        {
            if (Services?.NarrativeCoordinator == null)
                return DialogueFlowOperationResult.Failure(DialogueFlowOperationStatus.InvalidRequest, "Dialogue runtime is unavailable.", 0L);
            DialogueFlowOperationResult result = Services.NarrativeCoordinator.StartConversation(conversationDefinitionId, providerPersonId, interactionPointId, locationId, questSourceId);
            status = result.Message;
            if (!result.Succeeded) return result;
            flow = result.Snapshot;
            speakerName = string.IsNullOrWhiteSpace(displayName) ? "Conversation" : displayName;
            OpenModal(interactor);
            return result;
        }

        public override void Close()
        {
            if (IsOpen && flow != null && flow.State != DialogueFlowState.Ended)
                Services?.NarrativeCoordinator?.EndDialogue(flow.FlowId);
            flow = null;
            base.Close();
        }

        private void OnGUI()
        {
            if (!IsOpen || flow == null) return;
            float width = Mathf.Min(720f, Screen.width - 30f);
            float height = Mathf.Min(520f, Screen.height - 30f);
            Rect window = new Rect((Screen.width - width) * 0.5f, Screen.height - height - 20f, width, height);
            GameUiTheme.DrawPanelFrame(window, modal: true);
            GUILayout.BeginArea(new Rect(window.x + 18f, window.y + 16f, window.width - 36f, window.height - 32f));
            GUILayout.BeginHorizontal();
            GUILayout.Label(speakerName, GameUiTheme.TitleStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("End", GameUiTheme.DangerButtonStyle, GUILayout.Width(90f), GUILayout.Height(34f)))
            {
                Close();
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
                return;
            }
            GUILayout.EndHorizontal();
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.BeginVertical(GameUiTheme.CardStyle);
            GUILayout.Label(flow.AuthoredText, GameUiTheme.HeadingStyle);
            GUILayout.EndVertical();
            GUILayout.Space(12f);
            foreach (DialogueChoiceSnapshot choice in flow.VisibleChoices)
            {
                bool selectable = choice.Evaluation.Selectable;
                GUI.enabled = selectable;
                bool selected = GUILayout.Button(choice.DisplayText, selectable ? GameUiTheme.PrimaryButtonStyle : GameUiTheme.ButtonStyle, GUILayout.MinHeight(40f));
                GUI.enabled = true;
                if (!selectable && choice.Evaluation.VisibleFailureReasons.Count > 0)
                {
                    GUILayout.Label(string.Join(" ", choice.Evaluation.VisibleFailureReasons), GameUiTheme.MutedStyle);
                }
                if (!selected || !selectable) continue;
                DialogueFlowOperationResult result = Services.NarrativeCoordinator.SelectDialogueChoice(flow.FlowId, choice.ChoiceId);
                status = result.Message;
                if (!result.Succeeded)
                {
                    PrototypeHudMessageBus.Show(status);
                    continue;
                }
                flow = result.Snapshot;
                if (flow == null || flow.State == DialogueFlowState.Ended)
                {
                    Close();
                    break;
                }
            }
            GUILayout.EndScrollView();
            if (!string.IsNullOrWhiteSpace(status)) GUILayout.Label(status, GameUiTheme.StatusStyle);
            GUILayout.EndArea();
        }
    }
}
