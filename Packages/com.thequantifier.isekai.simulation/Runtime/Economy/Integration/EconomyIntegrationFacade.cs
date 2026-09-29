using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Contracts;
using UnityIsekaiGame.Economy.Businesses;
using UnityIsekaiGame.Economy.InstitutionalRevenue;
using UnityIsekaiGame.Economy.Markets;
using UnityIsekaiGame.Economy.Payroll;
using UnityIsekaiGame.Economy.Properties;
using UnityIsekaiGame.Economy.RegionalFlow;
using UnityIsekaiGame.Economy.Trading;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.Progression;

namespace UnityIsekaiGame.Economy.Integration
{
    public sealed class EconomyIntegrationFacade
    {
        private readonly DefinitionRegistry registry;
        private readonly EconomyRuntime economy;
        private readonly MarketRuntime markets;
        private readonly TradeRuntime trades;
        private readonly PayrollRuntime payroll;
        private readonly BusinessRuntime businesses;
        private readonly PropertyRuntime properties;
        private readonly ContractEconomyRuntime contracts;
        private readonly InstitutionalRevenueRuntime revenue;
        private readonly RegionalFlowRuntime regionalFlow;
        private readonly InformationAccessRuntime access;
        private readonly string worldId;

        public EconomyIntegrationFacade(
            DefinitionRegistry registry,
            EconomyRuntime economy = null,
            MarketRuntime markets = null,
            TradeRuntime trades = null,
            PayrollRuntime payroll = null,
            BusinessRuntime businesses = null,
            PropertyRuntime properties = null,
            ContractEconomyRuntime contracts = null,
            InstitutionalRevenueRuntime revenue = null,
            RegionalFlowRuntime regionalFlow = null,
            InformationAccessRuntime access = null,
            string worldId = "")
        {
            this.registry = registry;
            this.economy = economy;
            this.markets = markets;
            this.trades = trades;
            this.payroll = payroll;
            this.businesses = businesses;
            this.properties = properties;
            this.contracts = contracts;
            this.revenue = revenue;
            this.regionalFlow = regionalFlow;
            this.access = access;
            this.worldId = string.IsNullOrWhiteSpace(worldId) ? "local-world" : worldId.Trim();
        }

        public static IReadOnlyList<EconomicAuthorityMapEntryData> CreateAuthorityMap()
        {
            return new[]
            {
                Entry(EconomicDomainAuthorityId.CurrencyTransactions, "11.1", "Currency and Economic Transactions", nameof(EconomyRuntime),
                    "currency definitions", "exact monetary amounts", "accounts and wallets", "monetary reservations", "payments", "transfers", "ledger entries", "fixed price snapshots"),
                Entry(EconomicDomainAuthorityId.Markets, "11.2", "Markets and Price Formation", nameof(MarketRuntime),
                    "market definitions", "market instances", "market subjects", "supply observations", "demand observations", "scarcity", "reference prices", "regional price history", "merchant quotes"),
                Entry(EconomicDomainAuthorityId.Trade, "11.3", "Trade and Negotiation", nameof(TradeRuntime),
                    "trade sessions", "trade participants", "offers", "counteroffers", "accepted deals", "trade receipts", "item and money trade coordination"),
                Entry(EconomicDomainAuthorityId.Payroll, "11.4", "Wages, Employment, and Payroll", nameof(PayrollRuntime),
                    "compensation agreements", "timesheets", "pay periods", "gross-to-net calculations", "payroll obligations", "pay statements", "wage debt"),
                Entry(EconomicDomainAuthorityId.Businesses, "11.5", "Businesses and Production Ownership", nameof(BusinessRuntime),
                    "business instances", "ownership", "control", "establishments", "business account references", "business inventory references", "production ownership", "profit and loss", "cash flow"),
                Entry(EconomicDomainAuthorityId.Property, "11.6", "Property, Land, and Buildings", nameof(PropertyRuntime),
                    "property instances", "property hierarchy", "ownership interests", "title", "possession", "occupancy", "tenancy", "property access rights", "rent obligations", "condition", "maintenance"),
                Entry(EconomicDomainAuthorityId.Contracts, "11.7", "Contracts, Loans, and Obligations", nameof(ContractEconomyRuntime),
                    "contract lifecycle", "contract parties", "contract terms", "obligations", "performance records", "credit", "loans", "repayment schedules", "collateral", "default state"),
                Entry(EconomicDomainAuthorityId.InstitutionalRevenue, "11.8", "Taxes, Fees, and Institutional Revenue", nameof(InstitutionalRevenueRuntime),
                    "revenue definitions", "assessment authority", "taxable events", "assessments", "withholding", "remittance", "fees", "fines", "revenue allocation", "refunds", "filings", "audits"),
                Entry(EconomicDomainAuthorityId.RegionalFlow, "11.9", "Economic Simulation and Regional Flow", nameof(RegionalFlowRuntime),
                    "economic regions", "commodity pools", "cohorts", "aggregate production", "aggregate consumption", "labor indicators", "wealth summaries", "shortages", "surpluses", "abstract trade connections", "flow orders", "bounded cycles"),
                Entry(EconomicDomainAuthorityId.ExternalPersonsOrganizations, "external", "Persons, Organizations, Roles, and Titles", "External identity/profession systems",
                    "persons", "organizations", "roles", "titles", "positions"),
                Entry(EconomicDomainAuthorityId.ExternalItemsInventory, "external", "Items, Inventory, Crafting, and Production", "Item and crafting systems",
                    "exact item instances", "inventories", "custody", "ownership", "crafting jobs", "production jobs"),
                Entry(EconomicDomainAuthorityId.ExternalProfessions, "external", "Professions, Skills, Credentials, and Employment", "Profession systems",
                    "professions", "employment relationships", "skills", "knowledge", "credentials"),
                Entry(EconomicDomainAuthorityId.ExternalKnowledgeHistoryAccess, "external", "Knowledge, History, Records, and Access", "Knowledge and access systems",
                    "facts", "beliefs", "memories", "history", "records", "redaction", "access decisions"),
                Entry(EconomicDomainAuthorityId.ExternalWorldTimeLocations, "external", "World Time and Locations", "World systems",
                    "world time", "locations", "future laws", "future reputation", "future relationships")
            }.Select(item => item.Clone()).ToArray();
        }

