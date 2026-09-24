#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityIsekaiGame.Contracts;
using UnityIsekaiGame.Economy;
using UnityIsekaiGame.Economy.Businesses;
using UnityIsekaiGame.Economy.InstitutionalRevenue;
using UnityIsekaiGame.Economy.Integration;
using UnityIsekaiGame.Economy.Markets;
using UnityIsekaiGame.Economy.Payroll;
using UnityIsekaiGame.Economy.Properties;
using UnityIsekaiGame.Economy.RegionalFlow;
using UnityIsekaiGame.Economy.Trading;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Inventory.Durability;
using UnityIsekaiGame.Inventory.Quality;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Inventory.Production;
using UnityIsekaiGame.Inventory.Recipes;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.Professions;
using UnityIsekaiGame.Progression;

namespace UnityIsekaiGame.Development.Automation
{
    [PrototypeTestLabAutomationProvider(11, "Economy", 1100)]
    public static class PrototypeStep11AutomationSuites
    {
        private const string GoldCurrencyId = "currency.gold";
        private const string CoinCurrencyId = "currency.prototype.coin";
        private const string CoinItemId = "item.prototype-gold-coin";
        private const string PrototypeSwordItemId = "item.prototype-sword";

        public static void RegisterDefaults(TestLabAutomationRegistry registry)
        {
            registry?.TryRegister(Suite(
                "feature.11.1.currency-economic-transactions",
                "Currency and Transactions",
                "11.1",
                11010,
                new[] { "EconomyRuntime", "CurrencyDefinition", "ItemInstanceIdentityRuntime", "InformationAccessRuntime" },
                Scenario(
                    "amounts-and-accounts",
                    "Exact monetary amounts and accounts are authoritative",
                    10,
                    Step("step11-economy-amounts", "Create exact accounts", AmountsAndAccounts)),
                Scenario(
                    "payments-transfers-ledger",
                    "Payments and transfers commit atomically to a conserved ledger",
                    20,
                    Step("step11-economy-transfer", "Transfer and ledger", PaymentsTransfersAndLedger)),
                Scenario(
                    "reservations-refunds-reversals",
                    "Reservations, refunds, and reversals preserve balances",
                    30,
                    Step("step11-economy-reservations", "Reserve and refund", ReservationsRefundsAndReversals)),
                Scenario(
                    "physical-currency-price-snapshots",
                    "Physical currency conversion and fixed prices are explicit",
                    40,
                    Step("step11-economy-physical", "Convert physical currency", PhysicalCurrencyAndPrices)),
                Scenario(
                    "persistence-projections-validation",
                    "Persistence and access projections validate economy state",
                    50,
                    Step("step11-economy-persistence", "Persist and project", PersistenceProjectionAndValidation))), out _);

            registry?.TryRegister(Suite(
                "feature.11.2.markets-price-formation",
                "Markets and Price Formation",
                "11.2",
                11020,
                new[] { "MarketRuntime", "EconomyRuntime", "CurrencyDefinition", "ItemInstanceIdentityRuntime", "InformationAccessRuntime" },
                Scenario(
                    "supply-demand-scarcity",
                    "Supply and demand observations produce deterministic scarcity",
                    10,
                    Step("step11-markets-scarcity", "Evaluate scarcity", MarketSupplyDemandScarcity)),
                Scenario(
                    "reference-prices-regional-history",
                    "Reference prices use regional scarcity and immutable history",
                    20,
                    Step("step11-markets-prices", "Form regional prices", MarketReferencePrices)),
                Scenario(
                    "merchant-quotes-adjustments",
                    "Merchant quotes apply margins and item adjustments without trade mutation",
                    30,
                    Step("step11-markets-quotes", "Create merchant quotes", MarketMerchantQuotes)),
                Scenario(
                    "observations-persistence-projections",
                    "Transaction observations, persistence, and projections are explicit",
                    40,
                    Step("step11-markets-persistence", "Persist and project markets", MarketPersistenceAndProjection))), out _);

            registry?.TryRegister(Suite(
                "feature.11.3.trade-negotiation",
                "Trade and Negotiation",
                "11.3",
                11030,
                new[] { "TradeRuntime", "EconomyRuntime", "MarketRuntime", "ItemInstanceIdentityRuntime", "InformationAccessRuntime" },
                Scenario(
                    "offers-and-counteroffers",
                    "Trade sessions support offers, counteroffers, rejection, withdrawal, and expiry",
                    10,
                    Step("step11-trade-offers", "Negotiate offers", TradeOffersAndCounteroffers)),
                Scenario(
                    "fixed-price-purchase",
                    "Fixed-price purchases execute money and item ownership atomically",
                    20,
                    Step("step11-trade-purchase", "Execute purchase", TradeFixedPricePurchase)),
                Scenario(
                    "barter-reservation-rollback",
                    "Barter reservations and rollback preserve money and item state",
                    30,
                    Step("step11-trade-barter", "Reserve barter and rollback", TradeBarterReservationRollback)),
                Scenario(
                    "valuation-persistence-projections",
                    "Valuation, receipts, persistence, and projections remain explicit",
                    40,
                    Step("step11-trade-persistence", "Persist and project trades", TradeValuationPersistenceProjection))), out _);

            registry?.TryRegister(Suite(
                "feature.11.4.wages-employment-payroll",
                "Wages and Payroll",
                "11.4",
                11040,
                new[] { "PayrollRuntime", "PositionEmploymentRuntime", "EconomyRuntime", "CurrencyDefinition", "InformationAccessRuntime" },
                PayrollScenario(
                    "agreements-schedules-evidence",
                    "Compensation agreements, schedules, and work evidence validate against employment",
                    10,
                    Step("step11-payroll-agreement", "Create payroll agreement", PayrollAgreementsSchedulesEvidence)),
                PayrollScenario(
                    "gross-net-deductions",
                    "Gross, net, adjustments, and deductions calculate exactly",
                    20,
                    Step("step11-payroll-calculate", "Calculate payroll", PayrollGrossNetDeductions)),
                PayrollScenario(
                    "execution-rollback-debt",
                    "Payroll execution reserves funds, rolls back failures, and records wage debt",
                    30,
                    Step("step11-payroll-execute", "Execute payroll", PayrollExecutionRollbackDebt)),
                PayrollScenario(
                    "persistence-projections-corrections",
                    "Payroll persistence, projections, corrections, and overpayments are explicit",
                    40,
                    Step("step11-payroll-persist", "Persist and project payroll", PayrollPersistenceProjectionCorrection))), out _);

            registry?.TryRegister(Suite(
                "feature.11.5.businesses-production-ownership",
                "Businesses and Production Ownership",
                "11.5",
                11050,
                new[] { "BusinessRuntime", "EconomyRuntime", "ProductionWorkflowRuntime", "PayrollRuntime", "ItemInstanceIdentityRuntime", "InformationAccessRuntime" },
                BusinessScenario(
                    "formation-ownership-control",
                    "Businesses form with exact ownership and separate control",
                    10,
                    Step("step11-business-form", "Form business", BusinessFormationOwnershipControl)),
                BusinessScenario(
                    "establishments-accounts-inventory-stock",
                    "Establishments, accounts, inventory assignments, and stock remain references",
                    20,
                    Step("step11-business-stock", "Assign operating resources", BusinessEstablishmentsAccountsInventoryStock)),
                BusinessScenario(
                    "production-financial-statements",
                    "Production ownership, revenue, expenses, profit, and cash flow remain distinct",
                    30,
                    Step("step11-business-accounting", "Close accounting period", BusinessProductionFinancialStatements)),
                BusinessScenario(
                    "access-persistence-rollback",
                    "Access projections, persistence, and failed operations preserve state",
                    40,
                    Step("step11-business-persist", "Persist and project business", BusinessAccessPersistenceRollback))), out _);

            registry?.TryRegister(Suite(
                "feature.11.6.property-land-buildings",
                "Property, Land, and Buildings",
                "11.6",
                11060,
                new[] { "PropertyRuntime", "EconomyRuntime", "BusinessRuntime", "ItemInstanceIdentityRuntime", "InformationAccessRuntime" },
                PropertyScenario(
                    "definitions-hierarchy",
                    "Property definitions, spatial references, and hierarchy validate",
                    10,
                    Step("step11-property-hierarchy", "Register property hierarchy", PropertyDefinitionsHierarchy)),
                PropertyScenario(
                    "ownership-title-boundaries",
                    "Ownership interests, title, possession, and occupancy remain distinct",
                    20,
                    Step("step11-property-title", "Validate title boundaries", PropertyOwnershipTitleBoundaries)),
                PropertyScenario(
                    "tenancy-access-rent",
                    "Tenancy grants use access and rent without transferring ownership",
                    30,
                    Step("step11-property-tenancy", "Activate tenancy and rent", PropertyTenancyAccessRent)),
                PropertyScenario(
                    "transfer-rollback-business-boundaries",
                    "Property transfers stage title changes and preserve external runtime boundaries",
                    40,
                    Step("step11-property-transfer", "Transfer property atomically", PropertyTransferRollbackBusinessBoundaries)),
                PropertyScenario(
                    "condition-maintenance-persistence",
                    "Condition, inspection, maintenance, and persistence remain explicit",
                    50,
                    Step("step11-property-maintenance", "Maintain and persist property", PropertyConditionMaintenancePersistence))), out _);

            registry?.TryRegister(Suite(
                "feature.11.7.contracts-loans-obligations",
                "Contracts, Loans, and Obligations",
                "11.7",
                11070,
                new[] { "ContractEconomyRuntime", "EconomyRuntime", "CurrencyDefinition", "InformationAccessRuntime" },
                ContractScenario(
                    "proposal-activation",
                    "Proposals activate into versioned contracts and obligations without moving money",
                    10,
                    Step("step11-contract-proposal", "Activate proposal", ContractProposalActivation)),
                ContractScenario(
                    "obligation-payment-rollback",
                    "Obligation payments use EconomyRuntime and roll back injected failures",
                    20,
                    Step("step11-contract-obligation", "Pay obligation atomically", ContractObligationPaymentRollback)),
                ContractScenario(
                    "loan-interest-collateral",
                    "Loans disburse, accrue exact interest, schedule repayment, and track collateral",
                    30,
                    Step("step11-contract-loan", "Run loan lifecycle", ContractLoanInterestCollateral)),
                ContractScenario(
                    "persistence-graph-validation",
                    "Contract persistence rejects broken graphs before commit",
                    40,
                    Step("step11-contract-persistence", "Validate persistence graph", ContractPersistenceGraphValidation))), out _);

            registry?.TryRegister(Suite(
                "feature.11.8.taxes-fees-institutional-revenue",
                "Taxes, Fees, and Institutional Revenue",
                "11.8",
                11080,
                new[] { "InstitutionalRevenueRuntime", "EconomyRuntime", "CurrencyDefinition", "InformationAccessRuntime" },
                TaxScenario(
                    "exact-rates-and-assessments",
                    "Exact revenue rates generate immutable assessments without moving money",
                    10,
                    Step("step11-revenue-assessment", "Calculate rates and assess events", TaxExactRatesAndAssessments)),
                TaxScenario(
                    "charges-and-fees",
                    "Tariffs, tolls, license fees, and fines require explicit source events",
                    20,
                    Step("step11-revenue-charges", "Assess representative charges", TaxRepresentativeCharges)),
                TaxScenario(
                    "collection-allocation-refund",
                    "Collection, allocation, waiver, penalty, and refund use EconomyRuntime atomically",
                    30,
                    Step("step11-revenue-collection", "Collect and allocate revenue", TaxCollectionAllocationRefund)),
                TaxScenario(
                    "withholding-remittance",
                    "Withholding and remittance remain separate from direct tax payment",
                    40,
                    Step("step11-revenue-withholding", "Withhold and remit revenue", TaxWithholdingRemittance)),
                TaxScenario(
                    "filings-audits-projections-persistence",
                    "Filings, audits, projections, and persistence preserve institutional revenue state",
                    50,
                    Step("step11-revenue-persistence", "Persist and project revenue", TaxFilingsAuditsProjectionPersistence))), out _);

            registry?.TryRegister(Suite(
                "feature.11.9.economic-simulation-regional-flow",
                "Economic Simulation and Regional Flow",
                "11.9",
                11090,
                new[] { "RegionalFlowRuntime", "MarketRuntime", "EconomyRuntime", "InformationAccessRuntime" },
                RegionalFlowScenario(
                    "pools-and-exact-aggregation",
                    "Regions, commodity pools, and exact aggregation remain deterministic",
                    10,
                    Step("step11-regional-pools", "Create regional pools", RegionalFlowPoolsAndAggregation)),
                RegionalFlowScenario(
                    "production-consumption-market",
                    "Production and consumption mutate pools and publish market observations",
                    20,
                    Step("step11-regional-production", "Run aggregate production", RegionalFlowProductionConsumptionMarket)),
                RegionalFlowScenario(
                    "labor-wealth-shortage",
                    "Labor, wealth, shortage, and wage pressure are derived from aggregate state",
                    30,
                    Step("step11-regional-derived", "Evaluate derived regional state", RegionalFlowLaborWealthShortage)),
                RegionalFlowScenario(
                    "trade-flow-conservation",
                    "Inter-region flow planning, departure, arrival, and loss conserve quantities",
                    40,
                    Step("step11-regional-flow", "Move commodities between regions", RegionalFlowTradeConservation)),
                RegionalFlowScenario(
                    "cycle-access-persistence",
                    "Cycles, access projections, rollback, and persistence validation are explicit",
                    50,
                    Step("step11-regional-hardening", "Harden regional runtime", RegionalFlowCycleAccessPersistence))), out _);

            registry?.TryRegister(Suite(
                "feature.11.10.economy-integration-finalization",
                "Economy Integration Finalization",
                "11.10",
                11100,
                new[] { "EconomyIntegrationFacade", "EconomyRuntime", "MarketRuntime", "TradeRuntime", "PayrollRuntime", "BusinessRuntime", "PropertyRuntime", "ContractEconomyRuntime", "InstitutionalRevenueRuntime", "RegionalFlowRuntime", "InformationAccessRuntime" },
                IntegrationScenario(
                    "readiness-authority-map",
                    "Integrated Step 11 readiness and authority map validate",
                    10,
                    Step("step11-integration-readiness", "Validate readiness", EconomyIntegrationReadiness),
                    Step("step11-integration-authority", "Validate authority map", EconomyIntegrationAuthorityMap)),
                IntegrationScenario(
                    "graph-boundaries-conservation",
                    "Graph validation, boundary invariants, and conservation audits pass",
                    20,
                    Step("step11-integration-graph", "Validate graph", EconomyIntegrationGraph),
                    Step("step11-integration-boundaries", "Audit boundaries", EconomyIntegrationBoundaries),
                    Step("step11-integration-conservation", "Audit conservation", EconomyIntegrationConservation)),
                IntegrationScenario(
                    "persistence-restore-determinism",
                    "Persistence dependency, restore replay, and deterministic snapshots are explicit",
                    30,
                    Step("step11-integration-persistence", "Validate persistence dependencies", EconomyIntegrationPersistence),
                    Step("step11-integration-determinism", "Validate deterministic snapshot", EconomyIntegrationDeterminism)),
                IntegrationScenario(
                    "access-signals-step12",
                    "Economic access audit and Step 12 signals are mutation-free",
                    40,
                    Step("step11-integration-access", "Audit access", EconomyIntegrationAccess),
                    Step("step11-integration-signals", "Create Step 12 signal", EconomyIntegrationStep12Signals)),
                IntegrationScenario(
                    "complete-economic-scenario",
                    "Complete Step 11 integration scenario validates without taking ownership",
                    50,
                    Step("step11-integration-complete", "Validate complete integration", EconomyIntegrationComplete))), out _);
        }

        private static TestLabAutomationStepResult EconomyIntegrationReadiness(TestLabAutomationContext context)
        {
            EconomyIntegrationFacade facade = CreateIntegrationFacade(context);
            EconomicReadinessSnapshot readiness = facade.EvaluateReadiness();
            bool valid = readiness.Ready
                && readiness.RuntimeSummaries.Count == 9
                && readiness.RuntimeSummaries.All(summary => summary.present);
            return TestLabAssertions.True("step11-integration-readiness", "Integrated Step 11 readiness validates hostlessly", valid, $"Ready={readiness.Ready} Runtimes={readiness.RuntimeSummaries.Count} Diagnostics={Diagnostics(readiness.Diagnostics)}");
        }

        private static TestLabAutomationStepResult EconomyIntegrationAuthorityMap(TestLabAutomationContext context)
        {
            EconomyIntegrationFacade facade = CreateIntegrationFacade(context);
            EconomicValidationResult result = facade.ValidateAuthorityMap();
            bool valid = result.Succeeded
                && EconomyIntegrationFacade.CreateAuthorityMap().Count(entry => entry.featureId.StartsWith("11.", StringComparison.Ordinal)) == 9
                && EconomyIntegrationFacade.CreateAuthorityMap().Any(entry => entry.domainId == EconomicDomainAuthorityId.RegionalFlow && entry.authoritativeRuntime == nameof(RegionalFlowRuntime));
            return TestLabAssertions.True("step11-integration-authority", "Step 11 has one explicit economic authority per domain", valid, $"Succeeded={result.Succeeded} Diagnostics={result.Summary}");
        }

        private static TestLabAutomationStepResult EconomyIntegrationGraph(TestLabAutomationContext context)
        {
            EconomyIntegrationFacade facade = CreateIntegrationFacade(context);
            EconomicValidationResult result = facade.ValidateEconomicGraph();
            return TestLabAssertions.True("step11-integration-graph", "Integrated economic graph validates through authoritative save contracts", result.Succeeded, result.Summary);
        }

        private static TestLabAutomationStepResult EconomyIntegrationBoundaries(TestLabAutomationContext context)
        {
            EconomyIntegrationFacade facade = CreateIntegrationFacade(context);
            IReadOnlyList<EconomicBoundaryInvariantResult> invariants = facade.AuditBoundaryInvariants();
            bool valid = invariants.Count >= 11 && invariants.All(item => item.satisfied);
            return TestLabAssertions.True("step11-integration-boundaries", "Boundary invariants preserve Step 11 ownership", valid, $"Satisfied={invariants.Count(item => item.satisfied)}/{invariants.Count}");
        }

        private static TestLabAutomationStepResult EconomyIntegrationConservation(TestLabAutomationContext context)
        {
            EconomyIntegrationFacade facade = CreateIntegrationFacade(context);
            EconomicConservationAuditResult result = facade.AuditExactArithmeticAndConservation();
            return TestLabAssertions.True("step11-integration-conservation", "Exact arithmetic and conservation audits pass", result.succeeded, $"LedgerNet={result.monetaryLedgerNet} RegionalUnits={result.regionalKnownPoolUnits} Checked={result.checkedRuntimeCount}. {result.message}");
        }

        private static TestLabAutomationStepResult EconomyIntegrationPersistence(TestLabAutomationContext context)
        {
            EconomyIntegrationFacade facade = CreateIntegrationFacade(context);
            EconomicPersistenceDependencyMapResult result = facade.BuildPersistenceDependencyMap();
            bool valid = result.Succeeded
                && result.Participants.Count == 9
                && result.Participants.Last().participantKey == RegionalFlowPersistenceParticipant.Key;
            return TestLabAssertions.True("step11-integration-persistence", "Persistence dependency map is explicit and acyclic", valid, $"Succeeded={result.Succeeded} Participants={string.Join(",", result.Participants.Select(item => item.participantKey))} Diagnostics={Diagnostics(result.Diagnostics)}");
        }

        private static TestLabAutomationStepResult EconomyIntegrationDeterminism(TestLabAutomationContext context)
        {
            EconomyIntegrationFacade facade = CreateIntegrationFacade(context);
            EconomicIntegrationSnapshot first = facade.CreateSnapshot();
            EconomicIntegrationSnapshot second = facade.CreateSnapshot();
            bool valid = string.Equals(first.Fingerprint, second.Fingerprint, StringComparison.Ordinal)
                && first.RuntimeSummaries.Select(item => item.runtimeName).SequenceEqual(second.RuntimeSummaries.Select(item => item.runtimeName));
            return TestLabAssertions.True("step11-integration-determinism", "Integration snapshots are deterministic and registry-order independent", valid, $"First={first.Fingerprint} Second={second.Fingerprint}");
        }

        private static TestLabAutomationStepResult EconomyIntegrationAccess(TestLabAutomationContext context)
        {
            EconomyIntegrationFacade facade = CreateIntegrationFacade(context);
            EconomicValidationResult result = facade.AuditAccessAndRedaction();
            return TestLabAssertions.True("step11-integration-access", "Economic projections require the Step 8 access runtime", result.Succeeded, result.Summary);
        }

        private static TestLabAutomationStepResult EconomyIntegrationStep12Signals(TestLabAutomationContext context)
        {
            EconomyIntegrationFacade facade = CreateIntegrationFacade(context);
            EconomicSignalContractData signal = facade.CreateStep12SignalContract(context.ScenarioContext.ScopedId("economy-signal", "labor"), EconomicSignalCategory.LaborPressure, context.ScenarioContext.Runtimes.WorldId, 1L, 12d);
            EconomicIntegrationSnapshot snapshot = facade.CreateSnapshot(signal);
            signal.exactValue = 99L;
            bool valid = snapshot.Signals.Count == 1
                && snapshot.Signals[0].step12Ready
                && snapshot.Signals[0].mutationFree
                && snapshot.Signals[0].exactValue == 1L
                && snapshot.Signals[0].dependencyRevisions.Length == 9;
            return TestLabAssertions.True("step11-integration-signals", "Step 12 economic signal contract is immutable and mutation-free", valid, $"Signal={snapshot.Signals[0].signalId} Ready={snapshot.Signals[0].step12Ready} Revisions={snapshot.Signals[0].dependencyRevisions.Length}");
        }

        private static TestLabAutomationStepResult EconomyIntegrationComplete(TestLabAutomationContext context)
        {
            EconomyIntegrationFacade facade = CreateIntegrationFacade(context);
            EconomicValidationResult result = facade.ValidateAll();
            EconomicIntegrationSnapshot snapshot = facade.CreateSnapshot(facade.CreateStep12SignalContract(context.ScenarioContext.ScopedId("economy-signal", "complete"), EconomicSignalCategory.Step12Contract, context.ScenarioContext.Runtimes.WorldId, 0L));
            bool valid = result.Succeeded
                && snapshot.RuntimeSummaries.Count == 9
                && snapshot.Signals.Single().step12Ready;
            return TestLabAssertions.True("step11-integration-complete", "Complete Step 11 integration validates without assuming ownership of external systems", valid, $"Succeeded={result.Succeeded} Snapshot={snapshot.Fingerprint} Diagnostics={result.Summary}");
        }

        private static TestLabAutomationStepResult AmountsAndAccounts(TestLabAutomationContext context)
        {
            if (!TryGetRuntime(context, out EconomyRuntime economy, out CurrencyDefinition gold, out string failure))
            {
                return Fail("step11-economy-amounts", failure);
            }

            MoneyAmount first = new MoneyAmount(gold.Id, 100L);
            MoneyAmount total = first.Add(new MoneyAmount(gold.Id, 25L)).Subtract(new MoneyAmount(gold.Id, 5L));
            string accountId = Account(context, "player-wallet");
            EconomyOperationResult preview = economy.CreateAccount(accountId, gold, context.ScenarioContext.Runtimes.PersonId, EconomyAccountKind.PersonWallet, total.Units, preview: true);
            EconomyOperationResult create = economy.CreateAccount(accountId, gold, context.ScenarioContext.Runtimes.PersonId, EconomyAccountKind.PersonWallet, total.Units, transactionId: Tx(context, "opening"));

            bool valid = total.Units == 120L
                && preview.Succeeded
                && preview.Preview
                && create.Succeeded
                && economy.TryGetAccount(accountId, out EconomyAccountSnapshot account)
                && account.BalanceUnits == 120L
                && account.AvailableUnits == 120L
                && account.CurrencyId == GoldCurrencyId;
            return TestLabAssertions.True("step11-economy-amounts", "Exact monetary amounts and accounts are authoritative", valid, $"Preview={preview.Code} Create={create.Code} Balance={create.ToAccount?.BalanceUnits}");
        }

        private static TestLabAutomationStepResult PaymentsTransfersAndLedger(TestLabAutomationContext context)
        {
            if (!TryGetRuntime(context, out EconomyRuntime economy, out CurrencyDefinition gold, out string failure))
            {
                return Fail("step11-economy-transfer", failure);
            }

            string buyer = Account(context, "buyer");
            string seller = Account(context, "seller");
            economy.CreateAccount(buyer, gold, "person.buyer", EconomyAccountKind.PersonWallet, 100L, Tx(context, "buyer-open"));
            economy.CreateAccount(seller, gold, "person.seller", EconomyAccountKind.PersonWallet, 0L, Tx(context, "seller-open"));
            string transferId = Tx(context, "transfer");
            EconomyOperationResult preview = economy.Transfer(transferId, buyer, seller, new MoneyAmount(gold.Id, 35L), EconomyTransactionKind.Payment, preview: true);
            EconomyOperationResult execute = economy.Transfer(transferId, buyer, seller, new MoneyAmount(gold.Id, 35L), EconomyTransactionKind.Payment);
            EconomyOperationResult duplicate = economy.Transfer(transferId, buyer, seller, new MoneyAmount(gold.Id, 35L), EconomyTransactionKind.Payment);

            economy.TryGetAccount(buyer, out EconomyAccountSnapshot buyerSnapshot);
            economy.TryGetAccount(seller, out EconomyAccountSnapshot sellerSnapshot);
            bool conserved = buyerSnapshot.BalanceUnits + sellerSnapshot.BalanceUnits == 100L;
            bool valid = preview.Succeeded
                && preview.Preview
                && execute.Succeeded
                && duplicate.Succeeded
                && duplicate.Duplicate
                && buyerSnapshot.BalanceUnits == 65L
                && sellerSnapshot.BalanceUnits == 35L
                && execute.Transaction.LedgerEntries.Count == 2
                && conserved;
            return TestLabAssertions.True("step11-economy-transfer", "Payments and transfers commit atomically to a conserved ledger", valid, $"Preview={preview.Code} Execute={execute.Code} Duplicate={duplicate.Code} Buyer={buyerSnapshot?.BalanceUnits} Seller={sellerSnapshot?.BalanceUnits} Ledger={execute.Transaction?.LedgerEntries.Count}");
        }

        private static TestLabAutomationStepResult ReservationsRefundsAndReversals(TestLabAutomationContext context)
        {
            if (!TryGetRuntime(context, out EconomyRuntime economy, out CurrencyDefinition gold, out string failure))
            {
                return Fail("step11-economy-reservations", failure);
            }

            string buyer = Account(context, "reserve-buyer");
            string seller = Account(context, "reserve-seller");
            economy.CreateAccount(buyer, gold, "person.buyer", EconomyAccountKind.PersonWallet, 80L, Tx(context, "reserve-buyer-open"));
            economy.CreateAccount(seller, gold, "person.seller", EconomyAccountKind.PersonWallet, 0L, Tx(context, "reserve-seller-open"));
            string reservationId = context.ScenarioContext.ScopedId("economy-reservation", "deposit");
            EconomyOperationResult reserve = economy.Reserve(reservationId, buyer, new MoneyAmount(gold.Id, 40L), "shop.checkout");
            EconomyOperationResult pay = economy.Transfer(Tx(context, "reservation-pay"), buyer, seller, new MoneyAmount(gold.Id, 40L), EconomyTransactionKind.Payment, reservationId: reservationId);
            EconomyOperationResult refund = economy.Refund(Tx(context, "refund"), pay.Transaction?.TransactionId, actorId: "person.seller");
            EconomyOperationResult reverse = economy.Reverse(Tx(context, "reverse-invalid"), pay.Transaction?.TransactionId, actorId: "person.seller");

            economy.TryGetAccount(buyer, out EconomyAccountSnapshot buyerSnapshot);
            economy.TryGetAccount(seller, out EconomyAccountSnapshot sellerSnapshot);
            bool valid = reserve.Succeeded
                && pay.Succeeded
                && refund.Succeeded
                && !reverse.Succeeded
                && buyerSnapshot.BalanceUnits == 80L
                && sellerSnapshot.BalanceUnits == 0L
                && pay.Reservation?.state == EconomyReservationState.Committed
                && economy.TryGetTransaction(pay.Transaction.TransactionId, out EconomyTransactionSnapshot original)
                && original.State == EconomyTransactionState.Refunded;
            return TestLabAssertions.True("step11-economy-reservations", "Reservations, refunds, and reversals preserve balances", valid, $"Reserve={reserve.Code} Pay={pay.Code} Refund={refund.Code} Reverse={reverse.Code} Buyer={buyerSnapshot?.BalanceUnits} Seller={sellerSnapshot?.BalanceUnits}");
        }

