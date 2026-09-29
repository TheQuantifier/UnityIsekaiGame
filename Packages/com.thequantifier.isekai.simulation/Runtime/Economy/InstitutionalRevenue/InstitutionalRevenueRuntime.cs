using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Progression;

namespace UnityIsekaiGame.Economy.InstitutionalRevenue
{
    public sealed class InstitutionalRevenueRuntime
    {
        public const int CurrentSaveSchemaVersion = 1;
        public static readonly string[] ProtectedDetails =
        {
            "detail.subject",
            "detail.amount",
            "detail.accounts",
            "detail.adjustments",
            "detail.filing",
            "detail.audit",
            "detail.authority",
            "detail.provenance"
        };

        private readonly Dictionary<string, InstitutionalRevenueAuthorityData> authoritiesById = new Dictionary<string, InstitutionalRevenueAuthorityData>(StringComparer.Ordinal);
        private readonly Dictionary<string, InstitutionalRevenueAccountAssignmentData> accountAssignmentsById = new Dictionary<string, InstitutionalRevenueAccountAssignmentData>(StringComparer.Ordinal);
        private readonly Dictionary<string, TaxableEventData> taxableEventsById = new Dictionary<string, TaxableEventData>(StringComparer.Ordinal);
        private readonly Dictionary<string, TaxBaseCalculationData> baseCalculationsById = new Dictionary<string, TaxBaseCalculationData>(StringComparer.Ordinal);
        private readonly Dictionary<string, RevenueAdjustmentData> exemptionsById = new Dictionary<string, RevenueAdjustmentData>(StringComparer.Ordinal);
        private readonly Dictionary<string, RevenueAdjustmentData> deductionsById = new Dictionary<string, RevenueAdjustmentData>(StringComparer.Ordinal);
        private readonly Dictionary<string, RevenueAdjustmentData> creditsById = new Dictionary<string, RevenueAdjustmentData>(StringComparer.Ordinal);
        private readonly Dictionary<string, AssessmentPeriodData> periodsById = new Dictionary<string, AssessmentPeriodData>(StringComparer.Ordinal);
        private readonly Dictionary<string, InstitutionalAssessmentData> assessmentsById = new Dictionary<string, InstitutionalAssessmentData>(StringComparer.Ordinal);
        private readonly Dictionary<string, InstitutionalObligationData> obligationsById = new Dictionary<string, InstitutionalObligationData>(StringComparer.Ordinal);
        private readonly Dictionary<string, InstitutionalPaymentData> paymentsById = new Dictionary<string, InstitutionalPaymentData>(StringComparer.Ordinal);
        private readonly Dictionary<string, WithholdingRecordData> withholdingsById = new Dictionary<string, WithholdingRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, InstitutionalRevenueRecordData> revenueRecordsById = new Dictionary<string, InstitutionalRevenueRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, RevenueAllocationData> allocationsById = new Dictionary<string, RevenueAllocationData>(StringComparer.Ordinal);
        private readonly Dictionary<string, RevenueWaiverData> waiversById = new Dictionary<string, RevenueWaiverData>(StringComparer.Ordinal);
        private readonly Dictionary<string, RevenueRefundData> refundsById = new Dictionary<string, RevenueRefundData>(StringComparer.Ordinal);
        private readonly Dictionary<string, RevenuePenaltyData> penaltiesById = new Dictionary<string, RevenuePenaltyData>(StringComparer.Ordinal);
        private readonly Dictionary<string, RevenueFilingData> filingsById = new Dictionary<string, RevenueFilingData>(StringComparer.Ordinal);
        private readonly Dictionary<string, RevenueAuditFindingData> auditFindingsById = new Dictionary<string, RevenueAuditFindingData>(StringComparer.Ordinal);
        private readonly Dictionary<string, RevenueStatementData> statementsById = new Dictionary<string, RevenueStatementData>(StringComparer.Ordinal);
        private readonly Dictionary<string, RevenueReceiptData> receiptsById = new Dictionary<string, RevenueReceiptData>(StringComparer.Ordinal);
        private readonly Dictionary<string, RevenueProcessedTransactionData> processedByTransactionId = new Dictionary<string, RevenueProcessedTransactionData>(StringComparer.Ordinal);
        private readonly HashSet<string> exclusiveAssessmentKeys = new HashSet<string>(StringComparer.Ordinal);

        private DefinitionRegistry registry;
        private string worldId = PersistenceService.LocalWorldId;

