using System.Linq;
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
        [SerializeField] private string serviceDefinitionId;

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
        public string PreferredServiceDefinitionId => serviceDefinitionId;
        public InteractionPointSnapshot LastPoint { get; private set; }

        public void ConfigureInteraction(float range = 3f, bool enforcePhysicalRange = true, string preferredServiceDefinitionId = null)
        {
            interactionRange = Mathf.Max(0.1f, range);
            requirePhysicalRange = enforcePhysicalRange;
            serviceDefinitionId = preferredServiceDefinitionId?.Trim() ?? string.Empty;
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

            string serviceId = ResolveServiceId(point);
            if (string.IsNullOrWhiteSpace(serviceId)) return false;
            InteractionEligibilityResult eligibility = Runtime.EvaluateInteraction(LogicalId, serviceId, PlayerConsumer(), WorldTime());
            if (eligibility == null || !eligibility.Eligible) return false;

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
            string serviceId = ResolveServiceId(point);
            double worldTime = WorldTime();
            InteractionInvocationResult invocation = Runtime.InvokeInteraction(LogicalId, serviceId, PlayerConsumer(), worldTime);
            if (invocation == null || !invocation.Success)
            {
                PrototypeHudMessageBus.Show(invocation?.Message ?? "That service is currently unavailable.");
                return;
            }
            Runtime.SynchronizePhysicalPresence(PlayerBody(), point.ActiveHostLocationId, worldTime);

            IInteractionPointDestinationHandler handler = ResolveDestinationHandler();
            if (handler != null)
            {
                handler.HandleInteraction(context, point);
                return;
            }

            PrototypeHudMessageBus.Show($"Interacted with {DisplayName}.");
            Debug.Log($"Scene interaction routed to logical interaction point '{point.InteractionPointId}'.");
        }

        private string ResolveServiceId(InteractionPointSnapshot point)
        {
            if (!string.IsNullOrWhiteSpace(serviceDefinitionId) && point.ServiceDefinitionIds.Contains(serviceDefinitionId)) return serviceDefinitionId;
            return point.ServiceDefinitionIds.FirstOrDefault() ?? string.Empty;
        }

        private static EntityLocationReferenceData PlayerConsumer()
        {
            return PrototypeEntityLocationFactory.Person(PrototypeEntityLocationFactory.PlayerPersonId);
        }

        private static EntityLocationReferenceData PlayerBody()
        {
            return PrototypeEntityLocationFactory.Body(PrototypeEntityLocationFactory.PlayerBodyId);
        }

        private static double WorldTime()
        {
            PrototypePersistenceServiceBehaviour persistence = FindAnyObjectByType<PrototypePersistenceServiceBehaviour>();
            return persistence?.PlayTime?.CumulativeSeconds ?? Time.unscaledTimeAsDouble;
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
