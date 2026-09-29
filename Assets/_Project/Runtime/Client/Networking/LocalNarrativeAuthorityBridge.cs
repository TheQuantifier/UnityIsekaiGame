using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Interaction;
using UnityIsekaiGame.Parties;
using UnityIsekaiGame.Presentation;
using UnityIsekaiGame.WorldLocations.SceneBinding;
using UnityIsekaiGame.PrototypeIntegration;

namespace UnityIsekaiGame.Networking.Client
{
    [DisallowMultipleComponent]
    public sealed class LocalNarrativeAuthorityBridge : MonoBehaviour, INarrativeAuthorityClientBridge
    {
        public const string NarrativeSmokeFlag = "--narrative-smoke";
        [SerializeField] private LocalGameClient client;
        [SerializeField] private CameraInteractionDetector interactionDetector;
        [SerializeField] private PrototypePersistenceServiceBehaviour persistence;

        private readonly Dictionary<uint, NetworkNarrativeCommand> pending = new Dictionary<uint, NetworkNarrativeCommand>();
        private NetworkPlayerNarrative networkNarrative;
        private string authorizedInteractionPointId = string.Empty;
        private string authorizedQuestSourceId = string.Empty;
        private bool applyingReplica;
        private bool smokeEnabled;
        private int smokePhase;

        public static LocalNarrativeAuthorityBridge Active { get; private set; }
        public bool IsServerAuthorityActive => networkNarrative != null && networkNarrative.IsSpawned && networkNarrative.IsOwner;
        public bool IsApplyingReplica => applyingReplica;
        public event Action ReplicaChanged;
        public event Action<string> FeedbackReceived;

        private void Awake()
        {
            ResolveReferences();
            smokeEnabled = Array.Exists(Environment.GetCommandLineArgs(), value =>
                string.Equals(value, NarrativeSmokeFlag, StringComparison.OrdinalIgnoreCase));
        }

        private void Update()
        {
            if (!smokeEnabled || !IsServerAuthorityActive || smokePhase != 0) return;
            InteractionPointSceneBinding binding = FindObjectsByType<InteractionPointSceneBinding>(FindObjectsInactive.Exclude)
                .FirstOrDefault(value =>
                {
                    QuestSourceSceneBinding destination = value == null ? null : value.GetComponent<QuestSourceSceneBinding>();
                    return destination != null && !destination.OpensConversation && !destination.IsGuildDeskSurface;
                });
            if (binding == null || !Request(NarrativeAuthorityCommandType.Interact, binding.LogicalId)) return;
            smokePhase = 1;
            Debug.Log($"[Network Narrative] Client requested authoritative interaction with '{binding.LogicalId}'.", this);
        }

        private void OnEnable()
        {
            ResolveReferences();
            Active = this;
            NarrativeAuthorityBridgeRegistry.Active = this;
            if (client == null) return;
            client.LocalPlayerActorChanged += OnLocalPlayerActorChanged;
            OnLocalPlayerActorChanged(client.LocalPlayerActor);
        }

        private void OnDisable()
        {
            if (ReferenceEquals(Active, this)) Active = null;
            if (ReferenceEquals(NarrativeAuthorityBridgeRegistry.Active, this)) NarrativeAuthorityBridgeRegistry.Active = null;
            if (client != null) client.LocalPlayerActorChanged -= OnLocalPlayerActorChanged;
            Bind(null);
        }

        public void Configure(
            LocalGameClient localClient,
            CameraInteractionDetector detector,
            PrototypePersistenceServiceBehaviour runtimePersistence)
        {
            client = localClient;
            interactionDetector = detector;
            persistence = runtimePersistence;
        }

