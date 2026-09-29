using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Dialogue;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Parties;
using UnityIsekaiGame.PrototypeIntegration;
using UnityIsekaiGame.Quests;
using UnityIsekaiGame.WorldLocations;
using UnityIsekaiGame.WorldLocations.SceneBinding;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Networking.Server
{
    [DisallowMultipleComponent]
    public sealed class ServerPlayerNarrativeAuthority : MonoBehaviour
    {
        private NetworkPlayerActor actor;
        private NetworkPlayerNarrative networkNarrative;
        private PrototypePersistenceServiceBehaviour services;
        private PrototypeNarrativeCoordinator narrative;
        private string personId;
        private bool ownsNarrativeCoordinator;
        private ServerPlayerInventoryAuthority inventoryAuthority;
        private InteractionPointSceneBinding authorizedBinding;
        private string authorizedQuestSourceId = string.Empty;
        private string activeServerFlowId = string.Empty;
        private double authorizationExpiresAt;
        private bool configured;

        public void Configure(
            NetworkPlayerActor playerActor,
            NetworkPlayerNarrative replicatedNarrative,
            PrototypePersistenceServiceBehaviour persistence,
            ServerPlayerInventoryAuthority authoritativeInventory,
            PrototypeNarrativeCoordinator authoritativeNarrative = null)
        {
            if (configured) throw new InvalidOperationException("Server player narrative authority is already configured.");
            actor = playerActor ?? throw new ArgumentNullException(nameof(playerActor));
            networkNarrative = replicatedNarrative ?? throw new ArgumentNullException(nameof(replicatedNarrative));
            services = persistence ?? throw new ArgumentNullException(nameof(persistence));
            personId = actor.PersonId;
            if (string.IsNullOrWhiteSpace(personId)) throw new InvalidOperationException("The authoritative player Person ID is unavailable.");
            narrative = authoritativeNarrative ?? new PrototypeNarrativeCoordinator(
                services,
                services.DefinitionCatalog.CreateRegistry(),
                personId,
                subscribeToRuntimeEvents: false);
            ownsNarrativeCoordinator = authoritativeNarrative == null;
            narrative.InitializePrototypeContent();
            inventoryAuthority = authoritativeInventory ?? throw new ArgumentNullException(nameof(authoritativeInventory));
            inventoryAuthority.Inventory.ItemAdded += OnAuthoritativeItemAdded;
            narrative.Changed += PublishProjection;
            services.AdventuringParties.Changed += PublishProjection;
            services.PartyOperations.Changed += PublishProjection;
            networkNarrative.ServerCommandHandler = ExecuteCommand;
            configured = true;
        }

        private void OnDestroy()
        {
            if (networkNarrative != null) networkNarrative.ServerCommandHandler = null;
            if (inventoryAuthority?.Inventory != null) inventoryAuthority.Inventory.ItemAdded -= OnAuthoritativeItemAdded;
            if (narrative != null) narrative.Changed -= PublishProjection;
            if (services?.AdventuringParties != null) services.AdventuringParties.Changed -= PublishProjection;
            if (services?.PartyOperations != null) services.PartyOperations.Changed -= PublishProjection;
            if (ownsNarrativeCoordinator) narrative?.Dispose();
        }

        private NetworkNarrativeCommandResult ExecuteCommand(NetworkNarrativeCommand command)
        {
            try
            {
                NetworkNarrativeCommandResult result = command.CommandType switch
                {
                    NarrativeAuthorityCommandType.Interact => Interact(command),
                    NarrativeAuthorityCommandType.BrowseQuestSource => BrowseQuestSource(command),
                    NarrativeAuthorityCommandType.AcceptQuestListing => AcceptQuestListing(command),
                    NarrativeAuthorityCommandType.AbandonQuest => AbandonQuest(command),
                    NarrativeAuthorityCommandType.ClaimQuestReward => ClaimQuestReward(command),
                    NarrativeAuthorityCommandType.SelectDialogueChoice => SelectDialogueChoice(command),
                    NarrativeAuthorityCommandType.EndDialogue => EndDialogue(command),
                    NarrativeAuthorityCommandType.CreateParty => CreateParty(command),
                    NarrativeAuthorityCommandType.InvitePartyMember => InvitePartyMember(command),
                    NarrativeAuthorityCommandType.AcceptPartyInvitation => AcceptPartyInvitation(command),
                    NarrativeAuthorityCommandType.DeclinePartyInvitation => DeclinePartyInvitation(command),
                    NarrativeAuthorityCommandType.RemovePartyMember => RemovePartyMember(command),
                    NarrativeAuthorityCommandType.TransferPartyLeadership => TransferPartyLeadership(command),
                    NarrativeAuthorityCommandType.SetPartyReady => SetPartyReady(command),
                    NarrativeAuthorityCommandType.SetPartySettings => SetPartySettings(command),
                    NarrativeAuthorityCommandType.LeaveParty => LeaveParty(command),
                    NarrativeAuthorityCommandType.DissolveParty => DissolveParty(command),
                    _ => Reject(command, NarrativeAuthorityFailure.InvalidCommand, "Unsupported narrative command.")
                };
                if (result.Succeeded) PublishProjection();
                return result;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                return Reject(command, NarrativeAuthorityFailure.ServerRejected, "The server could not complete the narrative action.");
            }
        }

        public void PublishProjection()
        {
            if (!configured || networkNarrative == null || !networkNarrative.IsSpawned || !networkNarrative.IsServer) return;
            NarrativeSessionSnapshotData snapshot = new NarrativeSessionSnapshotData
            {
                personId = personId,
                revision = Math.Max(1L, narrative.Participation.Revision + narrative.Objectives.Revision + narrative.Outcomes.Revision),
                activeDialogueFlowId = activeServerFlowId
            };

            foreach (PrototypeQuestJournalEntry entry in narrative.GetJournal())
            {
                NarrativeQuestReplicaData quest = new NarrativeQuestReplicaData
                {
                    assignment = entry.Assignment?.ToSaveData(),
                    quest = entry.Quest?.ToSaveData(),
                    outcome = entry.Outcome?.ToSaveData()
                };
                if (entry.Objectives != null)
                    foreach (QuestObjectiveSnapshot objective in entry.Objectives)
                        if (objective != null) quest.objectives.Add(objective.ToSaveData());
                if (entry.Rewards != null)
                    foreach (QuestRewardEntitlementSnapshot reward in entry.Rewards)
                        if (reward != null) quest.rewards.Add(reward.ToSaveData());
                snapshot.quests.Add(quest);
            }

            PartySnapshot party = CurrentParty();
            if (party != null)
            {
                PartySettingsData settings = services.PartyOperations.GetSettings(party.PartyId);
                snapshot.party = new NarrativePartyReplicaData
                {
                    partyId = party.PartyId,
                    displayName = party.DisplayName,
                    minimumOperationalMembers = party.MinimumOperationalMembers,
                    maximumMembers = party.MaximumMembers,
                    formation = (int)(settings?.formation ?? PartyFormation.Wedge),
                    lootPolicy = (int)(settings?.lootPolicy ?? PartyLootPolicy.Individual),
                    friendlyFirePolicy = (int)(settings?.friendlyFire ?? PartyFriendlyFirePolicy.Prevent),
                    groupCommand = (int)(settings?.groupCommand ?? PartyCommand.Follow)
                };
                foreach (PartyMemberSnapshot member in party.Members)
                {
                    PartyMemberOperationalData state = services.PartyOperations.GetMember(party.PartyId, member.PersonId);
                    snapshot.party.members.Add(new NarrativePartyMemberReplicaData
                    {
                        personId = member.PersonId,
                        membershipId = member.MembershipId,
                        isLeader = member.IsLeader,
                        readiness = (int)(state?.readiness ?? PartyMemberReadiness.Missing),
                        locationId = state?.locationId ?? string.Empty,
                        distanceToLeader = state?.distanceToLeader ?? 0f,
                        connected = state?.loaded ?? false,
                        alive = state?.alive ?? true,
                        conscious = state?.conscious ?? true
                    });
                }
            }

            foreach (PartyInvitationData invitation in services.PartyOperations.QueryInvitations(personId, PartyInvitationStatus.Pending))
            {
                snapshot.invitations.Add(new NarrativePartyInvitationReplicaData
                {
                    invitationId = invitation.invitationId,
                    partyId = invitation.partyId,
                    inviterPersonId = invitation.inviterPersonId,
                    invitedPersonId = invitation.invitedPersonId,
                    status = (int)invitation.status
                });
            }

            if (!networkNarrative.PublishServerSnapshot(JsonUtility.ToJson(snapshot, false)))
                Debug.LogError($"[Network Narrative] Failed to publish authoritative reconnect snapshot for '{personId}'.", this);
        }

        private NetworkNarrativeCommandResult Interact(NetworkNarrativeCommand command)
        {
            InteractionPointSceneBinding binding = FindObjectsByType<InteractionPointSceneBinding>(FindObjectsInactive.Exclude)
                .FirstOrDefault(value => value != null && string.Equals(value.LogicalId, command.PrimaryIdText, StringComparison.Ordinal));
            if (binding == null)
                return Reject(command, NarrativeAuthorityFailure.InteractionUnavailable, "The requested interaction point does not exist in the authoritative scene.");
            if (!binding.IsWithinAuthoritativeRange(transform.position))
                return Reject(command, NarrativeAuthorityFailure.OutOfRange, "Move closer before interacting.");

            string bodyId = $"body.network.{Sanitize(actor.PlayerId)}";
            if (!binding.TryInvokeAuthoritative(
                    services,
                    transform.position,
                    personId,
                    bodyId,
                    WorldTime,
                    out InteractionPointSnapshot point,
                    out InteractionInvocationResult invocation,
                    out string failure))
            {
                NarrativeAuthorityFailure code = binding.IsWithinAuthoritativeRange(transform.position)
                    ? NarrativeAuthorityFailure.InteractionUnavailable
                    : NarrativeAuthorityFailure.OutOfRange;
                return Reject(command, code, failure);
            }

            authorizedBinding = binding;
            authorizationExpiresAt = Time.realtimeSinceStartupAsDouble + NarrativeAuthorityLimits.InteractionAuthorizationSeconds;
            narrative.HandleInteractionPointUsed(point.InteractionPointId, invocation.RequestId, inventoryAuthority.Inventory);

            QuestSourceSceneBinding destination = binding.GetComponent<QuestSourceSceneBinding>();
            authorizedQuestSourceId = destination?.QuestSourceId ?? string.Empty;
            if (destination == null)
            {
                return NetworkNarrativeCommandResult.Success(command, NarrativePresentationAction.InteractionConfirmed, invocation.Message);
            }

            if (destination.IsGuildDeskSurface)
            {
                return NetworkNarrativeCommandResult.Success(
                    command,
                    NarrativePresentationAction.OpenGuildDesk,
                    invocation.Message,
                    destination.InteractionPointId,
                    destination.QuestSourceId);
            }

            if (destination.OpensConversation)
            {
                DialogueFlowOperationResult started = narrative.StartConversation(
                    destination.ConversationDefinitionId,
                    destination.ProviderPersonId,
                    destination.InteractionPointId,
                    destination.HostLocationId,
                    destination.QuestSourceId);
                if (!started.Succeeded || started.Snapshot == null)
                    return Reject(command, NarrativeAuthorityFailure.DialogueRejected, started.Message);
                activeServerFlowId = started.Snapshot.FlowId;
                return NetworkNarrativeCommandResult.Success(
                    command,
                    NarrativePresentationAction.OpenConversation,
                    started.Message,
                    destination.InteractionPointId,
                    activeServerFlowId);
            }

            return NetworkNarrativeCommandResult.Success(
                command,
                NarrativePresentationAction.OpenQuestSource,
                invocation.Message,
                destination.InteractionPointId,
                destination.QuestSourceId);
        }

        private NetworkNarrativeCommandResult BrowseQuestSource(NetworkNarrativeCommand command)
        {
            if (!HasAuthorizedSource(command.PrimaryIdText))
                return Reject(command, NarrativeAuthorityFailure.UnauthorizedContext, "Interact with this quest source before browsing it.");
            QuestSourceBrowseResult result = narrative.BrowseSource(command.PrimaryIdText, authorizedBinding.LogicalId);
            return result.Succeeded
                ? NetworkNarrativeCommandResult.Success(command, NarrativePresentationAction.OpenQuestSource, result.Message, authorizedBinding.LogicalId, command.PrimaryIdText)
                : Reject(command, NarrativeAuthorityFailure.QuestRejected, result.Message);
        }

        private NetworkNarrativeCommandResult AcceptQuestListing(NetworkNarrativeCommand command)
        {
            if (!HasAuthorizedSource(command.SecondaryIdText))
                return Reject(command, NarrativeAuthorityFailure.UnauthorizedContext, "Interact with this quest source before accepting a posting.");
            QuestSourceOperationResult result = narrative.AcceptListing(command.PrimaryIdText, authorizedBinding.LogicalId, inventoryAuthority.Inventory);
            return result.Succeeded
                ? NetworkNarrativeCommandResult.Success(command, NarrativePresentationAction.RefreshQuestJournal, result.Message)
                : Reject(command, NarrativeAuthorityFailure.QuestRejected, result.Message);
        }

        private NetworkNarrativeCommandResult AbandonQuest(NetworkNarrativeCommand command)
        {
            QuestParticipationOperationResult result = narrative.AbandonAssignment(command.PrimaryIdText);
            return result.Succeeded
                ? NetworkNarrativeCommandResult.Success(command, NarrativePresentationAction.RefreshQuestJournal, result.Message)
                : Reject(command, NarrativeAuthorityFailure.QuestRejected, result.Message);
        }

        private NetworkNarrativeCommandResult ClaimQuestReward(NetworkNarrativeCommand command)
        {
            return Reject(
                command,
                NarrativeAuthorityFailure.DeferredTransaction,
                "Online reward claims remain disabled until rewards can be delivered atomically to authoritative inventory and currency state.");
        }

        private NetworkNarrativeCommandResult SelectDialogueChoice(NetworkNarrativeCommand command)
        {
            if (string.IsNullOrWhiteSpace(activeServerFlowId))
                return Reject(command, NarrativeAuthorityFailure.UnauthorizedContext, "No authoritative conversation is active.");
            if (narrative.Dialogue.TryGetSnapshot(activeServerFlowId, null, out DialogueFlowSnapshot current)
                && current.VisibleChoices.Any(choice => string.Equals(choice.ChoiceId, command.PrimaryIdText, StringComparison.Ordinal)
                    && choice.Category == DialogueChoiceCategory.RewardClaim))
            {
                return Reject(
                    command,
                    NarrativeAuthorityFailure.DeferredTransaction,
                    "Online reward claims remain disabled until rewards can be delivered atomically to authoritative inventory and currency state.");
            }

            DialogueFlowOperationResult result = narrative.SelectDialogueChoice(activeServerFlowId, command.PrimaryIdText);
            if (!result.Succeeded) return Reject(command, NarrativeAuthorityFailure.DialogueRejected, result.Message);
            bool ended = result.Snapshot == null || ConversationFlowLifecycle.IsFlowTerminal(result.Snapshot.State);
            if (ended) activeServerFlowId = string.Empty;
            return NetworkNarrativeCommandResult.Success(
                command,
                ended ? NarrativePresentationAction.CloseConversation : NarrativePresentationAction.OpenConversation,
                result.Message,
                command.PrimaryIdText,
                activeServerFlowId);
        }

        private NetworkNarrativeCommandResult EndDialogue(NetworkNarrativeCommand command)
        {
            if (string.IsNullOrWhiteSpace(activeServerFlowId))
                return NetworkNarrativeCommandResult.Success(command, NarrativePresentationAction.CloseConversation, "Conversation is already closed.");
            DialogueFlowOperationResult result = narrative.EndDialogue(activeServerFlowId);
            if (!result.Succeeded) return Reject(command, NarrativeAuthorityFailure.DialogueRejected, result.Message);
            activeServerFlowId = string.Empty;
            return NetworkNarrativeCommandResult.Success(command, NarrativePresentationAction.CloseConversation, result.Message);
        }

        private NetworkNarrativeCommandResult CreateParty(NetworkNarrativeCommand command)
        {
            string partyId = $"party.network.{Sanitize(actor.PlayerId)}.{command.Sequence}";
            PartyOperationResult result = services.AdventuringParties.CreateParty(
                partyId,
                string.IsNullOrWhiteSpace(command.PrimaryIdText) ? "Adventuring Party" : command.PrimaryIdText,
                personId,
                WorldTime,
                Transaction(command, "create"));
            return result.Succeeded
                ? NetworkNarrativeCommandResult.Success(command, NarrativePresentationAction.RefreshParty, result.Message, partyId)
                : Reject(command, NarrativeAuthorityFailure.PartyRejected, result.Message);
        }

        private NetworkNarrativeCommandResult InvitePartyMember(NetworkNarrativeCommand command)
        {
            PartySnapshot party = CurrentParty();
            if (party == null) return Reject(command, NarrativeAuthorityFailure.PartyRejected, "Create or join a party first.");
            bool succeeded = services.PartyOperations.Invite(
                party.PartyId,
                personId,
                command.PrimaryIdText,
                WorldTime,
                Transaction(command, "invite"),
                out string message);
            return PartyResult(command, succeeded, message);
        }

        private NetworkNarrativeCommandResult AcceptPartyInvitation(NetworkNarrativeCommand command)
            => PartyResult(command, services.PartyOperations.AcceptInvitation(command.PrimaryIdText, personId, WorldTime, Transaction(command, "accept")));

        private NetworkNarrativeCommandResult DeclinePartyInvitation(NetworkNarrativeCommand command)
        {
            bool succeeded = services.PartyOperations.ResolveInvitation(
                command.PrimaryIdText,
                personId,
                PartyInvitationStatus.Declined,
                WorldTime,
                out string message);
            return PartyResult(command, succeeded, message);
        }

        private NetworkNarrativeCommandResult RemovePartyMember(NetworkNarrativeCommand command)
        {
            PartySnapshot party = CurrentParty();
            if (party == null) return Reject(command, NarrativeAuthorityFailure.PartyRejected, "No active party was found.");
            return PartyResult(command, services.AdventuringParties.RemoveMember(
                party.PartyId,
                personId,
                command.PrimaryIdText,
                WorldTime,
                Transaction(command, "remove")));
        }

        private NetworkNarrativeCommandResult TransferPartyLeadership(NetworkNarrativeCommand command)
        {
            PartySnapshot party = CurrentParty();
            if (party == null) return Reject(command, NarrativeAuthorityFailure.PartyRejected, "No active party was found.");
            return PartyResult(command, services.AdventuringParties.TransferLeadership(
                party.PartyId,
                personId,
                command.PrimaryIdText,
                WorldTime,
                Transaction(command, "leadership")));
        }

        private NetworkNarrativeCommandResult SetPartyReady(NetworkNarrativeCommand command)
        {
            PartySnapshot party = CurrentParty();
            if (party == null) return Reject(command, NarrativeAuthorityFailure.PartyRejected, "No active party was found.");
            PartyMemberOperationalData current = services.PartyOperations.GetMember(party.PartyId, personId);
            services.PartyOperations.ReportMemberState(
                party.PartyId,
                personId,
                command.Value != 0 ? PartyMemberReadiness.Ready : PartyMemberReadiness.Busy,
                current?.locationId ?? string.Empty,
                current?.distanceToLeader ?? 0f,
                true,
                current?.alive ?? true,
                current?.conscious ?? true);
            return NetworkNarrativeCommandResult.Success(command, NarrativePresentationAction.RefreshParty, "Party readiness updated.");
        }

        private NetworkNarrativeCommandResult SetPartySettings(NetworkNarrativeCommand command)
        {
            PartySnapshot party = CurrentParty();
            if (party == null) return Reject(command, NarrativeAuthorityFailure.PartyRejected, "No active party was found.");
            UnpackSettings(command.Value, out PartyFormation formation, out PartyLootPolicy loot, out PartyFriendlyFirePolicy friendly, out PartyCommand partyCommand);
            bool succeeded = services.PartyOperations.SetSettings(party.PartyId, personId, formation, loot, friendly, partyCommand, out string message);
            return PartyResult(command, succeeded, message);
        }

        private NetworkNarrativeCommandResult LeaveParty(NetworkNarrativeCommand command)
        {
            PartySnapshot party = CurrentParty();
            if (party == null) return Reject(command, NarrativeAuthorityFailure.PartyRejected, "No active party was found.");
            return PartyResult(command, services.AdventuringParties.RemoveMember(
                party.PartyId,
                personId,
                personId,
                WorldTime,
                Transaction(command, "leave")));
        }

        private NetworkNarrativeCommandResult DissolveParty(NetworkNarrativeCommand command)
        {
            PartySnapshot party = CurrentParty();
            if (party == null) return Reject(command, NarrativeAuthorityFailure.PartyRejected, "No active party was found.");
            return PartyResult(command, services.AdventuringParties.DissolveParty(
                party.PartyId,
                personId,
                WorldTime,
                Transaction(command, "dissolve")));
        }

        private PartySnapshot CurrentParty() => services.AdventuringParties.GetPartyForPerson(personId);
        private void OnAuthoritativeItemAdded(ItemDefinition item, int quantity)
            => narrative?.HandleAuthoritativeInventoryItemAdded(item, quantity, inventoryAuthority?.Inventory);
        private double WorldTime => services.PlayTime?.CumulativeSeconds ?? Time.realtimeSinceStartupAsDouble;
        private bool HasAuthorizedSource(string sourceId) => authorizedBinding != null
            && Time.realtimeSinceStartupAsDouble <= authorizationExpiresAt
            && authorizedBinding.IsWithinAuthoritativeRange(transform.position)
            && string.Equals(authorizedQuestSourceId, sourceId, StringComparison.Ordinal);

        private static NetworkNarrativeCommandResult PartyResult(NetworkNarrativeCommand command, PartyOperationResult result)
            => PartyResult(command, result?.Succeeded == true, result?.Message ?? "Party operation failed.");
        private static NetworkNarrativeCommandResult PartyResult(NetworkNarrativeCommand command, bool succeeded, string message)
            => succeeded
                ? NetworkNarrativeCommandResult.Success(command, NarrativePresentationAction.RefreshParty, message)
                : Reject(command, NarrativeAuthorityFailure.PartyRejected, message);
        private static NetworkNarrativeCommandResult Reject(NetworkNarrativeCommand command, NarrativeAuthorityFailure failure, string message)
            => NetworkNarrativeCommandResult.Reject(command, failure, message);
        private string Transaction(NetworkNarrativeCommand command, string action)
            => $"network.{Sanitize(actor.PlayerId)}.{action}.{command.Sequence}";
        private static string Sanitize(string value) => new string((value ?? string.Empty).ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) || character == '.' || character == '-' ? character : '-')
            .ToArray()).Trim('-');

        private static void UnpackSettings(
            int packed,
            out PartyFormation formation,
            out PartyLootPolicy loot,
            out PartyFriendlyFirePolicy friendly,
            out PartyCommand command)
        {
            formation = (PartyFormation)(packed & 0xff);
            loot = (PartyLootPolicy)((packed >> 8) & 0xff);
            friendly = (PartyFriendlyFirePolicy)((packed >> 16) & 0xff);
            command = (PartyCommand)((packed >> 24) & 0x7f);
        }
    }
}
