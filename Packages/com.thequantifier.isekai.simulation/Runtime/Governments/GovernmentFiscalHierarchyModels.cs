using System;
using System.Linq;
using UnityIsekaiGame.Organizations;

namespace UnityIsekaiGame.Governments
{
    [Serializable]
    public sealed class GovernmentRemittancePolicyRecordData
    {
        public string policyId;
        public string sourceGovernmentId;
        public string destinationGovernmentId;
        public string sourceEconomyAccountId;
        public string destinationEconomyAccountId;
        public string currencyDefinitionId;
        public string[] eligibleRevenueDefinitionIds = Array.Empty<string>();
        public int remittanceBasisPoints;
        public long fixedTributeUnits;
        public long minimumReserveUnits;
        public double settlementIntervalWorldTime = 86400d;
        public double nextSettlementWorldTime;
        public double gracePeriodWorldTime;
        public GovernmentRemittancePolicyState state = GovernmentRemittancePolicyState.Active;
        public string[] processedRevenueRecordIds = Array.Empty<string>();
        public string[] processedIncomingRemittanceIds = Array.Empty<string>();
        public long arrearsUnits;
        public string sourceAuthorityId;
        public string provenanceId;
        public long revision = 1L;

        public GovernmentRemittancePolicyRecordData Clone()
        {
            GovernmentRemittancePolicyRecordData clone = (GovernmentRemittancePolicyRecordData)MemberwiseClone();
            clone.eligibleRevenueDefinitionIds = (eligibleRevenueDefinitionIds ?? Array.Empty<string>()).ToArray();
            clone.processedRevenueRecordIds = (processedRevenueRecordIds ?? Array.Empty<string>()).ToArray();
            clone.processedIncomingRemittanceIds = (processedIncomingRemittanceIds ?? Array.Empty<string>()).ToArray();
            return clone;
        }

        public bool IsDue(double worldTime) => state == GovernmentRemittancePolicyState.Active && worldTime >= nextSettlementWorldTime;
    }

    [Serializable]
    public sealed class GovernmentRemittanceSettlementRecordData
    {
        public string settlementId;
        public string policyId;
        public string sourceGovernmentId;
        public string destinationGovernmentId;
        public string sourceEconomyAccountId;
        public string destinationEconomyAccountId;
        public string currencyDefinitionId;
        public long newlyAssessedUnits;
        public long priorArrearsUnits;
        public long paidUnits;
        public long remainingArrearsUnits;
        public string[] sourceRevenueRecordIds = Array.Empty<string>();
        public string[] sourceRemittanceRecordIds = Array.Empty<string>();
        public string economyTransactionId;
        public GovernmentRemittanceSettlementState state;
        public double dueWorldTime;
        public double settledWorldTime;
        public string provenanceId;
        public long revision = 1L;

        public GovernmentRemittanceSettlementRecordData Clone()
        {
            GovernmentRemittanceSettlementRecordData clone = (GovernmentRemittanceSettlementRecordData)MemberwiseClone();
            clone.sourceRevenueRecordIds = (sourceRevenueRecordIds ?? Array.Empty<string>()).ToArray();
            clone.sourceRemittanceRecordIds = (sourceRemittanceRecordIds ?? Array.Empty<string>()).ToArray();
            return clone;
        }
    }

    [Serializable]
    public sealed class GovernmentFiscalReliefRecordData
    {
        public string reliefId;
        public string remittancePolicyId;
        public int reductionBasisPoints;
        public long waivedFixedUnits;
        public double effectiveWorldTime;
        public double expirationWorldTime = -1d;
        public string reason;
        public string sourceAuthorityId;
        public long revision = 1L;
        public GovernmentFiscalReliefRecordData Clone() => (GovernmentFiscalReliefRecordData)MemberwiseClone();
        public bool IsActiveAt(double worldTime) => effectiveWorldTime <= worldTime && (expirationWorldTime < 0d || worldTime < expirationWorldTime);
    }

    [Serializable]
    public sealed class GovernmentActionRegulationRecordData
    {
        public string regulationId;
        public string governmentId;
        public string jurisdictionId;
        public string actionId;
        public string requiredPermitDefinitionId;
        public GovernmentPermitHolderCategory[] regulatedHolderCategories = Array.Empty<GovernmentPermitHolderCategory>();
        public string[] exemptHolderIds = Array.Empty<string>();
        public double effectiveWorldTime;
        public double expirationWorldTime = -1d;
        public bool active = true;
        public string provenanceId;
        public long revision = 1L;

        public GovernmentActionRegulationRecordData Clone()
        {
            GovernmentActionRegulationRecordData clone = (GovernmentActionRegulationRecordData)MemberwiseClone();
            clone.regulatedHolderCategories = (regulatedHolderCategories ?? Array.Empty<GovernmentPermitHolderCategory>()).ToArray();
            clone.exemptHolderIds = (exemptHolderIds ?? Array.Empty<string>()).ToArray();
            return clone;
        }

        public bool IsActiveAt(double worldTime) => active && effectiveWorldTime <= worldTime && (expirationWorldTime < 0d || worldTime < expirationWorldTime);
    }

    [Serializable]
    public sealed class GovernmentOfficeVacancyRecordData
    {
        public string vacancyId;
        public string governmentId;
        public string officeDefinitionId;
        public string priorTenureId;
        public string priorHolderPersonId;
        public GovernmentOfficeVacancyCause cause;
        public GovernmentOfficeVacancyState state;
        public string successorTenureId;
        public double openedWorldTime;
        public double resolvedWorldTime = -1d;
        public string sourceEventId;
        public long revision = 1L;
        public GovernmentOfficeVacancyRecordData Clone() => (GovernmentOfficeVacancyRecordData)MemberwiseClone();
    }