        public bool RequestBrowseQuestSource(string sourceId)
            => Request(NarrativeAuthorityCommandType.BrowseQuestSource, sourceId);
        public bool RequestAcceptQuest(string listingId, string sourceId)
            => Request(NarrativeAuthorityCommandType.AcceptQuestListing, listingId, sourceId);
        public bool RequestAbandonQuest(string assignmentId)
            => Request(NarrativeAuthorityCommandType.AbandonQuest, assignmentId);
        public bool RequestClaimQuestReward(string entitlementId)
            => Request(NarrativeAuthorityCommandType.ClaimQuestReward, entitlementId);
        public bool RequestDialogueChoice(string choiceId)
            => Request(NarrativeAuthorityCommandType.SelectDialogueChoice, choiceId);
        public bool RequestEndDialogue()
            => Request(NarrativeAuthorityCommandType.EndDialogue, "active-dialogue");
        public bool RequestCreateParty(string displayName = "Adventuring Party")
            => Request(NarrativeAuthorityCommandType.CreateParty, displayName);
        public bool RequestInvitePartyMember(string personId)
            => Request(NarrativeAuthorityCommandType.InvitePartyMember, personId);
        public bool RequestAcceptPartyInvitation(string invitationId)
            => Request(NarrativeAuthorityCommandType.AcceptPartyInvitation, invitationId);
        public bool RequestDeclinePartyInvitation(string invitationId)
            => Request(NarrativeAuthorityCommandType.DeclinePartyInvitation, invitationId);
        public bool RequestRemovePartyMember(string personId)
            => Request(NarrativeAuthorityCommandType.RemovePartyMember, personId);
        public bool RequestTransferPartyLeadership(string personId)
            => Request(NarrativeAuthorityCommandType.TransferPartyLeadership, personId);
        public bool RequestSetPartyReady(bool ready)
            => Request(NarrativeAuthorityCommandType.SetPartyReady, "self", value: ready ? 1 : 0);
        public bool RequestPartySettings(PartyFormation formation, PartyLootPolicy loot, PartyFriendlyFirePolicy friendly, PartyCommand command)
            => Request(NarrativeAuthorityCommandType.SetPartySettings, value: PackSettings(formation, loot, friendly, command));
        public bool RequestLeaveParty()
            => Request(NarrativeAuthorityCommandType.LeaveParty, "self");
        public bool RequestDissolveParty()
            => Request(NarrativeAuthorityCommandType.DissolveParty, "current-party");

        private void OnLocalPlayerActorChanged(NetworkPlayerActor playerActor)
        {
            Bind(playerActor == null ? null : playerActor.GetComponent<NetworkPlayerNarrative>());
        }

        private void Bind(NetworkPlayerNarrative narrative)
        {
            if (ReferenceEquals(networkNarrative, narrative)) return;
            if (networkNarrative != null) networkNarrative.CommandResultChanged -= OnCommandResultChanged;
            if (interactionDetector != null && interactionDetector.ExternalInteractionHandler == HandleExternalInteraction)
                interactionDetector.ExternalInteractionHandler = null;

            pending.Clear();
            authorizedInteractionPointId = string.Empty;
            authorizedQuestSourceId = string.Empty;
            networkNarrative = narrative;
            smokePhase = 0;
            if (networkNarrative == null) return;
            networkNarrative.CommandResultChanged += OnCommandResultChanged;
            if (interactionDetector != null) interactionDetector.ExternalInteractionHandler = HandleExternalInteraction;
        }

        private bool HandleExternalInteraction(IInteractable interactable, InteractionContext context)
        {
            if (!IsServerAuthorityActive) return false;
            if (interactable is InteractionPointSceneBinding binding && !string.IsNullOrWhiteSpace(binding.LogicalId))
            {
                Request(NarrativeAuthorityCommandType.Interact, binding.LogicalId);
            }
            else
            {
                PublishFeedback("That interaction is not available until it has a server-authoritative scene binding.", true);
            }
            return true;
        }

        private bool Request(
            NarrativeAuthorityCommandType commandType,
            string primaryId = "",
            string secondaryId = "",
            int value = 0,
            int secondaryValue = 0)
        {
            if (!IsServerAuthorityActive) return false;
            if (!networkNarrative.Request(commandType, primaryId, secondaryId, value, secondaryValue)) return false;
            uint sequence = networkNarrative.LastSubmittedCommandSequence;
            pending[sequence] = new NetworkNarrativeCommand(sequence, commandType, primaryId, secondaryId, value, secondaryValue);
            return true;
        }

