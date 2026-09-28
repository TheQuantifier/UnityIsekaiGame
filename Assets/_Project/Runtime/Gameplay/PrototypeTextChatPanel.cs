using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityIsekaiGame.Input;
using UnityIsekaiGame.Presentation;

namespace UnityIsekaiGame.Gameplay
{
    public sealed class PrototypeTextChatPanel : MonoBehaviour
    {
        private const int MaxHistoryEntries = 100;
        private const string InputControlName = "PrototypeTextChatInput";

        private readonly List<string> history = new List<string>();
        private PlayerInputReader input;
        private Vector2 scroll;
        private string draft = string.Empty;
        private bool focusInput;

        public static bool IsChatOpen { get; private set; }
        public static event Action<string> MessageSubmitted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            IsChatOpen = false;
            MessageSubmitted = null;
        }

        private void Awake()
        {
            input = FindAnyObjectByType<PlayerInputReader>(FindObjectsInactive.Include);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.slashKey.wasPressedThisFrame)
            {
                SetOpen(!IsChatOpen);
                return;
            }
        }

        public void SetOpen(bool open)
        {
            if (IsChatOpen == open) return;
            IsChatOpen = open;
            focusInput = open;
            if (open) draft = string.Empty;
            if (input != null) input.SetMenuInputBlocked(this, open, open ? CloseFromCancel : null);
            else PlayerCursorMode.SetMenuOpen(this, open, open ? CloseFromCancel : null);
        }

        private void CloseFromCancel()
        {
            SetOpen(false);
        }

        public void AddSystemMessage(string message)
        {
            AddHistory(string.IsNullOrWhiteSpace(message) ? string.Empty : $"System: {message.Trim()}");
        }

        private void OnGUI()
        {
            if (!IsChatOpen) return;

            Event current = Event.current;
            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Slash) current.Use();

            float width = Mathf.Min(560f, Screen.width - 24f);
            float height = Mathf.Min(300f, Screen.height - 24f);
            Rect panel = new Rect(12f, Screen.height - height - 12f, width, height);
            GameUiTheme.DrawPanelFrame(panel);
            GUILayout.BeginArea(new Rect(panel.x + 14f, panel.y + 12f, panel.width - 28f, panel.height - 24f));

            GUILayout.BeginHorizontal();
            GUILayout.Label("Text Chat", GameUiTheme.HeadingStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label("/ to close", GameUiTheme.MutedStyle);
            GUILayout.EndHorizontal();

            scroll = GUILayout.BeginScrollView(scroll, GameUiTheme.CardStyle, GUILayout.ExpandHeight(true));
            if (history.Count == 0) GUILayout.Label("No messages yet.", GameUiTheme.MutedStyle);
            else foreach (string message in history) GUILayout.Label(message, GameUiTheme.BodyStyle);
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            GUI.SetNextControlName(InputControlName);
            draft = GUILayout.TextField(draft ?? string.Empty, 240, GUILayout.ExpandWidth(true), GUILayout.Height(32f));
            bool submit = GUILayout.Button("Send", GameUiTheme.PrimaryButtonStyle, GUILayout.Width(80f), GUILayout.Height(32f));
            GUILayout.EndHorizontal();

            if (focusInput)
            {
                GUI.FocusControl(InputControlName);
                focusInput = false;
            }

            if (current.type == EventType.KeyDown && (current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter))
            {
                submit = true;
                current.Use();
            }
            if (submit) SubmitDraft();

            GUILayout.EndArea();
        }

        private void SubmitDraft()
        {
            string message = draft?.Trim() ?? string.Empty;
            if (message.Length == 0)
            {
                focusInput = true;
                return;
            }

            AddHistory($"You: {message}");
            MessageSubmitted?.Invoke(message);
            draft = string.Empty;
            focusInput = true;
        }

        private void AddHistory(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            history.Add(message);
            if (history.Count > MaxHistoryEntries) history.RemoveRange(0, history.Count - MaxHistoryEntries);
            scroll.y = float.MaxValue;
        }

        private void OnDisable()
        {
            if (IsChatOpen) SetOpen(false);
        }
    }
}
