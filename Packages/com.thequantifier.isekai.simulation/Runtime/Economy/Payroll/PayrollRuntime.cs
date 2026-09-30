using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Professions;

namespace UnityIsekaiGame.Economy.Payroll
{
    public sealed class PayrollRuntime
    {
        private readonly Dictionary<string, CompensationAgreementData> agreementsById = new Dictionary<string, CompensationAgreementData>(StringComparer.Ordinal);
        private readonly Dictionary<string, WorkScheduleData> schedulesById = new Dictionary<string, WorkScheduleData>(StringComparer.Ordinal);
        private readonly Dictionary<string, WorkSessionData> workSessionsById = new Dictionary<string, WorkSessionData>(StringComparer.Ordinal);
        private readonly Dictionary<string, TimesheetData> timesheetsById = new Dictionary<string, TimesheetData>(StringComparer.Ordinal);
        private readonly Dictionary<string, PayPeriodData> payPeriodsById = new Dictionary<string, PayPeriodData>(StringComparer.Ordinal);
        private readonly Dictionary<string, CompensationAdjustmentData> adjustmentsById = new Dictionary<string, CompensationAdjustmentData>(StringComparer.Ordinal);
        private readonly Dictionary<string, PayrollCalculationData> calculationsById = new Dictionary<string, PayrollCalculationData>(StringComparer.Ordinal);
        private readonly Dictionary<string, PayrollObligationData> obligationsById = new Dictionary<string, PayrollObligationData>(StringComparer.Ordinal);
        private readonly Dictionary<string, PayrollRunData> payRunsById = new Dictionary<string, PayrollRunData>(StringComparer.Ordinal);
        private readonly Dictionary<string, PayrollPaymentRecordData> paymentRecordsById = new Dictionary<string, PayrollPaymentRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, PayStatementData> statementsById = new Dictionary<string, PayStatementData>(StringComparer.Ordinal);
        private readonly Dictionary<string, WageDebtData> wageDebtsById = new Dictionary<string, WageDebtData>(StringComparer.Ordinal);
        private readonly Dictionary<string, PayrollCorrectionData> correctionsById = new Dictionary<string, PayrollCorrectionData>(StringComparer.Ordinal);
        private readonly Dictionary<string, OverpaymentRecordData> overpaymentsById = new Dictionary<string, OverpaymentRecordData>(StringComparer.Ordinal);
        private readonly HashSet<string> processedTransactions = new HashSet<string>(StringComparer.Ordinal);

        private DefinitionRegistry registry;
        private string worldId;

        public long Revision { get; private set; }
        public int AgreementCount => agreementsById.Count;
        public int ObligationCount => obligationsById.Count;
        public int StatementCount => statementsById.Count;