        private static TestLabAutomationStepResult PhysicalCurrencyAndPrices(TestLabAutomationContext context)
        {
            if (!TryGetPhysicalRuntime(context, out EconomyRuntime economy, out CurrencyDefinition currency, out ItemDefinition coin, out string failure))
            {
                return Fail("step11-economy-physical", failure);
            }

            ItemInstanceIdentityRuntime items = context.ScenarioContext.Runtimes.ItemInstances;
            string wallet = Account(context, "physical-wallet");
            economy.CreateAccount(wallet, currency, context.ScenarioContext.Runtimes.PersonId, EconomyAccountKind.PersonWallet, 0L, Tx(context, "physical-open"));
            string coinInstance = RunGuid(context, "coin-input");
            ItemInstanceOperationResult createCoin = items.CreateItem(coin, ItemInstanceClassification.Fungible, coinInstance, ownerPersonId: context.ScenarioContext.Runtimes.PersonId, custodianPersonId: context.ScenarioContext.Runtimes.PersonId);
            EconomyOperationResult toAbstract = economy.ConvertPhysicalToAbstract(Tx(context, "to-abstract"), wallet, currency, items, coinInstance, 3, context.ScenarioContext.Runtimes.PersonId);
            EconomyOperationResult toPhysical = economy.ConvertAbstractToPhysical(Tx(context, "to-physical"), wallet, currency, items, 1, context.ScenarioContext.Runtimes.PersonId, RunGuid(context, "coin-output"));
            EconomyOperationResult price = economy.CaptureFixedPrice(context.ScenarioContext.ScopedId("fixed-price", "sword"), "item.prototype-sword", wallet, new MoneyAmount(currency.Id, 2L), "price-list.prototype.shop", context.ScenarioContext.Runtimes.PersonId, worldTime: 12d);

            items.TryGetSnapshot(coinInstance, out ItemInstanceSnapshot spentCoin);
            economy.TryGetAccount(wallet, out EconomyAccountSnapshot walletSnapshot);
            bool valid = createCoin.Succeeded
                && toAbstract.Succeeded
                && toPhysical.Succeeded
                && price.Succeeded
                && spentCoin.LifecycleState == ItemLifecycleState.Consumed
                && walletSnapshot.BalanceUnits == 2L
                && price.PriceSnapshot.currencyId == currency.Id
                && price.PriceSnapshot.units == 2L;
            return TestLabAssertions.True("step11-economy-physical", "Physical currency conversion and fixed prices are explicit", valid, $"Create={createCoin.Status} ToAbstract={toAbstract.Code} ToPhysical={toPhysical.Code} Price={price.Code} Wallet={walletSnapshot?.BalanceUnits}");
        }

        private static TestLabAutomationStepResult PersistenceProjectionAndValidation(TestLabAutomationContext context)
        {
            if (!TryGetRuntime(context, out EconomyRuntime economy, out CurrencyDefinition gold, out string failure))
            {
                return Fail("step11-economy-persistence", failure);
            }

            string wallet = Account(context, "private-wallet");
            EconomyOperationResult create = economy.CreateAccount(wallet, gold, "person.secret-holder", EconomyAccountKind.PersonWallet, 77L, Tx(context, "private-open"));
            economy.TryGetAccount(wallet, out EconomyAccountSnapshot account);
            InformationAccessRuntime access = context.ScenarioContext.Runtimes.Access;
            string policyId = context.ScenarioContext.ScopedId("information-access-policy", "economy-wallet");
            InformationAccessOperationResult policy = access.RegisterPolicy(new InformationAccessPolicyData
            {
                policyId = policyId,
                subject = account.CreateInformationSubject(),
                classification = InformationVisibilityClassification.Secret,
                detailVisibilityPolicy = InformationDetailVisibilityPolicy.Selected,
                defaultVisibleDetails = new[] { "detail.account", "detail.currency" },
                defaultRedactedDetails = new[] { "detail.owner", "detail.balance", "detail.reserved" },
                redactedAccessAcceptable = true
            }, Tx(context, "policy"));
            InformationAccessOperationResult grant = access.GrantAccess(new InformationAccessGrantData
            {
                grantId = context.ScenarioContext.ScopedId("information-access-grant", "economy-wallet"),
                policyId = policyId,
                subject = account.CreateInformationSubject(),
                granteeKind = InformationGranteeKind.Person,
                granteeId = "person.visitor",
                grantorId = "person.secret-holder",
                accessModes = new[] { InformationAccessMode.Inspect },
                detailIds = new[] { "detail.account", "detail.currency" }
            }, Tx(context, "grant"));
            InformationAccessProjection<EconomyAccountSnapshot> projection = economy.GetAccountProjection(wallet, access, new InformationAccessContext
            {
                RequestingPersonId = "person.visitor",
                HasDiscoveredSubject = true,
                RevealDenialReasons = true
            }, policyId);

            EconomyRuntimeSaveData save = economy.CreateSaveData();
            bool validSave = EconomyRuntime.ValidateSaveData(save, context.ScenarioContext.Runtimes.DefinitionRegistry, out string validFailure);
            EconomyRuntime restored = new EconomyRuntime();
            EconomyOperationResult restore = restored.RestoreFromSaveData(save, context.ScenarioContext.Runtimes.DefinitionRegistry);
            EconomyRuntimeSaveData corrupt = save.Clone();
            corrupt.accounts[0].currencyId = "currency.missing";
            bool rejected = !EconomyRuntime.ValidateSaveData(corrupt, context.ScenarioContext.Runtimes.DefinitionRegistry, out string corruptFailure);

            bool valid = create.Succeeded
                && policy.Succeeded
                && grant.Succeeded
                && projection.Redacted
                && projection.Record.AccountId == wallet
                && projection.Record.BalanceUnits == 0L
                && validSave
                && restore.Succeeded
                && rejected
                && restored.TryGetAccount(wallet, out EconomyAccountSnapshot restoredWallet)
                && restoredWallet.BalanceUnits == 77L;
            return TestLabAssertions.True("step11-economy-persistence", "Persistence and access projections validate economy state", valid, $"Policy={policy.Code} Grant={grant.Code} Redacted={projection.Redacted} Save={validSave}:{validFailure} Restore={restore.Code} Rejected={rejected}:{corruptFailure}");
        }

        private static TestLabAutomationStepResult BusinessFormationOwnershipControl(TestLabAutomationContext context)
        {
            if (!TryGetBusinessRuntime(context, out BusinessRuntime businesses, out _, out CurrencyDefinition gold, out string failure))
            {
                return Fail("step11-business-form", failure);
            }

            string businessId = BusinessId(context, "sole-shop");
            BusinessOperationResult create = businesses.CreateBusiness(new BusinessInstanceData
            {
                businessId = businessId,
                businessDefinitionId = "business.prototype-merchant-shop",
                displayName = "Prototype Merchant Shop",
                linkedOrganizationId = "organization.prototype.independent",
                founderSubjectIds = new[] { context.ScenarioContext.Runtimes.PersonId },
                operatingCurrencyIds = new[] { gold.Id },
                createdWorldTime = 10d,
                state = BusinessState.Planned
            });
            BusinessOperationResult owner = businesses.AddOwnership(new BusinessOwnershipRecordData
            {
                ownershipRecordId = BusinessId(context, "sole-owner"),
                businessId = businessId,
                owner = new BusinessSubjectReferenceData { kind = BusinessOwnerSubjectKind.Person, subjectId = context.ScenarioContext.Runtimes.PersonId },
                category = BusinessOwnershipCategory.SoleOwner,
                economicShare = new BusinessRationalData { numerator = 10000L, denominator = 10000L },
                votingShare = new BusinessRationalData { numerator = 10000L, denominator = 10000L },
                effectiveStartWorldTime = 10d
            }, 10d);
            BusinessOperationResult control = businesses.AssignController(new BusinessControlRecordData
            {
                controlRecordId = BusinessId(context, "controller"),
                businessId = businessId,
                controllerSubjectId = "position.prototype.shop-manager",
                authorityKinds = new[] { BusinessAuthorityKind.ViewBusinessState, BusinessAuthorityKind.ManageInventory, BusinessAuthorityKind.SellStock },
                effectiveStartWorldTime = 10d,
                sourceReferenceId = "position.prototype.shop-manager"
            }, 10d);
            BusinessOperationResult active = businesses.TransitionBusiness(businessId, BusinessState.Active, 12d);
            bool valid = create.Succeeded
                && owner.Succeeded
                && control.Succeeded
                && active.Succeeded
                && businesses.TryGetBusiness(businessId, out BusinessInstanceData snapshot)
                && snapshot.state == BusinessState.Active
                && snapshot.controllerSubjectId == "position.prototype.shop-manager"
                && businesses.OwnershipRecords.Count(record => record.businessId == businessId) == 1;
            return TestLabAssertions.True("step11-business-form", "Businesses form with exact ownership and separate control", valid, $"Create={create.Code} Owner={owner.Code} Control={control.Code} Active={active.Code}");
        }

        private static TestLabAutomationStepResult BusinessEstablishmentsAccountsInventoryStock(TestLabAutomationContext context)
        {
            if (!PrepareBusinessFixture(context, out BusinessRuntime businesses, out EconomyRuntime economy, out ItemInstanceIdentityRuntime items, out CurrencyDefinition gold, out string businessId, out string failure))
            {
                return Fail("step11-business-stock", failure);
            }

            string accountId = Account(context, "business-operating");
            economy.CreateAccount(accountId, gold, businessId, EconomyAccountKind.OrganizationAccount, 500L, Tx(context, "business-open"));
            string establishmentId = BusinessId(context, "stall");
            BusinessOperationResult establishment = businesses.AddEstablishment(new BusinessEstablishmentData
            {
                establishmentId = establishmentId,
                businessId = businessId,
                type = BusinessEstablishmentType.Stall,
                displayName = "Prototype Stall",
                state = BusinessEstablishmentState.Open,
                openedWorldTime = 20d,
                locationReferenceId = "location.prototype.market"
            });
            BusinessOperationResult account = businesses.AssignAccount(new BusinessAccountAssignmentData
            {
                assignmentId = BusinessId(context, "account-operating"),
                businessId = businessId,
                accountId = accountId,
                purpose = BusinessAccountPurpose.OperatingFunds,
                establishmentId = establishmentId,
                authorizedSpenderSubjectIds = new[] { context.ScenarioContext.Runtimes.PersonId },
                effectiveStartWorldTime = 20d
            }, economy);
            BusinessOperationResult inventory = businesses.AssignInventory(new BusinessInventoryAssignmentData
            {
                assignmentId = BusinessId(context, "inventory-retail"),
                businessId = businessId,
                inventoryId = "inventory.prototype.business.retail",
                establishmentId = establishmentId,
                purpose = BusinessInventoryPurpose.RetailStock,
                responsibleCustodianSubjectId = context.ScenarioContext.Runtimes.PersonId,
                effectiveStartWorldTime = 20d
            });
            ItemDefinition sword = CreateItemDefinition("item.business.prototype-sword", "Business Prototype Sword");
            string itemId = RunGuid(context, "business-stock-item");
            ItemInstanceOperationResult item = items.CreateItem(sword, ItemInstanceClassification.IndividuallyTracked, itemId, ownerPersonId: context.ScenarioContext.Runtimes.PersonId, custodianPersonId: context.ScenarioContext.Runtimes.PersonId);
            BusinessOperationResult stock = businesses.ClassifyStock(new BusinessStockClassificationData
            {
                stockClassificationId = BusinessId(context, "stock"),
                businessId = businessId,
                establishmentId = establishmentId,
                inventoryId = "inventory.prototype.business.retail",
                itemInstanceId = itemId,
                category = BusinessStockCategory.ForSale,
                saleEligible = true,
                productionEligible = false
            }, items);

            items.TryGetSnapshot(itemId, out ItemInstanceSnapshot itemAfter);
            bool valid = establishment.Succeeded
                && account.Succeeded
                && inventory.Succeeded
                && item.Succeeded
                && stock.Succeeded
                && itemAfter.OwnerPersonId == context.ScenarioContext.Runtimes.PersonId
                && businesses.AccountAssignments.Count == 1
                && businesses.InventoryAssignments.Count == 1
                && businesses.StockClassifications.Count == 1;
            return TestLabAssertions.True("step11-business-stock", "Establishments, accounts, inventory assignments, and stock remain references", valid, $"Establishment={establishment.Code} Account={account.Code} Inventory={inventory.Code} Stock={stock.Code} Owner={itemAfter?.OwnerPersonId}");
        }

        private static TestLabAutomationStepResult BusinessProductionFinancialStatements(TestLabAutomationContext context)
        {
            if (!PrepareBusinessFixture(context, out BusinessRuntime businesses, out EconomyRuntime economy, out _, out CurrencyDefinition gold, out string businessId, out string failure))
            {
                return Fail("step11-business-accounting", failure);
            }

            ProductionWorkflowRuntime workflow = context.ScenarioContext.Runtimes.ProductionWorkflow;
            DefinitionRegistry registry = BusinessRegistry(context, out _);
            string recipeId = "recipe.prototype.business-output";
            RecipeDefinition recipe = ScriptableObject.CreateInstance<RecipeDefinition>();
            SetPrivate(recipe, "recipeId", recipeId);
            SetPrivate(recipe, "displayName", "Prototype Business Output");
            SetPrivate(recipe, "category", RecipeCategory.Crafting);
            SetPrivate(recipe, "currentVersionId", "v1");
            registry = ExtendRegistry(registry, recipe);
            string workOrderId = BusinessId(context, "work-order");
            ProductionWorkflowResult order = workflow.CreateWorkOrder(new ProductionWorkOrderData
            {
                workOrderId = workOrderId,
                requesterPersonId = context.ScenarioContext.Runtimes.PersonId,
                recipeDefinitionId = recipeId,
                requestedQuantity = 1,
                ownerPersonId = context.ScenarioContext.Runtimes.PersonId,
                custodianPersonId = context.ScenarioContext.Runtimes.PersonId,
                state = ProductionWorkOrderState.Approved
            }, registry);
            string jobId = BusinessId(context, "production-job");
            ProductionWorkflowResult job = workflow.CreateJobFromWorkOrder(jobId, workOrderId, registry);
            string account = Account(context, "business-accounting");
            string customer = Account(context, "business-customer");
            string vendor = Account(context, "business-vendor");
            string ownerAccount = Account(context, "business-owner");
            economy.CreateAccount(account, gold, businessId, EconomyAccountKind.OrganizationAccount, 0L, Tx(context, "business-accounting-open"));
            economy.CreateAccount(customer, gold, "person.customer", EconomyAccountKind.PersonWallet, 200L, Tx(context, "customer-open"));
            economy.CreateAccount(vendor, gold, "person.vendor", EconomyAccountKind.PersonWallet, 0L, Tx(context, "vendor-open"));
            economy.CreateAccount(ownerAccount, gold, context.ScenarioContext.Runtimes.PersonId, EconomyAccountKind.PersonWallet, 50L, Tx(context, "owner-open"));
            EconomyOperationResult sale = economy.Transfer(Tx(context, "sale"), customer, account, new MoneyAmount(gold.Id, 120L), EconomyTransactionKind.Payment);
            EconomyOperationResult expensePayment = economy.Transfer(Tx(context, "expense-source"), account, vendor, new MoneyAmount(gold.Id, 30L), EconomyTransactionKind.Payment);
            EconomyOperationResult capitalPayment = economy.Transfer(Tx(context, "capital-source"), ownerAccount, account, new MoneyAmount(gold.Id, 50L), EconomyTransactionKind.Transfer);
            BusinessOperationResult production = businesses.SponsorProduction(new BusinessProductionOwnershipData
            {
                productionOwnershipId = BusinessId(context, "production-owner"),
                businessId = businessId,
                productionJobId = jobId,
                productionSponsorSubjectId = businessId,
                inputOwnerSubjectId = businessId,
                outputOwnerPolicy = ProductionOutputOwnerPolicy.BusinessOwnsOutputs,
                responsibleProducerSubjectId = context.ScenarioContext.Runtimes.PersonId,
                fundingAccountId = account,
                inputInventoryIds = new[] { "inventory.prototype.business.inputs" },
                outputInventoryIds = new[] { "inventory.prototype.business.finished" }
            }, workflow, economy);
            BusinessOperationResult revenue = businesses.RecordRevenue(new BusinessRevenueRecordData
            {
                revenueRecordId = BusinessId(context, "revenue"),
                businessId = businessId,
                category = BusinessRevenueCategory.RetailSale,
                amount = BusinessModelHelpers.Money(gold.Id, 120L),
                transactionId = sale.Transaction.TransactionId,
                recognitionWorldTime = 40d
            }, economy);
            BusinessOperationResult expense = businesses.RecordExpense(new BusinessExpenseRecordData
            {
                expenseRecordId = BusinessId(context, "expense"),
                businessId = businessId,
                category = BusinessExpenseCategory.MaterialPurchase,
                amount = BusinessModelHelpers.Money(gold.Id, 30L),
                transactionId = expensePayment.Transaction.TransactionId,
                productionJobId = jobId,
                recognitionWorldTime = 45d
            }, economy);
            BusinessOperationResult capital = businesses.AddCapitalContribution(new BusinessCapitalContributionData
            {
                contributionId = BusinessId(context, "capital"),
                businessId = businessId,
                contributingSubjectId = context.ScenarioContext.Runtimes.PersonId,
                monetaryValue = BusinessModelHelpers.Money(gold.Id, 50L),
                transactionOrTransferReferenceId = capitalPayment.Transaction.TransactionId,
                worldTime = 35d
            }, economy);
            BusinessOperationResult period = businesses.OpenAccountingPeriod(new BusinessAccountingPeriodData
            {
                accountingPeriodId = BusinessId(context, "period"),
                businessId = businessId,
                currencyId = gold.Id,
                startWorldTime = 0d,
                endWorldTime = 100d
            });
            BusinessOperationResult close = businesses.CloseAccountingPeriod(BusinessId(context, "period"), BusinessId(context, "pnl"), BusinessId(context, "cashflow"), 100d);

            bool valid = order.Succeeded
                && job.Succeeded
                && sale.Succeeded
                && expensePayment.Succeeded
                && capitalPayment.Succeeded
                && production.Succeeded
                && revenue.Succeeded
                && expense.Succeeded
                && capital.Succeeded
                && period.Succeeded
                && close.Succeeded
                && close.ProfitAndLossStatement.netOperatingResult.units == 90L
                && close.CashFlowSummary.netCashChange.units == 140L;
            return TestLabAssertions.True("step11-business-accounting", "Production ownership, revenue, expenses, profit, and cash flow remain distinct", valid, $"Order={order.Status} Job={job.Status} Production={production.Code} Revenue={revenue.Code} Expense={expense.Code} PnL={close.ProfitAndLossStatement?.netOperatingResult.units} Cash={close.CashFlowSummary?.netCashChange.units}");
        }

        private static TestLabAutomationStepResult BusinessAccessPersistenceRollback(TestLabAutomationContext context)
        {
            if (!PrepareBusinessFixture(context, out BusinessRuntime businesses, out EconomyRuntime economy, out ItemInstanceIdentityRuntime _, out CurrencyDefinition gold, out string businessId, out string failure))
            {
                return Fail("step11-business-persist", failure);
            }

            BusinessRuntimeSaveData before = businesses.CreateSaveData();
            BusinessOperationResult invalidExpense = businesses.RecordExpense(new BusinessExpenseRecordData
            {
                expenseRecordId = BusinessId(context, "invalid-expense"),
                businessId = businessId,
                category = BusinessExpenseCategory.OwnerWithdrawalExclusion,
                amount = BusinessModelHelpers.Money(gold.Id, 25L),
                transactionId = "missing.transaction",
                recognitionWorldTime = 50d
            }, economy);
            bool noMutation = before.revision == businesses.Revision && before.expenseRecords.Length == businesses.ExpenseRecords.Count;
            InformationAccessRuntime access = context.ScenarioContext.Runtimes.Access;
            businesses.TryGetBusiness(businessId, out BusinessInstanceData business);
            access.RegisterPolicy(new InformationAccessPolicyData
            {
                policyId = BusinessId(context, "fixture-policy"),
                subject = business.CreateInformationSubject(),
                classification = InformationVisibilityClassification.Secret,
                detailVisibilityPolicy = InformationDetailVisibilityPolicy.Selected,
                defaultVisibleDetails = new[] { "business.public" },
                defaultRedactedDetails = new[] { "business.owners", "business.control", "business.accounts", "business.financials" },
                redactedAccessAcceptable = true
            }, Tx(context, "business-access-policy"));
            access.GrantAccess(new InformationAccessGrantData
            {
                grantId = BusinessId(context, "fixture-grant"),
                policyId = BusinessId(context, "fixture-policy"),
                subject = business.CreateInformationSubject(),
                granteeKind = InformationGranteeKind.Person,
                granteeId = "person.prototype.viewer",
                grantorId = context.ScenarioContext.Runtimes.PersonId,
                accessModes = new[] { InformationAccessMode.Query },
                detailIds = new[] { "business.public" }
            }, Tx(context, "business-access-grant"));
            BusinessProjection projection = businesses.ProjectBusiness(businessId, access, new InformationAccessContext
            {
                RequestingPersonId = "person.prototype.viewer",
                HasDiscoveredSubject = true,
                RevealDenialReasons = true
            }, BusinessProjectionKind.Public);
            BusinessRuntime restored = new BusinessRuntime();
            BusinessOperationResult restore = restored.RestoreFromSaveData(businesses.CreateSaveData(), BusinessRegistry(context, out _));
            bool valid = !invalidExpense.Succeeded
                && invalidExpense.Code == BusinessOperationCode.PolicyViolation
                && noMutation
                && projection != null
                && !projection.Denied
                && projection.Redacted
                && projection.Business.founderSubjectIds.Length == 0
                && restore.Succeeded
                && restored.BusinessCount == businesses.BusinessCount;
            return TestLabAssertions.True("step11-business-persist", "Access projections, persistence, and failed operations preserve state", valid, $"Invalid={invalidExpense.Code} NoMutation={noMutation} ProjectionDenied={projection?.Denied} Restore={restore.Code}");
        }

        private static TestLabAutomationStepResult MarketSupplyDemandScarcity(TestLabAutomationContext context)
        {
            if (!TryGetMarketFixture(context, out MarketRuntime markets, out _, out MarketDefinition marketDefinition, out MarketSubjectDefinition subject, out _, out string failure))
            {
                return Fail("step11-markets-scarcity", failure);
            }

            string marketId = MarketId(context, "village");
            MarketOperationResult create = markets.CreateMarketInstance(marketDefinition, marketId, "region.prototype.village");
            MarketOperationResult supply = markets.RecordSupply(Supply(context, marketId, subject.Id, "granary", 120L, 40L, 90L, 1d));
            MarketOperationResult duplicateSource = markets.RecordSupply(Supply(context, marketId, subject.Id, "granary", 120L, 0L, 120L, 2d));
            MarketOperationResult expired = markets.RecordSupply(Supply(context, marketId, subject.Id, "old-cart", 30L, 0L, 30L, 0d, expires: 1d));
            MarketOperationResult demand = markets.RecordDemand(Demand(context, marketId, subject.Id, "villagers", 150L, 30L, 3d));
            MarketOperationResult scarcity = markets.EvaluateScarcity(Scoped(context, "market-scarcity", "main"), marketId, subject.Id, 3d);

            bool valid = create.Succeeded
                && supply.Succeeded
                && !duplicateSource.Succeeded
                && expired.Succeeded
                && demand.Succeeded
                && scarcity.Succeeded
                && scarcity.Scarcity.availableSupply == 90L
                && scarcity.Scarcity.currentDemand == 150L
                && scarcity.Scarcity.scarcityClass == MarketScarcityClass.Scarce;
            return TestLabAssertions.True("step11-markets-scarcity", "Supply and demand observations produce deterministic scarcity", valid, $"Create={create.Code} Supply={supply.Code} Duplicate={duplicateSource.Code} Expired={expired.Code} Demand={demand.Code} Scarcity={scarcity.Scarcity?.scarcityClass} Available={scarcity.Scarcity?.availableSupply} Demand={scarcity.Scarcity?.currentDemand}");
        }

        private static TestLabAutomationStepResult MarketReferencePrices(TestLabAutomationContext context)
        {
            if (!TryGetMarketFixture(context, out MarketRuntime markets, out _, out MarketDefinition marketDefinition, out MarketSubjectDefinition subject, out _, out string failure))
            {
                return Fail("step11-markets-prices", failure);
            }

            string scarceMarket = MarketId(context, "scarce");
            string abundantMarket = MarketId(context, "abundant");
            markets.CreateMarketInstance(marketDefinition, scarceMarket, "region.prototype.mountains");
            markets.CreateMarketInstance(marketDefinition, abundantMarket, "region.prototype.farms");
            markets.RecordSupply(Supply(context, scarceMarket, subject.Id, "scarce-source", 30L, 0L, 30L, 1d));
            markets.RecordDemand(Demand(context, scarceMarket, subject.Id, "scarce-demand", 120L, 0L, 1d));
            markets.RecordSupply(Supply(context, abundantMarket, subject.Id, "abundant-source", 200L, 0L, 200L, 1d));
            markets.RecordDemand(Demand(context, abundantMarket, subject.Id, "abundant-demand", 20L, 0L, 1d));

            MarketOperationResult scarcePrice = markets.UpdateMarketSubject(scarceMarket, subject.Id, 5d);
            MarketOperationResult duplicate = markets.UpdateMarketSubject(scarceMarket, subject.Id, 5d);
            MarketOperationResult abundantPrice = markets.UpdateMarketSubject(abundantMarket, subject.Id, 5d);
            IReadOnlyList<MarketPriceRecordData> history = markets.QueryPriceHistory(scarceMarket, subject.Id);

            bool valid = scarcePrice.Succeeded
                && duplicate.Succeeded
                && duplicate.Duplicate
                && abundantPrice.Succeeded
                && scarcePrice.Price.referenceAmountUnits > subject.BaselinePriceUnits
                && abundantPrice.Price.referenceAmountUnits < subject.BaselinePriceUnits
                && scarcePrice.Price.referenceAmountUnits > abundantPrice.Price.referenceAmountUnits
                && history.Count == 1;
            return TestLabAssertions.True("step11-markets-prices", "Reference prices use regional scarcity and immutable history", valid, $"Scarce={scarcePrice.Price?.referenceAmountUnits} Abundant={abundantPrice.Price?.referenceAmountUnits} Duplicate={duplicate.Duplicate} History={history.Count}");
        }

