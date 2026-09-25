using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.Contracts;
using UnityIsekaiGame.Economy;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.Progression;

namespace UnityIsekaiGame.Tests
{
    public sealed class ContractsLoansObligationsTests
    {
        [Test]
        public void ProposalAcceptanceActivationAndSnapshotsCreateObligationsWithoutPayment()
        {
            Fixture fixture = Fixture.Create();
            fixture.CreateAccounts(borrowerBalance: 100L, lenderBalance: 0L);
            ContractProposalData proposal = fixture.CreatePaymentProposal("proposal.service", 25L);

            ContractEconomyOperationResult create = fixture.Contracts.CreateProposal(proposal, "tx.proposal");
            ContractEconomyOperationResult acceptA = fixture.Contracts.AcceptProposal("proposal.service", "party.customer", 1d, "tx.accept.customer");
            ContractEconomyOperationResult acceptB = fixture.Contracts.AcceptProposal("proposal.service", "party.worker", 1d, "tx.accept.worker");
            ContractEconomyOperationResult activate = fixture.Contracts.ActivateProposal("proposal.service", "contract.service", 2d, "tx.activate");
            ContractObligationData snapshot = fixture.Contracts.Obligations.Single();
            snapshot.amountDueUnits = 999L;

            Assert.That(create.Succeeded, Is.True, create.Message);
            Assert.That(acceptA.Succeeded, Is.True, acceptA.Message);
            Assert.That(acceptB.Succeeded, Is.True, acceptB.Message);
            Assert.That(activate.Succeeded, Is.True, activate.Message);
            Assert.That(fixture.Contracts.TryGetObligation("obligation.contract.service.term.payment", out ContractObligationData live), Is.True);
            Assert.That(live.amountDueUnits, Is.EqualTo(25L));
            Assert.That(live.amountSatisfiedUnits, Is.Zero);
            Assert.That(fixture.Economy.TryGetAccount("account.customer", out EconomyAccountSnapshot customer), Is.True);
            Assert.That(customer.BalanceUnits, Is.EqualTo(100L));
        }

        [Test]
        public void ObligationPaymentUsesEconomyTransactionAndRollsBackInjectedFailure()
        {
            Fixture fixture = Fixture.Create();
            fixture.CreateAccounts(borrowerBalance: 100L, lenderBalance: 0L);
            fixture.ActivatePaymentContract("contract.service", 40L);

            ContractEconomyOperationResult failed = fixture.Contracts.AllocatePaymentToObligation("obligation.contract.service.term.payment", fixture.Economy, "tx.contract.failed", 10L, 3d, injectFailureStage: "after-economy-transfer");
            Assert.That(failed.Succeeded, Is.False);
            Assert.That(failed.Code, Is.EqualTo(ContractOperationCode.RolledBack));
            Assert.That(fixture.Economy.TryGetAccount("account.customer", out EconomyAccountSnapshot customerAfterFailure), Is.True);
            Assert.That(customerAfterFailure.BalanceUnits, Is.EqualTo(100L));

            ContractEconomyOperationResult paid = fixture.Contracts.AllocatePaymentToObligation("obligation.contract.service.term.payment", fixture.Economy, "tx.contract.pay", 30L, 4d);
            ContractEconomyOperationResult duplicate = fixture.Contracts.AllocatePaymentToObligation("obligation.contract.service.term.payment", fixture.Economy, "tx.contract.pay", 30L, 4d);

            Assert.That(paid.Succeeded, Is.True, paid.Message);
            Assert.That(duplicate.Succeeded, Is.True, duplicate.Message);
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(fixture.Economy.TryGetAccount("account.customer", out EconomyAccountSnapshot customerAfterPayment), Is.True);
            Assert.That(customerAfterPayment.BalanceUnits, Is.EqualTo(70L));
            Assert.That(fixture.Contracts.TryGetObligation("obligation.contract.service.term.payment", out ContractObligationData obligation), Is.True);
            Assert.That(obligation.amountSatisfiedUnits, Is.EqualTo(30L));
            Assert.That(obligation.OutstandingUnits, Is.EqualTo(10L));
            Assert.That(fixture.Economy.TryGetAccount("account.worker", out EconomyAccountSnapshot worker), Is.True);
            Assert.That(worker.BalanceUnits, Is.EqualTo(30L));
        }

