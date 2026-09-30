using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.Economy;
using UnityIsekaiGame.Economy.Payroll;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Knowledge.Sharing;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.Professions;
using UnityIsekaiGame.Progression;

namespace UnityIsekaiGame.Tests
{
    public sealed class WagesEmploymentPayrollTests
    {
        [Test]
        public void AgreementsRequireActiveEmploymentAndRejectOverlap()
        {
            Fixture fixture = Fixture.Create();
            PayrollOperationResult agreement = fixture.CreateAgreement("primary");
            PayrollOperationResult overlap = fixture.Payroll.ActivateAgreement(fixture.Agreement("overlap", start: 10d, end: 100d), fixture.Positions, fixture.Economy, "tx.payroll.overlap");
            PayrollOperationResult missingEmployment = fixture.Payroll.ActivateAgreement(fixture.Agreement("missing", employmentId: "employment.missing"), fixture.Positions, fixture.Economy, "tx.payroll.missing");

            Assert.That(agreement.Succeeded, Is.True, agreement.Message);
            Assert.That(overlap.Succeeded, Is.False);
            Assert.That(overlap.Code, Is.EqualTo(PayrollOperationCode.AgreementOverlap));
            Assert.That(missingEmployment.Succeeded, Is.False);
            Assert.That(missingEmployment.Code, Is.EqualTo(PayrollOperationCode.MissingEmployment));
            Assert.That(fixture.Payroll.AgreementCount, Is.EqualTo(1));
        }

        [Test]
        public void GrossNetDeductionsAndReimbursementsUseExactIntegerUnits()
        {
            Fixture fixture = Fixture.Create();
            fixture.CreateAgreement("calc");
            PayrollOperationResult session = fixture.RecordSession("calc", minutes: 480L);
            fixture.CreatePayPeriod("calc");
            fixture.Payroll.RecordAdjustment(fixture.Adjustment("premium", CompensationAdjustmentCategory.Premium, 10L), "tx.payroll.adjustment.premium");
            fixture.Payroll.RecordAdjustment(fixture.Adjustment("meal", CompensationAdjustmentCategory.Reimbursement, 5L), "tx.payroll.adjustment.meal");

            PayrollOperationResult calculation = fixture.Payroll.CalculatePay(
                "payroll-calculation.calc",
                fixture.PayPeriodId,
                new[] { session.WorkSession.workSessionId },
                new[] { "payroll-adjustment.premium", "payroll-adjustment.meal" },
                "tx.payroll.calculate");

            Assert.That(calculation.Succeeded, Is.True, calculation.Message);
            Assert.That(calculation.Calculation.regularGrossUnits, Is.EqualTo(80L));
            Assert.That(calculation.Calculation.adjustmentGrossUnits, Is.EqualTo(10L));
            Assert.That(calculation.Calculation.reimbursementUnits, Is.EqualTo(5L));
            Assert.That(calculation.Calculation.deductionUnits, Is.EqualTo(9L));
            Assert.That(calculation.Calculation.netPayUnits, Is.EqualTo(86L));
            Assert.That(calculation.Calculation.deductions.Single().units, Is.EqualTo(9L));
        }

        [Test]
        public void PayrollExecutionRollsBackInjectedFailureAcrossPayrollAndEconomy()
        {
            Fixture fixture = Fixture.Create();
            fixture.BuildObligation("rollback");
            fixture.Payroll.CreatePayrollRun("payroll-run.rollback", fixture.EmployerId, fixture.EmployerAccountId, new[] { fixture.ObligationId }, PayrollPaymentPolicy.AllOrNothing, 20d, "tx.payroll.run.rollback");
            PayrollRuntimeSaveData beforePayroll = fixture.Payroll.CreateSaveData();
            EconomyRuntimeSaveData beforeEconomy = fixture.Economy.CreateSaveData();

            PayrollOperationResult failed = fixture.Payroll.ExecutePayrollRun("payroll-run.rollback", fixture.Economy, "tx.payroll.execute.rollback", injectFailureStage: "before-run-commit");

            Assert.That(failed.Succeeded, Is.False);
            Assert.That(failed.Code, Is.EqualTo(PayrollOperationCode.RolledBack));
            Assert.That(JsonUtility.ToJson(fixture.Payroll.CreateSaveData()), Is.EqualTo(JsonUtility.ToJson(beforePayroll)));
            Assert.That(JsonUtility.ToJson(fixture.Economy.CreateSaveData()), Is.EqualTo(JsonUtility.ToJson(beforeEconomy)));
        }

