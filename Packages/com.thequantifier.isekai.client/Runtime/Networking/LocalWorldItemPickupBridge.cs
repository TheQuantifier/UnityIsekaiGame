using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Interaction;

namespace UnityIsekaiGame.Networking.Client
{
    [DisallowMultipleComponent]
    public sealed class LocalWorldItemPickupBridge : MonoBehaviour
    {
        private const float MaximumLegacyMatchDistance = 1f;

        private readonly List<LegacyPickupPresentation> legacyPickups = new List<LegacyPickupPresentation>();
        private readonly List<ActivePickupPresentation> activePresentations = new List<ActivePickupPresentation>();
        private LocalGameClient client;
        private DefinitionRegistry definitions;
        private bool authorityActive;

        private void OnEnable()
        {
            client = GetComponent<LocalGameClient>();
            ResolveDefinitions();
            if (client != null)
            {
                client.LocalPlayerActorChanged += OnLocalPlayerActorChanged;
                OnLocalPlayerActorChanged(client.LocalPlayerActor);
            }

            NetworkWorldItemPickup.ClientPickupSpawned += OnClientPickupSpawned;
            NetworkWorldItemPickup[] existing = FindObjectsByType<NetworkWorldItemPickup>(FindObjectsInactive.Exclude);
            for (int i = 0; i < existing.Length; i++) OnClientPickupSpawned(existing[i]);
        }

        private void OnDisable()
        {
            NetworkWorldItemPickup.ClientPickupSpawned -= OnClientPickupSpawned;
            if (client != null) client.LocalPlayerActorChanged -= OnLocalPlayerActorChanged;
            authorityActive = false;
            ClearActivePresentations(restoreDefaultRenderers: true);
            RestoreLegacyPickups();
        }

        private void LateUpdate()
        {
            for (int i = activePresentations.Count - 1; i >= 0; i--)
            {
                ActivePickupPresentation presentation = activePresentations[i];
                if (presentation.Pickup != null && presentation.Pickup.IsSpawned)
                {
                    continue;
                }

                presentation.Release(authorityActive);
                activePresentations.RemoveAt(i);
            }
        }

        private void OnLocalPlayerActorChanged(NetworkPlayerActor actor)
        {
            if (actor == null)
            {
                authorityActive = false;
                ClearActivePresentations(restoreDefaultRenderers: true);
                RestoreLegacyPickups();
                return;
            }

            authorityActive = true;
            ClearActivePresentations(restoreDefaultRenderers: true);
            RestoreLegacyPickups();
            CaptureLegacyPickups();

            NetworkWorldItemPickup[] replicated = FindObjectsByType<NetworkWorldItemPickup>(FindObjectsInactive.Exclude);
            for (int i = 0; i < replicated.Length; i++)
            {
                OnClientPickupSpawned(replicated[i]);
            }

            for (int i = 0; i < legacyPickups.Count; i++)
            {
                LegacyPickupPresentation legacy = legacyPickups[i];
                if (!legacy.Claimed && legacy.Pickup != null)
                {
                    legacy.Pickup.gameObject.SetActive(false);
                }
            }
        }

        private void CaptureLegacyPickups()
        {
            WorldItemPickup[] scenePickups = FindObjectsByType<WorldItemPickup>(FindObjectsInactive.Exclude);
            for (int i = 0; i < scenePickups.Length; i++)
            {
                WorldItemPickup pickup = scenePickups[i];
                if (pickup == null || pickup.GetComponentInParent<NetworkWorldItemPickup>() != null)
                {
                    continue;
                }

                var legacy = new LegacyPickupPresentation(pickup);
                legacy.DisableLocalAuthority();
                legacyPickups.Add(legacy);
            }
        }

        private void RestoreLegacyPickups()
        {
            for (int i = 0; i < legacyPickups.Count; i++)
            {
                legacyPickups[i].Restore();
            }

            legacyPickups.Clear();
        }

        private void OnClientPickupSpawned(NetworkWorldItemPickup pickup)
        {
            if (pickup == null || !pickup.IsClient || pickup.IsServer) return;
            NetworkWorldItemPickupInteractable interactable = pickup.GetComponent<NetworkWorldItemPickupInteractable>();
            if (interactable == null) interactable = pickup.gameObject.AddComponent<NetworkWorldItemPickupInteractable>();
            interactable.Configure(pickup);

            if (!authorityActive || FindActivePresentation(pickup) != null)
            {
                return;
            }

            RendererState[] defaultRenderers = CaptureDefaultRenderers(pickup);
            LegacyPickupPresentation legacy = FindLegacyPresentation(pickup);
            GameObject runtimeVisual = null;
            bool hasPresentation = legacy != null;
            if (legacy != null)
            {
                legacy.Claimed = true;
                legacy.Pickup.gameObject.SetActive(true);
            }
            else if (TryResolveItem(pickup.DefinitionId, out ItemDefinition item))
            {
                hasPresentation = WorldItemPickupVisualFactory.TryCreate(item, pickup.transform, out runtimeVisual);
            }

            if (!hasPresentation)
            {
                RestoreRenderers(defaultRenderers);
                Debug.LogWarning(
                    $"[World Items] '{pickup.DefinitionId}' has no authored pickup presentation; using the network fallback marker.",
                    pickup);
                return;
            }

            activePresentations.Add(new ActivePickupPresentation(pickup, legacy, runtimeVisual, defaultRenderers));
        }

        private ActivePickupPresentation FindActivePresentation(NetworkWorldItemPickup pickup)
        {
            for (int i = 0; i < activePresentations.Count; i++)
            {
                if (activePresentations[i].Pickup == pickup)
                {
                    return activePresentations[i];
                }
            }

            return null;
        }

