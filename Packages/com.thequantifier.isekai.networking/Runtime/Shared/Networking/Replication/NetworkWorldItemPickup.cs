using System;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace UnityIsekaiGame.Networking
{
    public enum NetworkWorldItemStorageMode : byte
    {
        DefinitionStack = 1,
        StatefulInstance = 2
    }

    public struct NetworkWorldItemState : INetworkSerializable, IEquatable<NetworkWorldItemState>
    {
        public NetworkWorldItemState(
            string definitionId,
            string itemInstanceId,
            string displayName,
            int quantity,
            NetworkWorldItemStorageMode storageMode)
        {
            DefinitionId = definitionId ?? string.Empty;
            ItemInstanceId = itemInstanceId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Quantity = quantity;
            StorageMode = storageMode;
        }

        public FixedString128Bytes DefinitionId;
        public FixedString128Bytes ItemInstanceId;
        public FixedString128Bytes DisplayName;
        public int Quantity;
        public NetworkWorldItemStorageMode StorageMode;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref DefinitionId);
            serializer.SerializeValue(ref ItemInstanceId);
            serializer.SerializeValue(ref DisplayName);
            serializer.SerializeValue(ref Quantity);
            serializer.SerializeValue(ref StorageMode);
        }

        public bool Equals(NetworkWorldItemState other)
        {
            return DefinitionId.Equals(other.DefinitionId)
                && ItemInstanceId.Equals(other.ItemInstanceId)
                && DisplayName.Equals(other.DisplayName)
                && Quantity == other.Quantity
                && StorageMode == other.StorageMode;
        }

        public override bool Equals(object obj) => obj is NetworkWorldItemState other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(DefinitionId, ItemInstanceId, DisplayName, Quantity, (byte)StorageMode);
    }

    public readonly struct WorldItemCollectionResult
    {
        private WorldItemCollectionResult(bool succeeded, string message)
        {
            Succeeded = succeeded;
            Message = message ?? string.Empty;
        }

        public bool Succeeded { get; }
        public string Message { get; }

        public static WorldItemCollectionResult Success(string message) => new WorldItemCollectionResult(true, message);
        public static WorldItemCollectionResult Reject(string message) => new WorldItemCollectionResult(false, message);
    }

    public static class NetworkWorldItemStateValidator
    {
        public static bool TryValidate(NetworkWorldItemState state, out string failure)
        {
            string definitionId = state.DefinitionId.ToString();
            string itemInstanceId = state.ItemInstanceId.ToString();
            if (string.IsNullOrWhiteSpace(definitionId))
            {
                failure = "A world pickup requires an item definition ID.";
                return false;
            }

            if (Encoding.UTF8.GetByteCount(definitionId) > InventoryAuthorityLimits.MaximumDefinitionIdBytes)
            {
                failure = "The world pickup definition ID exceeds the network limit.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(itemInstanceId))
            {
                failure = "A world pickup requires an exact item instance ID.";
                return false;
            }

            if (Encoding.UTF8.GetByteCount(itemInstanceId) > InventoryAuthorityLimits.MaximumItemInstanceIdBytes)
            {
                failure = "The world pickup item instance ID exceeds the network limit.";
                return false;
            }

            if (state.Quantity < 1 || state.Quantity > InventoryAuthorityLimits.MaximumQuantity)
            {
                failure = "The world pickup quantity is outside the supported range.";
                return false;
            }

            if (!Enum.IsDefined(typeof(NetworkWorldItemStorageMode), state.StorageMode))
            {
                failure = "The world pickup storage mode is invalid.";
                return false;
            }

            failure = string.Empty;
            return true;
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject), typeof(CapsuleCollider))]
    public sealed class NetworkWorldItemPickup : NetworkBehaviour
    {
        private readonly NetworkVariable<NetworkWorldItemState> state = new NetworkVariable<NetworkWorldItemState>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private NetworkWorldItemState configuredState;
        private bool hasConfiguredState;
        private bool collectionInProgress;
        private bool localRequestPending;

        public static event Action<NetworkWorldItemPickup> ClientPickupSpawned;
        public event Action<bool, string> CollectionResultReceived;
        public Func<ulong, NetworkWorldItemPickup, WorldItemCollectionResult> ServerCollectionHandler { get; set; }
        public NetworkWorldItemState State => state.Value;
        public int Quantity => state.Value.Quantity;
        public string DefinitionId => state.Value.DefinitionId.ToString();
        public string ItemInstanceId => state.Value.ItemInstanceId.ToString();
        public NetworkWorldItemStorageMode StorageMode => state.Value.StorageMode;
        public bool CanRequestCollection => enabled && isActiveAndEnabled && IsSpawned && !IsServer && state.Value.Quantity > 0 && !localRequestPending;
        public string InteractionPrompt
        {
            get
            {
                string displayName = state.Value.DisplayName.ToString();
                if (string.IsNullOrWhiteSpace(displayName)) displayName = "item";
                return state.Value.Quantity > 1 ? $"Pick up {state.Value.Quantity} x {displayName}" : $"Pick up {displayName}";
            }
        }

        public void ConfigureServer(
            NetworkWorldItemState initialState,
            Func<ulong, NetworkWorldItemPickup, WorldItemCollectionResult> collectionHandler)
        {
            if (IsSpawned) throw new InvalidOperationException("World pickup state must be configured before network spawn.");
            if (!NetworkWorldItemStateValidator.TryValidate(initialState, out string failure))
                throw new ArgumentException(failure, nameof(initialState));
            configuredState = initialState;
            hasConfiguredState = true;
            ServerCollectionHandler = collectionHandler ?? throw new ArgumentNullException(nameof(collectionHandler));
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                if (!hasConfiguredState || ServerCollectionHandler == null)
                    throw new InvalidOperationException("The server must configure world pickup state and collection authority before spawn.");
                state.Value = configuredState;
            }

            if (IsClient) ClientPickupSpawned?.Invoke(this);
        }

        public override void OnNetworkDespawn()
        {
            collectionInProgress = false;
            localRequestPending = false;
            ServerCollectionHandler = null;
        }

        public bool RequestCollection()
        {
            if (!CanRequestCollection) return false;
            localRequestPending = true;
            RequestCollectionRpc();
            return true;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone, Delivery = RpcDelivery.Reliable)]
        private void RequestCollectionRpc(RpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            WorldItemCollectionResult result;
            if (!IsServer || !IsSpawned)
            {
                result = WorldItemCollectionResult.Reject("The pickup is no longer available.");
            }
            else if (collectionInProgress)
            {
                result = WorldItemCollectionResult.Reject("Another player is already collecting this item.");
            }
            else if (ServerCollectionHandler == null)
            {
                result = WorldItemCollectionResult.Reject("World item authority is unavailable.");
            }
            else
            {
                collectionInProgress = true;
                try
                {
                    result = ServerCollectionHandler(senderClientId, this);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                    result = WorldItemCollectionResult.Reject("The server could not collect this item.");
                }
                finally
                {
                    collectionInProgress = false;
                }
            }

            if (IsSpawned)
            {
                ReceiveCollectionResultRpc(
                    result.Succeeded,
                    new FixedString512Bytes(result.Message),
                    RpcTarget.Single(senderClientId, RpcTargetUse.Temp));
                if (result.Succeeded && IsSpawned) NetworkObject.Despawn(true);
            }
        }

        [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server, Delivery = RpcDelivery.Reliable)]
        private void ReceiveCollectionResultRpc(bool succeeded, FixedString512Bytes message, RpcParams rpcParams = default)
        {
            localRequestPending = false;
            string text = message.ToString();
            CollectionResultReceived?.Invoke(succeeded, text);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ClientPickupSpawned = null;
        }
    }
}
