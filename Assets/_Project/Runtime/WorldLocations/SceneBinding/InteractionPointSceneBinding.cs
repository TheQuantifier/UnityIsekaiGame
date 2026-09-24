using UnityEngine;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Interaction;

namespace UnityIsekaiGame.WorldLocations.SceneBinding
{
    public interface IInteractionPointDestinationHandler
    {
        string InteractionPrompt { get; }
        bool CanHandleInteraction(in InteractionContext context, InteractionPointSnapshot point);
        void HandleInteraction(in InteractionContext context, InteractionPointSnapshot point);
    }

    public sealed class InteractionPointSceneBinding : WorldSceneBindingComponent, IInteractable
    {
        [SerializeField] private float interactionRange = 3f;
        [SerializeField] private bool requirePhysicalRange = true;

        public override WorldSceneBindingCategory Category => WorldSceneBindingCategory.InteractionPoint;
        public string InteractionPrompt
        {
            get
            {
                IInteractionPointDestinationHandler handler = ResolveDestinationHandler();
                return handler == null || string.IsNullOrWhiteSpace(handler.InteractionPrompt)
                    ? string.IsNullOrWhiteSpace(DisplayName) ? "Interact" : $"Interact: {DisplayName}"
                    : handler.InteractionPrompt;
            }
        }
        public float InteractionRange => interactionRange;
        public bool RequiresPhysicalRange => requirePhysicalRange;
        public InteractionPointSnapshot LastPoint { get; private set; }

        public void ConfigureInteraction(float range = 3f, bool enforcePhysicalRange = true)
        {
            interactionRange = Mathf.Max(0.1f, range);
            requirePhysicalRange = enforcePhysicalRange;
        }

        public bool CanInteract(in InteractionContext context)
        {
            if (Status != WorldSceneBindingStatus.Bound)
            {
                return false;
            }

            if (requirePhysicalRange && !IsWithinPhysicalRange(context))
            {
                return false;
            }

            if (!Runtime.TryGetInteractionPoint(LogicalId, out InteractionPointSnapshot point) || !point.IsActive)
            {
                return false;
            }

            IInteractionPointDestinationHandler handler = ResolveDestinationHandler();
            return handler == null || handler.CanHandleInteraction(context, point);
        }

        public void Interact(in InteractionContext context)
        {
            if (!Runtime.TryGetInteractionPoint(LogicalId, out InteractionPointSnapshot point))
            {
                ApplyBindingResolution(WorldSceneBindingStatus.WaitingForLogicalRecord, "Interaction request blocked because the authoritative point is missing.");
                return;
            }

            LastPoint = point;
            IInteractionPointDestinationHandler handler = ResolveDestinationHandler();
            if (handler != null)
            {
                handler.HandleInteraction(context, point);
                return;
            }

            PrototypeHudMessageBus.Show($"Interacted with {DisplayName}.");
            Debug.Log($"Scene interaction routed to logical interaction point '{point.InteractionPointId}'.");
        }

        private IInteractionPointDestinationHandler ResolveDestinationHandler()
        {
            MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] != this && behaviours[i] is IInteractionPointDestinationHandler handler)
                {
                    return handler;
                }
            }

            return null;
        }

        private bool IsWithinPhysicalRange(in InteractionContext context)
        {
            if (context.Origin == null)
            {
                return true;
            }

            Vector3 target = BindingTransform.position;
            if (context.Hit.collider != null)
            {
                target = context.Hit.point;
            }

            float distance = Vector3.Distance(context.Origin.position, target);
            return distance <= Mathf.Max(0.01f, interactionRange);
        }
    }
}