        private LegacyPickupPresentation FindLegacyPresentation(NetworkWorldItemPickup pickup)
        {
            string definitionId = pickup.DefinitionId;
            float maximumDistanceSquared = MaximumLegacyMatchDistance * MaximumLegacyMatchDistance;
            LegacyPickupPresentation nearest = null;
            float nearestDistanceSquared = maximumDistanceSquared;
            for (int i = 0; i < legacyPickups.Count; i++)
            {
                LegacyPickupPresentation candidate = legacyPickups[i];
                if (candidate.Claimed || candidate.Pickup == null || candidate.Pickup.Item == null ||
                    !string.Equals(candidate.Pickup.Item.Id, definitionId, StringComparison.Ordinal))
                {
                    continue;
                }

                float distanceSquared = (candidate.Pickup.transform.position - pickup.transform.position).sqrMagnitude;
                if (distanceSquared <= nearestDistanceSquared)
                {
                    nearest = candidate;
                    nearestDistanceSquared = distanceSquared;
                }
            }

            return nearest;
        }

        private bool TryResolveItem(string definitionId, out ItemDefinition item)
        {
            item = null;
            ResolveDefinitions();
            return definitions != null && definitions.TryGet(definitionId, out item);
        }

        private void ResolveDefinitions()
        {
            if (definitions != null)
            {
                return;
            }

            PrototypePersistenceServiceBehaviour persistence =
                FindAnyObjectByType<PrototypePersistenceServiceBehaviour>(FindObjectsInactive.Include);
            definitions = persistence?.DefinitionCatalog?.CreateRegistry();
        }

        private void ClearActivePresentations(bool restoreDefaultRenderers)
        {
            for (int i = activePresentations.Count - 1; i >= 0; i--)
            {
                ActivePickupPresentation presentation = activePresentations[i];
                if (restoreDefaultRenderers)
                {
                    presentation.RestoreDefaultRenderers();
                }

                presentation.Release(hideLegacy: false);
            }

            activePresentations.Clear();
        }

        private static RendererState[] CaptureDefaultRenderers(NetworkWorldItemPickup pickup)
        {
            Renderer[] renderers = pickup.GetComponents<Renderer>();
            var states = new RendererState[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                states[i] = new RendererState(renderers[i], renderers[i].enabled);
                renderers[i].enabled = false;
            }

            return states;
        }

        private static void RestoreRenderers(IReadOnlyList<RendererState> renderers)
        {
            for (int i = 0; i < renderers.Count; i++)
            {
                renderers[i].Restore();
            }
        }

        private sealed class LegacyPickupPresentation
        {
            private readonly bool originalActive;
            private readonly bool originalPickupEnabled;
            private readonly ColliderState[] colliders;

            public LegacyPickupPresentation(WorldItemPickup pickup)
            {
                Pickup = pickup;
                originalActive = pickup.gameObject.activeSelf;
                originalPickupEnabled = pickup.enabled;
                Collider[] found = pickup.GetComponentsInChildren<Collider>(true);
                colliders = new ColliderState[found.Length];
                for (int i = 0; i < found.Length; i++)
                {
                    colliders[i] = new ColliderState(found[i], found[i].enabled);
                }
            }

            public WorldItemPickup Pickup { get; }
            public bool Claimed { get; set; }

            public void DisableLocalAuthority()
            {
                if (Pickup == null) return;
                Pickup.enabled = false;
                for (int i = 0; i < colliders.Length; i++) colliders[i].SetEnabled(false);
            }

            public void Restore()
            {
                if (Pickup == null) return;
                Pickup.gameObject.SetActive(originalActive);
                Pickup.enabled = originalPickupEnabled;
                for (int i = 0; i < colliders.Length; i++) colliders[i].Restore();
                Claimed = false;
            }
        }

        private sealed class ActivePickupPresentation
        {
            private readonly LegacyPickupPresentation legacy;
            private readonly GameObject runtimeVisual;
            private readonly RendererState[] defaultRenderers;

            public ActivePickupPresentation(
                NetworkWorldItemPickup pickup,
                LegacyPickupPresentation legacyPresentation,
                GameObject runtimePresentation,
                RendererState[] fallbackRenderers)
            {
                Pickup = pickup;
                legacy = legacyPresentation;
                runtimeVisual = runtimePresentation;
                defaultRenderers = fallbackRenderers;
            }

            public NetworkWorldItemPickup Pickup { get; }

            public void RestoreDefaultRenderers() => RestoreRenderers(defaultRenderers);

            public void Release(bool hideLegacy)
            {
                if (runtimeVisual != null) UnityEngine.Object.Destroy(runtimeVisual);
                if (legacy == null) return;
                legacy.Claimed = false;
                if (hideLegacy && legacy.Pickup != null) legacy.Pickup.gameObject.SetActive(false);
            }
        }

        private readonly struct RendererState
        {
            private readonly Renderer renderer;
            private readonly bool enabled;

            public RendererState(Renderer target, bool wasEnabled)
            {
                renderer = target;
                enabled = wasEnabled;
            }

            public void Restore()
            {
                if (renderer != null) renderer.enabled = enabled;
            }
        }

        private readonly struct ColliderState
        {
            private readonly Collider collider;
            private readonly bool enabled;

            public ColliderState(Collider target, bool wasEnabled)
            {
                collider = target;
                enabled = wasEnabled;
            }

            public void SetEnabled(bool value)
            {
                if (collider != null) collider.enabled = value;
            }

            public void Restore() => SetEnabled(enabled);
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
