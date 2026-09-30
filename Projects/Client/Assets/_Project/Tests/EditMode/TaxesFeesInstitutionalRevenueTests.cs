using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.Economy;
using UnityIsekaiGame.Economy.InstitutionalRevenue;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.Progression;

namespace UnityIsekaiGame.Tests
{
    public sealed class TaxesFeesInstitutionalRevenueTests
    {
        [Test]
        public void ExactRatePoliciesCalculateWithoutFloatingPoint()
        {
            Assert.That(InstitutionalRevenueRuntime.CalculateCharge(Flat(25L), 999L), Is.EqualTo(25L));
            Assert.That(InstitutionalRevenueRuntime.CalculateCharge(Percent(1L, 10L), 255L), Is.EqualTo(25L));
            Assert.That(InstitutionalRevenueRuntime.CalculateCharge(PerUnit(3L), 7L), Is.EqualTo(21L));
            Assert.That(InstitutionalRevenueRuntime.CalculateCharge(CappedPercent(1L, 2L, 5L, 40L), 1000L), Is.EqualTo(40L));
            Assert.That(InstitutionalRevenueRuntime.CalculateCharge(Progressive(), 250L), Is.EqualTo(25L));
            Assert.That(InstitutionalRevenueRuntime.CalculateCharge(Threshold(100L, 12L), 99L), Is.Zero);
            Assert.That(InstitutionalRevenueRuntime.CalculateCharge(Threshold(100L, 12L), 100L), Is.EqualTo(12L));
        }

        [Test]
        public void AssessmentPaymentRevenueAllocationAndRollbackRemainAtomic()
        {
            Fixture fixture = Fixture.Create();
            fixture.CreateAccounts(payerBalance: 100L, treasuryBalance: 0L, allocationBalance: 0L);
            fixture.RegisterCoreAuthorityAndAccounts();
            fixture.RegisterEvent("event.tax.sale", fixture.SalesTax.Id, 200L, TaxableEventCategory.CompletedTrade);

            InstitutionalRevenueOperationResult preview = fixture.Revenue.GenerateAssessment("assessment.sale.preview", fixture.SalesTax.Id, new[] { "event.tax.sale" }, Fixture.AuthorityId, "period.sale.preview", 10d, approve: true, preview: true);
            Assert.That(preview.Succeeded, Is.True, preview.Message);
            Assert.That(fixture.Revenue.AssessmentCount, Is.Zero);
            InstitutionalRevenueOperationResult assessment = fixture.Revenue.GenerateAssessment("assessment.sale", fixture.SalesTax.Id, new[] { "event.tax.sale" }, Fixture.AuthorityId, "period.sale", 10d, approve: true, transactionId: "tx.assess.sale");
            InstitutionalRevenueOperationResult duplicateEvent = fixture.Revenue.GenerateAssessment("assessment.sale.duplicate", fixture.SalesTax.Id, new[] { "event.tax.sale" }, Fixture.AuthorityId, "period.sale", 10d, approve: true, transactionId: "tx.assess.sale.duplicate");
            InstitutionalRevenueOperationResult failedPayment = fixture.Revenue.PayObligation(assessment.Obligation.obligationId, fixture.Economy, "tx.pay.fail", 5L, 11d, injectFailureStage: "after-economy-transfer");
            InstitutionalRevenueOperationResult payment = fixture.Revenue.PayObligation(assessment.Obligation.obligationId, fixture.Economy, "tx.pay.sale", 20L, 12d);
            InstitutionalRevenueOperationResult revenue = fixture.Revenue.RecognizeRevenue(payment.Payment.paymentId, "revenue.sale", "tax.sales", "tx.revenue.sale");
            InstitutionalRevenueOperationResult allocation = fixture.Revenue.AllocateRevenue("revenue.sale", fixture.Economy, Fixture.AuthorityId, Fixture.AllocationAccountId, "tx.allocate.sale", 8L, 13d);

            Assert.That(assessment.Succeeded, Is.True, assessment.Message);
            Assert.That(fixture.Revenue.AssessmentCount, Is.EqualTo(1));
            Assert.That(assessment.Assessment.finalAssessedUnits, Is.EqualTo(20L));
            Assert.That(duplicateEvent.Code, Is.EqualTo(RevenueOperationCode.AlreadyAssessed));
            Assert.That(failedPayment.Code, Is.EqualTo(RevenueOperationCode.RolledBack));
            Assert.That(payment.Succeeded, Is.True, payment.Message);
            Assert.That(revenue.Succeeded, Is.True, revenue.Message);
            Assert.That(allocation.Succeeded, Is.True, allocation.Message);
            Assert.That(fixture.Economy.TryGetAccount(Fixture.PayerAccountId, out EconomyAccountSnapshot payer), Is.True);
            Assert.That(fixture.Economy.TryGetAccount(Fixture.TreasuryAccountId, out EconomyAccountSnapshot treasury), Is.True);
            Assert.That(fixture.Economy.TryGetAccount(Fixture.AllocationAccountId, out EconomyAccountSnapshot allocationAccount), Is.True);
            Assert.That(payer.BalanceUnits, Is.EqualTo(80L));
            Assert.That(treasury.BalanceUnits, Is.EqualTo(12L));
            Assert.That(allocationAccount.BalanceUnits, Is.EqualTo(8L));
        }

