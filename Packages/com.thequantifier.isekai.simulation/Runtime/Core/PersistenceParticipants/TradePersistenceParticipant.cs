using System;
using UnityEngine;
using UnityIsekaiGame.Economy;
using UnityIsekaiGame.Economy.Markets;
using UnityIsekaiGame.Economy.Trading;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;

namespace UnityIsekaiGame.Persistence
{
    public sealed class TradePersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "world.trades";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly TradeRuntime runtime;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly string ownerId;

        public TradePersistenceParticipant(TradeRuntime runtime, Func<DefinitionRegistry> registryProvider, string ownerId = PersistenceService.LocalWorldId)
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
        public int LoadPriority => 37;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => Array.Empty<string>();
        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies => new[] { EconomyPersistenceParticipant.Key, MarketPersistenceParticipant.Key, ItemInstanceIdentityPersistenceParticipant.Key };
        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => true;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (runtime == null)
            {
                return PersistenceParticipantSaveResult.Failure("Trade runtime is missing.");
            }

            TradeRuntimeSaveData saveData = runtime.CreateSaveData();
            PersistenceParticipantPrepareResult prepared = PreparePayload(PersistenceSerialization.Serialize(saveData), CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Trade snapshot failed validation.");
            }

            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(PersistenceSerialization.Serialize(saveData));
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported trade participant schema version {payloadSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Failure("Trade payload is empty.");
            }

            TradeRuntimeSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<TradeRuntimeSaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Trade payload is malformed JSON.");
            }

            if (!TradeRuntime.ValidateSaveData(saveData, registryProvider?.Invoke(), out string failureReason))
            {
                return PersistenceParticipantPrepareResult.Failure(failureReason);
            }

            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null)
            {
                return PersistenceParticipantCommitResult.Failure("Trade runtime is missing.");
            }

            if (preparedPayload is not PreparedPayload prepared)
            {
                return PersistenceParticipantCommitResult.Failure("Prepared trade payload has the wrong type.");
            }

            TradeRuntimeSaveData rollback = runtime.CreateSaveData();
            TradeOperationResult result = runtime.RestoreFromSaveData(prepared.SaveData, registryProvider?.Invoke());
            if (result.Succeeded)
            {
                return PersistenceParticipantCommitResult.Success("Trade runtime restored.");
            }

            runtime.RestoreFromSaveData(rollback, registryProvider?.Invoke());
            return PersistenceParticipantCommitResult.Failure($"Trade commit failed after preparation; rollback attempted: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(TradeRuntimeSaveData saveData)
            {
                SaveData = saveData;
            }

            public TradeRuntimeSaveData SaveData { get; }
        }
    }
}
