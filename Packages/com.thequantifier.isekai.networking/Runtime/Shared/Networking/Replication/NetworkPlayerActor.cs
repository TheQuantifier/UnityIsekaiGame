using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace UnityIsekaiGame.Networking
{
    public enum NetworkPlayerWorldParticipationState : byte
    {
        Active = 0,
        PausedProtected = 1
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkPlayerActor : NetworkBehaviour
    {
        private readonly NetworkVariable<FixedString128Bytes> sessionId = new NetworkVariable<FixedString128Bytes>(
            default,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<FixedString128Bytes> clientInstanceId = new NetworkVariable<FixedString128Bytes>(
            default,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<FixedString128Bytes> playerId = new NetworkVariable<FixedString128Bytes>(
            default,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<FixedString128Bytes> personId = new NetworkVariable<FixedString128Bytes>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<FixedString128Bytes> actorId = new NetworkVariable<FixedString128Bytes>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<long> sessionRevision = new NetworkVariable<long>(
            0L,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<NetworkPlayerWorldParticipationState> worldParticipationState =
            new NetworkVariable<NetworkPlayerWorldParticipationState>(
                NetworkPlayerWorldParticipationState.Active,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private FixedString128Bytes configuredSessionId;
        private FixedString128Bytes configuredClientInstanceId;
        private FixedString128Bytes configuredPlayerId;
        private FixedString128Bytes configuredPersonId;
        private FixedString128Bytes configuredActorId;
        private long configuredSessionRevision;
        private bool hasConfiguredIdentity;
        private readonly TokenBucketRateLimiter worldParticipationRateLimiter = new TokenBucketRateLimiter(4d, 2d);
        private uint backgroundTraceSequence;
        private double nextBackgroundTraceAt;

        private const double BackgroundTraceIntervalSeconds = 0.5d;

        public event Action<NetworkPlayerActor> IdentityChanged;
        public event Action<NetworkPlayerActor, NetworkPlayerWorldParticipationState, NetworkPlayerWorldParticipationState>
            WorldParticipationStateChanged;

        public string SessionId => (IsSpawned ? sessionId.Value : configuredSessionId).ToString();
        public string ClientInstanceId => (IsSpawned ? clientInstanceId.Value : configuredClientInstanceId).ToString();
        public string PlayerId => (IsSpawned ? playerId.Value : configuredPlayerId).ToString();
        public string PersonId => (IsSpawned ? personId.Value : configuredPersonId).ToString();
        public string ActorId => (IsSpawned ? actorId.Value : configuredActorId).ToString();
        public long SessionRevision => IsSpawned ? sessionRevision.Value : configuredSessionRevision;
        public NetworkPlayerWorldParticipationState WorldParticipationState => IsSpawned
            ? worldParticipationState.Value
            : NetworkPlayerWorldParticipationState.Active;
        public bool IsWorldParticipationActive => WorldParticipationState == NetworkPlayerWorldParticipationState.Active;
        public bool IsPausedProtected => WorldParticipationState == NetworkPlayerWorldParticipationState.PausedProtected;
        public bool HasIdentity => !string.IsNullOrWhiteSpace(SessionId)
            && !string.IsNullOrWhiteSpace(PlayerId)
            && !string.IsNullOrWhiteSpace(PersonId)
            && !string.IsNullOrWhiteSpace(ActorId);

        public void ConfigureServer(PlayerSessionSnapshot session)
        {
            if (session == null || session.Phase != PlayerSessionPhase.Active)
            {
                throw new ArgumentException("An active player session is required.", nameof(session));
            }

            if (IsSpawned)
            {
                throw new InvalidOperationException("Player actor identity must be configured before the NetworkObject is spawned.");
            }

            configuredSessionId = session.SessionId;
            configuredClientInstanceId = session.ClientInstanceId;
            configuredPlayerId = session.PlayerId;
            configuredPersonId = session.PersonId;
            configuredActorId = session.ActorId;
            configuredSessionRevision = session.Revision;
            hasConfiguredIdentity = true;
        }

        public override void OnNetworkSpawn()
        {
            sessionId.OnValueChanged += OnIdentityValueChanged;
            clientInstanceId.OnValueChanged += OnIdentityValueChanged;
            playerId.OnValueChanged += OnIdentityValueChanged;
            personId.OnValueChanged += OnIdentityValueChanged;
            actorId.OnValueChanged += OnIdentityValueChanged;
            sessionRevision.OnValueChanged += OnRevisionChanged;
            worldParticipationState.OnValueChanged += OnWorldParticipationStateChanged;

            if (IsServer)
            {
                if (!hasConfiguredIdentity)
                {
                    throw new InvalidOperationException("Server player identity was not configured before network spawn.");
                }

                sessionId.Value = configuredSessionId;
                clientInstanceId.Value = configuredClientInstanceId;
                playerId.Value = configuredPlayerId;
                personId.Value = configuredPersonId;
                actorId.Value = configuredActorId;
                sessionRevision.Value = configuredSessionRevision;
                worldParticipationState.Value = NetworkPlayerWorldParticipationState.Active;
            }

            IdentityChanged?.Invoke(this);
            WorldParticipationStateChanged?.Invoke(
                this,
                worldParticipationState.Value,
                worldParticipationState.Value);
        }

        public override void OnNetworkDespawn()
        {
            sessionId.OnValueChanged -= OnIdentityValueChanged;
            clientInstanceId.OnValueChanged -= OnIdentityValueChanged;
            playerId.OnValueChanged -= OnIdentityValueChanged;
            personId.OnValueChanged -= OnIdentityValueChanged;
            actorId.OnValueChanged -= OnIdentityValueChanged;
            sessionRevision.OnValueChanged -= OnRevisionChanged;
            worldParticipationState.OnValueChanged -= OnWorldParticipationStateChanged;
            worldParticipationRateLimiter.Reset();
            backgroundTraceSequence = 0;
            nextBackgroundTraceAt = 0d;
        }

        private void Update()
        {
            if (!IsSpawned || !IsClient || !IsOwner || IsServer || !HasIdentity)
            {
                return;
            }

            if (!NetworkActionTrace.IsEnabled)
            {
                nextBackgroundTraceAt = 0d;
                return;
            }

            double now = Time.realtimeSinceStartupAsDouble;
            if (now < nextBackgroundTraceAt)
            {
                return;
            }

            nextBackgroundTraceAt = now + BackgroundTraceIntervalSeconds;
            uint sequence = ++backgroundTraceSequence;
            string correlation = NetworkActionTrace.Correlation(ActorId, sequence);
            NetworkActionTrace.ClientSend(
                NetworkActionTraceCategory.System,
                "BackgroundProbe",
                correlation,
                "background",
                this);
            TraceBackgroundProbeRpc(sequence);
        }

        public bool RequestPausedProtected(bool paused)
        {
            if (!IsSpawned || !IsOwner)
            {
                return false;
            }

            NetworkPlayerWorldParticipationState requestedState = paused
                ? NetworkPlayerWorldParticipationState.PausedProtected
                : NetworkPlayerWorldParticipationState.Active;
            if (IsServer)
            {
                return SetWorldParticipationStateServer(requestedState);
            }

            NetworkActionTrace.ClientSend(
                NetworkActionTraceCategory.UI,
                requestedState.ToString(),
                $"{ActorId}:{requestedState}",
                context: this);
            RequestWorldParticipationStateRpc(requestedState);
            return true;
        }

        public bool SetWorldParticipationStateServer(NetworkPlayerWorldParticipationState state)
        {
            if (!IsSpawned || !IsServer || !IsKnownWorldParticipationState(state))
            {
                return false;
            }

            if (worldParticipationState.Value == state)
            {
                return true;
            }

            worldParticipationState.Value = state;
            return true;
        }

        public static bool IsKnownWorldParticipationState(NetworkPlayerWorldParticipationState state)
            => state == NetworkPlayerWorldParticipationState.Active
               || state == NetworkPlayerWorldParticipationState.PausedProtected;

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner, Delivery = RpcDelivery.Reliable)]
        private void RequestWorldParticipationStateRpc(
            NetworkPlayerWorldParticipationState requestedState,
            RpcParams rpcParams = default)
        {
            if (!IsServer
                || rpcParams.Receive.SenderClientId != OwnerClientId
                || !IsKnownWorldParticipationState(requestedState)
                || !worldParticipationRateLimiter.TryConsume(Time.realtimeSinceStartupAsDouble))
            {
                return;
            }

            NetworkActionTrace.ServerReceive(
                NetworkActionTraceCategory.UI,
                requestedState.ToString(),
                $"{ActorId}:{requestedState}",
                rpcParams.Receive.SenderClientId,
                context: this);
            SetWorldParticipationStateServer(requestedState);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner, Delivery = RpcDelivery.Unreliable)]
        private void TraceBackgroundProbeRpc(uint sequence, RpcParams rpcParams = default)
        {
            if (!IsServer || rpcParams.Receive.SenderClientId != OwnerClientId)
            {
                return;
            }

            NetworkActionTrace.ServerReceive(
                NetworkActionTraceCategory.System,
                "BackgroundProbe",
                NetworkActionTrace.Correlation(ActorId, sequence),
                rpcParams.Receive.SenderClientId,
                "background",
                this);
        }

        private void OnIdentityValueChanged(FixedString128Bytes previous, FixedString128Bytes current) => IdentityChanged?.Invoke(this);
        private void OnRevisionChanged(long previous, long current) => IdentityChanged?.Invoke(this);
        private void OnWorldParticipationStateChanged(
            NetworkPlayerWorldParticipationState previous,
            NetworkPlayerWorldParticipationState current)
            => WorldParticipationStateChanged?.Invoke(this, previous, current);
    }
}
