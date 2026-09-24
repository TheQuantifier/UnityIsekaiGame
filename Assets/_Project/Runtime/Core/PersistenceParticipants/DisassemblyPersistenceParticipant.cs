using System;
using System.Collections.Generic;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Inventory.Disassembly;

namespace UnityIsekaiGame.Persistence
{
    public sealed class DisassemblyPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "world.item-recovery";
        public const int CurrentParticipantSchemaVersion = 2;
        private readonly DisassemblyRuntime runtime;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly string worldId;

        public DisassemblyPersistenceParticipant(DisassemblyRuntime runtime, Func<DefinitionRegistry> registryProvider, string worldId)
        { this.runtime = runtime; this.registryProvider = registryProvider; this.worldId = worldId ?? string.Empty; }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => false;
        public PersistenceScope Scope => PersistenceScope.SharedWorld;
        public string OwnerId => string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.Inventory;
        public int LoadPriority => 46;
        public IReadOnlyList<string> RequiredDependencies => new[] { ItemInstanceIdentityPersistenceParticipant.Key, ItemCompositionPersistenceParticipant.Key };
        public IReadOnlyList<string> OptionalDependencies => new[] { ItemQualityAffixPersistenceParticipant.Key, ItemDurabilityPersistenceParticipant.Key };
        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => true;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (runtime == null) return PersistenceParticipantSaveResult.Failure("Item recovery runtime is missing.");
            DisassemblyRuntimeSaveData data = runtime.CreateSaveData();
            if (!DisassemblyRuntime.ValidateSaveData(data, registryProvider?.Invoke(), out string failure)) return PersistenceParticipantSaveResult.Failure(failure);
            return PersistenceParticipantSaveResult.Success(PersistenceSerialization.Serialize(data));
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion) return PersistenceParticipantPrepareResult.Failure($"Unsupported item recovery participant schema version {payloadSchemaVersion}.");
            try
            {
                DisassemblyRuntimeSaveData data = PersistenceSerialization.Deserialize<DisassemblyRuntimeSaveData>(payloadJson);
                return DisassemblyRuntime.ValidateSaveData(data, registryProvider?.Invoke(), out string failure)
                    ? PersistenceParticipantPrepareResult.Success(new PreparedPayload(data.Clone()))
                    : PersistenceParticipantPrepareResult.Failure(failure);
            }
            catch (Exception) { return PersistenceParticipantPrepareResult.Failure("Item recovery payload is malformed JSON."); }
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null || preparedPayload is not PreparedPayload prepared) return PersistenceParticipantCommitResult.Failure("Prepared item recovery payload is unavailable or invalid.");
            DisassemblyRuntimeSaveData rollback = runtime.CreateSaveData();
            DisassemblyResult result = runtime.RestoreFromSaveData(prepared.Data, registryProvider?.Invoke());
            if (result.Succeeded) return PersistenceParticipantCommitResult.Success("Item recovery operations restored.");
            runtime.RestoreFromSaveData(rollback, registryProvider?.Invoke());
            return PersistenceParticipantCommitResult.Failure($"Item recovery restore failed; rollback attempted: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload) { }
        private sealed class PreparedPayload { public PreparedPayload(DisassemblyRuntimeSaveData data) { Data = data; } public DisassemblyRuntimeSaveData Data { get; } }
    }
}
