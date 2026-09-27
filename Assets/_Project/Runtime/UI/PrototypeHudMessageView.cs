using UnityEngine;
using UnityEngine.UI;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Presentation;

namespace UnityIsekaiGame.UI
{
    public sealed class PrototypeHudMessageView : MonoBehaviour
    {
        [SerializeField] private Text label;
        [SerializeField, Min(0.1f)] private float messageDuration = 2.5f;

        private float hideAtTime;

        private void Awake()
        {
            if (label == null)
            {
                label = GetComponent<Text>();
            }


            PrototypeUiTheme.StyleText(label, PrototypeUiTextRole.Feedback);
            PrototypeUiTheme.EnsureTextShadow(label, 2f);

            Hide();
        }

        private void OnEnable()
        {
            PrototypeHudMessageBus.MessageRequested += Show;
        }

        private void OnDisable()
        {
            PrototypeHudMessageBus.MessageRequested -= Show;
        }

        private void Update()
        {
            if (label != null && label.enabled && Time.unscaledTime >= hideAtTime)
            {
                Hide();
            }
        }

        public void Show(string message)
        {
            if (label == null)
            {
                return;
            }

            label.text = message;
            label.enabled = true;
            hideAtTime = Time.unscaledTime + messageDuration;
        }

        private void Hide()
        {
            if (label != null)
            {
                label.text = string.Empty;
                label.enabled = false;
            }
        }
    }
}
