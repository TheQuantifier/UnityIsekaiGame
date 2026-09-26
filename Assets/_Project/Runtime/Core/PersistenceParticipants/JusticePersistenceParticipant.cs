using System;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Crimes;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Governments;
using UnityIsekaiGame.Justice;
using UnityIsekaiGame.Laws;
using UnityIsekaiGame.Organizations;

namespace UnityIsekaiGame.Persistence
{
    public sealed class JusticePersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "world.justice";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly JusticeRuntime runtime;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly Func<GovernmentRuntime> governmentProvider;
        private readonly Func<LegalRuntime> legalProvider;
        private readonly Func<OrganizationRuntime> organizationProvider;
        private readonly Func<OrganizationAuthorityRuntime> authorityProvider;
        private readonly Func<CrimeRuntime> crimeProvider;
        private readonly Func<string[]> personProvider;
        private readonly Func<string[]> placeProvider;
        private readonly JusticeRuntimeValidationService validationService = new JusticeRuntimeValidationService();
        private readonly string ownerId;

        public JusticePersistenceParticipant(JusticeRuntime runtime, Func<DefinitionRegistry> registryProvider, Func<GovernmentRuntime> governmentProvider, Func<LegalRuntime> legalProvider, Func<OrganizationRuntime> organizationProvider, Func<OrganizationAuthorityRuntime> authorityProvider, Func<CrimeRuntime> crimeProvider, string ownerId = PersistenceService.LocalWorldId, Func<string[]> personProvider = null, Func<string[]> placeProvider = null)
        {
            this.runtime = runtime;
            this.registryProvider = registryProvider;
            this.governmentProvider = governmentProvider;
            this.legalProvider = legalProvider;
            this.organizationProvider = organizationProvider;
            this.authorityProvider = authorityProvider;
            this.crimeProvider = crimeProvider;
            this.ownerId = string.IsNullOrWhiteSpace(ownerId) ? PersistenceService.LocalWorldId : ownerId;
            this.personProvider = personProvider;
            this.placeProvider = placeProvider;
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => true;
        public PersistenceScope Scope => PersistenceScope.SharedWorld;
        public string OwnerId => ownerId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.Institutions;
        public int LoadPriority => 71;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => new[] { GovernmentPersistenceParticipant.Key, LegalPersistenceParticipant.Key, CrimePersistenceParticipant.Key };
        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies => new[]
        {
            OrganizationPersistenceParticipant.Key,
            OrganizationAuthorityPersistenceParticipant.Key,
            EconomyPersistenceParticipant.Key,
            PropertyPersistenceParticipant.Key,
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
            if (runtime == null) return PersistenceParticipantSaveResult.Failure("Justice runtime is missing.");
            string payload = PersistenceSerialization.Serialize(runtime.CreateSaveData());
            PersistenceParticipantPrepareResult prepared = PreparePayload(payload, CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded) return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Justice snapshot failed validation.");
            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(payload);
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion) return PersistenceParticipantPrepareResult.Failure($"Unsupported justice participant schema version {payloadSchemaVersion}.");
            if (string.IsNullOrWhiteSpace(payloadJson)) return PersistenceParticipantPrepareResult.Failure("Justice payload is empty.");
            JusticeRuntimeSaveData saveData;
            try { saveData = PersistenceSerialization.Deserialize<JusticeRuntimeSaveData>(payloadJson); }
            catch { return PersistenceParticipantPrepareResult.Failure("Justice payload is malformed JSON."); }
            JusticeValidationReport validation = validationService.Validate(saveData, registryProvider?.Invoke(), governmentProvider?.Invoke(), legalProvider?.Invoke(), organizationProvider?.Invoke(), authorityProvider?.Invoke(), crimeProvider?.Invoke(), ownerId, personProvider?.Invoke(), placeProvider?.Invoke());
            if (!validation.IsValid) return PersistenceParticipantPrepareResult.Failure(validation.Errors.FirstOrDefault() ?? "Justice payload failed validation.");
            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null) return PersistenceParticipantCommitResult.Failure("Justice runtime is missing.");
            if (preparedPayload is not PreparedPayload prepared) return PersistenceParticipantCommitResult.Failure("Prepared justice payload has the wrong type.");
            JusticeRuntimeSaveData rollback = runtime.CreateSaveData();
            JusticeOperationResult result = Restore(prepared.SaveData);
            if (result.Succeeded) return PersistenceParticipantCommitResult.Success("Justice runtime restored without replaying legal-process mutations.");
            JusticeOperationResult rollbackResult = Restore(rollback);
            return PersistenceParticipantCommitResult.Failure($"Justice commit failed after preparation; rollback {(rollbackResult.Succeeded ? "succeeded" : "failed")}: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private JusticeOperationResult Restore(JusticeRuntimeSaveData saveData)
        {
            return runtime.RestoreFromSaveData(saveData, registryProvider?.Invoke(), governmentProvider?.Invoke(), legalProvider?.Invoke(), organizationProvider?.Invoke(), authorityProvider?.Invoke(), crimeProvider?.Invoke(), ownerId, personProvider?.Invoke(), placeProvider?.Invoke());
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(JusticeRuntimeSaveData saveData) { SaveData = saveData; }
            public JusticeRuntimeSaveData SaveData { get; }
        }
    }
}