        private static TestLabAutomationStepResult MarketMerchantQuotes(TestLabAutomationContext context)
        {
            if (!TryGetMarketFixture(context, out MarketRuntime markets, out _, out MarketDefinition marketDefinition, out MarketSubjectDefinition subject, out _, out string failure))
            {
                return Fail("step11-markets-quotes", failure);
            }

            string marketId = MarketId(context, "quotes");
            markets.CreateMarketInstance(marketDefinition, marketId, "region.prototype.shop");
            markets.RecordSupply(Supply(context, marketId, subject.Id, "merchant-stock", 20L, 0L, 20L, 1d));
            markets.RecordDemand(Demand(context, marketId, subject.Id, "buyers", 20L, 0L, 1d));
            MarketOperationResult price = markets.UpdateMarketSubject(marketId, subject.Id, 2d);
            ItemInstanceSnapshot item = new ItemInstanceSnapshot(new ItemInstanceRecordData
            {
                itemInstanceId = Scoped(context, "item-instance", "quote-sword"),
                itemDefinitionId = PrototypeSwordItemId,
                labels = new ItemIdentityLabelData { makerMark = "maker.secret" },
                revision = 1L
            });
            ItemQualitySnapshot quality = new ItemQualitySnapshot(new ItemQualityRecordData { itemInstanceId = item.ItemInstanceId, overallQuality = 0.8f, qualityTierId = "quality.fine" });
            ItemDurabilitySnapshot durability = new ItemDurabilitySnapshot(new ItemDurabilityRecordData { itemInstanceId = item.ItemInstanceId, currentDurability = 50f, maximumDurability = 100f });

            MarketOperationResult preview = markets.CreateMerchantQuote(Scoped(context, "quote", "preview"), "merchant.prototype", marketId, subject.Id, MerchantQuoteDirection.MerchantSells, 1L, 3d, 7d, item: item, preview: true, quality: quality, durability: durability);
            MarketOperationResult sell = markets.CreateMerchantQuote(Scoped(context, "quote", "sell"), "merchant.prototype", marketId, subject.Id, MerchantQuoteDirection.MerchantSells, 1L, 3d, 7d, item: item, quality: quality, durability: durability);
            MarketOperationResult buy = markets.CreateMerchantQuote(Scoped(context, "quote", "buy"), "merchant.prototype", marketId, subject.Id, MerchantQuoteDirection.MerchantBuys, 1L, 3d, 7d, item: item, quality: quality, durability: durability);
            MarketOperationResult hidden = markets.CreateMerchantQuote(Scoped(context, "quote", "hidden"), "merchant.prototype", marketId, subject.Id, MerchantQuoteDirection.MerchantSells, 1L, 3d, 7d, item: item, privilegedHiddenFactors: true, quality: quality, durability: durability);
            bool validNow = markets.ValidateQuoteForExecution(sell.Quote.quoteId, 4d, out _);
            bool expired = !markets.ValidateQuoteForExecution(sell.Quote.quoteId, 8d, out string expiredReason);

            bool valid = price.Succeeded
                && preview.Succeeded
                && preview.Preview
                && sell.Succeeded
                && buy.Succeeded
                && hidden.Succeeded
                && buy.Quote.finalAmountUnits < sell.Quote.finalAmountUnits
                && hidden.Quote.finalAmountUnits > sell.Quote.finalAmountUnits
                && !sell.Quote.hiddenFactorsApplied
                && hidden.Quote.hiddenFactorsApplied
                && markets.QuoteCount == 3
                && validNow
                && expired;
            return TestLabAssertions.True("step11-markets-quotes", "Merchant quotes apply margins and item adjustments without trade mutation", valid, $"Price={price.Code} Preview={preview.Code} Sell={sell.Quote?.finalAmountUnits} Buy={buy.Quote?.finalAmountUnits} Hidden={hidden.Quote?.finalAmountUnits} Count={markets.QuoteCount} Expired={expired}:{expiredReason}");
        }

        private static TestLabAutomationStepResult MarketPersistenceAndProjection(TestLabAutomationContext context)
        {
            if (!TryGetMarketFixture(context, out MarketRuntime markets, out DefinitionRegistry registry, out MarketDefinition marketDefinition, out MarketSubjectDefinition subject, out CurrencyDefinition gold, out string failure))
            {
                return Fail("step11-markets-persistence", failure);
            }

            string marketId = MarketId(context, "persist");
            markets.CreateMarketInstance(marketDefinition, marketId, "region.prototype.private");
            markets.RecordSupply(Supply(context, marketId, subject.Id, "private-stock", 10L, 0L, 10L, 1d));
            markets.RecordDemand(Demand(context, marketId, subject.Id, "private-demand", 10L, 0L, 1d));
            MarketOperationResult price = markets.UpdateMarketSubject(marketId, subject.Id, 2d);

            EconomyRuntime economy = context.ScenarioContext.Runtimes.Economy;
            economy.Configure(registry, context.ScenarioContext.Runtimes.WorldId);
            string buyer = Account(context, "market-buyer");
            string seller = Account(context, "market-seller");
            economy.CreateAccount(buyer, gold, "person.buyer", EconomyAccountKind.PersonWallet, 500L, Tx(context, "market-buyer"));
            economy.CreateAccount(seller, gold, "person.seller", EconomyAccountKind.PersonWallet, 0L, Tx(context, "market-seller"));
            EconomyOperationResult transfer = economy.Transfer(Tx(context, "market-observed"), buyer, seller, new MoneyAmount(gold.Id, price.Price.referenceAmountUnits), EconomyTransactionKind.Payment);
            MarketOperationResult observation = markets.AddTransactionObservation(Scoped(context, "market-transaction-observation", "sale"), transfer.Transaction, marketId, subject.Id, MarketTransactionObservationPolicy.IncludeCommitted, publicObservation: true, worldTime: 3d);

            InformationAccessRuntime access = context.ScenarioContext.Runtimes.Access;
            string policyId = Scoped(context, "information-access-policy", "market-price");
            access.RegisterPolicy(new InformationAccessPolicyData
            {
                policyId = policyId,
                subject = price.Price.CreateInformationSubject(),
                classification = InformationVisibilityClassification.Secret,
                detailVisibilityPolicy = InformationDetailVisibilityPolicy.Selected,
                defaultVisibleDetails = new[] { "detail.market", "detail.subject", "detail.reference-price" },
                defaultRedactedDetails = new[] { "detail.supply", "detail.demand", "detail.scarcity", "detail.source" },
                redactedAccessAcceptable = true
            }, Tx(context, "market-policy"));
            access.GrantAccess(new InformationAccessGrantData
            {
                grantId = Scoped(context, "information-access-grant", "market-price"),
                policyId = policyId,
                subject = price.Price.CreateInformationSubject(),
                granteeKind = InformationGranteeKind.Person,
                granteeId = "person.viewer",
                grantorId = "merchant.prototype",
                accessModes = new[] { InformationAccessMode.Inspect },
                detailIds = new[] { "detail.market", "detail.subject", "detail.reference-price" }
            }, Tx(context, "market-grant"));
            MarketProjection<MarketPriceRecordData> projection = markets.GetPriceProjection(price.Price.marketPriceId, access, new InformationAccessContext
            {
                RequestingPersonId = "person.viewer",
                HasDiscoveredSubject = true,
                RevealDenialReasons = true
            }, policyId);

            MarketRuntimeSaveData save = markets.CreateSaveData();
            bool validSave = MarketRuntime.ValidateSaveData(save, registry, out string validFailure);
            MarketRuntime restored = new MarketRuntime();
            MarketOperationResult restore = restored.RestoreFromSaveData(save, registry);
            MarketRuntimeSaveData corrupt = save.Clone();
            corrupt.currentPrices[0].marketPriceId = "market-price.missing";
            bool rejected = !MarketRuntime.ValidateSaveData(corrupt, registry, out string corruptFailure);

            bool valid = price.Succeeded
                && transfer.Succeeded
                && observation.Succeeded
                && projection.Succeeded
                && projection.Redacted
                && projection.Record.supplyAvailable == 0L
                && validSave
                && restore.Succeeded
                && rejected
                && restored.TryGetCurrentPrice(marketId, subject.Id, out MarketPriceRecordData restoredPrice)
                && restoredPrice.referenceAmountUnits == price.Price.referenceAmountUnits;
            return TestLabAssertions.True("step11-markets-persistence", "Transaction observations, persistence, and projections are explicit", valid, $"Price={price.Code} Transfer={transfer.Code} Observation={observation.Code} Redacted={projection.Redacted} Save={validSave}:{validFailure} Restore={restore.Code} Rejected={rejected}:{corruptFailure}");
        }

        private static TestLabAutomationStepResult TradeOffersAndCounteroffers(TestLabAutomationContext context)
        {
            if (!TryGetTradeFixture(context, out TradeFixture fixture, out string failure))
            {
                return Fail("step11-trade-offers", failure);
            }

            TradeOperationResult open = fixture.OpenSession("offers", out string sessionId);
            TradeOperationResult offer = fixture.Trades.SubmitOffer(sessionId, fixture.BuildMoneyForItemOffer(sessionId, "offer.initial", 30L), Tx(context, "trade-offer"));
            TradeOperationResult counter = fixture.Trades.SubmitCounteroffer(sessionId, offer.Offer.offerId, fixture.BuildMoneyForItemOffer(sessionId, "offer.counter", 25L, proposer: "participant.seller", responder: "participant.buyer"), Tx(context, "trade-counter"));
            TradeOperationResult reject = fixture.Trades.RejectOffer(sessionId, counter.Offer.offerId, "participant.buyer", 3d, Tx(context, "trade-reject"));
            TradeOperationResult secondOpen = fixture.OpenSession("withdraw", out string withdrawSession);
            TradeOperationResult withdrawOffer = fixture.Trades.SubmitOffer(withdrawSession, fixture.BuildMoneyForItemOffer(withdrawSession, "offer.withdraw", 20L), Tx(context, "trade-withdraw-offer"));
            TradeOperationResult withdraw = fixture.Trades.WithdrawOffer(withdrawSession, withdrawOffer.Offer.offerId, withdrawOffer.Offer.proposingParticipantId, 4d, Tx(context, "trade-withdraw"));
            TradeOperationResult thirdOpen = fixture.OpenSession("expire", out string expireSession);
            TradeOperationResult expiringOffer = fixture.Trades.SubmitOffer(expireSession, fixture.BuildMoneyForItemOffer(expireSession, "offer.expire", 20L), Tx(context, "trade-expire-offer"));
            TradeOperationResult expire = fixture.Trades.ExpireOffer(expireSession, expiringOffer.Offer.offerId, 200d, Tx(context, "trade-expire"));

            fixture.Trades.TryGetOffer(offer.Offer.offerId, out TradeOfferData original);
            bool valid = open.Succeeded
                && offer.Succeeded
                && counter.Succeeded
                && original.state == TradeOfferState.Superseded
                && reject.Succeeded
                && reject.Session.state == TradeSessionState.Rejected
                && secondOpen.Succeeded
                && withdraw.Succeeded
                && withdraw.Session.state == TradeSessionState.Withdrawn
                && thirdOpen.Succeeded
                && expire.Succeeded
                && expire.Session.state == TradeSessionState.Expired;
            return TestLabAssertions.True("step11-trade-offers", "Trade sessions support offers, counteroffers, rejection, withdrawal, and expiry", valid, $"Open={open.Code} Offer={offer.Code} Counter={counter.Code} Original={original?.state} Reject={reject.Code} Withdraw={withdraw.Code} WithdrawProposer={withdrawOffer.Offer?.proposingParticipantId} Expire={expire.Code}");
        }

        private static TestLabAutomationStepResult TradeFixedPricePurchase(TestLabAutomationContext context)
        {
            if (!TryGetTradeFixture(context, out TradeFixture fixture, out string failure))
            {
                return Fail("step11-trade-purchase", failure);
            }

            TradeOperationResult open = fixture.OpenSession("purchase", out string sessionId);
            TradeOfferData offerData = fixture.BuildMoneyForItemOffer(sessionId, "offer.purchase", 40L, quoteId: fixture.CreateQuote("quote.purchase", 40L));
            TradeOperationResult offer = fixture.Trades.SubmitOffer(sessionId, offerData, Tx(context, "trade-purchase-offer"));
            TradeOperationResult reserve = fixture.Trades.ReserveOfferAssets(offer.Offer.offerId, fixture.Economy, fixture.Items, 2d, Tx(context, "trade-purchase-reserve"));
            TradeOperationResult accept = fixture.Trades.AcceptOffer(sessionId, offer.Offer.offerId, "participant.seller", 3d, Tx(context, "trade-purchase-accept"));
            TradeOperationResult execute = fixture.Trades.ExecuteAcceptedDeal(sessionId, fixture.Economy, fixture.Items, fixture.Markets, fixture.Registry, 4d, Tx(context, "trade-purchase-execute"));
            TradeOperationResult duplicate = fixture.Trades.ExecuteAcceptedDeal(sessionId, fixture.Economy, fixture.Items, fixture.Markets, fixture.Registry, 4d, Tx(context, "trade-purchase-execute"));

            fixture.Economy.TryGetAccount(fixture.BuyerAccount, out EconomyAccountSnapshot buyer);
            fixture.Economy.TryGetAccount(fixture.SellerAccount, out EconomyAccountSnapshot seller);
            fixture.Items.TryGetSnapshot(fixture.SwordInstanceId, out ItemInstanceSnapshot sword);
            bool valid = open.Succeeded
                && offer.Succeeded
                && reserve.Succeeded
                && accept.Succeeded
                && execute.Succeeded
                && duplicate.Succeeded
                && duplicate.Duplicate
                && buyer.BalanceUnits == 60L
                && seller.BalanceUnits == 40L
                && sword.OwnerPersonId == fixture.BuyerPersonId
                && execute.TradeRecord != null
                && execute.Receipt != null
                && fixture.Trades.TradeRecordCount == 1
                && fixture.Trades.ReceiptCount == 1;
            return TestLabAssertions.True("step11-trade-purchase", "Fixed-price purchases execute money and item ownership atomically", valid, $"Offer={offer.Code} Reserve={reserve.Code} Accept={accept.Code} Execute={execute.Code} Duplicate={duplicate.Code} Buyer={buyer?.BalanceUnits} Seller={seller?.BalanceUnits} Owner={sword?.OwnerPersonId}");
        }

        private static TestLabAutomationStepResult TradeBarterReservationRollback(TestLabAutomationContext context)
        {
            if (!TryGetTradeFixture(context, out TradeFixture fixture, out string failure))
            {
                return Fail("step11-trade-barter", failure);
            }

            TradeOperationResult open = fixture.OpenSession("barter", out string sessionId);
            TradeOperationResult offer = fixture.Trades.SubmitOffer(sessionId, fixture.BuildBarterOffer(sessionId, "offer.barter"), Tx(context, "trade-barter-offer"));
            TradeOperationResult reserve = fixture.Trades.ReserveOfferAssets(offer.Offer.offerId, fixture.Economy, fixture.Items, 2d, Tx(context, "trade-barter-reserve"));
            TradeOperationResult accept = fixture.Trades.AcceptOffer(sessionId, offer.Offer.offerId, "participant.seller", 3d, Tx(context, "trade-barter-accept"));
            ItemInstanceRuntimeSaveData beforeItems = fixture.Items.CreateSaveData();
            EconomyRuntimeSaveData beforeEconomy = fixture.Economy.CreateSaveData();
            TradeOperationResult failed = fixture.Trades.ExecuteAcceptedDeal(sessionId, fixture.Economy, fixture.Items, fixture.Markets, fixture.Registry, 4d, Tx(context, "trade-barter-fail"), injectFailureStage: "after-money");
            bool rolledBack = JsonUtility.ToJson(beforeItems) == JsonUtility.ToJson(fixture.Items.CreateSaveData())
                && JsonUtility.ToJson(beforeEconomy) == JsonUtility.ToJson(fixture.Economy.CreateSaveData());
            TradeOperationResult execute = fixture.Trades.ExecuteAcceptedDeal(sessionId, fixture.Economy, fixture.Items, fixture.Markets, fixture.Registry, 5d, Tx(context, "trade-barter-execute"));

            fixture.Items.TryGetSnapshot(fixture.SwordInstanceId, out ItemInstanceSnapshot sword);
            fixture.Items.TryGetSnapshot(fixture.HerbStackId, out ItemInstanceSnapshot herbs);
            bool valid = open.Succeeded
                && offer.Succeeded
                && reserve.Succeeded
                && accept.Succeeded
                && !failed.Succeeded
                && rolledBack
                && execute.Succeeded
                && sword.OwnerPersonId == fixture.BuyerPersonId
                && herbs.StackQuantity == 3
                && fixture.Items.QueryByDefinition(fixture.Herb.Id).Any(item => item.OwnerPersonId == fixture.SellerPersonId && item.StackQuantity == 2);
            return TestLabAssertions.True("step11-trade-barter", "Barter reservations and rollback preserve money and item state", valid, $"Offer={offer.Code} Reserve={reserve.Code} Failed={failed.Code} RolledBack={rolledBack} Execute={execute.Code} SwordOwner={sword?.OwnerPersonId} Herbs={herbs?.StackQuantity}");
        }

        private static TestLabAutomationStepResult TradeValuationPersistenceProjection(TestLabAutomationContext context)
        {
            if (!TryGetTradeFixture(context, out TradeFixture fixture, out string failure))
            {
                return Fail("step11-trade-persistence", failure);
            }

            TradeOperationResult open = fixture.OpenSession("project", out string sessionId);
            TradeOfferData offerData = fixture.BuildMoneyForItemOffer(sessionId, "offer.project", 45L);
            TradeOperationResult offer = fixture.Trades.SubmitOffer(sessionId, offerData, Tx(context, "trade-project-offer"));
            TradeOperationResult valuation = fixture.Trades.ValueAsset(Scoped(context, "trade-valuation", "sword"), sessionId, offer.Offer.offerId, "participant.buyer", offer.Offer.AllAssets.First(asset => asset.IsItemAsset), fixture.Economy, fixture.Markets, fixture.Items, privilegedHiddenFactors: false, worldTime: 2d);

            InformationAccessRuntime access = context.ScenarioContext.Runtimes.Access;
            string policyId = Scoped(context, "information-access-policy", "trade-offer");
            access.RegisterPolicy(new InformationAccessPolicyData
            {
                policyId = policyId,
                subject = offer.Offer.CreateInformationSubject(),
                classification = InformationVisibilityClassification.Secret,
                detailVisibilityPolicy = InformationDetailVisibilityPolicy.Selected,
                defaultVisibleDetails = new[] { "detail.participants" },
                defaultRedactedDetails = new[] { "detail.assets", "detail.valuations", "detail.offer-history" },
                redactedAccessAcceptable = true
            }, Tx(context, "trade-policy"));
            access.GrantAccess(new InformationAccessGrantData
            {
                grantId = Scoped(context, "information-access-grant", "trade-offer"),
                policyId = policyId,
                subject = offer.Offer.CreateInformationSubject(),
                granteeKind = InformationGranteeKind.Person,
                granteeId = "person.viewer",
                grantorId = fixture.SellerPersonId,
                accessModes = new[] { InformationAccessMode.Inspect },
                detailIds = new[] { "detail.participants" }
            }, Tx(context, "trade-grant"));
            TradeProjection<TradeOfferData> projection = fixture.Trades.GetOfferProjection(offer.Offer.offerId, access, new InformationAccessContext
            {
                RequestingPersonId = "person.viewer",
                HasDiscoveredSubject = true,
                RevealDenialReasons = true
            }, policyId);

            TradeRuntimeSaveData save = fixture.Trades.CreateSaveData();
            bool validSave = TradeRuntime.ValidateSaveData(save, fixture.Registry, out string validFailure);
            TradeRuntime restored = new TradeRuntime();
            TradeOperationResult restore = restored.RestoreFromSaveData(save, fixture.Registry);
            TradeRuntimeSaveData corrupt = save.Clone();
            corrupt.offers[0].tradeSessionId = "trade-session.missing";
            bool rejected = !TradeRuntime.ValidateSaveData(corrupt, fixture.Registry, out string corruptFailure);

            bool valid = open.Succeeded
                && offer.Succeeded
                && valuation.Succeeded
                && projection.Succeeded
                && projection.Redacted
                && projection.Record.AllAssets.Count == 0
                && validSave
                && restore.Succeeded
                && rejected
                && restored.TryGetOffer(offer.Offer.offerId, out _);
            return TestLabAssertions.True("step11-trade-persistence", "Valuation, receipts, persistence, and projections remain explicit", valid, $"Open={open.Code} Offer={offer.Code} Valuation={valuation.Code} Redacted={projection.Redacted} Save={validSave}:{validFailure} Restore={restore.Code} Rejected={rejected}:{corruptFailure}");
        }

        private static bool TryGetTradeFixture(TestLabAutomationContext context, out TradeFixture fixture, out string failure)
        {
            fixture = null;
            failure = string.Empty;
            if (!TryGetMarketFixture(context, out MarketRuntime markets, out DefinitionRegistry registry, out MarketDefinition market, out MarketSubjectDefinition subject, out CurrencyDefinition gold, out failure))
            {
                return false;
            }

            TradeRuntime trades = context?.ScenarioContext?.Runtimes?.Trades;
            EconomyRuntime economy = context?.ScenarioContext?.Runtimes?.Economy;
            ItemInstanceIdentityRuntime items = context?.ScenarioContext?.Runtimes?.ItemInstances;
            if (trades == null || economy == null || items == null)
            {
                failure = trades == null ? "Trade runtime is missing." : economy == null ? "Economy runtime is missing." : "Item identity runtime is missing.";
                return false;
            }

            TradePolicyDefinition policy = ScriptableObject.CreateInstance<TradePolicyDefinition>();
            policy.Initialize(Scoped(context, "trade-policy", "direct"), "Prototype Direct Trade", TradePolicyCategory.DirectPersonToPerson);
            DefinitionRegistry extended = new DefinitionRegistry(registry.DefinitionsById.Values.Concat(new IGameDefinition[] { policy }));
            trades.Configure(extended, context.ScenarioContext.Runtimes.WorldId);
            economy.Configure(extended, context.ScenarioContext.Runtimes.WorldId);
            markets.Configure(extended, context.ScenarioContext.Runtimes.WorldId);

            ItemDefinition sword = extended.TryGet(PrototypeSwordItemId, out ItemDefinition foundSword) ? foundSword : CreateItemDefinition(PrototypeSwordItemId, "Prototype Sword");
            ItemDefinition herb = CreateStackDefinition(Scoped(context, "item", "barter-herb"), "Prototype Barter Herb");
            if (!extended.Contains(herb.Id) || !extended.Contains(sword.Id))
            {
                extended = new DefinitionRegistry(extended.DefinitionsById.Values.Concat(new IGameDefinition[] { sword, herb }).Distinct());
                trades.Configure(extended, context.ScenarioContext.Runtimes.WorldId);
                economy.Configure(extended, context.ScenarioContext.Runtimes.WorldId);
                markets.Configure(extended, context.ScenarioContext.Runtimes.WorldId);
            }

            string buyerAccount = Account(context, "trade-buyer");
            string sellerAccount = Account(context, "trade-seller");
            economy.CreateAccount(buyerAccount, gold, "person.trade.buyer", EconomyAccountKind.PersonWallet, 100L, Tx(context, "trade-buyer-open"));
            economy.CreateAccount(sellerAccount, gold, "person.trade.seller", EconomyAccountKind.PersonWallet, 0L, Tx(context, "trade-seller-open"));
            string swordInstance = RunGuid(context, "trade-sword");
            string herbStack = RunGuid(context, "trade-herbs");
            if (!items.TryGetSnapshot(swordInstance, out _))
            {
                items.CreateItem(sword, ItemInstanceClassification.IndividuallyTracked, swordInstance, ownerPersonId: "person.trade.seller", custodianPersonId: "person.trade.seller");
            }

            if (!items.TryGetSnapshot(herbStack, out _))
            {
                items.CreateItem(herb, ItemInstanceClassification.Fungible, herbStack, ownerPersonId: "person.trade.buyer", custodianPersonId: "person.trade.buyer");
                ItemInstanceRuntimeSaveData itemSave = items.CreateSaveData();
                ItemInstanceRecordData herbRecord = itemSave.records.FirstOrDefault(record => record.itemInstanceId == herbStack);
                if (herbRecord != null)
                {
                    herbRecord.stackQuantity = 5;
                    items.RestoreFromSaveData(itemSave, extended);
                }
            }

            markets.CreateMarketInstance(market, MarketId(context, "trade-market"), "region.prototype");
            markets.RecordSupply(Supply(context, MarketId(context, "trade-market"), subject.Id, "trade-stock", 10L, 0L, 10L, 1d));
            markets.RecordDemand(Demand(context, MarketId(context, "trade-market"), subject.Id, "trade-demand", 10L, 10L, 1d));
            markets.UpdateMarketSubject(MarketId(context, "trade-market"), subject.Id, 2d);

            fixture = new TradeFixture(context, trades, economy, markets, items, extended, policy, subject, gold, sword, herb, buyerAccount, sellerAccount, swordInstance, herbStack);
            return true;
        }

        private sealed class TradeFixture
        {
            private readonly TestLabAutomationContext context;

            public TradeFixture(TestLabAutomationContext context, TradeRuntime trades, EconomyRuntime economy, MarketRuntime markets, ItemInstanceIdentityRuntime items, DefinitionRegistry registry, TradePolicyDefinition policy, MarketSubjectDefinition subject, CurrencyDefinition gold, ItemDefinition sword, ItemDefinition herb, string buyerAccount, string sellerAccount, string swordInstanceId, string herbStackId)
            {
                this.context = context;
                Trades = trades;
                Economy = economy;
                Markets = markets;
                Items = items;
                Registry = registry;
                Policy = policy;
                Subject = subject;
                Gold = gold;
                Sword = sword;
                Herb = herb;
                BuyerAccount = buyerAccount;
                SellerAccount = sellerAccount;
                SwordInstanceId = swordInstanceId;
                HerbStackId = herbStackId;
            }

            public TradeRuntime Trades { get; }
            public EconomyRuntime Economy { get; }
            public MarketRuntime Markets { get; }
            public ItemInstanceIdentityRuntime Items { get; }
            public DefinitionRegistry Registry { get; }
            public TradePolicyDefinition Policy { get; }
            public MarketSubjectDefinition Subject { get; }
            public CurrencyDefinition Gold { get; }
            public ItemDefinition Sword { get; }
            public ItemDefinition Herb { get; }
            public string BuyerAccount { get; }
            public string SellerAccount { get; }
            public string SwordInstanceId { get; }
            public string HerbStackId { get; }
            public string BuyerPersonId => "person.trade.buyer";
            public string SellerPersonId => "person.trade.seller";

            public TradeOperationResult OpenSession(string slug, out string sessionId)
            {
                sessionId = Scoped(context, "trade-session", slug);
                return Trades.OpenSession(Policy, new TradeSessionData
                {
                    tradeSessionId = sessionId,
                    participants = new List<TradeParticipantData>
                    {
                        Participant("participant.buyer", BuyerPersonId, TradeParticipantRole.Buyer),
                        Participant("participant.seller", SellerPersonId, TradeParticipantRole.Seller)
                    },
                    initiatorParticipantId = "participant.buyer",
                    marketInstanceId = MarketId(context, "trade-market"),
                    createdWorldTime = 1d,
                    lastActivityWorldTime = 1d
                }, Tx(context, $"trade-session-{slug}"));
            }

