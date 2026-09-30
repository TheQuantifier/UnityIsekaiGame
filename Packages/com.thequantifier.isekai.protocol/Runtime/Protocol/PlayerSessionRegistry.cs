using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityIsekaiGame.Networking
{
    public enum PlayerSessionPhase
    {
        Active = 1,
        Disconnected = 2
    }

    public sealed class PlayerSessionSnapshot : IEquatable<PlayerSessionSnapshot>
    {
        public PlayerSessionSnapshot(
            string sessionId,
            ulong clientId,
            string clientInstanceId,
            string playerId,
            string personId,
            string actorId,
            PlayerSessionPhase phase,
            long startedAtUnixMilliseconds,
            long endedAtUnixMilliseconds,
            long revision)
        {
            SessionId = sessionId ?? string.Empty;
            ClientId = clientId;
            ClientInstanceId = clientInstanceId ?? string.Empty;
            PlayerId = playerId ?? string.Empty;
            PersonId = personId ?? string.Empty;
            ActorId = actorId ?? string.Empty;
            Phase = phase;
            StartedAtUnixMilliseconds = startedAtUnixMilliseconds;
            EndedAtUnixMilliseconds = endedAtUnixMilliseconds;
            Revision = revision;
        }

        public string SessionId { get; }
        public ulong ClientId { get; }
        public string ClientInstanceId { get; }
        public string PlayerId { get; }
        public string PersonId { get; }
        public string ActorId { get; }
        public PlayerSessionPhase Phase { get; }
        public long StartedAtUnixMilliseconds { get; }
        public long EndedAtUnixMilliseconds { get; }
        public long Revision { get; }

        public bool Equals(PlayerSessionSnapshot other) =>
            other != null
            && string.Equals(SessionId, other.SessionId, StringComparison.Ordinal)
            && ClientId == other.ClientId
            && string.Equals(ClientInstanceId, other.ClientInstanceId, StringComparison.Ordinal)
            && string.Equals(PlayerId, other.PlayerId, StringComparison.Ordinal)
            && string.Equals(PersonId, other.PersonId, StringComparison.Ordinal)
            && string.Equals(ActorId, other.ActorId, StringComparison.Ordinal)
            && StartedAtUnixMilliseconds == other.StartedAtUnixMilliseconds
            && EndedAtUnixMilliseconds == other.EndedAtUnixMilliseconds
            && Revision == other.Revision
            && Phase == other.Phase;

        public override bool Equals(object obj) => Equals(obj as PlayerSessionSnapshot);
        public override int GetHashCode() => HashCode.Combine(SessionId, ClientId, Revision, (int)Phase);
    }

    public sealed class PlayerSessionRegistry
    {
        private readonly Dictionary<ulong, PlayerSessionSnapshot> sessionsByClientId = new Dictionary<ulong, PlayerSessionSnapshot>();
        private readonly Dictionary<string, ulong> clientIdsByPlayerId = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);
        private long revision;

        public int Count => sessionsByClientId.Count;
        public long Revision => revision;
        public IReadOnlyList<PlayerSessionSnapshot> ActiveSessions => sessionsByClientId.Values.OrderBy(session => session.ClientId).ToArray();

        public bool TryOpen(ulong clientId, ConnectionRequestPayload request, out PlayerSessionSnapshot session, out string failure)
        {
            session = null;
            if (!LocalConnectionProtocol.Validate(request, out failure))
            {
                return false;
            }

            if (sessionsByClientId.ContainsKey(clientId))
            {
                failure = $"Client {clientId} already owns an active player session.";
                return false;
            }

            if (clientIdsByPlayerId.ContainsKey(request.PlayerId))
            {
                failure = $"Player '{request.PlayerId}' already owns an active player session.";
                return false;
            }

            long nextRevision = checked(revision + 1L);
            string canonicalPlayerId = request.PlayerId.ToLowerInvariant();
            session = new PlayerSessionSnapshot(
                $"session.{clientId}.{nextRevision}",
                clientId,
                request.ClientInstanceId,
                request.PlayerId,
                $"person.player.{canonicalPlayerId}",
                $"actor.player.{canonicalPlayerId}",
                PlayerSessionPhase.Active,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                0L,
                nextRevision);
            sessionsByClientId.Add(clientId, session);
            clientIdsByPlayerId.Add(request.PlayerId, clientId);
            revision = nextRevision;
            failure = string.Empty;
            return true;
        }

        public bool TryClose(ulong clientId, out PlayerSessionSnapshot closedSession)
        {
            closedSession = null;
            if (!sessionsByClientId.TryGetValue(clientId, out PlayerSessionSnapshot active))
            {
                return false;
            }

            sessionsByClientId.Remove(clientId);
            clientIdsByPlayerId.Remove(active.PlayerId);
            long nextRevision = checked(revision + 1L);
            closedSession = new PlayerSessionSnapshot(
                active.SessionId,
                active.ClientId,
                active.ClientInstanceId,
                active.PlayerId,
                active.PersonId,
                active.ActorId,
                PlayerSessionPhase.Disconnected,
                active.StartedAtUnixMilliseconds,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                nextRevision);
            revision = nextRevision;
            return true;
        }

        public bool TryGetByClientId(ulong clientId, out PlayerSessionSnapshot session) =>
            sessionsByClientId.TryGetValue(clientId, out session);

        public bool TryGetByPlayerId(string playerId, out PlayerSessionSnapshot session)
        {
            session = null;
            return !string.IsNullOrWhiteSpace(playerId)
                && clientIdsByPlayerId.TryGetValue(playerId, out ulong clientId)
                && sessionsByClientId.TryGetValue(clientId, out session);
        }

        public void Clear()
        {
            if (sessionsByClientId.Count == 0)
            {
                return;
            }

            sessionsByClientId.Clear();
            clientIdsByPlayerId.Clear();
            revision = checked(revision + 1L);
        }
    }
}
