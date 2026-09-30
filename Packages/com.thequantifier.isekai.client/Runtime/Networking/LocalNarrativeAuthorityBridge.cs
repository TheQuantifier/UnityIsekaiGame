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
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Quests;

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
        private string authorizedQuestSourceId = string.Empty;
        private bool applyingReplica;
        private bool smokeEnabled;
        private int smokePhase;
        private IReadOnlyList<PrototypeQuestJournalEntry> replicatedJournal = Array.Empty<PrototypeQuestJournalEntry>();
        private NarrativePartyReplicaData replicatedParty;
        private IReadOnlyList<NarrativePartyInvitationReplicaData> replicatedInvitations = Array.Empty<NarrativePartyInvitationReplicaData>();
        private string replicatedPersonId = string.Empty;

        public static LocalNarrativeAuthorityBridge Active { get; private set; }
        public bool IsServerAuthorityActive => networkNarrative != null && networkNarrative.IsSpawned && networkNarrative.IsOwner;
        public bool IsApplyingReplica => applyingReplica;
        public IReadOnlyList<PrototypeQuestJournalEntry> ReplicatedJournal => replicatedJournal;
        public NarrativePartyReplicaData ReplicatedParty => replicatedParty;
        public IReadOnlyList<NarrativePartyInvitationReplicaData> ReplicatedInvitations => replicatedInvitations;
        public string ReplicatedPersonId => replicatedPersonId;
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
            if (networkNarrative != null) networkNarrative.AuthoritativeSnapshotChanged -= OnAuthoritativeSnapshotChanged;
            if (interactionDetector != null && interactionDetector.ExternalInteractionHandler == HandleExternalInteraction)
                interactionDetector.ExternalInteractionHandler = null;

            pending.Clear();
            authorizedQuestSourceId = string.Empty;
            networkNarrative = narrative;
            replicatedJournal = Array.Empty<PrototypeQuestJournalEntry>();
            replicatedParty = null;
            replicatedInvitations = Array.Empty<NarrativePartyInvitationReplicaData>();
            replicatedPersonId = string.Empty;
            smokePhase = 0;
            if (networkNarrative == null) return;
            networkNarrative.CommandResultChanged += OnCommandResultChanged;
            networkNarrative.AuthoritativeSnapshotChanged += OnAuthoritativeSnapshotChanged;
            if (!string.IsNullOrWhiteSpace(networkNarrative.AuthoritativeSnapshotJson))
                OnAuthoritativeSnapshotChanged(networkNarrative.AuthoritativeSnapshotJson);
            if (interactionDetector != null) interactionDetector.ExternalInteractionHandler = HandleExternalInteraction;
        }

        private bool HandleExternalInteraction(IInteractable interactable, InteractionContext context)
        {
            if (!IsServerAuthorityActive) return false;
            if (interactable is InteractionPointSceneBinding binding && !string.IsNullOrWhiteSpace(binding.LogicalId))
            {
                Request(NarrativeAuthorityCommandType.Interact, binding.LogicalId);
                return true;
            }

            // Narrative authority owns authored interaction-point bindings only. Other replicated
            // interactables (for example NetworkWorldItemPickupInteractable) route through their
            // own server RPC and must be allowed to handle the interaction themselves.
            return false;
        }

        private bool Request(
            NarrativeAuthorityCommandType commandType,
            string primaryId = "",
            string secondaryId = "",
            int value = 0,
            int secondaryValue = 0)
        {
            if (!IsServerAuthorityActive) return false;
            foreach (NetworkNarrativeCommand command in pending.Values)
            {
                if (RepresentsSamePendingAction(
                        command,
                        commandType,
                        primaryId,
                        secondaryId,
                        value,
                        secondaryValue))
                {
                    return false;
                }
            }

            if (!networkNarrative.Request(commandType, primaryId, secondaryId, value, secondaryValue)) return false;
            uint sequence = networkNarrative.LastSubmittedCommandSequence;
            pending[sequence] = new NetworkNarrativeCommand(sequence, commandType, primaryId, secondaryId, value, secondaryValue);
            return true;
        }

        public static bool RepresentsSamePendingAction(
            NetworkNarrativeCommand pendingCommand,
            NarrativeAuthorityCommandType commandType,
            string primaryId,
            string secondaryId,
            int value,
            int secondaryValue)
        {
            return pendingCommand.CommandType == commandType
                && string.Equals(pendingCommand.PrimaryIdText, primaryId ?? string.Empty, StringComparison.Ordinal)
                && string.Equals(pendingCommand.SecondaryIdText, secondaryId ?? string.Empty, StringComparison.Ordinal)
                && pendingCommand.Value == value
                && pendingCommand.SecondaryValue == secondaryValue;
        }

        private void OnCommandResultChanged(NetworkNarrativeCommandResult result)
        {
            if (!pending.Remove(result.Sequence, out NetworkNarrativeCommand command))
            {
                Debug.LogWarning(
                    $"[Network Narrative] Ignored unmatched command result {result.Sequence}; it cannot safely drive client presentation.",
                    this);
                return;
            }

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

        private void OnAuthoritativeSnapshotChanged(string json)
        {
            try
            {
                NarrativeSessionSnapshotData snapshot = JsonUtility.FromJson<NarrativeSessionSnapshotData>(json);
                if (snapshot == null || snapshot.schemaVersion != NarrativeSessionSnapshotData.CurrentSchemaVersion)
                    throw new InvalidOperationException("The server sent an unsupported narrative snapshot.");
                string expectedPersonId = client?.LocalPlayerActor?.PersonId ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(expectedPersonId)
                    && !string.Equals(snapshot.personId, expectedPersonId, StringComparison.Ordinal))
                    throw new InvalidOperationException("The server narrative snapshot belongs to a different Person identity.");

                DefinitionRegistry definitions = persistence?.DefinitionCatalog?.CreateRegistry();
                List<PrototypeQuestJournalEntry> journal = new List<PrototypeQuestJournalEntry>();
                foreach (NarrativeQuestReplicaData replica in snapshot.quests ?? new List<NarrativeQuestReplicaData>())
                {
                    if (replica?.assignment == null || replica.quest == null) continue;
                    QuestDefinition definition = null;
                    definitions?.TryGet(replica.quest.questDefinitionId, out definition);
                    journal.Add(new PrototypeQuestJournalEntry(
                        new QuestAssignmentSnapshot(replica.assignment),
                        new QuestSnapshot(replica.quest),
                        definition,
                        (replica.objectives ?? new List<QuestObjectiveRecordData>()).Select(value => new QuestObjectiveSnapshot(value)).ToArray(),
                        replica.outcome == null ? null : new QuestTerminalOutcomeSnapshot(replica.outcome),
                        (replica.rewards ?? new List<QuestRewardEntitlementRecordData>()).Select(value => new QuestRewardEntitlementSnapshot(value)).ToArray()));
                }

                replicatedJournal = journal.OrderBy(entry => entry.IsTerminal).ThenBy(entry => entry.Title, StringComparer.Ordinal).ToArray();
                replicatedPersonId = snapshot.personId ?? string.Empty;
                replicatedParty = snapshot.party;
                replicatedInvitations = snapshot.invitations?.ToArray() ?? Array.Empty<NarrativePartyInvitationReplicaData>();
                ReplicaChanged?.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                PublishFeedback("The authoritative quest and party snapshot could not be applied.", true);
            }
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
            switch (command.CommandType)
            {
                case NarrativeAuthorityCommandType.Interact:
                    authorizedQuestSourceId = result.Presentation is NarrativePresentationAction.OpenQuestSource or NarrativePresentationAction.OpenGuildDesk
                        ? result.SecondaryIdText
                        : string.Empty;
                    FindBinding(command.PrimaryIdText)?.PresentAuthorizedInteraction(interactionDetector?.Interactor);
                    break;
                case NarrativeAuthorityCommandType.BrowseQuestSource:
                    FindAnyObjectByType<PrototypeQuestSourcePanel>()?.ApplyAuthorizedBrowse();
                    break;
                case NarrativeAuthorityCommandType.AcceptQuestListing:
                    FindAnyObjectByType<PrototypeQuestSourcePanel>()?.ApplyAuthorizedBrowse();
                    break;
                case NarrativeAuthorityCommandType.AbandonQuest:
                    break;
                case NarrativeAuthorityCommandType.ClaimQuestReward:
                    break;
                case NarrativeAuthorityCommandType.SelectDialogueChoice:
                    FindAnyObjectByType<PrototypeDialoguePanel>()?.ApplyAuthorizedChoice(command.PrimaryIdText);
                    break;
                case NarrativeAuthorityCommandType.EndDialogue:
                    FindAnyObjectByType<PrototypeDialoguePanel>()?.ApplyAuthorizedEnd();
                    break;
                default:
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
