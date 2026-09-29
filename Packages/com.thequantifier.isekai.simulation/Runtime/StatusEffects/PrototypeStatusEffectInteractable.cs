using UnityEngine;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Interaction;

namespace UnityIsekaiGame.StatusEffects
{
    public sealed class PrototypeStatusEffectInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private StatusEffectDefinition statusEffect;
        [SerializeField] private bool applyToThisObject;
        [SerializeField, Min(0f)] private float durationOverride;

        public string InteractionPrompt => statusEffect == null ? "Apply prototype status" : $"Apply {statusEffect.DisplayName}";

        public bool CanInteract(in InteractionContext context)
        {
            return statusEffect != null && FindReceiver(context) != null;
        }

        public void Interact(in InteractionContext context)
        {
            IStatusEffectReceiver receiver = FindReceiver(context);
            if (receiver == null)
            {
                GameHudMessageBus.Show("No status receiver");
                return;
            }

            StatusEffectApplicationRequest request = new StatusEffectApplicationRequest(
                statusEffect,
                context.Interactor,
                $"interactable.{name}",
                durationOverride,
                string.Empty,
                Time.time);
            StatusApplicationResult result = receiver.ApplyStatus(request);
            GameHudMessageBus.Show(result.Message);
            Debug.Log(result.Message);
        }

        private IStatusEffectReceiver FindReceiver(in InteractionContext context)
        {
            GameObject target = applyToThisObject ? gameObject : context.Interactor;
            return target == null ? null : target.GetComponentInParent<IStatusEffectReceiver>();
        }
    }
}
