using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UnityIsekaiGame.UI.Authentication
{
    /// <summary>
    /// Separates an explicit keyboard submit from InputField.onEndEdit, which also fires when
    /// focus moves to a button. This prevents clicking Create Account from first submitting Login.
    /// </summary>
    public sealed class ExplicitSubmitInputField : InputField
    {
        private int lastSubmittedFrame = -1;

        public event Action Submitted;

        public override void OnUpdateSelected(BaseEventData eventData)
        {
            bool wasFocused = isFocused;
            base.OnUpdateSelected(eventData);

            // A single-line InputField consumes Return while finishing its edit session and does
            // not route that first keypress through OnSubmit. Raise the explicit submit here so
            // login forms react to the first Enter instead of requiring a second one.
            if (wasFocused && WasSubmitPressedThisFrame())
            {
                RaiseSubmittedOncePerFrame();
            }
        }

        public override void OnSubmit(BaseEventData eventData)
        {
            if (!IsActive() || !IsInteractable()) return;
            base.OnSubmit(eventData);
            RaiseSubmittedOncePerFrame();
        }

        private void RaiseSubmittedOncePerFrame()
        {
            if (lastSubmittedFrame == Time.frameCount) return;
            lastSubmittedFrame = Time.frameCount;
            Submitted?.Invoke();
        }

        private static bool WasSubmitPressedThisFrame()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null
                && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame);
        }
    }
}
