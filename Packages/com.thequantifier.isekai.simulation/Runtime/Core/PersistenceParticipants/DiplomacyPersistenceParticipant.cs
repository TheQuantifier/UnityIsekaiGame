using System;
using UnityEngine;
using UnityIsekaiGame.Diplomacy;
using UnityIsekaiGame.Factions;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Organizations;

namespace UnityIsekaiGame.Persistence
{
    public sealed class DiplomacyPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "world.diplomacy";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly DiplomacyRuntime runtime;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly Func<OrganizationRuntime> organizationProvider;
        private readonly Func<FactionRuntime> factionProvider;
        private readonly Func<OrganizationAuthorityRuntime> authorityProvider;
        private readonly Func<OrganizationDecisionRuntime> decisionProvider;
        private readonly Func<OrganizationResourceRuntime> resourceProvider;
        private readonly Func<string[]> personProvider;
        private readonly string ownerId;

        public DiplomacyPersistenceParticipant(
            DiplomacyRuntime runtime,
            Func<DefinitionRegistry> registryProvider,
            Func<OrganizationRuntime> organizationProvider,
            Func<FactionRuntime> factionProvider,
            Func<OrganizationAuthorityRuntime> authorityProvider,
            Func<OrganizationDecisionRuntime> decisionProvider,
            Func<OrganizationResourceRuntime> resourceProvider,
            string ownerId = PersistenceService.LocalWorldId,
            Func<string[]> personProvider = null)
        {
            this.runtime = runtime;
            this.registryProvider = registryProvider;
            this.organizationProvider = organizationProvider;
            this.factionProvider = factionProvider;
            this.authorityProvider = authorityProvider;
            this.decisionProvider = decisionProvider;
            this.resourceProvider = resourceProvider;
            this.ownerId = string.IsNullOrWhiteSpace(ownerId) ? PersistenceService.LocalWorldId : ownerId;
            this.personProvider = personProvider;
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => true;
        public PersistenceScope Scope => PersistenceScope.SharedWorld;
        public string OwnerId => ownerId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.Institutions;
        public int LoadPriority => 67;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => new[]
        {
            OrganizationPersistenceParticipant.Key
        };
        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies => new[]
        {
            OrganizationMembershipPersistenceParticipant.Key,
            OrganizationAuthorityPersistenceParticipant.Key,
            OrganizationResourcePersistenceParticipant.Key,
            OrganizationDecisionPersistenceParticipant.Key,
            FactionPersistenceParticipant.Key,
            InformationAccessPersistenceParticipant.Key
        };
        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => true;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (runtime == null) return PersistenceParticipantSaveResult.Failure("Diplomacy runtime is missing.");
            string payload = PersistenceSerialization.Serialize(runtime.CreateSaveData());
            PersistenceParticipantPrepareResult prepared = PreparePayload(payload, CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded) return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Diplomacy snapshot failed validation.");
            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(payload);
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion) return PersistenceParticipantPrepareResult.Failure($"Unsupported diplomacy participant schema version {payloadSchemaVersion}.");
            if (string.IsNullOrWhiteSpace(payloadJson)) return PersistenceParticipantPrepareResult.Failure("Diplomacy payload is empty.");
            DiplomacyRuntimeSaveData saveData;
            try { saveData = PersistenceSerialization.Deserialize<DiplomacyRuntimeSaveData>(payloadJson); }
            catch { return PersistenceParticipantPrepareResult.Failure("Diplomacy payload is malformed JSON."); }
            if (!DiplomacyRuntime.ValidateSaveData(saveData, registryProvider?.Invoke(), organizationProvider?.Invoke(), factionProvider?.Invoke(), ownerId, personProvider?.Invoke(), out string failure)) return PersistenceParticipantPrepareResult.Failure(failure);
            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null) return PersistenceParticipantCommitResult.Failure("Diplomacy runtime is missing.");
            if (preparedPayload is not PreparedPayload prepared) return PersistenceParticipantCommitResult.Failure("Prepared diplomacy payload has the wrong type.");
            DiplomacyRuntimeSaveData rollback = runtime.CreateSaveData();
            DiplomacyOperationResult result = Restore(prepared.SaveData);
            if (result.Succeeded) return PersistenceParticipantCommitResult.Success("Diplomacy runtime restored without replaying diplomatic mutations.");
            DiplomacyOperationResult rollbackResult = Restore(rollback);
            return PersistenceParticipantCommitResult.Failure($"Diplomacy commit failed after preparation; rollback {(rollbackResult.Succeeded ? "succeeded" : "failed")}: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private DiplomacyOperationResult Restore(DiplomacyRuntimeSaveData saveData)
        {
            return runtime.RestoreFromSaveData(saveData, registryProvider?.Invoke(), organizationProvider?.Invoke(), factionProvider?.Invoke(), authorityProvider?.Invoke(), decisionProvider?.Invoke(), resourceProvider?.Invoke(), ownerId, personProvider?.Invoke(), restoring: true);
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(DiplomacyRuntimeSaveData saveData) { SaveData = saveData; }
            public DiplomacyRuntimeSaveData SaveData { get; }
        }
    }
}