            public TradeOfferData BuildMoneyForItemOffer(string sessionId, string slug, long units, string proposer = "participant.buyer", string responder = "participant.seller", string quoteId = "")
            {
                return new TradeOfferData
                {
                    offerId = Scoped(context, "trade-offer", slug),
                    tradeSessionId = sessionId,
                    proposingParticipantId = proposer,
                    respondingParticipantIds = new[] { responder },
                    createdWorldTime = 2d,
                    expiresWorldTime = 120d,
                    merchantQuoteIds = string.IsNullOrWhiteSpace(quoteId) ? Array.Empty<string>() : new[] { quoteId },
                    bundles = new List<TradeBundleData>
                    {
                        new TradeBundleData
                        {
                            bundleId = Scoped(context, "trade-bundle", $"{slug}.money"),
                            contributingParticipantId = "participant.buyer",
                            receivingParticipantId = "participant.seller",
                            assets = new List<TradeAssetEntryData>
                            {
                                Money(Scoped(context, "trade-asset", $"{slug}.gold"), "participant.buyer", "participant.seller", BuyerAccount, SellerAccount, Gold.Id, units, quoteId)
                            }
                        },
                        new TradeBundleData
                        {
                            bundleId = Scoped(context, "trade-bundle", $"{slug}.item"),
                            contributingParticipantId = "participant.seller",
                            receivingParticipantId = "participant.buyer",
                            assets = new List<TradeAssetEntryData>
                            {
                                Item(Scoped(context, "trade-asset", $"{slug}.sword"), "participant.seller", "participant.buyer", SwordInstanceId, 1)
                            }
                        }
                    }
                };
            }

            public TradeOfferData BuildBarterOffer(string sessionId, string slug)
            {
                TradeOfferData offer = BuildMoneyForItemOffer(sessionId, slug, 15L);
                offer.bundles[0].assets.Add(Item(Scoped(context, "trade-asset", $"{slug}.herbs"), "participant.buyer", "participant.seller", HerbStackId, 2, TradeAssetKind.StackQuantity));
                return offer;
            }

            public string CreateQuote(string slug, long units)
            {
                string marketId = MarketId(context, "trade-market");
                string quoteId = Scoped(context, "merchant-quote", slug);
                Markets.CreateMerchantQuote(quoteId, "merchant.prototype", marketId, Subject.Id, MerchantQuoteDirection.MerchantSells, 1L, 3d, 120d);
                return quoteId;
            }

            private static TradeParticipantData Participant(string participantId, string personId, TradeParticipantRole role)
            {
                return new TradeParticipantData
                {
                    participantId = participantId,
                    kind = TradeParticipantKind.Person,
                    role = role,
                    subjectId = personId,
                    sourceInventoryId = personId,
                    receivingInventoryId = personId
                };
            }

            private static TradeAssetEntryData Money(string assetId, string sourceParticipant, string destinationParticipant, string sourceAccount, string destinationAccount, string currencyId, long units, string quoteId)
            {
                return new TradeAssetEntryData
                {
                    assetEntryId = assetId,
                    assetKind = TradeAssetKind.Money,
                    sourceParticipantId = sourceParticipant,
                    destinationParticipantId = destinationParticipant,
                    sourceAccountId = sourceAccount,
                    destinationAccountId = destinationAccount,
                    currencyId = currencyId,
                    units = units,
                    quantity = 1,
                    quoteId = quoteId
                };
            }

            private static TradeAssetEntryData Item(string assetId, string sourceParticipant, string destinationParticipant, string itemInstanceId, int quantity, TradeAssetKind kind = TradeAssetKind.ItemInstance)
            {
                return new TradeAssetEntryData
                {
                    assetEntryId = assetId,
                    assetKind = kind,
                    sourceParticipantId = sourceParticipant,
                    destinationParticipantId = destinationParticipant,
                    itemInstanceId = itemInstanceId,
                    quantity = Math.Max(1, quantity)
                };
            }
        }

        private static TestLabAutomationStepResult PayrollAgreementsSchedulesEvidence(TestLabAutomationContext context)
        {
            if (!TryGetPayrollFixture(context, out PayrollFixture fixture, out string failure))
            {
                return Fail("step11-payroll-agreement", failure);
            }

            PayrollOperationResult agreement = fixture.CreateAgreement("primary");
            PayrollOperationResult overlap = fixture.CreateAgreement("overlap", start: 10d, end: 50d);
            PayrollOperationResult schedule = fixture.Payroll.CreateSchedule(new WorkScheduleData
            {
                scheduleId = Scoped(context, "payroll-schedule", "weekly"),
                agreementId = fixture.AgreementId,
                category = WorkScheduleCategory.FixedShift,
                expectedMinutesPerPeriod = 2400L,
                startWorldTime = 0d
            }, Tx(context, "payroll-schedule"));
            PayrollOperationResult session = fixture.RecordSession("shift-a", 0d, 8d * 60d, 480L, evidence: "evidence.shift-a");
            PayrollOperationResult duplicateEvidence = fixture.RecordSession("shift-b", 9d * 60d, 17d * 60d, 480L, evidence: "evidence.shift-a");
            PayrollOperationResult timesheet = fixture.Payroll.SubmitTimesheet(new TimesheetData
            {
                timesheetId = Scoped(context, "payroll-timesheet", "week-one"),
                agreementId = fixture.AgreementId,
                workSessionIds = new[] { session.WorkSession?.workSessionId },
                submittedByPersonId = fixture.EmployeePersonId,
                submittedWorldTime = 10d
            }, Tx(context, "payroll-timesheet"));
            PayrollOperationResult approve = fixture.Payroll.ApproveTimesheet(timesheet.Timesheet?.timesheetId, fixture.AuthorityId, 11d, Tx(context, "payroll-timesheet-approve"));

            bool valid = agreement.Succeeded
                && !overlap.Succeeded
                && overlap.Code == PayrollOperationCode.AgreementOverlap
                && schedule.Succeeded
                && session.Succeeded
                && !duplicateEvidence.Succeeded
                && timesheet.Succeeded
                && approve.Succeeded
                && fixture.Payroll.AgreementCount == 1;
            return TestLabAssertions.True("step11-payroll-agreement", "Compensation agreements, schedules, and work evidence validate against employment", valid, $"Agreement={agreement.Code} Overlap={overlap.Code} Schedule={schedule.Code} Session={session.Code} DuplicateEvidence={duplicateEvidence.Code} Timesheet={timesheet.Code} Approve={approve.Code}");
        }

        private static TestLabAutomationStepResult PayrollGrossNetDeductions(TestLabAutomationContext context)
        {
            if (!TryGetPayrollFixture(context, out PayrollFixture fixture, out string failure))
            {
                return Fail("step11-payroll-calculate", failure);
            }

            fixture.CreateAgreement("calc");
            PayrollOperationResult session = fixture.RecordSession("calc-shift", 0d, 8d * 60d, 480L);
            fixture.Payroll.CreatePayPeriod(new PayPeriodData
            {
                payPeriodId = fixture.PayPeriodId,
                agreementId = fixture.AgreementId,
                startWorldTime = 0d,
                endWorldTime = 7d * 24d * 60d * 60d,
                dueWorldTime = 8d * 24d * 60d * 60d
            }, Tx(context, "payroll-period"));
            fixture.Payroll.RecordAdjustment(new CompensationAdjustmentData
            {
                adjustmentId = Scoped(context, "payroll-adjustment", "hazard"),
                agreementId = fixture.AgreementId,
                payPeriodId = fixture.PayPeriodId,
                category = CompensationAdjustmentCategory.Premium,
                currencyId = fixture.Gold.Id,
                units = 10L
            }, Tx(context, "payroll-adjustment-hazard"));
            fixture.Payroll.RecordAdjustment(new CompensationAdjustmentData
            {
                adjustmentId = Scoped(context, "payroll-adjustment", "meal"),
                agreementId = fixture.AgreementId,
                payPeriodId = fixture.PayPeriodId,
                category = CompensationAdjustmentCategory.Reimbursement,
                currencyId = fixture.Gold.Id,
                units = 5L
            }, Tx(context, "payroll-adjustment-meal"));

            PayrollOperationResult preview = fixture.Payroll.CalculatePay(Scoped(context, "payroll-calculation", "preview"), fixture.PayPeriodId, new[] { session.WorkSession.workSessionId }, new[] { Scoped(context, "payroll-adjustment", "hazard"), Scoped(context, "payroll-adjustment", "meal") }, preview: true);
            PayrollOperationResult execute = fixture.Payroll.CalculatePay(fixture.CalculationId, fixture.PayPeriodId, new[] { session.WorkSession.workSessionId }, new[] { Scoped(context, "payroll-adjustment", "hazard"), Scoped(context, "payroll-adjustment", "meal") }, Tx(context, "payroll-calculate"));

            PayrollCalculationData calc = execute.Calculation;
            bool valid = preview.Succeeded
                && preview.Preview
                && execute.Succeeded
                && calc.regularGrossUnits == 80L
                && calc.adjustmentGrossUnits == 10L
                && calc.reimbursementUnits == 5L
                && calc.deductionUnits == 9L
                && calc.netPayUnits == 86L
                && calc.deductions.Count == 1;
            return TestLabAssertions.True("step11-payroll-calculate", "Gross, net, adjustments, and deductions calculate exactly", valid, $"Preview={preview.Code} Execute={execute.Code} Gross={calc?.regularGrossUnits}+{calc?.adjustmentGrossUnits} Reimburse={calc?.reimbursementUnits} Deduct={calc?.deductionUnits} Net={calc?.netPayUnits}");
        }

        private static TestLabAutomationStepResult PayrollExecutionRollbackDebt(TestLabAutomationContext context)
        {
            if (!TryGetPayrollFixture(context, out PayrollFixture fixture, out string failure))
            {
                return Fail("step11-payroll-execute", failure);
            }

            fixture.BuildCalculatedObligation("execute", 500L);
            fixture.Payroll.CreatePayrollRun(fixture.PayRunId, fixture.EmployerId, fixture.EmployerAccountId, new[] { fixture.ObligationId }, PayrollPaymentPolicy.AllOrNothing, 20d, Tx(context, "payroll-run"));
            PayrollRuntimeSaveData beforePayroll = fixture.Payroll.CreateSaveData();
            EconomyRuntimeSaveData beforeEconomy = fixture.Economy.CreateSaveData();
            PayrollOperationResult failed = fixture.Payroll.ExecutePayrollRun(fixture.PayRunId, fixture.Economy, Tx(context, "payroll-run-fail"), injectFailureStage: "before-run-commit");
            bool rolledBack = !failed.Succeeded
                && JsonUtility.ToJson(beforePayroll) == JsonUtility.ToJson(fixture.Payroll.CreateSaveData())
                && JsonUtility.ToJson(beforeEconomy) == JsonUtility.ToJson(fixture.Economy.CreateSaveData());

            PayrollOperationResult reserve = fixture.Payroll.ReservePayrollFunds(fixture.PayRunId, fixture.Economy, Tx(context, "payroll-run-reserve"));
            PayrollOperationResult execute = fixture.Payroll.ExecutePayrollRun(fixture.PayRunId, fixture.Economy, Tx(context, "payroll-run-execute"));
            bool hasEmployee = fixture.Economy.TryGetAccount(fixture.EmployeeAccountId, out EconomyAccountSnapshot employee);

            PayrollFixture partial = fixture.CreateSibling("partial");
            partial.BuildCalculatedObligation("partial", 60L);
            partial.Payroll.CreatePayrollRun(partial.PayRunId, partial.EmployerId, partial.EmployerAccountId, new[] { partial.ObligationId }, PayrollPaymentPolicy.PartialWithDebt, 30d, Tx(context, "payroll-partial-run"));
            PayrollOperationResult partialExecute = partial.Payroll.ExecutePayrollRun(partial.PayRunId, partial.Economy, Tx(context, "payroll-partial-execute"));
            bool hasPartialObligation = partial.Payroll.TryGetObligation(partial.ObligationId, out PayrollObligationData partialObligation);
            bool valid = rolledBack
                && reserve.Succeeded
                && execute.Succeeded
                && hasEmployee
                && employee.BalanceUnits == 72L
                && partialExecute.Succeeded
                && hasPartialObligation
                && partialObligation.amountOutstandingUnits > 0L
                && partial.Payroll.WageDebts.Count == 1;
            return TestLabAssertions.True("step11-payroll-execute", "Payroll execution reserves funds, rolls back failures, and records wage debt", valid, $"Rollback={rolledBack} Reserve={reserve.Code} Execute={execute.Code} HasEmployee={hasEmployee} Employee={employee?.BalanceUnits} Partial={partialExecute.Code} HasPartialObligation={hasPartialObligation} Debt={partialObligation?.amountOutstandingUnits}");
        }

        private static TestLabAutomationStepResult PayrollPersistenceProjectionCorrection(TestLabAutomationContext context)
        {
            if (!TryGetPayrollFixture(context, out PayrollFixture fixture, out string failure))
            {
                return Fail("step11-payroll-persist", failure);
            }

            fixture.BuildCalculatedObligation("persist", 200L);
            fixture.Payroll.CreatePayrollRun(fixture.PayRunId, fixture.EmployerId, fixture.EmployerAccountId, new[] { fixture.ObligationId }, PayrollPaymentPolicy.AllOrNothing, 20d, Tx(context, "payroll-persist-run"));
            PayrollOperationResult execute = fixture.Payroll.ExecutePayrollRun(fixture.PayRunId, fixture.Economy, Tx(context, "payroll-persist-execute"));
            string statementId = execute.PayRun?.statementIds?.FirstOrDefault() ?? string.Empty;
            PayrollProjection<PayStatementData> redacted = fixture.Payroll.ProjectPayStatement(statementId, PayrollProjectionAudience.Public, null);
            PayrollOperationResult correction = fixture.Payroll.RecordCorrection(new PayrollCorrectionData
            {
                correctionId = Scoped(context, "payroll-correction", "statement"),
                correctedRecordId = statementId,
                replacementRecordId = Scoped(context, "payroll-statement", "replacement"),
                authorityId = fixture.AuthorityId,
                reason = "Prototype correction",
                worldTime = 25d
            }, Tx(context, "payroll-correction"));
            PayrollOperationResult overpayment = fixture.Payroll.RecordOverpayment(new OverpaymentRecordData
            {
                overpaymentId = Scoped(context, "payroll-overpayment", "statement"),
                originalPaymentRecordId = execute.PayRun?.paymentRecordIds?.FirstOrDefault() ?? string.Empty,
                employeePersonId = fixture.EmployeePersonId,
                employerSubjectId = fixture.EmployerId,
                currencyId = fixture.Gold.Id,
                overpaidUnits = 1L,
                createdWorldTime = 26d
            }, Tx(context, "payroll-overpayment"));

            PayrollRuntimeSaveData save = fixture.Payroll.CreateSaveData();
            bool validSave = PayrollRuntime.ValidateSaveData(save, fixture.Registry, out string saveFailure);
            PayrollRuntime restored = new PayrollRuntime();
            restored.Configure(fixture.Registry, context.ScenarioContext.Runtimes.WorldId);
            PayrollOperationResult restore = restored.RestoreFromSaveData(save, fixture.Registry);
            PayrollRuntimeSaveData corrupt = save.Clone();
            corrupt.agreements[0].compensationDefinitionId = "compensation.missing";
            bool rejected = !PayrollRuntime.ValidateSaveData(corrupt, fixture.Registry, out string corruptFailure);

            bool valid = execute.Succeeded
                && redacted.Redacted
                && redacted.Record.netUnits == 0L
                && correction.Succeeded
                && overpayment.Succeeded
                && validSave
                && restore.Succeeded
                && restored.StatementCount == fixture.Payroll.StatementCount
                && rejected;
            return TestLabAssertions.True("step11-payroll-persist", "Payroll persistence, projections, corrections, and overpayments are explicit", valid, $"Execute={execute.Code} Redacted={redacted.Redacted} Correction={correction.Code} Overpay={overpayment.Code} Save={validSave}:{saveFailure} Restore={restore.Code} Rejected={rejected}:{corruptFailure}");
        }

        private static bool TryGetPayrollFixture(TestLabAutomationContext context, out PayrollFixture fixture, out string failure)
        {
            fixture = null;
            failure = string.Empty;
            TestLabRuntimeBundle runtimes = context?.ScenarioContext?.Runtimes;
            if (runtimes?.Payroll == null || runtimes.Economy == null || runtimes.PositionEmployment == null)
            {
                failure = "Payroll, Economy, or Position Employment runtime is missing.";
                return false;
            }

            if (!runtimes.DefinitionRegistry.TryGet(GoldCurrencyId, out CurrencyDefinition gold))
            {
                failure = $"Currency definition '{GoldCurrencyId}' is missing.";
                return false;
            }

            CurrencyDefinition currency = gold;
            string employerAccount = Account(context, "payroll-employer");
            string employeeAccount = Account(context, "payroll-employee");
            string deductionAccount = Account(context, "payroll-tax");
            CompensationDefinition compensation = ScriptableObject.CreateInstance<CompensationDefinition>();
            compensation.Initialize(Scoped(context, "compensation", "hourly"), "Prototype Hourly Wage", currency, CompensationCategory.HourlyWage, CompensationRateBasis.PerDurationUnit, 10L, duration: PayrollDurationUnit.Hour);
            PayrollDeductionDefinition deduction = ScriptableObject.CreateInstance<PayrollDeductionDefinition>();
            deduction.Initialize(Scoped(context, "payroll-deduction", "tax"), "Prototype Payroll Tax", currency, DeductionCategory.Tax, 0L, new PayrollRationalData { numerator = 1L, denominator = 10L }, 10, deductionAccount);
            PositionDefinition position = ScriptableObject.CreateInstance<PositionDefinition>();
            position.DevelopmentConfigure(
                Scoped(context, "position", "payroll-worker"),
                "Prototype Payroll Worker",
                PositionCategory.Custom,
                authorities: new[] { PrototypeProfessionDefinitionFactory.PositionAppointAuthorityId },
                compensationPolicy: compensation.Id,
                paymentSchedule: "pay-schedule.prototype.weekly",
                wageOrSalary: compensation.Id,
                maxHolders: 4,
                exclusive: false);
            DefinitionRegistry registry = new DefinitionRegistry(runtimes.DefinitionRegistry.DefinitionsById.Values.Concat(new IGameDefinition[] { compensation, deduction, position }));
            runtimes.Economy.Configure(registry, runtimes.WorldId);
            runtimes.Payroll.Configure(registry, runtimes.WorldId);
            runtimes.PositionEmployment.Configure(registry, runtimes.Professions, runtimes.Training, runtimes.ProfessionalActivities, runtimes.Credentials, runtimes.ProfessionalRanks, new[] { runtimes.PersonId, "person.prototype.payroll-worker" }, new[] { "organization.prototype.guild" }, new[] { PrototypeProfessionDefinitionFactory.PositionAppointAuthorityId, "organization.prototype.guild" });
            runtimes.Economy.CreateAccount(employerAccount, currency, "organization.prototype.guild", EconomyAccountKind.OrganizationAccount, 1000L, Tx(context, "payroll-employer-open"));
            runtimes.Economy.CreateAccount(employeeAccount, currency, "person.prototype.payroll-worker", EconomyAccountKind.PersonWallet, 0L, Tx(context, "payroll-employee-open"));
            runtimes.Economy.CreateAccount(deductionAccount, currency, "organization.prototype.tax", EconomyAccountKind.OrganizationAccount, 0L, Tx(context, "payroll-tax-open"));

            string positionInstanceId = Scoped(context, "position-instance", "payroll-worker");
            runtimes.PositionEmployment.CreatePosition(new PositionInstanceData
            {
                positionInstanceId = positionInstanceId,
                positionDefinitionId = position.Id,
                organizationId = "organization.prototype.guild",
                state = PositionInstanceState.Vacant,
                maximumHolders = 4,
                vacancyAllowed = true,
                createdWorldTime = "0"
            }, Tx(context, "payroll-position"));
            PositionEligibilityResult eligibility = runtimes.PositionEmployment.EvaluateEligibility("person.prototype.payroll-worker", positionInstanceId, privilegedDiagnostics: true);
            PositionEmploymentOperationResult employment = runtimes.PositionEmployment.AppointPerson(Scoped(context, "employment", "payroll-worker"), string.Empty, "person.prototype.payroll-worker", positionInstanceId, PrototypeProfessionDefinitionFactory.PositionAppointAuthorityId, eligibility.Snapshot, "0", Tx(context, "payroll-appoint"));
            if (!employment.Succeeded)
            {
                failure = $"Payroll employment fixture failed: {employment.Status} {employment.Message}";
                return false;
            }

            fixture = new PayrollFixture(context, registry, runtimes.Payroll, runtimes.Economy, runtimes.PositionEmployment, currency, compensation.Id, deduction.Id, employment.Employment.employmentId, employerAccount, employeeAccount, deductionAccount, "person.prototype.payroll-worker");
            return true;
        }

        private sealed class PayrollFixture
        {
            private readonly TestLabAutomationContext context;
            private readonly string employeePersonId;

            public PayrollFixture(TestLabAutomationContext context, DefinitionRegistry registry, PayrollRuntime payroll, EconomyRuntime economy, PositionEmploymentRuntime employmentRuntime, CurrencyDefinition gold, string compensationId, string deductionId, string employmentId, string employerAccountId, string employeeAccountId, string deductionAccountId, string employeePersonId)
            {
                this.context = context;
                this.employeePersonId = string.IsNullOrWhiteSpace(employeePersonId) ? "person.prototype.payroll-worker" : employeePersonId;
                Registry = registry;
                Payroll = payroll;
                Economy = economy;
                EmploymentRuntime = employmentRuntime;
                Gold = gold;
                CompensationId = compensationId;
                DeductionId = deductionId;
                EmploymentId = employmentId;
                EmployerAccountId = employerAccountId;
                EmployeeAccountId = employeeAccountId;
                DeductionAccountId = deductionAccountId;
                AgreementId = Scoped(context, "payroll-agreement", "primary");
                PayPeriodId = Scoped(context, "payroll-period", "primary");
                CalculationId = Scoped(context, "payroll-calculation", "primary");
                ObligationId = Scoped(context, "payroll-obligation", "primary");
                PayRunId = Scoped(context, "payroll-run", "primary");
            }

            public DefinitionRegistry Registry { get; }
            public PayrollRuntime Payroll { get; }
            public EconomyRuntime Economy { get; }
            public PositionEmploymentRuntime EmploymentRuntime { get; }
            public CurrencyDefinition Gold { get; }
            public string CompensationId { get; }
            public string DeductionId { get; }
            public string EmploymentId { get; }
            public string EmployerAccountId { get; }
            public string EmployeeAccountId { get; }
            public string DeductionAccountId { get; }
            public string AgreementId { get; private set; }
            public string PayPeriodId { get; private set; }
            public string CalculationId { get; private set; }
            public string ObligationId { get; private set; }
            public string PayRunId { get; private set; }
            public string EmployeePersonId => employeePersonId;
            public string EmployerId => "organization.prototype.guild";
            public string AuthorityId => PrototypeProfessionDefinitionFactory.PositionAppointAuthorityId;

            public PayrollOperationResult CreateAgreement(string slug, double start = 0d, double end = -1d)
            {
                string id = slug == "primary" || slug == "calc" ? AgreementId : Scoped(context, "payroll-agreement", slug);
                return Payroll.ActivateAgreement(new CompensationAgreementData
                {
                    agreementId = id,
                    compensationDefinitionId = CompensationId,
                    employmentId = EmploymentId,
                    employeePersonId = EmployeePersonId,
                    employerSubjectId = EmployerId,
                    employerFundingAccountId = EmployerAccountId,
                    employeeAccountId = EmployeeAccountId,
                    deductionDefinitionIds = new[] { DeductionId },
                    state = CompensationAgreementState.Active,
                    effectiveStartWorldTime = start,
                    effectiveEndWorldTime = end
                }, EmploymentRuntime, Economy, Tx(context, $"payroll-agreement-{slug}"));
            }

            public PayrollOperationResult RecordSession(string slug, double start, double end, long minutes, string evidence = "")
            {
                return Payroll.RecordWorkSession(new WorkSessionData
                {
                    workSessionId = Scoped(context, "payroll-session", slug),
                    agreementId = AgreementId,
                    startWorldTime = start,
                    endWorldTime = end,
                    durationMinutes = minutes,
                    evidenceIds = string.IsNullOrWhiteSpace(evidence) ? Array.Empty<string>() : new[] { evidence }
                }, EmploymentRuntime, Tx(context, $"payroll-session-{slug}"));
            }

            public void BuildCalculatedObligation(string slug, long employerOpeningBalance)
            {
                if (!Payroll.TryGetAgreement(AgreementId, out _))
                {
                    CreateAgreement("primary");
                }

                PayrollOperationResult session = RecordSession($"{slug}-shift", 0d, 8d * 60d, 480L);
                if (!session.Succeeded || session.WorkSession == null)
                {
                    return;
                }

                Payroll.CreatePayPeriod(new PayPeriodData
                {
                    payPeriodId = PayPeriodId,
                    agreementId = AgreementId,
                    startWorldTime = 0d,
                    endWorldTime = 7d * 24d * 60d * 60d,
                    dueWorldTime = 8d * 24d * 60d * 60d
                }, Tx(context, $"payroll-period-{slug}"));
                Payroll.CalculatePay(CalculationId, PayPeriodId, new[] { session.WorkSession.workSessionId }, Array.Empty<string>(), Tx(context, $"payroll-calc-{slug}"));
                Payroll.CreateObligation(ObligationId, CalculationId, 8d * 24d * 60d * 60d, Tx(context, $"payroll-obligation-{slug}"));
            }

            public PayrollFixture CreateSibling(string slug)
            {
                string personId = $"person.prototype.payroll-worker-{slug}";
                EmploymentRuntime.Configure(Registry, context.ScenarioContext.Runtimes.Professions, context.ScenarioContext.Runtimes.Training, context.ScenarioContext.Runtimes.ProfessionalActivities, context.ScenarioContext.Runtimes.Credentials, context.ScenarioContext.Runtimes.ProfessionalRanks, new[] { "person.prototype.payroll-worker", personId }, new[] { EmployerId }, new[] { PrototypeProfessionDefinitionFactory.PositionAppointAuthorityId, EmployerId });
                string positionInstanceId = Scoped(context, "position-instance", "payroll-worker");
                PositionEligibilityResult eligibility = EmploymentRuntime.EvaluateEligibility(personId, positionInstanceId, privilegedDiagnostics: true);
                PositionEmploymentOperationResult employment = EmploymentRuntime.AppointPerson(Scoped(context, "employment", $"payroll-worker-{slug}"), string.Empty, personId, positionInstanceId, AuthorityId, eligibility.Snapshot, "0", Tx(context, $"payroll-appoint-{slug}"));
                string employer = Account(context, $"payroll-employer-{slug}");
                string employee = Account(context, $"payroll-employee-{slug}");
                string deduction = Account(context, $"payroll-tax-{slug}");
                Economy.CreateAccount(employer, Gold, EmployerId, EconomyAccountKind.OrganizationAccount, 60L, Tx(context, $"payroll-employer-{slug}"));
                Economy.CreateAccount(employee, Gold, personId, EconomyAccountKind.PersonWallet, 0L, Tx(context, $"payroll-employee-{slug}"));
                Economy.CreateAccount(deduction, Gold, "organization.prototype.tax", EconomyAccountKind.OrganizationAccount, 0L, Tx(context, $"payroll-tax-{slug}"));
                PayrollFixture sibling = new PayrollFixture(context, Registry, Payroll, Economy, EmploymentRuntime, Gold, CompensationId, DeductionId, employment.Employment?.employmentId ?? EmploymentId, employer, employee, deduction, personId);
                sibling.AgreementId = Scoped(context, "payroll-agreement", slug);
                sibling.PayPeriodId = Scoped(context, "payroll-period", slug);
                sibling.CalculationId = Scoped(context, "payroll-calculation", slug);
                sibling.ObligationId = Scoped(context, "payroll-obligation", slug);
                sibling.PayRunId = Scoped(context, "payroll-run", slug);
                return sibling;
            }
        }