        [Test]
        public void ExemptionsDeductionsCreditsPenaltiesWaiversAndRefundsAreDistinct()
        {
            Fixture fixture = Fixture.Create();
            fixture.CreateAccounts(payerBalance: 100L, treasuryBalance: 100L, allocationBalance: 0L);
            fixture.RegisterCoreAuthorityAndAccounts();
            fixture.RegisterEvent("event.tax.adjusted", fixture.SalesTax.Id, 200L, TaxableEventCategory.CompletedTrade);
            fixture.Revenue.RegisterExemption(fixture.Adjustment("exemption.prototype", fixture.SalesTax.Id, 20L), Fixture.AuthorityId, "tx.exemption");
            fixture.Revenue.RegisterDeduction(fixture.Adjustment("deduction.prototype", fixture.SalesTax.Id, 30L), Fixture.AuthorityId, "tx.deduction");
            fixture.Revenue.RegisterCredit(fixture.Adjustment("credit.prototype", fixture.SalesTax.Id, 4L), Fixture.AuthorityId, "tx.credit");

            InstitutionalRevenueOperationResult assessment = fixture.Revenue.GenerateAssessment("assessment.adjusted", fixture.SalesTax.Id, new[] { "event.tax.adjusted" }, Fixture.AuthorityId, "period.adjusted", 10d, approve: true, transactionId: "tx.assess.adjusted");
            InstitutionalRevenueOperationResult penalty = fixture.Revenue.ApplyPenalty("penalty.adjusted", assessment.Obligation.obligationId, Fixture.AuthorityId, Flat(3L), 20d, "late", "tx.penalty");
            InstitutionalRevenueOperationResult payment = fixture.Revenue.PayObligation(assessment.Obligation.obligationId, fixture.Economy, "tx.pay.adjusted", assessment.Obligation.amountDueUnits, 21d);
            InstitutionalRevenueOperationResult waiver = fixture.Revenue.WaiveObligation("waiver.adjusted", assessment.Obligation.obligationId, Fixture.AuthorityId, 2L, 22d, "tx.waiver");
            InstitutionalRevenueOperationResult refund = fixture.Revenue.RefundPayment("refund.adjusted", payment.Payment.paymentId, fixture.Economy, Fixture.AuthorityId, 5L, 23d, "overpayment");

            Assert.That(assessment.Succeeded, Is.True, assessment.Message);
            Assert.That(assessment.Assessment.exemptionUnits, Is.EqualTo(20L));
            Assert.That(assessment.Assessment.deductionUnits, Is.EqualTo(30L));
            Assert.That(assessment.Assessment.creditUnits, Is.EqualTo(4L));
            Assert.That(assessment.Assessment.finalAssessedUnits, Is.EqualTo(11L));
            Assert.That(penalty.Succeeded, Is.True, penalty.Message);
            Assert.That(waiver.Succeeded, Is.True, waiver.Message);
            Assert.That(refund.Succeeded, Is.True, refund.Message);
            Assert.That(fixture.Revenue.CreateSaveData().penalties.Single().units, Is.EqualTo(3L));
            Assert.That(fixture.Revenue.CreateSaveData().waivers.Single().units, Is.EqualTo(2L));
            Assert.That(fixture.Revenue.CreateSaveData().refunds.Single().units, Is.EqualTo(5L));
        }

