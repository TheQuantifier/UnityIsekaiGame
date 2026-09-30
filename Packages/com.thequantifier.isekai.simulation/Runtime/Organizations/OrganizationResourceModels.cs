using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Economy;

namespace UnityIsekaiGame.Organizations
{
    [Serializable]
    public sealed class OrganizationAssetReferenceData
    {
        public OrganizationAssetReferenceKind kind = OrganizationAssetReferenceKind.Unknown;
        public string resourceId;
        public string definitionId;
        public string worldId;

        public string StableKey => $"{kind}:{worldId ?? string.Empty}:{resourceId ?? string.Empty}";

        public OrganizationAssetReferenceData Clone() => new OrganizationAssetReferenceData
        {
            kind = kind,
            resourceId = resourceId ?? string.Empty,
            definitionId = definitionId ?? string.Empty,
            worldId = worldId ?? string.Empty
        };
    }

    [Serializable]
    public sealed class OrganizationTreasuryRecordData
    {
        public string treasuryId;
        public string organizationId;
        public string resourceTypeDefinitionId;
        public string officialName;
        public OrganizationTreasuryCategory category = OrganizationTreasuryCategory.GeneralTreasury;
        public OrganizationTreasuryLifecycleState lifecycleState = OrganizationTreasuryLifecycleState.Active;
        public double creationWorldTime;
        public double closingWorldTime = -1d;
        public string branchOrganizationId;
        public OrganizationVisibility visibility = OrganizationVisibility.Restricted;
        public OrganizationAuthorityAuditPolicy auditPolicy = OrganizationAuthorityAuditPolicy.SuccessfulActions;
        public string provenanceId;
        public long revision = 1L;

        public OrganizationTreasuryRecordData Clone() => new OrganizationTreasuryRecordData
        {
            treasuryId = treasuryId ?? string.Empty,
            organizationId = organizationId ?? string.Empty,
            resourceTypeDefinitionId = resourceTypeDefinitionId ?? string.Empty,
            officialName = officialName ?? string.Empty,
            category = category,
            lifecycleState = lifecycleState,
            creationWorldTime = creationWorldTime,
            closingWorldTime = closingWorldTime,
            branchOrganizationId = branchOrganizationId ?? string.Empty,
            visibility = visibility,
            auditPolicy = auditPolicy,
            provenanceId = provenanceId ?? string.Empty,
            revision = Math.Max(1L, revision)
        };
    }

    [Serializable]
    public sealed class OrganizationAccountRecordData
    {
        public string accountId;
        public string treasuryId;
        public string organizationId;
        public string economyAccountId;
        public string officialName;
        public OrganizationAccountCategory category = OrganizationAccountCategory.GeneralOperating;
        public OrganizationAccountLifecycleState lifecycleState = OrganizationAccountLifecycleState.Active;
        public string currencyDefinitionId;
        public string branchOrganizationId;
        public string projectReferenceId;
        public double creationWorldTime;
        public double closingWorldTime = -1d;
        public OrganizationVisibility visibility = OrganizationVisibility.Restricted;
        public string provenanceId;
        public long revision = 1L;

        public OrganizationAccountRecordData Clone() => new OrganizationAccountRecordData
        {
            accountId = accountId ?? string.Empty,
            treasuryId = treasuryId ?? string.Empty,
            organizationId = organizationId ?? string.Empty,
            economyAccountId = economyAccountId ?? string.Empty,
            officialName = officialName ?? string.Empty,
            category = category,
            lifecycleState = lifecycleState,
            currencyDefinitionId = currencyDefinitionId ?? string.Empty,
            branchOrganizationId = branchOrganizationId ?? string.Empty,
            projectReferenceId = projectReferenceId ?? string.Empty,
            creationWorldTime = creationWorldTime,
            closingWorldTime = closingWorldTime,
            visibility = visibility,
            provenanceId = provenanceId ?? string.Empty,
            revision = Math.Max(1L, revision)
        };
    }

    [Serializable]
    public sealed class OrganizationFundRestrictionRecordData
    {
        public string restrictionId;
        public string organizationId;
        public string accountId;
        public string currencyDefinitionId;
        public long originalUnits;
        public long remainingUnits;
        public string allowedPurpose;
        public string allowedActionDefinitionId;
        public string[] allowedRecipientIds = Array.Empty<string>();
        public double startWorldTime;
        public double endWorldTime = -1d;
        public string sourceReferenceId;
        public OrganizationFundRestrictionLifecycleState lifecycleState = OrganizationFundRestrictionLifecycleState.Active;
        public OrganizationVisibility visibility = OrganizationVisibility.Secret;
        public string provenanceId;
        public long revision = 1L;

        public bool IsActiveAt(double worldTime) => lifecycleState == OrganizationFundRestrictionLifecycleState.Active && startWorldTime <= worldTime && (endWorldTime < 0d || endWorldTime > worldTime) && remainingUnits > 0L;