        private static TestLabAutomationStepResult PropertyDefinitionsHierarchy(TestLabAutomationContext context)
        {
            if (!PreparePropertyFixture(context, out PropertyRuntime properties, out _, out _, out _, out PropertyDefinition landDefinition, out _, out _, out string failure))
            {
                return Fail("step11-property-hierarchy", failure);
            }

            PropertyOperationResult land = RegisterPropertyLand(context, properties, landDefinition, "land");
            PropertyOperationResult building = RegisterPropertyBuilding(context, properties, "building", PropertyId(context, "land"));
            PropertyOperationResult unit = RegisterPropertyUnit(context, properties, "unit", PropertyId(context, "building"));
            PropertyOperationResult invalidChild = properties.RegisterProperty(new PropertyInstanceData
            {
                propertyId = PropertyId(context, "bad-child"),
                propertyDefinitionId = "property.prototype.unit",
                parentPropertyId = PropertyId(context, "unit"),
                sceneObjectReferenceId = "scene.prototype.bad-child"
            });
            PropertyInstanceData snapshot = properties.Properties.FirstOrDefault(item => item.propertyId == PropertyId(context, "land"));
            if (snapshot != null)
            {
                snapshot.childPropertyIds = new[] { "mutation.should-not-stick" };
            }

            bool immutable = properties.TryGetProperty(PropertyId(context, "land"), out PropertyInstanceData live)
                && live.childPropertyIds.Contains(PropertyId(context, "building"))
                && !live.childPropertyIds.Contains("mutation.should-not-stick");
            bool valid = land.Succeeded && building.Succeeded && unit.Succeeded && !invalidChild.Succeeded && immutable;
            return TestLabAssertions.True("step11-property-hierarchy", "Property definitions, spatial references, and hierarchy validate", valid, $"Land={land.Code} Building={building.Code} Unit={unit.Code} Invalid={invalidChild.Code} Immutable={immutable}");
        }

        private static TestLabAutomationStepResult PropertyOwnershipTitleBoundaries(TestLabAutomationContext context)
        {
            if (!PreparePropertyFixture(context, out PropertyRuntime properties, out _, out _, out _, out PropertyDefinition landDefinition, out _, out _, out string failure))
            {
                return Fail("step11-property-title", failure);
            }

            PropertyOperationResult prepared = PreparePropertyTitle(context, properties, landDefinition, "title-land");
            PropertyOperationResult possession = properties.BeginPossession(new PropertyPossessionRecordData
            {
                possessionId = Scoped(context, "property-possession", "tenant"),
                propertyId = PropertyId(context, "title-land"),
                possessor = PropertySubjectReferenceData.Person("person.prototype.tenant"),
                category = PossessionCategory.TenantPossession,
                startWorldTime = 2d,
                exclusive = true
            });
            PropertyOperationResult partner = properties.CreateOwnership(new PropertyOwnershipInterestData
            {
                ownershipInterestId = Scoped(context, "property-ownership", "partner"),
                propertyId = PropertyId(context, "title-land"),
                owner = PropertySubjectReferenceData.Person("person.prototype.partner"),
                ownershipModel = PropertyOwnershipModel.SharedFractional,
                ownershipShare = new PropertyShareData { units = 5000L, totalUnits = 10000L },
                votingShare = new PropertyShareData { units = 5000L, totalUnits = 10000L },
                economicBenefitShare = new PropertyShareData { units = 5000L, totalUnits = 10000L },
                effectiveStartWorldTime = 3d
            }, 3d);
            PropertyOperationResult badTitle = properties.CreateTitle(Scoped(context, "property-title", "bad"), PropertyId(context, "title-land"), new[] { partner.Ownership?.ownershipInterestId }, 3d);

            bool noTenantOwnership = properties.OwnershipInterests.All(item => item.owner.subjectId != "person.prototype.tenant");
            bool valid = prepared.Succeeded && possession.Succeeded && partner.Succeeded && !badTitle.Succeeded && noTenantOwnership;
            return TestLabAssertions.True("step11-property-title", "Ownership interests, title, possession, and occupancy remain distinct", valid, $"Prepared={prepared.Code} Possession={possession.Code} Partner={partner.Code} BadTitle={badTitle.Code} TenantOwns={!noTenantOwnership}");
        }

        private static TestLabAutomationStepResult PropertyTenancyAccessRent(TestLabAutomationContext context)
        {
            if (!PreparePropertyFixture(context, out PropertyRuntime properties, out EconomyRuntime economy, out CurrencyDefinition gold, out _, out PropertyDefinition landDefinition, out _, out _, out string failure))
            {
                return Fail("step11-property-tenancy", failure);
            }

            PreparePropertyTitle(context, properties, landDefinition, "rental");
            string landlord = Account(context, "property-landlord");
            string tenant = Account(context, "property-tenant");
            economy.CreateAccount(landlord, gold, "person.prototype.owner", EconomyAccountKind.PersonWallet, 0L, Tx(context, "property-landlord-account"));
            economy.CreateAccount(tenant, gold, "person.prototype.tenant", EconomyAccountKind.PersonWallet, 80L, Tx(context, "property-tenant-account"));
            PropertyOperationResult tenancy = properties.CreateTenancy(new PropertyTenancyAgreementData
            {
                tenancyId = Scoped(context, "property-tenancy", "rental"),
                propertyId = PropertyId(context, "rental"),
                landlord = PropertySubjectReferenceData.Person("person.prototype.owner"),
                tenant = PropertySubjectReferenceData.Person("person.prototype.tenant"),
                propertyOwnerInterestIds = new[] { Scoped(context, "property-ownership", "rental-owner") },
                permittedUse = PropertyUseCategory.Residential,
                startWorldTime = 1d,
                endWorldTime = 60d,
                landlordAccountId = landlord,
                tenantAccountId = tenant,
                rentTerms = new PropertyRentTermsData { currencyId = gold.Id, rentUnitsPerPeriod = 10L, depositUnits = 3L, periodLengthWorldTime = 30d },
                grantedAccessCategories = new[] { PropertyAccessCategory.Enter, PropertyAccessCategory.Occupy, PropertyAccessCategory.StoreItems }
            });
            PropertyOperationResult active = properties.ActivateTenancy(Scoped(context, "property-tenancy", "rental"), 1d);
            PropertyOperationResult rent = properties.GenerateRentObligation(Scoped(context, "property-rent", "rental-one"), Scoped(context, "property-tenancy", "rental"), 1d, 31d, 32d);
            PropertyOperationResult partial = properties.PayRent(Scoped(context, "property-rent", "rental-one"), economy, Tx(context, "property-rent-partial"), 6L, 12d);
            PropertyOperationResult overdue = properties.MarkOverdueRent(Scoped(context, "property-rent", "rental-one"), 40d);
            PropertyAccessEvaluationResult access = properties.EvaluateAccess(PropertyId(context, "rental"), PropertySubjectReferenceData.Person("person.prototype.tenant"), PropertyAccessCategory.Enter, 2d);

            bool noOwnershipTransfer = properties.OwnershipInterests.All(item => item.owner.subjectId != "person.prototype.tenant");
            bool valid = tenancy.Succeeded && active.Succeeded && rent.Succeeded && partial.Succeeded && overdue.Succeeded && access.Allowed && noOwnershipTransfer && properties.RentObligations.Single().OutstandingUnits == 4L;
            return TestLabAssertions.True("step11-property-tenancy", "Tenancy grants use access and rent without transferring ownership", valid, $"Tenancy={tenancy.Code} Active={active.Code} Rent={rent.Code} Partial={partial.Code} Overdue={overdue.Code} Access={access.Decision} TenantOwns={!noOwnershipTransfer}");
        }

        private static TestLabAutomationStepResult PropertyTransferRollbackBusinessBoundaries(TestLabAutomationContext context)
        {
            if (!PreparePropertyFixture(context, out PropertyRuntime properties, out EconomyRuntime economy, out CurrencyDefinition gold, out BusinessRuntime businesses, out PropertyDefinition landDefinition, out BusinessDefinition businessDefinition, out _, out string failure))
            {
                return Fail("step11-property-transfer", failure);
            }

            PreparePropertyTitle(context, properties, landDefinition, "shop-land");
            string sellerAccount = Account(context, "property-seller");
            string buyerAccount = Account(context, "property-buyer");
            economy.CreateAccount(sellerAccount, gold, "person.prototype.owner", EconomyAccountKind.PersonWallet, 0L, Tx(context, "property-seller-account"));
            economy.CreateAccount(buyerAccount, gold, "person.prototype.buyer", EconomyAccountKind.PersonWallet, 100L, Tx(context, "property-buyer-account"));
            BusinessOperationResult business = businesses.CreateBusiness(new BusinessInstanceData
            {
                businessId = BusinessId(context, "property-shop"),
                businessDefinitionId = businessDefinition.Id,
                displayName = "Prototype Property Shop",
                founderSubjectIds = new[] { "person.prototype.owner" },
                operatingCurrencyIds = new[] { gold.Id },
                state = BusinessState.Active
            });
            BusinessOperationResult establishment = businesses.AddEstablishment(new BusinessEstablishmentData
            {
                establishmentId = Scoped(context, "business-establishment", "property-shop"),
                businessId = BusinessId(context, "property-shop"),
                type = BusinessEstablishmentType.Shop,
                state = BusinessEstablishmentState.Open
            });
            PropertyOperationResult link = properties.LinkBusinessEstablishment(PropertyId(context, "shop-land"), Scoped(context, "business-establishment", "property-shop"), businesses);
            PropertyOperationResult injected = PropertyTransfer(context, properties, economy, "shop-land", "injected", PropertyTransferCategory.Sale, "person.prototype.owner", "person.prototype.buyer", 4000L, sellerAccount, buyerAccount, gold.Id, "title-creation");
            economy.TryGetAccount(buyerAccount, out EconomyAccountSnapshot buyerAfterInjected);
            economy.TryGetAccount(sellerAccount, out EconomyAccountSnapshot sellerAfterInjected);
            PropertyOperationResult sale = PropertyTransfer(context, properties, economy, "shop-land", "sale", PropertyTransferCategory.Sale, "person.prototype.owner", "person.prototype.buyer", 4000L, sellerAccount, buyerAccount, gold.Id);

            bool balancesSafe = buyerAfterInjected.BalanceUnits == 100L && sellerAfterInjected.BalanceUnits == 0L;
            bool valid = business.Succeeded && establishment.Succeeded && link.Succeeded && !injected.Succeeded && balancesSafe && sale.Succeeded && properties.OwnershipInterests.Any(item => item.owner.subjectId == "person.prototype.buyer" && item.IsActiveAt(6d));
            return TestLabAssertions.True("step11-property-transfer", "Property transfers stage title changes and preserve external runtime boundaries", valid, $"Business={business.Code} Establishment={establishment.Code} Link={link.Code} Injected={injected.Code} BalancesSafe={balancesSafe} Sale={sale.Code}");
        }

        private static TestLabAutomationStepResult PropertyConditionMaintenancePersistence(TestLabAutomationContext context)
        {
            if (!PreparePropertyFixture(context, out PropertyRuntime properties, out _, out _, out _, out PropertyDefinition landDefinition, out _, out ItemDefinition toolDefinition, out string failure))
            {
                return Fail("step11-property-maintenance", failure);
            }

            ItemInstanceIdentityRuntime items = context.ScenarioContext.Runtimes.ItemInstances;
            PreparePropertyTitle(context, properties, landDefinition, "maintenance-land");
            string hammer = RunGuid(context, "property-hammer");
            ItemInstanceOperationResult createTool = items.CreateItem(toolDefinition, itemInstanceId: hammer, ownerPersonId: "person.prototype.worker", custodianPersonId: "person.prototype.worker");
            PropertyOperationResult condition = properties.RecordCondition(new PropertyConditionRecordData { conditionRecordId = Scoped(context, "property-condition", "damaged"), propertyId = PropertyId(context, "maintenance-land"), condition = PropertyConditionState.Damaged, severity = 5, recordedWorldTime = 5d });
            PropertyOperationResult inspection = properties.PerformInspection(new PropertyInspectionRecordData
            {
                inspectionId = Scoped(context, "property-inspection", "damaged"),
                propertyId = PropertyId(context, "maintenance-land"),
                inspector = PropertySubjectReferenceData.Person("person.prototype.inspector"),
                inspectedWorldTime = 6d
            });
            PropertyOperationResult obligation = properties.CreateMaintenanceObligation(new PropertyMaintenanceObligationData
            {
                maintenanceObligationId = Scoped(context, "property-maintenance", "repair"),
                propertyId = PropertyId(context, "maintenance-land"),
                responsibleSubject = PropertySubjectReferenceData.Person("person.prototype.owner"),
                authorizedWorker = PropertySubjectReferenceData.Person("person.prototype.worker"),
                requiredToolItemInstanceIds = new[] { hammer },
                dueWorldTime = 10d
            });
            PropertyOperationResult injected = properties.ExecuteMaintenance(Scoped(context, "property-maintenance", "repair"), PropertySubjectReferenceData.Person("person.prototype.worker"), items, new[] { hammer }, Array.Empty<string>(), string.Empty, string.Empty, 7d, injectFailureStage: "repair");
            PropertyOperationResult repair = properties.ExecuteMaintenance(Scoped(context, "property-maintenance", "repair"), PropertySubjectReferenceData.Person("person.prototype.worker"), items, new[] { hammer }, Array.Empty<string>(), string.Empty, string.Empty, 8d);
            PropertyRuntimeSaveData save = properties.CreateSaveData();
            bool saveValid = PropertyRuntime.ValidateSaveData(save, null, out string saveFailure);
            PropertyRuntime restored = new PropertyRuntime();
            PropertyOperationResult restore = restored.RestoreFromSaveData(save, null);

            bool valid = createTool.Succeeded && condition.Succeeded && inspection.Succeeded && obligation.Succeeded && !injected.Succeeded && repair.Succeeded && saveValid && restore.Succeeded && restored.MaintenanceObligations.Single().state == MaintenanceObligationState.Completed;
            return TestLabAssertions.True("step11-property-maintenance", "Condition, inspection, maintenance, and persistence remain explicit", valid, $"Tool={createTool.Status} Condition={condition.Code} Inspection={inspection.Code} Obligation={obligation.Code} Injected={injected.Code} Repair={repair.Code} Save={saveValid}:{saveFailure} Restore={restore.Code}");
        }

        private static bool PreparePropertyFixture(
            TestLabAutomationContext context,
            out PropertyRuntime properties,
            out EconomyRuntime economy,
            out CurrencyDefinition gold,
            out BusinessRuntime businesses,
            out PropertyDefinition landDefinition,
            out BusinessDefinition businessDefinition,
            out ItemDefinition toolDefinition,
            out string failure)
        {
            properties = context?.ScenarioContext?.Runtimes?.Properties;
            economy = context?.ScenarioContext?.Runtimes?.Economy;
            businesses = context?.ScenarioContext?.Runtimes?.Businesses;
            gold = null;
            landDefinition = null;
            businessDefinition = null;
            toolDefinition = null;
            failure = string.Empty;
            DefinitionRegistry registry = context?.ScenarioContext?.Runtimes?.DefinitionRegistry;
            if (properties == null || economy == null || businesses == null || registry == null)
            {
                failure = $"Property fixture missing runtime. Properties={properties != null} Economy={economy != null} Business={businesses != null} Registry={registry != null}";
                return false;
            }

            if (!registry.TryGet(GoldCurrencyId, out gold))
            {
                failure = $"Currency definition '{GoldCurrencyId}' is missing.";
                return false;
            }

            landDefinition = CreatePrototypePropertyDefinition("property.prototype.land", "Prototype Land Parcel", PropertyCategory.LandParcel, new[] { PropertyCategory.ResidentialBuilding }, new[] { PropertyUseCategory.Residential, PropertyUseCategory.Commercial, PropertyUseCategory.Agricultural }, gold.Id);
            PropertyDefinition buildingDefinition = CreatePrototypePropertyDefinition("property.prototype.building", "Prototype Building", PropertyCategory.ResidentialBuilding, new[] { PropertyCategory.ApartmentUnit }, new[] { PropertyUseCategory.Residential, PropertyUseCategory.Commercial, PropertyUseCategory.Storage }, gold.Id);
            PropertyDefinition unitDefinition = CreatePrototypePropertyDefinition("property.prototype.unit", "Prototype Building Unit", PropertyCategory.ApartmentUnit, Array.Empty<PropertyCategory>(), new[] { PropertyUseCategory.Residential, PropertyUseCategory.Commercial, PropertyUseCategory.Storage }, gold.Id);
            businessDefinition = PrototypeBusinessDefinition();
            toolDefinition = CreateItemDefinition("item.prototype-property-repair-tool", "Prototype Property Repair Tool");
            DefinitionRegistry extended = registry;
            foreach (IGameDefinition definition in new IGameDefinition[] { landDefinition, buildingDefinition, unitDefinition, businessDefinition, toolDefinition })
            {
                extended = ExtendRegistry(extended, definition);
            }

            properties.Configure(extended, context.ScenarioContext.Runtimes.WorldId);
            economy.Configure(extended, context.ScenarioContext.Runtimes.WorldId);
            businesses.Configure(extended, context.ScenarioContext.Runtimes.WorldId);
            return true;
        }

        private static PropertyOperationResult PreparePropertyTitle(TestLabAutomationContext context, PropertyRuntime properties, PropertyDefinition landDefinition, string slug)
        {
            PropertyOperationResult land = RegisterPropertyLand(context, properties, landDefinition, slug);
            if (!land.Succeeded)
            {
                return land;
            }

            PropertyOperationResult owner = properties.CreateOwnership(new PropertyOwnershipInterestData
            {
                ownershipInterestId = Scoped(context, "property-ownership", $"{slug}-owner"),
                propertyId = PropertyId(context, slug),
                owner = PropertySubjectReferenceData.Person("person.prototype.owner"),
                ownershipModel = PropertyOwnershipModel.Sole,
                ownershipShare = PropertyShareData.Full(),
                votingShare = PropertyShareData.Full(),
                economicBenefitShare = PropertyShareData.Full(),
                effectiveStartWorldTime = 1d,
                rights = new[] { PropertyAccessCategory.Manage, PropertyAccessCategory.TransferProperty }
            }, 1d);
            if (!owner.Succeeded)
            {
                return owner;
            }

            return properties.CreateTitle(Scoped(context, "property-title", $"{slug}-initial"), PropertyId(context, slug), new[] { Scoped(context, "property-ownership", $"{slug}-owner") }, 1d);
        }

        private static PropertyOperationResult RegisterPropertyLand(TestLabAutomationContext context, PropertyRuntime properties, PropertyDefinition landDefinition, string slug)
        {
            return properties.RegisterProperty(new PropertyInstanceData
            {
                propertyId = PropertyId(context, slug),
                propertyDefinitionId = landDefinition.Id,
                displayName = $"Prototype {slug}",
                spatialReferenceId = Scoped(context, "place-reference", slug),
                sceneObjectReferenceId = Scoped(context, "scene-reference", slug),
                currentUses = new[] { PropertyUseCategory.Residential },
                creationWorldTime = 1d
            });
        }

        private static PropertyOperationResult RegisterPropertyBuilding(TestLabAutomationContext context, PropertyRuntime properties, string slug, string parent)
        {
            return properties.RegisterProperty(new PropertyInstanceData
            {
                propertyId = PropertyId(context, slug),
                propertyDefinitionId = "property.prototype.building",
                parentPropertyId = parent,
                sceneObjectReferenceId = Scoped(context, "scene-building", slug),
                currentUses = new[] { PropertyUseCategory.Residential }
            });
        }

        private static PropertyOperationResult RegisterPropertyUnit(TestLabAutomationContext context, PropertyRuntime properties, string slug, string parent)
        {
            return properties.RegisterProperty(new PropertyInstanceData
            {
                propertyId = PropertyId(context, slug),
                propertyDefinitionId = "property.prototype.unit",
                parentPropertyId = parent,
                sceneObjectReferenceId = Scoped(context, "scene-unit", slug),
                currentUses = new[] { PropertyUseCategory.Residential }
            });
        }

        private static PropertyOperationResult PropertyTransfer(TestLabAutomationContext context, PropertyRuntime properties, EconomyRuntime economy, string propertySlug, string transferSlug, PropertyTransferCategory category, string from, string to, long shareUnits, string sellerAccount, string buyerAccount, string currencyId, string inject = "")
        {
            return properties.TransferProperty(new PropertyTransferRequestData
            {
                transferId = Scoped(context, "property-transfer", transferSlug),
                propertyId = PropertyId(context, propertySlug),
                transferCategory = category,
                fromOwner = PropertySubjectReferenceData.Person(from),
                toOwner = PropertySubjectReferenceData.Person(to),
                share = new PropertyShareData { units = shareUnits, totalUnits = 10000L },
                sellerAccountId = sellerAccount,
                buyerAccountId = buyerAccount,
                currencyId = currencyId,
                considerationUnits = category == PropertyTransferCategory.Sale ? 40L : 0L,
                effectiveWorldTime = 5d + properties.Transfers.Count,
                approvalAuthorityId = "authority.prototype.registry",
                injectFailureStage = inject
            }, economy);
        }

        private static PropertyDefinition CreatePrototypePropertyDefinition(string id, string display, PropertyCategory category, PropertyCategory[] children, PropertyUseCategory[] uses, string currencyId)
        {
            PropertyDefinition definition = ScriptableObject.CreateInstance<PropertyDefinition>();
            definition.Initialize(id, display, category);
            definition.SetPolicies(children, new[] { PropertyOwnershipModel.Sole, PropertyOwnershipModel.SharedFractional, PropertyOwnershipModel.Business }, uses, currencyId);
            return definition;
        }

        private static string PropertyId(TestLabAutomationContext context, string slug)
        {
            return Scoped(context, "property", slug);
        }

        private static bool TryGetBusinessRuntime(
            TestLabAutomationContext context,
            out BusinessRuntime businesses,
            out DefinitionRegistry extendedRegistry,
            out CurrencyDefinition gold,
            out string failure)
        {
            businesses = context?.ScenarioContext?.Runtimes?.Businesses;
            extendedRegistry = null;
            gold = null;
            failure = string.Empty;
            DefinitionRegistry registry = context?.ScenarioContext?.Runtimes?.DefinitionRegistry;
            if (businesses == null)
            {
                failure = "Business runtime is missing from the Test Lab runtime bundle.";
                return false;
            }

            if (registry == null)
            {
                failure = "Definition registry is missing.";
                return false;
            }

            if (!registry.TryGet(GoldCurrencyId, out gold))
            {
                failure = $"Currency definition '{GoldCurrencyId}' is missing.";
                return false;
            }

            extendedRegistry = BusinessRegistry(context, out _);
            businesses.Configure(extendedRegistry, context.ScenarioContext.Runtimes.WorldId);
            context.ScenarioContext.Runtimes.Economy?.Configure(extendedRegistry, context.ScenarioContext.Runtimes.WorldId);
            context.ScenarioContext.Runtimes.Markets?.Configure(extendedRegistry, context.ScenarioContext.Runtimes.WorldId);
            context.ScenarioContext.Runtimes.Trades?.Configure(extendedRegistry, context.ScenarioContext.Runtimes.WorldId);
            context.ScenarioContext.Runtimes.Payroll?.Configure(extendedRegistry, context.ScenarioContext.Runtimes.WorldId);
            return true;
        }

        private static bool PrepareBusinessFixture(
            TestLabAutomationContext context,
            out BusinessRuntime businesses,
            out EconomyRuntime economy,
            out ItemInstanceIdentityRuntime items,
            out CurrencyDefinition gold,
            out string businessId,
            out string failure)
        {
            businesses = null;
            economy = context?.ScenarioContext?.Runtimes?.Economy;
            items = context?.ScenarioContext?.Runtimes?.ItemInstances;
            gold = null;
            businessId = string.Empty;
            if (!TryGetBusinessRuntime(context, out businesses, out _, out gold, out failure))
            {
                return false;
            }

            if (economy == null)
            {
                failure = "Economy runtime is missing from the Test Lab runtime bundle.";
                return false;
            }

            if (items == null)
            {
                failure = "Item instance runtime is missing from the Test Lab runtime bundle.";
                return false;
            }

            businessId = BusinessId(context, "fixture-shop");
            BusinessOperationResult create = businesses.CreateBusiness(new BusinessInstanceData
            {
                businessId = businessId,
                businessDefinitionId = "business.prototype-merchant-shop",
                displayName = "Prototype Fixture Shop",
                linkedOrganizationId = "organization.prototype.independent",
                founderSubjectIds = new[] { context.ScenarioContext.Runtimes.PersonId },
                operatingCurrencyIds = new[] { gold.Id },
                accessPolicyId = BusinessId(context, "fixture-policy"),
                createdWorldTime = 5d,
                state = BusinessState.Active
            }, Tx(context, "business-fixture-create"));
            BusinessOperationResult owner = businesses.AddOwnership(new BusinessOwnershipRecordData
            {
                ownershipRecordId = BusinessId(context, "fixture-owner"),
                businessId = businessId,
                owner = new BusinessSubjectReferenceData { kind = BusinessOwnerSubjectKind.Person, subjectId = context.ScenarioContext.Runtimes.PersonId },
                category = BusinessOwnershipCategory.SoleOwner,
                economicShare = new BusinessRationalData { numerator = 10000L, denominator = 10000L },
                votingShare = new BusinessRationalData { numerator = 10000L, denominator = 10000L },
                effectiveStartWorldTime = 5d
            }, 5d);

            if (!create.Succeeded || !owner.Succeeded)
            {
                failure = $"Business fixture failed. Create={create.Code} '{create.Message}' Owner={owner.Code} '{owner.Message}'";
                return false;
            }

            return true;
        }

        private static DefinitionRegistry BusinessRegistry(TestLabAutomationContext context, out BusinessDefinition businessDefinition)
        {
            DefinitionRegistry registry = context?.ScenarioContext?.Runtimes?.DefinitionRegistry;
            businessDefinition = PrototypeBusinessDefinition();
            if (registry == null)
            {
                return new DefinitionRegistry(new IGameDefinition[] { businessDefinition });
            }

            return registry.Contains(businessDefinition.Id)
                ? registry
                : new DefinitionRegistry(registry.DefinitionsById.Values.Concat(new IGameDefinition[] { businessDefinition }));
        }

        private static DefinitionRegistry ExtendRegistry(DefinitionRegistry registry, IGameDefinition definition)
        {
            if (registry == null)
            {
                return new DefinitionRegistry(definition == null ? Array.Empty<IGameDefinition>() : new[] { definition });
            }

            if (definition == null || registry.Contains(definition.Id))
            {
                return registry;
            }

            return new DefinitionRegistry(registry.DefinitionsById.Values.Concat(new[] { definition }));
        }

        private static BusinessDefinition PrototypeBusinessDefinition()
        {
            BusinessDefinition definition = ScriptableObject.CreateInstance<BusinessDefinition>();
            definition.Initialize("business.prototype-merchant-shop", "Prototype Merchant Shop", BusinessCategory.MerchantShop);
            return definition;
        }

