using System;
using UnityEngine;
using UnityIsekaiGame.Diplomacy;
using UnityIsekaiGame.Economy.Properties;
using UnityIsekaiGame.Factions;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Governments;
using UnityIsekaiGame.Organizations;

namespace UnityIsekaiGame.Persistence
{
    public sealed class GovernmentPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "world.governments";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly GovernmentRuntime runtime;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly Func<OrganizationRuntime> organizationProvider;
        private readonly Func<OrganizationMembershipRuntime> membershipProvider;
        private readonly Func<OrganizationAuthorityRuntime> authorityProvider;
        private readonly Func<OrganizationDecisionRuntime> decisionProvider;
        private readonly Func<OrganizationResourceRuntime> resourceProvider;
        private readonly Func<FactionRuntime> factionProvider;
        private readonly Func<DiplomacyRuntime> diplomacyProvider;
        private readonly Func<PropertyRuntime> propertyProvider;
        private readonly Func<string[]> personProvider;
        private readonly Func<string[]> placeProvider;
        private readonly string ownerId;

        public GovernmentPersistenceParticipant(
            GovernmentRuntime runtime,
            Func<DefinitionRegistry> registryProvider,
            Func<OrganizationRuntime> organizationProvider,
            Func<OrganizationMembershipRuntime> membershipProvider,
            Func<OrganizationAuthorityRuntime> authorityProvider,
            Func<OrganizationDecisionRuntime> decisionProvider,
            Func<OrganizationResourceRuntime> resourceProvider,
            Func<FactionRuntime> factionProvider,
            Func<DiplomacyRuntime> diplomacyProvider,
            Func<PropertyRuntime> propertyProvider,
            string ownerId = PersistenceService.LocalWorldId,
            Func<string[]> personProvider = null,
            Func<string[]> placeProvider = null)
        {
            this.runtime = runtime;
            this.registryProvider = registryProvider;
            this.organizationProvider = organizationProvider;
            this.membershipProvider = membershipProvider;
            this.authorityProvider = authorityProvider;
            this.decisionProvider = decisionProvider;
            this.resourceProvider = resourceProvider;
            this.factionProvider = factionProvider;
            this.diplomacyProvider = diplomacyProvider;
            this.propertyProvider = propertyProvider;
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
        public int LoadPriority => 68;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => new[] { OrganizationPersistenceParticipant.Key };
        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies => new[]
        {
            OrganizationMembershipPersistenceParticipant.Key,
            OrganizationAuthorityPersistenceParticipant.Key,
            OrganizationResourcePersistenceParticipant.Key,
            OrganizationDecisionPersistenceParticipant.Key,
            FactionPersistenceParticipant.Key,
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
            if (runtime == null) return PersistenceParticipantSaveResult.Failure("Government runtime is missing.");
            string payload = PersistenceSerialization.Serialize(runtime.CreateSaveData());
            PersistenceParticipantPrepareResult prepared = PreparePayload(payload, CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded) return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Government snapshot failed validation.");
            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(payload);
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion) return PersistenceParticipantPrepareResult.Failure($"Unsupported government participant schema version {payloadSchemaVersion}.");
            if (string.IsNullOrWhiteSpace(payloadJson)) return PersistenceParticipantPrepareResult.Failure("Government payload is empty.");
            GovernmentRuntimeSaveData saveData;
            try { saveData = PersistenceSerialization.Deserialize<GovernmentRuntimeSaveData>(payloadJson); }
            catch { return PersistenceParticipantPrepareResult.Failure("Government payload is malformed JSON."); }
            if (!GovernmentRuntime.ValidateSaveData(saveData, registryProvider?.Invoke(), organizationProvider?.Invoke(), factionProvider?.Invoke(), diplomacyProvider?.Invoke(), propertyProvider?.Invoke(), ownerId, personProvider?.Invoke(), placeProvider?.Invoke(), out string failure)) return PersistenceParticipantPrepareResult.Failure(failure);
            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null) return PersistenceParticipantCommitResult.Failure("Government runtime is missing.");
            if (preparedPayload is not PreparedPayload prepared) return PersistenceParticipantCommitResult.Failure("Prepared government payload has the wrong type.");
            GovernmentRuntimeSaveData rollback = runtime.CreateSaveData();
            PoliticalOperationResult result = Restore(prepared.SaveData);
            if (result.Succeeded) return PersistenceParticipantCommitResult.Success("Government runtime restored without replaying political mutations.");
            PoliticalOperationResult rollbackResult = Restore(rollback);
            return PersistenceParticipantCommitResult.Failure($"Government commit failed after preparation; rollback {(rollbackResult.Succeeded ? "succeeded" : "failed")}: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private PoliticalOperationResult Restore(GovernmentRuntimeSaveData saveData)
        {
            return runtime.RestoreFromSaveData(
                saveData,
                registryProvider?.Invoke(),
                organizationProvider?.Invoke(),
                membershipProvider?.Invoke(),
                authorityProvider?.Invoke(),
                decisionProvider?.Invoke(),
                resourceProvider?.Invoke(),
                factionProvider?.Invoke(),
                diplomacyProvider?.Invoke(),
                propertyProvider?.Invoke(),
                ownerId,
                personProvider?.Invoke(),
                placeProvider?.Invoke(),
                restoring: true);
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(GovernmentRuntimeSaveData saveData) { SaveData = saveData; }
            public GovernmentRuntimeSaveData SaveData { get; }
        }
    }
}