        [Test]
        public void WithholdingAndRemittanceAreSeparateFromDirectTaxPayment()
        {
            Fixture fixture = Fixture.Create();
            fixture.CreateAccounts(payerBalance: 100L, treasuryBalance: 0L, allocationBalance: 0L, holdingBalance: 0L);
            fixture.RegisterCoreAuthorityAndAccounts();
            fixture.RegisterEvent("event.tax.payroll", fixture.PayrollWithholding.Id, 100L, TaxableEventCategory.PayrollPayment, withholdingAgent: "person.employer");

            InstitutionalRevenueOperationResult assessment = fixture.Revenue.GenerateAssessment("assessment.payroll", fixture.PayrollWithholding.Id, new[] { "event.tax.payroll" }, Fixture.AuthorityId, "period.payroll", 10d, approve: true, transactionId: "tx.assess.payroll");
            InstitutionalRevenueOperationResult withholding = fixture.Revenue.WithholdFromPayment(new WithholdingRecordData
            {
                withholdingId = "withholding.payroll",
                assessmentId = assessment.Assessment.assessmentId,
                revenueDefinitionId = fixture.PayrollWithholding.Id,
                withholdingAgentSubjectId = "person.employer",
                remittingPartySubjectId = "person.employer",
                withheldFromAccountId = Fixture.PayerAccountId,
                holdingAccountId = Fixture.HoldingAccountId,
                institutionAccountId = Fixture.TreasuryAccountId,
                currencyId = fixture.Gold.Id,
                withheldUnits = assessment.Assessment.finalAssessedUnits
            }, fixture.Economy, "tx.withhold");
            InstitutionalRevenueOperationResult remit = fixture.Revenue.RemitWithholding("withholding.payroll", fixture.Economy, "tx.remit", 10L, 11d);

            Assert.That(assessment.Succeeded, Is.True, assessment.Message);
            Assert.That(withholding.Succeeded, Is.True, withholding.Message);
            Assert.That(remit.Succeeded, Is.True, remit.Message);
            Assert.That(fixture.Revenue.WithholdingCount, Is.EqualTo(1));
            Assert.That(fixture.Economy.TryGetAccount(Fixture.PayerAccountId, out EconomyAccountSnapshot payer), Is.True);
            Assert.That(fixture.Economy.TryGetAccount(Fixture.HoldingAccountId, out EconomyAccountSnapshot holding), Is.True);
            Assert.That(fixture.Economy.TryGetAccount(Fixture.TreasuryAccountId, out EconomyAccountSnapshot treasury), Is.True);
            Assert.That(payer.BalanceUnits, Is.EqualTo(90L));
            Assert.That(holding.BalanceUnits, Is.Zero);
            Assert.That(treasury.BalanceUnits, Is.EqualTo(10L));
        }

        [Test]
        public void FilingsAuditsStatementsAndAccessProjectionDoNotCreateLegalEnforcement()
        {
            Fixture fixture = Fixture.Create();
            fixture.CreateAccounts(payerBalance: 100L, treasuryBalance: 0L, allocationBalance: 0L);
            fixture.RegisterCoreAuthorityAndAccounts();
            fixture.RegisterEvent("event.tax.filing", fixture.SalesTax.Id, 100L, TaxableEventCategory.CompletedTrade);
            InstitutionalRevenueOperationResult assessment = fixture.Revenue.GenerateAssessment("assessment.filing", fixture.SalesTax.Id, new[] { "event.tax.filing" }, Fixture.AuthorityId, "period.filing", 10d, approve: true, transactionId: "tx.assess.filing");

            InstitutionalRevenueOperationResult filing = fixture.Revenue.SubmitFiling(new RevenueFilingData { filingId = "filing.sales", periodId = "period.filing", revenueDefinitionId = fixture.SalesTax.Id, reportingSubjectId = "person.taxpayer", declaredTaxableEventIds = new[] { "event.tax.filing" } }, "tx.filing");
            InstitutionalRevenueOperationResult audit = fixture.Revenue.AuditFiling("audit.sales", "filing.sales", Fixture.AuditAuthorityId, RevenueAuditFindingKind.AmountMismatchFoundation, assessment.Assessment.assessmentId, "Reported and assessed events differ.", 15d, "tx.audit");
            InstitutionalRevenueOperationResult statement = fixture.Revenue.GenerateStatement("statement.taxpayer", "person.taxpayer", fixture.Gold.Id, 16d);
            InformationAccessProjection<InstitutionalAssessmentData> projection = fixture.Revenue.GetAssessmentProjection("assessment.filing", fixture.Access, new InformationAccessContext { RequestingPersonId = "person.other" });

            Assert.That(filing.Succeeded, Is.True, filing.Message);
            Assert.That(audit.Succeeded, Is.True, audit.Message);
            Assert.That(statement.Succeeded, Is.True, statement.Message);
            Assert.That(statement.Statement.totalDueUnits, Is.EqualTo(10L));
            Assert.That(projection.Record, Is.Not.Null);
            Assert.That(projection.Redacted || projection.Denied, Is.True);
            Assert.That(fixture.Revenue.CreateSaveData().auditFindings.Single().findingKind, Is.EqualTo(RevenueAuditFindingKind.AmountMismatchFoundation));
        }

