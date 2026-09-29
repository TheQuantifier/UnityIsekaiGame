using System;
using UnityEngine;
using UnityIsekaiGame.Economy.InstitutionalRevenue;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;

namespace UnityIsekaiGame.Persistence
{
    public sealed class InstitutionalRevenuePersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "world.institutional-revenue";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly InstitutionalRevenueRuntime runtime;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly string ownerId;

        public InstitutionalRevenuePersistenceParticipant(InstitutionalRevenueRuntime runtime, Func<DefinitionRegistry> registryProvider, string ownerId = PersistenceService.LocalWorldId)
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
        public int LoadPriority => 51;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => Array.Empty<string>();
        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies => new[]
        {
            EconomyPersistenceParticipant.Key,
            PayrollPersistenceParticipant.Key,
            BusinessPersistenceParticipant.Key,
            PropertyPersistenceParticipant.Key,
            ContractEconomyPersistenceParticipant.Key,
            ItemInstanceIdentityPersistenceParticipant.Key,
            KnowledgeRecordPersistenceParticipant.Key,
            InformationAccessPersistenceParticipant.Key
        };

        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => true;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (runtime == null)
            {
                return PersistenceParticipantSaveResult.Failure("Institutional revenue runtime is missing.");
            }

            InstitutionalRevenueRuntimeSaveData saveData = runtime.CreateSaveData();
            PersistenceParticipantPrepareResult prepared = PreparePayload(PersistenceSerialization.Serialize(saveData), CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Institutional revenue snapshot failed validation.");
            }

            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(PersistenceSerialization.Serialize(saveData));
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported institutional revenue participant schema version {payloadSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Failure("Institutional revenue payload is empty.");
            }

            InstitutionalRevenueRuntimeSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<InstitutionalRevenueRuntimeSaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Institutional revenue payload is malformed JSON.");
            }

            if (!InstitutionalRevenueRuntime.ValidateSaveData(saveData, registryProvider?.Invoke(), out string failure))
            {
                return PersistenceParticipantPrepareResult.Failure(failure);
            }

            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null)
            {
                return PersistenceParticipantCommitResult.Failure("Institutional revenue runtime is missing.");
            }

            if (preparedPayload is not PreparedPayload prepared)
            {
                return PersistenceParticipantCommitResult.Failure("Prepared institutional revenue payload has the wrong type.");
            }

            InstitutionalRevenueRuntimeSaveData rollback = runtime.CreateSaveData();
            InstitutionalRevenueOperationResult result = runtime.RestoreFromSaveData(prepared.SaveData, registryProvider?.Invoke());
            if (result.Succeeded)
            {
                return PersistenceParticipantCommitResult.Success("Institutional revenue runtime restored.");
            }

            runtime.RestoreFromSaveData(rollback, registryProvider?.Invoke());
            return PersistenceParticipantCommitResult.Failure($"Institutional revenue commit failed after preparation; rollback attempted: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(InstitutionalRevenueRuntimeSaveData saveData)
            {
                SaveData = saveData;
            }

            public InstitutionalRevenueRuntimeSaveData SaveData { get; }
        }
    }
}