        [Test]
        public void PartialPayrollCreatesWageDebtWithoutLosingLedgerConservation()
        {
            Fixture fixture = Fixture.Create(employerOpeningBalance: 60L);
            fixture.BuildObligation("partial");
            fixture.Payroll.CreatePayrollRun("payroll-run.partial", fixture.EmployerId, fixture.EmployerAccountId, new[] { fixture.ObligationId }, PayrollPaymentPolicy.PartialWithDebt, 20d, "tx.payroll.run.partial");

            PayrollOperationResult executed = fixture.Payroll.ExecutePayrollRun("payroll-run.partial", fixture.Economy, "tx.payroll.execute.partial");

            Assert.That(executed.Succeeded, Is.True, executed.Message);
            Assert.That(fixture.Payroll.TryGetObligation(fixture.ObligationId, out PayrollObligationData obligation), Is.True);
            Assert.That(obligation.state, Is.EqualTo(PayrollObligationState.DebtOutstanding));
            Assert.That(obligation.amountPaidUnits, Is.EqualTo(60L));
            Assert.That(obligation.amountOutstandingUnits, Is.EqualTo(20L));
            Assert.That(fixture.Payroll.WageDebts.Single().outstandingUnits, Is.EqualTo(20L));
            Assert.That(fixture.Economy.TryGetAccount(fixture.EmployerAccountId, out EconomyAccountSnapshot employer), Is.True);
            Assert.That(fixture.Economy.TryGetAccount(fixture.EmployeeAccountId, out EconomyAccountSnapshot employee), Is.True);
            Assert.That(fixture.Economy.TryGetAccount(fixture.DeductionAccountId, out EconomyAccountSnapshot deduction), Is.True);
            Assert.That(employer.BalanceUnits + employee.BalanceUnits + deduction.BalanceUnits, Is.EqualTo(60L));
        }

        [Test]
        public void PayrollPersistenceRejectsMissingDefinitionsBeforeLiveMutation()
        {
            Fixture fixture = Fixture.Create();
            fixture.BuildObligation("persist");
            PayrollRuntimeSaveData save = fixture.Payroll.CreateSaveData();
            PayrollPersistenceParticipant participant = new PayrollPersistenceParticipant(fixture.Payroll, () => fixture.Registry);
            PayrollRuntimeSaveData corrupt = save.Clone();
            corrupt.agreements[0].compensationDefinitionId = "compensation.missing";

            PersistenceParticipantPrepareResult prepared = participant.PreparePayload(JsonUtility.ToJson(corrupt), PayrollPersistenceParticipant.CurrentParticipantSchemaVersion);

            Assert.That(prepared.Succeeded, Is.False);
            Assert.That(fixture.Payroll.TryGetAgreement(fixture.AgreementId, out CompensationAgreementData agreement), Is.True);
            Assert.That(agreement.compensationDefinitionId, Is.EqualTo(fixture.Compensation.Id));
        }

        [Test]
        public void PayStatementsProjectRedactedAccessWithoutMutatingRuntime()
        {
            Fixture fixture = Fixture.Create();
            fixture.BuildObligation("statement");
            fixture.Payroll.CreatePayrollRun("payroll-run.statement", fixture.EmployerId, fixture.EmployerAccountId, new[] { fixture.ObligationId }, PayrollPaymentPolicy.AllOrNothing, 20d, "tx.payroll.run.statement");
            PayrollOperationResult executed = fixture.Payroll.ExecutePayrollRun("payroll-run.statement", fixture.Economy, "tx.payroll.execute.statement");
            string statementId = executed.PayRun.statementIds.Single();

            PayrollProjection<PayStatementData> redacted = fixture.Payroll.ProjectPayStatement(statementId, PayrollProjectionAudience.Public, null);
            PayrollProjection<PayStatementData> employee = fixture.Payroll.ProjectPayStatement(statementId, PayrollProjectionAudience.Employee, null);

            Assert.That(redacted.Redacted, Is.True);
            Assert.That(redacted.Record.netUnits, Is.Zero);
            Assert.That(redacted.RedactedFields, Does.Contain("detail.payroll.net"));
            Assert.That(employee.Redacted, Is.False);
            Assert.That(employee.Record.netUnits, Is.EqualTo(72L));
            Assert.That(fixture.Payroll.TryGetStatement(statementId, out PayStatementData live), Is.True);
            Assert.That(live.netUnits, Is.EqualTo(72L));
        }

        private sealed class Fixture
        {
            private Fixture(DefinitionRegistry registry, CurrencyDefinition gold, CompensationDefinition compensation, PayrollDeductionDefinition deduction, EconomyRuntime economy, PayrollRuntime payroll, PositionEmploymentRuntime positions, string employmentId, string employerAccountId, string employeeAccountId, string deductionAccountId)
            {
                Registry = registry;
                Gold = gold;
                Compensation = compensation;
                Deduction = deduction;
                Economy = economy;
                Payroll = payroll;
                Positions = positions;
                EmploymentId = employmentId;
                EmployerAccountId = employerAccountId;
                EmployeeAccountId = employeeAccountId;
                DeductionAccountId = deductionAccountId;
            }

