using System;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Organizations;

namespace UnityIsekaiGame.Persistence
{
    public sealed class OrganizationDecisionPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "world.organization-decisions";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly OrganizationDecisionRuntime runtime;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly Func<OrganizationRuntime> organizationProvider;
        private readonly Func<OrganizationMembershipRuntime> membershipProvider;
        private readonly Func<OrganizationAuthorityRuntime> authorityProvider;
        private readonly Func<OrganizationResourceRuntime> resourceProvider;
        private readonly Func<string[]> personProvider;
        private readonly string ownerId;

        public OrganizationDecisionPersistenceParticipant(
            OrganizationDecisionRuntime runtime,
            Func<DefinitionRegistry> registryProvider,
            Func<OrganizationRuntime> organizationProvider,
            Func<OrganizationMembershipRuntime> membershipProvider,
            Func<OrganizationAuthorityRuntime> authorityProvider,
            Func<OrganizationResourceRuntime> resourceProvider,
            string ownerId = PersistenceService.LocalWorldId,
            Func<string[]> personProvider = null)
        {
            this.runtime = runtime;
            this.registryProvider = registryProvider;
            this.organizationProvider = organizationProvider;
            this.membershipProvider = membershipProvider;
            this.authorityProvider = authorityProvider;
            this.resourceProvider = resourceProvider;
            this.ownerId = string.IsNullOrWhiteSpace(ownerId) ? PersistenceService.LocalWorldId : ownerId;
            this.personProvider = personProvider;
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => false;
        public PersistenceScope Scope => PersistenceScope.SharedWorld;
        public string OwnerId => ownerId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.Inventory;
        public int LoadPriority => 65;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => new[]
        {
            OrganizationPersistenceParticipant.Key,
            OrganizationMembershipPersistenceParticipant.Key,
            OrganizationAuthorityPersistenceParticipant.Key
        };
        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies => new[]
        {
            OrganizationResourcePersistenceParticipant.Key
        };
        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => true;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (runtime == null) return PersistenceParticipantSaveResult.Failure("Organization decision runtime is missing.");
            string payload = PersistenceSerialization.Serialize(runtime.CreateSaveData());
            PersistenceParticipantPrepareResult prepared = PreparePayload(payload, CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded) return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Organization decision snapshot failed validation.");
            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(payload);
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion) return PersistenceParticipantPrepareResult.Failure($"Unsupported organization decision participant schema version {payloadSchemaVersion}.");
            if (string.IsNullOrWhiteSpace(payloadJson)) return PersistenceParticipantPrepareResult.Failure("Organization decision payload is empty.");
            OrganizationDecisionRuntimeSaveData saveData;
            try { saveData = PersistenceSerialization.Deserialize<OrganizationDecisionRuntimeSaveData>(payloadJson); }
            catch { return PersistenceParticipantPrepareResult.Failure("Organization decision payload is malformed JSON."); }
            if (!OrganizationDecisionRuntime.ValidateSaveData(saveData, registryProvider?.Invoke(), organizationProvider?.Invoke(), membershipProvider?.Invoke(), authorityProvider?.Invoke(), resourceProvider?.Invoke(), ownerId, personProvider?.Invoke(), out string failure)) return PersistenceParticipantPrepareResult.Failure(failure);
            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null) return PersistenceParticipantCommitResult.Failure("Organization decision runtime is missing.");
            if (preparedPayload is not PreparedPayload prepared) return PersistenceParticipantCommitResult.Failure("Prepared organization decision payload has the wrong type.");
            OrganizationDecisionRuntimeSaveData rollback = runtime.CreateSaveData();
            OrganizationDecisionOperationResult result = Restore(prepared.SaveData);
            if (result.Succeeded) return PersistenceParticipantCommitResult.Success("Organization decisions restored without replaying governance mutations.");
            OrganizationDecisionOperationResult rollbackResult = Restore(rollback);
            return PersistenceParticipantCommitResult.Failure($"Organization decision commit failed after preparation; rollback {(rollbackResult.Succeeded ? "succeeded" : "failed")}: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private OrganizationDecisionOperationResult Restore(OrganizationDecisionRuntimeSaveData saveData)
        {
            return runtime.RestoreFromSaveData(saveData, registryProvider?.Invoke(), organizationProvider?.Invoke(), membershipProvider?.Invoke(), authorityProvider?.Invoke(), resourceProvider?.Invoke(), ownerId, personProvider?.Invoke(), restoring: true);
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(OrganizationDecisionRuntimeSaveData saveData) { SaveData = saveData; }
            public OrganizationDecisionRuntimeSaveData SaveData { get; }
        }
    }
}
