using UnityEngine;
using UnityEngine.UI;

namespace UnityIsekaiGame.Interaction
{
    public sealed class InteractionPromptView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Text promptText;

        public bool IsVisible => canvasGroup != null ? canvasGroup.alpha > 0.5f : gameObject.activeSelf;
        public string DisplayedPrompt => promptText == null ? string.Empty : promptText.text;

        private void Awake()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }
        }

        public void Show(string prompt)
        {
            if (promptText != null)
            {
                promptText.text = prompt;
            }

            SetVisible(true);
        }

        public void Hide()
        {
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup == null)
            {
                gameObject.SetActive(visible);
                return;
            }

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }
    }
}