        public OrganizationFundRestrictionRecordData Clone() => new OrganizationFundRestrictionRecordData
        {
            restrictionId = restrictionId ?? string.Empty,
            organizationId = organizationId ?? string.Empty,
            accountId = accountId ?? string.Empty,
            currencyDefinitionId = currencyDefinitionId ?? string.Empty,
            originalUnits = Math.Max(0L, originalUnits),
            remainingUnits = Math.Max(0L, remainingUnits),
            allowedPurpose = allowedPurpose ?? string.Empty,
            allowedActionDefinitionId = allowedActionDefinitionId ?? string.Empty,
            allowedRecipientIds = OrganizationModelUtility.Clean(allowedRecipientIds),
            startWorldTime = startWorldTime,
            endWorldTime = endWorldTime,
            sourceReferenceId = sourceReferenceId ?? string.Empty,
            lifecycleState = lifecycleState,
            visibility = visibility,
            provenanceId = provenanceId ?? string.Empty,
            revision = Math.Max(1L, revision)
        };
    }

    [Serializable]
    public sealed class OrganizationBudgetRecordData
    {
        public string budgetId;
        public string organizationId;
        public string treasuryId;
        public string accountId;
        public OrganizationBudgetCategory category = OrganizationBudgetCategory.GeneralOperations;
        public OrganizationBudgetEnforcementPolicy enforcementPolicy = OrganizationBudgetEnforcementPolicy.InformationalOnly;
        public OrganizationBudgetLifecycleState lifecycleState = OrganizationBudgetLifecycleState.Active;
        public string currencyDefinitionId;
        public long authorizedUnits;
        public string purpose;
        public string fundingSourceId;
        public double startWorldTime;
        public double endWorldTime = -1d;
        public string sourceAuthorityId;
        public OrganizationVisibility visibility = OrganizationVisibility.Restricted;
        public string provenanceId;
        public long revision = 1L;

        public bool IsActiveAt(double worldTime) => lifecycleState == OrganizationBudgetLifecycleState.Active && startWorldTime <= worldTime && (endWorldTime < 0d || endWorldTime > worldTime);

        public OrganizationBudgetRecordData Clone() => new OrganizationBudgetRecordData
        {
            budgetId = budgetId ?? string.Empty,
            organizationId = organizationId ?? string.Empty,
            treasuryId = treasuryId ?? string.Empty,
            accountId = accountId ?? string.Empty,
            category = category,
            enforcementPolicy = enforcementPolicy,
            lifecycleState = lifecycleState,
            currencyDefinitionId = currencyDefinitionId ?? string.Empty,
            authorizedUnits = Math.Max(0L, authorizedUnits),
            purpose = purpose ?? string.Empty,
            fundingSourceId = fundingSourceId ?? string.Empty,
            startWorldTime = startWorldTime,
            endWorldTime = endWorldTime,
            sourceAuthorityId = sourceAuthorityId ?? string.Empty,
            visibility = visibility,
            provenanceId = provenanceId ?? string.Empty,
            revision = Math.Max(1L, revision)
        };
    }

    [Serializable]
    public sealed class OrganizationResourceReservationRecordData
    {
        public string reservationId;
        public string organizationId;
        public OrganizationAssetReferenceData resource = new OrganizationAssetReferenceData();
        public string accountId;
        public string inventoryId;
        public string economyReservationId;
        public string currencyDefinitionId;
        public long amountUnits;
        public int quantity;
        public OrganizationReservationCategory category = OrganizationReservationCategory.General;
        public OrganizationReservationLifecycleState lifecycleState = OrganizationReservationLifecycleState.Active;
        public string purpose;
        public string requestingOperationId;
        public int priority;
        public double startWorldTime;
        public double expirationWorldTime = -1d;
        public OrganizationVisibility visibility = OrganizationVisibility.Restricted;
        public string provenanceId;
        public long revision = 1L;

        public bool IsActiveAt(double worldTime) => lifecycleState == OrganizationReservationLifecycleState.Active && startWorldTime <= worldTime && (expirationWorldTime < 0d || expirationWorldTime > worldTime);

        public OrganizationResourceReservationRecordData Clone() => new OrganizationResourceReservationRecordData
        {
            reservationId = reservationId ?? string.Empty,
            organizationId = organizationId ?? string.Empty,
            resource = resource?.Clone() ?? new OrganizationAssetReferenceData(),
            accountId = accountId ?? string.Empty,
            inventoryId = inventoryId ?? string.Empty,
            economyReservationId = economyReservationId ?? string.Empty,
            currencyDefinitionId = currencyDefinitionId ?? string.Empty,
            amountUnits = Math.Max(0L, amountUnits),
            quantity = Math.Max(0, quantity),
            category = category,
            lifecycleState = lifecycleState,
            purpose = purpose ?? string.Empty,
            requestingOperationId = requestingOperationId ?? string.Empty,
            priority = priority,
            startWorldTime = startWorldTime,
            expirationWorldTime = expirationWorldTime,
            visibility = visibility,
            provenanceId = provenanceId ?? string.Empty,
            revision = Math.Max(1L, revision)
        };
    }

