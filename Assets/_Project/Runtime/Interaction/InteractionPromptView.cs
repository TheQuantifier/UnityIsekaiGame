using UnityEngine;
using UnityEngine.UI;
using UnityIsekaiGame.Presentation;

namespace UnityIsekaiGame.Interaction
{
    public sealed class InteractionPromptView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image panelImage;
        [SerializeField] private Text inputLabel;
        [SerializeField] private Text promptText;

        private bool visible;
        private string displayedPrompt = string.Empty;
        private string displayedInputHint = string.Empty;

        public bool IsVisible => visible;
        public string DisplayedPrompt => displayedPrompt;

        private void Awake()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            ApplyTheme();
            visible = canvasGroup != null ? canvasGroup.alpha > 0.5f : gameObject.activeSelf;
        }

        public void Configure(CanvasGroup group, Image panel, Text shortcut, Text prompt)
        {
            canvasGroup = group;
            panelImage = panel;
            inputLabel = shortcut;
            promptText = prompt;
            ApplyTheme();
            visible = true;
            Hide();
        }

        public void Show(string prompt) => Show(prompt, "[E]");

        public void Show(string prompt, string inputHint)
        {
            prompt ??= string.Empty;
            inputHint ??= string.Empty;
            if (!string.Equals(displayedPrompt, prompt, System.StringComparison.Ordinal))
            {
                displayedPrompt = prompt;
                if (promptText != null) promptText.text = prompt;
            }
            if (!string.Equals(displayedInputHint, inputHint, System.StringComparison.Ordinal))
            {
                displayedInputHint = inputHint;
                if (inputLabel != null) inputLabel.text = inputHint;
            }

            SetVisible(true);
        }

        public void Hide()
        {
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            bool canvasMatches = canvasGroup == null || (canvasGroup.alpha > 0.5f) == visible;
            if (this.visible == visible && canvasMatches)
            {
                return;
            }

            this.visible = visible;
            if (canvasGroup == null)
            {
                gameObject.SetActive(visible);
                return;
            }

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        private void ApplyTheme()
        {
            GameUiTheme.StylePanel(panelImage, raised: true);
            GameUiTheme.StyleText(inputLabel, GameUiTextRole.Title);
            GameUiTheme.StyleText(promptText, GameUiTextRole.Heading);
            GameUiTheme.EnsureTextShadow(inputLabel, 1f);
            GameUiTheme.EnsureTextShadow(promptText, 1f);
            if (panelImage != null) panelImage.raycastTarget = false;
            if (inputLabel != null) inputLabel.raycastTarget = false;
            if (promptText != null) promptText.raycastTarget = false;
        }
    }
}
