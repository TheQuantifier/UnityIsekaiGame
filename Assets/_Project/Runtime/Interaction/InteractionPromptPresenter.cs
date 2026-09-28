using UnityEngine;
using UnityIsekaiGame.Gameplay;

namespace UnityIsekaiGame.Interaction
{
    public sealed class InteractionPromptPresenter : MonoBehaviour
    {
        [SerializeField] private CameraInteractionDetector detector;
        [SerializeField] private InteractionPromptView promptView;

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

            if (GameUiModalState.IsModalActive)
            {
                promptView.Hide();
                return;
            }

            if (!detector.HasTarget)
            {
                promptView.Hide();
                return;
            }

            promptView.Show(detector.CurrentInteractable.InteractionPrompt);
        }
    }
}
