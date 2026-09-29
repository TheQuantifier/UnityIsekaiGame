using System;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Inventory.Identity;

namespace UnityIsekaiGame.Persistence
{
    public sealed class ItemInstanceIdentityPersistenceParticipant : IPersistenceParticipant
    {
        public const string Key = "world.item-instance-identity";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly ItemInstanceIdentityRuntime runtime;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly string ownerId;

        public ItemInstanceIdentityPersistenceParticipant(
            ItemInstanceIdentityRuntime runtime,
            Func<DefinitionRegistry> registryProvider,
            string ownerId = PersistenceService.LocalWorldId)
        {
            this.runtime = runtime;
            this.registryProvider = registryProvider;
            this.ownerId = string.IsNullOrWhiteSpace(ownerId) ? PersistenceService.LocalWorldId : ownerId;
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => false;
        public PersistenceScope Scope => PersistenceScope.SharedWorld;
        public string OwnerId => ownerId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.Inventory;
        public int LoadPriority => -10;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (runtime == null)
            {
                return PersistenceParticipantSaveResult.Failure("Item identity runtime is missing.");
            }

            ItemInstanceRuntimeSaveData saveData = runtime.CreateSaveData();
            PersistenceParticipantPrepareResult prepared = PreparePayload(PersistenceSerialization.Serialize(saveData), CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Item identity snapshot failed validation.");
            }

            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(PersistenceSerialization.Serialize(saveData));
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported item identity participant schema version {payloadSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Failure("Item identity payload is empty.");
            }

            ItemInstanceRuntimeSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<ItemInstanceRuntimeSaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Item identity payload is malformed JSON.");
            }

            DefinitionRegistry registry = registryProvider?.Invoke();
            if (!ItemInstanceIdentityRuntime.ValidateSaveData(saveData, registry, out string failureReason))
            {
                return PersistenceParticipantPrepareResult.Failure(failureReason);
            }

            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null)
            {
                return PersistenceParticipantCommitResult.Failure("Item identity runtime is missing.");
            }

            if (preparedPayload is not PreparedPayload prepared)
            {
                return PersistenceParticipantCommitResult.Failure("Prepared item identity payload has the wrong type.");
            }

            ItemInstanceRuntimeSaveData rollback = runtime.CreateSaveData();
            ItemInstanceOperationResult result = runtime.RestoreFromSaveData(prepared.SaveData, registryProvider?.Invoke());
            if (result.Succeeded)
            {
                return PersistenceParticipantCommitResult.Success("Item identities restored.");
            }

            runtime.RestoreFromSaveData(rollback, registryProvider?.Invoke());
            return PersistenceParticipantCommitResult.Failure($"Item identity commit failed after preparation; rollback attempted: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(ItemInstanceRuntimeSaveData saveData)
            {
                SaveData = saveData;
            }

            public ItemInstanceRuntimeSaveData SaveData { get; }
        }
    }
}