        [Test]
        public void PersistenceRejectsBrokenGraphBeforeCommitWithoutMutatingRuntime()
        {
            Fixture fixture = Fixture.Create();
            fixture.CreateAccounts(payerBalance: 100L, treasuryBalance: 0L, allocationBalance: 0L);
            fixture.RegisterCoreAuthorityAndAccounts();
            fixture.RegisterEvent("event.tax.persist", fixture.SalesTax.Id, 100L, TaxableEventCategory.CompletedTrade);
            fixture.Revenue.GenerateAssessment("assessment.persist", fixture.SalesTax.Id, new[] { "event.tax.persist" }, Fixture.AuthorityId, "period.persist", 10d, approve: true, transactionId: "tx.assess.persist");
            InstitutionalRevenuePersistenceParticipant participant = new InstitutionalRevenuePersistenceParticipant(fixture.Revenue, () => fixture.Registry);
            InstitutionalRevenueRuntimeSaveData corrupt = fixture.Revenue.CreateSaveData();
            corrupt.obligations[0].assessmentId = "assessment.missing";

            PersistenceParticipantPrepareResult rejected = participant.PreparePayload(JsonUtility.ToJson(corrupt), InstitutionalRevenuePersistenceParticipant.CurrentParticipantSchemaVersion);

            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(fixture.Revenue.TryGetAssessment("assessment.persist", out InstitutionalAssessmentData live), Is.True);
            Assert.That(live.finalAssessedUnits, Is.EqualTo(10L));
        }

        private sealed class Fixture
        {
            private Fixture(DefinitionRegistry registry, CurrencyDefinition gold, InstitutionalRevenueDefinition salesTax, InstitutionalRevenueDefinition payrollWithholding)
            {
                Registry = registry;
                Gold = gold;
                SalesTax = salesTax;
                PayrollWithholding = payrollWithholding;
                Economy = new EconomyRuntime();
                Revenue = new InstitutionalRevenueRuntime();
                Access = new InformationAccessRuntime();
                Economy.Configure(registry, PersistenceService.LocalWorldId);
                Revenue.Configure(registry, PersistenceService.LocalWorldId);
                Access.Configure(registry, "person.taxpayer");
            }

            public const string InstitutionId = "institution.prototype.city";
            public const string AuthorityId = "authority.prototype.tax";
            public const string AuditAuthorityId = "authority.prototype.audit";
            public const string PayerAccountId = "account.taxpayer";
            public const string TreasuryAccountId = "account.city.treasury";
            public const string AllocationAccountId = "account.city.roads";
            public const string HoldingAccountId = "account.employer.withholding";

            public DefinitionRegistry Registry { get; }
            public CurrencyDefinition Gold { get; }
            public InstitutionalRevenueDefinition SalesTax { get; }
            public InstitutionalRevenueDefinition PayrollWithholding { get; }
            public EconomyRuntime Economy { get; }
            public InstitutionalRevenueRuntime Revenue { get; }
            public InformationAccessRuntime Access { get; }

            public static Fixture Create()
            {
                CurrencyDefinition gold = ScriptableObject.CreateInstance<CurrencyDefinition>();
                gold.Initialize("currency.gold", "Gold", "G");
                InstitutionalRevenueDefinition sales = Definition("revenue.tax.sales", "Sales Tax", gold, InstitutionalRevenueCategory.SalesTaxFoundation, TaxableEventCategory.CompletedTrade, Percent(1L, 10L), withholding: false);
                InstitutionalRevenueDefinition payroll = Definition("revenue.tax.payroll-withholding", "Payroll Withholding", gold, InstitutionalRevenueCategory.PayrollTax, TaxableEventCategory.PayrollPayment, Percent(1L, 10L), withholding: true);
                DefinitionRegistry registry = new DefinitionRegistry(new IGameDefinition[] { gold, sales, payroll });
                return new Fixture(registry, gold, sales, payroll);
            }

