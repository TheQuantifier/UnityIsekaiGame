using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace UnityIsekaiGame.Networking
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkPlayerActor : NetworkBehaviour
    {
        private readonly NetworkVariable<FixedString128Bytes> sessionId = new NetworkVariable<FixedString128Bytes>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<FixedString128Bytes> clientInstanceId = new NetworkVariable<FixedString128Bytes>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<FixedString128Bytes> playerId = new NetworkVariable<FixedString128Bytes>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<FixedString128Bytes> actorId = new NetworkVariable<FixedString128Bytes>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<long> sessionRevision = new NetworkVariable<long>(
            0L,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private FixedString128Bytes configuredSessionId;
        private FixedString128Bytes configuredClientInstanceId;
        private FixedString128Bytes configuredPlayerId;
        private FixedString128Bytes configuredActorId;
        private long configuredSessionRevision;
        private bool hasConfiguredIdentity;

        public event Action<NetworkPlayerActor> IdentityChanged;

        public string SessionId => (IsSpawned ? sessionId.Value : configuredSessionId).ToString();
        public string ClientInstanceId => (IsSpawned ? clientInstanceId.Value : configuredClientInstanceId).ToString();
        public string PlayerId => (IsSpawned ? playerId.Value : configuredPlayerId).ToString();
        public string ActorId => (IsSpawned ? actorId.Value : configuredActorId).ToString();
        public long SessionRevision => IsSpawned ? sessionRevision.Value : configuredSessionRevision;
        public bool HasIdentity => !string.IsNullOrWhiteSpace(SessionId)
            && !string.IsNullOrWhiteSpace(PlayerId)
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
            configuredActorId = session.ActorId;
            configuredSessionRevision = session.Revision;
            hasConfiguredIdentity = true;
        }

        public override void OnNetworkSpawn()
        {
            sessionId.OnValueChanged += OnIdentityValueChanged;
            clientInstanceId.OnValueChanged += OnIdentityValueChanged;
            playerId.OnValueChanged += OnIdentityValueChanged;
            actorId.OnValueChanged += OnIdentityValueChanged;
            sessionRevision.OnValueChanged += OnRevisionChanged;

            if (IsServer)
            {
                if (!hasConfiguredIdentity)
                {
                    throw new InvalidOperationException("Server player identity was not configured before network spawn.");
                }

                sessionId.Value = configuredSessionId;
                clientInstanceId.Value = configuredClientInstanceId;
                playerId.Value = configuredPlayerId;
                actorId.Value = configuredActorId;
                sessionRevision.Value = configuredSessionRevision;
            }

            IdentityChanged?.Invoke(this);
        }

        public override void OnNetworkDespawn()
        {
            sessionId.OnValueChanged -= OnIdentityValueChanged;
            clientInstanceId.OnValueChanged -= OnIdentityValueChanged;
            playerId.OnValueChanged -= OnIdentityValueChanged;
            actorId.OnValueChanged -= OnIdentityValueChanged;
            sessionRevision.OnValueChanged -= OnRevisionChanged;
        }

        private void OnIdentityValueChanged(FixedString128Bytes previous, FixedString128Bytes current) => IdentityChanged?.Invoke(this);
        private void OnRevisionChanged(long previous, long current) => IdentityChanged?.Invoke(this);
    }
}
