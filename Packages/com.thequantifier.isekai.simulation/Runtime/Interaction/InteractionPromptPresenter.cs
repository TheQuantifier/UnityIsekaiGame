#if !ISEKAI_SERVER_PROJECT
using UnityEngine;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Input;

namespace UnityIsekaiGame.Interaction
{
    public sealed class InteractionPromptPresenter : MonoBehaviour
    {
        [SerializeField] private CameraInteractionDetector detector;
        [SerializeField] private InteractionPromptView promptView;

        private IInteractable lastTarget;
        private string lastPrompt = string.Empty;
        private bool wasSuppressed;

        private void Start()
        {
            if (promptView != null)
            {
                promptView.Hide();
            }
        }

        private void LateUpdate()
        {
            if (detector == null || promptView == null)
            {
                return;
            }

            bool suppressed = GameUiModalState.IsModalActive || PlayerCursorMode.HasOpenMenu;
            if (suppressed)
            {
                if (!wasSuppressed || promptView.IsVisible) promptView.Hide();
                wasSuppressed = true;
                lastTarget = null;
                return;
            }
            wasSuppressed = false;

            if (!detector.HasTarget)
            {
                if (lastTarget != null || promptView.IsVisible) promptView.Hide();
                lastTarget = null;
                lastPrompt = string.Empty;
                return;
            }

            IInteractable target = detector.CurrentInteractable;
            string prompt = target?.InteractionPrompt ?? string.Empty;
            if (!ReferenceEquals(target, lastTarget) || !string.Equals(prompt, lastPrompt, System.StringComparison.Ordinal) || !promptView.IsVisible)
            {
                promptView.Show(prompt);
                lastTarget = target;
                lastPrompt = prompt;
            }
        }
    }
}
#endif