        private static bool TryGetMarketFixture(
            TestLabAutomationContext context,
            out MarketRuntime markets,
            out DefinitionRegistry extendedRegistry,
            out MarketDefinition marketDefinition,
            out MarketSubjectDefinition subject,
            out CurrencyDefinition gold,
            out string failure)
        {
            markets = context?.ScenarioContext?.Runtimes?.Markets;
            extendedRegistry = null;
            marketDefinition = null;
            subject = null;
            gold = null;
            failure = string.Empty;
            DefinitionRegistry registry = context?.ScenarioContext?.Runtimes?.DefinitionRegistry;
            if (markets == null)
            {
                failure = "Market runtime is missing from the Test Lab runtime bundle.";
                return false;
            }

            if (registry == null)
            {
                failure = "Definition registry is missing.";
                return false;
            }

            if (!registry.TryGet(GoldCurrencyId, out gold))
            {
                failure = $"Currency definition '{GoldCurrencyId}' is missing.";
                return false;
            }

            ItemDefinition sword = registry.TryGet(PrototypeSwordItemId, out ItemDefinition foundSword)
                ? foundSword
                : CreateItemDefinition(PrototypeSwordItemId, "Prototype Sword");
            marketDefinition = ScriptableObject.CreateInstance<MarketDefinition>();
            marketDefinition.Initialize(
                "market.prototype.local",
                "Prototype Local Market",
                gold,
                MarketCategory.LocalSettlement,
                MarketScopeType.Settlement,
                new[] { MarketSubjectKind.ItemDefinition });

            subject = ScriptableObject.CreateInstance<MarketSubjectDefinition>();
            subject.Initialize(
                "market-subject.prototype-sword",
                "Prototype Sword",
                MarketSubjectKind.ItemDefinition,
                sword.Id,
                gold,
                100L,
                MarketQuantityUnit.Each,
                1L);
            SetPrivate(subject, "minimumPriceUnits", 1L);
            SetPrivate(subject, "maximumPriceUnits", 1000L);

            List<IGameDefinition> definitions = registry.DefinitionsById.Values.ToList();
            if (!registry.Contains(sword.Id))
            {
                definitions.Add(sword);
            }

            definitions.Add(marketDefinition);
            definitions.Add(subject);
            extendedRegistry = new DefinitionRegistry(definitions);
            markets.Configure(extendedRegistry, context.ScenarioContext.Runtimes.WorldId);
            context.ScenarioContext.Runtimes.Economy?.Configure(extendedRegistry, context.ScenarioContext.Runtimes.WorldId);
            return true;
        }

        private static MarketObservationRecordData Supply(TestLabAutomationContext context, string marketId, string subjectId, string source, long quantity, long reserved, long available, double observed, double expires = -1d)
        {
            return new MarketObservationRecordData
            {
                observationId = Scoped(context, "market-supply", source),
                marketInstanceId = marketId,
                marketSubjectId = subjectId,
                unit = MarketQuantityUnit.Each,
                quantity = quantity,
                reservedQuantity = reserved,
                availableNowQuantity = available,
                supplySourceCategory = MarketSupplySourceCategory.MerchantInventory,
                sourceReferenceId = source,
                observedWorldTime = observed,
                expiresWorldTime = expires,
                reliability = 9000
            };
        }

        private static MarketObservationRecordData Demand(TestLabAutomationContext context, string marketId, string subjectId, string source, long quantity, long expected, double observed)
        {
            return new MarketObservationRecordData
            {
                observationId = Scoped(context, "market-demand", source),
                marketInstanceId = marketId,
                marketSubjectId = subjectId,
                unit = MarketQuantityUnit.Each,
                quantity = quantity,
                expectedFutureQuantity = expected,
                demandCategory = MarketDemandCategory.Consumer,
                sourceReferenceId = source,
                observedWorldTime = observed,
                reliability = 9000
            };
        }

        private static ItemDefinition CreateItemDefinition(string id, string display)
        {
            ItemDefinition item = ScriptableObject.CreateInstance<ItemDefinition>();
            SetPrivate(item, "itemId", id);
            SetPrivate(item, "displayName", display);
            SetPrivate(item, "stackable", false);
            SetPrivate(item, "maximumStackSize", 1);
            SetPrivate(item, "instanceMode", ItemInstanceMode.AlwaysInstanced);
            return item;
        }

        private static ItemDefinition CreateStackDefinition(string id, string display)
        {
            ItemDefinition item = ScriptableObject.CreateInstance<ItemDefinition>();
            SetPrivate(item, "itemId", id);
            SetPrivate(item, "displayName", display);
            SetPrivate(item, "stackable", true);
            SetPrivate(item, "maximumStackSize", 99);
            SetPrivate(item, "instanceMode", ItemInstanceMode.AlwaysInstanced);
            return item;
        }

        private static TestLabAutomationStepResult ContractProposalActivation(TestLabAutomationContext context)
        {
            if (!TryGetContractRuntime(context, out ContractEconomyRuntime contracts, out EconomyRuntime economy, out CurrencyDefinition gold, out string failure))
            {
                return Fail("step11-contract-proposal", failure);
            }

            CreateContractAccounts(context, economy, gold, customerUnits: 100L, workerUnits: 0L, lenderUnits: 0L, borrowerUnits: 0L);
            ContractProposalData proposal = ContractProposal(context, "service", gold.Id, 25L);
            ContractEconomyOperationResult create = contracts.CreateProposal(proposal, Tx(context, "contract-proposal"));
            ContractEconomyOperationResult acceptCustomer = contracts.AcceptProposal(proposal.proposalId, "party.customer", 1d, Tx(context, "contract-accept-customer"));
            ContractEconomyOperationResult acceptWorker = contracts.AcceptProposal(proposal.proposalId, "party.worker", 1d, Tx(context, "contract-accept-worker"));
            ContractEconomyOperationResult activate = contracts.ActivateProposal(proposal.proposalId, Scoped(context, "contract", "service"), 2d, Tx(context, "contract-activate"));
            bool valid = create.Succeeded
                && acceptCustomer.Succeeded
                && acceptWorker.Succeeded
                && activate.Succeeded
                && contracts.Obligations.Count == 1
                && contracts.Obligations[0].amountDueUnits == 25L
                && economy.TryGetAccount(Account(context, "customer"), out EconomyAccountSnapshot customer)
                && customer.BalanceUnits == 100L;
            return TestLabAssertions.True("step11-contract-proposal", "Proposals activate into versioned contracts and obligations without moving money", valid, $"Create={create.Code} Activate={activate.Code} Obligations={contracts.Obligations.Count}");
        }

        private static TestLabAutomationStepResult ContractObligationPaymentRollback(TestLabAutomationContext context)
        {
            if (!TryGetContractRuntime(context, out ContractEconomyRuntime contracts, out EconomyRuntime economy, out CurrencyDefinition gold, out string failure))
            {
                return Fail("step11-contract-obligation", failure);
            }

            CreateContractAccounts(context, economy, gold, customerUnits: 100L, workerUnits: 0L, lenderUnits: 0L, borrowerUnits: 0L);
            string contractId = ActivateAutomationContract(context, contracts, gold.Id, "service", 40L);
            string obligationId = $"obligation.{contractId}.term.payment";
            ContractEconomyOperationResult failed = contracts.AllocatePaymentToObligation(obligationId, economy, Tx(context, "contract-pay-failed"), 10L, 3d, injectFailureStage: "after-economy-transfer");
            economy.TryGetAccount(Account(context, "customer"), out EconomyAccountSnapshot customerAfterFailure);
            ContractEconomyOperationResult paid = contracts.AllocatePaymentToObligation(obligationId, economy, Tx(context, "contract-pay"), 30L, 4d);
            ContractEconomyOperationResult duplicate = contracts.AllocatePaymentToObligation(obligationId, economy, Tx(context, "contract-pay"), 30L, 4d);
            contracts.TryGetObligation(obligationId, out ContractObligationData obligation);
            economy.TryGetAccount(Account(context, "worker"), out EconomyAccountSnapshot worker);
            bool valid = !failed.Succeeded
                && failed.Code == ContractOperationCode.RolledBack
                && customerAfterFailure != null
                && customerAfterFailure.BalanceUnits == 100L
                && paid.Succeeded
                && duplicate.Succeeded
                && duplicate.Duplicate
                && obligation != null
                && obligation.amountSatisfiedUnits == 30L
                && obligation.OutstandingUnits == 10L
                && worker != null
                && worker.BalanceUnits == 30L;
            return TestLabAssertions.True("step11-contract-obligation", "Obligation payments use EconomyRuntime and roll back injected failures", valid, $"Failed={failed.Code} Paid={paid.Code} Duplicate={duplicate.Code} Satisfied={obligation?.amountSatisfiedUnits}");
        }

        private static TestLabAutomationStepResult ContractLoanInterestCollateral(TestLabAutomationContext context)
        {
            if (!TryGetContractRuntime(context, out ContractEconomyRuntime contracts, out EconomyRuntime economy, out CurrencyDefinition gold, out string failure))
            {
                return Fail("step11-contract-loan", failure);
            }

            CreateContractAccounts(context, economy, gold, customerUnits: 0L, workerUnits: 0L, lenderUnits: 1000L, borrowerUnits: 100L);
            string contractId = ActivateAutomationContract(context, contracts, gold.Id, "loan", 0L);
            LoanData loan = new LoanData
            {
                loanId = Scoped(context, "loan", "prototype"),
                contractId = contractId,
                lenderPartyId = "party.lender",
                borrowerPartyId = "party.borrower",
                lenderAccountId = Account(context, "lender"),
                borrowerAccountId = Account(context, "borrower"),
                currencyId = gold.Id,
                principalUnits = 500L,
                interestRatePerPeriod = new ContractRationalData { numerator = 1L, denominator = 10L, rounding = ContractRoundingMode.Floor },
                state = LoanState.Approved
            };
            ContractEconomyOperationResult create = contracts.CreateLoan(loan, Tx(context, "loan-create"));
            ContractEconomyOperationResult disburse = contracts.DisburseLoan(loan.loanId, economy, Tx(context, "loan-disburse"), 5d);
            ContractEconomyOperationResult schedule = contracts.GenerateRepaymentSchedule(loan.loanId, 5, 10d, 10d, Tx(context, "loan-schedule"));
            ContractEconomyOperationResult accrue = contracts.AccrueLoanInterest(loan.loanId, "period.001", Tx(context, "loan-interest"));
            ContractEconomyOperationResult collateral = contracts.AddCollateral(new CollateralDesignationData
            {
                collateralId = Scoped(context, "collateral", "sword"),
                contractId = contractId,
                loanId = loan.loanId,
                assetKind = CollateralAssetKind.ItemInstance,
                assetId = Scoped(context, "item-instance", "sword"),
                providerPartyId = "party.borrower",
                currencyId = gold.Id,
                estimatedValueUnits = 80L
            }, Tx(context, "loan-collateral"));
            ContractEconomyOperationResult repay = contracts.RepayLoan(loan.loanId, economy, Tx(context, "loan-repay"), 75L, 20d);
            contracts.TryGetLoan(loan.loanId, out LoanData liveLoan);
            economy.TryGetAccount(Account(context, "lender"), out EconomyAccountSnapshot lender);
            economy.TryGetAccount(Account(context, "borrower"), out EconomyAccountSnapshot borrower);
            bool valid = create.Succeeded
                && disburse.Succeeded
                && schedule.Succeeded
                && accrue.Succeeded
                && collateral.Succeeded
                && repay.Succeeded
                && liveLoan != null
                && liveLoan.outstandingPrincipalUnits == 475L
                && liveLoan.accruedInterestOutstandingUnits == 0L
                && liveLoan.collateralIds.Contains(Scoped(context, "collateral", "sword"))
                && contracts.Installments.Count == 5
                && lender != null
                && borrower != null
                && lender.BalanceUnits == 575L
                && borrower.BalanceUnits == 525L;
            return TestLabAssertions.True("step11-contract-loan", "Loans disburse, accrue exact interest, schedule repayment, and track collateral", valid, $"Create={create.Code} Disburse={disburse.Code} Accrue={accrue.Code} Repay={repay.Code} Principal={liveLoan?.outstandingPrincipalUnits}");
        }

        private static TestLabAutomationStepResult ContractPersistenceGraphValidation(TestLabAutomationContext context)
        {
            if (!TryGetContractRuntime(context, out ContractEconomyRuntime contracts, out EconomyRuntime economy, out CurrencyDefinition gold, out string failure))
            {
                return Fail("step11-contract-persistence", failure);
            }

            CreateContractAccounts(context, economy, gold, customerUnits: 100L, workerUnits: 0L, lenderUnits: 0L, borrowerUnits: 0L);
            string contractId = ActivateAutomationContract(context, contracts, gold.Id, "persist", 20L);
            ContractRuntimeSaveData save = contracts.CreateSaveData();
            ContractEconomyRuntime restored = new ContractEconomyRuntime();
            restored.Configure(context.ScenarioContext.Runtimes.DefinitionRegistry, context.ScenarioContext.Runtimes.WorldId);
            ContractEconomyOperationResult restore = restored.RestoreFromSaveData(save, context.ScenarioContext.Runtimes.DefinitionRegistry);
            ContractRuntimeSaveData corrupt = save.Clone();
            corrupt.contracts[0].obligationIds = new[] { "obligation.missing" };
            bool rejected = !ContractEconomyRuntime.ValidateSaveData(corrupt, context.ScenarioContext.Runtimes.DefinitionRegistry, out string validationFailure);
            bool valid = restore.Succeeded
                && restored.TryGetContract(contractId, out _)
                && rejected
                && validationFailure.Contains("missing obligation");
            return TestLabAssertions.True("step11-contract-persistence", "Contract persistence rejects broken graphs before commit", valid, $"Restore={restore.Code} Rejected={rejected} Failure={validationFailure}");
        }

        private static TestLabAutomationStepResult TaxExactRatesAndAssessments(TestLabAutomationContext context)
        {
            if (!TryGetRevenueFixture(context, out TaxAutomationFixture fixture, out string failure))
            {
                return Fail("step11-revenue-assessment", failure);
            }

            fixture.CreateAccounts(100L, 0L, 0L);
            fixture.RegisterAuthorityAndAccounts();
            long fixedCharge = InstitutionalRevenueRuntime.CalculateCharge(TaxFixed(25L), 999L);
            long proportional = InstitutionalRevenueRuntime.CalculateCharge(TaxPercent(1L, 10L), 255L);
            long perUnit = InstitutionalRevenueRuntime.CalculateCharge(TaxPerUnit(3L), 7L);
            long progressive = InstitutionalRevenueRuntime.CalculateCharge(TaxProgressive(), 250L);
            long threshold = InstitutionalRevenueRuntime.CalculateCharge(TaxThreshold(100L, 12L), 100L);
            string eventId = Scoped(context, "revenue-event", "sale");
            fixture.RegisterEvent(eventId, fixture.SalesTax, 200L, 200L, RevenueSubjectRole.AssessedParty);
            TaxBaseCalculationData basePreview = fixture.Revenue.CalculateBasePreview(fixture.SalesTax.Id, eventId, 5d, out string baseFailure);
            InstitutionalRevenueOperationResult preview = fixture.Revenue.GenerateAssessment(Scoped(context, "revenue-assessment", "sale-preview"), fixture.SalesTax.Id, new[] { eventId }, fixture.AuthorityId, Scoped(context, "revenue-period", "sale-preview"), 6d, approve: true, preview: true);
            InstitutionalRevenueOperationResult assessment = fixture.Revenue.GenerateAssessment(Scoped(context, "revenue-assessment", "sale"), fixture.SalesTax.Id, new[] { eventId }, fixture.AuthorityId, Scoped(context, "revenue-period", "sale"), 6d, approve: true, transactionId: Tx(context, "revenue-assess-sale"));
            InstitutionalRevenueOperationResult duplicate = fixture.Revenue.GenerateAssessment(Scoped(context, "revenue-assessment", "sale-duplicate"), fixture.SalesTax.Id, new[] { eventId }, fixture.AuthorityId, Scoped(context, "revenue-period", "sale"), 7d, approve: true, transactionId: Tx(context, "revenue-assess-duplicate"));

            bool valid = fixedCharge == 25L
                && proportional == 25L
                && perUnit == 21L
                && progressive == 25L
                && threshold == 12L
                && basePreview != null
                && string.IsNullOrWhiteSpace(baseFailure)
                && basePreview.finalTaxableBaseUnits == 200L
                && preview.Succeeded
                && preview.Preview
                && assessment.Succeeded
                && fixture.Revenue.AssessmentCount == 1
                && assessment.Assessment.finalAssessedUnits == 20L
                && duplicate.Code == RevenueOperationCode.AlreadyAssessed;
            return TestLabAssertions.True("step11-revenue-assessment", "Exact revenue rates generate immutable assessments without moving money", valid, $"Fixed={fixedCharge} Proportional={proportional} PerUnit={perUnit} Progressive={progressive} Threshold={threshold} Base={basePreview?.finalTaxableBaseUnits} Preview={preview.Code} Assessment={assessment.Code} Duplicate={duplicate.Code}");
        }

        private static TestLabAutomationStepResult TaxRepresentativeCharges(TestLabAutomationContext context)
        {
            if (!TryGetRevenueFixture(context, out TaxAutomationFixture fixture, out string failure))
            {
                return Fail("step11-revenue-charges", failure);
            }

            fixture.CreateAccounts(500L, 0L, 0L);
            fixture.RegisterAuthorityAndAccounts();
            InstitutionalRevenueOperationResult tariff = fixture.AssessSingleEvent(context, fixture.ImportTariff, "import", 4L, 4L, RevenueSubjectRole.AssessedParty);
            InstitutionalRevenueOperationResult toll = fixture.AssessSingleEvent(context, fixture.Toll, "route-use", 1L, 1L, RevenueSubjectRole.AssessedParty);
            InstitutionalRevenueOperationResult license = fixture.AssessSingleEvent(context, fixture.LicenseFee, "license", 1L, 1L, RevenueSubjectRole.AssessedParty);
            InstitutionalRevenueOperationResult fine = fixture.AssessSingleEvent(context, fixture.Fine, "fine", 50L, 50L, RevenueSubjectRole.AssessedParty);

            bool valid = tariff.Succeeded
                && tariff.Assessment.finalAssessedUnits == 12L
                && toll.Succeeded
                && toll.Assessment.finalAssessedUnits == 5L
                && license.Succeeded
                && license.Assessment.finalAssessedUnits == 15L
                && fine.Succeeded
                && fine.Assessment.finalAssessedUnits == 50L;
            return TestLabAssertions.True("step11-revenue-charges", "Tariffs, tolls, license fees, and fines require explicit source events", valid, $"Tariff={tariff.Code}:{tariff.Assessment?.finalAssessedUnits} Toll={toll.Code}:{toll.Assessment?.finalAssessedUnits} License={license.Code}:{license.Assessment?.finalAssessedUnits} Fine={fine.Code}:{fine.Assessment?.finalAssessedUnits}");
        }

        private static TestLabAutomationStepResult TaxCollectionAllocationRefund(TestLabAutomationContext context)
        {
            if (!TryGetRevenueFixture(context, out TaxAutomationFixture fixture, out string failure))
            {
                return Fail("step11-revenue-collection", failure);
            }

            fixture.CreateAccounts(100L, 100L, 0L);
            fixture.RegisterAuthorityAndAccounts();
            string eventId = Scoped(context, "revenue-event", "adjusted");
            fixture.RegisterEvent(eventId, fixture.SalesTax, 200L, 200L, RevenueSubjectRole.AssessedParty);
            fixture.Revenue.RegisterExemption(fixture.Adjustment(context, "exemption", fixture.SalesTax.Id, 20L), fixture.AuthorityId, Tx(context, "revenue-exemption"));
            fixture.Revenue.RegisterDeduction(fixture.Adjustment(context, "deduction", fixture.SalesTax.Id, 30L), fixture.AuthorityId, Tx(context, "revenue-deduction"));
            fixture.Revenue.RegisterCredit(fixture.Adjustment(context, "credit", fixture.SalesTax.Id, 4L), fixture.AuthorityId, Tx(context, "revenue-credit"));
            InstitutionalRevenueOperationResult assessment = fixture.Revenue.GenerateAssessment(Scoped(context, "revenue-assessment", "adjusted"), fixture.SalesTax.Id, new[] { eventId }, fixture.AuthorityId, Scoped(context, "revenue-period", "adjusted"), 10d, approve: true, transactionId: Tx(context, "revenue-assess-adjusted"));
            InstitutionalRevenueOperationResult failed = fixture.Revenue.PayObligation(assessment.Obligation.obligationId, fixture.Economy, Tx(context, "revenue-pay-failed"), 5L, 11d, injectFailureStage: "after-economy-transfer");
            InstitutionalRevenueOperationResult payment = fixture.Revenue.PayObligation(assessment.Obligation.obligationId, fixture.Economy, Tx(context, "revenue-pay-adjusted"), assessment.Obligation.amountDueUnits, 12d);
            InstitutionalRevenueOperationResult revenue = fixture.Revenue.RecognizeRevenue(payment.Payment.paymentId, Scoped(context, "institutional-revenue", "adjusted"), "tax.sales", Tx(context, "revenue-recognize"));
            InstitutionalRevenueOperationResult allocation = fixture.Revenue.AllocateRevenue(revenue.RevenueRecord.revenueRecordId, fixture.Economy, fixture.AuthorityId, fixture.AllocationAccountId, Tx(context, "revenue-allocate"), 5L, 13d);
            InstitutionalRevenueOperationResult penalty = fixture.Revenue.ApplyPenalty(Scoped(context, "revenue-penalty", "late"), assessment.Obligation.obligationId, fixture.AuthorityId, TaxFixed(3L), 14d, "late", Tx(context, "revenue-penalty"));
            InstitutionalRevenueOperationResult waiver = fixture.Revenue.WaiveObligation(Scoped(context, "revenue-waiver", "hardship"), assessment.Obligation.obligationId, fixture.AuthorityId, 2L, 15d, Tx(context, "revenue-waiver"));
            InstitutionalRevenueOperationResult refund = fixture.Revenue.RefundPayment(Scoped(context, "revenue-refund", "adjusted"), payment.Payment.paymentId, fixture.Economy, fixture.AuthorityId, 5L, 16d, "overpayment", Tx(context, "revenue-refund"));

            fixture.Economy.TryGetAccount(fixture.PayerAccountId, out EconomyAccountSnapshot payer);
            fixture.Economy.TryGetAccount(fixture.TreasuryAccountId, out EconomyAccountSnapshot treasury);
            fixture.Economy.TryGetAccount(fixture.AllocationAccountId, out EconomyAccountSnapshot allocationAccount);
            bool valid = assessment.Succeeded
                && assessment.Assessment.finalAssessedUnits == 11L
                && failed.Code == RevenueOperationCode.RolledBack
                && payment.Succeeded
                && revenue.Succeeded
                && allocation.Succeeded
                && penalty.Succeeded
                && waiver.Succeeded
                && refund.Succeeded
                && payer != null
                && treasury != null
                && allocationAccount != null
                && payer.BalanceUnits == 94L
                && treasury.BalanceUnits == 101L
                && allocationAccount.BalanceUnits == 5L;
            return TestLabAssertions.True("step11-revenue-collection", "Collection, allocation, waiver, penalty, and refund use EconomyRuntime atomically", valid, $"Assessment={assessment.Code}:{assessment.Assessment?.finalAssessedUnits} Failed={failed.Code} Payment={payment.Code} Revenue={revenue.Code} Allocation={allocation.Code} Penalty={penalty.Code} Waiver={waiver.Code} Refund={refund.Code} Payer={payer?.BalanceUnits} Treasury={treasury?.BalanceUnits} Allocation={allocationAccount?.BalanceUnits}");
        }

        private static TestLabAutomationStepResult TaxWithholdingRemittance(TestLabAutomationContext context)
        {
            if (!TryGetRevenueFixture(context, out TaxAutomationFixture fixture, out string failure))
            {
                return Fail("step11-revenue-withholding", failure);
            }

            fixture.CreateAccounts(100L, 0L, 0L, 0L);
            fixture.RegisterAuthorityAndAccounts();
            string eventId = Scoped(context, "revenue-event", "payroll");
            fixture.RegisterEvent(eventId, fixture.PayrollWithholding, 100L, 100L, RevenueSubjectRole.AssessedParty, "person.employer");
            InstitutionalRevenueOperationResult assessment = fixture.Revenue.GenerateAssessment(Scoped(context, "revenue-assessment", "payroll"), fixture.PayrollWithholding.Id, new[] { eventId }, fixture.AuthorityId, Scoped(context, "revenue-period", "payroll"), 10d, approve: true, transactionId: Tx(context, "revenue-assess-payroll"));
            InstitutionalRevenueOperationResult withholding = fixture.Revenue.WithholdFromPayment(new WithholdingRecordData
            {
                withholdingId = Scoped(context, "revenue-withholding", "payroll"),
                assessmentId = assessment.Assessment.assessmentId,
                revenueDefinitionId = fixture.PayrollWithholding.Id,
                withholdingAgentSubjectId = "person.employer",
                remittingPartySubjectId = "person.employer",
                withheldFromAccountId = fixture.PayerAccountId,
                holdingAccountId = fixture.HoldingAccountId,
                institutionAccountId = fixture.TreasuryAccountId,
                currencyId = fixture.Gold.Id,
                withheldUnits = assessment.Assessment.finalAssessedUnits
            }, fixture.Economy, Tx(context, "revenue-withhold"));
            InstitutionalRevenueOperationResult remit = fixture.Revenue.RemitWithholding(withholding.Withholding.withholdingId, fixture.Economy, Tx(context, "revenue-remit"), withholding.Withholding.withheldUnits, 11d);

            fixture.Economy.TryGetAccount(fixture.PayerAccountId, out EconomyAccountSnapshot payer);
            fixture.Economy.TryGetAccount(fixture.HoldingAccountId, out EconomyAccountSnapshot holding);
            fixture.Economy.TryGetAccount(fixture.TreasuryAccountId, out EconomyAccountSnapshot treasury);
            bool valid = assessment.Succeeded
                && assessment.Assessment.finalAssessedUnits == 10L
                && withholding.Succeeded
                && remit.Succeeded
                && fixture.Revenue.WithholdingCount == 1
                && payer != null
                && holding != null
                && treasury != null
                && payer.BalanceUnits == 90L
                && holding.BalanceUnits == 0L
                && treasury.BalanceUnits == 10L;
            return TestLabAssertions.True("step11-revenue-withholding", "Withholding and remittance remain separate from direct tax payment", valid, $"Assessment={assessment.Code} Withhold={withholding.Code} Remit={remit.Code} Payer={payer?.BalanceUnits} Holding={holding?.BalanceUnits} Treasury={treasury?.BalanceUnits}");
        }