        public EconomicReadinessSnapshot EvaluateReadiness(bool sceneHostAvailable = false, bool sceneHostRequired = false)
        {
            List<EconomicIntegrationDiagnosticData> diagnostics = new List<EconomicIntegrationDiagnosticData>();
            if (registry == null)
            {
                diagnostics.Add(Diagnostic(EconomicIntegrationDiagnosticSeverity.Error, EconomicIntegrationDiagnosticCode.MissingDefinitionRegistry, "definitions", "DefinitionRegistry", "Economy integration requires a definition registry.", "Pass the catalog-backed registry used by the authoritative runtimes."));
            }

            if (sceneHostRequired && !sceneHostAvailable)
            {
                diagnostics.Add(Diagnostic(EconomicIntegrationDiagnosticSeverity.Error, EconomicIntegrationDiagnosticCode.SceneHostUnavailable, "scene-host", "TestLabSceneHost", "A scene host was required but not available.", "Run from a compatible scene host or choose logic-only integration validation."));
            }

            EconomicRuntimeSummaryData[] summaries = CreateRuntimeSummaries();
            foreach (EconomicRuntimeSummaryData summary in summaries.Where(item => !item.present))
            {
                diagnostics.Add(Diagnostic(EconomicIntegrationDiagnosticSeverity.Error, EconomicIntegrationDiagnosticCode.MissingRuntime, $"runtime/{summary.runtimeName}", summary.runtimeName, $"{summary.runtimeName} is missing.", "Provide the authoritative Step 11 runtime instead of substituting copied state."));
            }

            bool ready = diagnostics.All(item => item.severity != EconomicIntegrationDiagnosticSeverity.Error);
            return new EconomicReadinessSnapshot(ready, registry != null, sceneHostAvailable, sceneHostRequired, summaries, diagnostics);
        }

