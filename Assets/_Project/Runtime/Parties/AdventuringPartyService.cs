using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Social.Networks;

namespace UnityIsekaiGame.Parties
{
    public enum PartyOperationStatus
    {
        Succeeded = 0,
        InvalidRequest = 10,
        PartyNotFound = 11,
        AlreadyInParty = 12,
        NotPartyLeader = 13,
        MemberNotFound = 14,
        PartyFull = 15,
        LeadershipTransferRequired = 16,
        UnderlyingSocialNetworkFailure = 20
    }

    public sealed class PartyMemberSnapshot
    {
        public PartyMemberSnapshot(string personId, string membershipId, bool isLeader)
        {
            PersonId = personId ?? string.Empty;
            MembershipId = membershipId ?? string.Empty;
            IsLeader = isLeader;
        }

        public string PersonId { get; }
        public string MembershipId { get; }
        public bool IsLeader { get; }
    }

    public sealed class PartySnapshot
    {
        public PartySnapshot(string partyId, string displayName, IEnumerable<PartyMemberSnapshot> members, int minimumOperationalMembers, int maximumMembers)
        {
            PartyId = partyId ?? string.Empty;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? PartyId : displayName;
            Members = (members ?? Array.Empty<PartyMemberSnapshot>())
                .Where(member => member != null)
                .OrderByDescending(member => member.IsLeader)
                .ThenBy(member => member.PersonId, StringComparer.Ordinal)
                .ToArray();
            MinimumOperationalMembers = Math.Max(1, minimumOperationalMembers);
            MaximumMembers = Math.Max(MinimumOperationalMembers, maximumMembers);
        }

        public string PartyId { get; }
        public string DisplayName { get; }
        public IReadOnlyList<PartyMemberSnapshot> Members { get; }
        public int MinimumOperationalMembers { get; }
        public int MaximumMembers { get; }
        public int MemberCount => Members.Count;
        public string LeaderPersonId => Members.FirstOrDefault(member => member.IsLeader)?.PersonId ?? string.Empty;
        public IReadOnlyList<string> MemberPersonIds => Members.Select(member => member.PersonId).ToArray();
        public bool IsOperational => MemberCount >= MinimumOperationalMembers;
        public bool MeetsRequirement(int requiredPartySize) => MemberCount >= Math.Max(1, requiredPartySize);
    }

    public sealed class PartyOperationResult
    {
        private PartyOperationResult(PartyOperationStatus status, string message, PartySnapshot party)
        {
            Status = status;
            Message = message ?? string.Empty;
            Party = party;
        }

        public PartyOperationStatus Status { get; }
        public string Message { get; }
        public PartySnapshot Party { get; }
        public bool Succeeded => Status == PartyOperationStatus.Succeeded;

        public static PartyOperationResult Success(string message, PartySnapshot party) => new PartyOperationResult(PartyOperationStatus.Succeeded, message, party);
        public static PartyOperationResult Failure(PartyOperationStatus status, string message, PartySnapshot party = null) => new PartyOperationResult(status, message, party);
    }

    /// <summary>
    /// Game-facing party lifecycle built on the persisted social-group runtime. This keeps
    /// party identity, membership history, and save/load behavior in one authoritative model.
    /// </summary>
    public sealed class AdventuringPartyService
    {
        private readonly SocialNetworkRuntime networks;
        private readonly DefinitionRegistry registry;

        public AdventuringPartyService(SocialNetworkRuntime socialNetworks, DefinitionRegistry definitions)
        {
            networks = socialNetworks ?? throw new ArgumentNullException(nameof(socialNetworks));
            registry = definitions ?? throw new ArgumentNullException(nameof(definitions));
        }

        public event Action Changed;

        public PartySnapshot GetPartyForPerson(string personId)
        {
            if (string.IsNullOrWhiteSpace(personId)) return null;
            SocialGroupSnapshot group = networks.QueryGroupsByPerson(personId.Trim(), activeOnly: true)
                .FirstOrDefault(IsActiveAdventuringParty);
            return BuildSnapshot(group);
        }

        public PartySnapshot GetParty(string partyId) => BuildSnapshot(networks.Snapshot(N(partyId)));