        private static TestLabAutomationStepResult TaxFilingsAuditsProjectionPersistence(TestLabAutomationContext context)
        {
            if (!TryGetRevenueFixture(context, out TaxAutomationFixture fixture, out string failure))
            {
                return Fail("step11-revenue-persistence", failure);
            }

            fixture.CreateAccounts(100L, 0L, 0L);
            fixture.RegisterAuthorityAndAccounts();
            string eventId = Scoped(context, "revenue-event", "filing");
            fixture.RegisterEvent(eventId, fixture.SalesTax, 100L, 100L, RevenueSubjectRole.AssessedParty);
            string assessmentId = Scoped(context, "revenue-assessment", "filing");
            InstitutionalRevenueOperationResult assessment = fixture.Revenue.GenerateAssessment(assessmentId, fixture.SalesTax.Id, new[] { eventId }, fixture.AuthorityId, Scoped(context, "revenue-period", "filing"), 10d, approve: true, transactionId: Tx(context, "revenue-assess-filing"));
            string filingId = Scoped(context, "revenue-filing", "sales");
            InstitutionalRevenueOperationResult filing = fixture.Revenue.SubmitFiling(new RevenueFilingData
            {
                filingId = filingId,
                periodId = Scoped(context, "revenue-period", "filing"),
                revenueDefinitionId = fixture.SalesTax.Id,
                reportingSubjectId = "person.taxpayer",
                declaredTaxableEventIds = new[] { eventId }
            }, Tx(context, "revenue-filing"));
            InstitutionalRevenueOperationResult audit = fixture.Revenue.AuditFiling(Scoped(context, "revenue-audit", "filing"), filingId, fixture.AuditAuthorityId, RevenueAuditFindingKind.Match, assessment.Assessment.assessmentId, "Filing matches the assessed event.", 15d, Tx(context, "revenue-audit"));
            InstitutionalRevenueOperationResult statement = fixture.Revenue.GenerateStatement(Scoped(context, "revenue-statement", "taxpayer"), "person.taxpayer", fixture.Gold.Id, 16d);
            InformationAccessProjection<InstitutionalAssessmentData> projection = fixture.Revenue.GetAssessmentProjection(assessmentId, fixture.Access, new InformationAccessContext { RequestingPersonId = "person.public" });
            InstitutionalRevenueRuntimeSaveData save = fixture.Revenue.CreateSaveData();
            InstitutionalRevenueRuntime restored = new InstitutionalRevenueRuntime();
            restored.Configure(fixture.Registry, context.ScenarioContext.Runtimes.WorldId);
            InstitutionalRevenueOperationResult restore = restored.RestoreFromSaveData(save, fixture.Registry);
            InstitutionalRevenueRuntimeSaveData corrupt = save.Clone();
            corrupt.obligations[0].assessmentId = Scoped(context, "revenue-assessment", "missing");
            bool rejected = !InstitutionalRevenueRuntime.ValidateSaveData(corrupt, fixture.Registry, out string validationFailure);

            bool valid = assessment.Succeeded
                && filing.Succeeded
                && audit.Succeeded
                && statement.Succeeded
                && statement.Statement.totalDueUnits == 10L
                && projection.Record != null
                && (projection.Redacted || projection.Denied)
                && restore.Succeeded
                && restored.TryGetAssessment(assessmentId, out InstitutionalAssessmentData restoredAssessment)
                && restoredAssessment.finalAssessedUnits == 10L
                && rejected
                && validationFailure.Contains("missing Assessment", StringComparison.OrdinalIgnoreCase);
            return TestLabAssertions.True("step11-revenue-persistence", "Filings, audits, projections, and persistence preserve institutional revenue state", valid, $"Assessment={assessment.Code} Filing={filing.Code} Audit={audit.Code} Statement={statement.Code} Projection={projection.Message} Restore={restore.Code} Rejected={rejected} Failure={validationFailure}");
        }

        private static TestLabAutomationStepResult RegionalFlowPoolsAndAggregation(TestLabAutomationContext context)
        {
            if (!TryGetRegionalFlowFixture(context, out RegionalFlowAutomationFixture fixture, out string failure))
            {
                return Fail("step11-regional-pools", failure);
            }

            RegionalFlowOperationResult region = fixture.CreateRegion(fixture.RegionA);
            RegionalFlowOperationResult pool = fixture.CreatePool(fixture.FoodPoolA, fixture.RegionA, fixture.Food.Id, 10L);
            RegionalFlowOperationResult preview = fixture.Flow.ApplyQuantityOperation(RegionalOp("regional.preview", AggregateQuantityOperationKind.Add, fixture.Food.Id, string.Empty, fixture.FoodPoolA, 5L), preview: true);
            RegionalFlowOperationResult add = fixture.Flow.ApplyQuantityOperation(RegionalOp("regional.add", AggregateQuantityOperationKind.Add, fixture.Food.Id, string.Empty, fixture.FoodPoolA, 5L), Tx(context, "regional-add"));
            RegionalFlowOperationResult duplicate = fixture.Flow.ApplyQuantityOperation(RegionalOp("regional.add", AggregateQuantityOperationKind.Add, fixture.Food.Id, string.Empty, fixture.FoodPoolA, 5L), Tx(context, "regional-add"));
            RegionalFlowOperationResult aggregate = fixture.Flow.ApplyQuantityOperation(RegionalOp("regional.aggregate", AggregateQuantityOperationKind.AggregateExactItems, fixture.Food.Id, string.Empty, fixture.FoodPoolA, 3L, "exact.stack.1"), Tx(context, "regional-aggregate"));
            RegionalFlowOperationResult doubleCount = fixture.Flow.ApplyQuantityOperation(RegionalOp("regional.aggregate.double", AggregateQuantityOperationKind.AggregateExactItems, fixture.Food.Id, string.Empty, fixture.FoodPoolA, 3L, "exact.stack.1"), Tx(context, "regional-aggregate-double"));

            fixture.Flow.TryGetPool(fixture.FoodPoolA, out CommodityPoolData poolSnapshot);
            bool valid = region.Succeeded && pool.Succeeded && preview.Preview && add.Succeeded && duplicate.Duplicate && aggregate.Succeeded && !doubleCount.Succeeded && poolSnapshot != null && poolSnapshot.totalQuantity == 18L;
            return TestLabAssertions.True("step11-regional-pools", "Regions, commodity pools, and exact aggregation remain deterministic", valid, $"Region={region.Code} Pool={pool.Code} Preview={preview.Code} Add={add.Code} Duplicate={duplicate.Code} Aggregate={aggregate.Code} Double={doubleCount.Code} Quantity={poolSnapshot?.totalQuantity}");
        }

        private static TestLabAutomationStepResult RegionalFlowProductionConsumptionMarket(TestLabAutomationContext context)
        {
            if (!TryGetRegionalFlowFixture(context, out RegionalFlowAutomationFixture fixture, out string failure))
            {
                return Fail("step11-regional-production", failure);
            }

            fixture.CreateRegion(fixture.RegionA);
            fixture.CreatePool(fixture.OrePoolA, fixture.RegionA, fixture.Ore.Id, 10L);
            fixture.CreatePool(fixture.MetalPoolA, fixture.RegionA, fixture.Metal.Id, 0L);
            fixture.CreatePool(fixture.FoodPoolA, fixture.RegionA, fixture.Food.Id, 6L);
            fixture.CreateLaborCohort(2L, 5L);
            fixture.CreateMarket();

            ProductionCapacityResultData capacity = fixture.Flow.EvaluateProductionCapacity(Scoped(context, "regional-capacity", "smelting"), fixture.RegionA, fixture.CohortA, fixture.Smelting, 1d, new[] { fixture.OrePoolA });
            RegionalFlowOperationResult production = fixture.Flow.ExecuteAggregateProduction(Scoped(context, "regional-production", "smelting"), fixture.RegionA, fixture.CohortA, fixture.Smelting, new[] { fixture.OrePoolA }, new[] { fixture.MetalPoolA }, 1L, 1d, fixture.Markets, fixture.MarketId, Tx(context, "regional-production"));
            RegionalFlowOperationResult consumption = fixture.Flow.ExecuteAggregateConsumption(Scoped(context, "regional-consumption", "food"), fixture.RegionA, fixture.CohortA, fixture.Consumption, new[] { fixture.FoodPoolA }, 1L, 1d, fixture.Markets, fixture.MarketId, Tx(context, "regional-consumption"));

            fixture.Flow.TryGetPool(fixture.OrePoolA, out CommodityPoolData ore);
            fixture.Flow.TryGetPool(fixture.MetalPoolA, out CommodityPoolData metal);
            fixture.Flow.TryGetPool(fixture.FoodPoolA, out CommodityPoolData food);
            bool valid = capacity.effectiveOutputCapacity == 2L && production.Succeeded && consumption.Succeeded && ore.totalQuantity == 8L && metal.totalQuantity == 2L && food.totalQuantity == 4L && fixture.Markets.SupplyCount == 1 && fixture.Markets.DemandCount == 1;
            return TestLabAssertions.True("step11-regional-production", "Production and consumption mutate pools and publish market observations", valid, $"Capacity={capacity.effectiveOutputCapacity} Production={production.Code} Consumption={consumption.Code} Ore={ore?.totalQuantity} Metal={metal?.totalQuantity} Food={food?.totalQuantity} Supply={fixture.Markets.SupplyCount} Demand={fixture.Markets.DemandCount}");
        }

        private static TestLabAutomationStepResult RegionalFlowLaborWealthShortage(TestLabAutomationContext context)
        {
            if (!TryGetRegionalFlowFixture(context, out RegionalFlowAutomationFixture fixture, out string failure))
            {
                return Fail("step11-regional-derived", failure);
            }

            fixture.CreateRegion(fixture.RegionA);
            fixture.CreatePool(fixture.FoodPoolA, fixture.RegionA, fixture.Food.Id, 5L);
            RegionalFlowOperationResult cohort = fixture.CreateLaborCohort(8L, 10L);
            LaborMarketSnapshotData labor = fixture.Flow.EvaluateLaborMarket(Scoped(context, "regional-labor", "general"), fixture.RegionA, LaborCategory.GeneralLabor, 12L, 1d);
            WealthSummaryData wealth = fixture.Flow.EvaluateWealth(Scoped(context, "regional-wealth", "cohort"), fixture.RegionA, new RegionalSubjectReferenceData { subjectKind = "cohort", subjectId = fixture.CohortA }, fixture.Gold.Id, 50L, 100L, 30L, 1d);
            ShortageSurplusData shortage = fixture.Flow.EvaluateCommodityShortage(Scoped(context, "regional-shortage", "food"), fixture.RegionA, fixture.Food.Id, 8L, 2L, 1d);

            bool valid = cohort.Succeeded && labor.supplyUnits == 8L && labor.demandUnits == 12L && labor.wagePressure == WagePressureState.Upward && wealth.netEstimatedWealthUnits == 120L && shortage.state == ShortageState.Shortage && shortage.shortageKind == ShortageKind.Commodity;
            return TestLabAssertions.True("step11-regional-derived", "Labor, wealth, shortage, and wage pressure are derived from aggregate state", valid, $"Cohort={cohort.Code} Labor={labor.supplyUnits}/{labor.demandUnits} Pressure={labor.wagePressure} Wealth={wealth.netEstimatedWealthUnits} Shortage={shortage.state}");
        }

        private static TestLabAutomationStepResult RegionalFlowTradeConservation(TestLabAutomationContext context)
        {
            if (!TryGetRegionalFlowFixture(context, out RegionalFlowAutomationFixture fixture, out string failure))
            {
                return Fail("step11-regional-flow", failure);
            }

            fixture.CreateRegion(fixture.RegionA);
            fixture.CreateRegion(fixture.RegionB);
            fixture.CreatePool(fixture.FoodPoolA, fixture.RegionA, fixture.Food.Id, 20L);
            fixture.CreatePool(fixture.FoodPoolB, fixture.RegionB, fixture.Food.Id, 0L);
            RegionalFlowOperationResult connection = fixture.CreateConnection(10L, 2L);
            RegionalFlowOperationResult plan = fixture.Flow.PlanFlow(Scoped(context, "regional-flow", "food"), fixture.ConnectionAB, fixture.FoodPoolA, fixture.FoodPoolB, fixture.Food.Id, 6L, 1L);
            RegionalFlowOperationResult reserve = fixture.Flow.ReserveFlow(plan.FlowOrder, Tx(context, "regional-flow-reserve"));
            RegionalFlowOperationResult early = fixture.Flow.ArriveFlow(plan.FlowOrder.flowOrderId, 1L, 1d, transactionId: Tx(context, "regional-flow-early"));
            RegionalFlowOperationResult depart = fixture.Flow.DepartFlow(plan.FlowOrder.flowOrderId, 1L, 1d, Tx(context, "regional-flow-depart"));
            RegionalFlowOperationResult arrive = fixture.Flow.ArriveFlow(plan.FlowOrder.flowOrderId, 3L, 3d, lossUnits: 1L, transactionId: Tx(context, "regional-flow-arrive"));

            fixture.Flow.TryGetPool(fixture.FoodPoolA, out CommodityPoolData source);
            fixture.Flow.TryGetPool(fixture.FoodPoolB, out CommodityPoolData destination);
            bool valid = connection.Succeeded && plan.Preview && reserve.Succeeded && !early.Succeeded && depart.Succeeded && arrive.Succeeded && source.totalQuantity == 14L && destination.totalQuantity == 5L;
            return TestLabAssertions.True("step11-regional-flow", "Inter-region flow planning, departure, arrival, and loss conserve quantities", valid, $"Connection={connection.Code} Plan={plan.Code} Reserve={reserve.Code} Early={early.Code} Depart={depart.Code} Arrive={arrive.Code} Source={source?.totalQuantity} Destination={destination?.totalQuantity}");
        }

        private static TestLabAutomationStepResult RegionalFlowCycleAccessPersistence(TestLabAutomationContext context)
        {
            if (!TryGetRegionalFlowFixture(context, out RegionalFlowAutomationFixture fixture, out string failure))
            {
                return Fail("step11-regional-hardening", failure);
            }

            fixture.Access.RegisterPolicy(new InformationAccessPolicyData
            {
                policyId = fixture.PrivatePoolPolicyId,
                subject = RegionalFlowInformationSubject.Create("economy.pool", fixture.FoodPoolA, fixture.RegionA, new[] { fixture.Food.Id }),
                classification = InformationVisibilityClassification.Private,
                detailVisibilityPolicy = InformationDetailVisibilityPolicy.Redacted,
                defaultVisibleDetails = new[] { "detail.region" },
                defaultRedactedDetails = new[] { "detail.quantity", "detail.provenance" },
                redactedAccessAcceptable = true
            }, Tx(context, "regional-policy"));
            fixture.Access.GrantAccess(new InformationAccessGrantData
            {
                grantId = Scoped(context, "information-grant", "regional-pool-redacted"),
                policyId = fixture.PrivatePoolPolicyId,
                subject = RegionalFlowInformationSubject.Create("economy.pool", fixture.FoodPoolA, fixture.RegionA, new[] { fixture.Food.Id }),
                granteeKind = InformationGranteeKind.Person,
                granteeId = "person.public",
                grantorId = context.ScenarioContext.Runtimes.PersonId,
                accessModes = new[] { InformationAccessMode.Inspect },
                detailIds = new[] { "detail.region" }
            }, Tx(context, "regional-grant"));

            fixture.CreateRegion(fixture.RegionA);
            fixture.CreatePool(fixture.FoodPoolA, fixture.RegionA, fixture.Food.Id, 12L, fixture.PrivatePoolPolicyId);
            fixture.CreatePool(fixture.OrePoolA, fixture.RegionA, fixture.Ore.Id, 10L);
            fixture.CreatePool(fixture.MetalPoolA, fixture.RegionA, fixture.Metal.Id, 0L);
            fixture.CreateLaborCohort(2L, 5L);
            RegionalFlowRuntimeSaveData before = fixture.Flow.CreateSaveData();
            RegionalFlowOperationResult failed = fixture.Flow.RunEconomicCycle(Scoped(context, "regional-cycle", "fail"), fixture.RegionA, 1L, 1d, new[] { fixture.Smelting }, Array.Empty<AggregateConsumptionProfileDefinition>(), injectFailureStage: "after-production");
            RegionalFlowOperationResult cycle = fixture.Flow.RunEconomicCycle(Scoped(context, "regional-cycle", "ok"), fixture.RegionA, 2L, 2d, new[] { fixture.Smelting }, Array.Empty<AggregateConsumptionProfileDefinition>());
            RegionalFlowOperationResult duplicate = fixture.Flow.RunEconomicCycle(Scoped(context, "regional-cycle", "ok"), fixture.RegionA, 2L, 2d, new[] { fixture.Smelting }, Array.Empty<AggregateConsumptionProfileDefinition>());
            InformationAccessProjection<CommodityPoolData> publicProjection = fixture.Flow.GetPoolProjection(fixture.FoodPoolA, fixture.Access, new InformationAccessContext { RequestingPersonId = "person.public", RedactedAccessAcceptable = true });
            InformationAccessProjection<CommodityPoolData> privilegedProjection = fixture.Flow.GetPoolProjection(fixture.FoodPoolA, fixture.Access, new InformationAccessContext { RequestingPersonId = "person.admin", ContextKind = InformationContextKind.Debug });

            RegionalFlowPersistenceParticipant participant = new RegionalFlowPersistenceParticipant(fixture.Flow, () => fixture.Registry);
            RegionalFlowRuntimeSaveData corrupt = fixture.Flow.CreateSaveData();
            corrupt.pools[0].commodityId = "commodity.missing";
            PersistenceParticipantPrepareResult rejected = participant.PreparePayload(JsonUtility.ToJson(corrupt), RegionalFlowPersistenceParticipant.CurrentParticipantSchemaVersion);

            fixture.Flow.TryGetPool(fixture.OrePoolA, out CommodityPoolData ore);
            bool rollback = failed.Code == RegionalFlowResultCode.RolledBack && before.pools.Single(item => item.poolId == fixture.OrePoolA).totalQuantity == 10L;
            bool valid = rollback
                && cycle.Succeeded
                && cycle.Cycle != null
                && cycle.Cycle.succeeded
                && duplicate.Duplicate
                && publicProjection.Redacted
                && publicProjection.Record != null
                && publicProjection.Record.totalQuantity == 0L
                && privilegedProjection.FullAccess
                && privilegedProjection.Record != null
                && privilegedProjection.Record.totalQuantity == 12L
                && !rejected.Succeeded
                && ore != null
                && ore.totalQuantity == 8L;
            return TestLabAssertions.True("step11-regional-hardening", "Cycles, access projections, rollback, and persistence validation are explicit", valid, $"Failed={failed.Code} Cycle={cycle.Code} Duplicate={duplicate.Code} PublicRedacted={publicProjection.Redacted} PublicRecord={publicProjection.Record != null} Privileged={privilegedProjection.FullAccess} PrivilegedRecord={privilegedProjection.Record != null} Reject={rejected.Message} Ore={ore?.totalQuantity}");
        }

        private static bool TryGetRegionalFlowFixture(TestLabAutomationContext context, out RegionalFlowAutomationFixture fixture, out string failure)
        {
            fixture = null;
            failure = string.Empty;
            TestLabRuntimeBundle runtimes = context?.ScenarioContext?.Runtimes;
            RegionalFlowRuntime flow = runtimes?.RegionalFlow;
            MarketRuntime markets = runtimes?.Markets;
            InformationAccessRuntime access = runtimes?.Access;
            DefinitionRegistry registry = runtimes?.DefinitionRegistry;
            if (flow == null || markets == null || access == null || registry == null)
            {
                failure = flow == null ? "Regional Flow runtime is missing." : markets == null ? "Market runtime is missing." : access == null ? "Information Access runtime is missing." : "Definition registry is missing.";
                return false;
            }

            if (!registry.TryGet(GoldCurrencyId, out CurrencyDefinition gold))
            {
                failure = $"Currency definition '{GoldCurrencyId}' is missing.";
                return false;
            }

            fixture = RegionalFlowAutomationFixture.Create(context, registry, flow, markets, access, gold);
            return true;
        }

        private static AggregateQuantityOperationData RegionalOp(string id, AggregateQuantityOperationKind kind, string commodityId, string sourcePoolId, string destinationPoolId, long quantity, string sourceEventId = "")
        {
            return new AggregateQuantityOperationData
            {
                operationId = id,
                operationKind = kind,
                commodityId = commodityId,
                sourcePoolId = sourcePoolId,
                destinationPoolId = destinationPoolId,
                unit = CommodityUnit.Each,
                quantity = quantity,
                sourceEventId = sourceEventId,
                authorityId = "test-lab.regional-flow"
            };
        }

        private static bool TryGetRuntime(TestLabAutomationContext context, out EconomyRuntime economy, out CurrencyDefinition gold, out string failure)
        {
            economy = context?.ScenarioContext?.Runtimes?.Economy;
            gold = null;
            failure = string.Empty;
            DefinitionRegistry registry = context?.ScenarioContext?.Runtimes?.DefinitionRegistry;
            if (economy == null)
            {
                failure = "Economy runtime is missing from the Test Lab runtime bundle.";
                return false;
            }

            if (registry == null)
            {
                failure = "Definition registry is missing.";
                return false;
            }

            if (!registry.TryGet(GoldCurrencyId, out gold))
            {
                failure = $"Currency definition '{GoldCurrencyId}' is missing.";
                return false;
            }

            economy.Configure(registry, context.ScenarioContext.Runtimes.WorldId);
            return true;
        }

        private static bool TryGetContractRuntime(TestLabAutomationContext context, out ContractEconomyRuntime contracts, out EconomyRuntime economy, out CurrencyDefinition gold, out string failure)
        {
            contracts = context?.ScenarioContext?.Runtimes?.Contracts;
            if (!TryGetRuntime(context, out economy, out gold, out failure))
            {
                return false;
            }

            if (contracts == null)
            {
                failure = "Contract economy runtime is missing from the Test Lab runtime bundle.";
                return false;
            }

            contracts.Configure(context.ScenarioContext.Runtimes.DefinitionRegistry, context.ScenarioContext.Runtimes.WorldId);
            return true;
        }

        private static void CreateContractAccounts(TestLabAutomationContext context, EconomyRuntime economy, CurrencyDefinition gold, long customerUnits, long workerUnits, long lenderUnits, long borrowerUnits)
        {
            economy.CreateAccount(Account(context, "customer"), gold, "person.customer", EconomyAccountKind.PersonWallet, customerUnits, Tx(context, "open-customer"));
            economy.CreateAccount(Account(context, "worker"), gold, "person.worker", EconomyAccountKind.PersonWallet, workerUnits, Tx(context, "open-worker"));
            economy.CreateAccount(Account(context, "lender"), gold, "person.lender", EconomyAccountKind.PersonWallet, lenderUnits, Tx(context, "open-lender"));
            economy.CreateAccount(Account(context, "borrower"), gold, "person.borrower", EconomyAccountKind.PersonWallet, borrowerUnits, Tx(context, "open-borrower"));
        }

        private static ContractProposalData ContractProposal(TestLabAutomationContext context, string slug, string currencyId, long amountUnits)
        {
            return new ContractProposalData
            {
                proposalId = Scoped(context, "contract-proposal", slug),
                category = amountUnits <= 0L ? EconomicContractCategory.Loan : EconomicContractCategory.Service,
                state = ContractProposalState.Offered,
                createdByPartyId = amountUnits <= 0L ? "party.lender" : "party.customer",
                parties = amountUnits <= 0L
                    ? new[]
                    {
                        new ContractPartyData { partyId = "party.lender", role = ContractPartyRole.Lender, reference = ContractPartyReferenceData.Person("person.lender"), accountId = Account(context, "lender") },
                        new ContractPartyData { partyId = "party.borrower", role = ContractPartyRole.Borrower, reference = ContractPartyReferenceData.Person("person.borrower"), accountId = Account(context, "borrower") }
                    }
                    : new[]
                    {
                        new ContractPartyData { partyId = "party.customer", role = ContractPartyRole.Debtor, reference = ContractPartyReferenceData.Person("person.customer"), accountId = Account(context, "customer") },
                        new ContractPartyData { partyId = "party.worker", role = ContractPartyRole.Creditor, reference = ContractPartyReferenceData.Person("person.worker"), accountId = Account(context, "worker") }
                    },
                terms = new[]
                {
                    new ContractTermData
                    {
                        termId = "term.payment",
                        category = amountUnits <= 0L ? ContractTermCategory.General : ContractTermCategory.Payment,
                        responsiblePartyId = amountUnits <= 0L ? "party.borrower" : "party.customer",
                        beneficiaryPartyId = amountUnits <= 0L ? "party.lender" : "party.worker",
                        currencyId = currencyId,
                        amountUnits = amountUnits,
                        dueWorldTime = 10d
                    }
                }
            };
        }

        private static string ActivateAutomationContract(TestLabAutomationContext context, ContractEconomyRuntime contracts, string currencyId, string slug, long amountUnits)
        {
            ContractProposalData proposal = ContractProposal(context, slug, currencyId, amountUnits);
            string firstParty = amountUnits <= 0L ? "party.lender" : "party.customer";
            string secondParty = amountUnits <= 0L ? "party.borrower" : "party.worker";
            string contractId = Scoped(context, "contract", slug);
            contracts.CreateProposal(proposal, Tx(context, slug + "-proposal"));
            contracts.AcceptProposal(proposal.proposalId, firstParty, 1d, Tx(context, slug + "-accept-a"));
            contracts.AcceptProposal(proposal.proposalId, secondParty, 1d, Tx(context, slug + "-accept-b"));
            contracts.ActivateProposal(proposal.proposalId, contractId, 2d, Tx(context, slug + "-activate"));
            return contractId;
        }

        private static bool TryGetPhysicalRuntime(TestLabAutomationContext context, out EconomyRuntime economy, out CurrencyDefinition currency, out ItemDefinition coin, out string failure)
        {
            economy = context?.ScenarioContext?.Runtimes?.Economy;
            currency = null;
            coin = null;
            failure = string.Empty;
            DefinitionRegistry registry = context?.ScenarioContext?.Runtimes?.DefinitionRegistry;
            if (economy == null || registry == null)
            {
                failure = economy == null ? "Economy runtime is missing." : "Definition registry is missing.";
                return false;
            }

            coin = ScriptableObject.CreateInstance<ItemDefinition>();
            SetPrivate(coin, "itemId", CoinItemId);
            SetPrivate(coin, "displayName", "Prototype Gold Coin");
            SetPrivate(coin, "stackable", true);
            SetPrivate(coin, "maximumStackSize", 999);
            SetPrivate(coin, "instanceMode", ItemInstanceMode.AlwaysInstanced);

            currency = ScriptableObject.CreateInstance<CurrencyDefinition>();
            currency.Initialize(CoinCurrencyId, "Prototype Coin Currency", "G", physicalItem: coin, physicalUnits: 1L, issuer: "issuer.prototype");
            DefinitionRegistry extended = new DefinitionRegistry(registry.DefinitionsById.Values.Concat(new IGameDefinition[] { coin, currency }));
            economy.Configure(extended, context.ScenarioContext.Runtimes.WorldId);
            return true;
        }

        private static ITestLabAutomationSuite Suite(string suiteId, string displayName, string feature, int order, IReadOnlyList<string> required, params ITestLabAutomationScenario[] scenarios)
        {
            return new TestLabAutomationSuite(suiteId, displayName, feature, $"{displayName} runtime integration scenarios.", order, TestLabAutomationCategory.Standard, includeInRunAll: true, requiredServices: required, scenarios: scenarios);
        }

