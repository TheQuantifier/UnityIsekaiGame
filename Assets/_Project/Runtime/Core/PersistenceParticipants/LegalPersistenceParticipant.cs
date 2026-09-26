using System;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Diplomacy;
using UnityIsekaiGame.Economy.Properties;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Governments;
using UnityIsekaiGame.Laws;
using UnityIsekaiGame.Organizations;

namespace UnityIsekaiGame.Persistence
{
    public sealed class LegalPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "world.laws";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly LegalRuntime runtime;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly Func<GovernmentRuntime> governmentProvider;
        private readonly Func<OrganizationRuntime> organizationProvider;
        private readonly Func<OrganizationAuthorityRuntime> authorityProvider;
        private readonly Func<OrganizationDecisionRuntime> decisionProvider;
        private readonly Func<DiplomacyRuntime> diplomacyProvider;
        private readonly Func<PropertyRuntime> propertyProvider;
        private readonly Func<string[]> personProvider;
        private readonly Func<string[]> placeProvider;
        private readonly LegalRuntimeValidationService validationService = new LegalRuntimeValidationService();
        private readonly string ownerId;

        public LegalPersistenceParticipant(
            LegalRuntime runtime,
            Func<DefinitionRegistry> registryProvider,
            Func<GovernmentRuntime> governmentProvider,
            Func<OrganizationRuntime> organizationProvider,
            Func<OrganizationAuthorityRuntime> authorityProvider,
            Func<OrganizationDecisionRuntime> decisionProvider,
            Func<DiplomacyRuntime> diplomacyProvider,
            Func<PropertyRuntime> propertyProvider,
            string ownerId = PersistenceService.LocalWorldId,
            Func<string[]> personProvider = null,
            Func<string[]> placeProvider = null)
        {
            this.runtime = runtime;
            this.registryProvider = registryProvider;
            this.governmentProvider = governmentProvider;
            this.organizationProvider = organizationProvider;
            this.authorityProvider = authorityProvider;
            this.decisionProvider = decisionProvider;
            this.diplomacyProvider = diplomacyProvider;
            this.propertyProvider = propertyProvider;
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
        public int LoadPriority => 69;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => new[] { GovernmentPersistenceParticipant.Key };
        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies => new[]
        {
            OrganizationPersistenceParticipant.Key,
            OrganizationAuthorityPersistenceParticipant.Key,
            OrganizationDecisionPersistenceParticipant.Key,
            DiplomacyPersistenceParticipant.Key,
            PropertyPersistenceParticipant.Key,
            InformationAccessPersistenceParticipant.Key,
            AuthoritativeHistoryPersistenceParticipant.Key
        };
        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => true;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (runtime == null) return PersistenceParticipantSaveResult.Failure("Legal runtime is missing.");
            string payload = PersistenceSerialization.Serialize(runtime.CreateSaveData());
            PersistenceParticipantPrepareResult prepared = PreparePayload(payload, CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded) return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Legal snapshot failed validation.");
            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(payload);
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion) return PersistenceParticipantPrepareResult.Failure($"Unsupported legal participant schema version {payloadSchemaVersion}.");
            if (string.IsNullOrWhiteSpace(payloadJson)) return PersistenceParticipantPrepareResult.Failure("Legal payload is empty.");
            LegalRuntimeSaveData saveData;
            try { saveData = PersistenceSerialization.Deserialize<LegalRuntimeSaveData>(payloadJson); }
            catch { return PersistenceParticipantPrepareResult.Failure("Legal payload is malformed JSON."); }
            LegalValidationReport validation = validationService.Validate(saveData, registryProvider?.Invoke(), governmentProvider?.Invoke(), organizationProvider?.Invoke(), authorityProvider?.Invoke(), decisionProvider?.Invoke(), diplomacyProvider?.Invoke(), propertyProvider?.Invoke(), ownerId, personProvider?.Invoke(), placeProvider?.Invoke());
            if (!validation.IsValid) return PersistenceParticipantPrepareResult.Failure(validation.Errors.FirstOrDefault() ?? "Legal payload failed validation.");
            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null) return PersistenceParticipantCommitResult.Failure("Legal runtime is missing.");
            if (preparedPayload is not PreparedPayload prepared) return PersistenceParticipantCommitResult.Failure("Prepared legal payload has the wrong type.");
            LegalRuntimeSaveData rollback = runtime.CreateSaveData();
            LegalOperationResult result = Restore(prepared.SaveData);
            if (result.Succeeded) return PersistenceParticipantCommitResult.Success("Legal runtime restored without replaying legal mutations.");
            LegalOperationResult rollbackResult = Restore(rollback);
            return PersistenceParticipantCommitResult.Failure($"Legal commit failed after preparation; rollback {(rollbackResult.Succeeded ? "succeeded" : "failed")}: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private LegalOperationResult Restore(LegalRuntimeSaveData saveData)
        {
            return runtime.RestoreFromSaveData(
                saveData,
                registryProvider?.Invoke(),
                governmentProvider?.Invoke(),
                organizationProvider?.Invoke(),
                authorityProvider?.Invoke(),
                decisionProvider?.Invoke(),
                diplomacyProvider?.Invoke(),
                propertyProvider?.Invoke(),
                ownerId,
                personProvider?.Invoke(),
                placeProvider?.Invoke());
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(LegalRuntimeSaveData saveData) { SaveData = saveData; }
            public LegalRuntimeSaveData SaveData { get; }
        }
    }
}