            public void CreateAccounts(long payerBalance, long treasuryBalance, long allocationBalance, long holdingBalance = 0L)
            {
                Economy.CreateAccount(PayerAccountId, Gold, "person.taxpayer", EconomyAccountKind.PersonWallet, payerBalance, "tx.account.payer");
                Economy.CreateAccount(TreasuryAccountId, Gold, InstitutionId, EconomyAccountKind.OrganizationAccount, treasuryBalance, "tx.account.treasury");
                Economy.CreateAccount(AllocationAccountId, Gold, InstitutionId, EconomyAccountKind.OrganizationAccount, allocationBalance, "tx.account.roads");
                Economy.CreateAccount(HoldingAccountId, Gold, "person.employer", EconomyAccountKind.OrganizationAccount, holdingBalance, "tx.account.holding");
            }

            public void RegisterCoreAuthorityAndAccounts()
            {
                Revenue.RegisterAuthority(Authority(AuthorityId, InstitutionalRevenueAuthorityCategory.Assess, assess: true, collect: true, refund: true, waive: true, allocate: true, audit: true), "tx.authority.tax");
                Revenue.RegisterAuthority(Authority(AuditAuthorityId, InstitutionalRevenueAuthorityCategory.Audit, assess: false, collect: false, refund: false, waive: false, allocate: false, audit: true), "tx.authority.audit");
                Revenue.AssignRevenueAccount(Account("assignment.treasury", TreasuryAccountId, RevenueAccountPurpose.TaxCollection, AuthorityId), "tx.account.assign.treasury");
                Revenue.AssignRevenueAccount(Account("assignment.allocation", AllocationAccountId, RevenueAccountPurpose.RevenueDistribution, AuthorityId), "tx.account.assign.allocation");
            }

            public void RegisterEvent(string eventId, string definitionId, long valueUnits, TaxableEventCategory category, string withholdingAgent = "")
            {
                Revenue.RegisterTaxableEvent(new TaxableEventData
                {
                    taxableEventId = eventId,
                    revenueDefinitionId = definitionId,
                    eligibleCategory = InstitutionalRevenueCategory.SalesTaxFoundation,
                    eventCategory = category,
                    assessedSubject = Subject("person.taxpayer", PayerAccountId, RevenueSubjectRole.AssessedParty),
                    otherSubjects = string.IsNullOrWhiteSpace(withholdingAgent) ? System.Array.Empty<RevenueSubjectReferenceData>() : new[] { Subject(withholdingAgent, HoldingAccountId, RevenueSubjectRole.WithholdingAgent), Subject(withholdingAgent, HoldingAccountId, RevenueSubjectRole.RemittingParty) },
                    institutionId = InstitutionId,
                    currencyId = Gold.Id,
                    eventWorldTime = 1d,
                    monetaryValueUnits = valueUnits,
                    quantityUnits = valueUnits,
                    sourceRuntime = "test",
                    sourceRecordId = eventId
                }, AuthorityId, "tx.event." + eventId);
            }

            public RevenueAdjustmentData Adjustment(string id, string definitionId, long amountUnits)
            {
                return new RevenueAdjustmentData
                {
                    adjustmentId = id,
                    revenueDefinitionId = definitionId,
                    subjectId = "person.taxpayer",
                    amountUnits = amountUnits,
                    approvalAuthorityId = AuthorityId,
                    sourceReferenceId = id
                };
            }

            private static InstitutionalRevenueDefinition Definition(string id, string name, CurrencyDefinition gold, InstitutionalRevenueCategory category, TaxableEventCategory eventCategory, RevenueRatePolicyData rate, bool withholding)
            {
                InstitutionalRevenueDefinition definition = ScriptableObject.CreateInstance<InstitutionalRevenueDefinition>();
                definition.Initialize(id, name, category, InstitutionKind.Organization, InstitutionalRevenueAuthorityCategory.Assess, gold, TaxBaseKind.TransactionGrossAmount, rate, AssessmentPeriodKind.PerEvent, new[] { RevenueSubjectKind.Person }, new[] { eventCategory }, withholding, RevenueAccountPurpose.TaxCollection, requiresFiling: true);
                return definition;
            }

