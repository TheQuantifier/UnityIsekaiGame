using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace UnityIsekaiGame.Networking
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkCombatWorldState : NetworkBehaviour
    {
        private readonly NetworkVariable<uint> snapshotRevision = new NetworkVariable<uint>(
            0u,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkList<NetworkCombatantState> combatants = new NetworkList<NetworkCombatantState>(
            null,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public event Action<uint> SnapshotChanged;
        public uint SnapshotRevision => snapshotRevision.Value;
        public bool HasSnapshot => snapshotRevision.Value != 0u;
        public int CombatantCount => combatants.Count;

        public override void OnNetworkSpawn()
        {
            snapshotRevision.OnValueChanged += OnSnapshotRevisionChanged;
            if (!IsServer && HasSnapshot) SnapshotChanged?.Invoke(snapshotRevision.Value);
        }

        public override void OnNetworkDespawn()
        {
            snapshotRevision.OnValueChanged -= OnSnapshotRevisionChanged;
        }

        public void CopySnapshotTo(List<NetworkCombatantState> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            destination.Clear();
            for (int i = 0; i < combatants.Count; i++) destination.Add(combatants[i]);
        }

        public bool TryCopyCommittedSnapshotTo(uint expectedRevision, List<NetworkCombatantState> destination, out string failure)
        {
            if (expectedRevision == 0u || snapshotRevision.Value != expectedRevision)
            {
                failure = "The requested combat snapshot revision is no longer current.";
                return false;
            }

            CopySnapshotTo(destination);
            if (snapshotRevision.Value != expectedRevision)
            {
                destination.Clear();
                failure = "The combat snapshot changed while it was being copied.";
                return false;
            }

            return NetworkCombatantSnapshotValidator.HasCommittedGeneration(destination, expectedRevision, out failure);
        }

        public bool PublishServerSnapshot(IReadOnlyList<NetworkCombatantState> state)
        {
            if (!IsSpawned || !IsServer) return false;
            if (!NetworkCombatantSnapshotValidator.TryValidate(state, out string failure))
            {
                Debug.LogError($"[Network Combat] Refused invalid world snapshot: {failure}", this);
                return false;
            }

            uint next = NextRevision(snapshotRevision.Value);
            combatants.Clear();
            for (int i = 0; i < state.Count; i++)
            {
                NetworkCombatantState entry = state[i];
                entry.SnapshotGeneration = next;
                combatants.Add(entry);
            }
            snapshotRevision.Value = next;
            return true;
        }

        private static uint NextRevision(uint revision)
        {
            uint next = unchecked(revision + 1u);
            return next == 0u ? 1u : next;
        }

        private void OnSnapshotRevisionChanged(uint previous, uint current) => SnapshotChanged?.Invoke(current);
    }
}