        private void OnCommandResultChanged(NetworkNarrativeCommandResult result)
        {
            pending.TryGetValue(result.Sequence, out NetworkNarrativeCommand command);
            pending.Remove(result.Sequence);
            PublishFeedback(result.MessageText, !result.Succeeded);
            if (!result.Succeeded) return;

            applyingReplica = true;
            try
            {
                ApplySuccessfulCommand(command, result);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                PublishFeedback("The client could not apply the authoritative narrative presentation.", true);
            }
            finally
            {
                applyingReplica = false;
            }

            ReplicaChanged?.Invoke();
            AdvanceSmoke(result);
        }

        private void AdvanceSmoke(NetworkNarrativeCommandResult result)
        {
            if (!smokeEnabled || !result.Succeeded) return;
            if (smokePhase == 1 && result.CommandType == NarrativeAuthorityCommandType.Interact)
            {
                if (string.IsNullOrWhiteSpace(authorizedQuestSourceId) || !RequestBrowseQuestSource(authorizedQuestSourceId)) return;
                smokePhase = 2;
                return;
            }

            if (smokePhase == 2 && result.CommandType == NarrativeAuthorityCommandType.BrowseQuestSource)
            {
                if (!RequestCreateParty("Smoke Test Party")) return;
                smokePhase = 3;
                return;
            }

            if (smokePhase == 3 && result.CommandType == NarrativeAuthorityCommandType.CreateParty)
            {
                smokePhase = 4;
                Debug.Log("[Network Narrative] Authoritative narrative smoke completed: interaction, quest-source browse, and party creation.", this);
                client?.Disconnect();
            }
        }

        private void ApplySuccessfulCommand(NetworkNarrativeCommand command, NetworkNarrativeCommandResult result)
        {
            PrototypeNarrativeCoordinator coordinator = persistence?.NarrativeCoordinator;
            switch (command.CommandType)
            {
                case NarrativeAuthorityCommandType.Interact:
                    authorizedInteractionPointId = result.PrimaryIdText;
                    authorizedQuestSourceId = result.Presentation is NarrativePresentationAction.OpenQuestSource or NarrativePresentationAction.OpenGuildDesk
                        ? result.SecondaryIdText
                        : string.Empty;
                    FindBinding(command.PrimaryIdText)?.PresentAuthorizedInteraction(interactionDetector?.Interactor);
                    break;
                case NarrativeAuthorityCommandType.BrowseQuestSource:
                    FindAnyObjectByType<PrototypeQuestSourcePanel>()?.ApplyAuthorizedBrowse();
                    break;
                case NarrativeAuthorityCommandType.AcceptQuestListing:
                    coordinator?.AcceptListing(command.PrimaryIdText, authorizedInteractionPointId);
                    FindAnyObjectByType<PrototypeQuestSourcePanel>()?.ApplyAuthorizedBrowse();
                    break;
                case NarrativeAuthorityCommandType.AbandonQuest:
                    coordinator?.AbandonAssignment(command.PrimaryIdText);
                    break;
                case NarrativeAuthorityCommandType.ClaimQuestReward:
                    coordinator?.ClaimReward(command.PrimaryIdText);
                    break;
                case NarrativeAuthorityCommandType.SelectDialogueChoice:
                    FindAnyObjectByType<PrototypeDialoguePanel>()?.ApplyAuthorizedChoice(command.PrimaryIdText);
                    break;
                case NarrativeAuthorityCommandType.EndDialogue:
                    FindAnyObjectByType<PrototypeDialoguePanel>()?.ApplyAuthorizedEnd();
                    break;
                default:
                    ApplyPartyReplica(command, result);
                    break;
            }
        }

