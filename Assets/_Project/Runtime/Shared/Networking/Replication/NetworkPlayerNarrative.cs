using System;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace UnityIsekaiGame.Networking
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkPlayerNarrative : NetworkBehaviour
    {
        private readonly NetworkVariable<uint> lastAcceptedCommandSequence = new NetworkVariable<uint>(
            0u,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<NetworkNarrativeCommandResult> latestCommandResult = new NetworkVariable<NetworkNarrativeCommandResult>(
            default,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<uint> authoritativeRevision = new NetworkVariable<uint>(
            0u,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<uint> snapshotGeneration = new NetworkVariable<uint>(
            0u,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);
        private readonly NetworkList<FixedString4096Bytes> authoritativeSnapshotChunks;

        private uint localCommandSequence;

        public NetworkPlayerNarrative()
        {
            authoritativeSnapshotChunks = new NetworkList<FixedString4096Bytes>(
                null,
                NetworkVariableReadPermission.Owner,
                NetworkVariableWritePermission.Server);
        }

        public event Action<NetworkNarrativeCommandResult> CommandResultChanged;
        public event Action<string> AuthoritativeSnapshotChanged;
        public Func<NetworkNarrativeCommand, NetworkNarrativeCommandResult> ServerCommandHandler { get; set; }
        public uint LastAcceptedCommandSequence => lastAcceptedCommandSequence.Value;
        public uint LastSubmittedCommandSequence => localCommandSequence;
        public uint AuthoritativeRevision => authoritativeRevision.Value;
        public NetworkNarrativeCommandResult LatestCommandResult => latestCommandResult.Value;
        public string AuthoritativeSnapshotJson => BuildSnapshotJson();

        public override void OnNetworkSpawn()
        {
            latestCommandResult.OnValueChanged += OnCommandResultChanged;
            snapshotGeneration.OnValueChanged += OnSnapshotGenerationChanged;
            if (IsOwner && snapshotGeneration.Value != 0u) AuthoritativeSnapshotChanged?.Invoke(BuildSnapshotJson());
        }

        public override void OnNetworkDespawn()
        {
            latestCommandResult.OnValueChanged -= OnCommandResultChanged;
            snapshotGeneration.OnValueChanged -= OnSnapshotGenerationChanged;
            ServerCommandHandler = null;
            localCommandSequence = 0u;
        }

        public bool Request(
            NarrativeAuthorityCommandType commandType,
            string primaryId = "",
            string secondaryId = "",
            int value = 0,
            int secondaryValue = 0)
        {
            if (!IsSpawned || !IsOwner || IsServer) return false;
            localCommandSequence = NextSequence(localCommandSequence);
            SubmitNarrativeCommandRpc(new NetworkNarrativeCommand(
                localCommandSequence,
                commandType,
                primaryId,
                secondaryId,
                value,
                secondaryValue));
            return true;
        }

        public bool PublishServerResult(NetworkNarrativeCommandResult result, bool stateChanged)
        {
            if (!IsSpawned || !IsServer || result.Sequence == 0u) return false;
            if (stateChanged) authoritativeRevision.Value = NextSequence(authoritativeRevision.Value);
            latestCommandResult.Value = result;
            return true;
        }

        public bool PublishServerSnapshot(string snapshotJson)
        {
            if (!IsSpawned || !IsServer || string.IsNullOrWhiteSpace(snapshotJson)) return false;
            const int maximumChunks = 16;
            string[] chunks = SplitSnapshot(snapshotJson);
            if (chunks.Length > maximumChunks)
            {
                Debug.LogError($"Authoritative narrative snapshot exceeds the {maximumChunks}-chunk replication budget.", this);
                return false;
            }

            authoritativeSnapshotChunks.Clear();
            for (int i = 0; i < chunks.Length; i++) authoritativeSnapshotChunks.Add(new FixedString4096Bytes(chunks[i]));
            snapshotGeneration.Value = NextSequence(snapshotGeneration.Value);
            authoritativeRevision.Value = NextSequence(authoritativeRevision.Value);
            return true;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner, Delivery = RpcDelivery.Reliable)]
        private void SubmitNarrativeCommandRpc(NetworkNarrativeCommand command, RpcParams rpcParams = default)
        {
            if (!IsServer || rpcParams.Receive.SenderClientId != OwnerClientId) return;

            NarrativeCommandValidationResult validation = NetworkNarrativeCommandValidator.Validate(command, lastAcceptedCommandSequence.Value);
            if (!validation.Succeeded)
            {
                latestCommandResult.Value = NetworkNarrativeCommandResult.Reject(command, validation.Failure, validation.Message);
                return;
            }

            lastAcceptedCommandSequence.Value = command.Sequence;
            NetworkNarrativeCommandResult result = ServerCommandHandler == null
                ? NetworkNarrativeCommandResult.Reject(command, NarrativeAuthorityFailure.ServerRejected, "Server narrative authority is unavailable.")
                : ServerCommandHandler(command);
            if (result.Sequence != command.Sequence || result.CommandType != command.CommandType)
            {
                result = NetworkNarrativeCommandResult.Reject(command, NarrativeAuthorityFailure.ServerRejected, "Server returned an invalid narrative command result.");
            }

            if (result.Succeeded) authoritativeRevision.Value = NextSequence(authoritativeRevision.Value);
            latestCommandResult.Value = result;
        }

        private static uint NextSequence(uint sequence)
        {
            uint next = unchecked(sequence + 1u);
            return next == 0u ? 1u : next;
        }

        private void OnCommandResultChanged(NetworkNarrativeCommandResult previous, NetworkNarrativeCommandResult current)
        {
            if (current.Sequence != 0u) CommandResultChanged?.Invoke(current);
        }

        private void OnSnapshotGenerationChanged(uint previous, uint current)
        {
            AuthoritativeSnapshotChanged?.Invoke(BuildSnapshotJson());
        }

        private string BuildSnapshotJson()
        {
            if (authoritativeSnapshotChunks.Count == 0) return string.Empty;
            StringBuilder builder = new StringBuilder(authoritativeSnapshotChunks.Count * 2048);
            for (int i = 0; i < authoritativeSnapshotChunks.Count; i++) builder.Append(authoritativeSnapshotChunks[i].ToString());
            return builder.ToString();
        }

        private static string[] SplitSnapshot(string value)
        {
            System.Collections.Generic.List<string> chunks = new System.Collections.Generic.List<string>();
            int offset = 0;
            while (offset < value.Length)
            {
                int length = Math.Min(3000, value.Length - offset);
                while (length > 0 && Encoding.UTF8.GetByteCount(value.Substring(offset, length)) > FixedString4096Bytes.UTF8MaxLengthInBytes) length--;
                if (length <= 0) throw new InvalidOperationException("Narrative snapshot contains an unsupported UTF-8 sequence.");
                chunks.Add(value.Substring(offset, length));
                offset += length;
            }
            return chunks.ToArray();
        }
    }
}