    [Serializable]
    public sealed class OrganizationInventoryAssociationRecordData
    {
        public string associationId;
        public string organizationId;
        public string inventoryId;
        public OrganizationInventoryCategory category = OrganizationInventoryCategory.GeneralStores;
        public string propertyId;
        public string owningOrganizationId;
        public string operatingOrganizationId;
        public string custodianId;
        public string restrictionPolicyId;
        public double startWorldTime;
        public double endWorldTime = -1d;
        public OrganizationVisibility visibility = OrganizationVisibility.Restricted;
        public string provenanceId;
        public long revision = 1L;

        public bool IsActiveAt(double worldTime) => startWorldTime <= worldTime && (endWorldTime < 0d || endWorldTime > worldTime);
        public OrganizationInventoryAssociationRecordData Clone() => new OrganizationInventoryAssociationRecordData
        {
            associationId = associationId ?? string.Empty,
            organizationId = organizationId ?? string.Empty,
            inventoryId = inventoryId ?? string.Empty,
            category = category,
            propertyId = propertyId ?? string.Empty,
            owningOrganizationId = owningOrganizationId ?? string.Empty,
            operatingOrganizationId = operatingOrganizationId ?? string.Empty,
            custodianId = custodianId ?? string.Empty,
            restrictionPolicyId = restrictionPolicyId ?? string.Empty,
            startWorldTime = startWorldTime,
            endWorldTime = endWorldTime,
            visibility = visibility,
            provenanceId = provenanceId ?? string.Empty,
            revision = Math.Max(1L, revision)
        };
    }

    [Serializable]
    public sealed class OrganizationPropertyAssociationRecordData
    {
        public string associationId;
        public string organizationId;
        public string propertyId;
        public OrganizationPropertyAssociationCategory category = OrganizationPropertyAssociationCategory.Operator;
        public string ownershipRecordId;
        public string contractReferenceId;
        public string[] rightIds = Array.Empty<string>();
        public string[] responsibilityIds = Array.Empty<string>();
        public double startWorldTime;
        public double endWorldTime = -1d;
        public OrganizationVisibility visibility = OrganizationVisibility.Public;
        public string provenanceId;
        public long revision = 1L;

        public bool IsActiveAt(double worldTime) => startWorldTime <= worldTime && (endWorldTime < 0d || endWorldTime > worldTime);
        public OrganizationPropertyAssociationRecordData Clone() => new OrganizationPropertyAssociationRecordData
        {
            associationId = associationId ?? string.Empty,
            organizationId = organizationId ?? string.Empty,
            propertyId = propertyId ?? string.Empty,
            category = category,
            ownershipRecordId = ownershipRecordId ?? string.Empty,
            contractReferenceId = contractReferenceId ?? string.Empty,
            rightIds = OrganizationModelUtility.Clean(rightIds),
            responsibilityIds = OrganizationModelUtility.Clean(responsibilityIds),
            startWorldTime = startWorldTime,
            endWorldTime = endWorldTime,
            visibility = visibility,
            provenanceId = provenanceId ?? string.Empty,
            revision = Math.Max(1L, revision)
        };
    }

    [Serializable]
    public sealed class OrganizationBusinessAssociationRecordData
    {
        public string associationId;
        public string organizationId;
        public string businessId;
        public OrganizationBusinessAssociationCategory category = OrganizationBusinessAssociationCategory.Operator;
        public string ownershipRecordId;
        public long shareBasisPoints;
        public double startWorldTime;
        public double endWorldTime = -1d;
        public OrganizationVisibility visibility = OrganizationVisibility.Public;
        public string provenanceId;
        public long revision = 1L;

        public bool IsActiveAt(double worldTime) => startWorldTime <= worldTime && (endWorldTime < 0d || endWorldTime > worldTime);
        public OrganizationBusinessAssociationRecordData Clone() => new OrganizationBusinessAssociationRecordData
        {
            associationId = associationId ?? string.Empty,
            organizationId = organizationId ?? string.Empty,
            businessId = businessId ?? string.Empty,
            category = category,
            ownershipRecordId = ownershipRecordId ?? string.Empty,
            shareBasisPoints = Math.Max(0L, shareBasisPoints),
            startWorldTime = startWorldTime,
            endWorldTime = endWorldTime,
            visibility = visibility,
            provenanceId = provenanceId ?? string.Empty,
            revision = Math.Max(1L, revision)
        };
    }

    [Serializable]
    public sealed class OrganizationAssetCustodyRecordData
    {
        public string custodyId;
        public string organizationId;
        public OrganizationAssetReferenceData asset = new OrganizationAssetReferenceData();
        public string custodianPersonId;
        public string custodianOrganizationId;
        public string sourceInventoryId;
        public string destinationInventoryId;
        public double startWorldTime;
        public double expectedReturnWorldTime = -1d;
        public double returnWorldTime = -1d;
        public OrganizationCustodyLifecycleState lifecycleState = OrganizationCustodyLifecycleState.InCustody;
        public string sourceOperationId;
        public string conditionSnapshotReferenceId;
        public OrganizationVisibility visibility = OrganizationVisibility.Restricted;
        public string provenanceId;
        public long revision = 1L;