            public DefinitionRegistry Registry { get; }
            public CurrencyDefinition Gold { get; }
            public CompensationDefinition Compensation { get; }
            public PayrollDeductionDefinition Deduction { get; }
            public EconomyRuntime Economy { get; }
            public PayrollRuntime Payroll { get; }
            public PositionEmploymentRuntime Positions { get; }
            public string EmploymentId { get; }
            public string EmployerAccountId { get; }
            public string EmployeeAccountId { get; }
            public string DeductionAccountId { get; }
            public string AgreementId => "payroll-agreement.primary";
            public string PayPeriodId => "payroll-period.primary";
            public string CalculationId => "payroll-calculation.primary";
            public string ObligationId => "payroll-obligation.primary";
            public string EmployeeId => "person.payroll.employee";
            public string EmployerId => "organization.payroll.employer";

            public static Fixture Create(long employerOpeningBalance = 1000L)
            {
                CurrencyDefinition gold = ScriptableObject.CreateInstance<CurrencyDefinition>();
                gold.Initialize("currency.gold", "Gold", "G");
                CompensationDefinition compensation = ScriptableObject.CreateInstance<CompensationDefinition>();
                compensation.Initialize("compensation.test.hourly", "Test Hourly Wage", gold, CompensationCategory.HourlyWage, CompensationRateBasis.PerDurationUnit, 10L, duration: PayrollDurationUnit.Hour);
                PayrollDeductionDefinition deduction = ScriptableObject.CreateInstance<PayrollDeductionDefinition>();
                deduction.Initialize("payroll-deduction.test.tax", "Test Payroll Tax", gold, DeductionCategory.Tax, 0L, new PayrollRationalData { numerator = 1L, denominator = 10L }, 10, "account.payroll.tax");
                PositionDefinition position = ScriptableObject.CreateInstance<PositionDefinition>();
                position.DevelopmentConfigure(
                    "position.test.payroll-worker",
                    "Payroll Worker",
                    PositionCategory.Custom,
                    authorities: new[] { ProfessionContentIds.PositionAppointAuthorityId },
                    compensationPolicy: compensation.Id,
                    paymentSchedule: "pay-schedule.test.weekly",
                    wageOrSalary: compensation.Id,
                    maxHolders: 2,
                    exclusive: false);
                DefinitionRegistry registry = new DefinitionRegistry(new IGameDefinition[] { gold, compensation, deduction, position });

                EconomyRuntime economy = new EconomyRuntime();
                economy.Configure(registry, PersistenceService.LocalWorldId);
                PayrollRuntime payroll = new PayrollRuntime();
                payroll.Configure(registry, PersistenceService.LocalWorldId);

                PersonProfessionRuntime professions = new PersonProfessionRuntime();
                InformationTransferRuntime transfers = new InformationTransferRuntime();
                TrainingRuntime training = new TrainingRuntime();
                ProfessionalActivityRuntime activities = new ProfessionalActivityRuntime();
                CredentialRuntime credentials = new CredentialRuntime();
                ProfessionalRankRuntime ranks = new ProfessionalRankRuntime();
                professions.Configure(registry, new[] { "person.payroll.employee" });
                transfers.Configure(registry, "person.payroll.employee");
                training.Configure(registry, professions, transfers, new[] { "person.payroll.employee" });
                activities.Configure(registry, professions, new[] { "person.payroll.employee" });
                credentials.Configure(registry, professions, training, activities, new[] { "person.payroll.employee" }, new[] { ProfessionContentIds.PositionAppointAuthorityId });
                ranks.Configure(registry, professions, training, activities, credentials, new[] { "person.payroll.employee" }, new[] { ProfessionContentIds.PositionAppointAuthorityId });

                PositionEmploymentRuntime positions = new PositionEmploymentRuntime();
                positions.Configure(
                    registry,
                    professions,
                    training,
                    activities,
                    credentials,
                    ranks,
                    new[] { "person.payroll.employee" },
                    new[] { "organization.payroll.employer" },
                    new[] { ProfessionContentIds.PositionAppointAuthorityId, "organization.payroll.employer" });
                PositionEmploymentOperationResult positionResult = positions.CreatePosition(new PositionInstanceData
                {
                    positionInstanceId = "position-instance.payroll-worker",
                    positionDefinitionId = position.Id,
                    organizationId = "organization.payroll.employer",
                    state = PositionInstanceState.Vacant,
                    maximumHolders = 2,
                    vacancyAllowed = true,
                    createdWorldTime = "0"
                }, "tx.position.create");
                Assert.That(positionResult.Succeeded, Is.True, positionResult.Message);
                PositionEligibilityResult eligibility = positions.EvaluateEligibility("person.payroll.employee", "position-instance.payroll-worker", privilegedDiagnostics: true);
                PositionEmploymentOperationResult employment = positions.AppointPerson("employment.payroll.worker", string.Empty, "person.payroll.employee", "position-instance.payroll-worker", ProfessionContentIds.PositionAppointAuthorityId, eligibility.Snapshot, "1", "tx.position.appoint");
                Assert.That(employment.Succeeded, Is.True, employment.Message);

                economy.CreateAccount("account.payroll.employer", gold, "organization.payroll.employer", EconomyAccountKind.OrganizationAccount, employerOpeningBalance, "tx.account.employer");
                economy.CreateAccount("account.payroll.employee", gold, "person.payroll.employee", EconomyAccountKind.PersonWallet, 0L, "tx.account.employee");
                economy.CreateAccount("account.payroll.tax", gold, "organization.payroll.tax", EconomyAccountKind.OrganizationAccount, 0L, "tx.account.tax");
                return new Fixture(registry, gold, compensation, deduction, economy, payroll, positions, employment.Employment.employmentId, "account.payroll.employer", "account.payroll.employee", "account.payroll.tax");
            }

