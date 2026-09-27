using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityIsekaiGame.Dialogue;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Input;
using UnityIsekaiGame.Quests;

namespace UnityIsekaiGame.Gameplay
{
    public abstract class PrototypeNarrativeModalPanel : MonoBehaviour
    {
        protected PrototypePersistenceServiceBehaviour Services { get; private set; }
        protected PlayerInputReader Input { get; private set; }
        protected bool IsOpen { get; private set; }
        private CursorLockMode priorLockMode;
        private bool priorCursorVisible;

        protected virtual void Awake() => Services = GetComponent<PrototypePersistenceServiceBehaviour>();

        protected virtual void Update()
        {
            if (IsOpen && Keyboard.current?.escapeKey.wasPressedThisFrame == true) Close();
        }

        protected void OpenModal(GameObject interactor)
        {
            if (IsOpen) return;
            Input = interactor == null ? null : interactor.GetComponentInParent<PlayerInputReader>();
            if (Input == null) Input = FindAnyObjectByType<PlayerInputReader>(FindObjectsInactive.Include);
            priorLockMode = Cursor.lockState;
            priorCursorVisible = Cursor.visible;
            IsOpen = true;
            PrototypeGameplayModalState.SetNarrativeActive(true);
            Input?.SetGameplayInputBlocked(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public virtual void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            PrototypeGameplayModalState.SetNarrativeActive(false);
            Input?.SetGameplayInputBlocked(false);
            Cursor.lockState = priorLockMode;
            Cursor.visible = priorCursorVisible;
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
            GUI.Box(window, GUIContent.none);
            GUILayout.BeginArea(new Rect(window.x + 18f, window.y + 16f, window.width - 36f, window.height - 32f));
            GUILayout.BeginHorizontal();
            GUILayout.Label(title, HeaderStyle());
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", GUILayout.Width(90f), GUILayout.Height(30f)))
            {
                Close();
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
                return;
            }
            GUILayout.EndHorizontal();
            GUILayout.Label(status ?? string.Empty, WrapStyle());
            GUILayout.Space(8f);
            scroll = GUILayout.BeginScrollView(scroll);
            if (browseResult?.Listings == null || browseResult.Listings.Count == 0)
            {
                GUILayout.Label("No postings are currently available.");
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
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(definition?.Title ?? visible?.Quest?.QuestDefinitionId ?? "Unknown Quest", SubheaderStyle());
            if (!string.IsNullOrWhiteSpace(definition?.Summary)) GUILayout.Label(definition.Summary, WrapStyle());
            bool canAccept = visible != null && visible.Eligible && !visible.Taken && visible.Listing?.LifecycleState == QuestListingLifecycleState.Published;
            if (!canAccept)
            {
                string reason = visible?.Taken == true
                    ? "Already taken."
                    : string.Join(" ", visible?.Eligibility?.VisibleFailureReasons ?? Array.Empty<string>());
                GUILayout.Label(string.IsNullOrWhiteSpace(reason) ? "Currently unavailable." : reason, WrapStyle());
            }
            GUI.enabled = canAccept;
            if (GUILayout.Button("Accept", GUILayout.Height(32f)))
            {
                QuestSourceOperationResult result = Services.NarrativeCoordinator.AcceptListing(visible.Listing.QuestListingId, interactionPointId);
                status = result.Message;
                PrototypeHudMessageBus.Show(status);
                Refresh();
            }
            GUI.enabled = true;
            GUILayout.EndVertical();
        }

        private static GUIStyle HeaderStyle() => new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
        private static GUIStyle SubheaderStyle() => new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold };
        private static GUIStyle WrapStyle() => new GUIStyle(GUI.skin.label) { wordWrap = true };
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
            GUI.Box(window, GUIContent.none);
            GUILayout.BeginArea(new Rect(window.x + 18f, window.y + 16f, window.width - 36f, window.height - 32f));
            GUILayout.BeginHorizontal();
            GUILayout.Label(speakerName, new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold });
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("End", GUILayout.Width(90f), GUILayout.Height(30f)))
            {
                Close();
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
                return;
            }
            GUILayout.EndHorizontal();
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label(flow.AuthoredText, new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 16 });
            GUILayout.Space(12f);
            foreach (DialogueChoiceSnapshot choice in flow.VisibleChoices.Where(value => value.Evaluation.Selectable))
            {
                if (!GUILayout.Button(choice.DisplayText, GUILayout.MinHeight(38f))) continue;
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
            if (!string.IsNullOrWhiteSpace(status)) GUILayout.Label(status, new GUIStyle(GUI.skin.label) { wordWrap = true });
            GUILayout.EndArea();
        }
    }
}
