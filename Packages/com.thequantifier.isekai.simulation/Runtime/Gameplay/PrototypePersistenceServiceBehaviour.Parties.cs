using UnityEngine;
using UnityIsekaiGame.Parties;
using UnityIsekaiGame.Persistence;

namespace UnityIsekaiGame.Gameplay
{
    public sealed partial class PrototypePersistenceServiceBehaviour
    {
        [SerializeField] private bool registerWorldPartyOperations = true;
        private PartyOperationalRuntime partyOperations;
        private PartyOperationalPersistenceParticipant worldPartyOperationalParticipant;
        private PartyTravelCoordinator partyTravel;

        public PartyOperationalRuntime PartyOperations
        {
            get
            {
                if (partyOperations == null)
                {
                    partyOperations = new PartyOperationalRuntime();
                    partyOperations.Configure(AdventuringParties);
                    PartyCombatContext.Configure(AdventuringParties, partyOperations);
                    partyOperations.Changed += OnPartyOperationsChanged;
                }
                return partyOperations;
            }
        }

        public PartyTravelCoordinator PartyTravel => partyTravel ??= new PartyTravelCoordinator(AdventuringParties, PartyOperations);

        private void EnsureWorldPartyOperationalParticipant()
        {
            if (!registerWorldPartyOperations || worldPartyOperationalParticipant != null) return;
            _ = PartyOperations;
            worldPartyOperationalParticipant = new PartyOperationalPersistenceParticipant(partyOperations, playerService?.WorldId ?? GameData.Persistence.PersistenceService.LocalWorldId);
            RegisterParticipant(worldPartyOperationalParticipant, out string failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Debug.LogWarning(failureReason);
                worldPartyOperationalParticipant = null;
            }
        }

        private void OnPartyOperationsChanged() => dirtyTracker?.MarkDirty("Party membership, readiness, command, or settings changed.");

        private void ShutdownPartyOperations()
        {
            if (worldPartyOperationalParticipant != null)
            {
                UnregisterParticipant(worldPartyOperationalParticipant);
                worldPartyOperationalParticipant = null;
            }
            if (partyOperations != null)
            {
                PartyCombatContext.Clear(partyOperations);
                partyOperations.Changed -= OnPartyOperationsChanged;
                partyOperations.Dispose();
                partyOperations = null;
            }
            partyTravel = null;
        }
    }
}
