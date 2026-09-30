using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Economy.Businesses
{
    public static class BusinessModelHelpers
    {
        public static string[] CleanIds(IEnumerable<string> ids)
        {
            return (ids ?? Array.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
        }

        public static string[] CleanOrderedIds(IEnumerable<string> ids)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            return (ids ?? Array.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Where(seen.Add)
                .ToArray();
        }

        public static TEnum[] NormalizeEnums<TEnum>(IEnumerable<TEnum> values)
            where TEnum : struct, Enum
        {
            return (values ?? Array.Empty<TEnum>())
                .Where(value => Enum.IsDefined(typeof(TEnum), value))
                .Distinct()
                .OrderBy(value => Convert.ToInt32(value))
                .ToArray();
        }

        public static BusinessMoneyData Money(string currencyId, long units)
        {
            return new BusinessMoneyData { currencyId = currencyId ?? string.Empty, units = units };
        }
    }

    [Serializable]
    public sealed class BusinessRationalData
    {
        public long numerator;
        public long denominator = 1L;

        public BusinessRationalData Clone()
        {
            return new BusinessRationalData
            {
                numerator = numerator,
                denominator = Math.Max(1L, denominator)
            };
        }

        public bool IsPositive => numerator > 0L && denominator > 0L;
    }

    [Serializable]
    public sealed class BusinessMoneyData
    {
        public string currencyId;
        public long units;

        public BusinessMoneyData Clone()
        {
            return new BusinessMoneyData
            {
                currencyId = currencyId ?? string.Empty,
                units = units
            };
        }
    }

    [Serializable]
    public sealed class BusinessSubjectReferenceData
    {
        public BusinessOwnerSubjectKind kind = BusinessOwnerSubjectKind.Unknown;
        public string subjectId;

        public BusinessSubjectReferenceData Clone()
        {
            return new BusinessSubjectReferenceData
            {
                kind = kind,
                subjectId = subjectId ?? string.Empty
            };
        }

        public string StableKey => $"{kind}:{subjectId ?? string.Empty}";
    }

    [Serializable]
    public sealed class BusinessInstanceData
    {
        public string businessId;
        public string businessDefinitionId;
        public string displayName;
        public string legalName;
        public string linkedOrganizationId;
        public string[] founderSubjectIds = Array.Empty<string>();
        public string controllerSubjectId;
        public BusinessState state = BusinessState.Planned;
        public double createdWorldTime;
        public double suspendedWorldTime = -1d;
        public double closedWorldTime = -1d;
        public string[] operatingCurrencyIds = Array.Empty<string>();
        public string[] establishmentIds = Array.Empty<string>();
        public string[] accountAssignmentIds = Array.Empty<string>();
        public string[] inventoryAssignmentIds = Array.Empty<string>();
        public string[] positionIds = Array.Empty<string>();
        public string[] employmentIds = Array.Empty<string>();
        public string[] productionPolicyIds = Array.Empty<string>();
        public string[] marketIds = Array.Empty<string>();
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public BusinessInstanceData Clone()
        {
            return new BusinessInstanceData
            {
                businessId = businessId ?? string.Empty,
                businessDefinitionId = businessDefinitionId ?? string.Empty,
                displayName = displayName ?? string.Empty,
                legalName = legalName ?? string.Empty,
                linkedOrganizationId = linkedOrganizationId ?? string.Empty,
                founderSubjectIds = BusinessModelHelpers.CleanIds(founderSubjectIds),
                controllerSubjectId = controllerSubjectId ?? string.Empty,
                state = state,
                createdWorldTime = createdWorldTime,
                suspendedWorldTime = suspendedWorldTime,
                closedWorldTime = closedWorldTime,
                operatingCurrencyIds = BusinessModelHelpers.CleanIds(operatingCurrencyIds),
                establishmentIds = BusinessModelHelpers.CleanIds(establishmentIds),
                accountAssignmentIds = BusinessModelHelpers.CleanIds(accountAssignmentIds),
                inventoryAssignmentIds = BusinessModelHelpers.CleanIds(inventoryAssignmentIds),
                positionIds = BusinessModelHelpers.CleanIds(positionIds),
                employmentIds = BusinessModelHelpers.CleanIds(employmentIds),
                productionPolicyIds = BusinessModelHelpers.CleanIds(productionPolicyIds),
                marketIds = BusinessModelHelpers.CleanIds(marketIds),
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }

        public InformationSubjectReferenceData CreateInformationSubject()
        {
            return BusinessInformationSubject.Create("business.instance", businessId, linkedOrganizationId);
        }
    }

    [Serializable]
    public sealed class BusinessOwnershipRecordData
    {
        public string ownershipRecordId;
        public string businessId;
        public BusinessSubjectReferenceData owner = new BusinessSubjectReferenceData();
        public BusinessOwnershipCategory category = BusinessOwnershipCategory.Partner;
        public BusinessRationalData economicShare = new BusinessRationalData { numerator = 10000L, denominator = 10000L };
        public BusinessRationalData votingShare = new BusinessRationalData();
        public double effectiveStartWorldTime;
        public double effectiveEndWorldTime = -1d;
        public string acquisitionSourceId;
        public string transferReferenceId;
        public string[] rightIds = Array.Empty<string>();
        public string[] restrictionIds = Array.Empty<string>();
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public BusinessOwnershipRecordData Clone()
        {
            return new BusinessOwnershipRecordData
            {
                ownershipRecordId = ownershipRecordId ?? string.Empty,
                businessId = businessId ?? string.Empty,
                owner = owner?.Clone() ?? new BusinessSubjectReferenceData(),
                category = category,
                economicShare = economicShare?.Clone() ?? new BusinessRationalData(),
                votingShare = votingShare?.Clone() ?? new BusinessRationalData(),
                effectiveStartWorldTime = effectiveStartWorldTime,
                effectiveEndWorldTime = effectiveEndWorldTime,
                acquisitionSourceId = acquisitionSourceId ?? string.Empty,
                transferReferenceId = transferReferenceId ?? string.Empty,
                rightIds = BusinessModelHelpers.CleanIds(rightIds),
                restrictionIds = BusinessModelHelpers.CleanIds(restrictionIds),
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }

        public bool ActiveAt(double worldTime)
        {
            return effectiveStartWorldTime <= worldTime + 0.0001d && (effectiveEndWorldTime < 0d || effectiveEndWorldTime > worldTime + 0.0001d);
        }
    }

    [Serializable]
    public sealed class BusinessControlRecordData
    {
        public string controlRecordId;
        public string businessId;
        public string controllerSubjectId;
        public BusinessAuthorityKind[] authorityKinds = Array.Empty<BusinessAuthorityKind>();
        public string sourceReferenceId;
        public double effectiveStartWorldTime;
        public double effectiveEndWorldTime = -1d;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public BusinessControlRecordData Clone()
        {
            return new BusinessControlRecordData
            {
                controlRecordId = controlRecordId ?? string.Empty,
                businessId = businessId ?? string.Empty,
                controllerSubjectId = controllerSubjectId ?? string.Empty,
                authorityKinds = BusinessModelHelpers.NormalizeEnums(authorityKinds),
                sourceReferenceId = sourceReferenceId ?? string.Empty,
                effectiveStartWorldTime = effectiveStartWorldTime,
                effectiveEndWorldTime = effectiveEndWorldTime,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }

        public bool ActiveAt(double worldTime)
        {
            return effectiveStartWorldTime <= worldTime + 0.0001d && (effectiveEndWorldTime < 0d || effectiveEndWorldTime > worldTime + 0.0001d);
        }
    }

    [Serializable]
    public sealed class BusinessEstablishmentData
    {
        public string establishmentId;
        public string businessId;
        public BusinessEstablishmentType type = BusinessEstablishmentType.Shop;
        public string displayName;
        public string locationReferenceId;
        public BusinessEstablishmentState state = BusinessEstablishmentState.Open;
        public string responsibleManagerSubjectId;
        public string[] accountAssignmentIds = Array.Empty<string>();
        public string[] inventoryAssignmentIds = Array.Empty<string>();
        public string[] productionStationIds = Array.Empty<string>();
        public string marketInstanceId;
        public double openedWorldTime;
        public double closedWorldTime = -1d;
        public string operatingPolicyId;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public BusinessEstablishmentData Clone()
        {
            return new BusinessEstablishmentData
            {
                establishmentId = establishmentId ?? string.Empty,
                businessId = businessId ?? string.Empty,
                type = type,
                displayName = displayName ?? string.Empty,
                locationReferenceId = locationReferenceId ?? string.Empty,
                state = state,
                responsibleManagerSubjectId = responsibleManagerSubjectId ?? string.Empty,
                accountAssignmentIds = BusinessModelHelpers.CleanIds(accountAssignmentIds),
                inventoryAssignmentIds = BusinessModelHelpers.CleanIds(inventoryAssignmentIds),
                productionStationIds = BusinessModelHelpers.CleanIds(productionStationIds),
                marketInstanceId = marketInstanceId ?? string.Empty,
                openedWorldTime = openedWorldTime,
                closedWorldTime = closedWorldTime,
                operatingPolicyId = operatingPolicyId ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class BusinessAccountAssignmentData
    {
        public string assignmentId;
        public string businessId;
        public string accountId;
        public BusinessAccountPurpose purpose = BusinessAccountPurpose.OperatingFunds;
        public string establishmentId;
        public string[] authorizedSpenderSubjectIds = Array.Empty<string>();
        public string approvalPolicyId;
        public double effectiveStartWorldTime;
        public double effectiveEndWorldTime = -1d;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public BusinessAccountAssignmentData Clone()
        {
            return new BusinessAccountAssignmentData
            {
                assignmentId = assignmentId ?? string.Empty,
                businessId = businessId ?? string.Empty,
                accountId = accountId ?? string.Empty,
                purpose = purpose,
                establishmentId = establishmentId ?? string.Empty,
                authorizedSpenderSubjectIds = BusinessModelHelpers.CleanIds(authorizedSpenderSubjectIds),
                approvalPolicyId = approvalPolicyId ?? string.Empty,
                effectiveStartWorldTime = effectiveStartWorldTime,
                effectiveEndWorldTime = effectiveEndWorldTime,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class BusinessInventoryAssignmentData
    {
        public string assignmentId;
        public string businessId;
        public string inventoryId;
        public string establishmentId;
        public BusinessInventoryPurpose purpose = BusinessInventoryPurpose.RetailStock;
        public string responsibleCustodianSubjectId;
        public string[] permittedItemCategoryIds = Array.Empty<string>();
        public string stockPolicyId;
        public double effectiveStartWorldTime;
        public double effectiveEndWorldTime = -1d;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public BusinessInventoryAssignmentData Clone()
        {
            return new BusinessInventoryAssignmentData
            {
                assignmentId = assignmentId ?? string.Empty,
                businessId = businessId ?? string.Empty,
                inventoryId = inventoryId ?? string.Empty,
                establishmentId = establishmentId ?? string.Empty,
                purpose = purpose,
                responsibleCustodianSubjectId = responsibleCustodianSubjectId ?? string.Empty,
                permittedItemCategoryIds = BusinessModelHelpers.CleanIds(permittedItemCategoryIds),
                stockPolicyId = stockPolicyId ?? string.Empty,
                effectiveStartWorldTime = effectiveStartWorldTime,
                effectiveEndWorldTime = effectiveEndWorldTime,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class BusinessStockClassificationData
    {
        public string stockClassificationId;
        public string businessId;
        public string establishmentId;
        public string inventoryId;
        public string itemInstanceId;
        public string itemDefinitionId;
        public long quantity = 1L;
        public string owningSubjectId;
        public string custodianSubjectId;
        public BusinessStockCategory category = BusinessStockCategory.ForSale;
        public string intendedUse;
        public bool saleEligible;
        public bool productionEligible;
        public string reservationState;
        public string qualityReferenceId;
        public string durabilityReferenceId;
        public string acquisitionCostReferenceId;
        public string marketReferenceId;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public BusinessStockClassificationData Clone()
        {
            return new BusinessStockClassificationData
            {
                stockClassificationId = stockClassificationId ?? string.Empty,
                businessId = businessId ?? string.Empty,
                establishmentId = establishmentId ?? string.Empty,
                inventoryId = inventoryId ?? string.Empty,
                itemInstanceId = itemInstanceId ?? string.Empty,
                itemDefinitionId = itemDefinitionId ?? string.Empty,
                quantity = Math.Max(1L, quantity),
                owningSubjectId = owningSubjectId ?? string.Empty,
                custodianSubjectId = custodianSubjectId ?? string.Empty,
                category = category,
                intendedUse = intendedUse ?? string.Empty,
                saleEligible = saleEligible,
                productionEligible = productionEligible,
                reservationState = reservationState ?? string.Empty,
                qualityReferenceId = qualityReferenceId ?? string.Empty,
                durabilityReferenceId = durabilityReferenceId ?? string.Empty,
                acquisitionCostReferenceId = acquisitionCostReferenceId ?? string.Empty,
                marketReferenceId = marketReferenceId ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class BusinessProductionOwnershipData
    {
        public string productionOwnershipId;
        public string businessId;
        public string productionJobId;
        public string productionBatchId;
        public string establishmentId;
        public string productionSponsorSubjectId;
        public string inputOwnerSubjectId;
        public ProductionOutputOwnerPolicy outputOwnerPolicy = ProductionOutputOwnerPolicy.BusinessOwnsOutputs;
        public string explicitOutputOwnerSubjectId;
        public string outputCustodianSubjectId;
        public string responsibleProducerSubjectId;
        public string supervisingPositionId;
        public string fundingAccountId;
        public string[] inputInventoryIds = Array.Empty<string>();
        public string[] outputInventoryIds = Array.Empty<string>();
        public string[] toolInstanceIds = Array.Empty<string>();
        public string[] stationIds = Array.Empty<string>();
        public string revenueOrSaleIntentId;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public BusinessProductionOwnershipData Clone()
        {
            return new BusinessProductionOwnershipData
            {
                productionOwnershipId = productionOwnershipId ?? string.Empty,
                businessId = businessId ?? string.Empty,
                productionJobId = productionJobId ?? string.Empty,
                productionBatchId = productionBatchId ?? string.Empty,
                establishmentId = establishmentId ?? string.Empty,
                productionSponsorSubjectId = productionSponsorSubjectId ?? string.Empty,
                inputOwnerSubjectId = inputOwnerSubjectId ?? string.Empty,
                outputOwnerPolicy = outputOwnerPolicy,
                explicitOutputOwnerSubjectId = explicitOutputOwnerSubjectId ?? string.Empty,
                outputCustodianSubjectId = outputCustodianSubjectId ?? string.Empty,
                responsibleProducerSubjectId = responsibleProducerSubjectId ?? string.Empty,
                supervisingPositionId = supervisingPositionId ?? string.Empty,
                fundingAccountId = fundingAccountId ?? string.Empty,
                inputInventoryIds = BusinessModelHelpers.CleanIds(inputInventoryIds),
                outputInventoryIds = BusinessModelHelpers.CleanIds(outputInventoryIds),
                toolInstanceIds = BusinessModelHelpers.CleanIds(toolInstanceIds),
                stationIds = BusinessModelHelpers.CleanIds(stationIds),
                revenueOrSaleIntentId = revenueOrSaleIntentId ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class BusinessFundingAllocationData
    {
        public string allocationId;
        public string businessId;
        public string productionJobId;
        public string accountId;
        public BusinessMoneyData maximumAuthorizedAmount = new BusinessMoneyData();
        public string purpose;
        public string approvingAuthoritySubjectId;
        public string reservationReferenceId;
        public double effectiveStartWorldTime;
        public double expirationWorldTime = -1d;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public BusinessFundingAllocationData Clone()
        {
            return new BusinessFundingAllocationData
            {
                allocationId = allocationId ?? string.Empty,
                businessId = businessId ?? string.Empty,
                productionJobId = productionJobId ?? string.Empty,
                accountId = accountId ?? string.Empty,
                maximumAuthorizedAmount = maximumAuthorizedAmount?.Clone() ?? new BusinessMoneyData(),
                purpose = purpose ?? string.Empty,
                approvingAuthoritySubjectId = approvingAuthoritySubjectId ?? string.Empty,
                reservationReferenceId = reservationReferenceId ?? string.Empty,
                effectiveStartWorldTime = effectiveStartWorldTime,
                expirationWorldTime = expirationWorldTime,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class BusinessRevenueRecordData
    {
        public string revenueRecordId;
        public string businessId;
        public string establishmentId;
        public BusinessRevenueCategory category = BusinessRevenueCategory.RetailSale;
        public BusinessMoneyData amount = new BusinessMoneyData();
        public string transactionId;
        public string tradeRecordId;
        public string[] soldItemOrServiceIds = Array.Empty<string>();
        public string marketOrQuoteReferenceId;
        public double recognitionWorldTime;
        public string accountingPeriodId;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public BusinessRevenueRecordData Clone()
        {
            return new BusinessRevenueRecordData
            {
                revenueRecordId = revenueRecordId ?? string.Empty,
                businessId = businessId ?? string.Empty,
                establishmentId = establishmentId ?? string.Empty,
                category = category,
                amount = amount?.Clone() ?? new BusinessMoneyData(),
                transactionId = transactionId ?? string.Empty,
                tradeRecordId = tradeRecordId ?? string.Empty,
                soldItemOrServiceIds = BusinessModelHelpers.CleanIds(soldItemOrServiceIds),
                marketOrQuoteReferenceId = marketOrQuoteReferenceId ?? string.Empty,
                recognitionWorldTime = recognitionWorldTime,
                accountingPeriodId = accountingPeriodId ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class BusinessExpenseRecordData
    {
        public string expenseRecordId;
        public string businessId;
        public string establishmentId;
        public BusinessExpenseCategory category = BusinessExpenseCategory.InventoryPurchase;
        public BusinessMoneyData amount = new BusinessMoneyData();
        public string transactionId;
        public string payrollObligationId;
        public string payrollPaymentRecordId;
        public string[] purchasedItemOrServiceIds = Array.Empty<string>();
        public string productionJobId;
        public double recognitionWorldTime;
        public string accountingPeriodId;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public BusinessExpenseRecordData Clone()
        {
            return new BusinessExpenseRecordData
            {
                expenseRecordId = expenseRecordId ?? string.Empty,
                businessId = businessId ?? string.Empty,
                establishmentId = establishmentId ?? string.Empty,
                category = category,
                amount = amount?.Clone() ?? new BusinessMoneyData(),
                transactionId = transactionId ?? string.Empty,
                payrollObligationId = payrollObligationId ?? string.Empty,
                payrollPaymentRecordId = payrollPaymentRecordId ?? string.Empty,
                purchasedItemOrServiceIds = BusinessModelHelpers.CleanIds(purchasedItemOrServiceIds),
                productionJobId = productionJobId ?? string.Empty,
                recognitionWorldTime = recognitionWorldTime,
                accountingPeriodId = accountingPeriodId ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class BusinessCapitalContributionData
    {
        public string contributionId;
        public string businessId;
        public string contributingSubjectId;
        public string[] assetReferenceIds = Array.Empty<string>();
        public BusinessMoneyData monetaryValue = new BusinessMoneyData();
        public string ownershipEffectReferenceId;
        public string transactionOrTransferReferenceId;
        public double worldTime;
        public string approvalAuthoritySubjectId;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public BusinessCapitalContributionData Clone()
        {
            return new BusinessCapitalContributionData
            {
                contributionId = contributionId ?? string.Empty,
                businessId = businessId ?? string.Empty,
                contributingSubjectId = contributingSubjectId ?? string.Empty,
                assetReferenceIds = BusinessModelHelpers.CleanIds(assetReferenceIds),
                monetaryValue = monetaryValue?.Clone() ?? new BusinessMoneyData(),
                ownershipEffectReferenceId = ownershipEffectReferenceId ?? string.Empty,
                transactionOrTransferReferenceId = transactionOrTransferReferenceId ?? string.Empty,
                worldTime = worldTime,
                approvalAuthoritySubjectId = approvalAuthoritySubjectId ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class BusinessOwnerWithdrawalData
    {
        public string withdrawalId;
        public string businessId;
        public string receivingOwnerSubjectId;
        public string[] assetReferenceIds = Array.Empty<string>();
        public BusinessMoneyData amount = new BusinessMoneyData();
        public BusinessDistributionCategory category = BusinessDistributionCategory.OwnerDraw;
        public string authorizationSubjectId;
        public string transactionOrTransferReferenceId;
        public double worldTime;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public BusinessOwnerWithdrawalData Clone()
        {
            return new BusinessOwnerWithdrawalData
            {
                withdrawalId = withdrawalId ?? string.Empty,
                businessId = businessId ?? string.Empty,
                receivingOwnerSubjectId = receivingOwnerSubjectId ?? string.Empty,
                assetReferenceIds = BusinessModelHelpers.CleanIds(assetReferenceIds),
                amount = amount?.Clone() ?? new BusinessMoneyData(),
                category = category,
                authorizationSubjectId = authorizationSubjectId ?? string.Empty,
                transactionOrTransferReferenceId = transactionOrTransferReferenceId ?? string.Empty,
                worldTime = worldTime,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class BusinessAccountingPeriodData
    {
        public string accountingPeriodId;
        public string businessId;
        public double startWorldTime;
        public double endWorldTime;
        public string currencyId;
        public AccountingPeriodState state = AccountingPeriodState.Open;
        public string[] includedRevenueRecordIds = Array.Empty<string>();
        public string[] includedExpenseRecordIds = Array.Empty<string>();
        public string[] capitalContributionIds = Array.Empty<string>();
        public string[] ownerWithdrawalIds = Array.Empty<string>();
        public string[] openingAccountIds = Array.Empty<string>();
        public string[] closingAccountIds = Array.Empty<string>();
        public string inventoryValueReferenceId;
        public string[] statementIds = Array.Empty<string>();
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public BusinessAccountingPeriodData Clone()
        {
            return new BusinessAccountingPeriodData
            {
                accountingPeriodId = accountingPeriodId ?? string.Empty,
                businessId = businessId ?? string.Empty,
                startWorldTime = startWorldTime,
                endWorldTime = endWorldTime,
                currencyId = currencyId ?? string.Empty,
                state = state,
                includedRevenueRecordIds = BusinessModelHelpers.CleanIds(includedRevenueRecordIds),
                includedExpenseRecordIds = BusinessModelHelpers.CleanIds(includedExpenseRecordIds),
                capitalContributionIds = BusinessModelHelpers.CleanIds(capitalContributionIds),
                ownerWithdrawalIds = BusinessModelHelpers.CleanIds(ownerWithdrawalIds),
                openingAccountIds = BusinessModelHelpers.CleanIds(openingAccountIds),
                closingAccountIds = BusinessModelHelpers.CleanIds(closingAccountIds),
                inventoryValueReferenceId = inventoryValueReferenceId ?? string.Empty,
                statementIds = BusinessModelHelpers.CleanIds(statementIds),
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class BusinessProfitAndLossStatementData
    {
        public string statementId;
        public string businessId;
        public string accountingPeriodId;
        public string currencyId;
        public BusinessMoneyData revenueTotal = new BusinessMoneyData();
        public BusinessMoneyData refundAndReductionTotal = new BusinessMoneyData();
        public BusinessMoneyData inventoryAndMaterialExpenseTotal = new BusinessMoneyData();
        public BusinessMoneyData payrollExpenseTotal = new BusinessMoneyData();
        public BusinessMoneyData operatingExpenseTotal = new BusinessMoneyData();
        public BusinessMoneyData otherExpenseTotal = new BusinessMoneyData();
        public BusinessMoneyData grossOperatingResult = new BusinessMoneyData();
        public BusinessMoneyData netOperatingResult = new BusinessMoneyData();
        public string[] sourceRevenueRecordIds = Array.Empty<string>();
        public string[] sourceExpenseRecordIds = Array.Empty<string>();
        public string[] appliedPolicyIds = Array.Empty<string>();
        public string calculationDiagnostics;
        public double creationWorldTime;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public BusinessProfitAndLossStatementData Clone()
        {
            return new BusinessProfitAndLossStatementData
            {
                statementId = statementId ?? string.Empty,
                businessId = businessId ?? string.Empty,
                accountingPeriodId = accountingPeriodId ?? string.Empty,
                currencyId = currencyId ?? string.Empty,
                revenueTotal = revenueTotal?.Clone() ?? new BusinessMoneyData(),
                refundAndReductionTotal = refundAndReductionTotal?.Clone() ?? new BusinessMoneyData(),
                inventoryAndMaterialExpenseTotal = inventoryAndMaterialExpenseTotal?.Clone() ?? new BusinessMoneyData(),
                payrollExpenseTotal = payrollExpenseTotal?.Clone() ?? new BusinessMoneyData(),
                operatingExpenseTotal = operatingExpenseTotal?.Clone() ?? new BusinessMoneyData(),
                otherExpenseTotal = otherExpenseTotal?.Clone() ?? new BusinessMoneyData(),
                grossOperatingResult = grossOperatingResult?.Clone() ?? new BusinessMoneyData(),
                netOperatingResult = netOperatingResult?.Clone() ?? new BusinessMoneyData(),
                sourceRevenueRecordIds = BusinessModelHelpers.CleanIds(sourceRevenueRecordIds),
                sourceExpenseRecordIds = BusinessModelHelpers.CleanIds(sourceExpenseRecordIds),
                appliedPolicyIds = BusinessModelHelpers.CleanIds(appliedPolicyIds),
                calculationDiagnostics = calculationDiagnostics ?? string.Empty,
                creationWorldTime = creationWorldTime,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class BusinessCashFlowSummaryData
    {
        public string summaryId;
        public string businessId;
        public string accountingPeriodId;
        public string currencyId;
        public BusinessMoneyData operatingInflows = new BusinessMoneyData();
        public BusinessMoneyData operatingOutflows = new BusinessMoneyData();
        public BusinessMoneyData payrollOutflows = new BusinessMoneyData();
        public BusinessMoneyData capitalInflows = new BusinessMoneyData();
        public BusinessMoneyData ownerWithdrawals = new BusinessMoneyData();
        public BusinessMoneyData assetPurchases = new BusinessMoneyData();
        public BusinessMoneyData financingFoundation = new BusinessMoneyData();
        public BusinessMoneyData netCashChange = new BusinessMoneyData();
        public string[] sourceTransactionIds = Array.Empty<string>();
        public string diagnostics;
        public double creationWorldTime;
        public string accessPolicyId;
        public long revision = 1L;

        public BusinessCashFlowSummaryData Clone()
        {
            return new BusinessCashFlowSummaryData
            {
                summaryId = summaryId ?? string.Empty,
                businessId = businessId ?? string.Empty,
                accountingPeriodId = accountingPeriodId ?? string.Empty,
                currencyId = currencyId ?? string.Empty,
                operatingInflows = operatingInflows?.Clone() ?? new BusinessMoneyData(),
                operatingOutflows = operatingOutflows?.Clone() ?? new BusinessMoneyData(),
                payrollOutflows = payrollOutflows?.Clone() ?? new BusinessMoneyData(),
                capitalInflows = capitalInflows?.Clone() ?? new BusinessMoneyData(),
                ownerWithdrawals = ownerWithdrawals?.Clone() ?? new BusinessMoneyData(),
                assetPurchases = assetPurchases?.Clone() ?? new BusinessMoneyData(),
                financingFoundation = financingFoundation?.Clone() ?? new BusinessMoneyData(),
                netCashChange = netCashChange?.Clone() ?? new BusinessMoneyData(),
                sourceTransactionIds = BusinessModelHelpers.CleanIds(sourceTransactionIds),
                diagnostics = diagnostics ?? string.Empty,
                creationWorldTime = creationWorldTime,
                accessPolicyId = accessPolicyId ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class BusinessRuntimeSaveData
    {
        public int schemaVersion = BusinessRuntime.CurrentSaveSchemaVersion;
        public long revision;
        public BusinessInstanceData[] businesses = Array.Empty<BusinessInstanceData>();
        public BusinessOwnershipRecordData[] ownershipRecords = Array.Empty<BusinessOwnershipRecordData>();
        public BusinessControlRecordData[] controlRecords = Array.Empty<BusinessControlRecordData>();
        public BusinessEstablishmentData[] establishments = Array.Empty<BusinessEstablishmentData>();
        public BusinessAccountAssignmentData[] accountAssignments = Array.Empty<BusinessAccountAssignmentData>();
        public BusinessInventoryAssignmentData[] inventoryAssignments = Array.Empty<BusinessInventoryAssignmentData>();
        public BusinessStockClassificationData[] stockClassifications = Array.Empty<BusinessStockClassificationData>();
        public BusinessProductionOwnershipData[] productionOwnershipRecords = Array.Empty<BusinessProductionOwnershipData>();
        public BusinessFundingAllocationData[] fundingAllocations = Array.Empty<BusinessFundingAllocationData>();
        public BusinessRevenueRecordData[] revenueRecords = Array.Empty<BusinessRevenueRecordData>();
        public BusinessExpenseRecordData[] expenseRecords = Array.Empty<BusinessExpenseRecordData>();
        public BusinessCapitalContributionData[] capitalContributions = Array.Empty<BusinessCapitalContributionData>();
        public BusinessOwnerWithdrawalData[] ownerWithdrawals = Array.Empty<BusinessOwnerWithdrawalData>();
        public BusinessAccountingPeriodData[] accountingPeriods = Array.Empty<BusinessAccountingPeriodData>();
        public BusinessProfitAndLossStatementData[] profitAndLossStatements = Array.Empty<BusinessProfitAndLossStatementData>();
        public BusinessCashFlowSummaryData[] cashFlowSummaries = Array.Empty<BusinessCashFlowSummaryData>();

        public BusinessRuntimeSaveData Clone()
        {
            return new BusinessRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                revision = revision,
                businesses = (businesses ?? Array.Empty<BusinessInstanceData>()).Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                ownershipRecords = (ownershipRecords ?? Array.Empty<BusinessOwnershipRecordData>()).Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                controlRecords = (controlRecords ?? Array.Empty<BusinessControlRecordData>()).Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                establishments = (establishments ?? Array.Empty<BusinessEstablishmentData>()).Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                accountAssignments = (accountAssignments ?? Array.Empty<BusinessAccountAssignmentData>()).Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                inventoryAssignments = (inventoryAssignments ?? Array.Empty<BusinessInventoryAssignmentData>()).Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                stockClassifications = (stockClassifications ?? Array.Empty<BusinessStockClassificationData>()).Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                productionOwnershipRecords = (productionOwnershipRecords ?? Array.Empty<BusinessProductionOwnershipData>()).Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                fundingAllocations = (fundingAllocations ?? Array.Empty<BusinessFundingAllocationData>()).Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                revenueRecords = (revenueRecords ?? Array.Empty<BusinessRevenueRecordData>()).Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                expenseRecords = (expenseRecords ?? Array.Empty<BusinessExpenseRecordData>()).Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                capitalContributions = (capitalContributions ?? Array.Empty<BusinessCapitalContributionData>()).Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                ownerWithdrawals = (ownerWithdrawals ?? Array.Empty<BusinessOwnerWithdrawalData>()).Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                accountingPeriods = (accountingPeriods ?? Array.Empty<BusinessAccountingPeriodData>()).Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                profitAndLossStatements = (profitAndLossStatements ?? Array.Empty<BusinessProfitAndLossStatementData>()).Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                cashFlowSummaries = (cashFlowSummaries ?? Array.Empty<BusinessCashFlowSummaryData>()).Select(item => item?.Clone()).Where(item => item != null).ToArray()
            };
        }
    }

    public sealed class BusinessProjection
    {
        public BusinessProjection(BusinessInstanceData business, InformationAccessDecision decision, bool redacted, IReadOnlyList<string> visibleDetails)
        {
            Business = business;
            Decision = decision;
            Redacted = redacted;
            VisibleDetails = visibleDetails ?? Array.Empty<string>();
        }

        public BusinessInstanceData Business { get; }
        public InformationAccessDecision Decision { get; }
        public bool Redacted { get; }
        public bool Denied => Business == null || (Decision != null && Decision.Denied);
        public IReadOnlyList<string> VisibleDetails { get; }
    }

    public sealed class BusinessPerformanceSummary
    {
        public string businessId;
        public BusinessState state;
        public int activeEstablishments;
        public int activeOwnershipRecords;
        public int activeEmployees;
        public int vacantPositions;
        public int retailStockRecords;
        public int productionInputRecords;
        public int workInProgressRecords;
        public int finishedGoodsRecords;
        public int openProductionJobs;
        public int completedTrades;
        public long revenueUnits;
        public long expenseUnits;
        public long netProfitUnits;
        public long netCashChangeUnits;
        public string currencyId;

        public BusinessPerformanceSummary Clone()
        {
            return (BusinessPerformanceSummary)MemberwiseClone();
        }
    }

    public sealed class BusinessOperationResult
    {
        private BusinessOperationResult(BusinessOperationCode code, string message, long beforeRevision, long afterRevision, bool preview, bool duplicate)
        {
            Code = code;
            Message = message ?? string.Empty;
            BeforeRevision = beforeRevision;
            AfterRevision = afterRevision;
            Preview = preview;
            Duplicate = duplicate;
        }

        public BusinessOperationCode Code { get; }
        public string Message { get; }
        public long BeforeRevision { get; }
        public long AfterRevision { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public bool Succeeded => Code == BusinessOperationCode.Succeeded || Code == BusinessOperationCode.Preview;
        public BusinessInstanceData Business { get; private set; }
        public BusinessOwnershipRecordData Ownership { get; private set; }
        public BusinessControlRecordData Control { get; private set; }
        public BusinessEstablishmentData Establishment { get; private set; }
        public BusinessAccountAssignmentData AccountAssignment { get; private set; }
        public BusinessInventoryAssignmentData InventoryAssignment { get; private set; }
        public BusinessStockClassificationData StockClassification { get; private set; }
        public BusinessProductionOwnershipData ProductionOwnership { get; private set; }
        public BusinessFundingAllocationData FundingAllocation { get; private set; }
        public BusinessRevenueRecordData Revenue { get; private set; }
        public BusinessExpenseRecordData Expense { get; private set; }
        public BusinessCapitalContributionData CapitalContribution { get; private set; }
        public BusinessOwnerWithdrawalData OwnerWithdrawal { get; private set; }
        public BusinessAccountingPeriodData AccountingPeriod { get; private set; }
        public BusinessProfitAndLossStatementData ProfitAndLossStatement { get; private set; }
        public BusinessCashFlowSummaryData CashFlowSummary { get; private set; }

        public static BusinessOperationResult Success(string message, long before, long after, bool duplicate = false)
        {
            return new BusinessOperationResult(BusinessOperationCode.Succeeded, message, before, after, false, duplicate);
        }

        public static BusinessOperationResult PreviewResult(string message, long revision)
        {
            return new BusinessOperationResult(BusinessOperationCode.Preview, message, revision, revision, true, false);
        }

        public static BusinessOperationResult Failure(BusinessOperationCode code, string message, long revision)
        {
            return new BusinessOperationResult(code == BusinessOperationCode.Succeeded ? BusinessOperationCode.InvalidRequest : code, message, revision, revision, false, false);
        }

        public BusinessOperationResult With(
            BusinessInstanceData business = null,
            BusinessOwnershipRecordData ownership = null,
            BusinessControlRecordData control = null,
            BusinessEstablishmentData establishment = null,
            BusinessAccountAssignmentData accountAssignment = null,
            BusinessInventoryAssignmentData inventoryAssignment = null,
            BusinessStockClassificationData stockClassification = null,
            BusinessProductionOwnershipData productionOwnership = null,
            BusinessFundingAllocationData fundingAllocation = null,
            BusinessRevenueRecordData revenue = null,
            BusinessExpenseRecordData expense = null,
            BusinessCapitalContributionData capitalContribution = null,
            BusinessOwnerWithdrawalData ownerWithdrawal = null,
            BusinessAccountingPeriodData accountingPeriod = null,
            BusinessProfitAndLossStatementData profitAndLossStatement = null,
            BusinessCashFlowSummaryData cashFlowSummary = null)
        {
            Business = business?.Clone();
            Ownership = ownership?.Clone();
            Control = control?.Clone();
            Establishment = establishment?.Clone();
            AccountAssignment = accountAssignment?.Clone();
            InventoryAssignment = inventoryAssignment?.Clone();
            StockClassification = stockClassification?.Clone();
            ProductionOwnership = productionOwnership?.Clone();
            FundingAllocation = fundingAllocation?.Clone();
            Revenue = revenue?.Clone();
            Expense = expense?.Clone();
            CapitalContribution = capitalContribution?.Clone();
            OwnerWithdrawal = ownerWithdrawal?.Clone();
            AccountingPeriod = accountingPeriod?.Clone();
            ProfitAndLossStatement = profitAndLossStatement?.Clone();
            CashFlowSummary = cashFlowSummary?.Clone();
            return this;
        }
    }

    public static class BusinessInformationSubject
    {
        public static InformationSubjectReferenceData Create(string typeId, string stableId, string ownerOrScopeId = "")
        {
            return new InformationSubjectReferenceData
            {
                subjectType = InformationSubjectType.Custom,
                subjectId = stableId ?? string.Empty,
                parentSubjectId = ownerOrScopeId ?? string.Empty,
                ownerPersonId = string.Empty,
                controllingEntityId = ownerOrScopeId ?? string.Empty,
                tags = BusinessModelHelpers.CleanIds(new[] { typeId })
            };
        }
    }
}
