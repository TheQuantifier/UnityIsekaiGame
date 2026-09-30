using UnityEngine;
using UnityIsekaiGame.Interaction;

namespace UnityIsekaiGame.Gameplay
{
    public sealed class PrototypeManaSpendInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField, Min(1f)] private float manaCost = 30f;

        public string InteractionPrompt => $"Prototype spend {manaCost:0.#} mana";

        private void OnValidate()
        {
            manaCost = Mathf.Max(1f, manaCost);
        }

        public bool CanInteract(in InteractionContext context)
        {
            return FindMana(context.Interactor) != null;
        }

        public void Interact(in InteractionContext context)
        {
            PlayerMana mana = FindMana(context.Interactor);
            if (mana == null)
            {
                Debug.LogWarning($"{name} could not find PlayerMana on the interactor.");
                return;
            }

            VitalChangeResult result = mana.Spend(manaCost);
            Debug.Log(result.Succeeded
                ? $"Prototype mana spend succeeded. Mana is now {mana.CurrentMana:0.#} / {mana.MaximumMana:0.#}."
                : $"Prototype mana spend failed: {result.Message}");
        }

        private static PlayerMana FindMana(GameObject interactor)
        {
            if (interactor == null)
            {
                return null;
            }

            PlayerMana mana = interactor.GetComponentInParent<PlayerMana>();
            return mana != null ? mana : interactor.GetComponentInChildren<PlayerMana>();
        }
    }
}
