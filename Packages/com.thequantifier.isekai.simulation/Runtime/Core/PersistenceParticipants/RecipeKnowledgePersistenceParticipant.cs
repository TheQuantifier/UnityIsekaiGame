using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Inventory.Recipes;

namespace UnityIsekaiGame.Persistence
{
    public sealed class RecipeKnowledgePersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "person.recipe-knowledge";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly RecipeKnowledgeRuntime runtime;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly string personId;

        public RecipeKnowledgePersistenceParticipant(RecipeKnowledgeRuntime runtime, Func<DefinitionRegistry> registryProvider, string personId)
        {
            this.runtime = runtime;
            this.registryProvider = registryProvider;
            this.personId = personId ?? string.Empty;
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => false;
        public PersistenceScope Scope => PersistenceScope.Player;
        public string OwnerId => string.IsNullOrWhiteSpace(personId) ? PersistenceService.LocalPlayerId : personId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.Inventory;
        public int LoadPriority => 35;
        public IReadOnlyList<string> RequiredDependencies => Array.Empty<string>();
        public IReadOnlyList<string> OptionalDependencies => new[] { PersonKnowledgePersistenceParticipant.Key, PersonMemoryPersistenceParticipant.Key, KnowledgeRecordPersistenceParticipant.Key };
        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => true;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (runtime == null)
            {
                return PersistenceParticipantSaveResult.Failure("Recipe knowledge runtime is missing.");
            }

            RecipeKnowledgeSaveData saveData = runtime.CreateSaveData();
            if (!RecipeKnowledgeRuntime.ValidateSaveData(saveData, registryProvider?.Invoke(), out string failure))
            {
                return PersistenceParticipantSaveResult.Failure(failure);
            }

            PersistenceParticipantPrepareResult prepared = PreparePayload(PersistenceSerialization.Serialize(saveData), CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Recipe knowledge snapshot failed validation.");
            }

            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(PersistenceSerialization.Serialize(saveData));
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported recipe knowledge participant schema version {payloadSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Failure("Recipe knowledge payload is empty.");
            }

            RecipeKnowledgeSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<RecipeKnowledgeSaveData>(payloadJson);
            }
            catch (Exception)
            {
                return PersistenceParticipantPrepareResult.Failure("Recipe knowledge payload is malformed JSON.");
            }

            if (!RecipeKnowledgeRuntime.ValidateSaveData(saveData, registryProvider?.Invoke(), out string failure))
            {
                return PersistenceParticipantPrepareResult.Failure(failure);
            }

            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null)
            {
                return PersistenceParticipantCommitResult.Failure("Recipe knowledge runtime is missing.");
            }

            if (preparedPayload is not PreparedPayload prepared)
            {
                return PersistenceParticipantCommitResult.Failure("Prepared recipe knowledge payload has the wrong type.");
            }

            RecipeKnowledgeSaveData rollback = runtime.CreateSaveData();
            if (runtime.RestoreFromSaveData(prepared.SaveData, registryProvider?.Invoke(), out string failure))
            {
                return PersistenceParticipantCommitResult.Success("Recipe knowledge restored.");
            }

            runtime.RestoreFromSaveData(rollback, registryProvider?.Invoke(), out _);
            return PersistenceParticipantCommitResult.Failure($"Recipe knowledge restore failed; rollback attempted: {failure}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(RecipeKnowledgeSaveData saveData)
            {
                SaveData = saveData;
            }

            public RecipeKnowledgeSaveData SaveData { get; }
        }
    }
}