        public IReadOnlyList<PartySnapshot> QueryParties()
        {
            return networks.QueryGroupsByDefinition(PrototypeSocialNetworkDefinitionFactory.AdventuringPartyGroupId)
                .Where(IsActiveAdventuringParty)
                .Select(BuildSnapshot)
                .Where(party => party != null)
                .OrderBy(party => party.PartyId, StringComparer.Ordinal)
                .ToArray();
        }

        public PartyOperationResult CreateParty(string partyId, string displayName, string leaderPersonId, double worldTime, string transactionId)
        {
            partyId = N(partyId);
            leaderPersonId = N(leaderPersonId);
            transactionId = N(transactionId);
            if (string.IsNullOrWhiteSpace(partyId) || string.IsNullOrWhiteSpace(leaderPersonId) || string.IsNullOrWhiteSpace(transactionId))
                return PartyOperationResult.Failure(PartyOperationStatus.InvalidRequest, "Party ID, leader Person ID, and transaction ID are required.");
            if (GetPartyForPerson(leaderPersonId) != null)
                return PartyOperationResult.Failure(PartyOperationStatus.AlreadyInParty, $"'{leaderPersonId}' is already in an active adventuring party.");

            SocialNetworkMutationResult created = networks.Mutate(new SocialGroupMutationRequest
            {
                TransactionId = $"{transactionId}.create",
                MutationKind = SocialGroupMutationKind.CreateGroup,
                GroupId = partyId,
                GroupDefinitionId = PrototypeSocialNetworkDefinitionFactory.AdventuringPartyGroupId,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? $"{leaderPersonId}'s Party" : displayName.Trim(),
                WorldTime = worldTime,
                Tags = new[] { "adventuring-party", "player-manageable" }
            });
            if (!created.Succeeded) return FromNetworkFailure(created);

            SocialNetworkMutationResult leaderAdded = networks.Mutate(new SocialGroupMutationRequest
            {
                TransactionId = $"{transactionId}.leader",
                MutationKind = SocialGroupMutationKind.AddMembership,
                GroupId = partyId,
                MembershipId = MembershipId(partyId, leaderPersonId, transactionId),
                PersonId = leaderPersonId,
                RoleId = PrototypeSocialNetworkDefinitionFactory.LeaderRoleId,
                WorldTime = worldTime,
                SourceRecordId = transactionId,
                Tags = new[] { "party-founder" }
            });
            if (!leaderAdded.Succeeded)
            {
                networks.Mutate(new SocialGroupMutationRequest
                {
                    TransactionId = $"{transactionId}.rollback",
                    MutationKind = SocialGroupMutationKind.DissolveGroup,
                    GroupId = partyId,
                    WorldTime = worldTime
                });
                return FromNetworkFailure(leaderAdded);
            }

            PartySnapshot party = GetParty(partyId);
            Changed?.Invoke();
            return PartyOperationResult.Success("Party created.", party);
        }

        public PartyOperationResult AddMember(string partyId, string actingLeaderPersonId, string memberPersonId, double worldTime, string transactionId)
        {
            PartySnapshot party = GetParty(partyId);
            if (party == null) return PartyOperationResult.Failure(PartyOperationStatus.PartyNotFound, $"Party '{N(partyId)}' was not found.");
            if (!string.Equals(party.LeaderPersonId, N(actingLeaderPersonId), StringComparison.Ordinal)) return PartyOperationResult.Failure(PartyOperationStatus.NotPartyLeader, "Only the party leader may add a member.", party);
            memberPersonId = N(memberPersonId);
            if (string.IsNullOrWhiteSpace(memberPersonId) || string.IsNullOrWhiteSpace(transactionId)) return PartyOperationResult.Failure(PartyOperationStatus.InvalidRequest, "Member Person ID and transaction ID are required.", party);
            if (GetPartyForPerson(memberPersonId) != null) return PartyOperationResult.Failure(PartyOperationStatus.AlreadyInParty, $"'{memberPersonId}' is already in an active adventuring party.", party);
            if (party.MemberCount >= party.MaximumMembers) return PartyOperationResult.Failure(PartyOperationStatus.PartyFull, $"Party '{party.PartyId}' is full ({party.MaximumMembers} members).", party);

            SocialNetworkMutationResult added = networks.Mutate(new SocialGroupMutationRequest
            {
                TransactionId = N(transactionId),
                MutationKind = SocialGroupMutationKind.AddMembership,
                GroupId = party.PartyId,
                MembershipId = MembershipId(party.PartyId, memberPersonId, transactionId),
                PersonId = memberPersonId,
                RoleId = PrototypeSocialNetworkDefinitionFactory.CompanionRoleId,
                WorldTime = worldTime,
                SourceRecordId = transactionId,
                Tags = new[] { "party-companion" }
            });
            if (!added.Succeeded) return FromNetworkFailure(added, party);
            Changed?.Invoke();
            return PartyOperationResult.Success("Party member added.", GetParty(party.PartyId));
        }