        public OrganizationAssetCustodyRecordData Clone() => new OrganizationAssetCustodyRecordData
        {
            custodyId = custodyId ?? string.Empty,
            organizationId = organizationId ?? string.Empty,
            asset = asset?.Clone() ?? new OrganizationAssetReferenceData(),
            custodianPersonId = custodianPersonId ?? string.Empty,
            custodianOrganizationId = custodianOrganizationId ?? string.Empty,
            sourceInventoryId = sourceInventoryId ?? string.Empty,
            destinationInventoryId = destinationInventoryId ?? string.Empty,
            startWorldTime = startWorldTime,
            expectedReturnWorldTime = expectedReturnWorldTime,
            returnWorldTime = returnWorldTime,
            lifecycleState = lifecycleState,
            sourceOperationId = sourceOperationId ?? string.Empty,
            conditionSnapshotReferenceId = conditionSnapshotReferenceId ?? string.Empty,
            visibility = visibility,
            provenanceId = provenanceId ?? string.Empty,
            revision = Math.Max(1L, revision)
        };
    }

    [Serializable]
    public sealed class OrganizationRevenueRoutingRuleData
    {
        public string routingRuleId;
        public string organizationId;
        public string revenueSourceId;
        public string destinationAccountId;
        public long percentageBasisPoints;
        public long fixedUnits;
        public int priority;
        public string purpose;
        public string branchOrganizationId;
        public double startWorldTime;
        public double endWorldTime = -1d;
        public OrganizationRevenueRoutingLifecycleState lifecycleState = OrganizationRevenueRoutingLifecycleState.Active;
        public OrganizationVisibility visibility = OrganizationVisibility.Restricted;
        public string provenanceId;
        public long revision = 1L;

        public OrganizationRevenueRoutingRuleData Clone() => new OrganizationRevenueRoutingRuleData
        {
            routingRuleId = routingRuleId ?? string.Empty,
            organizationId = organizationId ?? string.Empty,
            revenueSourceId = revenueSourceId ?? string.Empty,
            destinationAccountId = destinationAccountId ?? string.Empty,
            percentageBasisPoints = Math.Max(0L, percentageBasisPoints),
            fixedUnits = Math.Max(0L, fixedUnits),
            priority = priority,
            purpose = purpose ?? string.Empty,
            branchOrganizationId = branchOrganizationId ?? string.Empty,
            startWorldTime = startWorldTime,
            endWorldTime = endWorldTime,
            lifecycleState = lifecycleState,
            visibility = visibility,
            provenanceId = provenanceId ?? string.Empty,
            revision = Math.Max(1L, revision)
        };
    }

    [Serializable]
    public sealed class OrganizationResourceTransactionRecordData
    {
        public string transactionId;
        public string operation;
        public string subjectId;
        public string economyTransactionId;
        public string organizationId;
        public string sourceAccountId;
        public string destinationAccountId;
        public string budgetId;
        public string restrictionId;
        public string purpose;
        public string currencyDefinitionId;
        public long units;
        public double worldTime;
        public OrganizationResourceOperationCode code;

        public OrganizationResourceTransactionRecordData Clone() => new OrganizationResourceTransactionRecordData
        {
            transactionId = transactionId ?? string.Empty,
            operation = operation ?? string.Empty,
            subjectId = subjectId ?? string.Empty,
            economyTransactionId = economyTransactionId ?? string.Empty,
            organizationId = organizationId ?? string.Empty,
            sourceAccountId = sourceAccountId ?? string.Empty,
            destinationAccountId = destinationAccountId ?? string.Empty,
            budgetId = budgetId ?? string.Empty,
            restrictionId = restrictionId ?? string.Empty,
            purpose = purpose ?? string.Empty,
            currencyDefinitionId = currencyDefinitionId ?? string.Empty,
            units = Math.Max(0L, units),
            worldTime = worldTime,
            code = code
        };
    }

    [Serializable]
    public sealed class OrganizationResourceRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public string worldId;
        public long revision;
        public List<OrganizationTreasuryRecordData> treasuries = new List<OrganizationTreasuryRecordData>();
        public List<OrganizationAccountRecordData> accounts = new List<OrganizationAccountRecordData>();
        public List<OrganizationFundRestrictionRecordData> restrictions = new List<OrganizationFundRestrictionRecordData>();
        public List<OrganizationBudgetRecordData> budgets = new List<OrganizationBudgetRecordData>();
        public List<OrganizationResourceReservationRecordData> reservations = new List<OrganizationResourceReservationRecordData>();
        public List<OrganizationInventoryAssociationRecordData> inventoryAssociations = new List<OrganizationInventoryAssociationRecordData>();
        public List<OrganizationPropertyAssociationRecordData> propertyAssociations = new List<OrganizationPropertyAssociationRecordData>();
        public List<OrganizationBusinessAssociationRecordData> businessAssociations = new List<OrganizationBusinessAssociationRecordData>();
        public List<OrganizationAssetCustodyRecordData> custodyRecords = new List<OrganizationAssetCustodyRecordData>();
        public List<OrganizationRevenueRoutingRuleData> revenueRoutingRules = new List<OrganizationRevenueRoutingRuleData>();
        public List<OrganizationResourceTransactionRecordData> transactions = new List<OrganizationResourceTransactionRecordData>();
        public List<OrganizationDissolutionResourcePlanData> dissolutionPlans = new List<OrganizationDissolutionResourcePlanData>();