        private void ApplyPartyReplica(NetworkNarrativeCommand command, NetworkNarrativeCommandResult result)
        {
            if (persistence == null) return;
            string playerId = persistence.PlayerPersonId;
            PartySnapshot party = persistence.AdventuringParties.GetPartyForPerson(playerId);
            double now = Time.unscaledTimeAsDouble;
            string transaction = $"replica.party.{command.CommandType}.{command.Sequence}";
            switch (command.CommandType)
            {
                case NarrativeAuthorityCommandType.CreateParty:
                    persistence.AdventuringParties.CreateParty(result.PrimaryIdText, command.PrimaryIdText, playerId, now, transaction);
                    break;
                case NarrativeAuthorityCommandType.InvitePartyMember:
                    if (party != null) persistence.PartyOperations.Invite(party.PartyId, playerId, command.PrimaryIdText, now, transaction, out _);
                    break;
                case NarrativeAuthorityCommandType.AcceptPartyInvitation:
                    persistence.PartyOperations.AcceptInvitation(command.PrimaryIdText, playerId, now, transaction);
                    break;
                case NarrativeAuthorityCommandType.DeclinePartyInvitation:
                    persistence.PartyOperations.ResolveInvitation(command.PrimaryIdText, playerId, PartyInvitationStatus.Declined, now, out _);
                    break;
                case NarrativeAuthorityCommandType.RemovePartyMember:
                    if (party != null) persistence.AdventuringParties.RemoveMember(party.PartyId, playerId, command.PrimaryIdText, now, transaction);
                    break;
                case NarrativeAuthorityCommandType.TransferPartyLeadership:
                    if (party != null) persistence.AdventuringParties.TransferLeadership(party.PartyId, playerId, command.PrimaryIdText, now, transaction);
                    break;
                case NarrativeAuthorityCommandType.SetPartyReady:
                    if (party != null)
                    {
                        PartyMemberOperationalData member = persistence.PartyOperations.GetMember(party.PartyId, playerId);
                        persistence.PartyOperations.ReportMemberState(party.PartyId, playerId, command.Value != 0 ? PartyMemberReadiness.Ready : PartyMemberReadiness.Busy, member?.locationId ?? string.Empty, member?.distanceToLeader ?? 0f, true, member?.alive ?? true, member?.conscious ?? true);
                    }
                    break;
                case NarrativeAuthorityCommandType.SetPartySettings:
                    if (party != null)
                    {
                        UnpackSettings(command.Value, out PartyFormation formation, out PartyLootPolicy loot, out PartyFriendlyFirePolicy friendly, out PartyCommand partyCommand);
                        persistence.PartyOperations.SetSettings(party.PartyId, playerId, formation, loot, friendly, partyCommand, out _);
                    }
                    break;
                case NarrativeAuthorityCommandType.LeaveParty:
                    if (party != null) persistence.AdventuringParties.RemoveMember(party.PartyId, playerId, playerId, now, transaction);
                    break;
                case NarrativeAuthorityCommandType.DissolveParty:
                    if (party != null) persistence.AdventuringParties.DissolveParty(party.PartyId, playerId, now, transaction);
                    break;
            }
        }

        private InteractionPointSceneBinding FindBinding(string logicalId)
            => FindObjectsByType<InteractionPointSceneBinding>(FindObjectsInactive.Exclude)
                .FirstOrDefault(value => value != null && string.Equals(value.LogicalId, logicalId, StringComparison.Ordinal));

        private void ResolveReferences()
        {
            client ??= GetComponent<LocalGameClient>();
            interactionDetector ??= FindAnyObjectByType<CameraInteractionDetector>();
            persistence ??= FindAnyObjectByType<PrototypePersistenceServiceBehaviour>();
        }

        private void PublishFeedback(string message, bool warning)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            if (warning) Debug.LogWarning($"[Network Narrative] {message}", this);
            else Debug.Log($"[Network Narrative] {message}", this);
            GameHudMessageBus.Show(message, warning ? GameHudMessageTone.Warning : GameHudMessageTone.Information);
            FeedbackReceived?.Invoke(message);
        }

        private static int PackSettings(PartyFormation formation, PartyLootPolicy loot, PartyFriendlyFirePolicy friendly, PartyCommand command)
            => ((int)formation & 0xff) | (((int)loot & 0xff) << 8) | (((int)friendly & 0xff) << 16) | (((int)command & 0x7f) << 24);
        private static void UnpackSettings(int packed, out PartyFormation formation, out PartyLootPolicy loot, out PartyFriendlyFirePolicy friendly, out PartyCommand command)
        {
            formation = (PartyFormation)(packed & 0xff);
            loot = (PartyLootPolicy)((packed >> 8) & 0xff);
            friendly = (PartyFriendlyFirePolicy)((packed >> 16) & 0xff);
            command = (PartyCommand)((packed >> 24) & 0x7f);
        }
    }
}