        public PartyOperationResult RemoveMember(string partyId, string actingPersonId, string memberPersonId, double worldTime, string transactionId)
        {
            PartySnapshot party = GetParty(partyId);
            if (party == null) return PartyOperationResult.Failure(PartyOperationStatus.PartyNotFound, $"Party '{N(partyId)}' was not found.");
            actingPersonId = N(actingPersonId);
            memberPersonId = N(memberPersonId);
            PartyMemberSnapshot member = party.Members.FirstOrDefault(value => string.Equals(value.PersonId, memberPersonId, StringComparison.Ordinal));
            if (member == null) return PartyOperationResult.Failure(PartyOperationStatus.MemberNotFound, $"'{memberPersonId}' is not an active member of party '{party.PartyId}'.", party);
            bool actorIsLeader = string.Equals(party.LeaderPersonId, actingPersonId, StringComparison.Ordinal);
            if (!actorIsLeader && !string.Equals(actingPersonId, memberPersonId, StringComparison.Ordinal)) return PartyOperationResult.Failure(PartyOperationStatus.NotPartyLeader, "Only the leader may remove another party member.", party);
            if (member.IsLeader)
            {
                if (party.MemberCount > 1) return PartyOperationResult.Failure(PartyOperationStatus.LeadershipTransferRequired, "Transfer leadership before the current leader leaves the party.", party);
                return DissolveParty(party.PartyId, actingPersonId, worldTime, transactionId);
            }

            SocialNetworkMutationResult removed = networks.Mutate(new SocialGroupMutationRequest
            {
                TransactionId = N(transactionId),
                MutationKind = SocialGroupMutationKind.EndMembership,
                MembershipId = member.MembershipId,
                WorldTime = worldTime
            });
            if (!removed.Succeeded) return FromNetworkFailure(removed, party);
            Changed?.Invoke();
            return PartyOperationResult.Success("Party member removed.", GetParty(party.PartyId));
        }

        public PartyOperationResult TransferLeadership(string partyId, string actingLeaderPersonId, string newLeaderPersonId, double worldTime, string transactionId)
        {
            PartySnapshot party = GetParty(partyId);
            if (party == null) return PartyOperationResult.Failure(PartyOperationStatus.PartyNotFound, $"Party '{N(partyId)}' was not found.");
            if (!string.Equals(party.LeaderPersonId, N(actingLeaderPersonId), StringComparison.Ordinal)) return PartyOperationResult.Failure(PartyOperationStatus.NotPartyLeader, "Only the current party leader may transfer leadership.", party);
            PartyMemberSnapshot currentLeader = party.Members.First(member => member.IsLeader);
            PartyMemberSnapshot newLeader = party.Members.FirstOrDefault(member => string.Equals(member.PersonId, N(newLeaderPersonId), StringComparison.Ordinal));
            if (newLeader == null) return PartyOperationResult.Failure(PartyOperationStatus.MemberNotFound, "The new leader must already be an active party member.", party);
            if (newLeader.IsLeader) return PartyOperationResult.Success("Party member is already the leader.", party);

            SocialNetworkMutationResult demoted = ChangeRole(currentLeader.MembershipId, PrototypeSocialNetworkDefinitionFactory.CompanionRoleId, worldTime, $"{N(transactionId)}.demote");
            if (!demoted.Succeeded) return FromNetworkFailure(demoted, party);
            SocialNetworkMutationResult promoted = ChangeRole(newLeader.MembershipId, PrototypeSocialNetworkDefinitionFactory.LeaderRoleId, worldTime, $"{N(transactionId)}.promote");
            if (!promoted.Succeeded)
            {
                ChangeRole(currentLeader.MembershipId, PrototypeSocialNetworkDefinitionFactory.LeaderRoleId, worldTime, $"{N(transactionId)}.rollback");
                return FromNetworkFailure(promoted, GetParty(party.PartyId));
            }

            Changed?.Invoke();
            return PartyOperationResult.Success("Party leadership transferred.", GetParty(party.PartyId));
        }