        [Test]
        public void LoansDisburseAccrueRepayAndTrackCollateralExactly()
        {
            Fixture fixture = Fixture.Create();
            fixture.CreateAccounts(borrowerBalance: 100L, lenderBalance: 1000L);
            fixture.ActivatePaymentContract("contract.loan", 0L);
            LoanData loan = fixture.CreateLoan("loan.prototype", principalUnits: 500L, numerator: 1L, denominator: 10L);

            ContractEconomyOperationResult create = fixture.Contracts.CreateLoan(loan, "tx.loan.create");
            ContractEconomyOperationResult disburse = fixture.Contracts.DisburseLoan("loan.prototype", fixture.Economy, "tx.loan.disburse", 5d);
            ContractEconomyOperationResult schedule = fixture.Contracts.GenerateRepaymentSchedule("loan.prototype", 5, 10d, 10d, "tx.loan.schedule");
            ContractEconomyOperationResult accrue = fixture.Contracts.AccrueLoanInterest("loan.prototype", "period.001", "tx.loan.interest");
            ContractEconomyOperationResult collateral = fixture.Contracts.AddCollateral(new CollateralDesignationData
            {
                collateralId = "collateral.prototype.sword",
                contractId = "contract.loan",
                loanId = "loan.prototype",
                assetKind = CollateralAssetKind.ItemInstance,
                assetId = "item-instance.prototype.sword",
                providerPartyId = "party.borrower",
                currencyId = fixture.Gold.Id,
                estimatedValueUnits = 80L
            }, "tx.loan.collateral");
            ContractEconomyOperationResult repay = fixture.Contracts.RepayLoan("loan.prototype", fixture.Economy, "tx.loan.repay", 75L, 20d);

            Assert.That(create.Succeeded, Is.True, create.Message);
            Assert.That(disburse.Succeeded, Is.True, disburse.Message);
            Assert.That(schedule.Succeeded, Is.True, schedule.Message);
            Assert.That(accrue.Succeeded, Is.True, accrue.Message);
            Assert.That(collateral.Succeeded, Is.True, collateral.Message);
            Assert.That(repay.Succeeded, Is.True, repay.Message);
            Assert.That(fixture.Contracts.TryGetLoan("loan.prototype", out LoanData liveLoan), Is.True);
            Assert.That(liveLoan.accruedInterestOutstandingUnits, Is.Zero);
            Assert.That(liveLoan.outstandingPrincipalUnits, Is.EqualTo(475L));
            Assert.That(liveLoan.collateralIds, Does.Contain("collateral.prototype.sword"));
            Assert.That(fixture.Contracts.Installments.Count, Is.EqualTo(5));
            Assert.That(fixture.Economy.TryGetAccount("account.lender", out EconomyAccountSnapshot lender), Is.True);
            Assert.That(fixture.Economy.TryGetAccount("account.borrower", out EconomyAccountSnapshot borrower), Is.True);
            Assert.That(lender.BalanceUnits, Is.EqualTo(575L));
            Assert.That(borrower.BalanceUnits, Is.EqualTo(525L));
        }

        [Test]
        public void PersistenceRejectsBrokenGraphBeforeCommitWithoutMutatingLiveRuntime()
        {
            Fixture fixture = Fixture.Create();
            fixture.CreateAccounts(borrowerBalance: 100L, lenderBalance: 0L);
            fixture.ActivatePaymentContract("contract.service", 20L);
            ContractEconomyPersistenceParticipant participant = new ContractEconomyPersistenceParticipant(fixture.Contracts, () => fixture.Registry);
            ContractRuntimeSaveData corrupt = fixture.Contracts.CreateSaveData();
            corrupt.contracts[0].obligationIds = new[] { "obligation.missing" };

            PersistenceParticipantPrepareResult prepared = participant.PreparePayload(JsonUtility.ToJson(corrupt), ContractEconomyPersistenceParticipant.CurrentParticipantSchemaVersion);

            Assert.That(prepared.Succeeded, Is.False);
            Assert.That(fixture.Contracts.TryGetObligation("obligation.contract.service.term.payment", out ContractObligationData live), Is.True);
            Assert.That(live.amountDueUnits, Is.EqualTo(20L));
        }

        [Test]
        public void ActiveContractAndObligationsSurviveSaveRestore()
        {
            Fixture fixture = Fixture.Create();
            fixture.CreateAccounts(borrowerBalance: 100L, lenderBalance: 0L);
            fixture.ActivatePaymentContract("contract.persistence", 20L);
            ContractRuntimeSaveData save = fixture.Contracts.CreateSaveData();
            ContractEconomyRuntime restored = new ContractEconomyRuntime();
            restored.Configure(fixture.Registry, PersistenceService.LocalWorldId);

            ContractEconomyOperationResult result = restored.RestoreFromSaveData(save, fixture.Registry);

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(restored.Contracts.Single().contractId, Is.EqualTo("contract.persistence"));
            Assert.That(restored.Contracts.Single().state, Is.EqualTo(EconomicContractState.Active));
            Assert.That(restored.Obligations.Single().amountDueUnits, Is.EqualTo(20L));
            Assert.That(restored.Obligations.Single().amountSatisfiedUnits, Is.Zero);
        }