    [Serializable]
    public sealed class GovernmentLegitimacyEventRecordData
    {
        public string eventId;
        public string governmentId;
        public GovernmentLegitimacyComponent component;
        public int deltaBasisPoints;
        public string reason;
        public string sourceRecordId;
        public string resultingAssessmentId;
        public double worldTime;
        public long revision = 1L;
        public GovernmentLegitimacyEventRecordData Clone() => (GovernmentLegitimacyEventRecordData)MemberwiseClone();
    }

    [Serializable]
    public sealed class GovernmentBudgetCycleRecordData
    {
        public string cycleId;
        public string governmentId;
        public string organizationId;
        public string treasuryId;
        public string accountId;
        public string currencyDefinitionId;
        public string budgetIdPrefix;
        public string currentBudgetId;
        public OrganizationBudgetCategory category = OrganizationBudgetCategory.GeneralOperations;
        public OrganizationBudgetEnforcementPolicy enforcementPolicy = OrganizationBudgetEnforcementPolicy.WarnWhenExceeded;
        public long authorizedUnits;
        public string purpose;
        public string fundingSourceId;
        public string actorPersonId;
        public string sourceAuthorityId;
        public double cycleDurationWorldTime = 31536000d;
        public double nextCycleWorldTime;
        public int completedCycles;
        public bool active = true;
        public string provenanceId;
        public long revision = 1L;
        public GovernmentBudgetCycleRecordData Clone() => (GovernmentBudgetCycleRecordData)MemberwiseClone();
    }

    public sealed class GovernmentRemittancePolicyRequest
    {
        public string transactionId;
        public string policyId;
        public string sourceGovernmentId;
        public string destinationGovernmentId;
        public string sourceEconomyAccountId;
        public string destinationEconomyAccountId;
        public string currencyDefinitionId;
        public string[] eligibleRevenueDefinitionIds = Array.Empty<string>();
        public int remittanceBasisPoints;
        public long fixedTributeUnits;
        public long minimumReserveUnits;
        public double settlementIntervalWorldTime = 86400d;
        public double firstSettlementWorldTime;
        public double gracePeriodWorldTime;
        public string sourceAuthorityId;
        public string provenanceId;
        public bool preview;
    }

    public sealed class GovernmentFiscalReliefRequest
    {
        public string transactionId;
        public string reliefId;
        public string remittancePolicyId;
        public int reductionBasisPoints;
        public long waivedFixedUnits;
        public double effectiveWorldTime;
        public double expirationWorldTime = -1d;
        public string reason;
        public string sourceAuthorityId;
        public bool preview;
    }

    public sealed class GovernmentActionRegulationRequest
    {
        public string transactionId;
        public string regulationId;
        public string governmentId;
        public string jurisdictionId;
        public string actionId;
        public string requiredPermitDefinitionId;
        public GovernmentPermitHolderCategory[] regulatedHolderCategories = Array.Empty<GovernmentPermitHolderCategory>();
        public string[] exemptHolderIds = Array.Empty<string>();
        public double effectiveWorldTime;
        public double expirationWorldTime = -1d;
        public string provenanceId;
        public bool preview;
    }

    public sealed class GovernmentPermitCheckResult
    {
        private GovernmentPermitCheckResult(bool allowed, bool required, string message, string regulationId, string permitId)
        {
            Allowed = allowed; Required = required; Message = message ?? string.Empty;
            RegulationId = regulationId ?? string.Empty; PermitId = permitId ?? string.Empty;
        }
        public bool Allowed { get; }
        public bool Required { get; }
        public string Message { get; }
        public string RegulationId { get; }
        public string PermitId { get; }
        public static GovernmentPermitCheckResult NotRegulated() => new GovernmentPermitCheckResult(true, false, "Action is not regulated for this holder.", string.Empty, string.Empty);
        public static GovernmentPermitCheckResult Granted(string regulationId, string permitId) => new GovernmentPermitCheckResult(true, true, "Required permit is active.", regulationId, permitId);
        public static GovernmentPermitCheckResult Denied(string regulationId, string message) => new GovernmentPermitCheckResult(false, true, message, regulationId, string.Empty);
    }

    public sealed class GovernmentOfficeEndRequest
    {
        public string transactionId;
        public string tenureId;
        public GovernmentOfficeVacancyCause cause;
        public double worldTime;
        public string sourceEventId;
        public bool preview;
    }

    public sealed class GovernmentLegitimacyEventRequest
    {
        public string transactionId;
        public string eventId;
        public string governmentId;
        public GovernmentLegitimacyComponent component;
        public int deltaBasisPoints;
        public string reason;
        public string sourceRecordId;
        public double worldTime;
        public bool preview;
    }

    public sealed class GovernmentBudgetCycleRequest
    {
        public string transactionId;
        public string cycleId;
        public string governmentId;
        public string organizationId;
        public string treasuryId;
        public string accountId;
        public string currencyDefinitionId;
        public string budgetIdPrefix;
        public string currentBudgetId;
        public OrganizationBudgetCategory category = OrganizationBudgetCategory.GeneralOperations;
        public OrganizationBudgetEnforcementPolicy enforcementPolicy = OrganizationBudgetEnforcementPolicy.WarnWhenExceeded;
        public long authorizedUnits;
        public string purpose;
        public string fundingSourceId;
        public string actorPersonId;
        public string sourceAuthorityId;
        public double cycleDurationWorldTime = 31536000d;
        public double nextCycleWorldTime;
        public string provenanceId;
        public bool preview;
    }
}
