using UnityEngine;
using UnityIsekaiGame.Presentation;
using UnityIsekaiGame.Input;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace UnityIsekaiGame.Gameplay
{
    public sealed class PrototypeSocialDebugPanel : MonoBehaviour
    {
        private PrototypePersistenceServiceBehaviour services;
        private PlayerInputReader input;
        private bool visible;
        private string targetPersonId = string.Empty;
        private Rect window = new Rect(20f, 20f, 560f, 380f);
        private Vector2 scroll;

        public void Configure(PrototypePersistenceServiceBehaviour value)
        {
            services = value;
            input = FindAnyObjectByType<PlayerInputReader>(FindObjectsInactive.Include);
        }

        private void Awake()
        {
            input = FindAnyObjectByType<PlayerInputReader>(FindObjectsInactive.Include);
            SetVisible(visible);
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame)
            {
                SetVisible(!visible);
            }
            else if (visible && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                SetVisible(false);
            }
#endif
        }

        private void OnDisable() => SetVisible(false);

        private void OnGUI()
        {
            if (!visible || services == null)
            {
                return;
            }

            window.width = Mathf.Min(window.width, Screen.width - 20f);
            window.height = Mathf.Min(window.height, Screen.height - 20f);
            window = GUI.Window(917214, window, DrawWindow, "SOCIAL STATE  [F8]", GameUiTheme.WindowStyle);
        }

        private void DrawWindow(int id)
        {
            GUILayout.Label("TARGET PERSON ID", GameUiTheme.HeadingStyle);
            GUILayout.Label("Leave blank to inspect the first available NPC.", GameUiTheme.MutedStyle);
            targetPersonId = GUILayout.TextField(targetPersonId ?? string.Empty);
            GUILayout.Space(8f);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label(services.BuildSocialDebugReport(targetPersonId), GameUiTheme.BodyStyle);
            GUILayout.EndScrollView();
            if (GUILayout.Button("Close", GameUiTheme.DangerButtonStyle, GUILayout.Height(32f))) SetVisible(false);
            GUI.DragWindow(new Rect(0f, 0f, window.width, 30f));
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
