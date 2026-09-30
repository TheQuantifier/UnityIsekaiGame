using System;
using UnityEngine;
using UnityIsekaiGame.Economy;
using UnityIsekaiGame.Economy.Businesses;
using UnityIsekaiGame.Economy.Payroll;
using UnityIsekaiGame.Economy.Properties;
using UnityIsekaiGame.Contracts;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Organizations;

namespace UnityIsekaiGame.Persistence
{
    public sealed class OrganizationResourcePersistenceParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
    {
        public const string Key = "world.organization-resources";
        public const int CurrentParticipantSchemaVersion = 1;

        private readonly OrganizationResourceRuntime runtime;
        private readonly Func<DefinitionRegistry> registryProvider;
        private readonly Func<OrganizationRuntime> organizationProvider;
        private readonly Func<OrganizationAuthorityRuntime> authorityProvider;
        private readonly Func<EconomyRuntime> economyProvider;
        private readonly Func<PropertyRuntime> propertyProvider;
        private readonly Func<BusinessRuntime> businessProvider;
        private readonly Func<ItemInstanceIdentityRuntime> itemProvider;
        private readonly Func<ContractEconomyRuntime> contractProvider;
        private readonly Func<PayrollRuntime> payrollProvider;
        private readonly string ownerId;

        public OrganizationResourcePersistenceParticipant(
            OrganizationResourceRuntime runtime,
            Func<DefinitionRegistry> registryProvider,
            Func<OrganizationRuntime> organizationProvider,
            Func<OrganizationAuthorityRuntime> authorityProvider,
            Func<EconomyRuntime> economyProvider,
            string ownerId = PersistenceService.LocalWorldId,
            Func<PropertyRuntime> propertyProvider = null,
            Func<BusinessRuntime> businessProvider = null,
            Func<ItemInstanceIdentityRuntime> itemProvider = null,
            Func<ContractEconomyRuntime> contractProvider = null,
            Func<PayrollRuntime> payrollProvider = null)
        {
            this.runtime = runtime;
            this.registryProvider = registryProvider;
            this.organizationProvider = organizationProvider;
            this.authorityProvider = authorityProvider;
            this.economyProvider = economyProvider;
            this.propertyProvider = propertyProvider;
            this.businessProvider = businessProvider;
            this.itemProvider = itemProvider;
            this.contractProvider = contractProvider;
            this.payrollProvider = payrollProvider;
            this.ownerId = string.IsNullOrWhiteSpace(ownerId) ? PersistenceService.LocalWorldId : ownerId;
        }

        public string ParticipantKey => Key;
        public int ParticipantSchemaVersion => CurrentParticipantSchemaVersion;
        public bool IsRequired => true;
        public PersistenceScope Scope => PersistenceScope.SharedWorld;
        public string OwnerId => ownerId;
        public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.Institutions;
        public int LoadPriority => 60;
        public System.Collections.Generic.IReadOnlyList<string> RequiredDependencies => new[]
        {
            OrganizationPersistenceParticipant.Key,
            OrganizationAuthorityPersistenceParticipant.Key,
            EconomyPersistenceParticipant.Key
        };
        public System.Collections.Generic.IReadOnlyList<string> OptionalDependencies => new[]
        {
            OrganizationMembershipPersistenceParticipant.Key,
            ItemInstanceIdentityPersistenceParticipant.Key,
            BusinessPersistenceParticipant.Key,
            PropertyPersistenceParticipant.Key,
            ContractEconomyPersistenceParticipant.Key,
            PayrollPersistenceParticipant.Key
        };
        public bool SupportsRollback => true;
        public bool RequiresSceneReadiness => false;
        public bool RequiresDefinitionRegistry => true;
        public bool RequiresWorldEntityRegistry => false;

        public PersistenceParticipantSaveResult CapturePayload()
        {
            if (runtime == null) return PersistenceParticipantSaveResult.Failure("Organization resource runtime is missing.");
            string payload = PersistenceSerialization.Serialize(runtime.CreateSaveData());
            PersistenceParticipantPrepareResult prepared = PreparePayload(payload, CurrentParticipantSchemaVersion);
            if (prepared == null || !prepared.Succeeded) return PersistenceParticipantSaveResult.Failure(prepared?.Message ?? "Organization resource snapshot failed validation.");
            DiscardPreparedPayload(prepared.PreparedPayload);
            return PersistenceParticipantSaveResult.Success(payload);
        }

        public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
        {
            if (payloadSchemaVersion != CurrentParticipantSchemaVersion) return PersistenceParticipantPrepareResult.Failure($"Unsupported organization resource participant schema version {payloadSchemaVersion}.");
            if (string.IsNullOrWhiteSpace(payloadJson)) return PersistenceParticipantPrepareResult.Failure("Organization resource payload is empty.");
            OrganizationResourceRuntimeSaveData saveData;
            try { saveData = PersistenceSerialization.Deserialize<OrganizationResourceRuntimeSaveData>(payloadJson); }
            catch { return PersistenceParticipantPrepareResult.Failure("Organization resource payload is malformed JSON."); }
            if (!OrganizationResourceRuntime.ValidateSaveData(saveData, registryProvider?.Invoke(), organizationProvider?.Invoke(), economyProvider?.Invoke(), ownerId, propertyProvider?.Invoke(), businessProvider?.Invoke(), itemProvider?.Invoke(), out string failure)) return PersistenceParticipantPrepareResult.Failure(failure);
            return PersistenceParticipantPrepareResult.Success(new PreparedPayload(saveData.Clone()));
        }

        public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
        {
            if (runtime == null) return PersistenceParticipantCommitResult.Failure("Organization resource runtime is missing.");
            if (preparedPayload is not PreparedPayload prepared) return PersistenceParticipantCommitResult.Failure("Prepared organization resource payload has the wrong type.");
            OrganizationResourceRuntimeSaveData rollback = runtime.CreateSaveData();
            OrganizationResourceOperationResult result = Restore(prepared.SaveData);
            if (result.Succeeded) return PersistenceParticipantCommitResult.Success("Organization resources restored without replaying economic mutations.");
            OrganizationResourceOperationResult rollbackResult = Restore(rollback);
            return PersistenceParticipantCommitResult.Failure($"Organization resource commit failed after preparation; rollback {(rollbackResult.Succeeded ? "succeeded" : "failed")}: {result.Message}");
        }

        public void DiscardPreparedPayload(object preparedPayload)
        {
        }

        private OrganizationResourceOperationResult Restore(OrganizationResourceRuntimeSaveData saveData)
        {
            return runtime.RestoreFromSaveData(saveData, registryProvider?.Invoke(), organizationProvider?.Invoke(), authorityProvider?.Invoke(), economyProvider?.Invoke(), ownerId, propertyProvider?.Invoke(), businessProvider?.Invoke(), itemProvider?.Invoke(), restoring: true, contractRuntime: contractProvider?.Invoke(), payrollRuntime: payrollProvider?.Invoke());
        }

        private sealed class PreparedPayload
        {
            public PreparedPayload(OrganizationResourceRuntimeSaveData saveData) { SaveData = saveData; }
            public OrganizationResourceRuntimeSaveData SaveData { get; }
        }
    }
}
