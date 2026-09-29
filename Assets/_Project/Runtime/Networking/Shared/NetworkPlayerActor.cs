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

        public event Action<NetworkPlayerActor> IdentityChanged;

        public string SessionId => sessionId.Value.ToString();
        public string ClientInstanceId => clientInstanceId.Value.ToString();
        public string PlayerId => playerId.Value.ToString();
        public string ActorId => actorId.Value.ToString();
        public long SessionRevision => sessionRevision.Value;
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

            sessionId.Value = session.SessionId;
            clientInstanceId.Value = session.ClientInstanceId;
            playerId.Value = session.PlayerId;
            actorId.Value = session.ActorId;
            sessionRevision.Value = session.Revision;
        }

        public override void OnNetworkSpawn()
        {
            sessionId.OnValueChanged += OnIdentityValueChanged;
            clientInstanceId.OnValueChanged += OnIdentityValueChanged;
            playerId.OnValueChanged += OnIdentityValueChanged;
            actorId.OnValueChanged += OnIdentityValueChanged;
            sessionRevision.OnValueChanged += OnRevisionChanged;
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
