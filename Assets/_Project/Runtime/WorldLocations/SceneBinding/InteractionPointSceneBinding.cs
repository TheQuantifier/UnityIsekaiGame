using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Interaction;
using UnityIsekaiGame.Quests;

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

        public bool IsWithinAuthoritativeRange(Vector3 actorPosition)
        {
            if (!requirePhysicalRange) return true;
            return Vector3.Distance(actorPosition, BindingTransform.position)
                <= Mathf.Max(0.01f, interactionRange) + 0.35f;
        }

        public bool TryInvokeAuthoritative(
            PrototypePersistenceServiceBehaviour persistence,
            Vector3 actorPosition,
            string personId,
            string bodyId,
            double worldTime,
            out InteractionPointSnapshot point,
            out InteractionInvocationResult invocation,
            out string failure)
        {
            point = null;
            invocation = null;
            if (persistence == null || string.IsNullOrWhiteSpace(personId))
            {
                failure = "Authoritative interaction identity is unavailable.";
                return false;
            }

            if (Status != WorldSceneBindingStatus.Bound || !IsWithinAuthoritativeRange(actorPosition))
            {
                failure = Status != WorldSceneBindingStatus.Bound
                    ? "The interaction point is not bound to an active world record."
                    : "The authoritative player is outside the interaction range.";
                return false;
            }

            if (!Runtime.TryGetInteractionPoint(LogicalId, out point) || point == null || !point.IsActive)
            {
                failure = "The authoritative interaction point is unavailable.";
                return false;
            }

            string serviceId = ResolveServiceId(point);
            if (string.IsNullOrWhiteSpace(serviceId))
            {
                failure = "The interaction point has no active service.";
                return false;
            }

            EntityLocationReferenceData consumer = PrototypeEntityLocationFactory.Person(personId);
            InteractionEligibilityResult eligibility = Runtime.EvaluateInteraction(LogicalId, serviceId, consumer, worldTime);
            if (eligibility == null || !eligibility.Eligible)
            {
                failure = eligibility?.Message ?? "The interaction service rejected this player.";
                return false;
            }

            invocation = Runtime.InvokeInteraction(LogicalId, serviceId, consumer, worldTime);
            if (invocation == null || !invocation.Success)
            {
                failure = invocation?.Message ?? "The authoritative interaction failed.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(bodyId))
            {
                Runtime.SynchronizePhysicalPresence(
                    PrototypeEntityLocationFactory.Body(bodyId),
                    point.ActiveHostLocationId,
                    worldTime);
            }

            LastPoint = point;
            failure = string.Empty;
            return true;
        }

        public bool PresentAuthorizedInteraction(GameObject interactor)
        {
            if (!Runtime.TryGetInteractionPoint(LogicalId, out InteractionPointSnapshot point) || point == null) return false;
            LastPoint = point;
            IInteractionPointDestinationHandler handler = ResolveDestinationHandler();
            if (handler == null) return false;
            handler.HandleInteraction(new InteractionContext(interactor, null, default), point);
            return true;
        }

        public void ConfigureInteraction(float range = 3f, bool enforcePhysicalRange = true, string preferredServiceDefinitionId = null)
        {
            interactionRange = Mathf.Max(0.1f, range);
            requirePhysicalRange = enforcePhysicalRange;
            serviceDefinitionId = preferredServiceDefinitionId?.Trim() ?? string.Empty;
        }

        public bool CanInteract(in InteractionContext context)
        {
            PrototypePersistenceServiceBehaviour persistence = PrototypePersistenceServiceBehaviour.FindForInteractor(context.Interactor);
            if (persistence == null || !persistence.OwnsPlayerInteractor(context.Interactor))
            {
                return false;
            }

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
            InteractionEligibilityResult eligibility = Runtime.EvaluateInteraction(LogicalId, serviceId, PlayerConsumer(persistence), WorldTime(persistence));
            if (eligibility == null || !eligibility.Eligible) return false;

            IInteractionPointDestinationHandler handler = ResolveDestinationHandler();
            return handler == null || handler.CanHandleInteraction(context, point);
        }

        public void Interact(in InteractionContext context)
        {
            PrototypePersistenceServiceBehaviour persistence = PrototypePersistenceServiceBehaviour.FindForInteractor(context.Interactor);
            if (persistence == null || !persistence.OwnsPlayerInteractor(context.Interactor))
            {
                return;
            }

            if (!Runtime.TryGetInteractionPoint(LogicalId, out InteractionPointSnapshot point))
            {
                ApplyBindingResolution(WorldSceneBindingStatus.WaitingForLogicalRecord, "Interaction request blocked because the authoritative point is missing.");
                return;
            }

            LastPoint = point;
            string serviceId = ResolveServiceId(point);
            double worldTime = WorldTime(persistence);
            InteractionInvocationResult invocation = Runtime.InvokeInteraction(LogicalId, serviceId, PlayerConsumer(persistence), worldTime);
            if (invocation == null || !invocation.Success)
            {
                GameHudMessageBus.Show(invocation?.Message ?? "That service is currently unavailable.");
                return;
            }
            Runtime.SynchronizePhysicalPresence(PlayerBody(), point.ActiveHostLocationId, worldTime);
            PrototypePersistenceServiceBehaviour services = persistence;
            if (services?.NarrativeCoordinator != null)
                services.NarrativeCoordinator.HandleInteractionPointUsed(point.InteractionPointId, string.IsNullOrWhiteSpace(invocation.RequestId) ? serviceId : invocation.RequestId);
            else
                QuestObjectiveSignalBus.Report(QuestObjectiveCategory.UseInteractionPoint, point.InteractionPointId, persistence.PlayerPersonId, worldTime, sourceEventId: invocation.RequestId);

            IInteractionPointDestinationHandler handler = ResolveDestinationHandler();
            if (handler != null)
            {
                handler.HandleInteraction(context, point);
                return;
            }

            GameHudMessageBus.Show($"Interacted with {DisplayName}.");
            Debug.Log($"Scene interaction routed to logical interaction point '{point.InteractionPointId}'.");
        }

        private string ResolveServiceId(InteractionPointSnapshot point)
        {
            if (!string.IsNullOrWhiteSpace(serviceDefinitionId) && point.ServiceDefinitionIds.Contains(serviceDefinitionId)) return serviceDefinitionId;
            return point.ServiceDefinitionIds.FirstOrDefault() ?? string.Empty;
        }

        private static EntityLocationReferenceData PlayerConsumer(PrototypePersistenceServiceBehaviour persistence)
        {
            return PrototypeEntityLocationFactory.Person(persistence.PlayerPersonId);
        }

        private static EntityLocationReferenceData PlayerBody()
        {
            return PrototypeEntityLocationFactory.Body(PrototypeEntityLocationFactory.PlayerBodyId);
        }

        private static double WorldTime(PrototypePersistenceServiceBehaviour persistence)
        {
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
