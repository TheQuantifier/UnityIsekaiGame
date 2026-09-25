using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace UnityIsekaiGame.Gameplay
{
    public sealed class PrototypeSocialDebugPanel : MonoBehaviour
    {
        private PrototypePersistenceServiceBehaviour services;
        private bool visible;
        private string targetPersonId = string.Empty;
        private Rect window = new Rect(20f, 20f, 520f, 245f);

        public void Configure(PrototypePersistenceServiceBehaviour value) => services = value;

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame)
            {
                visible = !visible;
            }
#endif
        }

        private void OnGUI()
        {
            if (!visible || services == null)
            {
                return;
            }

            window = GUI.Window(917214, window, DrawWindow, "Social State (F8)");
        }

        private void DrawWindow(int id)
        {
            GUILayout.Label("Target Person ID (blank selects the first NPC)");
            targetPersonId = GUILayout.TextField(targetPersonId ?? string.Empty);
            GUILayout.Space(4f);
            GUILayout.Label(services.BuildSocialDebugReport(targetPersonId));
            GUI.DragWindow(new Rect(0f, 0f, window.width, 24f));
        }
    }
}
