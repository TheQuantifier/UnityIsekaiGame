using System;
using Unity.Netcode;
using UnityEngine;

namespace UnityIsekaiGame.Networking
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkPlayerCombat : NetworkBehaviour
    {
        private readonly NetworkVariable<uint> lastAcceptedCommandSequence = new NetworkVariable<uint>(
            0u,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<NetworkCombatCommandResult> latestCommandResult = new NetworkVariable<NetworkCombatCommandResult>(
            default,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);

        private uint localCommandSequence;
        private readonly TokenBucketRateLimiter commandRateLimiter = new TokenBucketRateLimiter(12d, 12d);

        public event Action<NetworkCombatCommandResult> CommandResultChanged;
        public Func<NetworkCombatCommand, NetworkCombatCommandResult> ServerCommandHandler { get; set; }
        public uint LastAcceptedCommandSequence => lastAcceptedCommandSequence.Value;
        public uint LastSubmittedCommandSequence => localCommandSequence;
        public NetworkCombatCommandResult LatestCommandResult => latestCommandResult.Value;

        public override void OnNetworkSpawn()
        {
            latestCommandResult.OnValueChanged += OnCommandResultChanged;
        }

        public override void OnNetworkDespawn()
        {
            latestCommandResult.OnValueChanged -= OnCommandResultChanged;
            ServerCommandHandler = null;
            localCommandSequence = 0u;
            commandRateLimiter.Reset();
        }

        public bool RequestPrimaryAttack(Vector3 aimDirection)
            => SubmitLocalCommand(CombatAuthorityCommandType.PrimaryAttack, aimDirection, string.Empty);

        public bool RequestAbility(string abilityId, Vector3 aimDirection)
            => SubmitLocalCommand(CombatAuthorityCommandType.CastAbility, aimDirection, abilityId);

        public bool PublishServerResult(NetworkCombatCommandResult result)
        {
            if (!IsSpawned || !IsServer || result.Sequence == 0u) return false;
            latestCommandResult.Value = result;
            return true;
        }

        private bool SubmitLocalCommand(CombatAuthorityCommandType commandType, Vector3 aimDirection, string actionId)
        {
            if (!IsSpawned || !IsOwner || IsServer) return false;
            Vector3 normalized = aimDirection.sqrMagnitude > 0.0001f ? aimDirection.normalized : Vector3.forward;
            localCommandSequence = NextSequence(localCommandSequence);
            SubmitCombatCommandRpc(new NetworkCombatCommand(localCommandSequence, commandType, normalized, actionId));
            return true;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner, Delivery = RpcDelivery.Reliable)]
        private void SubmitCombatCommandRpc(NetworkCombatCommand command, RpcParams rpcParams = default)
        {
            if (!IsServer || rpcParams.Receive.SenderClientId != OwnerClientId) return;
            if (!commandRateLimiter.TryConsume(Time.realtimeSinceStartupAsDouble))
            {
                latestCommandResult.Value = NetworkCombatCommandResult.Reject(
                    command.Sequence,
                    CombatAuthorityFailure.ServerRejected,
                    "Combat commands are arriving too quickly.",
                    command.ActionIdText);
                return;
            }

            CombatCommandValidationResult validation = NetworkCombatCommandValidator.Validate(command, lastAcceptedCommandSequence.Value);
            if (!validation.Succeeded)
            {
                latestCommandResult.Value = NetworkCombatCommandResult.Reject(command.Sequence, validation.Failure, validation.Message, command.ActionIdText);
                return;
            }

            lastAcceptedCommandSequence.Value = command.Sequence;
            NetworkCombatCommandResult result = ServerCommandHandler == null
                ? NetworkCombatCommandResult.Reject(command.Sequence, CombatAuthorityFailure.ServerRejected, "Server combat authority is unavailable.", command.ActionIdText)
                : ServerCommandHandler(command);
            latestCommandResult.Value = result.Sequence == command.Sequence
                ? result
                : NetworkCombatCommandResult.Reject(command.Sequence, CombatAuthorityFailure.ServerRejected, "Server returned an invalid combat command result.", command.ActionIdText);
        }

        private static uint NextSequence(uint sequence)
        {
            uint next = unchecked(sequence + 1u);
            return next == 0u ? 1u : next;
        }

        private void OnCommandResultChanged(NetworkCombatCommandResult previous, NetworkCombatCommandResult current)
        {
            if (current.Sequence != 0u) CommandResultChanged?.Invoke(current);
        }
    }
}
