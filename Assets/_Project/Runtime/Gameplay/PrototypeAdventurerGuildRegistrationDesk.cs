using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Interaction;
using UnityIsekaiGame.Organizations;
using UnityIsekaiGame.WorldLocations;
using UnityIsekaiGame.WorldLocations.SceneBinding;

namespace UnityIsekaiGame.Gameplay
{
    public sealed class PrototypeAdventurerRegistrationResult
    {
        private PrototypeAdventurerRegistrationResult(bool succeeded, bool alreadyRegistered, OrganizationMembershipSnapshot membership, string message)
        {
            Succeeded = succeeded;
            AlreadyRegistered = alreadyRegistered;
            Membership = membership;
            Message = message ?? string.Empty;
        }

        public bool Succeeded { get; }
        public bool AlreadyRegistered { get; }
        public OrganizationMembershipSnapshot Membership { get; }
        public string Message { get; }

        public static PrototypeAdventurerRegistrationResult Success(OrganizationMembershipSnapshot membership, string message) => new PrototypeAdventurerRegistrationResult(true, false, membership, message);
        public static PrototypeAdventurerRegistrationResult ExistingRegistration(OrganizationMembershipSnapshot membership, string message) => new PrototypeAdventurerRegistrationResult(true, true, membership, message);
        public static PrototypeAdventurerRegistrationResult Failure(string message) => new PrototypeAdventurerRegistrationResult(false, false, null, message);
    }

    public sealed class PrototypeMerchantRegistrationResult
    {
        private PrototypeMerchantRegistrationResult(bool succeeded, bool alreadyRegistered, OrganizationMembershipSnapshot membership, string message)
        {
            Succeeded = succeeded;
            AlreadyRegistered = alreadyRegistered;
            Membership = membership;
            Message = message ?? string.Empty;
        }

        public bool Succeeded { get; }
        public bool AlreadyRegistered { get; }
        public OrganizationMembershipSnapshot Membership { get; }
        public string Message { get; }

        public static PrototypeMerchantRegistrationResult Success(OrganizationMembershipSnapshot membership, string message) => new PrototypeMerchantRegistrationResult(true, false, membership, message);
        public static PrototypeMerchantRegistrationResult ExistingRegistration(OrganizationMembershipSnapshot membership, string message) => new PrototypeMerchantRegistrationResult(true, true, membership, message);
        public static PrototypeMerchantRegistrationResult Failure(string message) => new PrototypeMerchantRegistrationResult(false, false, null, message);
    }

    [DisallowMultipleComponent]
    public sealed class PrototypeAdventurerGuildRegistrationDesk : MonoBehaviour, IInteractionPointDestinationHandler
    {
        [SerializeField] private PrototypePersistenceServiceBehaviour services;

        public string InteractionPrompt
        {
            get
            {
                ResolveServices();
                return "Use Adventurers Guild Desk";
            }
        }

        public bool CanHandleInteraction(in InteractionContext context, InteractionPointSnapshot point)
        {
            ResolveServices();
            return services != null
                && point != null
                && point.IsActive
                && point.ServiceDefinitionIds.Contains(PrototypeInteractionPointDefinitionFactory.RegisterAdventurerServiceId);
        }

        public void HandleInteraction(in InteractionContext context, InteractionPointSnapshot point)
        {
            ResolveServices();
            if (services == null)
            {
                PrototypeHudMessageBus.Show("Adventurer registration services are unavailable.");
                return;
            }

            PrototypeGuildDeskPanel panel = FindAnyObjectByType<PrototypeGuildDeskPanel>(FindObjectsInactive.Include);
            if (panel == null)
            {
                PrototypeHudMessageBus.Show("Guild desk services are unavailable.");
                return;
            }

            panel.OpenAdventurersGuildDesk(point?.InteractionPointId, context.Interactor);
        }

        private void ResolveServices()
        {
            if (services == null) services = FindAnyObjectByType<PrototypePersistenceServiceBehaviour>(FindObjectsInactive.Include);
        }
    }
}