        public EconomicValidationResult ValidateAuthorityMap(IEnumerable<EconomicAuthorityMapEntryData> entries = null)
        {
            EconomicAuthorityMapEntryData[] map = (entries ?? CreateAuthorityMap()).Select(item => item?.Clone()).Where(item => item != null).ToArray();
            List<EconomicIntegrationDiagnosticData> diagnostics = new List<EconomicIntegrationDiagnosticData>();

            foreach (IGrouping<EconomicDomainAuthorityId, EconomicAuthorityMapEntryData> group in map.GroupBy(item => item.domainId).Where(group => group.Count() > 1))
            {
                diagnostics.Add(Diagnostic(EconomicIntegrationDiagnosticSeverity.Error, EconomicIntegrationDiagnosticCode.DuplicateAuthority, $"authority/{group.Key}", "EconomyIntegrationFacade", $"Economic domain '{group.Key}' has {group.Count()} authority entries.", "Keep exactly one owner for each economic domain."));
            }

            foreach (EconomicDomainAuthorityId domain in Enum.GetValues(typeof(EconomicDomainAuthorityId)).Cast<EconomicDomainAuthorityId>())
            {
                if (!map.Any(item => item.domainId == domain))
                {
                    diagnostics.Add(Diagnostic(EconomicIntegrationDiagnosticSeverity.Error, EconomicIntegrationDiagnosticCode.MissingAuthority, $"authority/{domain}", "EconomyIntegrationFacade", $"Economic domain '{domain}' has no authority entry.", "Add the missing authority-map entry."));
                }
            }

            return new EconomicValidationResult(diagnostics.All(item => item.severity != EconomicIntegrationDiagnosticSeverity.Error), diagnostics);
        }

        public EconomicValidationResult ValidateDefinitionSet()
        {
            List<EconomicIntegrationDiagnosticData> diagnostics = new List<EconomicIntegrationDiagnosticData>();
            if (registry == null)
            {
                diagnostics.Add(Diagnostic(EconomicIntegrationDiagnosticSeverity.Error, EconomicIntegrationDiagnosticCode.MissingDefinitionRegistry, "definitions", "DefinitionRegistry", "Definition registry is missing.", "Use the catalog-backed registry."));
                return new EconomicValidationResult(false, diagnostics);
            }

            AddMissingDefinitionDiagnostic<CurrencyDefinition>(diagnostics, "11.1 currency definitions");
            AddMissingDefinitionDiagnostic<MarketDefinition>(diagnostics, "11.2 market definitions");
            AddMissingDefinitionDiagnostic<MarketSubjectDefinition>(diagnostics, "11.2 market subject definitions");
            AddMissingDefinitionDiagnostic<TradePolicyDefinition>(diagnostics, "11.3 trade policy definitions");
            AddMissingDefinitionDiagnostic<CompensationDefinition>(diagnostics, "11.4 compensation definitions");
            AddMissingDefinitionDiagnostic<BusinessDefinition>(diagnostics, "11.5 business definitions");
            AddMissingDefinitionDiagnostic<PropertyDefinition>(diagnostics, "11.6 property definitions");
            AddMissingDefinitionDiagnostic<ContractFinanceDefinition>(diagnostics, "11.7 contract finance definitions");
            AddMissingDefinitionDiagnostic<InstitutionalRevenueDefinition>(diagnostics, "11.8 institutional revenue definitions");
            AddMissingDefinitionDiagnostic<EconomicRegionDefinition>(diagnostics, "11.9 economic region definitions");
            AddMissingDefinitionDiagnostic<CommodityDefinition>(diagnostics, "11.9 commodity definitions");

            return new EconomicValidationResult(diagnostics.All(item => item.severity != EconomicIntegrationDiagnosticSeverity.Error), diagnostics);
        }

        public EconomicValidationResult ValidateRequiredDefinitions(IEnumerable<string> requiredDefinitionIds)
        {
            List<EconomicIntegrationDiagnosticData> diagnostics = new List<EconomicIntegrationDiagnosticData>();
            foreach (string id in (requiredDefinitionIds ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal))
            {
                if (registry == null || !registry.DefinitionsById.ContainsKey(id))
                {
                    diagnostics.Add(Diagnostic(EconomicIntegrationDiagnosticSeverity.Error, EconomicIntegrationDiagnosticCode.MissingDefinition,
                        $"definitions/{id}", nameof(DefinitionRegistry), $"Required economic definition '{id}' is missing.",
                        "Author the definition in the active catalog; production bootstraps must not silently omit economic systems."));
                }
            }

            return new EconomicValidationResult(diagnostics.Count == 0, diagnostics);
        }

