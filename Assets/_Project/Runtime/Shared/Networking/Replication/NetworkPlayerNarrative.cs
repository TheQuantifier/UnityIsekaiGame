using System;
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

        private uint localCommandSequence;

        public event Action<NetworkNarrativeCommandResult> CommandResultChanged;
        public Func<NetworkNarrativeCommand, NetworkNarrativeCommandResult> ServerCommandHandler { get; set; }
        public uint LastAcceptedCommandSequence => lastAcceptedCommandSequence.Value;
        public uint LastSubmittedCommandSequence => localCommandSequence;
        public uint AuthoritativeRevision => authoritativeRevision.Value;
        public NetworkNarrativeCommandResult LatestCommandResult => latestCommandResult.Value;

        public override void OnNetworkSpawn()
        {
            latestCommandResult.OnValueChanged += OnCommandResultChanged;
        }

        public override void OnNetworkDespawn()
        {
            latestCommandResult.OnValueChanged -= OnCommandResultChanged;
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
    }
}
