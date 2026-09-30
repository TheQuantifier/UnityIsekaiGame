using System;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Organizations;

namespace UnityIsekaiGame.Persistence
{
    public sealed class OrganizationAuthorityPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "world.organization-authority";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly OrganizationAuthorityRuntime runtime;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly Func<OrganizationRuntime> organizationRuntimeProvider;
        private readonly Func<OrganizationMembershipRuntime> membershipRuntimeProvider;
        private readonly Func<string[]> knownPersonProvider;
        private readonly Func<string[]> knownOrganizationProvider;
        private readonly string ownerId;

        public OrganizationAuthorityPersistenceParticipant(
            OrganizationAuthorityRuntime runtime,
            Func<DefinitionRegistry> registryProvider,
            Func<OrganizationRuntime> organizationRuntimeProvider,
            Func<OrganizationMembershipRuntime> membershipRuntimeProvider,
            string ownerId = PersistenceService.LocalWorldId,
            Func<string[]> knownPersonProvider = null,
            Func<string[]> knownOrganizationProvider = null)
        {
            this.runtime = runtime;
            this.registryProvider = registryProvider;
            this.organizationRuntimeProvider = organizationRuntimeProvider;
            this.membershipRuntimeProvider = membershipRuntimeProvider;
            this.ownerId = string.IsNullOrWhiteSpace(ownerId) ? PersistenceService.LocalWorldId : ownerId;
            this.knownPersonProvider = knownPersonProvider;
            this.knownOrganizationProvider = knownOrganizationProvider;
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => true;
        public PersistenceScope Scope => PersistenceScope.SharedWorld;
        public string OwnerId => ownerId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.Institutions;
        public int LoadPriority => 42;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => new[] { OrganizationPersistenceParticipant.Key, OrganizationMembershipPersistenceParticipant.Key };
        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies => new[]
        {
            InformationAccessPersistenceParticipant.Key,
            AuthoritativeHistoryPersistenceParticipant.Key,
            KnowledgeRecordPersistenceParticipant.Key
        };

        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => true;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (runtime == null)
            {
                return PersistenceParticipantSaveResult.Failure("Organization authority runtime is missing.");
            }

            OrganizationAuthorityRuntimeSaveData saveData = runtime.CreateSaveData();
            string payload = PersistenceSerialization.Serialize(saveData);
            PersistenceParticipantPrepareResult prepared = PreparePayload(payload, CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Organization authority snapshot failed validation.");
            }

            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(payload);
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported organization authority participant schema version {payloadSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Failure("Organization authority payload is empty.");
            }

            OrganizationAuthorityRuntimeSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<OrganizationAuthorityRuntimeSaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Organization authority payload is malformed JSON.");
            }

            if (!OrganizationAuthorityRuntime.ValidateSaveData(saveData, registryProvider?.Invoke(), organizationRuntimeProvider?.Invoke(), membershipRuntimeProvider?.Invoke(), ownerId, knownPersonProvider?.Invoke(), knownOrganizationProvider?.Invoke(), out string failure))
            {
                return PersistenceParticipantPrepareResult.Failure(failure);
            }

            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null)
            {
                return PersistenceParticipantCommitResult.Failure("Organization authority runtime is missing.");
            }

            if (preparedPayload is not PreparedPayload prepared)
            {
                return PersistenceParticipantCommitResult.Failure("Prepared organization authority payload has the wrong type.");
            }

            OrganizationAuthorityRuntimeSaveData rollback = runtime.CreateSaveData();
            OrganizationAuthorityOperationResult result = runtime.RestoreFromSaveData(prepared.SaveData, registryProvider?.Invoke(), organizationRuntimeProvider?.Invoke(), membershipRuntimeProvider?.Invoke(), ownerId, knownPersonProvider?.Invoke(), knownOrganizationProvider?.Invoke(), restoring: true);
            if (result.Succeeded)
            {
                return PersistenceParticipantCommitResult.Success("Organization authority restored.");
            }

            runtime.RestoreFromSaveData(rollback, registryProvider?.Invoke(), organizationRuntimeProvider?.Invoke(), membershipRuntimeProvider?.Invoke(), ownerId, knownPersonProvider?.Invoke(), knownOrganizationProvider?.Invoke(), restoring: true);
            return PersistenceParticipantCommitResult.Failure($"Organization authority commit failed after preparation; rollback attempted: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(OrganizationAuthorityRuntimeSaveData saveData)
            {
                SaveData = saveData;
            }

            public OrganizationAuthorityRuntimeSaveData SaveData { get; }
        }
    }
}