        private sealed class Fixture
        {
            private Fixture(DefinitionRegistry registry, CurrencyDefinition gold, EconomyRuntime economy, ContractEconomyRuntime contracts)
            {
                Registry = registry;
                Gold = gold;
                Economy = economy;
                Contracts = contracts;
            }

            public DefinitionRegistry Registry { get; }
            public CurrencyDefinition Gold { get; }
            public EconomyRuntime Economy { get; }
            public ContractEconomyRuntime Contracts { get; }

            public static Fixture Create()
            {
                CurrencyDefinition gold = ScriptableObject.CreateInstance<CurrencyDefinition>();
                gold.Initialize("currency.gold", "Gold", "G");
                ContractFinanceDefinition definition = ScriptableObject.CreateInstance<ContractFinanceDefinition>();
                definition.Initialize(
                    "contract-definition.prototype.service",
                    "Prototype Service Contract",
                    EconomicContractCategory.Service,
                    new[] { ContractPartyRole.Debtor, ContractPartyRole.Creditor },
                    new[] { new ContractTermData { termId = "term.payment", category = ContractTermCategory.Payment, currencyId = gold.Id, amountUnits = 1L } });
                DefinitionRegistry registry = new DefinitionRegistry(new IGameDefinition[] { gold, definition });
                EconomyRuntime economy = new EconomyRuntime();
                economy.Configure(registry, PersistenceService.LocalWorldId);
                ContractEconomyRuntime contracts = new ContractEconomyRuntime();
                contracts.Configure(registry, PersistenceService.LocalWorldId);
                return new Fixture(registry, gold, economy, contracts);
            }

            public void CreateAccounts(long borrowerBalance, long lenderBalance)
            {
                Economy.CreateAccount("account.customer", Gold, "person.customer", EconomyAccountKind.PersonWallet, borrowerBalance, "tx.open.customer");
                Economy.CreateAccount("account.worker", Gold, "person.worker", EconomyAccountKind.PersonWallet, lenderBalance, "tx.open.worker");
                Economy.CreateAccount("account.borrower", Gold, "person.borrower", EconomyAccountKind.PersonWallet, borrowerBalance, "tx.open.borrower");
                Economy.CreateAccount("account.lender", Gold, "person.lender", EconomyAccountKind.PersonWallet, lenderBalance, "tx.open.lender");
            }

            public ContractProposalData CreatePaymentProposal(string proposalId, long amountUnits)
            {
                return new ContractProposalData
                {
                    proposalId = proposalId,
                    definitionId = "contract-definition.prototype.service",
                    category = EconomicContractCategory.Service,
                    state = ContractProposalState.Offered,
                    createdByPartyId = "party.customer",
                    parties = new[]
                    {
                        new ContractPartyData { partyId = "party.customer", role = ContractPartyRole.Debtor, reference = ContractPartyReferenceData.Person("person.customer"), accountId = "account.customer" },
                        new ContractPartyData { partyId = "party.worker", role = ContractPartyRole.Creditor, reference = ContractPartyReferenceData.Person("person.worker"), accountId = "account.worker" }
                    },
                    terms = new[]
                    {
                        new ContractTermData
                        {
                            termId = "term.payment",
                            category = amountUnits <= 0L ? ContractTermCategory.General : ContractTermCategory.Payment,
                            responsiblePartyId = "party.customer",
                            beneficiaryPartyId = "party.worker",
                            currencyId = Gold.Id,
                            amountUnits = amountUnits,
                            dueWorldTime = 10d
                        }
                    }
                };
            }

            public void ActivatePaymentContract(string contractId, long amountUnits)
            {
                ContractProposalData proposal = CreatePaymentProposal("proposal." + contractId, amountUnits);
                Contracts.CreateProposal(proposal, "tx." + contractId + ".proposal");
                Contracts.AcceptProposal(proposal.proposalId, "party.customer", 1d, "tx." + contractId + ".accept.customer");
                Contracts.AcceptProposal(proposal.proposalId, "party.worker", 1d, "tx." + contractId + ".accept.worker");
                Contracts.ActivateProposal(proposal.proposalId, contractId, 2d, "tx." + contractId + ".activate");
            }

            public LoanData CreateLoan(string loanId, long principalUnits, long numerator, long denominator)
            {
                return new LoanData
                {
                    loanId = loanId,
                    contractId = "contract.loan",
                    lenderPartyId = "party.lender",
                    borrowerPartyId = "party.borrower",
                    lenderAccountId = "account.lender",
                    borrowerAccountId = "account.borrower",
                    currencyId = Gold.Id,
                    principalUnits = principalUnits,
                    interestRatePerPeriod = new ContractRationalData { numerator = numerator, denominator = denominator, rounding = ContractRoundingMode.Floor },
                    state = LoanState.Approved
                };
            }
        }
    }
}
