using UnityEngine;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Interaction;

namespace UnityIsekaiGame.Networking.Client
{
    [DisallowMultipleComponent]
    public sealed class LocalWorldItemPickupBridge : MonoBehaviour
    {
        private void OnEnable()
        {
            NetworkWorldItemPickup.ClientPickupSpawned += AttachClientInteraction;
            NetworkWorldItemPickup[] existing = FindObjectsByType<NetworkWorldItemPickup>(FindObjectsInactive.Exclude);
            for (int i = 0; i < existing.Length; i++) AttachClientInteraction(existing[i]);
        }

        private void OnDisable()
        {
            NetworkWorldItemPickup.ClientPickupSpawned -= AttachClientInteraction;
        }

        private static void AttachClientInteraction(NetworkWorldItemPickup pickup)
        {
            if (pickup == null || !pickup.IsClient || pickup.IsServer) return;
            NetworkWorldItemPickupInteractable interactable = pickup.GetComponent<NetworkWorldItemPickupInteractable>();
            if (interactable == null) interactable = pickup.gameObject.AddComponent<NetworkWorldItemPickupInteractable>();
            interactable.Configure(pickup);
        }
    }

    [DisallowMultipleComponent]
    public sealed class NetworkWorldItemPickupInteractable : MonoBehaviour, IInteractable
    {
        private NetworkWorldItemPickup pickup;

        public string InteractionPrompt => pickup == null ? "Pick up" : pickup.InteractionPrompt;

        public void Configure(NetworkWorldItemPickup replicatedPickup)
        {
            if (pickup != null) pickup.CollectionResultReceived -= OnCollectionResult;
            pickup = replicatedPickup;
            if (pickup != null) pickup.CollectionResultReceived += OnCollectionResult;
        }

        private void OnDestroy()
        {
            if (pickup != null) pickup.CollectionResultReceived -= OnCollectionResult;
        }

        public bool CanInteract(in InteractionContext context)
        {
            return pickup != null && pickup.CanRequestCollection;
        }

        public void Interact(in InteractionContext context)
        {
            pickup?.RequestCollection();
        }

        private static void OnCollectionResult(bool succeeded, string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                GameHudMessageBus.Show(message, succeeded ? GameHudMessageTone.Success : GameHudMessageTone.Warning);
        }
    }
}