        public EconomicValidationResult ValidateEconomicGraph()
        {
            List<EconomicIntegrationDiagnosticData> diagnostics = new List<EconomicIntegrationDiagnosticData>();
            ValidateRuntimeSaveData(diagnostics, nameof(EconomyRuntime), () => economy == null ? null : EconomyRuntime.ValidateSaveData(economy.CreateSaveData(), registry, out string failure) ? string.Empty : failure);
            ValidateRuntimeSaveData(diagnostics, nameof(MarketRuntime), () => markets == null ? null : MarketRuntime.ValidateSaveData(markets.CreateSaveData(), registry, out string failure) ? string.Empty : failure);
            ValidateRuntimeSaveData(diagnostics, nameof(TradeRuntime), () => trades == null ? null : TradeRuntime.ValidateSaveData(trades.CreateSaveData(), registry, out string failure) ? string.Empty : failure);
            ValidateRuntimeSaveData(diagnostics, nameof(PayrollRuntime), () => payroll == null ? null : PayrollRuntime.ValidateSaveData(payroll.CreateSaveData(), registry, out string failure) ? string.Empty : failure);
            ValidateRuntimeSaveData(diagnostics, nameof(BusinessRuntime), () => businesses == null ? null : BusinessRuntime.ValidateSaveData(businesses.CreateSaveData(), registry, out string failure) ? string.Empty : failure);
            ValidateRuntimeSaveData(diagnostics, nameof(PropertyRuntime), () => properties == null ? null : PropertyRuntime.ValidateSaveData(properties.CreateSaveData(), registry, out string failure) ? string.Empty : failure);
            ValidateRuntimeSaveData(diagnostics, nameof(ContractEconomyRuntime), () => contracts == null ? null : ContractEconomyRuntime.ValidateSaveData(contracts.CreateSaveData(), registry, out string failure) ? string.Empty : failure);
            ValidateRuntimeSaveData(diagnostics, nameof(InstitutionalRevenueRuntime), () => revenue == null ? null : InstitutionalRevenueRuntime.ValidateSaveData(revenue.CreateSaveData(), registry, out string failure) ? string.Empty : failure);
            ValidateRuntimeSaveData(diagnostics, nameof(RegionalFlowRuntime), () => regionalFlow == null ? null : RegionalFlowRuntime.ValidateSaveData(regionalFlow.CreateSaveData(), registry, out string failure) ? string.Empty : failure);

            return new EconomicValidationResult(diagnostics.All(item => item.severity != EconomicIntegrationDiagnosticSeverity.Error), diagnostics);
        }

        public IReadOnlyList<EconomicBoundaryInvariantResult> AuditBoundaryInvariants()
        {
            return new[]
            {
                Invariant(EconomicBoundaryInvariantId.MoneyMutatesOnlyThroughEconomyRuntime, nameof(EconomyRuntime), string.Empty, economy != null, "Money balances, reservations, and ledger entries remain EconomyRuntime-owned."),
                Invariant(EconomicBoundaryInvariantId.ItemsMutateOnlyThroughItemIdentityRuntime, "ItemInstanceIdentityRuntime", nameof(TradeRuntime), true, "Step 11 stores item references and expected item revisions, not duplicate item state."),
                Invariant(EconomicBoundaryInvariantId.MarketsReadMoneyAndItemsButOwnOnlyPrices, nameof(MarketRuntime), nameof(EconomyRuntime), markets != null, "Markets consume observations and own price history/quotes only."),
                Invariant(EconomicBoundaryInvariantId.TradeCoordinatesMoneyAndItemsAtomically, nameof(TradeRuntime), nameof(EconomyRuntime), trades != null, "Trade prepares cross-runtime movement through authoritative runtimes and rollback snapshots."),
                Invariant(EconomicBoundaryInvariantId.PayrollUsesEconomyRuntimeForPayment, nameof(PayrollRuntime), nameof(EconomyRuntime), payroll != null, "Payroll owns compensation state and settles payment through EconomyRuntime."),
                Invariant(EconomicBoundaryInvariantId.BusinessesReferenceAccountsInventoriesAndProduction, nameof(BusinessRuntime), "EconomyRuntime/ItemInstanceIdentityRuntime/ProductionWorkflowRuntime", businesses != null, "Business account, inventory, and production fields are references."),
                Invariant(EconomicBoundaryInvariantId.PropertyOwnsTitleNotAccountsOrItems, nameof(PropertyRuntime), nameof(EconomyRuntime), properties != null, "Property owns title and occupancy, not money or item custody."),
                Invariant(EconomicBoundaryInvariantId.ContractsUseEconomyRuntimeForSettlement, nameof(ContractEconomyRuntime), nameof(EconomyRuntime), contracts != null, "Contracts own obligations and loans, with settlement delegated to EconomyRuntime."),
                Invariant(EconomicBoundaryInvariantId.RevenueUsesEconomyRuntimeForCollection, nameof(InstitutionalRevenueRuntime), nameof(EconomyRuntime), revenue != null, "Revenue owns assessments and obligations, with collection delegated to EconomyRuntime."),
                Invariant(EconomicBoundaryInvariantId.RegionalFlowOwnsAggregatePoolsOnly, nameof(RegionalFlowRuntime), "MarketRuntime/ItemInstanceIdentityRuntime", regionalFlow != null, "Regional flow owns aggregate commodity pools and never exact item instances."),
                Invariant(EconomicBoundaryInvariantId.AccessRuntimeOwnsRedaction, nameof(InformationAccessRuntime), "Step 11 projections", access != null, "Access runtime remains the redaction authority for economic projections.")
            }.Select(item => item.Clone()).ToArray();
        }

