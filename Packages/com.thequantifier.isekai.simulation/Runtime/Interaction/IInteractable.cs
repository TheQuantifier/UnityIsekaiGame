using UnityEngine;

namespace UnityIsekaiGame.Interaction
{
    public interface IInteractable
    {
        string InteractionPrompt { get; }
        bool CanInteract(in InteractionContext context);
        void Interact(in InteractionContext context);
    }
}
