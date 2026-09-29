using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityIsekaiGame.Parties
{
    public enum PartyCommand { Follow = 0, Hold = 1, Regroup = 2, Attack = 3, Defend = 4, Support = 5 }
    public enum PartyFormation { Loose = 0, Line = 1, Wedge = 2, Circle = 3 }
    public enum PartyLootPolicy { Individual = 0, RoundRobin = 1, LeaderChoice = 2, Shared = 3 }
    public enum PartyFriendlyFirePolicy { Prevent = 0, Allow = 1 }
    public enum PartyMemberReadiness { Ready = 0, TooFar = 1, DifferentLocation = 2, Busy = 3, Incapacitated = 4, Dead = 5, Missing = 6 }
    public enum PartyInvitationStatus { Pending = 0, Accepted = 1, Declined = 2, Revoked = 3 }

    [Serializable]
    public sealed class PartyInvitationData
    {
        public string invitationId;
        public string partyId;
        public string inviterPersonId;
        public string invitedPersonId;
        public PartyInvitationStatus status;
        public double createdWorldTime;
        public double resolvedWorldTime = -1d;
        public PartyInvitationData Clone() => (PartyInvitationData)MemberwiseClone();
    }

    [Serializable]
    public sealed class PartySettingsData
    {
        public string partyId;
        public PartyFormation formation = PartyFormation.Wedge;
        public PartyLootPolicy lootPolicy = PartyLootPolicy.Individual;
        public PartyFriendlyFirePolicy friendlyFire = PartyFriendlyFirePolicy.Prevent;
        public PartyCommand groupCommand = PartyCommand.Follow;
        public long revision = 1;
        public PartySettingsData Clone() => (PartySettingsData)MemberwiseClone();
    }

    [Serializable]
    public sealed class PartyMemberOperationalData
    {
        public string partyId;
        public string personId;
        public PartyCommand command = PartyCommand.Follow;
        public PartyMemberReadiness readiness = PartyMemberReadiness.Missing;
        public string locationId;
        public float distanceToLeader;
        public bool loaded;
        public bool alive = true;
        public bool conscious = true;
        public int formationSlot;
        public int questContribution;
        public long revision = 1;
        public PartyMemberOperationalData Clone() => (PartyMemberOperationalData)MemberwiseClone();
    }

    [Serializable]
    public sealed class PartyOperationalSaveData
    {
        public const int CurrentSchemaVersion = 2;
        public int schemaVersion = CurrentSchemaVersion;
        public long revision;
        public List<PartyInvitationData> invitations = new List<PartyInvitationData>();
        public List<PartySettingsData> settings = new List<PartySettingsData>();
        public List<PartyMemberOperationalData> members = new List<PartyMemberOperationalData>();

        public PartyOperationalSaveData Clone() => new PartyOperationalSaveData
        {
            schemaVersion = schemaVersion,
            revision = revision,
            invitations = (invitations ?? new List<PartyInvitationData>()).Where(x => x != null).Select(x => x.Clone()).ToList(),
            settings = (settings ?? new List<PartySettingsData>()).Where(x => x != null).Select(x => x.Clone()).ToList(),
            members = (members ?? new List<PartyMemberOperationalData>()).Where(x => x != null).Select(x => x.Clone()).ToList()
        };
    }

    public sealed class PartyOperationalRuntime
    {
        private readonly Dictionary<string, PartyInvitationData> invitations = new Dictionary<string, PartyInvitationData>(StringComparer.Ordinal);
        private readonly Dictionary<string, PartySettingsData> settings = new Dictionary<string, PartySettingsData>(StringComparer.Ordinal);
        private readonly Dictionary<string, PartyMemberOperationalData> members = new Dictionary<string, PartyMemberOperationalData>(StringComparer.Ordinal);
        private AdventuringPartyService parties;

        public long Revision { get; private set; }
        public bool IsDirty { get; private set; }
        public event Action Changed;

        public void Configure(AdventuringPartyService partyService)
        {
            if (parties != null) parties.Changed -= Reconcile;
            parties = partyService;
            if (parties != null) parties.Changed += Reconcile;
            Reconcile();
        }

        public PartySettingsData GetSettings(string partyId)
        {
            partyId = N(partyId);
            if (!settings.TryGetValue(partyId, out PartySettingsData value)) value = new PartySettingsData { partyId = partyId };
            return value.Clone();
        }

        public IReadOnlyList<PartyInvitationData> QueryInvitations(string personId = "", PartyInvitationStatus? status = null)
        {
            string normalized = N(personId);
            return invitations.Values
                .Where(x => string.IsNullOrEmpty(normalized) || x.invitedPersonId == normalized || x.inviterPersonId == normalized)
                .Where(x => !status.HasValue || x.status == status.Value)
                .OrderBy(x => x.createdWorldTime).ThenBy(x => x.invitationId, StringComparer.Ordinal)
                .Select(x => x.Clone()).ToArray();
        }

        public IReadOnlyList<PartyMemberOperationalData> QueryMembers(string partyId)
        {
            string prefix = N(partyId) + "|";
            return members.Where(x => x.Key.StartsWith(prefix, StringComparison.Ordinal)).Select(x => x.Value.Clone()).OrderBy(x => x.formationSlot).ThenBy(x => x.personId, StringComparer.Ordinal).ToArray();
        }

        public PartyMemberOperationalData GetMember(string partyId, string personId)
        {
            return members.TryGetValue(MemberKey(partyId, personId), out PartyMemberOperationalData value) ? value.Clone() : null;
        }

        public IReadOnlyList<string> GetReadyMemberIds(string partyId, string leaderLocationId = "", float maximumDistance = float.PositiveInfinity)
        {
            string location = N(leaderLocationId);
            return QueryMembers(partyId)
                .Where(x => x.readiness == PartyMemberReadiness.Ready && x.alive && x.conscious && x.loaded)
                .Where(x => string.IsNullOrEmpty(location) || string.Equals(x.locationId, location, StringComparison.Ordinal))
                .Where(x => x.distanceToLeader <= maximumDistance)
                .Select(x => x.personId).ToArray();
        }

        public bool Invite(string partyId, string inviterPersonId, string invitedPersonId, double worldTime, string transactionId, out string message)
        {
            PartySnapshot party = parties?.GetParty(N(partyId));
            if (party == null) return Fail("Party was not found.", out message);
            if (party.LeaderPersonId != N(inviterPersonId)) return Fail("Only the party leader may invite companions.", out message);
            if (party.MemberCount >= party.MaximumMembers) return Fail("The party is full.", out message);
            if (party.MemberPersonIds.Contains(N(invitedPersonId), StringComparer.Ordinal)) return Fail("That person is already in the party.", out message);
            if (parties.GetPartyForPerson(N(invitedPersonId)) != null) return Fail("That person already belongs to another party.", out message);
            if (invitations.Values.Any(x => x.partyId == party.PartyId && x.invitedPersonId == N(invitedPersonId) && x.status == PartyInvitationStatus.Pending)) return Fail("An invitation is already pending.", out message);
            string id = string.IsNullOrWhiteSpace(transactionId) ? $"party-invite.{party.PartyId}.{N(invitedPersonId)}.{Revision + 1}" : N(transactionId);
            invitations[id] = new PartyInvitationData { invitationId = id, partyId = party.PartyId, inviterPersonId = party.LeaderPersonId, invitedPersonId = N(invitedPersonId), status = PartyInvitationStatus.Pending, createdWorldTime = worldTime };
            Touch(); message = "Party invitation sent."; return true;
        }

        public PartyOperationResult AcceptInvitation(string invitationId, string invitedPersonId, double worldTime, string transactionId)
        {
            if (!invitations.TryGetValue(N(invitationId), out PartyInvitationData invitation) || invitation.status != PartyInvitationStatus.Pending)
                return PartyOperationResult.Failure(PartyOperationStatus.InvalidRequest, "Pending party invitation was not found.");
            if (invitation.invitedPersonId != N(invitedPersonId)) return PartyOperationResult.Failure(PartyOperationStatus.InvalidRequest, "Only the invited person may accept this invitation.");
            PartyOperationResult result = parties.AddMember(invitation.partyId, invitation.inviterPersonId, invitation.invitedPersonId, worldTime, transactionId);
            if (!result.Succeeded) return result;
            invitation.status = PartyInvitationStatus.Accepted; invitation.resolvedWorldTime = worldTime; Touch(); Reconcile(); return result;
        }

        public bool ResolveInvitation(string invitationId, string personId, PartyInvitationStatus resolution, double worldTime, out string message)
        {
            if (resolution != PartyInvitationStatus.Declined && resolution != PartyInvitationStatus.Revoked) return Fail("Invitation can only be declined or revoked.", out message);
            if (!invitations.TryGetValue(N(invitationId), out PartyInvitationData invitation) || invitation.status != PartyInvitationStatus.Pending) return Fail("Pending invitation was not found.", out message);
            string actor = N(personId);
            if (resolution == PartyInvitationStatus.Declined && invitation.invitedPersonId != actor) return Fail("Only the invitee may decline.", out message);
            if (resolution == PartyInvitationStatus.Revoked && invitation.inviterPersonId != actor) return Fail("Only the inviter may revoke.", out message);
            invitation.status = resolution; invitation.resolvedWorldTime = worldTime; Touch(); message = resolution == PartyInvitationStatus.Declined ? "Invitation declined." : "Invitation revoked."; return true;
        }

        public bool SetSettings(string partyId, string actingPersonId, PartyFormation formation, PartyLootPolicy lootPolicy, PartyFriendlyFirePolicy friendlyFire, PartyCommand groupCommand, out string message)
        {
            PartySnapshot party = parties?.GetParty(N(partyId));
            if (party == null) return Fail("Party was not found.", out message);
            if (party.LeaderPersonId != N(actingPersonId)) return Fail("Only the party leader may change party settings.", out message);
            PartySettingsData value = GetSettings(party.PartyId);
            value.formation = formation; value.lootPolicy = lootPolicy; value.friendlyFire = friendlyFire; value.groupCommand = groupCommand; value.revision++;
            settings[party.PartyId] = value;
            foreach (PartyMemberOperationalData member in QueryMembers(party.PartyId))
            {
                member.command = groupCommand; member.revision++; members[MemberKey(member.partyId, member.personId)] = member;
            }
            Touch(); message = "Party settings updated."; return true;
        }

        public bool SetMemberCommand(string partyId, string actingPersonId, string memberPersonId, PartyCommand command, out string message)
        {
            PartySnapshot party = parties?.GetParty(N(partyId));
            if (party == null) return Fail("Party was not found.", out message);
            if (party.LeaderPersonId != N(actingPersonId) && N(actingPersonId) != N(memberPersonId)) return Fail("Only the leader may command another party member.", out message);
            string key = MemberKey(party.PartyId, memberPersonId);
            if (!members.TryGetValue(key, out PartyMemberOperationalData value)) return Fail("Party member was not found.", out message);
            value.command = command; value.revision++; Touch(); message = "Party command updated."; return true;
        }

        public void ReportMemberState(string partyId, string personId, PartyMemberReadiness readiness, string locationId, float distanceToLeader, bool loaded, bool alive, bool conscious)
        {
            string key = MemberKey(partyId, personId);
            if (!members.TryGetValue(key, out PartyMemberOperationalData value)) return;
            if (value.readiness == readiness && value.locationId == N(locationId) && Math.Abs(value.distanceToLeader - distanceToLeader) < 0.05f && value.loaded == loaded && value.alive == alive && value.conscious == conscious) return;
            value.readiness = readiness; value.locationId = N(locationId); value.distanceToLeader = Math.Max(0f, distanceToLeader); value.loaded = loaded; value.alive = alive; value.conscious = conscious; value.revision++; Touch();
        }

        public void RecordContribution(string partyId, string personId, int amount = 1)
        {
            if (amount <= 0 || !members.TryGetValue(MemberKey(partyId, personId), out PartyMemberOperationalData value)) return;
            value.questContribution += amount; value.revision++; Touch();
        }

        public void CompleteTravel(string partyId, string destinationLocationId)
        {
            foreach (PartyMemberOperationalData value in QueryMembers(partyId))
            {
                value.locationId = N(destinationLocationId); value.distanceToLeader = 0f; value.readiness = value.alive && value.conscious ? PartyMemberReadiness.Ready : value.alive ? PartyMemberReadiness.Incapacitated : PartyMemberReadiness.Dead; value.revision++;
                members[MemberKey(value.partyId, value.personId)] = value;
            }
            Touch();
        }

        public PartyOperationalSaveData CreateSaveData() => new PartyOperationalSaveData { revision = Revision, invitations = invitations.Values.Select(x => x.Clone()).ToList(), settings = settings.Values.Select(x => x.Clone()).ToList(), members = members.Values.Select(x => x.Clone()).ToList() };

        public bool Restore(PartyOperationalSaveData data, out string message)
        {
            if (!TryMigrate(data, out PartyOperationalSaveData migrated, out message) || !Validate(migrated, out message)) return false;
            invitations.Clear(); settings.Clear(); members.Clear();
            foreach (PartyInvitationData x in migrated.invitations) invitations[x.invitationId] = x.Clone();
            foreach (PartySettingsData x in migrated.settings) settings[x.partyId] = x.Clone();
            foreach (PartyMemberOperationalData x in migrated.members) members[MemberKey(x.partyId, x.personId)] = x.Clone();
            Revision = migrated.revision; IsDirty = false; Reconcile(false); message = "Party operations restored."; Changed?.Invoke(); return true;
        }

        public static bool TryMigrate(PartyOperationalSaveData source, out PartyOperationalSaveData result, out string message)
        {
            result = source?.Clone(); message = string.Empty;
            if (result == null) { message = "Party operational save data is missing."; return false; }
            if (result.schemaVersion == 1)
            {
                result.invitations ??= new List<PartyInvitationData>();
                result.schemaVersion = PartyOperationalSaveData.CurrentSchemaVersion;
            }
            if (result.schemaVersion != PartyOperationalSaveData.CurrentSchemaVersion) { message = $"Unsupported party operational schema version {result.schemaVersion}."; return false; }
            return true;
        }

        public static bool Validate(PartyOperationalSaveData data, out string message)
        {
            if (data == null) { message = "Party operational save data is missing."; return false; }
            if (data.schemaVersion != PartyOperationalSaveData.CurrentSchemaVersion) { message = "Party operational save data has an unsupported schema."; return false; }
            if ((data.settings ?? new List<PartySettingsData>()).Any(x => x == null || string.IsNullOrWhiteSpace(x.partyId))) { message = "Party settings contain an empty party ID."; return false; }
            if ((data.settings ?? new List<PartySettingsData>()).GroupBy(x => x.partyId, StringComparer.Ordinal).Any(x => x.Count() > 1)) { message = "Party settings contain duplicate party IDs."; return false; }
            if ((data.members ?? new List<PartyMemberOperationalData>()).Any(x => x == null || string.IsNullOrWhiteSpace(x.partyId) || string.IsNullOrWhiteSpace(x.personId))) { message = "Party member operations contain an empty party or Person ID."; return false; }
            if ((data.members ?? new List<PartyMemberOperationalData>()).GroupBy(x => MemberKey(x.partyId, x.personId), StringComparer.Ordinal).Any(x => x.Count() > 1)) { message = "Party member operations contain duplicate members."; return false; }
            if ((data.invitations ?? new List<PartyInvitationData>()).Any(x => x == null || string.IsNullOrWhiteSpace(x.invitationId) || string.IsNullOrWhiteSpace(x.partyId) || string.IsNullOrWhiteSpace(x.invitedPersonId))) { message = "Party invitations contain incomplete records."; return false; }
            if ((data.invitations ?? new List<PartyInvitationData>()).GroupBy(x => x.invitationId, StringComparer.Ordinal).Any(x => x.Count() > 1)) { message = "Party invitations contain duplicate IDs."; return false; }
            message = string.Empty; return true;
        }

        public void MarkClean() => IsDirty = false;
        public void Dispose() { if (parties != null) parties.Changed -= Reconcile; parties = null; invitations.Clear(); settings.Clear(); members.Clear(); }

        private void Reconcile() => Reconcile(true);
        private void Reconcile(bool dirty)
        {
            if (parties == null) return;
            HashSet<string> liveKeys = new HashSet<string>(StringComparer.Ordinal);
            bool changed = false;
            foreach (PartySnapshot party in parties.QueryParties())
            {
                if (!settings.ContainsKey(party.PartyId)) { settings[party.PartyId] = new PartySettingsData { partyId = party.PartyId }; changed = true; }
                int slot = 0;
                foreach (PartyMemberSnapshot member in party.Members)
                {
                    string key = MemberKey(party.PartyId, member.PersonId); liveKeys.Add(key);
                    if (!members.ContainsKey(key)) { members[key] = new PartyMemberOperationalData { partyId = party.PartyId, personId = member.PersonId, formationSlot = slot, command = settings[party.PartyId].groupCommand }; changed = true; }
                    else if (members[key].formationSlot != slot) { members[key].formationSlot = slot; members[key].revision++; changed = true; }
                    slot++;
                }
            }
            foreach (string key in members.Keys.Where(x => !liveKeys.Contains(x)).ToArray()) { members.Remove(key); changed = true; }
            if (changed && dirty) Touch();
        }

        private void Touch() { Revision++; IsDirty = true; Changed?.Invoke(); }
        private static bool Fail(string value, out string message) { message = value; return false; }
        private static string MemberKey(string partyId, string personId) => N(partyId) + "|" + N(personId);
        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
