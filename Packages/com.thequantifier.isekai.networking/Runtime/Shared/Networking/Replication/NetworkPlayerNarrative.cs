using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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
        private uint lastDeliveredSnapshotGeneration;
        private readonly TokenBucketRateLimiter commandRateLimiter = new TokenBucketRateLimiter(6d, 6d);
        private string lastPublishedSnapshotJson = string.Empty;

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
        public string AuthoritativeSnapshotJson => TryBuildSnapshotJson(out string json) ? json : string.Empty;

        public override void OnNetworkSpawn()
        {
            latestCommandResult.OnValueChanged += OnCommandResultChanged;
            snapshotGeneration.OnValueChanged += OnSnapshotGenerationChanged;
            authoritativeSnapshotChunks.OnListChanged += OnSnapshotChunksChanged;
            TryPublishSnapshot();
        }

        public override void OnNetworkDespawn()
        {
            latestCommandResult.OnValueChanged -= OnCommandResultChanged;
            snapshotGeneration.OnValueChanged -= OnSnapshotGenerationChanged;
            authoritativeSnapshotChunks.OnListChanged -= OnSnapshotChunksChanged;
            ServerCommandHandler = null;
            localCommandSequence = 0u;
            lastDeliveredSnapshotGeneration = 0u;
            commandRateLimiter.Reset();
            lastPublishedSnapshotJson = string.Empty;
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
            if (string.Equals(snapshotJson, lastPublishedSnapshotJson, StringComparison.Ordinal)) return true;
            const int maximumChunks = 32;
            uint generation = NextSequence(snapshotGeneration.Value);
            string[] chunks = NetworkNarrativeSnapshotCodec.CreateChunks(snapshotJson, generation);
            if (chunks.Length > maximumChunks)
            {
                Debug.LogError($"Authoritative narrative snapshot exceeds the {maximumChunks}-chunk replication budget.", this);
                return false;
            }

            authoritativeSnapshotChunks.Clear();
            for (int i = 0; i < chunks.Length; i++) authoritativeSnapshotChunks.Add(new FixedString4096Bytes(chunks[i]));
            snapshotGeneration.Value = generation;
            authoritativeRevision.Value = NextSequence(authoritativeRevision.Value);
            lastPublishedSnapshotJson = snapshotJson;
            return true;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner, Delivery = RpcDelivery.Reliable)]
        private void SubmitNarrativeCommandRpc(NetworkNarrativeCommand command, RpcParams rpcParams = default)
        {
            if (!IsServer || rpcParams.Receive.SenderClientId != OwnerClientId) return;
            if (!commandRateLimiter.TryConsume(Time.realtimeSinceStartupAsDouble))
            {
                latestCommandResult.Value = NetworkNarrativeCommandResult.Reject(
                    command,
                    NarrativeAuthorityFailure.ServerRejected,
                    "Narrative commands are arriving too quickly.");
                return;
            }

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
            TryPublishSnapshot();
        }

        private void OnSnapshotChunksChanged(NetworkListEvent<FixedString4096Bytes> change)
        {
            TryPublishSnapshot();
        }

        private void TryPublishSnapshot()
        {
            uint generation = snapshotGeneration.Value;
            if (!IsOwner || generation == 0u || generation == lastDeliveredSnapshotGeneration) return;
            if (!TryBuildSnapshotJson(out string json)) return;
            lastDeliveredSnapshotGeneration = generation;
            AuthoritativeSnapshotChanged?.Invoke(json);
        }

        private bool TryBuildSnapshotJson(out string json)
        {
            List<string> chunks = new List<string>(authoritativeSnapshotChunks.Count);
            for (int i = 0; i < authoritativeSnapshotChunks.Count; i++)
                chunks.Add(authoritativeSnapshotChunks[i].ToString());
            return NetworkNarrativeSnapshotCodec.TryAssemble(chunks, snapshotGeneration.Value, out json);
        }
    }

    /// <summary>
    /// Frames narrative snapshot chunks with their committed generation. NetworkVariable and
    /// NetworkList deltas can arrive in either order, so a client must never parse chunks until
    /// every chunk belongs to the generation announced by the server.
    /// </summary>
    public static class NetworkNarrativeSnapshotCodec
    {
        private const string EnvelopePrefix = "n1|";
        private const int EnvelopeReserveBytes = 96;

        public static string[] CreateChunks(string snapshotJson, uint generation)
        {
            if (string.IsNullOrWhiteSpace(snapshotJson)) throw new ArgumentException("Snapshot JSON is required.", nameof(snapshotJson));
            if (generation == 0u) throw new ArgumentOutOfRangeException(nameof(generation));
            string encoded = Encode(snapshotJson);
            List<string> payloads = Split(encoded);
            string[] chunks = new string[payloads.Count];
            for (int i = 0; i < payloads.Count; i++)
                chunks[i] = $"{EnvelopePrefix}{generation}|{i}|{payloads.Count}|{payloads[i]}";
            return chunks;
        }

        public static bool TryAssemble(IReadOnlyList<string> chunks, uint generation, out string snapshotJson)
        {
            snapshotJson = string.Empty;
            if (generation == 0u || chunks == null || chunks.Count == 0) return false;

            string[] payloads = null;
            bool[] received = null;
            int expectedCount = -1;
            for (int i = 0; i < chunks.Count; i++)
            {
                if (!TryParseEnvelope(chunks[i], out uint chunkGeneration, out int index, out int count, out string payload)
                    || chunkGeneration != generation
                    || count <= 0
                    || count > 32
                    || index < 0
                    || index >= count)
                    return false;

                if (expectedCount < 0)
                {
                    expectedCount = count;
                    payloads = new string[count];
                    received = new bool[count];
                }
                else if (count != expectedCount)
                {
                    return false;
                }

                if (received[index]) return false;
                received[index] = true;
                payloads[index] = payload;
            }

            if (expectedCount != chunks.Count) return false;
            for (int i = 0; i < received.Length; i++)
                if (!received[i]) return false;

            string decoded = Decode(string.Concat(payloads));
            if (string.IsNullOrWhiteSpace(decoded)) return false;
            snapshotJson = decoded;
            return true;
        }

        private static bool TryParseEnvelope(string chunk, out uint generation, out int index, out int count, out string payload)
        {
            generation = 0u;
            index = -1;
            count = 0;
            payload = string.Empty;
            if (string.IsNullOrEmpty(chunk) || !chunk.StartsWith(EnvelopePrefix, StringComparison.Ordinal)) return false;

            int generationEnd = chunk.IndexOf('|', EnvelopePrefix.Length);
            int indexEnd = generationEnd < 0 ? -1 : chunk.IndexOf('|', generationEnd + 1);
            int countEnd = indexEnd < 0 ? -1 : chunk.IndexOf('|', indexEnd + 1);
            if (generationEnd < 0 || indexEnd < 0 || countEnd < 0) return false;
            if (!uint.TryParse(chunk.Substring(EnvelopePrefix.Length, generationEnd - EnvelopePrefix.Length), out generation)) return false;
            if (!int.TryParse(chunk.Substring(generationEnd + 1, indexEnd - generationEnd - 1), out index)) return false;
            if (!int.TryParse(chunk.Substring(indexEnd + 1, countEnd - indexEnd - 1), out count)) return false;
            payload = chunk.Substring(countEnd + 1);
            return true;
        }

        private static string Encode(string value)
        {
            byte[] input = Encoding.UTF8.GetBytes(value);
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, System.IO.Compression.CompressionLevel.Fastest, true))
                gzip.Write(input, 0, input.Length);
            string compressed = "gz:" + Convert.ToBase64String(output.ToArray());
            string raw = "raw:" + value;
            return compressed.Length < raw.Length ? compressed : raw;
        }

        private static string Decode(string value)
        {
            if (value.StartsWith("raw:", StringComparison.Ordinal)) return value.Substring(4);
            if (!value.StartsWith("gz:", StringComparison.Ordinal)) return string.Empty;
            try
            {
                byte[] compressed = Convert.FromBase64String(value.Substring(3));
                using var input = new MemoryStream(compressed);
                using var gzip = new GZipStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                gzip.CopyTo(output);
                return Encoding.UTF8.GetString(output.ToArray());
            }
            catch
            {
                return string.Empty;
            }
        }

        private static List<string> Split(string value)
        {
            List<string> chunks = new List<string>();
            int offset = 0;
            int maximumPayloadBytes = FixedString4096Bytes.UTF8MaxLengthInBytes - EnvelopeReserveBytes;
            while (offset < value.Length)
            {
                int length = Math.Min(3000, value.Length - offset);
                while (length > 0 && Encoding.UTF8.GetByteCount(value.Substring(offset, length)) > maximumPayloadBytes) length--;
                if (length <= 0) throw new InvalidOperationException("Narrative snapshot contains an unsupported UTF-8 sequence.");
                chunks.Add(value.Substring(offset, length));
                offset += length;
            }
            return chunks;
        }
    }
}
