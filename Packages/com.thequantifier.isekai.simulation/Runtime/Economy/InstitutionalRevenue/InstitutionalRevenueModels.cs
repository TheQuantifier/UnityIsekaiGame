using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Economy.InstitutionalRevenue
{
    public static class InstitutionalRevenueModelHelpers
    {
        public static string[] CleanIds(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        public static T[] CloneArray<T>(IEnumerable<T> values, Func<T, T> clone)
        {
            return (values ?? Array.Empty<T>())
                .Where(value => value != null)
                .Select(clone)
                .ToArray();
        }

        public static InformationSubjectReferenceData Subject(string id, string parent = "", string owner = "", string controlling = "", params string[] tags)
        {
            return new InformationSubjectReferenceData
            {
                subjectType = InformationSubjectType.Custom,
                subjectId = id ?? string.Empty,
                parentSubjectId = parent ?? string.Empty,
                ownerPersonId = owner ?? string.Empty,
                controllingEntityId = controlling ?? string.Empty,
                tags = CleanIds(tags)
            };
        }
    }

    [Serializable]
    public sealed class RevenueRationalData
    {
        public long numerator;
        public long denominator = 1L;

        public RevenueRationalData Clone()
        {
            return new RevenueRationalData { numerator = numerator, denominator = denominator };
        }
    }

    [Serializable]
    public sealed class RevenueBracketData
    {
        public string bracketId;
        public long lowerInclusive;
        public long upperExclusive = -1L;
        public RevenueRationalData rate = new RevenueRationalData();
        public long fixedUnits;
        public int priority;

        public RevenueBracketData Clone()
        {
            return new RevenueBracketData
            {
                bracketId = bracketId ?? string.Empty,
                lowerInclusive = lowerInclusive,
                upperExclusive = upperExclusive,
                rate = rate?.Clone() ?? new RevenueRationalData(),
                fixedUnits = fixedUnits,
                priority = priority
            };
        }
    }

    [Serializable]
    public sealed class RevenueRatePolicyData
    {
        public string ratePolicyId;
        public RevenueRateKind rateKind = RevenueRateKind.FixedAmount;
        public ProgressiveCalculationKind progressiveCalculation = ProgressiveCalculationKind.Marginal;
        public string currencyOrUnitId;
        public RevenueRationalData rate = new RevenueRationalData();
        public long fixedUnits;
        public long perUnitUnits;
        public long thresholdUnits;
        public long minimumUnits;
        public long maximumUnits = -1L;
        public long smallestChargeableUnit = 1L;
        public RevenueRoundingMode roundingMode = RevenueRoundingMode.Down;
        public RevenueBracketData[] brackets = Array.Empty<RevenueBracketData>();
        public double effectiveStartWorldTime;
        public double effectiveEndWorldTime = -1d;
        public string accessPolicyId;
        public int version = 1;

        public RevenueRatePolicyData Clone()
        {
            return new RevenueRatePolicyData
            {
                ratePolicyId = ratePolicyId ?? string.Empty,
                rateKind = rateKind,
                progressiveCalculation = progressiveCalculation,
                currencyOrUnitId = currencyOrUnitId ?? string.Empty,
                rate = rate?.Clone() ?? new RevenueRationalData(),
                fixedUnits = fixedUnits,
                perUnitUnits = perUnitUnits,
                thresholdUnits = thresholdUnits,
                minimumUnits = minimumUnits,
                maximumUnits = maximumUnits,
                smallestChargeableUnit = smallestChargeableUnit <= 0L ? 1L : smallestChargeableUnit,
                roundingMode = roundingMode,
                brackets = InstitutionalRevenueModelHelpers.CloneArray(brackets, item => item.Clone()),
                effectiveStartWorldTime = effectiveStartWorldTime,
                effectiveEndWorldTime = effectiveEndWorldTime,
                accessPolicyId = accessPolicyId ?? string.Empty,
                version = version <= 0 ? 1 : version
            };
        }
    }

    [Serializable]
    public sealed class RevenueSubjectReferenceData
    {
        public RevenueSubjectKind subjectKind;
        public RevenueSubjectRole role;
        public string subjectId;
        public string accountId;
        public string organizationId;
        public string personId;

        public RevenueSubjectReferenceData Clone()
        {
            return new RevenueSubjectReferenceData
            {
                subjectKind = subjectKind,
                role = role,
                subjectId = subjectId ?? string.Empty,
                accountId = accountId ?? string.Empty,
                organizationId = organizationId ?? string.Empty,
                personId = personId ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class InstitutionalRevenueAuthorityData
    {
        public string authorityId;
        public string institutionId;
        public InstitutionKind institutionKind = InstitutionKind.Organization;
        public InstitutionalRevenueAuthorityCategory authorityCategory = InstitutionalRevenueAuthorityCategory.Assess;
        public string sourceReferenceId;
        public string sourceRuntime;
        public string[] permittedRevenueDefinitionIds = Array.Empty<string>();
        public InstitutionalRevenueCategory[] permittedRevenueCategories = Array.Empty<InstitutionalRevenueCategory>();
        public RevenueSubjectKind[] permittedSubjectKinds = Array.Empty<RevenueSubjectKind>();
        public string[] permittedSubjectIds = Array.Empty<string>();
        public string[] permittedCurrencyIds = Array.Empty<string>();
        public string scopeReferenceId;
        public double effectiveStartWorldTime;
        public double effectiveEndWorldTime = -1d;
        public bool canAssess;
        public bool canCollect;
        public bool canReceiveRemittance;
        public bool canIssueRefund;
        public bool canWaive;
        public bool canAdjust;
        public bool canAudit;
        public bool canAllocateRevenue;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public InstitutionalRevenueAuthorityData Clone()
        {
            return new InstitutionalRevenueAuthorityData
            {
                authorityId = authorityId ?? string.Empty,
                institutionId = institutionId ?? string.Empty,
                institutionKind = institutionKind,
                authorityCategory = authorityCategory,
                sourceReferenceId = sourceReferenceId ?? string.Empty,
                sourceRuntime = sourceRuntime ?? string.Empty,
                permittedRevenueDefinitionIds = InstitutionalRevenueModelHelpers.CleanIds(permittedRevenueDefinitionIds),
                permittedRevenueCategories = permittedRevenueCategories == null ? Array.Empty<InstitutionalRevenueCategory>() : permittedRevenueCategories.Distinct().OrderBy(item => item).ToArray(),
                permittedSubjectKinds = permittedSubjectKinds == null ? Array.Empty<RevenueSubjectKind>() : permittedSubjectKinds.Distinct().OrderBy(item => item).ToArray(),
                permittedSubjectIds = InstitutionalRevenueModelHelpers.CleanIds(permittedSubjectIds),
                permittedCurrencyIds = InstitutionalRevenueModelHelpers.CleanIds(permittedCurrencyIds),
                scopeReferenceId = scopeReferenceId ?? string.Empty,
                effectiveStartWorldTime = effectiveStartWorldTime,
                effectiveEndWorldTime = effectiveEndWorldTime,
                canAssess = canAssess,
                canCollect = canCollect,
                canReceiveRemittance = canReceiveRemittance,
                canIssueRefund = canIssueRefund,
                canWaive = canWaive,
                canAdjust = canAdjust,
                canAudit = canAudit,
                canAllocateRevenue = canAllocateRevenue,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class InstitutionalRevenueAccountAssignmentData
    {
        public string assignmentId;
        public string institutionId;
        public InstitutionKind institutionKind = InstitutionKind.Organization;
        public string accountId;
        public RevenueAccountPurpose purpose = RevenueAccountPurpose.GeneralTreasury;
        public string currencyId;
        public string receivingAuthorityId;
        public string allocationPolicyId;
        public double effectiveStartWorldTime;
        public double effectiveEndWorldTime = -1d;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public InstitutionalRevenueAccountAssignmentData Clone()
        {
            return new InstitutionalRevenueAccountAssignmentData
            {
                assignmentId = assignmentId ?? string.Empty,
                institutionId = institutionId ?? string.Empty,
                institutionKind = institutionKind,
                accountId = accountId ?? string.Empty,
                purpose = purpose,
                currencyId = currencyId ?? string.Empty,
                receivingAuthorityId = receivingAuthorityId ?? string.Empty,
                allocationPolicyId = allocationPolicyId ?? string.Empty,
                effectiveStartWorldTime = effectiveStartWorldTime,
                effectiveEndWorldTime = effectiveEndWorldTime,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class TaxableEventData
    {
        public string taxableEventId;
        public string revenueDefinitionId;
        public InstitutionalRevenueCategory eligibleCategory;
        public string sourceRuntime;
        public string sourceRecordId;
        public TaxableEventCategory eventCategory = TaxableEventCategory.Custom;
        public RevenueSubjectReferenceData assessedSubject = new RevenueSubjectReferenceData();
        public RevenueSubjectReferenceData[] otherSubjects = Array.Empty<RevenueSubjectReferenceData>();
        public string institutionId;
        public string currencyId;
        public double eventWorldTime;
        public long monetaryValueUnits;
        public long quantityUnits;
        public string propertyId;
        public string businessId;
        public string payrollRecordId;
        public string tradeRecordId;
        public string transactionId;
        public string itemInstanceId;
        public string contractId;
        public string borderOrRouteReferenceId;
        public string licenseOrPermitReferenceId;
        public string violationOrJudgmentReferenceId;
        public string accessPolicyId;
        public string provenance;
        public string[] sourceRuntimeRevisions = Array.Empty<string>();
        public bool exclusiveAssessment = true;
        public long revision = 1L;

        public TaxableEventData Clone()
        {
            return new TaxableEventData
            {
                taxableEventId = taxableEventId ?? string.Empty,
                revenueDefinitionId = revenueDefinitionId ?? string.Empty,
                eligibleCategory = eligibleCategory,
                sourceRuntime = sourceRuntime ?? string.Empty,
                sourceRecordId = sourceRecordId ?? string.Empty,
                eventCategory = eventCategory,
                assessedSubject = assessedSubject?.Clone() ?? new RevenueSubjectReferenceData(),
                otherSubjects = InstitutionalRevenueModelHelpers.CloneArray(otherSubjects, item => item.Clone()),
                institutionId = institutionId ?? string.Empty,
                currencyId = currencyId ?? string.Empty,
                eventWorldTime = eventWorldTime,
                monetaryValueUnits = monetaryValueUnits,
                quantityUnits = quantityUnits,
                propertyId = propertyId ?? string.Empty,
                businessId = businessId ?? string.Empty,
                payrollRecordId = payrollRecordId ?? string.Empty,
                tradeRecordId = tradeRecordId ?? string.Empty,
                transactionId = transactionId ?? string.Empty,
                itemInstanceId = itemInstanceId ?? string.Empty,
                contractId = contractId ?? string.Empty,
                borderOrRouteReferenceId = borderOrRouteReferenceId ?? string.Empty,
                licenseOrPermitReferenceId = licenseOrPermitReferenceId ?? string.Empty,
                violationOrJudgmentReferenceId = violationOrJudgmentReferenceId ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                sourceRuntimeRevisions = InstitutionalRevenueModelHelpers.CleanIds(sourceRuntimeRevisions),
                exclusiveAssessment = exclusiveAssessment,
                revision = revision
            };
        }

        public InformationSubjectReferenceData CreateInformationSubject()
        {
            return InstitutionalRevenueModelHelpers.Subject(taxableEventId, institutionId, assessedSubject?.personId, institutionId, "institutional-revenue", "taxable-event", eventCategory.ToString());
        }
    }

    [Serializable]
    public sealed class RevenueAdjustmentData
    {
        public string adjustmentId;
        public string revenueDefinitionId;
        public string subjectId;
        public string assessmentId;
        public string periodId;
        public string sourceReferenceId;
        public long amountUnits;
        public RevenueRationalData share = new RevenueRationalData();
        public bool fullExemption;
        public bool refundable;
        public int priority;
        public double effectiveStartWorldTime;
        public double expirationWorldTime = -1d;
        public string approvalAuthorityId;
        public string reason;
        public string accessPolicyId;
        public string provenance;
        public long amountUsedUnits;
        public long revision = 1L;

        public long RemainingUnits => Math.Max(0L, amountUnits - amountUsedUnits);

        public RevenueAdjustmentData Clone()
        {
            return new RevenueAdjustmentData
            {
                adjustmentId = adjustmentId ?? string.Empty,
                revenueDefinitionId = revenueDefinitionId ?? string.Empty,
                subjectId = subjectId ?? string.Empty,
                assessmentId = assessmentId ?? string.Empty,
                periodId = periodId ?? string.Empty,
                sourceReferenceId = sourceReferenceId ?? string.Empty,
                amountUnits = amountUnits,
                share = share?.Clone() ?? new RevenueRationalData(),
                fullExemption = fullExemption,
                refundable = refundable,
                priority = priority,
                effectiveStartWorldTime = effectiveStartWorldTime,
                expirationWorldTime = expirationWorldTime,
                approvalAuthorityId = approvalAuthorityId ?? string.Empty,
                reason = reason ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                amountUsedUnits = amountUsedUnits,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class TaxBaseCalculationData
    {
        public string baseCalculationId;
        public string revenueDefinitionId;
        public string taxableEventId;
        public RevenueSubjectReferenceData subject = new RevenueSubjectReferenceData();
        public TaxBaseKind baseKind = TaxBaseKind.FixedAmount;
        public string currencyOrUnitId;
        public long grossBaseUnits;
        public long exclusionUnits;
        public long exemptUnits;
        public long deductionUnits;
        public long finalTaxableBaseUnits;
        public string[] appliedPolicyIds = Array.Empty<string>();
        public string[] sourceReferenceIds = Array.Empty<string>();
        public string[] sourceRevisionTokens = Array.Empty<string>();
        public double calculationWorldTime;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public TaxBaseCalculationData Clone()
        {
            return new TaxBaseCalculationData
            {
                baseCalculationId = baseCalculationId ?? string.Empty,
                revenueDefinitionId = revenueDefinitionId ?? string.Empty,
                taxableEventId = taxableEventId ?? string.Empty,
                subject = subject?.Clone() ?? new RevenueSubjectReferenceData(),
                baseKind = baseKind,
                currencyOrUnitId = currencyOrUnitId ?? string.Empty,
                grossBaseUnits = grossBaseUnits,
                exclusionUnits = exclusionUnits,
                exemptUnits = exemptUnits,
                deductionUnits = deductionUnits,
                finalTaxableBaseUnits = finalTaxableBaseUnits,
                appliedPolicyIds = InstitutionalRevenueModelHelpers.CleanIds(appliedPolicyIds),
                sourceReferenceIds = InstitutionalRevenueModelHelpers.CleanIds(sourceReferenceIds),
                sourceRevisionTokens = InstitutionalRevenueModelHelpers.CleanIds(sourceRevisionTokens),
                calculationWorldTime = calculationWorldTime,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class AssessmentPeriodData
    {
        public string periodId;
        public string revenueDefinitionId;
        public string institutionId;
        public string subjectId;
        public AssessmentPeriodKind periodKind = AssessmentPeriodKind.PerEvent;
        public AssessmentPeriodState state = AssessmentPeriodState.Open;
        public double startWorldTime;
        public double endWorldTime;
        public double filingDueWorldTime = -1d;
        public double paymentDueWorldTime = -1d;
        public string[] taxableEventIds = Array.Empty<string>();
        public string[] assessmentIds = Array.Empty<string>();
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public AssessmentPeriodData Clone()
        {
            return new AssessmentPeriodData
            {
                periodId = periodId ?? string.Empty,
                revenueDefinitionId = revenueDefinitionId ?? string.Empty,
                institutionId = institutionId ?? string.Empty,
                subjectId = subjectId ?? string.Empty,
                periodKind = periodKind,
                state = state,
                startWorldTime = startWorldTime,
                endWorldTime = endWorldTime,
                filingDueWorldTime = filingDueWorldTime,
                paymentDueWorldTime = paymentDueWorldTime,
                taxableEventIds = InstitutionalRevenueModelHelpers.CleanIds(taxableEventIds),
                assessmentIds = InstitutionalRevenueModelHelpers.CleanIds(assessmentIds),
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class InstitutionalAssessmentData
    {
        public string assessmentId;
        public string revenueDefinitionId;
        public string institutionId;
        public RevenueSubjectReferenceData assessedSubject = new RevenueSubjectReferenceData();
        public RevenueSubjectReferenceData withholdingSubject = new RevenueSubjectReferenceData();
        public RevenueSubjectReferenceData remittingSubject = new RevenueSubjectReferenceData();
        public string periodId;
        public string[] taxableEventIds = Array.Empty<string>();
        public string[] baseCalculationIds = Array.Empty<string>();
        public string currencyId;
        public long grossChargeUnits;
        public long exemptionUnits;
        public long deductionUnits;
        public long creditUnits;
        public long penaltyUnits;
        public long finalAssessedUnits;
        public long alreadyWithheldUnits;
        public long amountDueUnits;
        public long amountPaidUnits;
        public double dueWorldTime = -1d;
        public string approvalAuthorityId;
        public RevenueAssessmentState state = RevenueAssessmentState.Calculated;
        public string priorAssessmentId;
        public string correctedByAssessmentId;
        public string obligationId;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public long OutstandingUnits => Math.Max(0L, amountDueUnits - amountPaidUnits);
        public bool Immutable => state is RevenueAssessmentState.Approved or RevenueAssessmentState.Issued or RevenueAssessmentState.Due or RevenueAssessmentState.PartiallyPaid or RevenueAssessmentState.Paid or RevenueAssessmentState.Overdue or RevenueAssessmentState.Corrected or RevenueAssessmentState.Replaced or RevenueAssessmentState.Waived or RevenueAssessmentState.Cancelled or RevenueAssessmentState.Disputed;

        public InstitutionalAssessmentData Clone()
        {
            return new InstitutionalAssessmentData
            {
                assessmentId = assessmentId ?? string.Empty,
                revenueDefinitionId = revenueDefinitionId ?? string.Empty,
                institutionId = institutionId ?? string.Empty,
                assessedSubject = assessedSubject?.Clone() ?? new RevenueSubjectReferenceData(),
                withholdingSubject = withholdingSubject?.Clone() ?? new RevenueSubjectReferenceData(),
                remittingSubject = remittingSubject?.Clone() ?? new RevenueSubjectReferenceData(),
                periodId = periodId ?? string.Empty,
                taxableEventIds = InstitutionalRevenueModelHelpers.CleanIds(taxableEventIds),
                baseCalculationIds = InstitutionalRevenueModelHelpers.CleanIds(baseCalculationIds),
                currencyId = currencyId ?? string.Empty,
                grossChargeUnits = grossChargeUnits,
                exemptionUnits = exemptionUnits,
                deductionUnits = deductionUnits,
                creditUnits = creditUnits,
                penaltyUnits = penaltyUnits,
                finalAssessedUnits = finalAssessedUnits,
                alreadyWithheldUnits = alreadyWithheldUnits,
                amountDueUnits = amountDueUnits,
                amountPaidUnits = amountPaidUnits,
                dueWorldTime = dueWorldTime,
                approvalAuthorityId = approvalAuthorityId ?? string.Empty,
                state = state,
                priorAssessmentId = priorAssessmentId ?? string.Empty,
                correctedByAssessmentId = correctedByAssessmentId ?? string.Empty,
                obligationId = obligationId ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = revision
            };
        }

        public InformationSubjectReferenceData CreateInformationSubject()
        {
            return InstitutionalRevenueModelHelpers.Subject(assessmentId, periodId, assessedSubject?.personId, institutionId, "institutional-revenue", "assessment", revenueDefinitionId);
        }
    }

    [Serializable]
    public sealed class InstitutionalObligationData
    {
        public string obligationId;
        public string assessmentId;
        public string revenueDefinitionId;
        public string institutionId;
        public string payerSubjectId;
        public string payerAccountId;
        public string institutionAccountId;
        public string currencyId;
        public long amountDueUnits;
        public long amountPaidUnits;
        public long amountWaivedUnits;
        public double dueWorldTime = -1d;
        public InstitutionalObligationState state = InstitutionalObligationState.Due;
        public string[] paymentIds = Array.Empty<string>();
        public string[] waiverIds = Array.Empty<string>();
        public string[] refundIds = Array.Empty<string>();
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public long OutstandingUnits => Math.Max(0L, amountDueUnits - amountPaidUnits - amountWaivedUnits);

        public InstitutionalObligationData Clone()
        {
            return new InstitutionalObligationData
            {
                obligationId = obligationId ?? string.Empty,
                assessmentId = assessmentId ?? string.Empty,
                revenueDefinitionId = revenueDefinitionId ?? string.Empty,
                institutionId = institutionId ?? string.Empty,
                payerSubjectId = payerSubjectId ?? string.Empty,
                payerAccountId = payerAccountId ?? string.Empty,
                institutionAccountId = institutionAccountId ?? string.Empty,
                currencyId = currencyId ?? string.Empty,
                amountDueUnits = amountDueUnits,
                amountPaidUnits = amountPaidUnits,
                amountWaivedUnits = amountWaivedUnits,
                dueWorldTime = dueWorldTime,
                state = state,
                paymentIds = InstitutionalRevenueModelHelpers.CleanIds(paymentIds),
                waiverIds = InstitutionalRevenueModelHelpers.CleanIds(waiverIds),
                refundIds = InstitutionalRevenueModelHelpers.CleanIds(refundIds),
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class InstitutionalPaymentData
    {
        public string paymentId;
        public string obligationId;
        public string assessmentId;
        public string revenueDefinitionId;
        public string institutionId;
        public string economyTransactionId;
        public string payerAccountId;
        public string institutionAccountId;
        public string currencyId;
        public long units;
        public double worldTime;
        public string receiptId;
        public bool remittance;
        public string withholdingId;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public InstitutionalPaymentData Clone()
        {
            return new InstitutionalPaymentData
            {
                paymentId = paymentId ?? string.Empty,
                obligationId = obligationId ?? string.Empty,
                assessmentId = assessmentId ?? string.Empty,
                revenueDefinitionId = revenueDefinitionId ?? string.Empty,
                institutionId = institutionId ?? string.Empty,
                economyTransactionId = economyTransactionId ?? string.Empty,
                payerAccountId = payerAccountId ?? string.Empty,
                institutionAccountId = institutionAccountId ?? string.Empty,
                currencyId = currencyId ?? string.Empty,
                units = units,
                worldTime = worldTime,
                receiptId = receiptId ?? string.Empty,
                remittance = remittance,
                withholdingId = withholdingId ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class WithholdingRecordData
    {
        public string withholdingId;
        public string assessmentId;
        public string revenueDefinitionId;
        public string withholdingAgentSubjectId;
        public string remittingPartySubjectId;
        public string withheldFromAccountId;
        public string holdingAccountId;
        public string institutionAccountId;
        public string currencyId;
        public long withheldUnits;
        public long remittedUnits;
        public double withheldWorldTime;
        public double dueWorldTime = -1d;
        public WithholdingState state = WithholdingState.Withheld;
        public string sourceTransactionId;
        public string remittancePaymentId;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public long UnremittedUnits => Math.Max(0L, withheldUnits - remittedUnits);

        public WithholdingRecordData Clone()
        {
            return new WithholdingRecordData
            {
                withholdingId = withholdingId ?? string.Empty,
                assessmentId = assessmentId ?? string.Empty,
                revenueDefinitionId = revenueDefinitionId ?? string.Empty,
                withholdingAgentSubjectId = withholdingAgentSubjectId ?? string.Empty,
                remittingPartySubjectId = remittingPartySubjectId ?? string.Empty,
                withheldFromAccountId = withheldFromAccountId ?? string.Empty,
                holdingAccountId = holdingAccountId ?? string.Empty,
                institutionAccountId = institutionAccountId ?? string.Empty,
                currencyId = currencyId ?? string.Empty,
                withheldUnits = withheldUnits,
                remittedUnits = remittedUnits,
                withheldWorldTime = withheldWorldTime,
                dueWorldTime = dueWorldTime,
                state = state,
                sourceTransactionId = sourceTransactionId ?? string.Empty,
                remittancePaymentId = remittancePaymentId ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class InstitutionalRevenueRecordData
    {
        public string revenueRecordId;
        public string institutionId;
        public string revenueDefinitionId;
        public string sourcePaymentId;
        public string economyTransactionId;
        public string currencyId;
        public long units;
        public double recognizedWorldTime;
        public string classification;
        public string[] allocationIds = Array.Empty<string>();
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public InstitutionalRevenueRecordData Clone()
        {
            return new InstitutionalRevenueRecordData
            {
                revenueRecordId = revenueRecordId ?? string.Empty,
                institutionId = institutionId ?? string.Empty,
                revenueDefinitionId = revenueDefinitionId ?? string.Empty,
                sourcePaymentId = sourcePaymentId ?? string.Empty,
                economyTransactionId = economyTransactionId ?? string.Empty,
                currencyId = currencyId ?? string.Empty,
                units = units,
                recognizedWorldTime = recognizedWorldTime,
                classification = classification ?? string.Empty,
                allocationIds = InstitutionalRevenueModelHelpers.CleanIds(allocationIds),
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class RevenueAllocationData
    {
        public string allocationId;
        public string revenueRecordId;
        public string fromAccountId;
        public string toAccountId;
        public string currencyId;
        public long units;
        public string economyTransactionId;
        public string authorityId;
        public double worldTime;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public RevenueAllocationData Clone()
        {
            return new RevenueAllocationData
            {
                allocationId = allocationId ?? string.Empty,
                revenueRecordId = revenueRecordId ?? string.Empty,
                fromAccountId = fromAccountId ?? string.Empty,
                toAccountId = toAccountId ?? string.Empty,
                currencyId = currencyId ?? string.Empty,
                units = units,
                economyTransactionId = economyTransactionId ?? string.Empty,
                authorityId = authorityId ?? string.Empty,
                worldTime = worldTime,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class RevenueWaiverData
    {
        public string waiverId;
        public string obligationId;
        public string assessmentId;
        public string authorityId;
        public string reason;
        public long units;
        public double worldTime;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public RevenueWaiverData Clone()
        {
            return new RevenueWaiverData
            {
                waiverId = waiverId ?? string.Empty,
                obligationId = obligationId ?? string.Empty,
                assessmentId = assessmentId ?? string.Empty,
                authorityId = authorityId ?? string.Empty,
                reason = reason ?? string.Empty,
                units = units,
                worldTime = worldTime,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class RevenueRefundData
    {
        public string refundId;
        public string obligationId;
        public string assessmentId;
        public string originalPaymentId;
        public string economyTransactionId;
        public string authorityId;
        public string fromAccountId;
        public string toAccountId;
        public string currencyId;
        public long units;
        public double worldTime;
        public string reason;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public RevenueRefundData Clone()
        {
            return new RevenueRefundData
            {
                refundId = refundId ?? string.Empty,
                obligationId = obligationId ?? string.Empty,
                assessmentId = assessmentId ?? string.Empty,
                originalPaymentId = originalPaymentId ?? string.Empty,
                economyTransactionId = economyTransactionId ?? string.Empty,
                authorityId = authorityId ?? string.Empty,
                fromAccountId = fromAccountId ?? string.Empty,
                toAccountId = toAccountId ?? string.Empty,
                currencyId = currencyId ?? string.Empty,
                units = units,
                worldTime = worldTime,
                reason = reason ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class RevenuePenaltyData
    {
        public string penaltyId;
        public string obligationId;
        public string assessmentId;
        public string policyId;
        public string sourceReferenceId;
        public string currencyId;
        public long units;
        public double appliedWorldTime;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public RevenuePenaltyData Clone()
        {
            return new RevenuePenaltyData
            {
                penaltyId = penaltyId ?? string.Empty,
                obligationId = obligationId ?? string.Empty,
                assessmentId = assessmentId ?? string.Empty,
                policyId = policyId ?? string.Empty,
                sourceReferenceId = sourceReferenceId ?? string.Empty,
                currencyId = currencyId ?? string.Empty,
                units = units,
                appliedWorldTime = appliedWorldTime,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class RevenueFilingData
    {
        public string filingId;
        public string periodId;
        public string revenueDefinitionId;
        public string reportingSubjectId;
        public RevenueFilingState state = RevenueFilingState.Submitted;
        public string[] declaredTaxableEventIds = Array.Empty<string>();
        public string correctedByFilingId;
        public string originalFilingId;
        public double submittedWorldTime;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public RevenueFilingData Clone()
        {
            return new RevenueFilingData
            {
                filingId = filingId ?? string.Empty,
                periodId = periodId ?? string.Empty,
                revenueDefinitionId = revenueDefinitionId ?? string.Empty,
                reportingSubjectId = reportingSubjectId ?? string.Empty,
                state = state,
                declaredTaxableEventIds = InstitutionalRevenueModelHelpers.CleanIds(declaredTaxableEventIds),
                correctedByFilingId = correctedByFilingId ?? string.Empty,
                originalFilingId = originalFilingId ?? string.Empty,
                submittedWorldTime = submittedWorldTime,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class RevenueAuditFindingData
    {
        public string findingId;
        public string filingId;
        public string assessmentId;
        public RevenueAuditFindingKind findingKind = RevenueAuditFindingKind.Match;
        public string sourceReferenceId;
        public string message;
        public double worldTime;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public RevenueAuditFindingData Clone()
        {
            return new RevenueAuditFindingData
            {
                findingId = findingId ?? string.Empty,
                filingId = filingId ?? string.Empty,
                assessmentId = assessmentId ?? string.Empty,
                findingKind = findingKind,
                sourceReferenceId = sourceReferenceId ?? string.Empty,
                message = message ?? string.Empty,
                worldTime = worldTime,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class RevenueStatementData
    {
        public string statementId;
        public string subjectId;
        public string[] assessmentIds = Array.Empty<string>();
        public string[] obligationIds = Array.Empty<string>();
        public string[] paymentIds = Array.Empty<string>();
        public string[] arrearsObligationIds = Array.Empty<string>();
        public string currencyId;
        public long totalDueUnits;
        public long totalPaidUnits;
        public long totalOutstandingUnits;
        public double generatedWorldTime;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public RevenueStatementData Clone()
        {
            return new RevenueStatementData
            {
                statementId = statementId ?? string.Empty,
                subjectId = subjectId ?? string.Empty,
                assessmentIds = InstitutionalRevenueModelHelpers.CleanIds(assessmentIds),
                obligationIds = InstitutionalRevenueModelHelpers.CleanIds(obligationIds),
                paymentIds = InstitutionalRevenueModelHelpers.CleanIds(paymentIds),
                arrearsObligationIds = InstitutionalRevenueModelHelpers.CleanIds(arrearsObligationIds),
                currencyId = currencyId ?? string.Empty,
                totalDueUnits = totalDueUnits,
                totalPaidUnits = totalPaidUnits,
                totalOutstandingUnits = totalOutstandingUnits,
                generatedWorldTime = generatedWorldTime,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class RevenueReceiptData
    {
        public string receiptId;
        public string paymentId;
        public string obligationId;
        public string assessmentId;
        public string economyTransactionId;
        public string currencyId;
        public long units;
        public double issuedWorldTime;
        public string accessPolicyId;
        public string provenance;
        public long revision = 1L;

        public RevenueReceiptData Clone()
        {
            return new RevenueReceiptData
            {
                receiptId = receiptId ?? string.Empty,
                paymentId = paymentId ?? string.Empty,
                obligationId = obligationId ?? string.Empty,
                assessmentId = assessmentId ?? string.Empty,
                economyTransactionId = economyTransactionId ?? string.Empty,
                currencyId = currencyId ?? string.Empty,
                units = units,
                issuedWorldTime = issuedWorldTime,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenance = provenance ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class RevenueProcessedTransactionData
    {
        public string transactionId;
        public string operationKey;
        public RevenueOperationCode code;
        public long revision;

        public RevenueProcessedTransactionData Clone()
        {
            return new RevenueProcessedTransactionData
            {
                transactionId = transactionId ?? string.Empty,
                operationKey = operationKey ?? string.Empty,
                code = code,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class InstitutionalRevenueRuntimeSaveData
    {
        public int schemaVersion = InstitutionalRevenueRuntime.CurrentSaveSchemaVersion;
        public string worldId;
        public long revision;
        public List<InstitutionalRevenueAuthorityData> authorities = new List<InstitutionalRevenueAuthorityData>();
        public List<InstitutionalRevenueAccountAssignmentData> accountAssignments = new List<InstitutionalRevenueAccountAssignmentData>();
        public List<TaxableEventData> taxableEvents = new List<TaxableEventData>();
        public List<TaxBaseCalculationData> baseCalculations = new List<TaxBaseCalculationData>();
        public List<RevenueAdjustmentData> exemptions = new List<RevenueAdjustmentData>();
        public List<RevenueAdjustmentData> deductions = new List<RevenueAdjustmentData>();
        public List<RevenueAdjustmentData> credits = new List<RevenueAdjustmentData>();
        public List<AssessmentPeriodData> periods = new List<AssessmentPeriodData>();
        public List<InstitutionalAssessmentData> assessments = new List<InstitutionalAssessmentData>();
        public List<InstitutionalObligationData> obligations = new List<InstitutionalObligationData>();
        public List<InstitutionalPaymentData> payments = new List<InstitutionalPaymentData>();
        public List<WithholdingRecordData> withholdings = new List<WithholdingRecordData>();
        public List<InstitutionalRevenueRecordData> revenueRecords = new List<InstitutionalRevenueRecordData>();
        public List<RevenueAllocationData> allocations = new List<RevenueAllocationData>();
        public List<RevenueWaiverData> waivers = new List<RevenueWaiverData>();
        public List<RevenueRefundData> refunds = new List<RevenueRefundData>();
        public List<RevenuePenaltyData> penalties = new List<RevenuePenaltyData>();
        public List<RevenueFilingData> filings = new List<RevenueFilingData>();
        public List<RevenueAuditFindingData> auditFindings = new List<RevenueAuditFindingData>();
        public List<RevenueStatementData> statements = new List<RevenueStatementData>();
        public List<RevenueReceiptData> receipts = new List<RevenueReceiptData>();
        public List<RevenueProcessedTransactionData> processedTransactions = new List<RevenueProcessedTransactionData>();

        public InstitutionalRevenueRuntimeSaveData Clone()
        {
            return new InstitutionalRevenueRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                worldId = worldId ?? string.Empty,
                revision = revision,
                authorities = CloneList(authorities, item => item.Clone()),
                accountAssignments = CloneList(accountAssignments, item => item.Clone()),
                taxableEvents = CloneList(taxableEvents, item => item.Clone()),
                baseCalculations = CloneList(baseCalculations, item => item.Clone()),
                exemptions = CloneList(exemptions, item => item.Clone()),
                deductions = CloneList(deductions, item => item.Clone()),
                credits = CloneList(credits, item => item.Clone()),
                periods = CloneList(periods, item => item.Clone()),
                assessments = CloneList(assessments, item => item.Clone()),
                obligations = CloneList(obligations, item => item.Clone()),
                payments = CloneList(payments, item => item.Clone()),
                withholdings = CloneList(withholdings, item => item.Clone()),
                revenueRecords = CloneList(revenueRecords, item => item.Clone()),
                allocations = CloneList(allocations, item => item.Clone()),
                waivers = CloneList(waivers, item => item.Clone()),
                refunds = CloneList(refunds, item => item.Clone()),
                penalties = CloneList(penalties, item => item.Clone()),
                filings = CloneList(filings, item => item.Clone()),
                auditFindings = CloneList(auditFindings, item => item.Clone()),
                statements = CloneList(statements, item => item.Clone()),
                receipts = CloneList(receipts, item => item.Clone()),
                processedTransactions = CloneList(processedTransactions, item => item.Clone())
            };
        }

        private static List<T> CloneList<T>(IEnumerable<T> values, Func<T, T> clone)
        {
            return (values ?? Array.Empty<T>()).Where(value => value != null).Select(clone).ToList();
        }
    }

    public sealed class InstitutionalRevenueOperationResult
    {
        private InstitutionalRevenueOperationResult(bool succeeded, bool preview, bool duplicate, RevenueOperationCode code, string message, long before, long after)
        {
            Succeeded = succeeded;
            Preview = preview;
            Duplicate = duplicate;
            Code = code;
            Message = message ?? string.Empty;
            RevisionBefore = before;
            RevisionAfter = after;
        }

        public bool Succeeded { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public RevenueOperationCode Code { get; }
        public string Message { get; }
        public long RevisionBefore { get; }
        public long RevisionAfter { get; }
        public InstitutionalRevenueAuthorityData Authority { get; private set; }
        public InstitutionalRevenueAccountAssignmentData AccountAssignment { get; private set; }
        public TaxableEventData TaxableEvent { get; private set; }
        public TaxBaseCalculationData BaseCalculation { get; private set; }
        public InstitutionalAssessmentData Assessment { get; private set; }
        public InstitutionalObligationData Obligation { get; private set; }
        public InstitutionalPaymentData Payment { get; private set; }
        public WithholdingRecordData Withholding { get; private set; }
        public InstitutionalRevenueRecordData RevenueRecord { get; private set; }
        public RevenueAllocationData Allocation { get; private set; }
        public RevenueRefundData Refund { get; private set; }
        public RevenueWaiverData Waiver { get; private set; }
        public RevenuePenaltyData Penalty { get; private set; }
        public RevenueFilingData Filing { get; private set; }
        public RevenueAuditFindingData AuditFinding { get; private set; }
        public RevenueStatementData Statement { get; private set; }
        public RevenueReceiptData Receipt { get; private set; }
        public EconomyTransactionData EconomyTransaction { get; private set; }

        public static InstitutionalRevenueOperationResult Success(string message, long before, long after, bool preview = false, bool duplicate = false)
        {
            return new InstitutionalRevenueOperationResult(true, preview, duplicate, preview ? RevenueOperationCode.Preview : duplicate ? RevenueOperationCode.Duplicate : RevenueOperationCode.Succeeded, message, before, after);
        }

        public static InstitutionalRevenueOperationResult Failure(RevenueOperationCode code, string message, long revision, bool preview = false)
        {
            return new InstitutionalRevenueOperationResult(false, preview, false, code, message, revision, revision);
        }

        public InstitutionalRevenueOperationResult With(
            InstitutionalRevenueAuthorityData authority = null,
            InstitutionalRevenueAccountAssignmentData accountAssignment = null,
            TaxableEventData taxableEvent = null,
            TaxBaseCalculationData baseCalculation = null,
            InstitutionalAssessmentData assessment = null,
            InstitutionalObligationData obligation = null,
            InstitutionalPaymentData payment = null,
            WithholdingRecordData withholding = null,
            InstitutionalRevenueRecordData revenueRecord = null,
            RevenueAllocationData allocation = null,
            RevenueRefundData refund = null,
            RevenueWaiverData waiver = null,
            RevenuePenaltyData penalty = null,
            RevenueFilingData filing = null,
            RevenueAuditFindingData auditFinding = null,
            RevenueStatementData statement = null,
            RevenueReceiptData receipt = null,
            EconomyTransactionData economyTransaction = null)
        {
            Authority = authority?.Clone();
            AccountAssignment = accountAssignment?.Clone();
            TaxableEvent = taxableEvent?.Clone();
            BaseCalculation = baseCalculation?.Clone();
            Assessment = assessment?.Clone();
            Obligation = obligation?.Clone();
            Payment = payment?.Clone();
            Withholding = withholding?.Clone();
            RevenueRecord = revenueRecord?.Clone();
            Allocation = allocation?.Clone();
            Refund = refund?.Clone();
            Waiver = waiver?.Clone();
            Penalty = penalty?.Clone();
            Filing = filing?.Clone();
            AuditFinding = auditFinding?.Clone();
            Statement = statement?.Clone();
            Receipt = receipt?.Clone();
            EconomyTransaction = economyTransaction?.Clone();
            return this;
        }
    }

    public sealed class InstitutionalRevenueSnapshot
    {
        public InstitutionalRevenueSnapshot(InstitutionalRevenueRuntimeSaveData data)
        {
            Data = data?.Clone() ?? new InstitutionalRevenueRuntimeSaveData();
            Authorities = new ReadOnlyCollection<InstitutionalRevenueAuthorityData>(Data.authorities.Select(item => item.Clone()).ToList());
            Assessments = new ReadOnlyCollection<InstitutionalAssessmentData>(Data.assessments.Select(item => item.Clone()).ToList());
            Obligations = new ReadOnlyCollection<InstitutionalObligationData>(Data.obligations.Select(item => item.Clone()).ToList());
        }

        public InstitutionalRevenueRuntimeSaveData Data { get; }
        public IReadOnlyList<InstitutionalRevenueAuthorityData> Authorities { get; }
        public IReadOnlyList<InstitutionalAssessmentData> Assessments { get; }
        public IReadOnlyList<InstitutionalObligationData> Obligations { get; }
    }
}