            public CompensationAgreementData Agreement(string slug, double start = 0d, double end = -1d, string employmentId = "")
            {
                return new CompensationAgreementData
                {
                    agreementId = slug == "primary" || slug == "calc" ? AgreementId : $"payroll-agreement.{slug}",
                    compensationDefinitionId = Compensation.Id,
                    employmentId = string.IsNullOrWhiteSpace(employmentId) ? EmploymentId : employmentId,
                    employeePersonId = EmployeeId,
                    employerSubjectId = EmployerId,
                    employerFundingAccountId = EmployerAccountId,
                    employeeAccountId = EmployeeAccountId,
                    deductionDefinitionIds = new[] { Deduction.Id },
                    state = CompensationAgreementState.Active,
                    effectiveStartWorldTime = start,
                    effectiveEndWorldTime = end
                };
            }

            public PayrollOperationResult CreateAgreement(string slug)
            {
                return Payroll.ActivateAgreement(Agreement(slug), Positions, Economy, $"tx.payroll.agreement.{slug}");
            }

            public PayrollOperationResult RecordSession(string slug, long minutes)
            {
                return Payroll.RecordWorkSession(new WorkSessionData
                {
                    workSessionId = $"payroll-session.{slug}",
                    agreementId = AgreementId,
                    startWorldTime = 0d,
                    endWorldTime = minutes,
                    durationMinutes = minutes,
                    evidenceIds = new[] { $"evidence.payroll.{slug}" }
                }, Positions, $"tx.payroll.session.{slug}");
            }

            public void CreatePayPeriod(string slug)
            {
                Payroll.CreatePayPeriod(new PayPeriodData
                {
                    payPeriodId = PayPeriodId,
                    agreementId = AgreementId,
                    startWorldTime = 0d,
                    endWorldTime = 604800d,
                    dueWorldTime = 691200d
                }, $"tx.payroll.period.{slug}");
            }

            public CompensationAdjustmentData Adjustment(string slug, CompensationAdjustmentCategory category, long units)
            {
                return new CompensationAdjustmentData
                {
                    adjustmentId = $"payroll-adjustment.{slug}",
                    agreementId = AgreementId,
                    payPeriodId = PayPeriodId,
                    category = category,
                    currencyId = Gold.Id,
                    units = units
                };
            }

            public void BuildObligation(string slug)
            {
                if (!Payroll.TryGetAgreement(AgreementId, out _))
                {
                    CreateAgreement("primary");
                }

                PayrollOperationResult session = RecordSession(slug, 480L);
                Assert.That(session.Succeeded, Is.True, session.Message);
                CreatePayPeriod(slug);
                PayrollOperationResult calculation = Payroll.CalculatePay(CalculationId, PayPeriodId, new[] { session.WorkSession.workSessionId }, Array.Empty<string>(), $"tx.payroll.calculate.{slug}");
                Assert.That(calculation.Succeeded, Is.True, calculation.Message);
                PayrollOperationResult obligation = Payroll.CreateObligation(ObligationId, CalculationId, 691200d, $"tx.payroll.obligation.{slug}");
                Assert.That(obligation.Succeeded, Is.True, obligation.Message);
            }
        }
    }
}