        public long Revision { get; private set; }
        public string WorldId => worldId;
        public int AssessmentCount => assessmentsById.Count;
        public int ObligationCount => obligationsById.Count;
        public int RevenueRecordCount => revenueRecordsById.Count;
        public int WithholdingCount => withholdingsById.Count;
        public IReadOnlyList<InstitutionalRevenueAuthorityData> Authorities => authoritiesById.Values.OrderBy(item => item.authorityId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<InstitutionalRevenueAccountAssignmentData> AccountAssignments => accountAssignmentsById.Values.OrderBy(item => item.assignmentId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<TaxableEventData> TaxableEvents => taxableEventsById.Values.OrderBy(item => item.taxableEventId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<TaxBaseCalculationData> BaseCalculations => baseCalculationsById.Values.OrderBy(item => item.baseCalculationId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<InstitutionalAssessmentData> Assessments => assessmentsById.Values.OrderBy(item => item.assessmentId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<InstitutionalObligationData> Obligations => obligationsById.Values.OrderBy(item => item.obligationId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<InstitutionalPaymentData> Payments => paymentsById.Values.OrderBy(item => item.paymentId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<WithholdingRecordData> Withholdings => withholdingsById.Values.OrderBy(item => item.withholdingId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<InstitutionalRevenueRecordData> RevenueRecords => revenueRecordsById.Values.OrderBy(item => item.revenueRecordId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<RevenueAllocationData> Allocations => allocationsById.Values.OrderBy(item => item.allocationId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<RevenueRefundData> Refunds => refundsById.Values.OrderBy(item => item.refundId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<RevenueFilingData> Filings => filingsById.Values.OrderBy(item => item.filingId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<RevenueAuditFindingData> AuditFindings => auditFindingsById.Values.OrderBy(item => item.findingId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();

        public void Configure(DefinitionRegistry definitionRegistry, string runtimeWorldId = "")
        {
            registry = definitionRegistry;
            worldId = string.IsNullOrWhiteSpace(runtimeWorldId) ? PersistenceService.LocalWorldId : runtimeWorldId;
        }

        public bool TryGetAssessment(string assessmentId, out InstitutionalAssessmentData assessment)
        {
            if (assessmentsById.TryGetValue(assessmentId ?? string.Empty, out InstitutionalAssessmentData found))
            {
                assessment = found.Clone();
                return true;
            }

            assessment = null;
            return false;
        }

        public bool TryGetObligation(string obligationId, out InstitutionalObligationData obligation)
        {
            if (obligationsById.TryGetValue(obligationId ?? string.Empty, out InstitutionalObligationData found))
            {
                obligation = found.Clone();
                return true;
            }

            obligation = null;
            return false;
        }

        public bool TryGetRevenueRecord(string recordId, out InstitutionalRevenueRecordData record)
        {
            if (revenueRecordsById.TryGetValue(recordId ?? string.Empty, out InstitutionalRevenueRecordData found))
            {
                record = found.Clone();
                return true;
            }

            record = null;
            return false;
        }

        public InstitutionalRevenueOperationResult RegisterAuthority(InstitutionalRevenueAuthorityData authority, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            if (!ValidateAuthority(authority, out string failure))
            {
                return Fail(RevenueOperationCode.InvalidRequest, failure, preview);
            }

            InstitutionalRevenueAuthorityData clean = authority.Clone();
            string key = $"authority:{clean.authorityId}";
            if (!preview && IsDuplicate(transactionId, key, out InstitutionalRevenueOperationResult duplicate))
            {
                return duplicate.With(authority: authoritiesById.TryGetValue(clean.authorityId, out InstitutionalRevenueAuthorityData live) ? live : clean);
            }

            if (authoritiesById.ContainsKey(clean.authorityId))
            {
                return Fail(RevenueOperationCode.InvalidRequest, $"Revenue authority '{clean.authorityId}' already exists.", preview);
            }

            if (preview)
            {
                return InstitutionalRevenueOperationResult.Success("Revenue authority preview succeeded.", before, before, preview: true).With(authority: clean);
            }

            authoritiesById.Add(clean.authorityId, clean);
            Revision++;
            Remember(transactionId, key, RevenueOperationCode.Succeeded);
            return InstitutionalRevenueOperationResult.Success("Revenue authority registered.", before, Revision).With(authority: clean);
        }

        public InstitutionalRevenueOperationResult AssignRevenueAccount(InstitutionalRevenueAccountAssignmentData assignment, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            if (!ValidateAccountAssignment(assignment, out string failure))
            {
                return Fail(RevenueOperationCode.InvalidRequest, failure, preview);
            }

            InstitutionalRevenueAccountAssignmentData clean = assignment.Clone();
            if (!authoritiesById.TryGetValue(clean.receivingAuthorityId, out InstitutionalRevenueAuthorityData authority) || !authority.canCollect && !authority.canReceiveRemittance)
            {
                return Fail(RevenueOperationCode.MissingAuthority, $"Receiving authority '{clean.receivingAuthorityId}' is missing or cannot collect/remit.", preview);
            }

            string key = $"account-assignment:{clean.assignmentId}";
            if (!preview && IsDuplicate(transactionId, key, out InstitutionalRevenueOperationResult duplicate))
            {
                return duplicate.With(accountAssignment: accountAssignmentsById.TryGetValue(clean.assignmentId, out InstitutionalRevenueAccountAssignmentData live) ? live : clean);
            }

            if (accountAssignmentsById.ContainsKey(clean.assignmentId))
            {
                return Fail(RevenueOperationCode.InvalidRequest, $"Revenue account assignment '{clean.assignmentId}' already exists.", preview);
            }

            if (preview)
            {
                return InstitutionalRevenueOperationResult.Success("Revenue account assignment preview succeeded.", before, before, preview: true).With(accountAssignment: clean);
            }

            accountAssignmentsById.Add(clean.assignmentId, clean);
            Revision++;
            Remember(transactionId, key, RevenueOperationCode.Succeeded);
            return InstitutionalRevenueOperationResult.Success("Revenue account assignment registered.", before, Revision).With(accountAssignment: clean);
        }

        public InstitutionalRevenueOperationResult RegisterTaxableEvent(TaxableEventData taxableEvent, string authorityId, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            if (!ValidateTaxableEvent(taxableEvent, out string failure))
            {
                return Fail(RevenueOperationCode.InvalidRequest, failure, preview);
            }

            TaxableEventData clean = taxableEvent.Clone();
            if (!TryGetDefinition(clean.revenueDefinitionId, out InstitutionalRevenueDefinition definition, out failure))
            {
                return Fail(RevenueOperationCode.MissingDefinition, failure, preview);
            }

            if (!AuthorityPermits(authorityId, definition, clean.assessedSubject, clean.currencyId, clean.eventWorldTime, requireAssess: true, requireCollect: false, requireRefund: false, requireWaive: false, requireAudit: false, requireAllocate: false, out failure))
            {
                return Fail(RevenueOperationCode.Unauthorized, failure, preview);
            }

            if (!definition.TaxableEventCategories.Contains(clean.eventCategory))
            {
                return Fail(RevenueOperationCode.InvalidRequest, $"Taxable event category '{clean.eventCategory}' is not eligible for '{definition.Id}'.", preview);
            }

            string key = $"taxable-event:{clean.taxableEventId}";
            if (!preview && IsDuplicate(transactionId, key, out InstitutionalRevenueOperationResult duplicate))
            {
                return duplicate.With(taxableEvent: taxableEventsById.TryGetValue(clean.taxableEventId, out TaxableEventData live) ? live : clean);
            }

            if (taxableEventsById.ContainsKey(clean.taxableEventId))
            {
                return Fail(RevenueOperationCode.InvalidRequest, $"Taxable event '{clean.taxableEventId}' already exists.", preview);
            }

            if (preview)
            {
                return InstitutionalRevenueOperationResult.Success("Taxable event preview succeeded.", before, before, preview: true).With(taxableEvent: clean);
            }

            taxableEventsById.Add(clean.taxableEventId, clean);
            Revision++;
            Remember(transactionId, key, RevenueOperationCode.Succeeded);
            return InstitutionalRevenueOperationResult.Success("Taxable event registered.", before, Revision).With(taxableEvent: clean);
        }

        public InstitutionalRevenueOperationResult RegisterExemption(RevenueAdjustmentData exemption, string authorityId, string transactionId = "", bool preview = false)
        {
            return RegisterAdjustment(exemption, exemptionsById, "exemption", authorityId, requireWaive: true, transactionId, preview);
        }

        public InstitutionalRevenueOperationResult RegisterDeduction(RevenueAdjustmentData deduction, string authorityId, string transactionId = "", bool preview = false)
        {
            return RegisterAdjustment(deduction, deductionsById, "deduction", authorityId, requireWaive: false, transactionId, preview);
        }

        public InstitutionalRevenueOperationResult RegisterCredit(RevenueAdjustmentData credit, string authorityId, string transactionId = "", bool preview = false)
        {
            return RegisterAdjustment(credit, creditsById, "credit", authorityId, requireWaive: false, transactionId, preview);
        }

        public TaxBaseCalculationData CalculateBasePreview(string revenueDefinitionId, string taxableEventId, double worldTime, out string failure)
        {
            failure = string.Empty;
            if (!taxableEventsById.TryGetValue(taxableEventId ?? string.Empty, out TaxableEventData taxableEvent))
            {
                failure = $"Taxable event '{taxableEventId}' is missing.";
                return null;
            }

            if (!TryGetDefinition(revenueDefinitionId, out InstitutionalRevenueDefinition definition, out failure))
            {
                return null;
            }

            if (!string.Equals(taxableEvent.currencyId, definition.CurrencyId, StringComparison.Ordinal))
            {
                failure = $"Taxable event currency '{taxableEvent.currencyId}' does not match revenue definition currency '{definition.CurrencyId}'.";
                return null;
            }

            return BuildBaseCalculation(definition, taxableEvent, worldTime, mutateAdjustments: false);
        }

        public InstitutionalRevenueOperationResult GenerateAssessment(string assessmentId, string revenueDefinitionId, IEnumerable<string> taxableEventIds, string authorityId, string periodId, double worldTime = 0d, bool approve = false, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            if (string.IsNullOrWhiteSpace(assessmentId))
            {
                return Fail(RevenueOperationCode.InvalidRequest, "Assessment ID is required.", preview);
            }

            if (assessmentsById.ContainsKey(assessmentId))
            {
                return Fail(RevenueOperationCode.InvalidRequest, $"Assessment '{assessmentId}' already exists.", preview);
            }

            if (!TryGetDefinition(revenueDefinitionId, out InstitutionalRevenueDefinition definition, out string failure))
            {
                return Fail(RevenueOperationCode.MissingDefinition, failure, preview);
            }

            string[] events = InstitutionalRevenueModelHelpers.CleanIds(taxableEventIds);
            if (events.Length == 0)
            {
                return Fail(RevenueOperationCode.MissingEvent, "At least one taxable event is required.", preview);
            }

            List<TaxableEventData> taxableEvents = new List<TaxableEventData>();
            foreach (string eventId in events)
            {
                if (!taxableEventsById.TryGetValue(eventId, out TaxableEventData taxableEvent))
                {
                    return Fail(RevenueOperationCode.MissingEvent, $"Taxable event '{eventId}' is missing.", preview);
                }

                if (!string.Equals(taxableEvent.revenueDefinitionId, definition.Id, StringComparison.Ordinal))
                {
                    return Fail(RevenueOperationCode.InvalidRequest, $"Taxable event '{eventId}' is not for revenue definition '{definition.Id}'.", preview);
                }

                if (taxableEvent.exclusiveAssessment && exclusiveAssessmentKeys.Contains(ExclusiveAssessmentKey(definition.Id, periodId, eventId)))
                {
                    return Fail(RevenueOperationCode.AlreadyAssessed, $"Taxable event '{eventId}' was already assessed for definition '{definition.Id}' and period '{periodId}'.", preview);
                }

                taxableEvents.Add(taxableEvent);
            }

            TaxableEventData first = taxableEvents.OrderBy(item => item.taxableEventId, StringComparer.Ordinal).First();
            if (!AuthorityPermits(authorityId, definition, first.assessedSubject, definition.CurrencyId, worldTime, requireAssess: true, requireCollect: false, requireRefund: false, requireWaive: false, requireAudit: false, requireAllocate: false, out failure))
            {
                return Fail(RevenueOperationCode.Unauthorized, failure, preview);
            }

            string key = $"assessment:{definition.Id}:{periodId}:{string.Join(",", events)}";
            if (!preview && IsDuplicate(transactionId, key, out InstitutionalRevenueOperationResult duplicate))
            {
                return duplicate.With(assessment: assessmentsById.TryGetValue(assessmentId, out InstitutionalAssessmentData live) ? live : null);
            }

            List<TaxBaseCalculationData> bases = taxableEvents
                .OrderBy(item => item.taxableEventId, StringComparer.Ordinal)
                .Select(item => BuildBaseCalculation(definition, item, worldTime, mutateAdjustments: false))
                .ToList();

            long taxableBase = bases.Aggregate(0L, (sum, item) => checked(sum + item.finalTaxableBaseUnits));
            long grossCharge = CalculateCharge(definition.RatePolicy, taxableBase);
            long credit = MatchingAdjustments(creditsById.Values, definition.Id, first.assessedSubject.subjectId, worldTime).Aggregate(0L, (sum, item) => checked(sum + Math.Min(item.RemainingUnits, Math.Max(0L, grossCharge - sum))));
            long final = Math.Max(0L, checked(grossCharge - credit));
            InstitutionalAssessmentData assessment = new InstitutionalAssessmentData
            {
                assessmentId = assessmentId.Trim(),
                revenueDefinitionId = definition.Id,
                institutionId = first.institutionId,
                assessedSubject = first.assessedSubject.Clone(),
                withholdingSubject = first.otherSubjects.FirstOrDefault(subject => subject.role == RevenueSubjectRole.WithholdingAgent)?.Clone() ?? new RevenueSubjectReferenceData(),
                remittingSubject = first.otherSubjects.FirstOrDefault(subject => subject.role == RevenueSubjectRole.RemittingParty)?.Clone() ?? new RevenueSubjectReferenceData(),
                periodId = string.IsNullOrWhiteSpace(periodId) ? $"period.{Sanitize(assessmentId)}" : periodId.Trim(),
                taxableEventIds = events,
                baseCalculationIds = bases.Select(item => item.baseCalculationId).ToArray(),
                currencyId = definition.CurrencyId,
                grossChargeUnits = grossCharge,
                exemptionUnits = bases.Sum(item => item.exemptUnits),
                deductionUnits = bases.Sum(item => item.deductionUnits),
                creditUnits = credit,
                finalAssessedUnits = final,
                amountDueUnits = final,
                dueWorldTime = definition.DueDelayUnits <= 0L ? worldTime : worldTime + definition.DueDelayUnits,
                approvalAuthorityId = approve ? authorityId : string.Empty,
                state = approve ? RevenueAssessmentState.Approved : RevenueAssessmentState.Calculated,
                accessPolicyId = definition.AccessPolicyId,
                provenance = $"authority:{authorityId}",
                revision = 1L
            };

            AssessmentPeriodData period = periodsById.TryGetValue(assessment.periodId, out AssessmentPeriodData existingPeriod)
                ? existingPeriod.Clone()
                : new AssessmentPeriodData
                {
                    periodId = assessment.periodId,
                    revenueDefinitionId = definition.Id,
                    institutionId = assessment.institutionId,
                    subjectId = assessment.assessedSubject.subjectId,
                    periodKind = definition.PeriodKind,
                    state = AssessmentPeriodState.Open,
                    startWorldTime = taxableEvents.Min(item => item.eventWorldTime),
                    endWorldTime = taxableEvents.Max(item => item.eventWorldTime),
                    paymentDueWorldTime = assessment.dueWorldTime
                };
            period.taxableEventIds = period.taxableEventIds.Concat(events).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            period.assessmentIds = period.assessmentIds.Concat(new[] { assessment.assessmentId }).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            period.state = approve ? AssessmentPeriodState.Assessed : AssessmentPeriodState.ReadyForAssessment;
            period.revision++;

            InstitutionalObligationData obligation = null;
            if (approve)
            {
                if (!TryResolveInstitutionAccount(definition, assessment.institutionId, definition.CurrencyId, definition.CollectionAccountPurpose, out InstitutionalRevenueAccountAssignmentData assignment))
                {
                    return Fail(RevenueOperationCode.MissingAccount, $"No institutional collection account is assigned for institution '{assessment.institutionId}' and currency '{definition.CurrencyId}'.", preview);
                }

                obligation = new InstitutionalObligationData
                {
                    obligationId = $"obligation.{assessment.assessmentId}",
                    assessmentId = assessment.assessmentId,
                    revenueDefinitionId = definition.Id,
                    institutionId = assessment.institutionId,
                    payerSubjectId = assessment.assessedSubject.subjectId,
                    payerAccountId = assessment.assessedSubject.accountId,
                    institutionAccountId = assignment.accountId,
                    currencyId = definition.CurrencyId,
                    amountDueUnits = assessment.amountDueUnits,
                    dueWorldTime = assessment.dueWorldTime,
                    state = InstitutionalObligationState.Due,
                    accessPolicyId = assessment.accessPolicyId,
                    provenance = assessment.provenance
                };
                assessment.obligationId = obligation.obligationId;
            }

            if (preview)
            {
                return InstitutionalRevenueOperationResult.Success("Assessment preview succeeded.", before, before, preview: true).With(assessment: assessment, obligation: obligation, baseCalculation: bases.FirstOrDefault());
            }

            foreach (TaxBaseCalculationData baseCalculation in bases)
            {
                baseCalculationsById.Add(baseCalculation.baseCalculationId, baseCalculation);
            }

            foreach (RevenueAdjustmentData adjustment in MatchingAdjustments(exemptionsById.Values, definition.Id, first.assessedSubject.subjectId, worldTime).Concat(MatchingAdjustments(deductionsById.Values, definition.Id, first.assessedSubject.subjectId, worldTime)).Concat(MatchingAdjustments(creditsById.Values, definition.Id, first.assessedSubject.subjectId, worldTime)))
            {
                ApplyAdjustmentUse(adjustment.adjustmentId, Math.Min(adjustment.RemainingUnits, adjustment.amountUnits));
            }

            periodsById[period.periodId] = period;
            assessmentsById.Add(assessment.assessmentId, assessment);
            foreach (string eventId in events)
            {
                if (taxableEventsById.TryGetValue(eventId, out TaxableEventData taxableEvent) && taxableEvent.exclusiveAssessment)
                {
                    exclusiveAssessmentKeys.Add(ExclusiveAssessmentKey(definition.Id, period.periodId, eventId));
                }
            }

            if (obligation != null)
            {
                obligationsById.Add(obligation.obligationId, obligation);
            }

            Revision++;
            Remember(transactionId, key, RevenueOperationCode.Succeeded);
            return InstitutionalRevenueOperationResult.Success("Assessment generated.", before, Revision).With(assessment: assessment, obligation: obligation, baseCalculation: bases.FirstOrDefault());
        }

        public InstitutionalRevenueOperationResult ApproveAssessment(string assessmentId, string authorityId, string payerAccountId, string institutionAccountId, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            if (!assessmentsById.TryGetValue(assessmentId ?? string.Empty, out InstitutionalAssessmentData assessment))
            {
                return Fail(RevenueOperationCode.MissingAssessment, $"Assessment '{assessmentId}' is missing.", preview);
            }

            if (assessment.Immutable)
            {
                return Fail(RevenueOperationCode.Immutable, $"Assessment '{assessmentId}' is already immutable.", preview);
            }

            if (!TryGetDefinition(assessment.revenueDefinitionId, out InstitutionalRevenueDefinition definition, out string failure))
            {
                return Fail(RevenueOperationCode.MissingDefinition, failure, preview);
            }

            if (!AuthorityPermits(authorityId, definition, assessment.assessedSubject, assessment.currencyId, 0d, requireAssess: true, requireCollect: false, requireRefund: false, requireWaive: false, requireAudit: false, requireAllocate: false, out failure))
            {
                return Fail(RevenueOperationCode.Unauthorized, failure, preview);
            }

            string key = $"approve-assessment:{assessmentId}";
            if (!preview && IsDuplicate(transactionId, key, out InstitutionalRevenueOperationResult duplicate))
            {
                return duplicate.With(assessment: assessment);
            }

            InstitutionalAssessmentData updated = assessment.Clone();
            updated.state = RevenueAssessmentState.Approved;
            updated.approvalAuthorityId = authorityId;
            updated.revision++;
            InstitutionalObligationData obligation = new InstitutionalObligationData
            {
                obligationId = $"obligation.{updated.assessmentId}",
                assessmentId = updated.assessmentId,
                revenueDefinitionId = updated.revenueDefinitionId,
                institutionId = updated.institutionId,
                payerSubjectId = updated.assessedSubject.subjectId,
                payerAccountId = string.IsNullOrWhiteSpace(payerAccountId) ? updated.assessedSubject.accountId : payerAccountId,
                institutionAccountId = institutionAccountId,
                currencyId = updated.currencyId,
                amountDueUnits = updated.amountDueUnits,
                dueWorldTime = updated.dueWorldTime,
                state = InstitutionalObligationState.Due,
                accessPolicyId = updated.accessPolicyId,
                provenance = $"authority:{authorityId}"
            };
            updated.obligationId = obligation.obligationId;

            if (preview)
            {
                return InstitutionalRevenueOperationResult.Success("Assessment approval preview succeeded.", before, before, preview: true).With(assessment: updated, obligation: obligation);
            }

            assessmentsById[updated.assessmentId] = updated;
            obligationsById.Add(obligation.obligationId, obligation);
            Revision++;
            Remember(transactionId, key, RevenueOperationCode.Succeeded);
            return InstitutionalRevenueOperationResult.Success("Assessment approved.", before, Revision).With(assessment: updated, obligation: obligation);
        }

        public InstitutionalRevenueOperationResult PayObligation(string obligationId, EconomyRuntime economy, string transactionId, long units, double worldTime = 0d, string injectFailureStage = "", bool preview = false)
        {
            return TransferToInstitution(obligationId, economy, transactionId, units, worldTime, injectFailureStage, preview, remittance: false, withholdingId: string.Empty);
        }

        public InstitutionalRevenueOperationResult WithholdFromPayment(WithholdingRecordData withholding, EconomyRuntime economy, string transactionId, string injectFailureStage = "", bool preview = false)
        {
            long before = Revision;
            if (!ValidateWithholding(withholding, out string failure))
            {
                return Fail(RevenueOperationCode.InvalidRequest, failure, preview);
            }

            WithholdingRecordData clean = withholding.Clone();
            string key = $"withholding:{clean.withholdingId}:{clean.withheldUnits}";
            if (!preview && IsDuplicate(transactionId, key, out InstitutionalRevenueOperationResult duplicate))
            {
                return duplicate.With(withholding: withholdingsById.TryGetValue(clean.withholdingId, out WithholdingRecordData live) ? live : clean);
            }

            if (withholdingsById.ContainsKey(clean.withholdingId))
            {
                return Fail(RevenueOperationCode.InvalidRequest, $"Withholding '{clean.withholdingId}' already exists.", preview);
            }

            if (economy == null)
            {
                return Fail(RevenueOperationCode.MissingAccount, "Economy runtime is missing.", preview);
            }

            InstitutionalRevenueRuntimeSaveData revenueRollback = CreateSaveData();
            EconomyRuntimeSaveData economyRollback = economy.CreateSaveData();
            try
            {
                EconomyOperationResult transfer = economy.Transfer(transactionId, clean.withheldFromAccountId, clean.holdingAccountId, new MoneyAmount(clean.currencyId, clean.withheldUnits), EconomyTransactionKind.Payment, actorId: clean.withholdingAgentSubjectId, preview: preview);
                if (!transfer.Succeeded)
                {
                    return EconomyFailure(transfer, before, preview);
                }

                if (preview)
                {
                    return InstitutionalRevenueOperationResult.Success("Withholding preview succeeded.", before, before, preview: true).With(withholding: clean, economyTransaction: transfer.Transaction?.Data);
                }

                if (FailAt(injectFailureStage, "after-economy-transfer"))
                {
                    throw new InvalidOperationException("Injected withholding failure after economy transfer.");
                }

                withholdingsById.Add(clean.withholdingId, clean);
                Revision++;
                Remember(transactionId, key, RevenueOperationCode.Succeeded);
                return InstitutionalRevenueOperationResult.Success("Withholding recorded.", before, Revision).With(withholding: clean, economyTransaction: transfer.Transaction?.Data);
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is OverflowException)
            {
                RestoreFromSaveData(revenueRollback, registry);
                economy.RestoreFromSaveData(economyRollback, registry);
                return InstitutionalRevenueOperationResult.Failure(exception is OverflowException ? RevenueOperationCode.ArithmeticOverflow : RevenueOperationCode.RolledBack, exception.Message, before);
            }
        }

        public InstitutionalRevenueOperationResult RemitWithholding(string withholdingId, EconomyRuntime economy, string transactionId, long units, double worldTime = 0d, string injectFailureStage = "", bool preview = false)
        {
            long before = Revision;
            if (!withholdingsById.TryGetValue(withholdingId ?? string.Empty, out WithholdingRecordData withholding))
            {
                return Fail(RevenueOperationCode.MissingWithholding, $"Withholding '{withholdingId}' is missing.", preview);
            }

            long payable = Math.Min(Math.Max(0L, units), withholding.UnremittedUnits);
            if (payable <= 0L)
            {
                return Fail(RevenueOperationCode.InvalidRequest, "Remittance amount must be positive.", preview);
            }

            string obligationId = string.IsNullOrWhiteSpace(withholding.assessmentId) ? string.Empty : assessmentsById.TryGetValue(withholding.assessmentId, out InstitutionalAssessmentData assessment) ? assessment.obligationId : string.Empty;
            InstitutionalRevenueOperationResult payment = TransferWithholdingToInstitution(withholding, economy, transactionId, payable, worldTime, injectFailureStage, preview, obligationId);
            return payment;
        }

        public InstitutionalRevenueOperationResult RecognizeRevenue(string paymentId, string revenueRecordId, string classification, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            if (!paymentsById.TryGetValue(paymentId ?? string.Empty, out InstitutionalPaymentData payment))
            {
                return Fail(RevenueOperationCode.InvalidRequest, $"Payment '{paymentId}' is missing.", preview);
            }

            if (string.IsNullOrWhiteSpace(revenueRecordId))
            {
                return Fail(RevenueOperationCode.InvalidRequest, "Revenue record ID is required.", preview);
            }

            string key = $"revenue-record:{revenueRecordId}";
            if (!preview && IsDuplicate(transactionId, key, out InstitutionalRevenueOperationResult duplicate))
            {
                return duplicate.With(revenueRecord: revenueRecordsById.TryGetValue(revenueRecordId, out InstitutionalRevenueRecordData live) ? live : null);
            }

            if (revenueRecordsById.ContainsKey(revenueRecordId))
            {
                return Fail(RevenueOperationCode.InvalidRequest, $"Revenue record '{revenueRecordId}' already exists.", preview);
            }

            InstitutionalRevenueRecordData record = new InstitutionalRevenueRecordData
            {
                revenueRecordId = revenueRecordId.Trim(),
                institutionId = payment.institutionId,
                revenueDefinitionId = payment.revenueDefinitionId,
                sourcePaymentId = payment.paymentId,
                economyTransactionId = payment.economyTransactionId,
                currencyId = payment.currencyId,
                units = payment.units,
                recognizedWorldTime = payment.worldTime,
                classification = classification ?? string.Empty,
                accessPolicyId = payment.accessPolicyId,
                provenance = payment.provenance
            };

            if (preview)
            {
                return InstitutionalRevenueOperationResult.Success("Institutional revenue preview succeeded.", before, before, preview: true).With(revenueRecord: record);
            }

            revenueRecordsById.Add(record.revenueRecordId, record);
            Revision++;
            Remember(transactionId, key, RevenueOperationCode.Succeeded);
            return InstitutionalRevenueOperationResult.Success("Institutional revenue recognized.", before, Revision).With(revenueRecord: record);
        }

        public InstitutionalRevenueOperationResult AllocateRevenue(string revenueRecordId, EconomyRuntime economy, string authorityId, string toAccountId, string transactionId, long units, double worldTime = 0d, string injectFailureStage = "", bool preview = false)
        {
            long before = Revision;
            if (!revenueRecordsById.TryGetValue(revenueRecordId ?? string.Empty, out InstitutionalRevenueRecordData record))
            {
                return Fail(RevenueOperationCode.MissingRevenueRecord, $"Revenue record '{revenueRecordId}' is missing.", preview);
            }

            if (!TryGetDefinition(record.revenueDefinitionId, out InstitutionalRevenueDefinition definition, out string failure))
            {
                return Fail(RevenueOperationCode.MissingDefinition, failure, preview);
            }

            if (!AuthorityPermits(authorityId, definition, new RevenueSubjectReferenceData { subjectId = record.institutionId, subjectKind = RevenueSubjectKind.Institution }, record.currencyId, worldTime, requireAssess: false, requireCollect: false, requireRefund: false, requireWaive: false, requireAudit: false, requireAllocate: true, out failure))
            {
                return Fail(RevenueOperationCode.Unauthorized, failure, preview);
            }

            long alreadyAllocated = allocationsById.Values.Where(item => string.Equals(item.revenueRecordId, record.revenueRecordId, StringComparison.Ordinal)).Sum(item => item.units);
            long allocatable = Math.Max(0L, record.units - alreadyAllocated);
            long amount = Math.Min(Math.Max(0L, units), allocatable);
            if (amount <= 0L)
            {
                return Fail(RevenueOperationCode.InvalidRequest, "Allocation amount must be positive and not exceed unallocated revenue.", preview);
            }

            string allocationId = $"allocation.{record.revenueRecordId}.{Sanitize(transactionId)}";
            string key = $"revenue-allocation:{record.revenueRecordId}:{toAccountId}:{units}";
            if (!preview && IsDuplicate(transactionId, key, out InstitutionalRevenueOperationResult duplicate))
            {
                return duplicate.With(allocation: allocationsById.TryGetValue(allocationId, out RevenueAllocationData live) ? live : null);
            }

            InstitutionalRevenueRuntimeSaveData revenueRollback = CreateSaveData();
            EconomyRuntimeSaveData economyRollback = economy?.CreateSaveData();
            try
            {
                if (economy == null)
                {
                    return Fail(RevenueOperationCode.MissingAccount, "Economy runtime is missing.", preview);
                }

                InstitutionalPaymentData payment = paymentsById.TryGetValue(record.sourcePaymentId, out InstitutionalPaymentData sourcePayment) ? sourcePayment : null;
                if (payment == null)
                {
                    return Fail(RevenueOperationCode.InvalidRequest, $"Revenue record '{record.revenueRecordId}' has no committed source payment.", preview);
                }

                EconomyOperationResult transfer = economy.Transfer(transactionId, payment.institutionAccountId, toAccountId, new MoneyAmount(record.currencyId, amount), EconomyTransactionKind.Transfer, actorId: authorityId, preview: preview);
                if (!transfer.Succeeded)
                {
                    return EconomyFailure(transfer, before, preview);
                }

                RevenueAllocationData allocation = new RevenueAllocationData
                {
                    allocationId = allocationId,
                    revenueRecordId = record.revenueRecordId,
                    fromAccountId = payment.institutionAccountId,
                    toAccountId = toAccountId,
                    currencyId = record.currencyId,
                    units = amount,
                    economyTransactionId = transfer.Transaction?.TransactionId ?? transactionId,
                    authorityId = authorityId,
                    worldTime = worldTime,
                    accessPolicyId = record.accessPolicyId,
                    provenance = $"authority:{authorityId}"
                };
                InstitutionalRevenueRecordData updated = record.Clone();
                updated.allocationIds = updated.allocationIds.Concat(new[] { allocation.allocationId }).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
                updated.revision++;

                if (preview)
                {
                    return InstitutionalRevenueOperationResult.Success("Revenue allocation preview succeeded.", before, before, preview: true).With(allocation: allocation, revenueRecord: updated, economyTransaction: transfer.Transaction?.Data);
                }

                if (FailAt(injectFailureStage, "after-economy-transfer"))
                {
                    throw new InvalidOperationException("Injected revenue allocation failure after economy transfer.");
                }

                allocationsById.Add(allocation.allocationId, allocation);
                revenueRecordsById[updated.revenueRecordId] = updated;
                Revision++;
                Remember(transactionId, key, RevenueOperationCode.Succeeded);
                return InstitutionalRevenueOperationResult.Success("Revenue allocated.", before, Revision).With(allocation: allocation, revenueRecord: updated, economyTransaction: transfer.Transaction?.Data);
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is OverflowException)
            {
                RestoreFromSaveData(revenueRollback, registry);
                if (economy != null && economyRollback != null)
                {
                    economy.RestoreFromSaveData(economyRollback, registry);
                }

                return InstitutionalRevenueOperationResult.Failure(exception is OverflowException ? RevenueOperationCode.ArithmeticOverflow : RevenueOperationCode.RolledBack, exception.Message, before);
            }
        }

        public InstitutionalRevenueOperationResult ApplyPenalty(string penaltyId, string obligationId, string authorityId, RevenueRatePolicyData policy, double worldTime, string sourceReferenceId, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            if (!obligationsById.TryGetValue(obligationId ?? string.Empty, out InstitutionalObligationData obligation))
            {
                return Fail(RevenueOperationCode.MissingObligation, $"Obligation '{obligationId}' is missing.", preview);
            }

            if (!TryGetDefinition(obligation.revenueDefinitionId, out InstitutionalRevenueDefinition definition, out string failure))
            {
                return Fail(RevenueOperationCode.MissingDefinition, failure, preview);
            }

            if (!AuthorityPermits(authorityId, definition, new RevenueSubjectReferenceData { subjectId = obligation.payerSubjectId }, obligation.currencyId, worldTime, requireAssess: true, requireCollect: false, requireRefund: false, requireWaive: false, requireAudit: false, requireAllocate: false, out failure))
            {
                return Fail(RevenueOperationCode.Unauthorized, failure, preview);
            }

            if (string.IsNullOrWhiteSpace(sourceReferenceId))
            {
                return Fail(RevenueOperationCode.InvalidRequest, "Penalty requires an explicit source reference.", preview);
            }

            string key = $"penalty:{obligationId}:{sourceReferenceId}";
            if (!preview && IsDuplicate(transactionId, key, out InstitutionalRevenueOperationResult duplicate))
            {
                return duplicate.With(penalty: penaltiesById.Values.FirstOrDefault(item => string.Equals(item.obligationId, obligationId, StringComparison.Ordinal) && string.Equals(item.sourceReferenceId, sourceReferenceId, StringComparison.Ordinal)));
            }

            if (penaltiesById.Values.Any(item => string.Equals(item.obligationId, obligationId, StringComparison.Ordinal) && string.Equals(item.sourceReferenceId, sourceReferenceId, StringComparison.Ordinal)))
            {
                return Fail(RevenueOperationCode.Duplicate, "Penalty boundary was already applied.", preview);
            }

            long units = CalculateCharge(policy ?? definition.RatePolicy, obligation.OutstandingUnits);
            if (units <= 0L)
            {
                return Fail(RevenueOperationCode.InvalidRequest, "Penalty amount must be positive.", preview);
            }

            RevenuePenaltyData penalty = new RevenuePenaltyData
            {
                penaltyId = string.IsNullOrWhiteSpace(penaltyId) ? $"penalty.{obligationId}.{Sanitize(sourceReferenceId)}" : penaltyId.Trim(),
                obligationId = obligationId,
                assessmentId = obligation.assessmentId,
                policyId = policy?.ratePolicyId ?? definition.RatePolicy.ratePolicyId,
                sourceReferenceId = sourceReferenceId,
                currencyId = obligation.currencyId,
                units = units,
                appliedWorldTime = worldTime,
                accessPolicyId = obligation.accessPolicyId,
                provenance = $"authority:{authorityId}"
            };
            InstitutionalObligationData updated = obligation.Clone();
            updated.amountDueUnits = checked(updated.amountDueUnits + units);
            updated.state = InstitutionalObligationState.Overdue;
            updated.revision++;

            if (preview)
            {
                return InstitutionalRevenueOperationResult.Success("Penalty preview succeeded.", before, before, preview: true).With(penalty: penalty, obligation: updated);
            }

            penaltiesById.Add(penalty.penaltyId, penalty);
            obligationsById[updated.obligationId] = updated;
            if (assessmentsById.TryGetValue(updated.assessmentId, out InstitutionalAssessmentData assessment))
            {
                InstitutionalAssessmentData updatedAssessment = assessment.Clone();
                updatedAssessment.penaltyUnits = checked(updatedAssessment.penaltyUnits + units);
                updatedAssessment.amountDueUnits = checked(updatedAssessment.amountDueUnits + units);
                updatedAssessment.state = RevenueAssessmentState.Overdue;
                updatedAssessment.revision++;
                assessmentsById[updatedAssessment.assessmentId] = updatedAssessment;
            }

            Revision++;
            Remember(transactionId, key, RevenueOperationCode.Succeeded);
            return InstitutionalRevenueOperationResult.Success("Penalty applied.", before, Revision).With(penalty: penalty, obligation: updated);
        }

        public InstitutionalRevenueOperationResult WaiveObligation(string waiverId, string obligationId, string authorityId, long units, double worldTime = 0d, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            if (!obligationsById.TryGetValue(obligationId ?? string.Empty, out InstitutionalObligationData obligation))
            {
                return Fail(RevenueOperationCode.MissingObligation, $"Obligation '{obligationId}' is missing.", preview);
            }

            if (!TryGetDefinition(obligation.revenueDefinitionId, out InstitutionalRevenueDefinition definition, out string failure))
            {
                return Fail(RevenueOperationCode.MissingDefinition, failure, preview);
            }

            if (!AuthorityPermits(authorityId, definition, new RevenueSubjectReferenceData { subjectId = obligation.payerSubjectId }, obligation.currencyId, worldTime, requireAssess: false, requireCollect: false, requireRefund: false, requireWaive: true, requireAudit: false, requireAllocate: false, out failure))
            {
                return Fail(RevenueOperationCode.Unauthorized, failure, preview);
            }

            long amount = Math.Min(Math.Max(0L, units), obligation.OutstandingUnits);
            if (amount <= 0L)
            {
                return Fail(RevenueOperationCode.InvalidRequest, "Waiver amount must be positive and not exceed outstanding amount.", preview);
            }

            string key = $"waiver:{obligationId}:{waiverId}:{amount}";
            if (!preview && IsDuplicate(transactionId, key, out InstitutionalRevenueOperationResult duplicate))
            {
                return duplicate.With(waiver: waiversById.TryGetValue(waiverId, out RevenueWaiverData live) ? live : null);
            }

            RevenueWaiverData waiver = new RevenueWaiverData
            {
                waiverId = string.IsNullOrWhiteSpace(waiverId) ? $"waiver.{obligationId}.{Sanitize(transactionId)}" : waiverId.Trim(),
                obligationId = obligationId,
                assessmentId = obligation.assessmentId,
                authorityId = authorityId,
                units = amount,
                worldTime = worldTime,
                accessPolicyId = obligation.accessPolicyId,
                provenance = $"authority:{authorityId}"
            };
            InstitutionalObligationData updated = obligation.Clone();
            updated.amountWaivedUnits = checked(updated.amountWaivedUnits + amount);
            updated.waiverIds = updated.waiverIds.Concat(new[] { waiver.waiverId }).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            updated.state = updated.OutstandingUnits == 0L ? InstitutionalObligationState.Waived : InstitutionalObligationState.PartiallyPaid;
            updated.revision++;

            if (preview)
            {
                return InstitutionalRevenueOperationResult.Success("Waiver preview succeeded.", before, before, preview: true).With(waiver: waiver, obligation: updated);
            }

            waiversById.Add(waiver.waiverId, waiver);
            obligationsById[updated.obligationId] = updated;
            Revision++;
            Remember(transactionId, key, RevenueOperationCode.Succeeded);
            return InstitutionalRevenueOperationResult.Success("Obligation waived.", before, Revision).With(waiver: waiver, obligation: updated);
        }

        public InstitutionalRevenueOperationResult RefundPayment(string refundId, string paymentId, EconomyRuntime economy, string authorityId, long units, double worldTime = 0d, string reason = "", string transactionId = "", string injectFailureStage = "", bool preview = false)
        {
            long before = Revision;
            if (!paymentsById.TryGetValue(paymentId ?? string.Empty, out InstitutionalPaymentData payment))
            {
                return Fail(RevenueOperationCode.InvalidRequest, $"Payment '{paymentId}' is missing.", preview);
            }

            if (!TryGetDefinition(payment.revenueDefinitionId, out InstitutionalRevenueDefinition definition, out string failure))
            {
                return Fail(RevenueOperationCode.MissingDefinition, failure, preview);
            }

            if (!definition.RefundsAllowed)
            {
                return Fail(RevenueOperationCode.InvalidRequest, $"Revenue definition '{definition.Id}' does not allow refunds.", preview);
            }

            if (!AuthorityPermits(authorityId, definition, new RevenueSubjectReferenceData { subjectId = payment.institutionId }, payment.currencyId, worldTime, requireAssess: false, requireCollect: false, requireRefund: true, requireWaive: false, requireAudit: false, requireAllocate: false, out failure))
            {
                return Fail(RevenueOperationCode.Unauthorized, failure, preview);
            }

            long alreadyRefunded = refundsById.Values.Where(item => string.Equals(item.originalPaymentId, payment.paymentId, StringComparison.Ordinal)).Sum(item => item.units);
            long refundable = Math.Max(0L, payment.units - alreadyRefunded);
            long amount = Math.Min(Math.Max(0L, units), refundable);
            if (amount <= 0L)
            {
                return Fail(RevenueOperationCode.InvalidRequest, "Refund amount must be positive and not exceed eligible payment amount.", preview);
            }

            string cleanRefundId = string.IsNullOrWhiteSpace(refundId) ? $"refund.{payment.paymentId}.{Sanitize(transactionId)}" : refundId.Trim();
            string key = $"refund:{payment.paymentId}:{amount}";
            if (!preview && IsDuplicate(transactionId, key, out InstitutionalRevenueOperationResult duplicate))
            {
                return duplicate.With(refund: refundsById.TryGetValue(cleanRefundId, out RevenueRefundData live) ? live : null);
            }

            InstitutionalRevenueRuntimeSaveData revenueRollback = CreateSaveData();
            EconomyRuntimeSaveData economyRollback = economy?.CreateSaveData();
            try
            {
                if (economy == null)
                {
                    return Fail(RevenueOperationCode.MissingAccount, "Economy runtime is missing.", preview);
                }

                EconomyOperationResult transfer = economy.Transfer(transactionId, payment.institutionAccountId, payment.payerAccountId, new MoneyAmount(payment.currencyId, amount), EconomyTransactionKind.Refund, actorId: authorityId, preview: preview);
                if (!transfer.Succeeded)
                {
                    return EconomyFailure(transfer, before, preview);
                }

                RevenueRefundData refund = new RevenueRefundData
                {
                    refundId = cleanRefundId,
                    obligationId = payment.obligationId,
                    assessmentId = payment.assessmentId,
                    originalPaymentId = payment.paymentId,
                    economyTransactionId = transfer.Transaction?.TransactionId ?? transactionId,
                    authorityId = authorityId,
                    fromAccountId = payment.institutionAccountId,
                    toAccountId = payment.payerAccountId,
                    currencyId = payment.currencyId,
                    units = amount,
                    worldTime = worldTime,
                    reason = reason ?? string.Empty,
                    accessPolicyId = payment.accessPolicyId,
                    provenance = $"authority:{authorityId}"
                };

                if (preview)
                {
                    return InstitutionalRevenueOperationResult.Success("Refund preview succeeded.", before, before, preview: true).With(refund: refund, economyTransaction: transfer.Transaction?.Data);
                }

                if (FailAt(injectFailureStage, "after-economy-transfer"))
                {
                    throw new InvalidOperationException("Injected refund failure after economy transfer.");
                }

                refundsById.Add(refund.refundId, refund);
                if (obligationsById.TryGetValue(payment.obligationId, out InstitutionalObligationData obligation))
                {
                    InstitutionalObligationData updated = obligation.Clone();
                    updated.refundIds = updated.refundIds.Concat(new[] { refund.refundId }).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
                    updated.state = InstitutionalObligationState.Refunded;
                    updated.revision++;
                    obligationsById[updated.obligationId] = updated;
                }

                Revision++;
                Remember(transactionId, key, RevenueOperationCode.Succeeded);
                return InstitutionalRevenueOperationResult.Success("Refund issued.", before, Revision).With(refund: refund, economyTransaction: transfer.Transaction?.Data);
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is OverflowException)
            {
                RestoreFromSaveData(revenueRollback, registry);
                if (economy != null && economyRollback != null)
                {
                    economy.RestoreFromSaveData(economyRollback, registry);
                }

                return InstitutionalRevenueOperationResult.Failure(exception is OverflowException ? RevenueOperationCode.ArithmeticOverflow : RevenueOperationCode.RolledBack, exception.Message, before);
            }
        }

        public InstitutionalRevenueOperationResult SubmitFiling(RevenueFilingData filing, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            if (filing == null || string.IsNullOrWhiteSpace(filing.filingId) || string.IsNullOrWhiteSpace(filing.revenueDefinitionId) || string.IsNullOrWhiteSpace(filing.reportingSubjectId))
            {
                return Fail(RevenueOperationCode.InvalidRequest, "Filing ID, revenue definition ID, and reporting subject are required.", preview);
            }

            RevenueFilingData clean = filing.Clone();
            string key = $"filing:{clean.filingId}";
            if (!preview && IsDuplicate(transactionId, key, out InstitutionalRevenueOperationResult duplicate))
            {
                return duplicate.With(filing: filingsById.TryGetValue(clean.filingId, out RevenueFilingData live) ? live : clean);
            }

            if (filingsById.ContainsKey(clean.filingId))
            {
                return Fail(RevenueOperationCode.Immutable, $"Filing '{clean.filingId}' already exists. Submit a correction instead.", preview);
            }

            if (preview)
            {
                return InstitutionalRevenueOperationResult.Success("Filing preview succeeded.", before, before, preview: true).With(filing: clean);
            }

            filingsById.Add(clean.filingId, clean);
            Revision++;
            Remember(transactionId, key, RevenueOperationCode.Succeeded);
            return InstitutionalRevenueOperationResult.Success("Filing submitted.", before, Revision).With(filing: clean);
        }

        public InstitutionalRevenueOperationResult AuditFiling(string findingId, string filingId, string authorityId, RevenueAuditFindingKind kind, string sourceReferenceId, string message, double worldTime = 0d, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            if (!filingsById.TryGetValue(filingId ?? string.Empty, out RevenueFilingData filing))
            {
                return Fail(RevenueOperationCode.InvalidRequest, $"Filing '{filingId}' is missing.", preview);
            }

            if (!TryGetDefinition(filing.revenueDefinitionId, out InstitutionalRevenueDefinition definition, out string failure))
            {
                return Fail(RevenueOperationCode.MissingDefinition, failure, preview);
            }

            if (!AuthorityPermits(authorityId, definition, new RevenueSubjectReferenceData { subjectId = filing.reportingSubjectId }, definition.CurrencyId, worldTime, requireAssess: false, requireCollect: false, requireRefund: false, requireWaive: false, requireAudit: true, requireAllocate: false, out failure))
            {
                return Fail(RevenueOperationCode.Unauthorized, failure, preview);
            }

            RevenueAuditFindingData finding = new RevenueAuditFindingData
            {
                findingId = string.IsNullOrWhiteSpace(findingId) ? $"audit.{filingId}.{Sanitize(transactionId)}" : findingId.Trim(),
                filingId = filingId,
                findingKind = kind,
                sourceReferenceId = sourceReferenceId ?? string.Empty,
                message = message ?? string.Empty,
                worldTime = worldTime,
                accessPolicyId = filing.accessPolicyId,
                provenance = $"authority:{authorityId}"
            };
            string key = $"audit:{finding.findingId}";
            if (!preview && IsDuplicate(transactionId, key, out InstitutionalRevenueOperationResult duplicate))
            {
                return duplicate.With(auditFinding: auditFindingsById.TryGetValue(finding.findingId, out RevenueAuditFindingData live) ? live : finding);
            }

            if (preview)
            {
                return InstitutionalRevenueOperationResult.Success("Audit preview succeeded.", before, before, preview: true).With(auditFinding: finding);
            }

            auditFindingsById.Add(finding.findingId, finding);
            Revision++;
            Remember(transactionId, key, RevenueOperationCode.Succeeded);
            return InstitutionalRevenueOperationResult.Success("Audit finding recorded.", before, Revision).With(auditFinding: finding);
        }

        public InstitutionalRevenueOperationResult GenerateStatement(string statementId, string subjectId, string currencyId, double worldTime = 0d, bool preview = false)
        {
            long before = Revision;
            if (string.IsNullOrWhiteSpace(statementId) || string.IsNullOrWhiteSpace(subjectId) || string.IsNullOrWhiteSpace(currencyId))
            {
                return Fail(RevenueOperationCode.InvalidRequest, "Statement ID, subject ID, and currency are required.", preview);
            }

            InstitutionalObligationData[] obligations = obligationsById.Values
                .Where(item => string.Equals(item.payerSubjectId, subjectId, StringComparison.Ordinal) && string.Equals(item.currencyId, currencyId, StringComparison.Ordinal))
                .OrderBy(item => item.obligationId, StringComparer.Ordinal)
                .ToArray();
            InstitutionalPaymentData[] payments = paymentsById.Values
                .Where(item => obligations.Any(obligation => string.Equals(obligation.obligationId, item.obligationId, StringComparison.Ordinal)))
                .OrderBy(item => item.paymentId, StringComparer.Ordinal)
                .ToArray();
            RevenueStatementData statement = new RevenueStatementData
            {
                statementId = statementId.Trim(),
                subjectId = subjectId.Trim(),
                assessmentIds = obligations.Select(item => item.assessmentId).ToArray(),
                obligationIds = obligations.Select(item => item.obligationId).ToArray(),
                paymentIds = payments.Select(item => item.paymentId).ToArray(),
                arrearsObligationIds = obligations.Where(item => item.state == InstitutionalObligationState.Overdue).Select(item => item.obligationId).ToArray(),
                currencyId = currencyId.Trim(),
                totalDueUnits = obligations.Sum(item => item.amountDueUnits),
                totalPaidUnits = obligations.Sum(item => item.amountPaidUnits),
                totalOutstandingUnits = obligations.Sum(item => item.OutstandingUnits),
                generatedWorldTime = worldTime
            };

            if (preview)
            {
                return InstitutionalRevenueOperationResult.Success("Statement preview succeeded.", before, before, preview: true).With(statement: statement);
            }

            statementsById[statement.statementId] = statement;
            Revision++;
            return InstitutionalRevenueOperationResult.Success("Statement generated.", before, Revision).With(statement: statement);
        }

        public InformationAccessProjection<InstitutionalAssessmentData> GetAssessmentProjection(string assessmentId, InformationAccessRuntime accessRuntime, InformationAccessContext accessContext, string policyId = "")
        {
            if (!assessmentsById.TryGetValue(assessmentId ?? string.Empty, out InstitutionalAssessmentData assessment))
            {
                return new InformationAccessProjection<InstitutionalAssessmentData>(null, null, new Dictionary<string, InformationRedactionState>(), string.Empty, $"Assessment '{assessmentId}' was not found.");
            }

            InstitutionalAssessmentData projected = assessment.Clone();
            if (accessRuntime == null)
            {
                RedactAssessment(projected, null, null);
                return new InformationAccessProjection<InstitutionalAssessmentData>(projected, null, new Dictionary<string, InformationRedactionState>(), string.Empty, "Information access runtime is missing.");
            }

            InformationAccessContext context = InformationAccessProjectionUtility.BuildContext(accessContext, assessment.CreateInformationSubject(), InformationAccessMode.Inspect, InformationAccessPurpose.Gameplay, ProtectedDetails, policyId);
            InformationAccessDecision decision = accessRuntime.EvaluateAccess(context);
            Dictionary<string, InformationRedactionState> details = BuildDetailStates(decision, ProtectedDetails);
            RedactAssessment(projected, decision, details);
            string visibleId = InformationAccessProjectionUtility.IsVisible(details, "detail.subject") ? projected.assessmentId : string.Empty;
            return new InformationAccessProjection<InstitutionalAssessmentData>(projected, decision, details, visibleId, decision.VisibleReason);
        }

        public InstitutionalRevenueRuntimeSaveData CreateSaveData()
        {
            return new InstitutionalRevenueRuntimeSaveData
            {
                schemaVersion = CurrentSaveSchemaVersion,
                worldId = worldId,
                revision = Revision,
                authorities = authoritiesById.Values.OrderBy(item => item.authorityId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                accountAssignments = accountAssignmentsById.Values.OrderBy(item => item.assignmentId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                taxableEvents = taxableEventsById.Values.OrderBy(item => item.taxableEventId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                baseCalculations = baseCalculationsById.Values.OrderBy(item => item.baseCalculationId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                exemptions = exemptionsById.Values.OrderBy(item => item.adjustmentId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                deductions = deductionsById.Values.OrderBy(item => item.adjustmentId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                credits = creditsById.Values.OrderBy(item => item.adjustmentId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                periods = periodsById.Values.OrderBy(item => item.periodId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                assessments = assessmentsById.Values.OrderBy(item => item.assessmentId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                obligations = obligationsById.Values.OrderBy(item => item.obligationId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                payments = paymentsById.Values.OrderBy(item => item.paymentId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                withholdings = withholdingsById.Values.OrderBy(item => item.withholdingId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                revenueRecords = revenueRecordsById.Values.OrderBy(item => item.revenueRecordId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                allocations = allocationsById.Values.OrderBy(item => item.allocationId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                waivers = waiversById.Values.OrderBy(item => item.waiverId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                refunds = refundsById.Values.OrderBy(item => item.refundId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                penalties = penaltiesById.Values.OrderBy(item => item.penaltyId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                filings = filingsById.Values.OrderBy(item => item.filingId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                auditFindings = auditFindingsById.Values.OrderBy(item => item.findingId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                statements = statementsById.Values.OrderBy(item => item.statementId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                receipts = receiptsById.Values.OrderBy(item => item.receiptId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                processedTransactions = processedByTransactionId.Values.OrderBy(item => item.transactionId, StringComparer.Ordinal).Select(item => item.Clone()).ToList()
            };
        }

        public InstitutionalRevenueOperationResult RestoreFromSaveData(InstitutionalRevenueRuntimeSaveData saveData, DefinitionRegistry definitionRegistry = null)
        {
            long before = Revision;
            if (!ValidateSaveData(saveData, definitionRegistry ?? registry, out string failure))
            {
                return InstitutionalRevenueOperationResult.Failure(RevenueOperationCode.PersistenceRejected, failure, before);
            }

            authoritiesById.Clear();
            accountAssignmentsById.Clear();
            taxableEventsById.Clear();
            baseCalculationsById.Clear();
            exemptionsById.Clear();
            deductionsById.Clear();
            creditsById.Clear();
            periodsById.Clear();
            assessmentsById.Clear();
            obligationsById.Clear();
            paymentsById.Clear();
            withholdingsById.Clear();
            revenueRecordsById.Clear();
            allocationsById.Clear();
            waiversById.Clear();
            refundsById.Clear();
            penaltiesById.Clear();
            filingsById.Clear();
            auditFindingsById.Clear();
            statementsById.Clear();
            receiptsById.Clear();
            processedByTransactionId.Clear();
            exclusiveAssessmentKeys.Clear();

            foreach (InstitutionalRevenueAuthorityData item in saveData.authorities.OrderBy(item => item.authorityId, StringComparer.Ordinal)) authoritiesById.Add(item.authorityId, item.Clone());
            foreach (InstitutionalRevenueAccountAssignmentData item in saveData.accountAssignments.OrderBy(item => item.assignmentId, StringComparer.Ordinal)) accountAssignmentsById.Add(item.assignmentId, item.Clone());
            foreach (TaxableEventData item in saveData.taxableEvents.OrderBy(item => item.taxableEventId, StringComparer.Ordinal)) taxableEventsById.Add(item.taxableEventId, item.Clone());
            foreach (TaxBaseCalculationData item in saveData.baseCalculations.OrderBy(item => item.baseCalculationId, StringComparer.Ordinal)) baseCalculationsById.Add(item.baseCalculationId, item.Clone());
            foreach (RevenueAdjustmentData item in saveData.exemptions.OrderBy(item => item.adjustmentId, StringComparer.Ordinal)) exemptionsById.Add(item.adjustmentId, item.Clone());
            foreach (RevenueAdjustmentData item in saveData.deductions.OrderBy(item => item.adjustmentId, StringComparer.Ordinal)) deductionsById.Add(item.adjustmentId, item.Clone());
            foreach (RevenueAdjustmentData item in saveData.credits.OrderBy(item => item.adjustmentId, StringComparer.Ordinal)) creditsById.Add(item.adjustmentId, item.Clone());
            foreach (AssessmentPeriodData item in saveData.periods.OrderBy(item => item.periodId, StringComparer.Ordinal)) periodsById.Add(item.periodId, item.Clone());
            foreach (InstitutionalAssessmentData item in saveData.assessments.OrderBy(item => item.assessmentId, StringComparer.Ordinal))
            {
                assessmentsById.Add(item.assessmentId, item.Clone());
                foreach (string eventId in item.taxableEventIds ?? Array.Empty<string>())
                {
                    if (taxableEventsById.TryGetValue(eventId, out TaxableEventData taxableEvent) && taxableEvent.exclusiveAssessment)
                    {
                        exclusiveAssessmentKeys.Add(ExclusiveAssessmentKey(item.revenueDefinitionId, item.periodId, eventId));
                    }
                }
            }
            foreach (InstitutionalObligationData item in saveData.obligations.OrderBy(item => item.obligationId, StringComparer.Ordinal)) obligationsById.Add(item.obligationId, item.Clone());
            foreach (InstitutionalPaymentData item in saveData.payments.OrderBy(item => item.paymentId, StringComparer.Ordinal)) paymentsById.Add(item.paymentId, item.Clone());
            foreach (WithholdingRecordData item in saveData.withholdings.OrderBy(item => item.withholdingId, StringComparer.Ordinal)) withholdingsById.Add(item.withholdingId, item.Clone());
            foreach (InstitutionalRevenueRecordData item in saveData.revenueRecords.OrderBy(item => item.revenueRecordId, StringComparer.Ordinal)) revenueRecordsById.Add(item.revenueRecordId, item.Clone());
            foreach (RevenueAllocationData item in saveData.allocations.OrderBy(item => item.allocationId, StringComparer.Ordinal)) allocationsById.Add(item.allocationId, item.Clone());
            foreach (RevenueWaiverData item in saveData.waivers.OrderBy(item => item.waiverId, StringComparer.Ordinal)) waiversById.Add(item.waiverId, item.Clone());
            foreach (RevenueRefundData item in saveData.refunds.OrderBy(item => item.refundId, StringComparer.Ordinal)) refundsById.Add(item.refundId, item.Clone());
            foreach (RevenuePenaltyData item in saveData.penalties.OrderBy(item => item.penaltyId, StringComparer.Ordinal)) penaltiesById.Add(item.penaltyId, item.Clone());
            foreach (RevenueFilingData item in saveData.filings.OrderBy(item => item.filingId, StringComparer.Ordinal)) filingsById.Add(item.filingId, item.Clone());
            foreach (RevenueAuditFindingData item in saveData.auditFindings.OrderBy(item => item.findingId, StringComparer.Ordinal)) auditFindingsById.Add(item.findingId, item.Clone());
            foreach (RevenueStatementData item in saveData.statements.OrderBy(item => item.statementId, StringComparer.Ordinal)) statementsById.Add(item.statementId, item.Clone());
            foreach (RevenueReceiptData item in saveData.receipts.OrderBy(item => item.receiptId, StringComparer.Ordinal)) receiptsById.Add(item.receiptId, item.Clone());
            foreach (RevenueProcessedTransactionData item in saveData.processedTransactions.OrderBy(item => item.transactionId, StringComparer.Ordinal)) processedByTransactionId.Add(item.transactionId, item.Clone());

            worldId = saveData.worldId ?? string.Empty;
            Revision = saveData.revision;
            return InstitutionalRevenueOperationResult.Success("Institutional revenue runtime restored.", before, Revision);
        }

        public static bool ValidateSaveData(InstitutionalRevenueRuntimeSaveData saveData, DefinitionRegistry registry, out string failure)
        {
            failure = string.Empty;
            if (saveData == null)
            {
                failure = "Institutional revenue payload is missing.";
                return false;
            }

            if (saveData.schemaVersion != CurrentSaveSchemaVersion)
            {
                failure = $"Unsupported institutional revenue schema version {saveData.schemaVersion}.";
                return false;
            }

            if (!ValidateUnique(saveData.authorities, item => item.authorityId, "authority", out failure)
                || !ValidateUnique(saveData.accountAssignments, item => item.assignmentId, "account assignment", out failure)
                || !ValidateUnique(saveData.taxableEvents, item => item.taxableEventId, "taxable event", out failure)
                || !ValidateUnique(saveData.baseCalculations, item => item.baseCalculationId, "base calculation", out failure)
                || !ValidateUnique(saveData.assessments, item => item.assessmentId, "assessment", out failure)
                || !ValidateUnique(saveData.obligations, item => item.obligationId, "obligation", out failure)
                || !ValidateUnique(saveData.payments, item => item.paymentId, "payment", out failure)
                || !ValidateUnique(saveData.withholdings, item => item.withholdingId, "withholding", out failure)
                || !ValidateUnique(saveData.revenueRecords, item => item.revenueRecordId, "revenue record", out failure)
                || !ValidateUnique(saveData.allocations, item => item.allocationId, "allocation", out failure)
                || !ValidateUnique(saveData.refunds, item => item.refundId, "refund", out failure)
                || !ValidateUnique(saveData.filings, item => item.filingId, "filing", out failure))
            {
                return false;
            }

            HashSet<string> definitions = registry == null
                ? new HashSet<string>(StringComparer.Ordinal)
                : new HashSet<string>(registry.DefinitionsById.Values.OfType<InstitutionalRevenueDefinition>().Select(item => item.Id), StringComparer.Ordinal);
            foreach (TaxableEventData taxableEvent in saveData.taxableEvents ?? new List<TaxableEventData>())
            {
                if (string.IsNullOrWhiteSpace(taxableEvent.revenueDefinitionId) || (registry != null && !definitions.Contains(taxableEvent.revenueDefinitionId)))
                {
                    failure = $"Taxable event '{taxableEvent?.taxableEventId}' references missing revenue definition '{taxableEvent?.revenueDefinitionId}'.";
                    return false;
                }
            }

            HashSet<string> eventIds = new HashSet<string>((saveData.taxableEvents ?? new List<TaxableEventData>()).Select(item => item.taxableEventId), StringComparer.Ordinal);
            HashSet<string> baseIds = new HashSet<string>((saveData.baseCalculations ?? new List<TaxBaseCalculationData>()).Select(item => item.baseCalculationId), StringComparer.Ordinal);
            HashSet<string> assessmentIds = new HashSet<string>((saveData.assessments ?? new List<InstitutionalAssessmentData>()).Select(item => item.assessmentId), StringComparer.Ordinal);
            HashSet<string> obligationIds = new HashSet<string>((saveData.obligations ?? new List<InstitutionalObligationData>()).Select(item => item.obligationId), StringComparer.Ordinal);
            HashSet<string> paymentIds = new HashSet<string>((saveData.payments ?? new List<InstitutionalPaymentData>()).Select(item => item.paymentId), StringComparer.Ordinal);
            HashSet<string> revenueIds = new HashSet<string>((saveData.revenueRecords ?? new List<InstitutionalRevenueRecordData>()).Select(item => item.revenueRecordId), StringComparer.Ordinal);

            foreach (InstitutionalAssessmentData assessment in saveData.assessments ?? new List<InstitutionalAssessmentData>())
            {
                if (registry != null && !definitions.Contains(assessment.revenueDefinitionId))
                {
                    failure = $"Assessment '{assessment.assessmentId}' references missing revenue definition '{assessment.revenueDefinitionId}'.";
                    return false;
                }

                if ((assessment.taxableEventIds ?? Array.Empty<string>()).Any(id => !eventIds.Contains(id)))
                {
                    failure = $"Assessment '{assessment.assessmentId}' references a missing taxable event.";
                    return false;
                }

                if ((assessment.baseCalculationIds ?? Array.Empty<string>()).Any(id => !baseIds.Contains(id)))
                {
                    failure = $"Assessment '{assessment.assessmentId}' references a missing base calculation.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(assessment.obligationId) && !obligationIds.Contains(assessment.obligationId))
                {
                    failure = $"Assessment '{assessment.assessmentId}' references missing obligation '{assessment.obligationId}'.";
                    return false;
                }
            }

            foreach (InstitutionalObligationData obligation in saveData.obligations ?? new List<InstitutionalObligationData>())
            {
                if (!assessmentIds.Contains(obligation.assessmentId))
                {
                    failure = $"Obligation '{obligation.obligationId}' references missing assessment '{obligation.assessmentId}'.";
                    return false;
                }

                if ((obligation.paymentIds ?? Array.Empty<string>()).Any(id => !paymentIds.Contains(id)))
                {
                    failure = $"Obligation '{obligation.obligationId}' references a missing payment.";
                    return false;
                }
            }

            foreach (InstitutionalPaymentData payment in saveData.payments ?? new List<InstitutionalPaymentData>())
            {
                if (!obligationIds.Contains(payment.obligationId))
                {
                    failure = $"Payment '{payment.paymentId}' references missing obligation '{payment.obligationId}'.";
                    return false;
                }
            }

            foreach (InstitutionalRevenueRecordData record in saveData.revenueRecords ?? new List<InstitutionalRevenueRecordData>())
            {
                if (!paymentIds.Contains(record.sourcePaymentId))
                {
                    failure = $"Revenue record '{record.revenueRecordId}' references missing payment '{record.sourcePaymentId}'.";
                    return false;
                }
            }

            foreach (RevenueAllocationData allocation in saveData.allocations ?? new List<RevenueAllocationData>())
            {
                if (!revenueIds.Contains(allocation.revenueRecordId))
                {
                    failure = $"Revenue allocation '{allocation.allocationId}' references missing revenue record '{allocation.revenueRecordId}'.";
                    return false;
                }
            }

            return true;
        }

        public static long CalculateCharge(RevenueRatePolicyData policy, long baseUnits)
        {
            if (!InstitutionalRevenueDefinition.ValidateRatePolicy(policy, out string failure))
            {
                throw new InvalidOperationException(failure);
            }

            long safeBase = Math.Max(0L, baseUnits);
            long result = policy.rateKind switch
            {
                RevenueRateKind.FixedAmount => policy.fixedUnits,
                RevenueRateKind.FlatProportional => ApplyRatio(safeBase, policy.rate, policy.roundingMode),
                RevenueRateKind.PerUnit => checked(safeBase * policy.perUnitUnits),
                RevenueRateKind.ProgressiveBracket => CalculateBracketed(policy, safeBase),
                RevenueRateKind.ThresholdCharge => safeBase >= policy.thresholdUnits ? CalculateBracketed(policy, safeBase) : 0L,
                RevenueRateKind.TieredFixedCharge => CalculateBracketed(policy, safeBase),
                RevenueRateKind.MinimumCharge => Math.Max(policy.minimumUnits, ApplyRatio(safeBase, policy.rate, policy.roundingMode)),
                RevenueRateKind.MaximumCharge => policy.maximumUnits < 0L ? ApplyRatio(safeBase, policy.rate, policy.roundingMode) : Math.Min(policy.maximumUnits, ApplyRatio(safeBase, policy.rate, policy.roundingMode)),
                RevenueRateKind.CappedProportionalCharge => policy.maximumUnits < 0L ? ApplyRatio(safeBase, policy.rate, policy.roundingMode) : Math.Min(policy.maximumUnits, ApplyRatio(safeBase, policy.rate, policy.roundingMode)),
                RevenueRateKind.PercentagePlusFixedAmount => checked(policy.fixedUnits + ApplyRatio(safeBase, policy.rate, policy.roundingMode)),
                RevenueRateKind.ValueBand => CalculateBracketed(policy, safeBase),
                RevenueRateKind.QuantityBand => CalculateBracketed(policy, safeBase),
                _ => throw new InvalidOperationException($"Unsupported revenue rate kind '{policy.rateKind}'.")
            };

            if (policy.minimumUnits > 0L)
            {
                result = Math.Max(policy.minimumUnits, result);
            }

            if (policy.maximumUnits >= 0L)
            {
                result = Math.Min(policy.maximumUnits, result);
            }

            return Math.Max(0L, result);
        }

        private InstitutionalRevenueOperationResult RegisterAdjustment(RevenueAdjustmentData adjustment, Dictionary<string, RevenueAdjustmentData> target, string label, string authorityId, bool requireWaive, string transactionId, bool preview)
        {
            long before = Revision;
            if (adjustment == null || string.IsNullOrWhiteSpace(adjustment.adjustmentId) || string.IsNullOrWhiteSpace(adjustment.revenueDefinitionId) || string.IsNullOrWhiteSpace(adjustment.subjectId))
            {
                return Fail(RevenueOperationCode.InvalidRequest, $"{label} ID, revenue definition, and subject are required.", preview);
            }

            if (!TryGetDefinition(adjustment.revenueDefinitionId, out InstitutionalRevenueDefinition definition, out string failure))
            {
                return Fail(RevenueOperationCode.MissingDefinition, failure, preview);
            }

            if (!AuthorityPermits(authorityId, definition, new RevenueSubjectReferenceData { subjectId = adjustment.subjectId }, definition.CurrencyId, adjustment.effectiveStartWorldTime, requireAssess: false, requireCollect: false, requireRefund: false, requireWaive: requireWaive, requireAudit: false, requireAllocate: false, out failure))
            {
                return Fail(RevenueOperationCode.Unauthorized, failure, preview);
            }

            RevenueAdjustmentData clean = adjustment.Clone();
            string key = $"{label}:{clean.adjustmentId}";
            if (!preview && IsDuplicate(transactionId, key, out InstitutionalRevenueOperationResult duplicate))
            {
                return duplicate;
            }

            if (target.ContainsKey(clean.adjustmentId))
            {
                return Fail(RevenueOperationCode.InvalidRequest, $"{label} '{clean.adjustmentId}' already exists.", preview);
            }

            if (preview)
            {
                return InstitutionalRevenueOperationResult.Success($"{label} preview succeeded.", before, before, preview: true);
            }

            target.Add(clean.adjustmentId, clean);
            Revision++;
            Remember(transactionId, key, RevenueOperationCode.Succeeded);
            return InstitutionalRevenueOperationResult.Success($"{label} registered.", before, Revision);
        }

        private InstitutionalRevenueOperationResult TransferToInstitution(string obligationId, EconomyRuntime economy, string transactionId, long units, double worldTime, string injectFailureStage, bool preview, bool remittance, string withholdingId)
        {
            long before = Revision;
            if (!obligationsById.TryGetValue(obligationId ?? string.Empty, out InstitutionalObligationData obligation))
            {
                return Fail(RevenueOperationCode.MissingObligation, $"Obligation '{obligationId}' is missing.", preview);
            }

            if (!TryGetDefinition(obligation.revenueDefinitionId, out InstitutionalRevenueDefinition definition, out string failure))
            {
                return Fail(RevenueOperationCode.MissingDefinition, failure, preview);
            }

            if (!AuthorityPermits(FindCollectAuthority(definition, obligation.institutionId, obligation.currencyId), definition, new RevenueSubjectReferenceData { subjectId = obligation.payerSubjectId }, obligation.currencyId, worldTime, requireAssess: false, requireCollect: true, requireRefund: false, requireWaive: false, requireAudit: false, requireAllocate: false, out failure))
            {
                return Fail(RevenueOperationCode.Unauthorized, failure, preview);
            }

            long requestedUnits = Math.Max(0L, units);
            long payable = Math.Min(requestedUnits, obligation.OutstandingUnits);
            if (payable <= 0L)
            {
                return units > obligation.OutstandingUnits ? Fail(RevenueOperationCode.OverpaymentRejected, "Payment above outstanding amount is rejected.", preview) : Fail(RevenueOperationCode.InvalidRequest, "Payment amount must be positive.", preview);
            }

            string key = $"{(remittance ? "remittance" : "payment")}:{obligationId}:{requestedUnits}";
            string paymentId = $"{(remittance ? "remittance" : "payment")}.{obligationId}.{Sanitize(transactionId)}";
            if (!preview && IsDuplicate(transactionId, key, out InstitutionalRevenueOperationResult duplicate))
            {
                return duplicate.With(payment: paymentsById.TryGetValue(paymentId, out InstitutionalPaymentData live) ? live : null, obligation: obligation);
            }

            InstitutionalRevenueRuntimeSaveData revenueRollback = CreateSaveData();
            EconomyRuntimeSaveData economyRollback = economy?.CreateSaveData();
            try
            {
                if (economy == null)
                {
                    return Fail(RevenueOperationCode.MissingAccount, "Economy runtime is missing.", preview);
                }

                EconomyOperationResult transfer = economy.Transfer(transactionId, obligation.payerAccountId, obligation.institutionAccountId, new MoneyAmount(obligation.currencyId, payable), EconomyTransactionKind.Payment, actorId: obligation.payerSubjectId, preview: preview);
                if (!transfer.Succeeded)
                {
                    return EconomyFailure(transfer, before, preview);
                }

                InstitutionalPaymentData payment = BuildPayment(paymentId, obligation, transfer.Transaction?.TransactionId ?? transactionId, payable, worldTime, remittance, withholdingId);
                RevenueReceiptData receipt = BuildReceipt(payment, worldTime);
                payment.receiptId = receipt.receiptId;
                InstitutionalObligationData updated = obligation.Clone();
                updated.amountPaidUnits = checked(updated.amountPaidUnits + payable);
                updated.paymentIds = updated.paymentIds.Concat(new[] { payment.paymentId }).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
                updated.state = updated.OutstandingUnits == 0L ? InstitutionalObligationState.Paid : InstitutionalObligationState.PartiallyPaid;
                updated.revision++;

                InstitutionalAssessmentData updatedAssessment = null;
                if (assessmentsById.TryGetValue(updated.assessmentId, out InstitutionalAssessmentData assessment))
                {
                    updatedAssessment = assessment.Clone();
                    updatedAssessment.amountPaidUnits = checked(updatedAssessment.amountPaidUnits + payable);
                    updatedAssessment.state = updated.OutstandingUnits == 0L ? RevenueAssessmentState.Paid : RevenueAssessmentState.PartiallyPaid;
                    updatedAssessment.revision++;
                }

                if (preview)
                {
                    return InstitutionalRevenueOperationResult.Success("Institutional payment preview succeeded.", before, before, preview: true).With(payment: payment, obligation: updated, assessment: updatedAssessment, receipt: receipt, economyTransaction: transfer.Transaction?.Data);
                }

                if (FailAt(injectFailureStage, "after-economy-transfer"))
                {
                    throw new InvalidOperationException("Injected institutional payment failure after economy transfer.");
                }

                paymentsById.Add(payment.paymentId, payment);
                receiptsById.Add(receipt.receiptId, receipt);
                obligationsById[updated.obligationId] = updated;
                if (updatedAssessment != null)
                {
                    assessmentsById[updatedAssessment.assessmentId] = updatedAssessment;
                }

                Revision++;
                Remember(transactionId, key, RevenueOperationCode.Succeeded);
                return InstitutionalRevenueOperationResult.Success("Institutional payment collected.", before, Revision).With(payment: payment, obligation: updated, assessment: updatedAssessment, receipt: receipt, economyTransaction: transfer.Transaction?.Data);
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is OverflowException)
            {
                RestoreFromSaveData(revenueRollback, registry);
                if (economy != null && economyRollback != null)
                {
                    economy.RestoreFromSaveData(economyRollback, registry);
                }

                return InstitutionalRevenueOperationResult.Failure(exception is OverflowException ? RevenueOperationCode.ArithmeticOverflow : RevenueOperationCode.RolledBack, exception.Message, before);
            }
        }

        private InstitutionalRevenueOperationResult TransferWithholdingToInstitution(WithholdingRecordData withholding, EconomyRuntime economy, string transactionId, long units, double worldTime, string injectFailureStage, bool preview, string obligationId)
        {
            long before = Revision;
            if (economy == null)
            {
                return Fail(RevenueOperationCode.MissingAccount, "Economy runtime is missing.", preview);
            }

            InstitutionalRevenueRuntimeSaveData revenueRollback = CreateSaveData();
            EconomyRuntimeSaveData economyRollback = economy.CreateSaveData();
            try
            {
                EconomyOperationResult transfer = economy.Transfer(transactionId, withholding.holdingAccountId, withholding.institutionAccountId, new MoneyAmount(withholding.currencyId, units), EconomyTransactionKind.Payment, actorId: withholding.remittingPartySubjectId, preview: preview);
                if (!transfer.Succeeded)
                {
                    return EconomyFailure(transfer, before, preview);
                }

                WithholdingRecordData updatedWithholding = withholding.Clone();
                updatedWithholding.remittedUnits = checked(updatedWithholding.remittedUnits + units);
                updatedWithholding.state = updatedWithholding.UnremittedUnits == 0L ? WithholdingState.Remitted : WithholdingState.PartiallyRemitted;
                updatedWithholding.remittancePaymentId = $"remittance.{withholding.withholdingId}.{Sanitize(transactionId)}";
                updatedWithholding.revision++;

                InstitutionalPaymentData payment = new InstitutionalPaymentData
                {
                    paymentId = updatedWithholding.remittancePaymentId,
                    obligationId = obligationId,
                    assessmentId = withholding.assessmentId,
                    revenueDefinitionId = withholding.revenueDefinitionId,
                    institutionId = string.Empty,
                    economyTransactionId = transfer.Transaction?.TransactionId ?? transactionId,
                    payerAccountId = withholding.holdingAccountId,
                    institutionAccountId = withholding.institutionAccountId,
                    currencyId = withholding.currencyId,
                    units = units,
                    worldTime = worldTime,
                    remittance = true,
                    withholdingId = withholding.withholdingId,
                    accessPolicyId = withholding.accessPolicyId,
                    provenance = withholding.provenance
                };
                RevenueReceiptData receipt = BuildReceipt(payment, worldTime);
                payment.receiptId = receipt.receiptId;

                if (preview)
                {
                    return InstitutionalRevenueOperationResult.Success("Withholding remittance preview succeeded.", before, before, preview: true).With(withholding: updatedWithholding, payment: payment, receipt: receipt, economyTransaction: transfer.Transaction?.Data);
                }

                if (FailAt(injectFailureStage, "after-economy-transfer"))
                {
                    throw new InvalidOperationException("Injected remittance failure after economy transfer.");
                }

                withholdingsById[updatedWithholding.withholdingId] = updatedWithholding;
                paymentsById.Add(payment.paymentId, payment);
                receiptsById.Add(receipt.receiptId, receipt);
                Revision++;
                Remember(transactionId, $"remittance:{withholding.withholdingId}:{units}", RevenueOperationCode.Succeeded);
                return InstitutionalRevenueOperationResult.Success("Withholding remitted.", before, Revision).With(withholding: updatedWithholding, payment: payment, receipt: receipt, economyTransaction: transfer.Transaction?.Data);
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is OverflowException)
            {
                RestoreFromSaveData(revenueRollback, registry);
                economy.RestoreFromSaveData(economyRollback, registry);
                return InstitutionalRevenueOperationResult.Failure(exception is OverflowException ? RevenueOperationCode.ArithmeticOverflow : RevenueOperationCode.RolledBack, exception.Message, before);
            }
        }

        private TaxBaseCalculationData BuildBaseCalculation(InstitutionalRevenueDefinition definition, TaxableEventData taxableEvent, double worldTime, bool mutateAdjustments)
        {
            long gross = definition.TaxBaseKind switch
            {
                TaxBaseKind.FixedAmount => taxableEvent.monetaryValueUnits > 0L ? taxableEvent.monetaryValueUnits : 1L,
                TaxBaseKind.ProductionQuantity or TaxBaseKind.ItemQuantity or TaxBaseKind.RouteUsageCount or TaxBaseKind.DistanceTravelledFoundation or TaxBaseKind.LicenseDuration or TaxBaseKind.AdministrativeServiceOccurrence or TaxBaseKind.CustomExactQuantity => taxableEvent.quantityUnits,
                _ => taxableEvent.monetaryValueUnits
            };
            string subjectId = taxableEvent.assessedSubject?.subjectId ?? string.Empty;
            long exempt = 0L;
            long deduction = 0L;
            foreach (RevenueAdjustmentData adjustment in MatchingAdjustments(exemptionsById.Values, definition.Id, subjectId, worldTime))
            {
                if (adjustment.fullExemption)
                {
                    exempt = gross;
                    break;
                }

                exempt = checked(exempt + Math.Min(adjustment.RemainingUnits, Math.Max(0L, gross - exempt)));
            }

            foreach (RevenueAdjustmentData adjustment in MatchingAdjustments(deductionsById.Values, definition.Id, subjectId, worldTime))
            {
                deduction = checked(deduction + Math.Min(adjustment.RemainingUnits, Math.Max(0L, gross - exempt - deduction)));
            }

            long final = Math.Max(0L, checked(gross - exempt - deduction));
            return new TaxBaseCalculationData
            {
                baseCalculationId = $"base.{definition.Id}.{taxableEvent.taxableEventId}",
                revenueDefinitionId = definition.Id,
                taxableEventId = taxableEvent.taxableEventId,
                subject = taxableEvent.assessedSubject.Clone(),
                baseKind = definition.TaxBaseKind,
                currencyOrUnitId = string.IsNullOrWhiteSpace(taxableEvent.currencyId) ? definition.CurrencyId : taxableEvent.currencyId,
                grossBaseUnits = gross,
                exemptUnits = exempt,
                deductionUnits = deduction,
                finalTaxableBaseUnits = final,
                appliedPolicyIds = MatchingAdjustments(exemptionsById.Values, definition.Id, subjectId, worldTime).Concat(MatchingAdjustments(deductionsById.Values, definition.Id, subjectId, worldTime)).Select(item => item.adjustmentId).ToArray(),
                sourceReferenceIds = new[] { taxableEvent.sourceRecordId, taxableEvent.transactionId, taxableEvent.tradeRecordId, taxableEvent.payrollRecordId, taxableEvent.businessId, taxableEvent.propertyId, taxableEvent.contractId, taxableEvent.violationOrJudgmentReferenceId },
                sourceRevisionTokens = taxableEvent.sourceRuntimeRevisions,
                calculationWorldTime = worldTime,
                accessPolicyId = taxableEvent.accessPolicyId,
                provenance = taxableEvent.provenance
            };
        }

        private static long CalculateBracketed(RevenueRatePolicyData policy, long baseUnits)
        {
            long result = 0L;
            RevenueBracketData[] brackets = (policy.brackets ?? Array.Empty<RevenueBracketData>())
                .OrderBy(item => item.lowerInclusive)
                .ThenBy(item => item.bracketId, StringComparer.Ordinal)
                .ToArray();
            if (policy.progressiveCalculation == ProgressiveCalculationKind.WholeBase || policy.rateKind is RevenueRateKind.ValueBand or RevenueRateKind.QuantityBand or RevenueRateKind.TieredFixedCharge)
            {
                RevenueBracketData match = brackets.LastOrDefault(item => baseUnits >= item.lowerInclusive && (item.upperExclusive < 0L || baseUnits < item.upperExclusive));
                return match == null ? 0L : checked(match.fixedUnits + ApplyRatio(baseUnits, match.rate, policy.roundingMode));
            }

            foreach (RevenueBracketData bracket in brackets)
            {
                if (baseUnits <= bracket.lowerInclusive)
                {
                    continue;
                }

                long upper = bracket.upperExclusive < 0L ? baseUnits : Math.Min(baseUnits, bracket.upperExclusive);
                long span = Math.Max(0L, upper - bracket.lowerInclusive);
                result = checked(result + bracket.fixedUnits + ApplyRatio(span, bracket.rate, policy.roundingMode));
            }

            return result;
        }

        private static long ApplyRatio(long value, RevenueRationalData ratio, RevenueRoundingMode rounding)
        {
            if (value <= 0L || ratio == null || ratio.numerator <= 0L)
            {
                return 0L;
            }

            if (ratio.denominator <= 0L)
            {
                throw new InvalidOperationException("Ratio denominator must be positive.");
            }

            long product = checked(value * ratio.numerator);
            long quotient = product / ratio.denominator;
            long remainder = product % ratio.denominator;
            return rounding switch
            {
                RevenueRoundingMode.Up => remainder == 0L ? quotient : checked(quotient + 1L),
                RevenueRoundingMode.ToNearest => checked((remainder * 2L) >= ratio.denominator ? quotient + 1L : quotient),
                _ => quotient
            };
        }

        private IEnumerable<RevenueAdjustmentData> MatchingAdjustments(IEnumerable<RevenueAdjustmentData> adjustments, string definitionId, string subjectId, double worldTime)
        {
            return (adjustments ?? Array.Empty<RevenueAdjustmentData>())
                .Where(item => string.Equals(item.revenueDefinitionId, definitionId, StringComparison.Ordinal)
                    && (string.IsNullOrWhiteSpace(item.subjectId) || string.Equals(item.subjectId, subjectId, StringComparison.Ordinal))
                    && item.RemainingUnits > 0L
                    && item.effectiveStartWorldTime <= worldTime
                    && (item.expirationWorldTime < 0d || item.expirationWorldTime > worldTime))
                .OrderBy(item => item.priority)
                .ThenBy(item => item.adjustmentId, StringComparer.Ordinal);
        }

        private void ApplyAdjustmentUse(string adjustmentId, long units)
        {
            if (units <= 0L)
            {
                return;
            }

            if (exemptionsById.TryGetValue(adjustmentId, out RevenueAdjustmentData exemption))
            {
                RevenueAdjustmentData updated = exemption.Clone();
                updated.amountUsedUnits = Math.Min(updated.amountUnits, checked(updated.amountUsedUnits + units));
                updated.revision++;
                exemptionsById[adjustmentId] = updated;
            }
            else if (deductionsById.TryGetValue(adjustmentId, out RevenueAdjustmentData deduction))
            {
                RevenueAdjustmentData updated = deduction.Clone();
                updated.amountUsedUnits = Math.Min(updated.amountUnits, checked(updated.amountUsedUnits + units));
                updated.revision++;
                deductionsById[adjustmentId] = updated;
            }
            else if (creditsById.TryGetValue(adjustmentId, out RevenueAdjustmentData credit))
            {
                RevenueAdjustmentData updated = credit.Clone();
                updated.amountUsedUnits = Math.Min(updated.amountUnits, checked(updated.amountUsedUnits + units));
                updated.revision++;
                creditsById[adjustmentId] = updated;
            }
        }

        private bool TryGetDefinition(string definitionId, out InstitutionalRevenueDefinition definition, out string failure)
        {
            failure = string.Empty;
            definition = null;
            if (registry == null || !registry.TryGet(definitionId, out definition))
            {
                failure = $"Institutional revenue definition '{definitionId}' is missing.";
                return false;
            }

            return true;
        }

        private bool AuthorityPermits(string authorityId, InstitutionalRevenueDefinition definition, RevenueSubjectReferenceData subject, string currencyId, double worldTime, bool requireAssess, bool requireCollect, bool requireRefund, bool requireWaive, bool requireAudit, bool requireAllocate, out string failure)
        {
            failure = string.Empty;
            if (!authoritiesById.TryGetValue(authorityId ?? string.Empty, out InstitutionalRevenueAuthorityData authority))
            {
                failure = $"Revenue authority '{authorityId}' is missing.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(authority.sourceReferenceId))
            {
                failure = $"Revenue authority '{authority.authorityId}' has no external source reference.";
                return false;
            }

            if (!string.Equals(authority.institutionId, subject?.subjectId, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(subject?.subjectId)
                && authority.permittedSubjectIds.Length > 0
                && !authority.permittedSubjectIds.Contains(subject.subjectId))
            {
                failure = $"Revenue authority '{authority.authorityId}' does not permit subject '{subject.subjectId}'.";
                return false;
            }

            if (authority.permittedRevenueDefinitionIds.Length > 0 && !authority.permittedRevenueDefinitionIds.Contains(definition.Id))
            {
                failure = $"Revenue authority '{authority.authorityId}' does not permit revenue definition '{definition.Id}'.";
                return false;
            }

            if (authority.permittedRevenueCategories.Length > 0 && !authority.permittedRevenueCategories.Contains(definition.Category))
            {
                failure = $"Revenue authority '{authority.authorityId}' does not permit revenue category '{definition.Category}'.";
                return false;
            }

            if (authority.permittedCurrencyIds.Length > 0 && !authority.permittedCurrencyIds.Contains(currencyId))
            {
                failure = $"Revenue authority '{authority.authorityId}' does not permit currency '{currencyId}'.";
                return false;
            }

            if (authority.effectiveStartWorldTime > worldTime || (authority.effectiveEndWorldTime >= 0d && authority.effectiveEndWorldTime <= worldTime))
            {
                failure = $"Revenue authority '{authority.authorityId}' is not effective at world time {worldTime}.";
                return false;
            }

            if ((requireAssess && !authority.canAssess)
                || (requireCollect && !authority.canCollect)
                || (requireRefund && !authority.canIssueRefund)
                || (requireWaive && !authority.canWaive)
                || (requireAudit && !authority.canAudit)
                || (requireAllocate && !authority.canAllocateRevenue))
            {
                failure = $"Revenue authority '{authority.authorityId}' lacks the required permission.";
                return false;
            }

            return true;
        }

        private string FindCollectAuthority(InstitutionalRevenueDefinition definition, string institutionId, string currencyId)
        {
            return authoritiesById.Values
                .Where(item => string.Equals(item.institutionId, institutionId, StringComparison.Ordinal)
                    && item.canCollect
                    && !string.IsNullOrWhiteSpace(item.sourceReferenceId)
                    && (item.permittedRevenueDefinitionIds.Length == 0 || item.permittedRevenueDefinitionIds.Contains(definition.Id))
                    && (item.permittedCurrencyIds.Length == 0 || item.permittedCurrencyIds.Contains(currencyId)))
                .OrderBy(item => item.authorityId, StringComparer.Ordinal)
                .Select(item => item.authorityId)
                .FirstOrDefault() ?? string.Empty;
        }

        private bool TryResolveInstitutionAccount(InstitutionalRevenueDefinition definition, string institutionId, string currencyId, RevenueAccountPurpose purpose, out InstitutionalRevenueAccountAssignmentData assignment)
        {
            assignment = accountAssignmentsById.Values
                .Where(item => string.Equals(item.institutionId, institutionId, StringComparison.Ordinal)
                    && string.Equals(item.currencyId, currencyId, StringComparison.Ordinal)
                    && (item.purpose == purpose || item.purpose == definition.CollectionAccountPurpose || item.purpose == RevenueAccountPurpose.GeneralTreasury))
                .OrderByDescending(item => item.purpose == definition.CollectionAccountPurpose)
                .ThenBy(item => item.assignmentId, StringComparer.Ordinal)
                .FirstOrDefault()?.Clone();
            return assignment != null;
        }

        private static bool ValidateAuthority(InstitutionalRevenueAuthorityData authority, out string failure)
        {
            failure = string.Empty;
            if (authority == null || string.IsNullOrWhiteSpace(authority.authorityId) || string.IsNullOrWhiteSpace(authority.institutionId) || string.IsNullOrWhiteSpace(authority.sourceReferenceId))
            {
                failure = "Authority ID, institution, and external source reference are required.";
                return false;
            }

            if (authority.authorityCategory == InstitutionalRevenueAuthorityCategory.Unknown)
            {
                failure = "Authority category is required.";
                return false;
            }

            return true;
        }

        private static bool ValidateAccountAssignment(InstitutionalRevenueAccountAssignmentData assignment, out string failure)
        {
            failure = string.Empty;
            if (assignment == null || string.IsNullOrWhiteSpace(assignment.assignmentId) || string.IsNullOrWhiteSpace(assignment.institutionId) || string.IsNullOrWhiteSpace(assignment.accountId) || string.IsNullOrWhiteSpace(assignment.currencyId))
            {
                failure = "Account assignment ID, institution, account, and currency are required.";
                return false;
            }

            return true;
        }

        private static bool ValidateTaxableEvent(TaxableEventData taxableEvent, out string failure)
        {
            failure = string.Empty;
            if (taxableEvent == null || string.IsNullOrWhiteSpace(taxableEvent.taxableEventId) || string.IsNullOrWhiteSpace(taxableEvent.revenueDefinitionId) || string.IsNullOrWhiteSpace(taxableEvent.sourceRuntime) || string.IsNullOrWhiteSpace(taxableEvent.sourceRecordId))
            {
                failure = "Taxable event ID, revenue definition, source runtime, and source record are required.";
                return false;
            }

            if (taxableEvent.assessedSubject == null || string.IsNullOrWhiteSpace(taxableEvent.assessedSubject.subjectId))
            {
                failure = "Taxable event requires an assessed subject.";
                return false;
            }

            return true;
        }

        private static bool ValidateWithholding(WithholdingRecordData withholding, out string failure)
        {
            failure = string.Empty;
            if (withholding == null || string.IsNullOrWhiteSpace(withholding.withholdingId) || string.IsNullOrWhiteSpace(withholding.revenueDefinitionId) || string.IsNullOrWhiteSpace(withholding.withheldFromAccountId) || string.IsNullOrWhiteSpace(withholding.holdingAccountId) || string.IsNullOrWhiteSpace(withholding.institutionAccountId) || string.IsNullOrWhiteSpace(withholding.currencyId))
            {
                failure = "Withholding ID, revenue definition, accounts, and currency are required.";
                return false;
            }

            if (withholding.withheldUnits <= 0L)
            {
                failure = "Withheld amount must be positive.";
                return false;
            }

            return true;
        }

        private static bool ValidateUnique<T>(IEnumerable<T> values, Func<T, string> idSelector, string label, out string failure)
        {
            failure = string.Empty;
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (T value in values ?? Array.Empty<T>())
            {
                string id = idSelector(value);
                if (string.IsNullOrWhiteSpace(id))
                {
                    failure = $"Institutional revenue save data contains a {label} without an ID.";
                    return false;
                }

                if (!ids.Add(id))
                {
                    failure = $"Institutional revenue save data contains duplicate {label} '{id}'.";
                    return false;
                }
            }

            return true;
        }

        private InstitutionalPaymentData BuildPayment(string paymentId, InstitutionalObligationData obligation, string economyTransactionId, long units, double worldTime, bool remittance, string withholdingId)
        {
            return new InstitutionalPaymentData
            {
                paymentId = paymentId,
                obligationId = obligation.obligationId,
                assessmentId = obligation.assessmentId,
                revenueDefinitionId = obligation.revenueDefinitionId,
                institutionId = obligation.institutionId,
                economyTransactionId = economyTransactionId,
                payerAccountId = obligation.payerAccountId,
                institutionAccountId = obligation.institutionAccountId,
                currencyId = obligation.currencyId,
                units = units,
                worldTime = worldTime,
                remittance = remittance,
                withholdingId = withholdingId,
                accessPolicyId = obligation.accessPolicyId,
                provenance = obligation.provenance
            };
        }

        private static RevenueReceiptData BuildReceipt(InstitutionalPaymentData payment, double worldTime)
        {
            return new RevenueReceiptData
            {
                receiptId = $"receipt.{payment.paymentId}",
                paymentId = payment.paymentId,
                obligationId = payment.obligationId,
                assessmentId = payment.assessmentId,
                economyTransactionId = payment.economyTransactionId,
                currencyId = payment.currencyId,
                units = payment.units,
                issuedWorldTime = worldTime,
                accessPolicyId = payment.accessPolicyId,
                provenance = payment.provenance
            };
        }

        private static Dictionary<string, InformationRedactionState> BuildDetailStates(InformationAccessDecision decision, IEnumerable<string> detailIds)
        {
            Dictionary<string, InformationRedactionState> states = (detailIds ?? Array.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToDictionary(id => id, _ => InformationRedactionState.Hidden, StringComparer.Ordinal);
            if (decision == null)
            {
                return states;
            }

            foreach (string detailId in decision.AllowedDetails ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(detailId))
                {
                    states[detailId] = InformationRedactionState.Visible;
                }
            }

            foreach (string detailId in decision.RedactedDetails ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(detailId))
                {
                    states[detailId] = InformationRedactionState.Redacted;
                }
            }

            foreach (string detailId in decision.HiddenDetails ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(detailId))
                {
                    states[detailId] = InformationRedactionState.Hidden;
                }
            }

            return states;
        }

        private static void RedactAssessment(InstitutionalAssessmentData data, InformationAccessDecision decision, IReadOnlyDictionary<string, InformationRedactionState> details)
        {
            if (data == null || details == null)
            {
                return;
            }

            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.subject"))
            {
                data.assessedSubject = new RevenueSubjectReferenceData();
                data.withholdingSubject = new RevenueSubjectReferenceData();
                data.remittingSubject = new RevenueSubjectReferenceData();
            }

            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.amount"))
            {
                data.grossChargeUnits = 0L;
                data.exemptionUnits = 0L;
                data.deductionUnits = 0L;
                data.creditUnits = 0L;
                data.penaltyUnits = 0L;
                data.finalAssessedUnits = 0L;
                data.amountDueUnits = 0L;
                data.amountPaidUnits = 0L;
            }

            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.accounts"))
            {
                data.obligationId = string.Empty;
            }

            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.adjustments"))
            {
                data.baseCalculationIds = Array.Empty<string>();
            }

            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.authority"))
            {
                data.approvalAuthorityId = string.Empty;
            }

            if (!InformationAccessProjectionUtility.IsVisible(details, "detail.provenance"))
            {
                data.provenance = string.Empty;
                data.taxableEventIds = Array.Empty<string>();
                data.priorAssessmentId = string.Empty;
                data.correctedByAssessmentId = string.Empty;
            }
        }

        private InstitutionalRevenueOperationResult EconomyFailure(EconomyOperationResult economyResult, long revision, bool preview)
        {
            RevenueOperationCode code = economyResult.Code == EconomyResultCode.InsufficientFunds
                ? RevenueOperationCode.InsufficientFunds
                : economyResult.Code == EconomyResultCode.MissingAccount
                    ? RevenueOperationCode.MissingAccount
                    : economyResult.Code == EconomyResultCode.CurrencyMismatch
                        ? RevenueOperationCode.CurrencyMismatch
                        : RevenueOperationCode.InvalidRequest;
            return InstitutionalRevenueOperationResult.Failure(code, economyResult.Message, revision, preview);
        }

        private InstitutionalRevenueOperationResult Fail(RevenueOperationCode code, string message, bool preview)
        {
            return InstitutionalRevenueOperationResult.Failure(code, message, Revision, preview);
        }

        private bool IsDuplicate(string transactionId, string operationKey, out InstitutionalRevenueOperationResult duplicate)
        {
            duplicate = null;
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                return false;
            }

            if (!processedByTransactionId.TryGetValue(transactionId, out RevenueProcessedTransactionData processed))
            {
                return false;
            }

            if (string.Equals(processed.operationKey, operationKey, StringComparison.Ordinal))
            {
                duplicate = InstitutionalRevenueOperationResult.Success("Duplicate institutional revenue transaction ignored.", Revision, Revision, duplicate: true);
                return true;
            }

            duplicate = InstitutionalRevenueOperationResult.Failure(RevenueOperationCode.InvalidRequest, $"Transaction ID '{transactionId}' was already used for revenue operation '{processed.operationKey}', not '{operationKey}'.", Revision);
            return true;
        }

        private void Remember(string transactionId, string operationKey, RevenueOperationCode code)
        {
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                return;
            }

            processedByTransactionId[transactionId] = new RevenueProcessedTransactionData
            {
                transactionId = transactionId,
                operationKey = operationKey ?? string.Empty,
                code = code,
                revision = Revision
            };
        }

        private static string ExclusiveAssessmentKey(string definitionId, string periodId, string eventId)
        {
            return $"{definitionId}|{periodId}|{eventId}";
        }

        private static bool FailAt(string stage, string expected)
        {
            return !string.IsNullOrWhiteSpace(stage) && string.Equals(stage, expected, StringComparison.Ordinal);
        }

        private static string Sanitize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "auto" : new string(value.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray()).Trim('-').ToLowerInvariant();
        }
    }
}