            private static InstitutionalRevenueAuthorityData Authority(string authorityId, InstitutionalRevenueAuthorityCategory category, bool assess, bool collect, bool refund, bool waive, bool allocate, bool audit)
            {
                return new InstitutionalRevenueAuthorityData
                {
                    authorityId = authorityId,
                    institutionId = InstitutionId,
                    institutionKind = InstitutionKind.Organization,
                    authorityCategory = category,
                    sourceReferenceId = "charter.prototype.tax-office",
                    permittedRevenueCategories = new[] { InstitutionalRevenueCategory.SalesTaxFoundation, InstitutionalRevenueCategory.PayrollTax, InstitutionalRevenueCategory.Toll, InstitutionalRevenueCategory.ImportTariff, InstitutionalRevenueCategory.LicenseFee, InstitutionalRevenueCategory.Fine },
                    permittedSubjectKinds = new[] { RevenueSubjectKind.Person, RevenueSubjectKind.Organization },
                    permittedCurrencyIds = new[] { "currency.gold" },
                    canAssess = assess,
                    canCollect = collect,
                    canReceiveRemittance = collect,
                    canIssueRefund = refund,
                    canWaive = waive,
                    canAdjust = true,
                    canAudit = audit,
                    canAllocateRevenue = allocate
                };
            }

            private static InstitutionalRevenueAccountAssignmentData Account(string id, string accountId, RevenueAccountPurpose purpose, string authorityId)
            {
                return new InstitutionalRevenueAccountAssignmentData
                {
                    assignmentId = id,
                    institutionId = InstitutionId,
                    institutionKind = InstitutionKind.Organization,
                    accountId = accountId,
                    purpose = purpose,
                    currencyId = "currency.gold",
                    receivingAuthorityId = authorityId
                };
            }

            private static RevenueSubjectReferenceData Subject(string subjectId, string accountId, RevenueSubjectRole role)
            {
                return new RevenueSubjectReferenceData
                {
                    subjectKind = RevenueSubjectKind.Person,
                    role = role,
                    subjectId = subjectId,
                    accountId = accountId,
                    personId = subjectId
                };
            }
        }

        private static RevenueRatePolicyData Flat(long units)
        {
            return new RevenueRatePolicyData { ratePolicyId = "rate.flat." + units, rateKind = RevenueRateKind.FixedAmount, fixedUnits = units, smallestChargeableUnit = 1L };
        }

        private static RevenueRatePolicyData Percent(long numerator, long denominator)
        {
            return new RevenueRatePolicyData { ratePolicyId = $"rate.percent.{numerator}.{denominator}", rateKind = RevenueRateKind.FlatProportional, rate = new RevenueRationalData { numerator = numerator, denominator = denominator }, smallestChargeableUnit = 1L };
        }

        private static RevenueRatePolicyData CappedPercent(long numerator, long denominator, long minimum, long maximum)
        {
            return new RevenueRatePolicyData { ratePolicyId = "rate.capped", rateKind = RevenueRateKind.CappedProportionalCharge, rate = new RevenueRationalData { numerator = numerator, denominator = denominator }, minimumUnits = minimum, maximumUnits = maximum, smallestChargeableUnit = 1L };
        }

        private static RevenueRatePolicyData PerUnit(long units)
        {
            return new RevenueRatePolicyData { ratePolicyId = "rate.per-unit", rateKind = RevenueRateKind.PerUnit, perUnitUnits = units, smallestChargeableUnit = 1L };
        }

        private static RevenueRatePolicyData Threshold(long threshold, long fixedUnits)
        {
            return new RevenueRatePolicyData
            {
                ratePolicyId = "rate.threshold",
                rateKind = RevenueRateKind.ThresholdCharge,
                thresholdUnits = threshold,
                smallestChargeableUnit = 1L,
                brackets = new[]
                {
                    new RevenueBracketData { bracketId = "threshold", lowerInclusive = 0L, upperExclusive = -1L, fixedUnits = fixedUnits, rate = new RevenueRationalData { numerator = 0L, denominator = 1L } }
                }
            };
        }

        private static RevenueRatePolicyData Progressive()
        {
            return new RevenueRatePolicyData
            {
                ratePolicyId = "rate.progressive",
                rateKind = RevenueRateKind.ProgressiveBracket,
                progressiveCalculation = ProgressiveCalculationKind.Marginal,
                smallestChargeableUnit = 1L,
                brackets = new[]
                {
                    new RevenueBracketData { bracketId = "first", lowerInclusive = 0L, upperExclusive = 100L, rate = new RevenueRationalData { numerator = 1L, denominator = 10L } },
                    new RevenueBracketData { bracketId = "second", lowerInclusive = 100L, upperExclusive = -1L, rate = new RevenueRationalData { numerator = 1L, denominator = 10L } }
                }
            };
        }
    }
}