        public IReadOnlyList<CompensationAgreementData> Agreements => Ordered(agreementsById.Values, item => item.agreementId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<WorkSessionData> WorkSessions => Ordered(workSessionsById.Values, item => item.workSessionId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<TimesheetData> Timesheets => Ordered(timesheetsById.Values, item => item.timesheetId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<PayrollCalculationData> Calculations => Ordered(calculationsById.Values, item => item.calculationId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<PayrollObligationData> Obligations => Ordered(obligationsById.Values, item => item.obligationId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<PayrollPaymentRecordData> Payments => Ordered(paymentRecordsById.Values, item => item.paymentRecordId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<PayStatementData> Statements => Ordered(statementsById.Values, item => item.statementId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<WageDebtData> WageDebts => Ordered(wageDebtsById.Values, item => item.wageDebtId).Select(item => item.Clone()).ToArray();

        public void Configure(DefinitionRegistry definitionRegistry, string world)
        {
            registry = definitionRegistry ?? registry;
            worldId = world ?? worldId ?? string.Empty;
        }

        public bool TryGetAgreement(string agreementId, out CompensationAgreementData agreement)
        {
            if (!string.IsNullOrWhiteSpace(agreementId) && agreementsById.TryGetValue(agreementId, out CompensationAgreementData found))
            {
                agreement = found.Clone();
                return true;
            }

            agreement = null;
            return false;
        }

        public bool TryGetObligation(string obligationId, out PayrollObligationData obligation)
        {
            if (!string.IsNullOrWhiteSpace(obligationId) && obligationsById.TryGetValue(obligationId, out PayrollObligationData found))
            {
                obligation = found.Clone();
                return true;
            }

            obligation = null;
            return false;
        }

        public bool TryGetStatement(string statementId, out PayStatementData statement)
        {
            if (!string.IsNullOrWhiteSpace(statementId) && statementsById.TryGetValue(statementId, out PayStatementData found))
            {
                statement = found.Clone();
                return true;
            }

            statement = null;
            return false;
        }

        public PayrollOperationResult ActivateAgreement(CompensationAgreementData request, PositionEmploymentRuntime employmentRuntime, EconomyRuntime economy, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            CompensationAgreementData agreement = request?.Clone();
            PayrollOperationCode validation = ValidateAgreement(agreement, employmentRuntime, economy, out string failure);
            if (validation != PayrollOperationCode.Succeeded)
            {
                return PayrollOperationResult.Failure(validation, failure, before);
            }

            if (agreementsById.TryGetValue(agreement.agreementId, out CompensationAgreementData existing))
            {
                return SameAgreement(existing, agreement)
                    ? PayrollOperationResult.Success("Compensation agreement already exists.", before, before, duplicate: true).With(agreement: existing)
                    : PayrollOperationResult.Failure(PayrollOperationCode.Duplicate, $"Compensation agreement '{agreement.agreementId}' already exists with different data.", before);
            }

            if (agreementsById.Values.Any(item => item.state == CompensationAgreementState.Active
                && string.Equals(item.employmentId, agreement.employmentId, StringComparison.Ordinal)
                && RangesOverlap(item.effectiveStartWorldTime, item.effectiveEndWorldTime, agreement.effectiveStartWorldTime, agreement.effectiveEndWorldTime)))
            {
                return PayrollOperationResult.Failure(PayrollOperationCode.AgreementOverlap, $"Active compensation agreement overlaps employment '{agreement.employmentId}'.", before);
            }

            if (preview)
            {
                return PayrollOperationResult.Success("Compensation agreement preview succeeded.", before, before, preview: true).With(agreement: agreement);
            }

            agreementsById.Add(agreement.agreementId, agreement);
            Remember(transactionId);
            Revision++;
            return PayrollOperationResult.Success("Compensation agreement activated.", before, Revision).With(agreement: agreement);
        }

        public PayrollOperationResult CreateSchedule(WorkScheduleData request, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            WorkScheduleData schedule = request?.Clone();
            if (schedule == null || string.IsNullOrWhiteSpace(schedule.scheduleId) || string.IsNullOrWhiteSpace(schedule.agreementId))
            {
                return PayrollOperationResult.Failure(PayrollOperationCode.InvalidRequest, "Schedule ID and agreement ID are required.", before);
            }

            if (!agreementsById.ContainsKey(schedule.agreementId))
            {
                return PayrollOperationResult.Failure(PayrollOperationCode.InvalidRequest, $"Agreement '{schedule.agreementId}' is missing.", before);
            }

            if (schedule.endWorldTime >= 0d && schedule.endWorldTime < schedule.startWorldTime)
            {
                return PayrollOperationResult.Failure(PayrollOperationCode.InvalidRequest, "Schedule end cannot be before start.", before);
            }

            if (schedulesById.ContainsKey(schedule.scheduleId))
            {
                return PayrollOperationResult.Success("Work schedule already exists.", before, before, duplicate: true);
            }

            if (preview)
            {
                return PayrollOperationResult.Success("Work schedule preview succeeded.", before, before, preview: true);
            }

            schedulesById.Add(schedule.scheduleId, schedule);
            Remember(transactionId);
            Revision++;
            return PayrollOperationResult.Success("Work schedule created.", before, Revision);
        }

        public PayrollOperationResult RecordWorkSession(WorkSessionData request, PositionEmploymentRuntime employmentRuntime, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            WorkSessionData session = request?.Clone();
            if (session == null || string.IsNullOrWhiteSpace(session.workSessionId) || string.IsNullOrWhiteSpace(session.agreementId))
            {
                return PayrollOperationResult.Failure(PayrollOperationCode.InvalidRequest, "Work session ID and agreement ID are required.", before);
            }

            if (workSessionsById.TryGetValue(session.workSessionId, out WorkSessionData existing))
            {
                return SameSession(existing, session)
                    ? PayrollOperationResult.Success("Work session already exists.", before, before, duplicate: true).With(workSession: existing)
                    : PayrollOperationResult.Failure(PayrollOperationCode.Duplicate, $"Work session '{session.workSessionId}' already exists with different data.", before);
            }

            if (!agreementsById.TryGetValue(session.agreementId, out CompensationAgreementData agreement))
            {
                return PayrollOperationResult.Failure(PayrollOperationCode.InvalidRequest, $"Agreement '{session.agreementId}' is missing.", before);
            }

            if (!employmentRuntime.TryGetEmployment(agreement.employmentId, out EmploymentRecordData employment) || !string.Equals(employment.personId, agreement.employeePersonId, StringComparison.Ordinal))
            {
                return PayrollOperationResult.Failure(PayrollOperationCode.MissingEmployment, $"Employment '{agreement.employmentId}' is not active for payroll evidence.", before);
            }

            if (session.endWorldTime <= session.startWorldTime)
            {
                return PayrollOperationResult.Failure(PayrollOperationCode.InvalidRequest, "Work session end must be after start.", before);
            }

            if (session.durationMinutes <= 0L)
            {
                session.durationMinutes = Math.Max(0L, RoundLong((session.endWorldTime - session.startWorldTime) / 60d));
            }

            if (session.durationMinutes <= 0L && session.creditedOutputQuantity <= 0L)
            {
                return PayrollOperationResult.Failure(PayrollOperationCode.InvalidRequest, "Work session must credit duration or output.", before);
            }

            HashSet<string> evidence = new HashSet<string>(session.evidenceIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            if (evidence.Count > 0 && workSessionsById.Values.Any(item => (item.evidenceIds ?? Array.Empty<string>()).Any(id => evidence.Contains(id))))
            {
                return PayrollOperationResult.Failure(PayrollOperationCode.InvalidRequest, "Work evidence already backs another payroll work session.", before);
            }

            session.employmentId = agreement.employmentId;
            session.employeePersonId = agreement.employeePersonId;
            if (preview)
            {
                return PayrollOperationResult.Success("Work session preview succeeded.", before, before, preview: true).With(workSession: session);
            }

            workSessionsById.Add(session.workSessionId, session);
            Remember(transactionId);
            Revision++;
            return PayrollOperationResult.Success("Work session recorded.", before, Revision).With(workSession: session);
        }

        public PayrollOperationResult SubmitTimesheet(TimesheetData request, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            TimesheetData timesheet = request?.Clone();
            if (timesheet == null || string.IsNullOrWhiteSpace(timesheet.timesheetId) || string.IsNullOrWhiteSpace(timesheet.agreementId))
            {
                return PayrollOperationResult.Failure(PayrollOperationCode.InvalidRequest, "Timesheet ID and agreement ID are required.", before);
            }

            if (timesheetsById.ContainsKey(timesheet.timesheetId))
            {
                return PayrollOperationResult.Success("Timesheet already exists.", before, before, duplicate: true).With(timesheet: timesheetsById[timesheet.timesheetId]);
            }

            if (!agreementsById.TryGetValue(timesheet.agreementId, out CompensationAgreementData agreement))
            {
                return PayrollOperationResult.Failure(PayrollOperationCode.InvalidRequest, $"Agreement '{timesheet.agreementId}' is missing.", before);
            }

            if (timesheet.workSessionIds == null || timesheet.workSessionIds.Length == 0)
            {
                return PayrollOperationResult.Failure(PayrollOperationCode.InvalidRequest, "Timesheet must reference at least one work session.", before);
            }

            foreach (string sessionId in timesheet.workSessionIds)
            {
                if (!workSessionsById.TryGetValue(sessionId, out WorkSessionData session) || !string.Equals(session.agreementId, timesheet.agreementId, StringComparison.Ordinal))
                {
                    return PayrollOperationResult.Failure(PayrollOperationCode.InvalidRequest, $"Timesheet references invalid work session '{sessionId}'.", before);
                }
            }

            timesheet.employmentId = agreement.employmentId;
            timesheet.state = timesheet.state == TimesheetState.Approved ? TimesheetState.Approved : TimesheetState.Submitted;
            if (preview)
            {
                return PayrollOperationResult.Success("Timesheet preview succeeded.", before, before, preview: true).With(timesheet: timesheet);
            }

            timesheetsById.Add(timesheet.timesheetId, timesheet);
            Remember(transactionId);
            Revision++;
            return PayrollOperationResult.Success("Timesheet submitted.", before, Revision).With(timesheet: timesheet);
        }

        public PayrollOperationResult ApproveTimesheet(string timesheetId, string authorityId, double worldTime, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            if (!timesheetsById.TryGetValue(timesheetId ?? string.Empty, out TimesheetData timesheet))
            {
                return PayrollOperationResult.Failure(PayrollOperationCode.InvalidRequest, $"Timesheet '{timesheetId}' is missing.", before);
            }

            if (timesheet.state == TimesheetState.Approved)
            {
                return PayrollOperationResult.Success("Timesheet is already approved.", before, before, duplicate: true).With(timesheet: timesheet);
            }

            if (timesheet.state != TimesheetState.Submitted)
            {
                return PayrollOperationResult.Failure(PayrollOperationCode.InvalidState, "Only submitted timesheets can be approved.", before);
            }

            TimesheetData updated = timesheet.Clone();
            updated.state = TimesheetState.Approved;
            updated.approvedByAuthorityId = authorityId ?? string.Empty;
            updated.approvedWorldTime = worldTime;
            updated.revision++;
            if (preview)
            {
                return PayrollOperationResult.Success("Timesheet approval preview succeeded.", before, before, preview: true).With(timesheet: updated);
            }

            timesheetsById[timesheetId] = updated;
            Remember(transactionId);
            Revision++;
            return PayrollOperationResult.Success("Timesheet approved.", before, Revision).With(timesheet: updated);
        }

        public PayrollOperationResult CreatePayPeriod(PayPeriodData request, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            PayPeriodData period = request?.Clone();
            if (period == null || string.IsNullOrWhiteSpace(period.payPeriodId) || string.IsNullOrWhiteSpace(period.agreementId))
            {
                return PayrollOperationResult.Failure(PayrollOperationCode.InvalidRequest, "Pay period ID and agreement ID are required.", before);
            }

            if (!agreementsById.ContainsKey(period.agreementId))
            {
                return PayrollOperationResult.Failure(PayrollOperationCode.InvalidRequest, $"Agreement '{period.agreementId}' is missing.", before);
            }

            if (period.endWorldTime <= period.startWorldTime)
            {
                return PayrollOperationResult.Failure(PayrollOperationCode.InvalidRequest, "Pay period end must be after start.", before);
            }

            if (payPeriodsById.ContainsKey(period.payPeriodId))
            {
                return PayrollOperationResult.Success("Pay period already exists.", before, before, duplicate: true);
            }

            if (preview)
            {
                return PayrollOperationResult.Success("Pay period preview succeeded.", before, before, preview: true);
            }

            payPeriodsById.Add(period.payPeriodId, period);
            Remember(transactionId);
            Revision++;
            return PayrollOperationResult.Success("Pay period created.", before, Revision);
        }

        public PayrollOperationResult RecordAdjustment(CompensationAdjustmentData request, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            CompensationAdjustmentData adjustment = request?.Clone();
            if (adjustment == null || string.IsNullOrWhiteSpace(adjustment.adjustmentId) || string.IsNullOrWhiteSpace(adjustment.agreementId))
            {
                return PayrollOperationResult.Failure(PayrollOperationCode.InvalidRequest, "Adjustment ID and agreement ID are required.", before);
            }

            if (!agreementsById.TryGetValue(adjustment.agreementId, out CompensationAgreementData agreement))
            {
                return PayrollOperationResult.Failure(PayrollOperationCode.InvalidRequest, $"Agreement '{adjustment.agreementId}' is missing.", before);
            }

            if (adjustment.units == 0L)
            {
                return PayrollOperationResult.Failure(PayrollOperationCode.InvalidRequest, "Adjustment units cannot be zero.", before);
            }

            adjustment.currencyId = string.IsNullOrWhiteSpace(adjustment.currencyId) ? GetCompensation(agreement.compensationDefinitionId)?.CurrencyId ?? string.Empty : adjustment.currencyId;
            if (adjustmentsById.ContainsKey(adjustment.adjustmentId))
            {
                return PayrollOperationResult.Success("Adjustment already exists.", before, before, duplicate: true);
            }

            if (preview)
            {
                return PayrollOperationResult.Success("Adjustment preview succeeded.", before, before, preview: true);
            }

            adjustmentsById.Add(adjustment.adjustmentId, adjustment);
            Remember(transactionId);
            Revision++;
            return PayrollOperationResult.Success("Adjustment recorded.", before, Revision);
        }

        public PayrollOperationResult CalculatePay(string calculationId, string payPeriodId, IEnumerable<string> workSessionIds, IEnumerable<string> adjustmentIds, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            if (string.IsNullOrWhiteSpace(calculationId) || string.IsNullOrWhiteSpace(payPeriodId))
            {
                return PayrollOperationResult.Failure(PayrollOperationCode.InvalidRequest, "Calculation ID and pay period ID are required.", before);
            }

            if (calculationsById.TryGetValue(calculationId, out PayrollCalculationData existing))
            {
                return PayrollOperationResult.Success("Payroll calculation already exists.", before, before, duplicate: true).With(calculation: existing);
            }

            if (!payPeriodsById.TryGetValue(payPeriodId, out PayPeriodData period) || !agreementsById.TryGetValue(period.agreementId, out CompensationAgreementData agreement))
            {
                return PayrollOperationResult.Failure(PayrollOperationCode.InvalidRequest, "Pay period or agreement is missing.", before);
            }

            CompensationDefinition compensation = GetCompensation(agreement.compensationDefinitionId);
            if (compensation == null)
            {
                return PayrollOperationResult.Failure(PayrollOperationCode.MissingDefinition, $"Compensation definition '{agreement.compensationDefinitionId}' is missing.", before);
            }

            List<WorkSessionData> sessions = PayrollModelHelpers.CleanIds(workSessionIds)
                .Select(id => workSessionsById.TryGetValue(id, out WorkSessionData session) ? session : null)
                .Where(session => session != null && string.Equals(session.agreementId, agreement.agreementId, StringComparison.Ordinal))
                .OrderBy(session => session.startWorldTime)
                .ThenBy(session => session.workSessionId, StringComparer.Ordinal)
                .Select(session => session.Clone())
                .ToList();

            List<CompensationAdjustmentData> adjustments = PayrollModelHelpers.CleanIds(adjustmentIds)
                .Select(id => adjustmentsById.TryGetValue(id, out CompensationAdjustmentData adjustment) ? adjustment : null)
                .Where(adjustment => adjustment != null && string.Equals(adjustment.agreementId, agreement.agreementId, StringComparison.Ordinal))
                .OrderBy(adjustment => adjustment.adjustmentId, StringComparer.Ordinal)
                .Select(adjustment => adjustment.Clone())
                .ToList();

            long minutes = sessions.Sum(session => session.durationMinutes);
            long output = sessions.Sum(session => session.creditedOutputQuantity);
            long gross = CalculateRegularGross(compensation, period, agreement, sessions.Count, minutes, output);
            long adjustmentGross = adjustments.Where(item => item.category != CompensationAdjustmentCategory.Reimbursement).Sum(item => item.units);
            long reimbursements = adjustments.Where(item => item.category == CompensationAdjustmentCategory.Reimbursement).Sum(item => Math.Max(0L, item.units));
            if (gross < 0L || gross + adjustmentGross < 0L)
            {
                return PayrollOperationResult.Failure(PayrollOperationCode.CalculationRejected, "Payroll gross calculation became negative.", before);
            }

            PayrollCalculationData calculation = new PayrollCalculationData
            {
                calculationId = calculationId,
                agreementId = agreement.agreementId,
                payPeriodId = period.payPeriodId,
                compensationDefinitionId = compensation.Id,
                currencyId = compensation.CurrencyId,
                regularGrossUnits = gross,
                adjustmentGrossUnits = adjustmentGross,
                reimbursementUnits = reimbursements,
                minutesCredited = minutes,
                outputCredited = output,
                workSessionIds = sessions.Select(session => session.workSessionId).ToArray(),
                adjustmentIds = adjustments.Select(adjustment => adjustment.adjustmentId).ToArray(),
                preview = preview
            };

            PayrollOperationResult net = ApplyDeductions(calculation, agreement, preview);
            if (!net.Succeeded)
            {
                return net;
            }

            calculation = net.Calculation;
            if (preview)
            {
                return PayrollOperationResult.Success("Payroll calculation preview succeeded.", before, before, preview: true).With(calculation: calculation);
            }

            calculationsById.Add(calculation.calculationId, calculation);
            PayPeriodData updatedPeriod = period.Clone();
            updatedPeriod.state = PayPeriodState.Calculated;
            updatedPeriod.calculationId = calculation.calculationId;
            updatedPeriod.revision++;
            payPeriodsById[updatedPeriod.payPeriodId] = updatedPeriod;
            Remember(transactionId);
            Revision++;
            return PayrollOperationResult.Success("Payroll calculated.", before, Revision).With(calculation: calculation);
        }

        public PayrollOperationResult CreateObligation(string obligationId, string calculationId, double dueWorldTime, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            if (string.IsNullOrWhiteSpace(obligationId) || string.IsNullOrWhiteSpace(calculationId))
            {
                return PayrollOperationResult.Failure(PayrollOperationCode.InvalidRequest, "Obligation ID and calculation ID are required.", before);
            }

            if (obligationsById.TryGetValue(obligationId, out PayrollObligationData existing))
            {
                return PayrollOperationResult.Success("Payroll obligation already exists.", before, before, duplicate: true).With(obligation: existing);
            }

            if (!calculationsById.TryGetValue(calculationId, out PayrollCalculationData calculation)
                || !agreementsById.TryGetValue(calculation.agreementId, out CompensationAgreementData agreement))
            {
                return PayrollOperationResult.Failure(PayrollOperationCode.InvalidRequest, "Calculation or agreement is missing.", before);
            }

            long due = checked(calculation.netPayUnits + calculation.deductionUnits);
            PayrollObligationData obligation = new PayrollObligationData
            {
                obligationId = obligationId,
                calculationId = calculation.calculationId,
                agreementId = agreement.agreementId,
                employeePersonId = agreement.employeePersonId,
                employerSubjectId = agreement.employerSubjectId,
                employerFundingAccountId = agreement.employerFundingAccountId,
                employeeAccountId = agreement.employeeAccountId,
                currencyId = calculation.currencyId,
                amountDueUnits = due,
                amountOutstandingUnits = due,
                dueWorldTime = dueWorldTime,
                state = PayrollObligationState.Pending
            };

            if (preview)
            {
                return PayrollOperationResult.Success("Payroll obligation preview succeeded.", before, before, preview: true).With(obligation: obligation);
            }

            obligationsById.Add(obligation.obligationId, obligation);
            if (payPeriodsById.TryGetValue(calculation.payPeriodId, out PayPeriodData period))
            {
                PayPeriodData updated = period.Clone();
                updated.state = PayPeriodState.Obligated;
                updated.obligationId = obligation.obligationId;
                updated.revision++;
                payPeriodsById[updated.payPeriodId] = updated;
            }

            Remember(transactionId);
            Revision++;
            return PayrollOperationResult.Success("Payroll obligation created.", before, Revision).With(obligation: obligation);
        }

        public PayrollOperationResult CreatePayrollRun(string payRunId, string employerSubjectId, string fundingAccountId, IEnumerable<string> obligationIds, PayrollPaymentPolicy policy, double runWorldTime, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            string[] ids = PayrollModelHelpers.CleanIds(obligationIds);
            if (string.IsNullOrWhiteSpace(payRunId) || string.IsNullOrWhiteSpace(employerSubjectId) || string.IsNullOrWhiteSpace(fundingAccountId) || ids.Length == 0)
            {
                return PayrollOperationResult.Failure(PayrollOperationCode.InvalidRequest, "Payroll run ID, employer, funding account, and obligations are required.", before);
            }

            if (payRunsById.TryGetValue(payRunId, out PayrollRunData existing))
            {
                return PayrollOperationResult.Success("Payroll run already exists.", before, before, duplicate: true).With(payRun: existing);
            }

            List<PayrollObligationData> obligations = new List<PayrollObligationData>();
            foreach (string id in ids)
            {
                if (!obligationsById.TryGetValue(id, out PayrollObligationData obligation))
                {
                    return PayrollOperationResult.Failure(PayrollOperationCode.InvalidRequest, $"Obligation '{id}' is missing.", before);
                }

                if (!string.Equals(obligation.employerSubjectId, employerSubjectId, StringComparison.Ordinal) || !string.Equals(obligation.employerFundingAccountId, fundingAccountId, StringComparison.Ordinal))
                {
                    return PayrollOperationResult.Failure(PayrollOperationCode.InvalidRequest, $"Obligation '{id}' does not belong to this employer funding account.", before);
                }

                if (obligation.state != PayrollObligationState.Pending && obligation.state != PayrollObligationState.PartiallyPaid && obligation.state != PayrollObligationState.DebtOutstanding)
                {
                    return PayrollOperationResult.Failure(PayrollOperationCode.InvalidState, $"Obligation '{id}' is not payable.", before);
                }

                obligations.Add(obligation);
            }

            string currencyId = obligations[0].currencyId;
            if (obligations.Any(item => !string.Equals(item.currencyId, currencyId, StringComparison.Ordinal)))
            {
                return PayrollOperationResult.Failure(PayrollOperationCode.CurrencyMismatch, "Payroll run cannot mix currencies.", before);
            }

            PayrollRunData run = new PayrollRunData
            {
                payRunId = payRunId,
                employerSubjectId = employerSubjectId,
                fundingAccountId = fundingAccountId,
                obligationIds = ids,
                paymentPolicy = policy,
                state = PayrollRunState.Draft,
                runWorldTime = runWorldTime,
                totalDueUnits = obligations.Sum(item => item.amountOutstandingUnits),
                currencyId = currencyId
            };

            if (preview)
            {
                return PayrollOperationResult.Success("Payroll run preview succeeded.", before, before, preview: true).With(payRun: run);
            }

            payRunsById.Add(run.payRunId, run);
            Remember(transactionId);
            Revision++;
            return PayrollOperationResult.Success("Payroll run created.", before, Revision).With(payRun: run);
        }

        public PayrollOperationResult ReservePayrollFunds(string payRunId, EconomyRuntime economy, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            if (!payRunsById.TryGetValue(payRunId ?? string.Empty, out PayrollRunData run))
            {
                return PayrollOperationResult.Failure(PayrollOperationCode.InvalidRequest, $"Payroll run '{payRunId}' is missing.", before);
            }

            if (run.totalDueUnits <= 0L)
            {
                return PayrollOperationResult.Failure(PayrollOperationCode.InvalidRequest, "Payroll run total must be positive.", before);
            }

            if (economy == null || !economy.TryGetAccount(run.fundingAccountId, out EconomyAccountSnapshot fundingAccount))
            {
                return PayrollOperationResult.Failure(PayrollOperationCode.MissingAccount, $"Payroll funding account '{run.fundingAccountId}' is missing.", before);
            }

            if (!string.Equals(fundingAccount.CurrencyId, run.currencyId, StringComparison.Ordinal))
            {
                return PayrollOperationResult.Failure(PayrollOperationCode.CurrencyMismatch, "Payroll funding account currency does not match the payroll run.", before);
            }

            long reservationUnits = run.paymentPolicy == PayrollPaymentPolicy.PartialWithDebt
                ? Math.Min(run.totalDueUnits, fundingAccount.AvailableUnits)
                : run.totalDueUnits;
            if (reservationUnits <= 0L)
            {
                return PayrollOperationResult.Failure(PayrollOperationCode.InsufficientFunds, "Payroll funding account has no available funds.", before);
            }

            string reservationId = string.IsNullOrWhiteSpace(run.reservationId) ? $"{run.payRunId}.reservation" : run.reservationId;
            EconomyOperationResult reservation = economy.Reserve(reservationId, run.fundingAccountId, new MoneyAmount(run.currencyId, reservationUnits), run.payRunId, run.runWorldTime, preview: preview);
            if (!reservation.Succeeded)
            {
                return PayrollOperationResult.Failure(PayrollOperationCode.InsufficientFunds, reservation.Message, before);
            }

            PayrollRunData updated = run.Clone();
            updated.reservationId = reservationId;
            updated.state = PayrollRunState.FundsReserved;
            updated.revision++;
            if (preview)
            {
                return PayrollOperationResult.Success("Payroll fund reservation preview succeeded.", before, before, preview: true).With(payRun: updated);
            }

            payRunsById[updated.payRunId] = updated;
            foreach (string obligationId in updated.obligationIds)
            {
                PayrollObligationData obligation = obligationsById[obligationId].Clone();
                obligation.reservationId = reservationId;
                obligation.payRunId = updated.payRunId;
                obligation.state = PayrollObligationState.Reserved;
                obligation.revision++;
                obligationsById[obligation.obligationId] = obligation;
            }

            Remember(transactionId);
            Revision++;
            return PayrollOperationResult.Success("Payroll funds reserved.", before, Revision).With(payRun: updated);
        }

        public PayrollOperationResult ExecutePayrollRun(string payRunId, EconomyRuntime economy, string transactionId = "", string injectFailureStage = "", bool preview = false)
        {
            long before = Revision;
            if (!payRunsById.TryGetValue(payRunId ?? string.Empty, out PayrollRunData run))
            {
                return PayrollOperationResult.Failure(PayrollOperationCode.InvalidRequest, $"Payroll run '{payRunId}' is missing.", before);
            }

            if (run.state == PayrollRunState.Executed)
            {
                return PayrollOperationResult.Success("Payroll run already executed.", before, before, duplicate: true).With(payRun: run);
            }

            if (economy == null)
            {
                return PayrollOperationResult.Failure(PayrollOperationCode.MissingAccount, "Economy runtime is missing.", before);
            }

            PayrollRuntimeSaveData payrollRollback = CreateSaveData();
            EconomyRuntimeSaveData economyRollback = economy.CreateSaveData();
            try
            {
                if (FailAt(injectFailureStage, "before-reservation")) throw new InvalidOperationException("Injected payroll failure before reservation.");
                if (string.IsNullOrWhiteSpace(run.reservationId))
                {
                    PayrollOperationResult reserve = ReservePayrollFunds(payRunId, economy, transactionId + ".reserve", preview);
                    if (!reserve.Succeeded)
                    {
                        return reserve;
                    }

                    run = payRunsById[payRunId];
                }

                if (preview)
                {
                    return PayrollOperationResult.Success("Payroll execution preview succeeded.", before, before, preview: true).With(payRun: run);
                }

                if (FailAt(injectFailureStage, "after-reservation")) throw new InvalidOperationException("Injected payroll failure after reservation.");
                List<string> paymentIds = new List<string>();
                List<string> statementIds = new List<string>();
                long paidTotal = 0L;
                foreach (string obligationId in run.obligationIds.OrderBy(id => obligationsById[id].dueWorldTime).ThenBy(id => id, StringComparer.Ordinal))
                {
                    PayrollObligationData obligation = obligationsById[obligationId].Clone();
                    PayrollCalculationData calculation = calculationsById[obligation.calculationId].Clone();
                    if (!economy.TryGetAccount(obligation.employerFundingAccountId, out EconomyAccountSnapshot from)
                        || !economy.TryGetAccount(obligation.employeeAccountId, out EconomyAccountSnapshot to))
                    {
                        throw new InvalidOperationException("Payroll account validation failed.");
                    }

                    if (!string.Equals(from.CurrencyId, obligation.currencyId, StringComparison.Ordinal) || !string.Equals(to.CurrencyId, obligation.currencyId, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Payroll account currency mismatch.");
                    }

                    long payable = run.paymentPolicy == PayrollPaymentPolicy.PartialWithDebt
                        ? Math.Min(obligation.amountOutstandingUnits, from.AvailableUnits + ReservationAvailable(run.reservationId, economy))
                        : obligation.amountOutstandingUnits;
                    if (payable <= 0L)
                    {
                        throw new InvalidOperationException("Payroll run has no payable funds.");
                    }

                    if (run.paymentPolicy == PayrollPaymentPolicy.AllOrNothing && payable < obligation.amountOutstandingUnits)
                    {
                        throw new InvalidOperationException("Payroll run requires all-or-nothing funding.");
                    }

                    long employeePay = Math.Min(calculation.netPayUnits, payable);
                    if (FailAt(injectFailureStage, "before-employee-transfer")) throw new InvalidOperationException("Injected payroll failure before employee transfer.");
                    if (employeePay > 0L)
                    {
                        string tx = $"{transactionId}.{obligation.obligationId}.net";
                        EconomyOperationResult pay = economy.Transfer(tx, obligation.employerFundingAccountId, obligation.employeeAccountId, new MoneyAmount(obligation.currencyId, employeePay), EconomyTransactionKind.Payment, reservationId: run.reservationId, actorId: run.employerSubjectId, worldTime: run.runWorldTime);
                        if (!pay.Succeeded)
                        {
                            throw new InvalidOperationException(pay.Message);
                        }

                        string paymentId = $"{run.payRunId}.{obligation.obligationId}.net";
                        paymentRecordsById[paymentId] = Payment(paymentId, run.payRunId, obligation.obligationId, pay.Transaction?.TransactionId, obligation.employerFundingAccountId, obligation.employeeAccountId, obligation.currencyId, employeePay, run.runWorldTime, "net");
                        paymentIds.Add(paymentId);
                    }

                    long remainingForDeductions = Math.Max(0L, payable - employeePay);
                    foreach (PayrollDeductionLineData deduction in calculation.deductions.OrderBy(item => item.priority).ThenBy(item => item.deductionDefinitionId, StringComparer.Ordinal))
                    {
                        if (remainingForDeductions <= 0L || string.IsNullOrWhiteSpace(deduction.recipientAccountId) || deduction.units <= 0L)
                        {
                            continue;
                        }

                        long deductionPay = Math.Min(deduction.units, remainingForDeductions);
                        if (FailAt(injectFailureStage, "before-deduction-transfer")) throw new InvalidOperationException("Injected payroll failure before deduction transfer.");
                        EconomyOperationResult pay = economy.Transfer($"{transactionId}.{obligation.obligationId}.{deduction.deductionDefinitionId}", obligation.employerFundingAccountId, deduction.recipientAccountId, new MoneyAmount(obligation.currencyId, deductionPay), EconomyTransactionKind.Payment, reservationId: string.Empty, actorId: run.employerSubjectId, worldTime: run.runWorldTime);
                        if (!pay.Succeeded)
                        {
                            throw new InvalidOperationException(pay.Message);
                        }

                        string paymentId = $"{run.payRunId}.{obligation.obligationId}.{deduction.deductionDefinitionId}";
                        paymentRecordsById[paymentId] = Payment(paymentId, run.payRunId, obligation.obligationId, pay.Transaction?.TransactionId, obligation.employerFundingAccountId, deduction.recipientAccountId, obligation.currencyId, deductionPay, run.runWorldTime, "deduction");
                        paymentIds.Add(paymentId);
                        remainingForDeductions -= deductionPay;
                    }

                    long paid = payable - remainingForDeductions;
                    paidTotal += paid;
                    obligation.amountPaidUnits = checked(obligation.amountPaidUnits + paid);
                    obligation.amountOutstandingUnits = Math.Max(0L, obligation.amountDueUnits - obligation.amountPaidUnits);
                    obligation.state = obligation.amountOutstandingUnits == 0L ? PayrollObligationState.Paid : PayrollObligationState.DebtOutstanding;
                    obligation.paymentRecordIds = PayrollModelHelpers.CleanIds((obligation.paymentRecordIds ?? Array.Empty<string>()).Concat(paymentIds));
                    obligation.revision++;
                    obligationsById[obligation.obligationId] = obligation;

                    PayStatementData statement = Statement($"{run.payRunId}.{obligation.obligationId}.statement", run, obligation, calculation, paid);
                    statementsById[statement.statementId] = statement;
                    statementIds.Add(statement.statementId);
                    if (obligation.amountOutstandingUnits > 0L)
                    {
                        string debtId = $"{obligation.obligationId}.debt";
                        wageDebtsById[debtId] = new WageDebtData
                        {
                            wageDebtId = debtId,
                            obligationId = obligation.obligationId,
                            employeePersonId = obligation.employeePersonId,
                            employerSubjectId = obligation.employerSubjectId,
                            currencyId = obligation.currencyId,
                            outstandingUnits = obligation.amountOutstandingUnits,
                            createdWorldTime = run.runWorldTime
                        };
                    }
                }

                if (FailAt(injectFailureStage, "before-run-commit")) throw new InvalidOperationException("Injected payroll failure before run commit.");
                PayrollRunData updatedRun = run.Clone();
                updatedRun.paymentRecordIds = PayrollModelHelpers.CleanIds(paymentIds);
                updatedRun.statementIds = PayrollModelHelpers.CleanIds(statementIds);
                updatedRun.totalPaidUnits = paidTotal;
                updatedRun.state = paidTotal >= updatedRun.totalDueUnits ? PayrollRunState.Executed : PayrollRunState.PartiallyExecuted;
                updatedRun.revision++;
                payRunsById[updatedRun.payRunId] = updatedRun;
                Remember(transactionId);
                Revision++;
                return PayrollOperationResult.Success("Payroll run executed.", before, Revision).With(payRun: updatedRun);
            }
            catch (Exception ex)
            {
                RestoreRuntimeState(payrollRollback);
                economy.RestoreFromSaveData(economyRollback, registry);
                PayrollRunData rolledBack = run.Clone();
                rolledBack.state = PayrollRunState.FailedRolledBack;
                return PayrollOperationResult.Failure(PayrollOperationCode.RolledBack, $"Payroll run rolled back: {ex.Message}", before).With(payRun: rolledBack);
            }
        }

        public PayrollOperationResult RecordCorrection(PayrollCorrectionData request, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            PayrollCorrectionData correction = request?.Clone();
            if (correction == null || string.IsNullOrWhiteSpace(correction.correctionId) || string.IsNullOrWhiteSpace(correction.correctedRecordId))
            {
                return PayrollOperationResult.Failure(PayrollOperationCode.InvalidRequest, "Correction ID and corrected record ID are required.", before);
            }

            if (correctionsById.ContainsKey(correction.correctionId))
            {
                return PayrollOperationResult.Success("Payroll correction already exists.", before, before, duplicate: true);
            }

            if (preview)
            {
                return PayrollOperationResult.Success("Payroll correction preview succeeded.", before, before, preview: true);
            }

            correctionsById.Add(correction.correctionId, correction);
            Remember(transactionId);
            Revision++;
            return PayrollOperationResult.Success("Payroll correction recorded.", before, Revision);
        }

        public PayrollOperationResult RecordOverpayment(OverpaymentRecordData request, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            OverpaymentRecordData overpayment = request?.Clone();
            if (overpayment == null || string.IsNullOrWhiteSpace(overpayment.overpaymentId) || overpayment.overpaidUnits <= 0L)
            {
                return PayrollOperationResult.Failure(PayrollOperationCode.InvalidRequest, "Overpayment ID and positive amount are required.", before);
            }

            if (overpaymentsById.ContainsKey(overpayment.overpaymentId))
            {
                return PayrollOperationResult.Success("Overpayment already exists.", before, before, duplicate: true);
            }

            if (preview)
            {
                return PayrollOperationResult.Success("Overpayment preview succeeded.", before, before, preview: true);
            }

            overpaymentsById.Add(overpayment.overpaymentId, overpayment);
            Remember(transactionId);
            Revision++;
            return PayrollOperationResult.Success("Overpayment recorded.", before, Revision);
        }

        public PayrollProjection<PayStatementData> ProjectPayStatement(string statementId, PayrollProjectionAudience audience, InformationAccessDecision decision)
        {
            if (!statementsById.TryGetValue(statementId ?? string.Empty, out PayStatementData statement))
            {
                return new PayrollProjection<PayStatementData>(null, audience, decision, false, true, Array.Empty<string>(), PayrollInformationSubject.ProtectedFields);
            }

            bool privileged = audience == PayrollProjectionAudience.Employee || audience == PayrollProjectionAudience.Employer || audience == PayrollProjectionAudience.PayrollAuthority || audience == PayrollProjectionAudience.PrivilegedDebug;
            bool denied = decision != null && decision.Denied && !privileged;
            if (denied)
            {
                return new PayrollProjection<PayStatementData>(null, audience, decision, false, true, Array.Empty<string>(), PayrollInformationSubject.ProtectedFields);
            }

            bool redacted = !privileged && (decision == null || !decision.FullAccess);
            PayStatementData projected = statement.Clone();
            if (redacted)
            {
                projected.grossUnits = 0L;
                projected.reimbursementUnits = 0L;
                projected.deductionUnits = 0L;
                projected.netUnits = 0L;
                projected.paidUnits = 0L;
                projected.deductionLineIds = Array.Empty<string>();
            }

            return new PayrollProjection<PayStatementData>(projected, audience, decision, redacted, false, redacted ? new[] { "employee", "employer", "period" } : new[] { "all" }, redacted ? PayrollInformationSubject.ProtectedFields : Array.Empty<string>());
        }

        public PayrollRuntimeSaveData CreateSaveData()
        {
            return new PayrollRuntimeSaveData
            {
                schemaVersion = PayrollRuntimeSaveData.CurrentSchemaVersion,
                revision = Revision,
                agreements = Ordered(agreementsById.Values, item => item.agreementId).Select(item => item.Clone()).ToList(),
                schedules = Ordered(schedulesById.Values, item => item.scheduleId).Select(item => item.Clone()).ToList(),
                workSessions = Ordered(workSessionsById.Values, item => item.workSessionId).Select(item => item.Clone()).ToList(),
                timesheets = Ordered(timesheetsById.Values, item => item.timesheetId).Select(item => item.Clone()).ToList(),
                payPeriods = Ordered(payPeriodsById.Values, item => item.payPeriodId).Select(item => item.Clone()).ToList(),
                adjustments = Ordered(adjustmentsById.Values, item => item.adjustmentId).Select(item => item.Clone()).ToList(),
                calculations = Ordered(calculationsById.Values, item => item.calculationId).Select(item => item.Clone()).ToList(),
                obligations = Ordered(obligationsById.Values, item => item.obligationId).Select(item => item.Clone()).ToList(),
                payRuns = Ordered(payRunsById.Values, item => item.payRunId).Select(item => item.Clone()).ToList(),
                paymentRecords = Ordered(paymentRecordsById.Values, item => item.paymentRecordId).Select(item => item.Clone()).ToList(),
                statements = Ordered(statementsById.Values, item => item.statementId).Select(item => item.Clone()).ToList(),
                wageDebts = Ordered(wageDebtsById.Values, item => item.wageDebtId).Select(item => item.Clone()).ToList(),
                corrections = Ordered(correctionsById.Values, item => item.correctionId).Select(item => item.Clone()).ToList(),
                overpayments = Ordered(overpaymentsById.Values, item => item.overpaymentId).Select(item => item.Clone()).ToList(),
                processedTransactionIds = PayrollModelHelpers.CleanIds(processedTransactions)
            };
        }

        public PayrollOperationResult RestoreFromSaveData(PayrollRuntimeSaveData saveData, DefinitionRegistry definitionRegistry)
        {
            long before = Revision;
            if (!ValidateSaveData(saveData, definitionRegistry, out string failure))
            {
                return PayrollOperationResult.Failure(PayrollOperationCode.PersistenceRejected, failure, before);
            }

            Clear();
            PayrollRuntimeSaveData incoming = saveData.Clone();
            AddAll(agreementsById, incoming.agreements, item => item.agreementId);
            AddAll(schedulesById, incoming.schedules, item => item.scheduleId);
            AddAll(workSessionsById, incoming.workSessions, item => item.workSessionId);
            AddAll(timesheetsById, incoming.timesheets, item => item.timesheetId);
            AddAll(payPeriodsById, incoming.payPeriods, item => item.payPeriodId);
            AddAll(adjustmentsById, incoming.adjustments, item => item.adjustmentId);
            AddAll(calculationsById, incoming.calculations, item => item.calculationId);
            AddAll(obligationsById, incoming.obligations, item => item.obligationId);
            AddAll(payRunsById, incoming.payRuns, item => item.payRunId);
            AddAll(paymentRecordsById, incoming.paymentRecords, item => item.paymentRecordId);
            AddAll(statementsById, incoming.statements, item => item.statementId);
            AddAll(wageDebtsById, incoming.wageDebts, item => item.wageDebtId);
            AddAll(correctionsById, incoming.corrections, item => item.correctionId);
            AddAll(overpaymentsById, incoming.overpayments, item => item.overpaymentId);
            processedTransactions.Clear();
            foreach (string tx in incoming.processedTransactionIds ?? Array.Empty<string>())
            {
                processedTransactions.Add(tx);
            }

            registry = definitionRegistry ?? registry;
            Revision = Math.Max(0L, incoming.revision);
            return PayrollOperationResult.Success("Payroll runtime restored.", before, Revision);
        }

        public static bool ValidateSaveData(PayrollRuntimeSaveData saveData, DefinitionRegistry registry, out string failure)
        {
            failure = string.Empty;
            if (saveData == null)
            {
                failure = "Payroll payload is missing.";
                return false;
            }

            if (saveData.schemaVersion != PayrollRuntimeSaveData.CurrentSchemaVersion)
            {
                failure = $"Unsupported payroll schema version {saveData.schemaVersion}.";
                return false;
            }

            HashSet<string> agreements = new HashSet<string>(StringComparer.Ordinal);
            foreach (CompensationAgreementData agreement in saveData.agreements ?? new List<CompensationAgreementData>())
            {
                if (agreement == null || string.IsNullOrWhiteSpace(agreement.agreementId) || !agreements.Add(agreement.agreementId))
                {
                    failure = "Payroll agreement has a missing or duplicate ID.";
                    return false;
                }

                if (registry != null && !registry.TryGet(agreement.compensationDefinitionId, out CompensationDefinition _))
                {
                    failure = $"Payroll agreement '{agreement.agreementId}' references missing Compensation definition '{agreement.compensationDefinitionId}'.";
                    return false;
                }
            }

            HashSet<string> sessions = Unique(saveData.workSessions, item => item?.workSessionId, "work session", out failure);
            if (sessions == null) return false;
            HashSet<string> calculations = Unique(saveData.calculations, item => item?.calculationId, "calculation", out failure);
            if (calculations == null) return false;
            HashSet<string> obligations = Unique(saveData.obligations, item => item?.obligationId, "obligation", out failure);
            if (obligations == null) return false;
            HashSet<string> runs = Unique(saveData.payRuns, item => item?.payRunId, "pay run", out failure);
            if (runs == null) return false;

            foreach (TimesheetData timesheet in saveData.timesheets ?? new List<TimesheetData>())
            {
                if (timesheet == null || string.IsNullOrWhiteSpace(timesheet.timesheetId) || !agreements.Contains(timesheet.agreementId) || (timesheet.workSessionIds ?? Array.Empty<string>()).Any(id => !sessions.Contains(id)))
                {
                    failure = $"Payroll timesheet '{timesheet?.timesheetId}' has invalid references.";
                    return false;
                }
            }

            foreach (PayPeriodData period in saveData.payPeriods ?? new List<PayPeriodData>())
            {
                if (period == null || string.IsNullOrWhiteSpace(period.payPeriodId) || !agreements.Contains(period.agreementId) || period.endWorldTime <= period.startWorldTime)
                {
                    failure = $"Payroll pay period '{period?.payPeriodId}' has invalid references or time range.";
                    return false;
                }
            }

            foreach (PayrollObligationData obligation in saveData.obligations ?? new List<PayrollObligationData>())
            {
                if (obligation == null || !calculations.Contains(obligation.calculationId) || obligation.amountDueUnits < obligation.amountPaidUnits || obligation.amountOutstandingUnits != Math.Max(0L, obligation.amountDueUnits - obligation.amountPaidUnits))
                {
                    failure = $"Payroll obligation '{obligation?.obligationId}' has invalid calculation or balance.";
                    return false;
                }
            }

            foreach (PayrollRunData run in saveData.payRuns ?? new List<PayrollRunData>())
            {
                if (run == null || (run.obligationIds ?? Array.Empty<string>()).Any(id => !obligations.Contains(id)))
                {
                    failure = $"Payroll run '{run?.payRunId}' references missing obligations.";
                    return false;
                }
            }

            return true;
        }

        internal void RestoreRuntimeState(PayrollRuntimeSaveData saveData)
        {
            RestoreFromSaveData(saveData, registry);
        }

        private PayrollOperationResult ApplyDeductions(PayrollCalculationData calculation, CompensationAgreementData agreement, bool preview)
        {
            long before = Revision;
            long taxableGross = Math.Max(0L, calculation.regularGrossUnits + calculation.adjustmentGrossUnits);
            long totalWithReimbursements = Math.Max(0L, taxableGross + calculation.reimbursementUnits);
            long available = taxableGross;
            foreach (string deductionId in PayrollModelHelpers.CleanIds(agreement.deductionDefinitionIds)
                .Select(id => GetDeduction(id))
                .Where(item => item != null)
                .OrderBy(item => item.Priority)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .Select(item => item.Id))
            {
                PayrollDeductionDefinition definition = GetDeduction(deductionId);
                long basis = definition.CalculationBase == DeductionCalculationBase.GrossIncludingReimbursements
                    ? totalWithReimbursements
                    : definition.CalculationBase == DeductionCalculationBase.NetAfterEarlierDeductions
                        ? available
                        : taxableGross;
                long units = checked(definition.FixedAmountUnits + ApplyRatio(basis, definition.Ratio, definition.RoundingMode));
                if (units > available)
                {
                    if (definition.InsufficientGrossPolicy == DeductionInsufficientGrossPolicy.RejectCalculation)
                    {
                        return PayrollOperationResult.Failure(PayrollOperationCode.CalculationRejected, $"Deduction '{definition.Id}' exceeds available gross.", before);
                    }

                    if (definition.InsufficientGrossPolicy == DeductionInsufficientGrossPolicy.CapAtAvailable)
                    {
                        units = available;
                    }
                }

                units = Math.Max(0L, units);
                if (units == 0L)
                {
                    continue;
                }

                calculation.deductions.Add(new PayrollDeductionLineData
                {
                    deductionLineId = $"{calculation.calculationId}.{definition.Id}",
                    deductionDefinitionId = definition.Id,
                    recipientAccountId = definition.RecipientAccountId,
                    currencyId = calculation.currencyId,
                    units = units,
                    priority = definition.Priority
                });
                available = Math.Max(0L, available - units);
            }

            calculation.deductionUnits = calculation.deductions.Sum(item => item.units);
            calculation.netPayUnits = Math.Max(0L, taxableGross - calculation.deductionUnits + calculation.reimbursementUnits);
            return PayrollOperationResult.Success("Deductions applied.", before, before, preview: preview).With(calculation: calculation);
        }

        private PayrollOperationCode ValidateAgreement(CompensationAgreementData agreement, PositionEmploymentRuntime employmentRuntime, EconomyRuntime economy, out string failure)
        {
            failure = string.Empty;
            if (agreement == null || string.IsNullOrWhiteSpace(agreement.agreementId) || string.IsNullOrWhiteSpace(agreement.compensationDefinitionId) || string.IsNullOrWhiteSpace(agreement.employmentId))
            {
                failure = "Agreement ID, compensation definition ID, and employment ID are required.";
                return PayrollOperationCode.InvalidRequest;
            }

            if (GetCompensation(agreement.compensationDefinitionId) == null)
            {
                failure = $"Compensation definition '{agreement.compensationDefinitionId}' is missing.";
                return PayrollOperationCode.MissingDefinition;
            }

            if (employmentRuntime == null || !employmentRuntime.TryGetEmployment(agreement.employmentId, out EmploymentRecordData employment))
            {
                failure = $"Employment '{agreement.employmentId}' is missing.";
                return PayrollOperationCode.MissingEmployment;
            }

            if (!string.Equals(employment.personId, agreement.employeePersonId, StringComparison.Ordinal)
                || !string.Equals(employment.employerOrganizationId, agreement.employerSubjectId, StringComparison.Ordinal)
                || (!string.IsNullOrWhiteSpace(agreement.positionInstanceId) && !string.Equals(employment.positionInstanceId, agreement.positionInstanceId, StringComparison.Ordinal)))
            {
                failure = "Agreement does not match the authoritative employment record.";
                return PayrollOperationCode.MissingEmployment;
            }

            CompensationDefinition compensation = GetCompensation(agreement.compensationDefinitionId);
            if (economy == null || !economy.TryGetAccount(agreement.employerFundingAccountId, out EconomyAccountSnapshot employerAccount) || !economy.TryGetAccount(agreement.employeeAccountId, out EconomyAccountSnapshot employeeAccount))
            {
                failure = "Employer funding and employee receiving accounts are required.";
                return PayrollOperationCode.MissingAccount;
            }

            if (!string.Equals(employerAccount.CurrencyId, compensation.CurrencyId, StringComparison.Ordinal) || !string.Equals(employeeAccount.CurrencyId, compensation.CurrencyId, StringComparison.Ordinal))
            {
                failure = "Agreement account currency does not match compensation definition currency.";
                return PayrollOperationCode.CurrencyMismatch;
            }

            foreach (string deductionId in agreement.deductionDefinitionIds ?? Array.Empty<string>())
            {
                PayrollDeductionDefinition deduction = GetDeduction(deductionId);
                if (deduction == null)
                {
                    failure = $"Deduction definition '{deductionId}' is missing.";
                    return PayrollOperationCode.MissingDefinition;
                }

                if (!string.Equals(deduction.CurrencyId, compensation.CurrencyId, StringComparison.Ordinal))
                {
                    failure = $"Deduction definition '{deductionId}' has a different currency.";
                    return PayrollOperationCode.CurrencyMismatch;
                }

                if (!string.IsNullOrWhiteSpace(deduction.RecipientAccountId)
                    && (!economy.TryGetAccount(deduction.RecipientAccountId, out EconomyAccountSnapshot recipient) || !string.Equals(recipient.CurrencyId, compensation.CurrencyId, StringComparison.Ordinal)))
                {
                    failure = $"Deduction recipient account '{deduction.RecipientAccountId}' is missing or has a different currency.";
                    return PayrollOperationCode.MissingAccount;
                }
            }

            if (agreement.effectiveEndWorldTime >= 0d && agreement.effectiveEndWorldTime <= agreement.effectiveStartWorldTime)
            {
                failure = "Agreement end must be after start.";
                return PayrollOperationCode.InvalidRequest;
            }

            agreement.state = agreement.state == CompensationAgreementState.Draft ? CompensationAgreementState.Active : agreement.state;
            return PayrollOperationCode.Succeeded;
        }

        private long CalculateRegularGross(CompensationDefinition compensation, PayPeriodData period, CompensationAgreementData agreement, int sessionCount, long minutes, long output)
        {
            switch (compensation.RateBasis)
            {
                case CompensationRateBasis.PerShift:
                    return checked(compensation.RateUnits * Math.Max(0, sessionCount));
                case CompensationRateBasis.PerTask:
                    return checked(compensation.RateUnits * Math.Max(0, sessionCount));
                case CompensationRateBasis.PerOutputQuantity:
                    return ApplyRatio(checked(compensation.RateUnits * output), new PayrollRationalData { numerator = 1L, denominator = compensation.QuantityUnit }, compensation.RoundingMode);
                case CompensationRateBasis.PerPayPeriod:
                    return ProrateSalary(compensation.RateUnits, period, agreement, compensation.RoundingMode);
                case CompensationRateBasis.PerDurationUnit:
                default:
                    long divisor = compensation.DurationUnit == PayrollDurationUnit.Day ? 1440L : compensation.DurationUnit == PayrollDurationUnit.Minute ? 1L : 60L;
                    return ApplyRatio(checked(compensation.RateUnits * minutes), new PayrollRationalData { numerator = 1L, denominator = divisor }, compensation.RoundingMode);
            }
        }

        private static long ProrateSalary(long rateUnits, PayPeriodData period, CompensationAgreementData agreement, PayrollRoundingMode rounding)
        {
            double start = Math.Max(period.startWorldTime, agreement.effectiveStartWorldTime);
            double end = agreement.effectiveEndWorldTime >= 0d ? Math.Min(period.endWorldTime, agreement.effectiveEndWorldTime) : period.endWorldTime;
            if (end <= start)
            {
                return 0L;
            }

            long active = Math.Max(0L, RoundLong(end - start));
            long full = Math.Max(1L, RoundLong(period.endWorldTime - period.startWorldTime));
            return ApplyRatio(rateUnits, new PayrollRationalData { numerator = active, denominator = full }, rounding);
        }

        public static long ApplyRatio(long baseUnits, PayrollRationalData ratio, PayrollRoundingMode rounding)
        {
            ratio ??= new PayrollRationalData();
            long denominator = Math.Max(1L, ratio.denominator);
            long numerator = ratio.numerator;
            decimal raw = (decimal)baseUnits * numerator / denominator;
            switch (rounding)
            {
                case PayrollRoundingMode.Ceiling:
                    return (long)Math.Ceiling(raw);
                case PayrollRoundingMode.Floor:
                    return (long)Math.Floor(raw);
                case PayrollRoundingMode.NearestUp:
                case PayrollRoundingMode.HalfAwayFromZero:
                    return (long)Math.Round(raw, 0, MidpointRounding.AwayFromZero);
                case PayrollRoundingMode.TowardZero:
                default:
                    return (long)decimal.Truncate(raw);
            }
        }

        private long ReservationAvailable(string reservationId, EconomyRuntime economy)
        {
            return economy.Reservations.FirstOrDefault(item => string.Equals(item.reservationId, reservationId, StringComparison.Ordinal) && item.state == EconomyReservationState.Active)?.units ?? 0L;
        }

        private CompensationDefinition GetCompensation(string id)
        {
            return registry != null && registry.TryGet(id ?? string.Empty, out CompensationDefinition definition) ? definition : null;
        }

        private PayrollDeductionDefinition GetDeduction(string id)
        {
            return registry != null && registry.TryGet(id ?? string.Empty, out PayrollDeductionDefinition definition) ? definition : null;
        }

        private void Remember(string transactionId)
        {
            if (!string.IsNullOrWhiteSpace(transactionId))
            {
                processedTransactions.Add(transactionId);
            }
        }

        private static bool FailAt(string actual, string expected)
        {
            return !string.IsNullOrWhiteSpace(actual) && string.Equals(actual, expected, StringComparison.Ordinal);
        }

        private static PayrollPaymentRecordData Payment(string id, string runId, string obligationId, string economyTx, string from, string to, string currency, long units, double worldTime, string kind)
        {
            return new PayrollPaymentRecordData
            {
                paymentRecordId = id,
                payRunId = runId,
                obligationId = obligationId,
                economyTransactionId = economyTx ?? string.Empty,
                fromAccountId = from,
                toAccountId = to,
                currencyId = currency,
                units = units,
                paidWorldTime = worldTime,
                kind = kind
            };
        }

        private static PayStatementData Statement(string id, PayrollRunData run, PayrollObligationData obligation, PayrollCalculationData calculation, long paid)
        {
            return new PayStatementData
            {
                statementId = id,
                payRunId = run.payRunId,
                obligationId = obligation.obligationId,
                calculationId = calculation.calculationId,
                employeePersonId = obligation.employeePersonId,
                employerSubjectId = obligation.employerSubjectId,
                currencyId = obligation.currencyId,
                grossUnits = Math.Max(0L, calculation.regularGrossUnits + calculation.adjustmentGrossUnits),
                reimbursementUnits = calculation.reimbursementUnits,
                deductionUnits = calculation.deductionUnits,
                netUnits = calculation.netPayUnits,
                paidUnits = paid,
                deductionLineIds = calculation.deductions.Select(item => item.deductionLineId).ToArray()
            };
        }

        private static bool SameAgreement(CompensationAgreementData first, CompensationAgreementData second)
        {
            return first != null && second != null
                && string.Equals(first.compensationDefinitionId, second.compensationDefinitionId, StringComparison.Ordinal)
                && string.Equals(first.employmentId, second.employmentId, StringComparison.Ordinal)
                && string.Equals(first.employeePersonId, second.employeePersonId, StringComparison.Ordinal)
                && string.Equals(first.employerSubjectId, second.employerSubjectId, StringComparison.Ordinal)
                && string.Equals(first.employerFundingAccountId, second.employerFundingAccountId, StringComparison.Ordinal)
                && string.Equals(first.employeeAccountId, second.employeeAccountId, StringComparison.Ordinal);
        }

        private static bool SameSession(WorkSessionData first, WorkSessionData second)
        {
            return first != null && second != null
                && string.Equals(first.agreementId, second.agreementId, StringComparison.Ordinal)
                && first.startWorldTime.Equals(second.startWorldTime)
                && first.endWorldTime.Equals(second.endWorldTime)
                && first.durationMinutes == second.durationMinutes
                && first.creditedOutputQuantity == second.creditedOutputQuantity;
        }

        private static bool RangesOverlap(double startA, double endA, double startB, double endB)
        {
            double aEnd = endA < 0d ? double.MaxValue : endA;
            double bEnd = endB < 0d ? double.MaxValue : endB;
            return startA < bEnd && startB < aEnd;
        }

        private static long RoundLong(double value)
        {
            return (long)Math.Round(value, 0, MidpointRounding.AwayFromZero);
        }

        private void Clear()
        {
            agreementsById.Clear();
            schedulesById.Clear();
            workSessionsById.Clear();
            timesheetsById.Clear();
            payPeriodsById.Clear();
            adjustmentsById.Clear();
            calculationsById.Clear();
            obligationsById.Clear();
            payRunsById.Clear();
            paymentRecordsById.Clear();
            statementsById.Clear();
            wageDebtsById.Clear();
            correctionsById.Clear();
            overpaymentsById.Clear();
            processedTransactions.Clear();
        }

        private static IEnumerable<T> Ordered<T>(IEnumerable<T> items, Func<T, string> key)
        {
            return (items ?? Array.Empty<T>()).OrderBy(key, StringComparer.Ordinal);
        }

        private static void AddAll<T>(Dictionary<string, T> target, IEnumerable<T> source, Func<T, string> key)
        {
            foreach (T item in source ?? Array.Empty<T>())
            {
                target.Add(key(item), item);
            }
        }

        private static HashSet<string> Unique<T>(IEnumerable<T> source, Func<T, string> key, string label, out string failure)
        {
            failure = string.Empty;
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (T item in source ?? Array.Empty<T>())
            {
                string id = key(item);
                if (string.IsNullOrWhiteSpace(id) || !ids.Add(id))
                {
                    failure = $"Payroll {label} has a missing or duplicate ID.";
                    return null;
                }
            }

            return ids;
        }
    }
}
