using System;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Crimes;
using UnityIsekaiGame.Diplomacy;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Governments;
using UnityIsekaiGame.Laws;
using UnityIsekaiGame.Organizations;

namespace UnityIsekaiGame.Persistence
{
    public sealed class CrimePersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "world.crimes";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly CrimeRuntime runtime;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly Func<GovernmentRuntime> governmentProvider;
        private readonly Func<LegalRuntime> legalProvider;
        private readonly Func<OrganizationAuthorityRuntime> authorityProvider;
        private readonly Func<DiplomacyRuntime> diplomacyProvider;
        private readonly Func<string[]> personProvider;
        private readonly Func<string[]> placeProvider;
        private readonly CrimeRuntimeValidationService validationService = new CrimeRuntimeValidationService();
        private readonly string ownerId;

        public CrimePersistenceParticipant(
            CrimeRuntime runtime,
            Func<DefinitionRegistry> registryProvider,
            Func<GovernmentRuntime> governmentProvider,
            Func<LegalRuntime> legalProvider,
            Func<OrganizationAuthorityRuntime> authorityProvider,
            Func<DiplomacyRuntime> diplomacyProvider,
            string ownerId = PersistenceService.LocalWorldId,
            Func<string[]> personProvider = null,
            Func<string[]> placeProvider = null)
        {
            this.runtime = runtime;
            this.registryProvider = registryProvider;
            this.governmentProvider = governmentProvider;
            this.legalProvider = legalProvider;
            this.authorityProvider = authorityProvider;
            this.diplomacyProvider = diplomacyProvider;
            this.ownerId = string.IsNullOrWhiteSpace(ownerId) ? PersistenceService.LocalWorldId : ownerId;
            this.personProvider = personProvider;
            this.placeProvider = placeProvider;
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => false;
        public PersistenceScope Scope => PersistenceScope.SharedWorld;
        public string OwnerId => ownerId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.Inventory;
        public int LoadPriority => 70;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => new[] { GovernmentPersistenceParticipant.Key, LegalPersistenceParticipant.Key };
        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies => new[]
        {
            OrganizationAuthorityPersistenceParticipant.Key,
            DiplomacyPersistenceParticipant.Key,
            InformationAccessPersistenceParticipant.Key,
            AuthoritativeHistoryPersistenceParticipant.Key,
            InformationSourcePersistenceParticipant.Key
        };
        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => true;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (runtime == null) return PersistenceParticipantSaveResult.Failure("Crime runtime is missing.");
            string payload = PersistenceSerialization.Serialize(runtime.CreateSaveData());
            PersistenceParticipantPrepareResult prepared = PreparePayload(payload, CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded) return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Crime snapshot failed validation.");
            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(payload);
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion) return PersistenceParticipantPrepareResult.Failure($"Unsupported crime participant schema version {payloadSchemaVersion}.");
            if (string.IsNullOrWhiteSpace(payloadJson)) return PersistenceParticipantPrepareResult.Failure("Crime payload is empty.");
            CrimeRuntimeSaveData saveData;
            try { saveData = PersistenceSerialization.Deserialize<CrimeRuntimeSaveData>(payloadJson); }
            catch { return PersistenceParticipantPrepareResult.Failure("Crime payload is malformed JSON."); }
            CrimeValidationReport validation = validationService.Validate(saveData, registryProvider?.Invoke(), governmentProvider?.Invoke(), legalProvider?.Invoke(), authorityProvider?.Invoke(), diplomacyProvider?.Invoke(), ownerId, personProvider?.Invoke(), placeProvider?.Invoke());
            if (!validation.IsValid) return PersistenceParticipantPrepareResult.Failure(validation.Errors.FirstOrDefault() ?? "Crime payload failed validation.");
            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null) return PersistenceParticipantCommitResult.Failure("Crime runtime is missing.");
            if (preparedPayload is not PreparedPayload prepared) return PersistenceParticipantCommitResult.Failure("Prepared crime payload has the wrong type.");
            CrimeRuntimeSaveData rollback = runtime.CreateSaveData();
            CrimeOperationResult result = Restore(prepared.SaveData);
            if (result.Succeeded) return PersistenceParticipantCommitResult.Success("Crime runtime restored without replaying crime mutations.");
            CrimeOperationResult rollbackResult = Restore(rollback);
            return PersistenceParticipantCommitResult.Failure($"Crime commit failed after preparation; rollback {(rollbackResult.Succeeded ? "succeeded" : "failed")}: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private CrimeOperationResult Restore(CrimeRuntimeSaveData saveData)
        {
            return runtime.RestoreFromSaveData(saveData, registryProvider?.Invoke(), governmentProvider?.Invoke(), legalProvider?.Invoke(), authorityProvider?.Invoke(), diplomacyProvider?.Invoke(), ownerId, personProvider?.Invoke(), placeProvider?.Invoke());
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(CrimeRuntimeSaveData saveData) { SaveData = saveData; }
            public CrimeRuntimeSaveData SaveData { get; }
        }
    }
}
