using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Economy.Payroll
{
    public static class PayrollModelHelpers
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
    }

    [Serializable]
    public sealed class CompensationAgreementData
    {
        public string agreementId;
        public string compensationDefinitionId;
        public string employmentId;
        public string employeePersonId;
        public string employerSubjectId;
        public string employerFundingAccountId;
        public string employeeAccountId;
        public string positionInstanceId;
        public CompensationAgreementState state = CompensationAgreementState.Active;
        public double effectiveStartWorldTime;
        public double effectiveEndWorldTime = -1d;
        public string[] deductionDefinitionIds = Array.Empty<string>();
        public string accessPolicyId;
        public long revision = 1L;

        public CompensationAgreementData Clone()
        {
            return new CompensationAgreementData
            {
                agreementId = agreementId ?? string.Empty,
                compensationDefinitionId = compensationDefinitionId ?? string.Empty,
                employmentId = employmentId ?? string.Empty,
                employeePersonId = employeePersonId ?? string.Empty,
                employerSubjectId = employerSubjectId ?? string.Empty,
                employerFundingAccountId = employerFundingAccountId ?? string.Empty,
                employeeAccountId = employeeAccountId ?? string.Empty,
                positionInstanceId = positionInstanceId ?? string.Empty,
                state = state,
                effectiveStartWorldTime = effectiveStartWorldTime,
                effectiveEndWorldTime = effectiveEndWorldTime,
                deductionDefinitionIds = PayrollModelHelpers.CleanIds(deductionDefinitionIds),
                accessPolicyId = accessPolicyId ?? string.Empty,
                revision = revision
            };
        }

        public InformationSubjectReferenceData CreateInformationSubject()
        {
            return PayrollInformationSubject.Create("payroll.agreement", agreementId, employeePersonId, employerSubjectId);
        }
    }

    [Serializable]
    public sealed class WorkScheduleData
    {
        public string scheduleId;
        public string agreementId;
        public WorkScheduleCategory category = WorkScheduleCategory.FixedShift;
        public long expectedMinutesPerPeriod;
        public long expectedOutputPerPeriod;
        public double startWorldTime;
        public double endWorldTime = -1d;
        public string accessPolicyId;
        public long revision = 1L;

        public WorkScheduleData Clone()
        {
            return new WorkScheduleData
            {
                scheduleId = scheduleId ?? string.Empty,
                agreementId = agreementId ?? string.Empty,
                category = category,
                expectedMinutesPerPeriod = Math.Max(0L, expectedMinutesPerPeriod),
                expectedOutputPerPeriod = Math.Max(0L, expectedOutputPerPeriod),
                startWorldTime = startWorldTime,
                endWorldTime = endWorldTime,
                accessPolicyId = accessPolicyId ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class WorkSessionData
    {
        public string workSessionId;
        public string agreementId;
        public string employmentId;
        public string employeePersonId;
        public double startWorldTime;
        public double endWorldTime;
        public long durationMinutes;
        public long creditedOutputQuantity;
        public string taskDefinitionId;
        public WorkClassification classification = WorkClassification.Regular;
        public string[] evidenceIds = Array.Empty<string>();
        public string sourceId;
        public string accessPolicyId;
        public long revision = 1L;

        public WorkSessionData Clone()
        {
            return new WorkSessionData
            {
                workSessionId = workSessionId ?? string.Empty,
                agreementId = agreementId ?? string.Empty,
                employmentId = employmentId ?? string.Empty,
                employeePersonId = employeePersonId ?? string.Empty,
                startWorldTime = startWorldTime,
                endWorldTime = endWorldTime,
                durationMinutes = Math.Max(0L, durationMinutes),
                creditedOutputQuantity = Math.Max(0L, creditedOutputQuantity),
                taskDefinitionId = taskDefinitionId ?? string.Empty,
                classification = classification,
                evidenceIds = PayrollModelHelpers.CleanIds(evidenceIds),
                sourceId = sourceId ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class TimesheetData
    {
        public string timesheetId;
        public string agreementId;
        public string employmentId;
        public string[] workSessionIds = Array.Empty<string>();
        public TimesheetState state = TimesheetState.Submitted;
        public string submittedByPersonId;
        public string approvedByAuthorityId;
        public string replacementForTimesheetId;
        public double submittedWorldTime;
        public double approvedWorldTime;
        public string accessPolicyId;
        public long revision = 1L;

        public TimesheetData Clone()
        {
            return new TimesheetData
            {
                timesheetId = timesheetId ?? string.Empty,
                agreementId = agreementId ?? string.Empty,
                employmentId = employmentId ?? string.Empty,
                workSessionIds = PayrollModelHelpers.CleanIds(workSessionIds),
                state = state,
                submittedByPersonId = submittedByPersonId ?? string.Empty,
                approvedByAuthorityId = approvedByAuthorityId ?? string.Empty,
                replacementForTimesheetId = replacementForTimesheetId ?? string.Empty,
                submittedWorldTime = submittedWorldTime,
                approvedWorldTime = approvedWorldTime,
                accessPolicyId = accessPolicyId ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class PayPeriodData
    {
        public string payPeriodId;
        public string agreementId;
        public double startWorldTime;
        public double endWorldTime;
        public double dueWorldTime;
        public PayPeriodState state = PayPeriodState.Open;
        public string calculationId;
        public string obligationId;
        public long revision = 1L;

        public PayPeriodData Clone()
        {
            return new PayPeriodData
            {
                payPeriodId = payPeriodId ?? string.Empty,
                agreementId = agreementId ?? string.Empty,
                startWorldTime = startWorldTime,
                endWorldTime = endWorldTime,
                dueWorldTime = dueWorldTime,
                state = state,
                calculationId = calculationId ?? string.Empty,
                obligationId = obligationId ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class CompensationAdjustmentData
    {
        public string adjustmentId;
        public string agreementId;
        public string payPeriodId;
        public CompensationAdjustmentCategory category = CompensationAdjustmentCategory.Bonus;
        public string currencyId;
        public long units;
        public string reason;
        public string evidenceId;
        public long revision = 1L;

        public CompensationAdjustmentData Clone()
        {
            return new CompensationAdjustmentData
            {
                adjustmentId = adjustmentId ?? string.Empty,
                agreementId = agreementId ?? string.Empty,
                payPeriodId = payPeriodId ?? string.Empty,
                category = category,
                currencyId = currencyId ?? string.Empty,
                units = units,
                reason = reason ?? string.Empty,
                evidenceId = evidenceId ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class PayrollDeductionLineData
    {
        public string deductionLineId;
        public string deductionDefinitionId;
        public string recipientAccountId;
        public string currencyId;
        public long units;
        public int priority;

        public PayrollDeductionLineData Clone()
        {
            return new PayrollDeductionLineData
            {
                deductionLineId = deductionLineId ?? string.Empty,
                deductionDefinitionId = deductionDefinitionId ?? string.Empty,
                recipientAccountId = recipientAccountId ?? string.Empty,
                currencyId = currencyId ?? string.Empty,
                units = Math.Max(0L, units),
                priority = Math.Max(0, priority)
            };
        }
    }

    [Serializable]
    public sealed class PayrollCalculationData
    {
        public string calculationId;
        public string agreementId;
        public string payPeriodId;
        public string compensationDefinitionId;
        public string currencyId;
        public long regularGrossUnits;
        public long adjustmentGrossUnits;
        public long reimbursementUnits;
        public long deductionUnits;
        public long netPayUnits;
        public long minutesCredited;
        public long outputCredited;
        public string[] workSessionIds = Array.Empty<string>();
        public string[] adjustmentIds = Array.Empty<string>();
        public List<PayrollDeductionLineData> deductions = new List<PayrollDeductionLineData>();
        public bool preview;
        public long revision = 1L;

        public PayrollCalculationData Clone()
        {
            return new PayrollCalculationData
            {
                calculationId = calculationId ?? string.Empty,
                agreementId = agreementId ?? string.Empty,
                payPeriodId = payPeriodId ?? string.Empty,
                compensationDefinitionId = compensationDefinitionId ?? string.Empty,
                currencyId = currencyId ?? string.Empty,
                regularGrossUnits = Math.Max(0L, regularGrossUnits),
                adjustmentGrossUnits = adjustmentGrossUnits,
                reimbursementUnits = Math.Max(0L, reimbursementUnits),
                deductionUnits = Math.Max(0L, deductionUnits),
                netPayUnits = Math.Max(0L, netPayUnits),
                minutesCredited = Math.Max(0L, minutesCredited),
                outputCredited = Math.Max(0L, outputCredited),
                workSessionIds = PayrollModelHelpers.CleanIds(workSessionIds),
                adjustmentIds = PayrollModelHelpers.CleanIds(adjustmentIds),
                deductions = deductions == null ? new List<PayrollDeductionLineData>() : deductions.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                preview = preview,
                revision = revision
            };
        }

        public long TotalGrossUnits => Math.Max(0L, regularGrossUnits + adjustmentGrossUnits + reimbursementUnits);
    }

    [Serializable]
    public sealed class PayrollObligationData
    {
        public string obligationId;
        public string calculationId;
        public string agreementId;
        public string employeePersonId;
        public string employerSubjectId;
        public string employerFundingAccountId;
        public string employeeAccountId;
        public string currencyId;
        public long amountDueUnits;
        public long amountPaidUnits;
        public long amountOutstandingUnits;
        public double dueWorldTime;
        public PayrollObligationState state = PayrollObligationState.Pending;
        public string reservationId;
        public string payRunId;
        public string[] paymentRecordIds = Array.Empty<string>();
        public long revision = 1L;

        public PayrollObligationData Clone()
        {
            return new PayrollObligationData
            {
                obligationId = obligationId ?? string.Empty,
                calculationId = calculationId ?? string.Empty,
                agreementId = agreementId ?? string.Empty,
                employeePersonId = employeePersonId ?? string.Empty,
                employerSubjectId = employerSubjectId ?? string.Empty,
                employerFundingAccountId = employerFundingAccountId ?? string.Empty,
                employeeAccountId = employeeAccountId ?? string.Empty,
                currencyId = currencyId ?? string.Empty,
                amountDueUnits = Math.Max(0L, amountDueUnits),
                amountPaidUnits = Math.Max(0L, amountPaidUnits),
                amountOutstandingUnits = Math.Max(0L, amountOutstandingUnits),
                dueWorldTime = dueWorldTime,
                state = state,
                reservationId = reservationId ?? string.Empty,
                payRunId = payRunId ?? string.Empty,
                paymentRecordIds = PayrollModelHelpers.CleanIds(paymentRecordIds),
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class PayrollRunData
    {
        public string payRunId;
        public string employerSubjectId;
        public string fundingAccountId;
        public string[] obligationIds = Array.Empty<string>();
        public PayrollPaymentPolicy paymentPolicy = PayrollPaymentPolicy.AllOrNothing;
        public PayrollRunState state = PayrollRunState.Draft;
        public double runWorldTime;
        public string reservationId;
        public long totalDueUnits;
        public long totalPaidUnits;
        public string currencyId;
        public string[] paymentRecordIds = Array.Empty<string>();
        public string[] statementIds = Array.Empty<string>();
        public long revision = 1L;

        public PayrollRunData Clone()
        {
            return new PayrollRunData
            {
                payRunId = payRunId ?? string.Empty,
                employerSubjectId = employerSubjectId ?? string.Empty,
                fundingAccountId = fundingAccountId ?? string.Empty,
                obligationIds = PayrollModelHelpers.CleanIds(obligationIds),
                paymentPolicy = paymentPolicy,
                state = state,
                runWorldTime = runWorldTime,
                reservationId = reservationId ?? string.Empty,
                totalDueUnits = Math.Max(0L, totalDueUnits),
                totalPaidUnits = Math.Max(0L, totalPaidUnits),
                currencyId = currencyId ?? string.Empty,
                paymentRecordIds = PayrollModelHelpers.CleanIds(paymentRecordIds),
                statementIds = PayrollModelHelpers.CleanIds(statementIds),
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class PayrollPaymentRecordData
    {
        public string paymentRecordId;
        public string payRunId;
        public string obligationId;
        public string economyTransactionId;
        public string fromAccountId;
        public string toAccountId;
        public string currencyId;
        public long units;
        public double paidWorldTime;
        public string kind;

        public PayrollPaymentRecordData Clone()
        {
            return new PayrollPaymentRecordData
            {
                paymentRecordId = paymentRecordId ?? string.Empty,
                payRunId = payRunId ?? string.Empty,
                obligationId = obligationId ?? string.Empty,
                economyTransactionId = economyTransactionId ?? string.Empty,
                fromAccountId = fromAccountId ?? string.Empty,
                toAccountId = toAccountId ?? string.Empty,
                currencyId = currencyId ?? string.Empty,
                units = Math.Max(0L, units),
                paidWorldTime = paidWorldTime,
                kind = kind ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class PayStatementData
    {
        public string statementId;
        public string payRunId;
        public string obligationId;
        public string calculationId;
        public string employeePersonId;
        public string employerSubjectId;
        public string currencyId;
        public long grossUnits;
        public long reimbursementUnits;
        public long deductionUnits;
        public long netUnits;
        public long paidUnits;
        public string[] deductionLineIds = Array.Empty<string>();
        public string accessPolicyId;
        public long revision = 1L;

        public PayStatementData Clone()
        {
            return new PayStatementData
            {
                statementId = statementId ?? string.Empty,
                payRunId = payRunId ?? string.Empty,
                obligationId = obligationId ?? string.Empty,
                calculationId = calculationId ?? string.Empty,
                employeePersonId = employeePersonId ?? string.Empty,
                employerSubjectId = employerSubjectId ?? string.Empty,
                currencyId = currencyId ?? string.Empty,
                grossUnits = Math.Max(0L, grossUnits),
                reimbursementUnits = Math.Max(0L, reimbursementUnits),
                deductionUnits = Math.Max(0L, deductionUnits),
                netUnits = Math.Max(0L, netUnits),
                paidUnits = Math.Max(0L, paidUnits),
                deductionLineIds = PayrollModelHelpers.CleanIds(deductionLineIds),
                accessPolicyId = accessPolicyId ?? string.Empty,
                revision = revision
            };
        }

        public InformationSubjectReferenceData CreateInformationSubject()
        {
            return PayrollInformationSubject.Create("payroll.statement", statementId, employeePersonId, employerSubjectId);
        }
    }

    [Serializable]
    public sealed class WageDebtData
    {
        public string wageDebtId;
        public string obligationId;
        public string employeePersonId;
        public string employerSubjectId;
        public string currencyId;
        public long outstandingUnits;
        public double createdWorldTime;
        public bool resolved;
        public long revision = 1L;

        public WageDebtData Clone()
        {
            return new WageDebtData
            {
                wageDebtId = wageDebtId ?? string.Empty,
                obligationId = obligationId ?? string.Empty,
                employeePersonId = employeePersonId ?? string.Empty,
                employerSubjectId = employerSubjectId ?? string.Empty,
                currencyId = currencyId ?? string.Empty,
                outstandingUnits = Math.Max(0L, outstandingUnits),
                createdWorldTime = createdWorldTime,
                resolved = resolved,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class PayrollCorrectionData
    {
        public string correctionId;
        public string correctedRecordId;
        public string replacementRecordId;
        public string reason;
        public string authorityId;
        public double worldTime;
        public long revision = 1L;

        public PayrollCorrectionData Clone()
        {
            return new PayrollCorrectionData
            {
                correctionId = correctionId ?? string.Empty,
                correctedRecordId = correctedRecordId ?? string.Empty,
                replacementRecordId = replacementRecordId ?? string.Empty,
                reason = reason ?? string.Empty,
                authorityId = authorityId ?? string.Empty,
                worldTime = worldTime,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class OverpaymentRecordData
    {
        public string overpaymentId;
        public string originalPaymentRecordId;
        public string employeePersonId;
        public string employerSubjectId;
        public string currencyId;
        public long overpaidUnits;
        public long recoveredUnits;
        public double createdWorldTime;
        public bool resolved;
        public long revision = 1L;

        public OverpaymentRecordData Clone()
        {
            return new OverpaymentRecordData
            {
                overpaymentId = overpaymentId ?? string.Empty,
                originalPaymentRecordId = originalPaymentRecordId ?? string.Empty,
                employeePersonId = employeePersonId ?? string.Empty,
                employerSubjectId = employerSubjectId ?? string.Empty,
                currencyId = currencyId ?? string.Empty,
                overpaidUnits = Math.Max(0L, overpaidUnits),
                recoveredUnits = Math.Max(0L, recoveredUnits),
                createdWorldTime = createdWorldTime,
                resolved = resolved,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class PayrollRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public long revision;
        public List<CompensationAgreementData> agreements = new List<CompensationAgreementData>();
        public List<WorkScheduleData> schedules = new List<WorkScheduleData>();
        public List<WorkSessionData> workSessions = new List<WorkSessionData>();
        public List<TimesheetData> timesheets = new List<TimesheetData>();
        public List<PayPeriodData> payPeriods = new List<PayPeriodData>();
        public List<CompensationAdjustmentData> adjustments = new List<CompensationAdjustmentData>();
        public List<PayrollCalculationData> calculations = new List<PayrollCalculationData>();
        public List<PayrollObligationData> obligations = new List<PayrollObligationData>();
        public List<PayrollRunData> payRuns = new List<PayrollRunData>();
        public List<PayrollPaymentRecordData> paymentRecords = new List<PayrollPaymentRecordData>();
        public List<PayStatementData> statements = new List<PayStatementData>();
        public List<WageDebtData> wageDebts = new List<WageDebtData>();
        public List<PayrollCorrectionData> corrections = new List<PayrollCorrectionData>();
        public List<OverpaymentRecordData> overpayments = new List<OverpaymentRecordData>();
        public string[] processedTransactionIds = Array.Empty<string>();

        public PayrollRuntimeSaveData Clone()
        {
            return new PayrollRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                revision = revision,
                agreements = agreements == null ? new List<CompensationAgreementData>() : agreements.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                schedules = schedules == null ? new List<WorkScheduleData>() : schedules.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                workSessions = workSessions == null ? new List<WorkSessionData>() : workSessions.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                timesheets = timesheets == null ? new List<TimesheetData>() : timesheets.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                payPeriods = payPeriods == null ? new List<PayPeriodData>() : payPeriods.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                adjustments = adjustments == null ? new List<CompensationAdjustmentData>() : adjustments.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                calculations = calculations == null ? new List<PayrollCalculationData>() : calculations.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                obligations = obligations == null ? new List<PayrollObligationData>() : obligations.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                payRuns = payRuns == null ? new List<PayrollRunData>() : payRuns.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                paymentRecords = paymentRecords == null ? new List<PayrollPaymentRecordData>() : paymentRecords.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                statements = statements == null ? new List<PayStatementData>() : statements.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                wageDebts = wageDebts == null ? new List<WageDebtData>() : wageDebts.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                corrections = corrections == null ? new List<PayrollCorrectionData>() : corrections.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                overpayments = overpayments == null ? new List<OverpaymentRecordData>() : overpayments.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                processedTransactionIds = PayrollModelHelpers.CleanIds(processedTransactionIds)
            };
        }
    }

    public sealed class PayrollOperationResult
    {
        private PayrollOperationResult(bool succeeded, PayrollOperationCode code, string message, long before, long after, bool preview, bool duplicate)
        {
            Succeeded = succeeded;
            Code = code;
            Message = string.IsNullOrWhiteSpace(message) ? code.ToString() : message;
            RevisionBefore = before;
            RevisionAfter = after;
            Preview = preview;
            Duplicate = duplicate;
        }

        public bool Succeeded { get; }
        public PayrollOperationCode Code { get; }
        public string Message { get; }
        public long RevisionBefore { get; }
        public long RevisionAfter { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public CompensationAgreementData Agreement { get; private set; }
        public WorkSessionData WorkSession { get; private set; }
        public TimesheetData Timesheet { get; private set; }
        public PayrollCalculationData Calculation { get; private set; }
        public PayrollObligationData Obligation { get; private set; }
        public PayrollRunData PayRun { get; private set; }
        public PayStatementData Statement { get; private set; }
        public WageDebtData WageDebt { get; private set; }

        public static PayrollOperationResult Success(string message, long before, long after, bool preview = false, bool duplicate = false)
        {
            return new PayrollOperationResult(true, preview ? PayrollOperationCode.Preview : duplicate ? PayrollOperationCode.Duplicate : PayrollOperationCode.Succeeded, message, before, after, preview, duplicate);
        }

        public static PayrollOperationResult Failure(PayrollOperationCode code, string message, long revision)
        {
            return new PayrollOperationResult(false, code, message, revision, revision, false, false);
        }

        public PayrollOperationResult With(
            CompensationAgreementData agreement = null,
            WorkSessionData workSession = null,
            TimesheetData timesheet = null,
            PayrollCalculationData calculation = null,
            PayrollObligationData obligation = null,
            PayrollRunData payRun = null,
            PayStatementData statement = null,
            WageDebtData wageDebt = null)
        {
            Agreement = agreement?.Clone();
            WorkSession = workSession?.Clone();
            Timesheet = timesheet?.Clone();
            Calculation = calculation?.Clone();
            Obligation = obligation?.Clone();
            PayRun = payRun?.Clone();
            Statement = statement?.Clone();
            WageDebt = wageDebt?.Clone();
            return this;
        }
    }

    public sealed class PayrollProjection<T>
    {
        public PayrollProjection(T record, PayrollProjectionAudience audience, InformationAccessDecision decision, bool redacted, bool concealed, string[] visibleFields, string[] redactedFields)
        {
            Record = record;
            Audience = audience;
            Decision = decision;
            Redacted = redacted;
            Concealed = concealed;
            VisibleFields = visibleFields ?? Array.Empty<string>();
            RedactedFields = redactedFields ?? Array.Empty<string>();
        }

        public T Record { get; }
        public PayrollProjectionAudience Audience { get; }
        public InformationAccessDecision Decision { get; }
        public bool Redacted { get; }
        public bool Concealed { get; }
        public string[] VisibleFields { get; }
        public string[] RedactedFields { get; }
    }

    public static class PayrollInformationSubject
    {
        public static readonly string[] ProtectedFields =
        {
            "detail.payroll.account",
            "detail.payroll.rate",
            "detail.payroll.gross",
            "detail.payroll.deductions",
            "detail.payroll.net",
            "detail.payroll.evidence"
        };

        public static InformationSubjectReferenceData Create(string kind, string recordId, string employeeId, string employerId)
        {
            return new InformationSubjectReferenceData
            {
                subjectType = InformationSubjectType.Custom,
                subjectId = recordId ?? string.Empty,
                parentSubjectId = kind ?? string.Empty,
                ownerPersonId = employeeId ?? string.Empty,
                controllingEntityId = employerId ?? string.Empty,
                tags = PayrollModelHelpers.CleanIds(new[] { "payroll", kind, employeeId, employerId })
            };
        }
    }
}
