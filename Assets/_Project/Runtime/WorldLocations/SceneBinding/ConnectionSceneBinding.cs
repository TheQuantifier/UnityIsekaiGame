using System;
using UnityEngine;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Interaction;

namespace UnityIsekaiGame.WorldLocations.SceneBinding
{
    public sealed class ConnectionSceneBinding : WorldSceneBindingComponent, IInteractable
    {
        [SerializeField] private Collider stateControlledCollider;
        [SerializeField] private bool colliderBlocksWhenClosed = true;
        [SerializeField] private bool interactTogglesOpenClosed;
        [SerializeField, Min(0.1f)] private float interactionRange = 3f;
        [SerializeField] private bool requirePhysicalRange = true;
        [SerializeField] private string sourceLocationId;
        [SerializeField] private string destinationLocationId;

        public override WorldSceneBindingCategory Category => WorldSceneBindingCategory.Connection;
        public string InteractionPrompt => LastConnection != null ? $"{(LastConnection.OpenState == LocationConnectionOpenState.Open ? "Close" : "Open")} {DisplayName}" : $"Use {DisplayName}";
        public LocationConnectionSnapshot LastConnection { get; private set; }
        public LocationConnectionOperationResult LastInteractionResult { get; private set; }
        public bool InteractionEnabled => interactTogglesOpenClosed;
        public float InteractionRange => interactionRange;

        public void ConfigureConnection(string connectionId, string sceneBindingKey, string sourceLocation, string destinationLocation, string scene, string world, Collider controlledCollider = null, bool requiredBinding = false, bool allowInteractionToggle = false, string authoredDisplayName = null)
        {
            ConfigureBinding(connectionId, sceneBindingKey, scene, world, WorldSceneBindingRole.Primary, requiredBinding, authoredDisplayName);
            sourceLocationId = N(sourceLocation);
            destinationLocationId = N(destinationLocation);
            stateControlledCollider = controlledCollider;
            interactTogglesOpenClosed = allowInteractionToggle;
        }

        public void ConfigureInteractionRange(float range = 3f, bool enforcePhysicalRange = true)
        {
            interactionRange = Mathf.Max(0.1f, range);
            requirePhysicalRange = enforcePhysicalRange;
        }

        public override void SyncFromAuthoritative(WorldSceneBindingRuntime bindingRuntime, bool initialSync)
        {
            if (!bindingRuntime.TryGetConnection(LogicalId, out LocationConnectionSnapshot connection))
            {
                return;
            }

            LastConnection = connection;
            Collider controlled = stateControlledCollider != null ? stateControlledCollider : GetComponent<Collider>();
            if (controlled != null && colliderBlocksWhenClosed)
            {
                bool closed = connection.OpenState == LocationConnectionOpenState.Closed || connection.BlockageState != LocationConnectionBlockageState.Clear;
                controlled.enabled = closed;
            }
        }

        public bool CanInteract(in InteractionContext context)
        {
            return interactTogglesOpenClosed
                && Status == WorldSceneBindingStatus.Bound
                && IsWithinPhysicalRange(context)
                && Runtime.TryGetConnection(LogicalId, out _);
        }

        public void Interact(in InteractionContext context)
        {
            if (!interactTogglesOpenClosed || !Runtime.TryGetConnection(LogicalId, out LocationConnectionSnapshot connection))
            {
                return;
            }

            LocationConnectionOpenState next = connection.OpenState == LocationConnectionOpenState.Open ? LocationConnectionOpenState.Closed : LocationConnectionOpenState.Open;
            LocationConnectionOperationResult result = Runtime.RequestConnectionOpenState($"scene-binding.toggle.{LogicalId}.{Guid.NewGuid():N}", LogicalId, next, null, null, 0d, false);
            LastInteractionResult = result;
            if (result.Succeeded)
            {
                SyncFromAuthoritative(Runtime, false);
            }

            PrototypeHudMessageBus.Show(result.Message);
        }

        private bool IsWithinPhysicalRange(in InteractionContext context)
        {
            if (!requirePhysicalRange || context.Origin == null)
            {
                return true;
            }

            Vector3 target = context.Hit.collider != null ? context.Hit.point : BindingTransform.position;
            return Vector3.Distance(context.Origin.position, target) <= Mathf.Max(0.1f, interactionRange);
        }

        public SceneBindingTransitionResult RequestTraversal(EntityLocationReferenceData actor, LocationConnectionAccessContextData accessContext = null, double worldTime = 0d, bool preview = false)
        {
            return Runtime.RequestTransition(new SceneBindingTransitionRequest
            {
                transactionId = $"scene-binding.traverse.{LogicalId}.{Guid.NewGuid():N}",
                actor = actor,
                connectionId = LogicalId,
                fromLocationId = sourceLocationId,
                toLocationId = destinationLocationId,
                accessContext = accessContext,
                worldTime = worldTime,
                preview = preview
            });
        }
    }
}