        public EconomicConservationAuditResult AuditExactArithmeticAndConservation()
        {
            Dictionary<string, EconomyTransactionKind> transactionKinds = economy?.Transactions
                .ToDictionary(transaction => transaction.TransactionId, transaction => transaction.Kind, StringComparer.Ordinal)
                ?? new Dictionary<string, EconomyTransactionKind>(StringComparer.Ordinal);
            long ledgerNet = economy?.LedgerEntries
                .Where(entry => transactionKinds.TryGetValue(entry.transactionId, out EconomyTransactionKind kind)
                    && kind != EconomyTransactionKind.Issuance
                    && kind != EconomyTransactionKind.Destruction
                    && kind != EconomyTransactionKind.PhysicalToAbstract
                    && kind != EconomyTransactionKind.AbstractToPhysical)
                .Sum(entry => entry.kind == EconomyLedgerEntryKind.Debit ? -entry.units : entry.units) ?? 0L;
            long regionalUnits = regionalFlow?.Pools.Sum(pool => pool.totalQuantity + pool.inboundQuantity - pool.outboundQuantity - pool.consumedQuantity - pool.lostQuantity) ?? 0L;
            int checkedRuntimeCount = CreateRuntimeSummaries().Count(item => item.present);
            return new EconomicConservationAuditResult
            {
                auditId = "step11.conservation.exact-arithmetic",
                succeeded = checkedRuntimeCount > 0 && ledgerNet == 0L,
                monetaryLedgerNet = ledgerNet,
                regionalKnownPoolUnits = regionalUnits,
                checkedRuntimeCount = checkedRuntimeCount,
                message = ledgerNet == 0L ? "Checked exact integer ledger and aggregate regional quantities without floating-point ownership." : "Ledger entries do not sum to zero for the current snapshot."
            };
        }

        public EconomicPersistenceDependencyMapResult BuildPersistenceDependencyMap()
        {
            EconomicPersistenceDependencyData[] participants =
            {
                Participant(EconomyPersistenceParticipant.Key, Array.Empty<string>(), ItemInstanceIdentityPersistenceParticipant.Key),
                Participant(MarketPersistenceParticipant.Key, Array.Empty<string>(), EconomyPersistenceParticipant.Key, ItemInstanceIdentityPersistenceParticipant.Key),
                Participant(TradePersistenceParticipant.Key, Array.Empty<string>(), EconomyPersistenceParticipant.Key, MarketPersistenceParticipant.Key, ItemInstanceIdentityPersistenceParticipant.Key),
                Participant(PayrollPersistenceParticipant.Key, Array.Empty<string>(), EconomyPersistenceParticipant.Key, PositionEmploymentPersistenceParticipant.Key),
                Participant(BusinessPersistenceParticipant.Key, Array.Empty<string>(), EconomyPersistenceParticipant.Key, MarketPersistenceParticipant.Key, TradePersistenceParticipant.Key, PayrollPersistenceParticipant.Key, ItemInstanceIdentityPersistenceParticipant.Key, ProductionWorkflowPersistenceParticipant.Key, PositionEmploymentPersistenceParticipant.Key),
                Participant(PropertyPersistenceParticipant.Key, Array.Empty<string>(), EconomyPersistenceParticipant.Key, BusinessPersistenceParticipant.Key, ContractEconomyPersistenceParticipant.Key, ItemInstanceIdentityPersistenceParticipant.Key),
                Participant(ContractEconomyPersistenceParticipant.Key, Array.Empty<string>(), EconomyPersistenceParticipant.Key, PropertyPersistenceParticipant.Key, InformationAccessPersistenceParticipant.Key),
                Participant(InstitutionalRevenuePersistenceParticipant.Key, Array.Empty<string>(), EconomyPersistenceParticipant.Key, PayrollPersistenceParticipant.Key, BusinessPersistenceParticipant.Key, PropertyPersistenceParticipant.Key, ContractEconomyPersistenceParticipant.Key, InformationAccessPersistenceParticipant.Key),
                Participant(RegionalFlowPersistenceParticipant.Key, Array.Empty<string>(), EconomyPersistenceParticipant.Key, MarketPersistenceParticipant.Key, TradePersistenceParticipant.Key, PayrollPersistenceParticipant.Key, BusinessPersistenceParticipant.Key, PropertyPersistenceParticipant.Key, ContractEconomyPersistenceParticipant.Key, InstitutionalRevenuePersistenceParticipant.Key, ItemInstanceIdentityPersistenceParticipant.Key, InformationAccessPersistenceParticipant.Key)
            };

            List<EconomicIntegrationDiagnosticData> diagnostics = DetectDependencyCycles(participants);
            return new EconomicPersistenceDependencyMapResult(diagnostics.Count == 0, participants, diagnostics);
        }

