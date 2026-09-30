using System;
using UnityEngine;
using UnityIsekaiGame.Factions;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Organizations;

namespace UnityIsekaiGame.Persistence
{
    public sealed class FactionPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "world.factions";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly FactionRuntime runtime;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly Func<OrganizationRuntime> organizationProvider;
        private readonly Func<OrganizationMembershipRuntime> membershipProvider;
        private readonly Func<OrganizationAuthorityRuntime> authorityProvider;
        private readonly Func<OrganizationResourceRuntime> resourceProvider;
        private readonly Func<OrganizationDecisionRuntime> decisionProvider;
        private readonly Func<string[]> personProvider;
        private readonly string ownerId;

        public FactionPersistenceParticipant(
            FactionRuntime runtime,
            Func<DefinitionRegistry> registryProvider,
            Func<OrganizationRuntime> organizationProvider,
            Func<OrganizationMembershipRuntime> membershipProvider,
            Func<OrganizationAuthorityRuntime> authorityProvider,
            Func<OrganizationResourceRuntime> resourceProvider,
            Func<OrganizationDecisionRuntime> decisionProvider,
            string ownerId = PersistenceService.LocalWorldId,
            Func<string[]> personProvider = null)
        {
            this.runtime = runtime;
            this.registryProvider = registryProvider;
            this.organizationProvider = organizationProvider;
            this.membershipProvider = membershipProvider;
            this.authorityProvider = authorityProvider;
            this.resourceProvider = resourceProvider;
            this.decisionProvider = decisionProvider;
            this.ownerId = string.IsNullOrWhiteSpace(ownerId) ? PersistenceService.LocalWorldId : ownerId;
            this.personProvider = personProvider;
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => true;
        public PersistenceScope Scope => PersistenceScope.SharedWorld;
        public string OwnerId => ownerId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.Institutions;
        public int LoadPriority => 66;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => new[]
        {
            OrganizationPersistenceParticipant.Key,
            OrganizationMembershipPersistenceParticipant.Key,
            OrganizationDecisionPersistenceParticipant.Key
        };
        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies => new[]
        {
            OrganizationAuthorityPersistenceParticipant.Key,
            OrganizationResourcePersistenceParticipant.Key,
            InformationAccessPersistenceParticipant.Key
        };
        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => true;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (runtime == null) return PersistenceParticipantSaveResult.Failure("Faction runtime is missing.");
            string payload = PersistenceSerialization.Serialize(runtime.CreateSaveData());
            PersistenceParticipantPrepareResult prepared = PreparePayload(payload, CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded) return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Faction snapshot failed validation.");
            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(payload);
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion) return PersistenceParticipantPrepareResult.Failure($"Unsupported faction participant schema version {payloadSchemaVersion}.");
            if (string.IsNullOrWhiteSpace(payloadJson)) return PersistenceParticipantPrepareResult.Failure("Faction payload is empty.");
            FactionRuntimeSaveData saveData;
            try { saveData = PersistenceSerialization.Deserialize<FactionRuntimeSaveData>(payloadJson); }
            catch { return PersistenceParticipantPrepareResult.Failure("Faction payload is malformed JSON."); }
            if (!FactionRuntime.ValidateSaveData(saveData, registryProvider?.Invoke(), organizationProvider?.Invoke(), membershipProvider?.Invoke(), ownerId, personProvider?.Invoke(), out string failure)) return PersistenceParticipantPrepareResult.Failure(failure);
            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null) return PersistenceParticipantCommitResult.Failure("Faction runtime is missing.");
            if (preparedPayload is not PreparedPayload prepared) return PersistenceParticipantCommitResult.Failure("Prepared faction payload has the wrong type.");
            FactionRuntimeSaveData rollback = runtime.CreateSaveData();
            FactionOperationResult result = Restore(prepared.SaveData);
            if (result.Succeeded) return PersistenceParticipantCommitResult.Success("Faction runtime restored without replaying faction mutations.");
            FactionOperationResult rollbackResult = Restore(rollback);
            return PersistenceParticipantCommitResult.Failure($"Faction commit failed after preparation; rollback {(rollbackResult.Succeeded ? "succeeded" : "failed")}: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private FactionOperationResult Restore(FactionRuntimeSaveData saveData)
        {
            return runtime.RestoreFromSaveData(saveData, registryProvider?.Invoke(), organizationProvider?.Invoke(), membershipProvider?.Invoke(), authorityProvider?.Invoke(), resourceProvider?.Invoke(), decisionProvider?.Invoke(), ownerId, personProvider?.Invoke(), restoring: true);
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(FactionRuntimeSaveData saveData) { SaveData = saveData; }
            public FactionRuntimeSaveData SaveData { get; }
        }
    }
}