        public OrganizationResourceRuntimeSaveData Clone() => new OrganizationResourceRuntimeSaveData
        {
            schemaVersion = schemaVersion,
            worldId = worldId ?? string.Empty,
            revision = Math.Max(0L, revision),
            treasuries = Clone(treasuries, item => item.Clone()),
            accounts = Clone(accounts, item => item.Clone()),
            restrictions = Clone(restrictions, item => item.Clone()),
            budgets = Clone(budgets, item => item.Clone()),
            reservations = Clone(reservations, item => item.Clone()),
            inventoryAssociations = Clone(inventoryAssociations, item => item.Clone()),
            propertyAssociations = Clone(propertyAssociations, item => item.Clone()),
            businessAssociations = Clone(businessAssociations, item => item.Clone()),
            custodyRecords = Clone(custodyRecords, item => item.Clone()),
            revenueRoutingRules = Clone(revenueRoutingRules, item => item.Clone()),
            transactions = Clone(transactions, item => item.Clone()),
            dissolutionPlans = Clone(dissolutionPlans, item => item.Clone())
        };

        private static List<T> Clone<T>(IEnumerable<T> source, Func<T, T> clone) where T : class => (source ?? Array.Empty<T>()).Where(item => item != null).Select(clone).ToList();
    }

    public sealed class OrganizationTreasuryRequest
    {
        public string transactionId;
        public string treasuryId;
        public string organizationId;
        public string resourceTypeDefinitionId;
        public string officialName;
        public OrganizationTreasuryCategory category = OrganizationTreasuryCategory.GeneralTreasury;
        public string branchOrganizationId;
        public string actorPersonId;
        public string actionDefinitionId;
        public string[] approvalPersonIds = Array.Empty<string>();
        public double worldTime;
        public OrganizationVisibility visibility = OrganizationVisibility.Restricted;
        public string provenanceId;
        public bool preview;
    }

    public sealed class OrganizationAccountRequest
    {
        public string transactionId;
        public string accountId;
        public string treasuryId;
        public string organizationId;
        public string economyAccountId;
        public string officialName;
        public OrganizationAccountCategory category = OrganizationAccountCategory.GeneralOperating;
        public string currencyDefinitionId;
        public long openingBalanceUnits;
        public string branchOrganizationId;
        public string projectReferenceId;
        public string actorPersonId;
        public string actionDefinitionId;
        public string[] approvalPersonIds = Array.Empty<string>();
        public double worldTime;
        public OrganizationVisibility visibility = OrganizationVisibility.Restricted;
        public string provenanceId;
        public bool preview;
    }

    public sealed class OrganizationAccountLifecycleRequest
    {
        public string transactionId;
        public string accountId;
        public OrganizationAccountLifecycleState targetState;
        public string actorPersonId;
        public string actionDefinitionId;
        public string[] approvalPersonIds = Array.Empty<string>();
        public double worldTime;
        public string reason;
        public bool preview;
    }

    public sealed class OrganizationFundRestrictionRequest
    {
        public string transactionId;
        public string restrictionId;
        public string organizationId;
        public string accountId;
        public string currencyDefinitionId;
        public long units;
        public string allowedPurpose;
        public string allowedActionDefinitionId;
        public string[] allowedRecipientIds = Array.Empty<string>();
        public string sourceReferenceId;
        public string actorPersonId;
        public string actionDefinitionId;
        public double startWorldTime;
        public double endWorldTime = -1d;
        public OrganizationVisibility visibility = OrganizationVisibility.Secret;
        public string provenanceId;
        public bool preview;
    }

    public sealed class OrganizationBudgetRequest
    {
        public string transactionId;
        public string budgetId;
        public string organizationId;
        public string treasuryId;
        public string accountId;
        public OrganizationBudgetCategory category = OrganizationBudgetCategory.GeneralOperations;
        public OrganizationBudgetEnforcementPolicy enforcementPolicy = OrganizationBudgetEnforcementPolicy.InformationalOnly;
        public string currencyDefinitionId;
        public long authorizedUnits;
        public string purpose;
        public string fundingSourceId;
        public string sourceAuthorityId;
        public string actorPersonId;
        public string actionDefinitionId;
        public double startWorldTime;
        public double endWorldTime = -1d;
        public OrganizationVisibility visibility = OrganizationVisibility.Restricted;
        public string provenanceId;
        public bool preview;
    }

    public sealed class OrganizationReservationRequest
    {
        public string transactionId;
        public string reservationId;
        public string organizationId;
        public OrganizationAssetReferenceData resource;
        public string accountId;
        public string inventoryId;
        public string currencyDefinitionId;
        public long amountUnits;
        public int quantity;
        public OrganizationReservationCategory category = OrganizationReservationCategory.General;
        public string purpose;
        public string requestingOperationId;
        public int priority;
        public string actorPersonId;
        public string actionDefinitionId;
        public double startWorldTime;
        public double expirationWorldTime = -1d;
        public OrganizationVisibility visibility = OrganizationVisibility.Restricted;
        public string provenanceId;
        public bool preview;
    }

    public sealed class OrganizationFinancialTransactionRequest
    {
        public string transactionId;
        public string organizationId;
        public string sourceAccountId;
        public string destinationAccountId;
        public string currencyDefinitionId;
        public long units;
        public EconomyTransactionKind transactionKind = EconomyTransactionKind.Transfer;
        public string actorPersonId;
        public string actionDefinitionId;
        public string purpose;
        public string restrictionId;
        public string reservationId;
        public string budgetId;
        public string relatedRecordId;
        public string[] approvalPersonIds = Array.Empty<string>();
        public double worldTime;
        public bool preview;
    }

    public sealed class OrganizationAssociationRequest
    {
        public string transactionId;
        public string associationId;
        public string organizationId;
        public string resourceId;
        public string secondaryOrganizationId;
        public string sourceRecordId;
        public string propertyId;
        public string actorPersonId;
        public string actionDefinitionId;
        public string[] rightIds = Array.Empty<string>();
        public string[] responsibilityIds = Array.Empty<string>();
        public int category;
        public long shareBasisPoints;
        public double startWorldTime;
        public double endWorldTime = -1d;
        public OrganizationVisibility visibility = OrganizationVisibility.Restricted;
        public string provenanceId;
        public bool preview;
    }

    public sealed class OrganizationCustodyRequest
    {
        public string transactionId;
        public string custodyId;
        public string organizationId;
        public OrganizationAssetReferenceData asset;
        public string custodianPersonId;
        public string custodianOrganizationId;
        public string sourceInventoryId;
        public string destinationInventoryId;
        public string actorPersonId;
        public string actionDefinitionId;
        public string sourceOperationId;
        public string conditionSnapshotReferenceId;
        public double startWorldTime;
        public double expectedReturnWorldTime = -1d;
        public OrganizationVisibility visibility = OrganizationVisibility.Restricted;
        public string provenanceId;
        public bool preview;
    }

    public sealed class OrganizationRevenueRoutingRequest
    {
        public string transactionId;
        public string routingRuleId;
        public string organizationId;
        public string revenueSourceId;
        public string destinationAccountId;
        public long percentageBasisPoints;
        public long fixedUnits;
        public int priority;
        public string purpose;
        public string branchOrganizationId;
        public string actorPersonId;
        public string actionDefinitionId;
        public double startWorldTime;
        public double endWorldTime = -1d;
        public OrganizationVisibility visibility = OrganizationVisibility.Restricted;
        public string provenanceId;
        public bool preview;
    }

    public sealed class OrganizationAccountBalanceSnapshot
    {
        public OrganizationAccountBalanceSnapshot(OrganizationAccountRecordData account, EconomyAccountSnapshot economy, long restrictedUnits, long reservedUnits, long encumberedUnits)
        {
            Account = account?.Clone() ?? new OrganizationAccountRecordData();
            EconomyAccount = economy;
            BalanceUnits = economy?.BalanceUnits ?? 0L;
            RestrictedUnits = Math.Max(0L, Math.Min(BalanceUnits, restrictedUnits));
            ReservedUnits = Math.Max(0L, reservedUnits);
            EncumberedUnits = Math.Max(0L, encumberedUnits);
            FrozenUnits = Account.lifecycleState == OrganizationAccountLifecycleState.Frozen ? BalanceUnits : 0L;
            AvailableUnits = FrozenUnits > 0L || Account.lifecycleState != OrganizationAccountLifecycleState.Active ? 0L : Math.Max(0L, BalanceUnits - RestrictedUnits - ReservedUnits - EncumberedUnits);
        }

        public OrganizationAccountRecordData Account { get; }
        public EconomyAccountSnapshot EconomyAccount { get; }
        public long BalanceUnits { get; }
        public long AvailableUnits { get; }
        public long RestrictedUnits { get; }
        public long ReservedUnits { get; }
        public long EncumberedUnits { get; }
        public long FrozenUnits { get; }
    }

    public sealed class OrganizationResourceOperationResult
    {
        private OrganizationResourceOperationResult(bool succeeded, OrganizationResourceOperationCode code, string message, long before, long after, bool preview, bool duplicate, OrganizationAuthorizationResult authorization, OrganizationTreasuryRecordData treasury, OrganizationAccountRecordData account, OrganizationAccountBalanceSnapshot sourceBalance, OrganizationAccountBalanceSnapshot destinationBalance, EconomyTransactionSnapshot transaction, string subjectId)
        {
            Succeeded = succeeded;
            Code = code;
            Message = message ?? string.Empty;
            RevisionBefore = before;
            RevisionAfter = after;
            Preview = preview;
            Duplicate = duplicate;
            Authorization = authorization;
            Treasury = treasury?.Clone();
            Account = account?.Clone();
            SourceBalance = sourceBalance;
            DestinationBalance = destinationBalance;
            EconomyTransaction = transaction;
            SubjectId = subjectId ?? string.Empty;
        }

        public bool Succeeded { get; }
        public OrganizationResourceOperationCode Code { get; }
        public string Message { get; }
        public long RevisionBefore { get; }
        public long RevisionAfter { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public OrganizationAuthorizationResult Authorization { get; }
        public OrganizationTreasuryRecordData Treasury { get; }
        public OrganizationAccountRecordData Account { get; }
        public OrganizationAccountBalanceSnapshot SourceBalance { get; }
        public OrganizationAccountBalanceSnapshot DestinationBalance { get; }
        public EconomyTransactionSnapshot EconomyTransaction { get; }
        public string SubjectId { get; }

        public static OrganizationResourceOperationResult Success(string message, long before, long after, bool preview = false, bool duplicate = false, OrganizationAuthorizationResult authorization = null, OrganizationTreasuryRecordData treasury = null, OrganizationAccountRecordData account = null, OrganizationAccountBalanceSnapshot sourceBalance = null, OrganizationAccountBalanceSnapshot destinationBalance = null, EconomyTransactionSnapshot transaction = null, string subjectId = "") => new OrganizationResourceOperationResult(true, preview ? OrganizationResourceOperationCode.Preview : duplicate ? OrganizationResourceOperationCode.Duplicate : OrganizationResourceOperationCode.Success, message, before, after, preview, duplicate, authorization, treasury, account, sourceBalance, destinationBalance, transaction, subjectId);
        public static OrganizationResourceOperationResult Failure(OrganizationResourceOperationCode code, string message, long revision, bool preview = false, OrganizationAuthorizationResult authorization = null) => new OrganizationResourceOperationResult(false, code, message, revision, revision, preview, false, authorization, null, null, null, null, null, string.Empty);
    }

    public sealed class OrganizationReconciliationDiscrepancy
    {
        public OrganizationReconciliationDiscrepancy(string code, OrganizationReconciliationSeverity severity, string subjectId, string message)
        {
            Code = code ?? string.Empty;
            Severity = severity;
            SubjectId = subjectId ?? string.Empty;
            Message = message ?? string.Empty;
        }
        public string Code { get; }
        public OrganizationReconciliationSeverity Severity { get; }
        public string SubjectId { get; }
        public string Message { get; }
    }

    public sealed class OrganizationReconciliationResult
    {
        public OrganizationReconciliationResult(IEnumerable<OrganizationReconciliationDiscrepancy> discrepancies)
        {
            Discrepancies = (discrepancies ?? Array.Empty<OrganizationReconciliationDiscrepancy>()).OrderBy(item => item.Severity).ThenBy(item => item.Code, StringComparer.Ordinal).ThenBy(item => item.SubjectId, StringComparer.Ordinal).ToArray();
        }
        public IReadOnlyList<OrganizationReconciliationDiscrepancy> Discrepancies { get; }
        public bool IsReconciled => Discrepancies.All(item => item.Severity != OrganizationReconciliationSeverity.Error);
    }

    public sealed class OrganizationResourceProjection
    {
        public OrganizationResourceProjection(OrganizationResourceProjectionAccess access, string subjectId, bool redacted, OrganizationTreasuryRecordData treasury, OrganizationAccountBalanceSnapshot balance)
        {
            Access = access;
            SubjectId = subjectId ?? string.Empty;
            Redacted = redacted;
            Treasury = treasury?.Clone();
            Balance = balance;
        }
        public OrganizationResourceProjectionAccess Access { get; }
        public string SubjectId { get; }
        public bool Redacted { get; }
        public OrganizationTreasuryRecordData Treasury { get; }
        public OrganizationAccountBalanceSnapshot Balance { get; }
    }

    [Serializable]
    public sealed class OrganizationDissolutionAssetInstructionData
    {
        public OrganizationAssetReferenceData asset = new OrganizationAssetReferenceData();
        public OrganizationDissolutionAssetInstructionKind kind = OrganizationDissolutionAssetInstructionKind.PreserveUnresolved;
        public string destinationId;

        public OrganizationDissolutionAssetInstructionData Clone() => new OrganizationDissolutionAssetInstructionData
        {
            asset = asset?.Clone() ?? new OrganizationAssetReferenceData(), kind = kind, destinationId = destinationId ?? string.Empty
        };
    }

    [Serializable]
    public sealed class OrganizationDissolutionResourcePlanData
    {
        public string planId;
        public string organizationId;
        public OrganizationDissolutionPlanLifecycleState lifecycleState = OrganizationDissolutionPlanLifecycleState.Proposed;
        public string[] accountIdsToFreeze = Array.Empty<string>();
        public string[] preservedObligationIds = Array.Empty<string>();
        public OrganizationDissolutionAssetInstructionData[] assetInstructions = Array.Empty<OrganizationDissolutionAssetInstructionData>();
        public string approvedByPersonId;
        public double createdWorldTime;
        public double executedWorldTime = -1d;
        public OrganizationVisibility visibility = OrganizationVisibility.Restricted;
        public string provenanceId;
        public long revision = 1L;

        public OrganizationDissolutionResourcePlanData Clone() => new OrganizationDissolutionResourcePlanData
        {
            planId = planId ?? string.Empty, organizationId = organizationId ?? string.Empty, lifecycleState = lifecycleState,
            accountIdsToFreeze = OrganizationModelUtility.Clean(accountIdsToFreeze), preservedObligationIds = OrganizationModelUtility.Clean(preservedObligationIds),
            assetInstructions = (assetInstructions ?? Array.Empty<OrganizationDissolutionAssetInstructionData>()).Where(item => item != null).Select(item => item.Clone()).ToArray(),
            approvedByPersonId = approvedByPersonId ?? string.Empty, createdWorldTime = createdWorldTime, executedWorldTime = executedWorldTime,
            visibility = visibility, provenanceId = provenanceId ?? string.Empty, revision = Math.Max(1L, revision)
        };
    }

    public sealed class OrganizationDissolutionResourcePlanRequest
    {
        public string transactionId;
        public string planId;
        public string organizationId;
        public string[] accountIdsToFreeze = Array.Empty<string>();
        public string[] preservedObligationIds = Array.Empty<string>();
        public OrganizationDissolutionAssetInstructionData[] assetInstructions = Array.Empty<OrganizationDissolutionAssetInstructionData>();
        public string actorPersonId;
        public string actionDefinitionId;
        public string[] approvalPersonIds = Array.Empty<string>();
        public double worldTime;
        public OrganizationVisibility visibility = OrganizationVisibility.Restricted;
        public string provenanceId;
        public bool preview;
    }

    public sealed class OrganizationRevenueRoutingExecutionRequest
    {
        public string transactionId;
        public string organizationId;
        public string revenueSourceId;
        public string sourceAccountId;
        public string currencyDefinitionId;
        public long grossUnits;
        public string actorPersonId;
        public string[] approvalPersonIds = Array.Empty<string>();
        public double worldTime;
        public bool preview;
    }

    public sealed class OrganizationLiabilitySnapshot
    {
        public OrganizationLiabilitySnapshot(OrganizationLiabilitySourceKind sourceKind, string sourceId, string organizationId, string currencyId, long payableUnits, long receivableUnits, double dueWorldTime)
        {
            SourceKind = sourceKind; SourceId = sourceId ?? string.Empty; OrganizationId = organizationId ?? string.Empty; CurrencyId = currencyId ?? string.Empty;
            PayableUnits = Math.Max(0L, payableUnits); ReceivableUnits = Math.Max(0L, receivableUnits); DueWorldTime = dueWorldTime;
        }
        public OrganizationLiabilitySourceKind SourceKind { get; }
        public string SourceId { get; }
        public string OrganizationId { get; }
        public string CurrencyId { get; }
        public long PayableUnits { get; }
        public long ReceivableUnits { get; }
        public double DueWorldTime { get; }
    }

    public sealed class OrganizationResourceValuationSnapshot
    {
        public OrganizationResourceValuationSnapshot(string organizationId, string currencyId, long cashUnits, long receivableUnits, long liabilityUnits, IEnumerable<string> unvaluedAssetIds)
        {
            OrganizationId = organizationId ?? string.Empty; CurrencyId = currencyId ?? string.Empty; CashUnits = cashUnits;
            ReceivableUnits = Math.Max(0L, receivableUnits); LiabilityUnits = Math.Max(0L, liabilityUnits); NetKnownUnits = CashUnits + ReceivableUnits - LiabilityUnits;
            UnvaluedAssetIds = OrganizationModelUtility.Clean(unvaluedAssetIds);
        }
        public string OrganizationId { get; }
        public string CurrencyId { get; }
        public long CashUnits { get; }
        public long ReceivableUnits { get; }
        public long LiabilityUnits { get; }
        public long NetKnownUnits { get; }
        public IReadOnlyList<string> UnvaluedAssetIds { get; }
    }

    public sealed class OrganizationConsolidatedResourceSnapshot
    {
        public OrganizationConsolidatedResourceSnapshot(string rootOrganizationId, IEnumerable<string> organizationIds, IEnumerable<OrganizationAccountBalanceSnapshot> balances)
        {
            RootOrganizationId = rootOrganizationId ?? string.Empty; OrganizationIds = OrganizationModelUtility.Clean(organizationIds);
            AccountBalances = (balances ?? Array.Empty<OrganizationAccountBalanceSnapshot>()).Where(item => item != null).OrderBy(item => item.Account.organizationId, StringComparer.Ordinal).ThenBy(item => item.Account.accountId, StringComparer.Ordinal).ToArray();
        }
        public string RootOrganizationId { get; }
        public IReadOnlyList<string> OrganizationIds { get; }
        public IReadOnlyList<OrganizationAccountBalanceSnapshot> AccountBalances { get; }
        public long Total(string currencyId) => AccountBalances.Where(item => item.Account.currencyDefinitionId == currencyId).Sum(item => item.BalanceUnits);
    }

    public sealed class OrganizationResourceCommittedEvent
    {
        public OrganizationResourceCommittedEvent(OrganizationResourceTransactionRecordData transaction, long revision) { Transaction = transaction?.Clone() ?? new OrganizationResourceTransactionRecordData(); Revision = revision; }
        public OrganizationResourceTransactionRecordData Transaction { get; }
        public long Revision { get; }
    }
}
