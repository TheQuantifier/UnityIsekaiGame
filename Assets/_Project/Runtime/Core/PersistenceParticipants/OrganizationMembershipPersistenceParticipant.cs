using System;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Organizations;

namespace UnityIsekaiGame.Persistence
{
    public sealed class OrganizationMembershipPersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "world.organization-memberships";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly OrganizationMembershipRuntime runtime;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly Func<OrganizationRuntime> organizationRuntimeProvider;
        private readonly Func<string[]> knownPersonProvider;
        private readonly Func<string[]> knownOrganizationProvider;
        private readonly string ownerId;

        public OrganizationMembershipPersistenceParticipant(
            OrganizationMembershipRuntime runtime,
            Func<DefinitionRegistry> registryProvider,
            Func<OrganizationRuntime> organizationRuntimeProvider,
            string ownerId = PersistenceService.LocalWorldId,
            Func<string[]> knownPersonProvider = null,
            Func<string[]> knownOrganizationProvider = null)
        {
            this.runtime = runtime;
            this.registryProvider = registryProvider;
            this.organizationRuntimeProvider = organizationRuntimeProvider;
            this.ownerId = string.IsNullOrWhiteSpace(ownerId) ? PersistenceService.LocalWorldId : ownerId;
            this.knownPersonProvider = knownPersonProvider;
            this.knownOrganizationProvider = knownOrganizationProvider;
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => false;
        public PersistenceScope Scope => PersistenceScope.SharedWorld;
        public string OwnerId => ownerId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.IdentityAndProgression;
        public int LoadPriority => 41;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => new[] { OrganizationPersistenceParticipant.Key };
        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies => new[]
        {
            InformationAccessPersistenceParticipant.Key,
            CareerHistoryPersistenceParticipant.Key,
            PositionEmploymentPersistenceParticipant.Key
        };

        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => true;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (runtime == null)
            {
                return PersistenceParticipantSaveResult.Failure("Organization membership runtime is missing.");
            }

            OrganizationMembershipRuntimeSaveData saveData = runtime.CreateSaveData();
            string payload = PersistenceSerialization.Serialize(saveData);
            PersistenceParticipantPrepareResult prepared = PreparePayload(payload, CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded)
            {
                return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Organization membership snapshot failed validation.");
            }

            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(payload);
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion)
            {
                return PersistenceParticipantPrepareResult.Failure($"Unsupported organization membership participant schema version {payloadSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return PersistenceParticipantPrepareResult.Failure("Organization membership payload is empty.");
            }

            OrganizationMembershipRuntimeSaveData saveData;
            try
            {
                saveData = PersistenceSerialization.Deserialize<OrganizationMembershipRuntimeSaveData>(payloadJson);
            }
            catch
            {
                return PersistenceParticipantPrepareResult.Failure("Organization membership payload is malformed JSON.");
            }

            if (!OrganizationMembershipRuntime.ValidateSaveData(saveData, registryProvider?.Invoke(), organizationRuntimeProvider?.Invoke(), ownerId, knownPersonProvider?.Invoke(), knownOrganizationProvider?.Invoke(), out string failure))
            {
                return PersistenceParticipantPrepareResult.Failure(failure);
            }

            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null)
            {
                return PersistenceParticipantCommitResult.Failure("Organization membership runtime is missing.");
            }

            if (preparedPayload is not PreparedPayload prepared)
            {
                return PersistenceParticipantCommitResult.Failure("Prepared organization membership payload has the wrong type.");
            }

            OrganizationMembershipRuntimeSaveData rollback = runtime.CreateSaveData();
            OrganizationMembershipOperationResult result = runtime.RestoreFromSaveData(prepared.SaveData, registryProvider?.Invoke(), organizationRuntimeProvider?.Invoke(), ownerId, knownPersonProvider?.Invoke(), knownOrganizationProvider?.Invoke(), restoring: true);
            if (result.Succeeded)
            {
                return PersistenceParticipantCommitResult.Success("Organization memberships restored.");
            }

            runtime.RestoreFromSaveData(rollback, registryProvider?.Invoke(), organizationRuntimeProvider?.Invoke(), ownerId, knownPersonProvider?.Invoke(), knownOrganizationProvider?.Invoke(), restoring: true);
            return PersistenceParticipantCommitResult.Failure($"Organization membership commit failed after preparation; rollback attempted: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(OrganizationMembershipRuntimeSaveData saveData)
            {
                SaveData = saveData;
            }

            public OrganizationMembershipRuntimeSaveData SaveData { get; }
        }
    }
}