        public PartyOperationResult DissolveParty(string partyId, string actingLeaderPersonId, double worldTime, string transactionId)
        {
            PartySnapshot party = GetParty(partyId);
            if (party == null) return PartyOperationResult.Failure(PartyOperationStatus.PartyNotFound, $"Party '{N(partyId)}' was not found.");
            if (!string.Equals(party.LeaderPersonId, N(actingLeaderPersonId), StringComparison.Ordinal)) return PartyOperationResult.Failure(PartyOperationStatus.NotPartyLeader, "Only the party leader may dissolve the party.", party);
            SocialNetworkMutationResult dissolved = networks.Mutate(new SocialGroupMutationRequest
            {
                TransactionId = N(transactionId),
                MutationKind = SocialGroupMutationKind.DissolveGroup,
                GroupId = party.PartyId,
                WorldTime = worldTime
            });
            if (!dissolved.Succeeded) return FromNetworkFailure(dissolved, party);
            Changed?.Invoke();
            return PartyOperationResult.Success("Party dissolved.", null);
        }

        private PartySnapshot BuildSnapshot(SocialGroupSnapshot group)
        {
            if (!IsActiveAdventuringParty(group)) return null;
            InformalSocialGroupDefinition definition = ResolveDefinition();
            PartyMemberSnapshot[] members = networks.QueryMembers(group.GroupId, activeOnly: true)
                .Select(member => new PartyMemberSnapshot(member.PersonId, member.MembershipId, string.Equals(member.RoleId, PrototypeSocialNetworkDefinitionFactory.LeaderRoleId, StringComparison.Ordinal)))
                .ToArray();
            return new PartySnapshot(group.GroupId, group.Data.displayName, members, definition?.MinimumMembers ?? 2, definition?.MaximumMembers ?? 8);
        }

        private bool IsActiveAdventuringParty(SocialGroupSnapshot group)
        {
            return group != null
                && group.Lifecycle == InformalSocialGroupLifecycleStatus.Active
                && string.Equals(group.GroupDefinitionId, PrototypeSocialNetworkDefinitionFactory.AdventuringPartyGroupId, StringComparison.Ordinal);
        }

        private InformalSocialGroupDefinition ResolveDefinition()
        {
            return registry.TryGet(PrototypeSocialNetworkDefinitionFactory.AdventuringPartyGroupId, out InformalSocialGroupDefinition definition) ? definition : null;
        }

        private SocialNetworkMutationResult ChangeRole(string membershipId, string roleId, double worldTime, string transactionId)
        {
            return networks.Mutate(new SocialGroupMutationRequest
            {
                TransactionId = transactionId,
                MutationKind = SocialGroupMutationKind.ChangeMembershipRole,
                MembershipId = membershipId,
                RoleId = roleId,
                WorldTime = worldTime
            });
        }

        private static PartyOperationResult FromNetworkFailure(SocialNetworkMutationResult result, PartySnapshot party = null)
        {
            PartyOperationStatus status = result?.Status == SocialNetworkOperationStatus.LimitExceeded ? PartyOperationStatus.PartyFull : PartyOperationStatus.UnderlyingSocialNetworkFailure;
            return PartyOperationResult.Failure(status, result?.Message ?? "Party operation failed in the social-network runtime.", party);
        }

        private static string MembershipId(string partyId, string personId, string transactionId) => $"membership.{Sanitize(partyId)}.{Sanitize(personId)}.{Sanitize(transactionId)}";
        private static string Sanitize(string value) => new string(N(value).ToLowerInvariant().Select(character => char.IsLetterOrDigit(character) || character == '.' || character == '-' ? character : '-').ToArray()).Trim('-');
        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