        public EconomicValidationResult AuditAccessAndRedaction()
        {
            List<EconomicIntegrationDiagnosticData> diagnostics = new List<EconomicIntegrationDiagnosticData>();
            if (access == null)
            {
                diagnostics.Add(Diagnostic(EconomicIntegrationDiagnosticSeverity.Error, EconomicIntegrationDiagnosticCode.AccessProjectionUnavailable, "access", nameof(InformationAccessRuntime), "InformationAccessRuntime is missing.", "Use the Step 8 access runtime for economic projections and redaction."));
            }

            return new EconomicValidationResult(diagnostics.Count == 0, diagnostics);
        }

        public EconomicSignalContractData CreateStep12SignalContract(string signalId, EconomicSignalCategory category, string subjectId, long exactValue, double worldTime = 0d)
        {
            long[] revisions = CreateRuntimeSummaries().Where(item => item.present).Select(item => item.revision).ToArray();
            return new EconomicSignalContractData
            {
                signalId = string.IsNullOrWhiteSpace(signalId) ? $"economy.signal.{category.ToString().ToLowerInvariant()}" : signalId.Trim(),
                category = category,
                sourceRuntime = "EconomyIntegrationFacade",
                subjectId = subjectId ?? string.Empty,
                valueKind = "ExactInteger",
                exactValue = exactValue,
                worldTime = Math.Max(0d, worldTime),
                dependencyRevisions = revisions,
                mutationFree = true,
                step12Ready = !string.IsNullOrWhiteSpace(subjectId)
            };
        }

        public EconomicIntegrationSnapshot CreateSnapshot(params EconomicSignalContractData[] signals)
        {
            EconomicRuntimeSummaryData[] summaries = CreateRuntimeSummaries();
            string fingerprint = string.Join("|", summaries.Select(item => $"{item.runtimeName}:{item.present}:{item.revision}:{item.primaryRecordCount}:{item.secondaryRecordCount}:{item.tertiaryRecordCount}:{item.fingerprint}"));
            return new EconomicIntegrationSnapshot(summaries, signals ?? Array.Empty<EconomicSignalContractData>(), fingerprint);
        }

        public EconomicValidationResult ValidateAll(bool sceneHostAvailable = false, bool sceneHostRequired = false, IEnumerable<string> requiredDefinitionIds = null)
        {
            List<EconomicIntegrationDiagnosticData> diagnostics = new List<EconomicIntegrationDiagnosticData>();
            diagnostics.AddRange(EvaluateReadiness(sceneHostAvailable, sceneHostRequired).Diagnostics);
            diagnostics.AddRange(ValidateAuthorityMap().Diagnostics);
            diagnostics.AddRange(ValidateDefinitionSet().Diagnostics.Where(item => item.severity == EconomicIntegrationDiagnosticSeverity.Error));
            diagnostics.AddRange(ValidateRequiredDefinitions(requiredDefinitionIds).Diagnostics);
            diagnostics.AddRange(ValidateEconomicGraph().Diagnostics);
            diagnostics.AddRange(BuildPersistenceDependencyMap().Diagnostics);
            diagnostics.AddRange(AuditAccessAndRedaction().Diagnostics);
            return new EconomicValidationResult(diagnostics.All(item => item.severity != EconomicIntegrationDiagnosticSeverity.Error), diagnostics);
        }

