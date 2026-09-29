using UnityEngine;
using UnityIsekaiGame.Interaction;

namespace UnityIsekaiGame.Gameplay
{
    public sealed class PrototypeDamageInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField, Min(1)] private int damageAmount = 30;

        public string InteractionPrompt => $"Prototype damage -{damageAmount} health";

        private void OnValidate()
        {
            damageAmount = Mathf.Max(1, damageAmount);
        }

        public bool CanInteract(in InteractionContext context)
        {
            return FindHealth(context.Interactor) != null;
        }

        public void Interact(in InteractionContext context)
        {
            PlayerHealth health = FindHealth(context.Interactor);
            if (health == null)
            {
                Debug.LogWarning($"{name} could not find PlayerHealth on the interactor.");
                return;
            }

            int damageApplied = health.Damage(damageAmount);
            Debug.Log($"Prototype damage applied: {damageApplied}. Health is now {health.CurrentHealth} / {health.MaximumHealth}.");
        }

        private static PlayerHealth FindHealth(GameObject interactor)
        {
            if (interactor == null)
            {
                return null;
            }

            PlayerHealth health = interactor.GetComponentInParent<PlayerHealth>();
            return health != null ? health : interactor.GetComponentInChildren<PlayerHealth>();
        }
    }
}
