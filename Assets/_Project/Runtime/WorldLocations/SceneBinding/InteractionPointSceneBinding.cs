using UnityEngine;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Interaction;

namespace UnityIsekaiGame.WorldLocations.SceneBinding
{
    public sealed class InteractionPointSceneBinding : WorldSceneBindingComponent, IInteractable
    {
        [SerializeField] private float interactionRange = 3f;
        [SerializeField] private bool requirePhysicalRange = true;

        public override WorldSceneBindingCategory Category => WorldSceneBindingCategory.InteractionPoint;
        public string InteractionPrompt => string.IsNullOrWhiteSpace(DisplayName) ? "Interact" : $"Interact: {DisplayName}";
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

            return Runtime.TryGetInteractionPoint(LogicalId, out InteractionPointSnapshot point) && point.IsActive;
        }

        public void Interact(in InteractionContext context)
        {
            if (!Runtime.TryGetInteractionPoint(LogicalId, out InteractionPointSnapshot point))
            {
                ApplyBindingResolution(WorldSceneBindingStatus.WaitingForLogicalRecord, "Interaction request blocked because the authoritative point is missing.");
                return;
            }

            LastPoint = point;
            PrototypeHudMessageBus.Show($"Interacted with {DisplayName}.");
            Debug.Log($"Scene interaction routed to logical interaction point '{point.InteractionPointId}'.");
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