        private EconomicRuntimeSummaryData[] CreateRuntimeSummaries()
        {
            return new[]
            {
                Summary(nameof(EconomyRuntime), EconomyPersistenceParticipant.Key, economy != null, economy?.Revision ?? 0L, economy?.AccountCount ?? 0, economy?.TransactionCount ?? 0, economy?.LedgerEntryCount ?? 0),
                Summary(nameof(MarketRuntime), MarketPersistenceParticipant.Key, markets != null, markets?.Revision ?? 0L, markets?.MarketCount ?? 0, markets?.PriceCount ?? 0, markets?.QuoteCount ?? 0),
                Summary(nameof(TradeRuntime), TradePersistenceParticipant.Key, trades != null, trades?.Revision ?? 0L, trades?.SessionCount ?? 0, trades?.OfferCount ?? 0, trades?.TradeRecordCount ?? 0),
                Summary(nameof(PayrollRuntime), PayrollPersistenceParticipant.Key, payroll != null, payroll?.Revision ?? 0L, payroll?.AgreementCount ?? 0, payroll?.ObligationCount ?? 0, payroll?.StatementCount ?? 0),
                Summary(nameof(BusinessRuntime), BusinessPersistenceParticipant.Key, businesses != null, businesses?.Revision ?? 0L, businesses?.BusinessCount ?? 0, businesses?.OwnershipCount ?? 0, businesses?.StatementCount ?? 0),
                Summary(nameof(PropertyRuntime), PropertyPersistenceParticipant.Key, properties != null, properties?.Revision ?? 0L, properties?.PropertyCount ?? 0, properties?.OwnershipCount ?? 0, properties?.TenancyCount ?? 0),
                Summary(nameof(ContractEconomyRuntime), ContractEconomyPersistenceParticipant.Key, contracts != null, contracts?.Revision ?? 0L, contracts?.ContractCount ?? 0, contracts?.ObligationCount ?? 0, contracts?.LoanCount ?? 0),
                Summary(nameof(InstitutionalRevenueRuntime), InstitutionalRevenuePersistenceParticipant.Key, revenue != null, revenue?.Revision ?? 0L, revenue?.AssessmentCount ?? 0, revenue?.ObligationCount ?? 0, revenue?.RevenueRecordCount ?? 0),
                Summary(nameof(RegionalFlowRuntime), RegionalFlowPersistenceParticipant.Key, regionalFlow != null, regionalFlow?.Revision ?? 0L, regionalFlow?.RegionCount ?? 0, regionalFlow?.PoolCount ?? 0, regionalFlow?.FlowCount ?? 0)
            };
        }

        private void AddMissingDefinitionDiagnostic<TDefinition>(List<EconomicIntegrationDiagnosticData> diagnostics, string label)
            where TDefinition : class, IGameDefinition
        {
            if (registry.DefinitionsById.Values.OfType<TDefinition>().Any())
            {
                return;
            }

            diagnostics.Add(Diagnostic(EconomicIntegrationDiagnosticSeverity.Warning, EconomicIntegrationDiagnosticCode.None, $"definitions/{typeof(TDefinition).Name}", typeof(TDefinition).Name, $"No {label} were found in the current registry.", "This is acceptable for a minimal test fixture, but authored catalog validation should cover production definitions."));
        }

        private static void ValidateRuntimeSaveData(List<EconomicIntegrationDiagnosticData> diagnostics, string runtimeName, Func<string> validate)
        {
            string failure = validate();
            if (failure == null)
            {
                diagnostics.Add(Diagnostic(EconomicIntegrationDiagnosticSeverity.Error, EconomicIntegrationDiagnosticCode.MissingRuntime, $"graph/{runtimeName}", runtimeName, $"{runtimeName} is missing.", "Provide the authoritative runtime before integrated graph validation."));
                return;
            }

            if (!string.IsNullOrEmpty(failure))
            {
                diagnostics.Add(Diagnostic(EconomicIntegrationDiagnosticSeverity.Error, EconomicIntegrationDiagnosticCode.InvalidSaveGraph, $"graph/{runtimeName}", runtimeName, failure, "Repair the owning runtime save graph instead of bypassing validation."));
            }
        }