        private static ITestLabAutomationScenario Scenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                TestLabAutomationCategory.Standard,
                includeInQuickRun: true,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Economy | TestLabRuntimeArea.Items | TestLabRuntimeArea.KnowledgeHistory,
                requiredDefinitionIds: new[] { GoldCurrencyId });
        }

        private static ITestLabAutomationScenario PayrollScenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                TestLabAutomationCategory.Standard,
                includeInQuickRun: true,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Economy | TestLabRuntimeArea.Professions | TestLabRuntimeArea.KnowledgeHistory,
                requiredDefinitionIds: new[] { GoldCurrencyId });
        }

        private static ITestLabAutomationScenario BusinessScenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                TestLabAutomationCategory.Standard,
                includeInQuickRun: true,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Economy | TestLabRuntimeArea.Items | TestLabRuntimeArea.Professions | TestLabRuntimeArea.KnowledgeHistory,
                requiredDefinitionIds: new[] { GoldCurrencyId });
        }

        private static ITestLabAutomationScenario PropertyScenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                TestLabAutomationCategory.Standard,
                includeInQuickRun: true,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Economy | TestLabRuntimeArea.Items | TestLabRuntimeArea.Professions | TestLabRuntimeArea.KnowledgeHistory,
                requiredDefinitionIds: new[] { GoldCurrencyId });
        }

        private static ITestLabAutomationScenario ContractScenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                TestLabAutomationCategory.Standard,
                includeInQuickRun: true,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Economy | TestLabRuntimeArea.Items | TestLabRuntimeArea.Professions | TestLabRuntimeArea.KnowledgeHistory,
                requiredDefinitionIds: new[] { GoldCurrencyId });
        }

        private static ITestLabAutomationScenario TaxScenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                TestLabAutomationCategory.Standard,
                includeInQuickRun: true,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Economy | TestLabRuntimeArea.KnowledgeHistory,
                requiredDefinitionIds: new[] { GoldCurrencyId });
        }

        private static ITestLabAutomationScenario RegionalFlowScenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                TestLabAutomationCategory.Standard,
                includeInQuickRun: true,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Economy | TestLabRuntimeArea.KnowledgeHistory,
                requiredDefinitionIds: new[] { GoldCurrencyId });
        }

        private static ITestLabAutomationScenario IntegrationScenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                TestLabAutomationCategory.Standard,
                includeInQuickRun: true,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.FreshRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Economy | TestLabRuntimeArea.Items | TestLabRuntimeArea.Professions | TestLabRuntimeArea.KnowledgeHistory,
                requiredDefinitionIds: new[] { GoldCurrencyId });
        }

        private static EconomyIntegrationFacade CreateIntegrationFacade(TestLabAutomationContext context)
        {
            TestLabRuntimeBundle runtimes = context?.ScenarioContext?.Runtimes;
            return new EconomyIntegrationFacade(
                runtimes?.DefinitionRegistry,
                runtimes?.Economy,
                runtimes?.Markets,
                runtimes?.Trades,
                runtimes?.Payroll,
                runtimes?.Businesses,
                runtimes?.Properties,
                runtimes?.Contracts,
                runtimes?.InstitutionalRevenue,
                runtimes?.RegionalFlow,
                runtimes?.Access,
                runtimes?.WorldId);
        }

        private static string Diagnostics(IEnumerable<EconomicIntegrationDiagnosticData> diagnostics)
        {
            return diagnostics == null ? "None" : string.Join(" | ", diagnostics.Select(item => $"{item.severity}:{item.code}:{item.message}"));
        }

        private static ITestLabScenarioStep Step(string stepId, string displayName, Func<TestLabAutomationContext, TestLabAutomationStepResult> action)
        {
            return new TestLabScenarioStep(stepId, displayName, action);
        }

        private static TestLabAutomationStepResult Fail(string stepId, string diagnostics)
        {
            return TestLabAssertions.Fail(stepId, "Currency and Transactions", "OperationSucceeded", "Succeeded", "Failed", diagnostics);
        }

        private static string Account(TestLabAutomationContext context, string slug)
        {
            return context.ScenarioContext.ScopedId("economy-account", slug);
        }

        private static string Tx(TestLabAutomationContext context, string slug)
        {
            return context.ScenarioContext.ScopedId("economy-tx", slug);
        }

        private static string MarketId(TestLabAutomationContext context, string slug)
        {
            return Scoped(context, "market-instance", slug);
        }

        private static string BusinessId(TestLabAutomationContext context, string slug)
        {
            return Scoped(context, "business", slug);
        }

        private static string Scoped(TestLabAutomationContext context, string prefix, string slug)
        {
            return context.ScenarioContext.ScopedId(prefix, slug);
        }

        private static bool TryGetRevenueFixture(TestLabAutomationContext context, out TaxAutomationFixture fixture, out string failure)
        {
            fixture = null;
            failure = string.Empty;
            EconomyRuntime economy = context?.ScenarioContext?.Runtimes?.Economy;
            InstitutionalRevenueRuntime revenue = context?.ScenarioContext?.Runtimes?.InstitutionalRevenue;
            InformationAccessRuntime access = context?.ScenarioContext?.Runtimes?.Access;
            DefinitionRegistry registry = context?.ScenarioContext?.Runtimes?.DefinitionRegistry;
            if (economy == null || revenue == null || registry == null)
            {
                failure = economy == null ? "Economy runtime is missing." : revenue == null ? "Institutional revenue runtime is missing." : "Definition registry is missing.";
                return false;
            }

            if (!registry.TryGet(GoldCurrencyId, out CurrencyDefinition gold))
            {
                failure = $"Currency definition '{GoldCurrencyId}' is missing.";
                return false;
            }

            fixture = TaxAutomationFixture.Create(context, registry, economy, revenue, access ?? new InformationAccessRuntime(), gold);
            return true;
        }

        private static RevenueRatePolicyData TaxFixed(long units)
        {
            return new RevenueRatePolicyData { ratePolicyId = "rate.revenue.fixed." + units, rateKind = RevenueRateKind.FixedAmount, fixedUnits = units, smallestChargeableUnit = 1L };
        }

        private static RevenueRatePolicyData TaxPercent(long numerator, long denominator)
        {
            return new RevenueRatePolicyData { ratePolicyId = $"rate.revenue.percent.{numerator}.{denominator}", rateKind = RevenueRateKind.FlatProportional, rate = new RevenueRationalData { numerator = numerator, denominator = denominator }, smallestChargeableUnit = 1L };
        }

        private static RevenueRatePolicyData TaxPerUnit(long units)
        {
            return new RevenueRatePolicyData { ratePolicyId = "rate.revenue.per-unit." + units, rateKind = RevenueRateKind.PerUnit, perUnitUnits = units, smallestChargeableUnit = 1L };
        }

        private static RevenueRatePolicyData TaxThreshold(long threshold, long fixedUnits)
        {
            return new RevenueRatePolicyData
            {
                ratePolicyId = "rate.revenue.threshold",
                rateKind = RevenueRateKind.ThresholdCharge,
                thresholdUnits = threshold,
                smallestChargeableUnit = 1L,
                brackets = new[]
                {
                    new RevenueBracketData { bracketId = "threshold", lowerInclusive = 0L, upperExclusive = -1L, fixedUnits = fixedUnits, rate = new RevenueRationalData { numerator = 0L, denominator = 1L } }
                }
            };
        }

        private static RevenueRatePolicyData TaxProgressive()
        {
            return new RevenueRatePolicyData
            {
                ratePolicyId = "rate.revenue.progressive",
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

        private sealed class RegionalFlowAutomationFixture
        {
            private RegionalFlowAutomationFixture(TestLabAutomationContext context, DefinitionRegistry baseRegistry, RegionalFlowRuntime flow, MarketRuntime markets, InformationAccessRuntime access, CurrencyDefinition gold)
            {
                Context = context;
                Gold = gold;
                Food = Commodity(Scoped(context, "commodity", "food"), "Automation Food", CommodityCategory.Food, Scoped(context, "market-subject", "food"));
                Ore = Commodity(Scoped(context, "commodity", "ore"), "Automation Ore", CommodityCategory.Ore, Scoped(context, "market-subject", "ore"));
                Metal = Commodity(Scoped(context, "commodity", "metal"), "Automation Metal", CommodityCategory.Metal, Scoped(context, "market-subject", "metal"));
                RegionDefinition = ScriptableObject.CreateInstance<EconomicRegionDefinition>();
                RegionDefinition.Initialize(Scoped(context, "economic-region-definition", "settlement"), "Automation Settlement Economy", EconomicRegionCategory.SettlementEconomy, new[] { LaborCategory.GeneralLabor });
                Smelting = Production(Scoped(context, "production-profile", "smelting"), Ore.Id, Metal.Id);
                Consumption = ConsumptionProfile(Scoped(context, "consumption-profile", "food"), Food.Id);
                MarketDefinition = ScriptableObject.CreateInstance<MarketDefinition>();
                MarketDefinition.Initialize(Scoped(context, "market-definition", "regional"), "Automation Regional Market", gold);
                FoodSubject = MarketSubject(Food.MarketSubjectId, "Automation Food Subject", Food.Id, gold, 2L);
                MetalSubject = MarketSubject(Metal.MarketSubjectId, "Automation Metal Subject", Metal.Id, gold, 5L);
                Registry = new DefinitionRegistry(baseRegistry.DefinitionsById.Values.Concat(new IGameDefinition[] { Food, Ore, Metal, RegionDefinition, Smelting, Consumption, MarketDefinition, FoodSubject, MetalSubject }));
                Flow = flow;
                Markets = markets;
                Access = access;
                RegionA = Scoped(context, "economic-region", "a");
                RegionB = Scoped(context, "economic-region", "b");
                FoodPoolA = Scoped(context, "commodity-pool", "food-a");
                FoodPoolB = Scoped(context, "commodity-pool", "food-b");
                OrePoolA = Scoped(context, "commodity-pool", "ore-a");
                MetalPoolA = Scoped(context, "commodity-pool", "metal-a");
                CohortA = Scoped(context, "economic-cohort", "laborers");
                ConnectionAB = Scoped(context, "trade-connection", "a-b");
                MarketId = Scoped(context, "market-instance", "regional-flow");
                PrivatePoolPolicyId = Scoped(context, "information-policy", "regional-pool");
                Flow.Configure(Registry, context.ScenarioContext.Runtimes.WorldId);
                Markets.Configure(Registry, context.ScenarioContext.Runtimes.WorldId);
                Access.Configure(Registry, context.ScenarioContext.Runtimes.PersonId);
            }

            public TestLabAutomationContext Context { get; }
            public DefinitionRegistry Registry { get; }
            public RegionalFlowRuntime Flow { get; }
            public MarketRuntime Markets { get; }
            public InformationAccessRuntime Access { get; }
            public CurrencyDefinition Gold { get; }
            public CommodityDefinition Food { get; }
            public CommodityDefinition Ore { get; }
            public CommodityDefinition Metal { get; }
            public EconomicRegionDefinition RegionDefinition { get; }
            public AggregateProductionProfileDefinition Smelting { get; }
            public AggregateConsumptionProfileDefinition Consumption { get; }
            public MarketDefinition MarketDefinition { get; }
            public MarketSubjectDefinition FoodSubject { get; }
            public MarketSubjectDefinition MetalSubject { get; }
            public string RegionA { get; }
            public string RegionB { get; }
            public string FoodPoolA { get; }
            public string FoodPoolB { get; }
            public string OrePoolA { get; }
            public string MetalPoolA { get; }
            public string CohortA { get; }
            public string ConnectionAB { get; }
            public string MarketId { get; }
            public string PrivatePoolPolicyId { get; }

            public static RegionalFlowAutomationFixture Create(TestLabAutomationContext context, DefinitionRegistry baseRegistry, RegionalFlowRuntime flow, MarketRuntime markets, InformationAccessRuntime access, CurrencyDefinition gold)
            {
                return new RegionalFlowAutomationFixture(context, baseRegistry, flow, markets, access, gold);
            }

            public RegionalFlowOperationResult CreateRegion(string regionId)
            {
                return Flow.RegisterRegion(new EconomicRegionData
                {
                    regionId = regionId,
                    regionDefinitionId = RegionDefinition.Id,
                    displayName = regionId,
                    state = EconomicRegionState.Active,
                    simulationFidelity = RegionalSimulationFidelity.AggregatePools
                }, Tx(Context, "regional-region-" + regionId));
            }

            public RegionalFlowOperationResult CreatePool(string poolId, string regionId, string commodityId, long quantity, string accessPolicyId = "")
            {
                return Flow.RegisterCommodityPool(new CommodityPoolData
                {
                    poolId = poolId,
                    regionId = regionId,
                    commodityId = commodityId,
                    unit = CommodityUnit.Each,
                    totalQuantity = quantity,
                    purpose = CommodityPoolPurpose.GeneralRegionalSupply,
                    owner = new RegionalSubjectReferenceData { subjectKind = "cohort", subjectId = CohortA },
                    accessPolicyId = accessPolicyId
                }, Tx(Context, "regional-pool-" + poolId));
            }

            public RegionalFlowOperationResult CreateLaborCohort(long laborUnits, long population)
            {
                return Flow.RegisterCohort(new EconomicCohortData
                {
                    cohortId = CohortA,
                    regionId = RegionA,
                    category = EconomicCohortCategory.Laborers,
                    populationQuantity = population,
                    laborDistribution = new[] { new RegionalLaborQuantityData { laborCategory = LaborCategory.GeneralLabor, units = laborUnits } },
                    accountId = Account(Context, "regional-cohort"),
                    commodityPoolIds = new[] { FoodPoolA }
                }, Tx(Context, "regional-cohort"));
            }

            public RegionalFlowOperationResult CreateConnection(long capacity, long leadTime)
            {
                return Flow.RegisterTradeConnection(new TradeConnectionData
                {
                    connectionId = ConnectionAB,
                    sourceRegionId = RegionA,
                    destinationRegionId = RegionB,
                    permittedCommodityIds = new[] { Food.Id },
                    capacityUnits = capacity,
                    leadTimeUnits = leadTime,
                    state = TradeConnectionState.Active
                }, Tx(Context, "regional-connection"));
            }

            public void CreateMarket()
            {
                Markets.CreateMarketInstance(MarketDefinition, MarketId, RegionA);
            }

            private static CommodityDefinition Commodity(string id, string name, CommodityCategory category, string marketSubjectId)
            {
                CommodityDefinition definition = ScriptableObject.CreateInstance<CommodityDefinition>();
                definition.Initialize(id, name, category, CommodityUnit.Each, marketSubjectId);
                return definition;
            }

            private static MarketSubjectDefinition MarketSubject(string id, string name, string commodityId, CurrencyDefinition gold, long priceUnits)
            {
                MarketSubjectDefinition subject = ScriptableObject.CreateInstance<MarketSubjectDefinition>();
                subject.Initialize(id, name, MarketSubjectKind.Custom, commodityId, gold, priceUnits, MarketQuantityUnit.Each, 1L);
                return subject;
            }

            private static AggregateProductionProfileDefinition Production(string id, string inputCommodity, string outputCommodity)
            {
                RegionalCommodityQuantityDefinitionData input = new RegionalCommodityQuantityDefinitionData();
                input.Initialize(inputCommodity, CommodityUnit.Each, 2L);
                RegionalCommodityQuantityDefinitionData output = new RegionalCommodityQuantityDefinitionData();
                output.Initialize(outputCommodity, CommodityUnit.Each, 2L);
                AggregateProductionProfileDefinition profile = ScriptableObject.CreateInstance<AggregateProductionProfileDefinition>();
                profile.Initialize(id, "Automation Smelting", ProductionProfileCategory.Smelting, new[] { output }, new[] { input }, new[] { Labor(LaborCategory.GeneralLabor, 1L) });
                return profile;
            }

            private static AggregateConsumptionProfileDefinition ConsumptionProfile(string id, string commodityId)
            {
                RegionalCommodityQuantityDefinitionData item = new RegionalCommodityQuantityDefinitionData();
                item.Initialize(commodityId, CommodityUnit.Each, 2L);
                AggregateConsumptionProfileDefinition profile = ScriptableObject.CreateInstance<AggregateConsumptionProfileDefinition>();
                profile.Initialize(id, "Automation Food Consumption", ConsumptionProfileCategory.HouseholdNeed, new[] { item });
                return profile;
            }

            private static RegionalLaborQuantityDefinitionData Labor(LaborCategory category, long units)
            {
                RegionalLaborQuantityDefinitionData labor = new RegionalLaborQuantityDefinitionData();
                labor.Initialize(category, units);
                return labor;
            }
        }

        private sealed class TaxAutomationFixture
        {
            private TaxAutomationFixture(TestLabAutomationContext context, DefinitionRegistry registry, EconomyRuntime economy, InstitutionalRevenueRuntime revenue, InformationAccessRuntime access, CurrencyDefinition gold)
            {
                Context = context;
                Gold = gold;
                SalesTax = Definition(Scoped(context, "revenue-definition", "sales-tax"), "Automation Sales Tax", gold, InstitutionalRevenueCategory.SalesTaxFoundation, TaxableEventCategory.CompletedTrade, TaxBaseKind.TransactionGrossAmount, TaxPercent(1L, 10L), RevenueAccountPurpose.TaxCollection, false);
                PayrollWithholding = Definition(Scoped(context, "revenue-definition", "payroll-withholding"), "Automation Payroll Withholding", gold, InstitutionalRevenueCategory.PayrollTax, TaxableEventCategory.PayrollPayment, TaxBaseKind.PayrollGrossPay, TaxPercent(1L, 10L), RevenueAccountPurpose.PayrollContributionCollection, true);
                ImportTariff = Definition(Scoped(context, "revenue-definition", "import-tariff"), "Automation Import Tariff", gold, InstitutionalRevenueCategory.ImportTariff, TaxableEventCategory.ItemImportFoundation, TaxBaseKind.ItemQuantity, TaxPerUnit(3L), RevenueAccountPurpose.CustomsCollection, false);
                Toll = Definition(Scoped(context, "revenue-definition", "toll"), "Automation Toll", gold, InstitutionalRevenueCategory.Toll, TaxableEventCategory.RouteUseFoundation, TaxBaseKind.RouteUsageCount, TaxFixed(5L), RevenueAccountPurpose.TollCollection, false);
                LicenseFee = Definition(Scoped(context, "revenue-definition", "license-fee"), "Automation License Fee", gold, InstitutionalRevenueCategory.LicenseFee, TaxableEventCategory.LicenseApplication, TaxBaseKind.AdministrativeServiceOccurrence, TaxFixed(15L), RevenueAccountPurpose.LicenseRevenue, false);
                Fine = Definition(Scoped(context, "revenue-definition", "fine"), "Automation Fine", gold, InstitutionalRevenueCategory.Fine, TaxableEventCategory.ExternalFineDecision, TaxBaseKind.ExternalFineAmount, TaxFixed(50L), RevenueAccountPurpose.FineCollection, false);
                Registry = new DefinitionRegistry(registry.DefinitionsById.Values.Concat(new IGameDefinition[] { SalesTax, PayrollWithholding, ImportTariff, Toll, LicenseFee, Fine }));
                Economy = economy;
                Revenue = revenue;
                Access = access;
                AuthorityId = Scoped(context, "revenue-authority", "tax-office");
                AuditAuthorityId = Scoped(context, "revenue-authority", "audit-office");
                InstitutionId = Scoped(context, "institution", "city");
                PayerAccountId = Account(context, "revenue-taxpayer");
                TreasuryAccountId = Account(context, "revenue-treasury");
                AllocationAccountId = Account(context, "revenue-roads");
                HoldingAccountId = Account(context, "revenue-withholding");
                Economy.Configure(Registry, context.ScenarioContext.Runtimes.WorldId);
                Revenue.Configure(Registry, context.ScenarioContext.Runtimes.WorldId);
                Access.Configure(Registry, context.ScenarioContext.Runtimes.PersonId);
            }

            public TestLabAutomationContext Context { get; }
            public DefinitionRegistry Registry { get; }
            public EconomyRuntime Economy { get; }
            public InstitutionalRevenueRuntime Revenue { get; }
            public InformationAccessRuntime Access { get; }
            public CurrencyDefinition Gold { get; }
            public InstitutionalRevenueDefinition SalesTax { get; }
            public InstitutionalRevenueDefinition PayrollWithholding { get; }
            public InstitutionalRevenueDefinition ImportTariff { get; }
            public InstitutionalRevenueDefinition Toll { get; }
            public InstitutionalRevenueDefinition LicenseFee { get; }
            public InstitutionalRevenueDefinition Fine { get; }
            public string AuthorityId { get; }
            public string AuditAuthorityId { get; }
            public string InstitutionId { get; }
            public string PayerAccountId { get; }
            public string TreasuryAccountId { get; }
            public string AllocationAccountId { get; }
            public string HoldingAccountId { get; }

            public static TaxAutomationFixture Create(TestLabAutomationContext context, DefinitionRegistry registry, EconomyRuntime economy, InstitutionalRevenueRuntime revenue, InformationAccessRuntime access, CurrencyDefinition gold)
            {
                return new TaxAutomationFixture(context, registry, economy, revenue, access, gold);
            }

            public void CreateAccounts(long payerUnits, long treasuryUnits, long allocationUnits, long holdingUnits = 0L)
            {
                Economy.CreateAccount(PayerAccountId, Gold, "person.taxpayer", EconomyAccountKind.PersonWallet, payerUnits, Tx(Context, "revenue-account-payer"));
                Economy.CreateAccount(TreasuryAccountId, Gold, InstitutionId, EconomyAccountKind.OrganizationAccount, treasuryUnits, Tx(Context, "revenue-account-treasury"));
                Economy.CreateAccount(AllocationAccountId, Gold, InstitutionId, EconomyAccountKind.OrganizationAccount, allocationUnits, Tx(Context, "revenue-account-allocation"));
                Economy.CreateAccount(HoldingAccountId, Gold, "person.employer", EconomyAccountKind.OrganizationAccount, holdingUnits, Tx(Context, "revenue-account-holding"));
            }

            public void RegisterAuthorityAndAccounts()
            {
                Revenue.RegisterAuthority(Authority(AuthorityId, InstitutionalRevenueAuthorityCategory.Assess, assess: true, collect: true, refund: true, waive: true, allocate: true, audit: true), Tx(Context, "revenue-authority-tax"));
                Revenue.RegisterAuthority(Authority(AuditAuthorityId, InstitutionalRevenueAuthorityCategory.Audit, assess: false, collect: false, refund: false, waive: false, allocate: false, audit: true), Tx(Context, "revenue-authority-audit"));
                Assign(RevenueAccountPurpose.TaxCollection, TreasuryAccountId, AuthorityId);
                Assign(RevenueAccountPurpose.PayrollContributionCollection, TreasuryAccountId, AuthorityId);
                Assign(RevenueAccountPurpose.CustomsCollection, TreasuryAccountId, AuthorityId);
                Assign(RevenueAccountPurpose.TollCollection, TreasuryAccountId, AuthorityId);
                Assign(RevenueAccountPurpose.LicenseRevenue, TreasuryAccountId, AuthorityId);
                Assign(RevenueAccountPurpose.FineCollection, TreasuryAccountId, AuthorityId);
                Assign(RevenueAccountPurpose.RevenueDistribution, AllocationAccountId, AuthorityId);
            }

            public InstitutionalRevenueOperationResult AssessSingleEvent(TestLabAutomationContext context, InstitutionalRevenueDefinition definition, string slug, long monetaryValueUnits, long quantityUnits, RevenueSubjectRole role)
            {
                string eventId = Scoped(context, "revenue-event", slug);
                RegisterEvent(eventId, definition, monetaryValueUnits, quantityUnits, role);
                return Revenue.GenerateAssessment(Scoped(context, "revenue-assessment", slug), definition.Id, new[] { eventId }, AuthorityId, Scoped(context, "revenue-period", slug), 10d, approve: true, transactionId: Tx(context, "revenue-assess-" + slug));
            }

            public void RegisterEvent(string eventId, InstitutionalRevenueDefinition definition, long monetaryValueUnits, long quantityUnits, RevenueSubjectRole role, string withholdingAgent = "")
            {
                Revenue.RegisterTaxableEvent(new TaxableEventData
                {
                    taxableEventId = eventId,
                    revenueDefinitionId = definition.Id,
                    eligibleCategory = definition.Category,
                    eventCategory = definition.TaxableEventCategories.First(),
                    assessedSubject = Subject("person.taxpayer", PayerAccountId, role),
                    otherSubjects = string.IsNullOrWhiteSpace(withholdingAgent)
                        ? Array.Empty<RevenueSubjectReferenceData>()
                        : new[]
                        {
                            Subject(withholdingAgent, HoldingAccountId, RevenueSubjectRole.WithholdingAgent),
                            Subject(withholdingAgent, HoldingAccountId, RevenueSubjectRole.RemittingParty)
                        },
                    institutionId = InstitutionId,
                    currencyId = Gold.Id,
                    eventWorldTime = 1d,
                    monetaryValueUnits = monetaryValueUnits,
                    quantityUnits = quantityUnits,
                    sourceRuntime = "test-lab.automation",
                    sourceRecordId = eventId
                }, AuthorityId, Tx(Context, "revenue-event-" + eventId));
            }

            public RevenueAdjustmentData Adjustment(TestLabAutomationContext context, string slug, string definitionId, long amountUnits)
            {
                return new RevenueAdjustmentData
                {
                    adjustmentId = Scoped(context, "revenue-adjustment", slug),
                    revenueDefinitionId = definitionId,
                    subjectId = "person.taxpayer",
                    amountUnits = amountUnits,
                    approvalAuthorityId = AuthorityId,
                    sourceReferenceId = Scoped(context, "revenue-adjustment-source", slug)
                };
            }

            private void Assign(RevenueAccountPurpose purpose, string accountId, string authorityId)
            {
                Revenue.AssignRevenueAccount(new InstitutionalRevenueAccountAssignmentData
                {
                    assignmentId = Scoped(Context, "revenue-account-assignment", purpose.ToString()),
                    institutionId = InstitutionId,
                    institutionKind = InstitutionKind.Organization,
                    accountId = accountId,
                    purpose = purpose,
                    currencyId = Gold.Id,
                    receivingAuthorityId = authorityId
                }, Tx(Context, "revenue-account-assignment-" + purpose));
            }

            private InstitutionalRevenueAuthorityData Authority(string authorityId, InstitutionalRevenueAuthorityCategory category, bool assess, bool collect, bool refund, bool waive, bool allocate, bool audit)
            {
                return new InstitutionalRevenueAuthorityData
                {
                    authorityId = authorityId,
                    institutionId = InstitutionId,
                    institutionKind = InstitutionKind.Organization,
                    authorityCategory = category,
                    sourceReferenceId = Scoped(Context, "revenue-authority-source", authorityId),
                    permittedRevenueCategories = new[]
                    {
                        InstitutionalRevenueCategory.SalesTaxFoundation,
                        InstitutionalRevenueCategory.PayrollTax,
                        InstitutionalRevenueCategory.ImportTariff,
                        InstitutionalRevenueCategory.Toll,
                        InstitutionalRevenueCategory.LicenseFee,
                        InstitutionalRevenueCategory.Fine
                    },
                    permittedSubjectKinds = new[] { RevenueSubjectKind.Person, RevenueSubjectKind.Organization },
                    permittedCurrencyIds = new[] { Gold.Id },
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

            private static InstitutionalRevenueDefinition Definition(string id, string name, CurrencyDefinition gold, InstitutionalRevenueCategory category, TaxableEventCategory eventCategory, TaxBaseKind baseKind, RevenueRatePolicyData rate, RevenueAccountPurpose purpose, bool withholding)
            {
                InstitutionalRevenueDefinition definition = ScriptableObject.CreateInstance<InstitutionalRevenueDefinition>();
                definition.Initialize(id, name, category, InstitutionKind.Organization, InstitutionalRevenueAuthorityCategory.Assess, gold, baseKind, rate, AssessmentPeriodKind.PerEvent, new[] { RevenueSubjectKind.Person }, new[] { eventCategory }, withholding, purpose, requiresFiling: true);
                return definition;
            }
        }

        private static void SetPrivate(object target, string fieldName, object value)
        {
            target?.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(target, value);
        }

        private static string RunGuid(TestLabAutomationContext context, string slug)
        {
            string seed = $"{context?.RunId}.{context?.CurrentSuiteId}.{context?.CurrentScenarioId}.{slug}";
            using MD5 md5 = MD5.Create();
            byte[] bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(seed ?? string.Empty));
            return new Guid(bytes).ToString("D");
        }
    }
}
#endif
