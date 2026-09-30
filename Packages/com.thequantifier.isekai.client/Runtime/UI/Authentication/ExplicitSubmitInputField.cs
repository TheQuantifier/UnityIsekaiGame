using System;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnityIsekaiGame.UI.Authentication
{
    /// <summary>
    /// Separates an explicit keyboard submit from InputField.onEndEdit, which also fires when
    /// focus moves to a button. This prevents clicking Create Account from first submitting Login.
    /// </summary>
    public sealed class ExplicitSubmitInputField : InputField
    {
        public event Action Submitted;

        public override void OnSubmit(BaseEventData eventData)
        {
            if (!IsActive() || !IsInteractable()) return;
            base.OnSubmit(eventData);
            Submitted?.Invoke();
        }
    }
}