        private static List<EconomicIntegrationDiagnosticData> DetectDependencyCycles(IReadOnlyList<EconomicPersistenceDependencyData> participants)
        {
            Dictionary<string, EconomicPersistenceDependencyData> byKey = participants.ToDictionary(item => item.participantKey, StringComparer.Ordinal);
            List<EconomicIntegrationDiagnosticData> diagnostics = new List<EconomicIntegrationDiagnosticData>();
            foreach (EconomicPersistenceDependencyData participant in participants)
            {
                HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
                if (HasCycle(participant.participantKey, participant.participantKey, byKey, visited))
                {
                    diagnostics.Add(Diagnostic(EconomicIntegrationDiagnosticSeverity.Error, EconomicIntegrationDiagnosticCode.PersistenceDependencyCycle, $"persistence/{participant.participantKey}", participant.participantKey, $"Persistence dependency cycle detected from '{participant.participantKey}'.", "Break the dependency cycle by keeping Step 11 dependencies optional or earlier-phase only."));
                }
            }

            return diagnostics;
        }

        private static bool HasCycle(string root, string current, IReadOnlyDictionary<string, EconomicPersistenceDependencyData> byKey, HashSet<string> visited)
        {
            if (!visited.Add(current) || !byKey.TryGetValue(current, out EconomicPersistenceDependencyData participant))
            {
                return false;
            }

            foreach (string dependency in participant.requiredDependencies)
            {
                if (string.Equals(dependency, root, StringComparison.Ordinal) || HasCycle(root, dependency, byKey, visited))
                {
                    return true;
                }
            }

            return false;
        }

        private static EconomicAuthorityMapEntryData Entry(EconomicDomainAuthorityId id, string feature, string displayName, string runtime, params string[] owns)
        {
            return new EconomicAuthorityMapEntryData
            {
                domainId = id,
                featureId = feature ?? string.Empty,
                displayName = displayName ?? string.Empty,
                authoritativeRuntime = runtime ?? string.Empty,
                owns = owns ?? Array.Empty<string>(),
                externalAuthorities = Array.Empty<string>()
            };
        }

        private static EconomicRuntimeSummaryData Summary(string name, string key, bool present, long revision, int primary, int secondary, int tertiary)
        {
            return new EconomicRuntimeSummaryData
            {
                runtimeName = name ?? string.Empty,
                persistenceKey = key ?? string.Empty,
                present = present,
                revision = Math.Max(0L, revision),
                primaryRecordCount = Math.Max(0, primary),
                secondaryRecordCount = Math.Max(0, secondary),
                tertiaryRecordCount = Math.Max(0, tertiary),
                fingerprint = $"{Math.Max(0L, revision)}:{Math.Max(0, primary)}:{Math.Max(0, secondary)}:{Math.Max(0, tertiary)}"
            };
        }

        private static EconomicBoundaryInvariantResult Invariant(EconomicBoundaryInvariantId id, string owner, string dependency, bool satisfied, string message)
        {
            return new EconomicBoundaryInvariantResult
            {
                invariantId = id,
                owningRuntime = owner ?? string.Empty,
                dependentRuntime = dependency ?? string.Empty,
                satisfied = satisfied,
                message = message ?? string.Empty
            };
        }

        private static EconomicPersistenceDependencyData Participant(string key, IEnumerable<string> required, params string[] optional)
        {
            return new EconomicPersistenceDependencyData
            {
                participantKey = key ?? string.Empty,
                requiredDependencies = (required ?? Array.Empty<string>()).ToArray(),
                optionalDependencies = optional ?? Array.Empty<string>()
            };
        }

        private static EconomicIntegrationDiagnosticData Diagnostic(EconomicIntegrationDiagnosticSeverity severity, EconomicIntegrationDiagnosticCode code, string path, string runtime, string message, string action)
        {
            return new EconomicIntegrationDiagnosticData
            {
                severity = severity,
                code = code,
                path = path ?? string.Empty,
                owningRuntime = runtime ?? string.Empty,
                message = message ?? string.Empty,
                correctiveAction = action ?? string.Empty
            };
        }
    }
}
