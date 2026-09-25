using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Economy.Payroll;
using UnityIsekaiGame.Economy.Trading;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Inventory.Production;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Professions;
using UnityIsekaiGame.Progression;

namespace UnityIsekaiGame.Economy.Businesses
{
    public sealed class BusinessRuntime
    {
        public const int CurrentSaveSchemaVersion = 1;

        private readonly Dictionary<string, BusinessInstanceData> businessesById = new Dictionary<string, BusinessInstanceData>(StringComparer.Ordinal);
        private readonly Dictionary<string, BusinessOwnershipRecordData> ownershipById = new Dictionary<string, BusinessOwnershipRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, BusinessControlRecordData> controlsById = new Dictionary<string, BusinessControlRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, BusinessEstablishmentData> establishmentsById = new Dictionary<string, BusinessEstablishmentData>(StringComparer.Ordinal);
        private readonly Dictionary<string, BusinessAccountAssignmentData> accountAssignmentsById = new Dictionary<string, BusinessAccountAssignmentData>(StringComparer.Ordinal);
        private readonly Dictionary<string, BusinessInventoryAssignmentData> inventoryAssignmentsById = new Dictionary<string, BusinessInventoryAssignmentData>(StringComparer.Ordinal);
        private readonly Dictionary<string, BusinessStockClassificationData> stockClassificationsById = new Dictionary<string, BusinessStockClassificationData>(StringComparer.Ordinal);
        private readonly Dictionary<string, BusinessProductionOwnershipData> productionOwnershipById = new Dictionary<string, BusinessProductionOwnershipData>(StringComparer.Ordinal);
        private readonly Dictionary<string, BusinessFundingAllocationData> fundingAllocationsById = new Dictionary<string, BusinessFundingAllocationData>(StringComparer.Ordinal);
        private readonly Dictionary<string, BusinessRevenueRecordData> revenueById = new Dictionary<string, BusinessRevenueRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, BusinessExpenseRecordData> expensesById = new Dictionary<string, BusinessExpenseRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, BusinessCapitalContributionData> capitalById = new Dictionary<string, BusinessCapitalContributionData>(StringComparer.Ordinal);
        private readonly Dictionary<string, BusinessOwnerWithdrawalData> withdrawalsById = new Dictionary<string, BusinessOwnerWithdrawalData>(StringComparer.Ordinal);
        private readonly Dictionary<string, BusinessAccountingPeriodData> periodsById = new Dictionary<string, BusinessAccountingPeriodData>(StringComparer.Ordinal);
        private readonly Dictionary<string, BusinessProfitAndLossStatementData> statementsById = new Dictionary<string, BusinessProfitAndLossStatementData>(StringComparer.Ordinal);
        private readonly Dictionary<string, BusinessCashFlowSummaryData> cashFlowsById = new Dictionary<string, BusinessCashFlowSummaryData>(StringComparer.Ordinal);
        private DefinitionRegistry registry;
        private string worldId;

        public long Revision { get; private set; }
        public int BusinessCount => businessesById.Count;
        public int OwnershipCount => ownershipById.Count;
        public int EstablishmentCount => establishmentsById.Count;
        public int RevenueCount => revenueById.Count;
        public int ExpenseCount => expensesById.Count;
        public int StatementCount => statementsById.Count;

        public IReadOnlyList<BusinessInstanceData> Businesses => Ordered(businessesById.Values, item => item.businessId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<BusinessOwnershipRecordData> OwnershipRecords => Ordered(ownershipById.Values, item => item.ownershipRecordId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<BusinessControlRecordData> ControlRecords => Ordered(controlsById.Values, item => item.controlRecordId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<BusinessEstablishmentData> Establishments => Ordered(establishmentsById.Values, item => item.establishmentId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<BusinessAccountAssignmentData> AccountAssignments => Ordered(accountAssignmentsById.Values, item => item.assignmentId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<BusinessInventoryAssignmentData> InventoryAssignments => Ordered(inventoryAssignmentsById.Values, item => item.assignmentId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<BusinessStockClassificationData> StockClassifications => Ordered(stockClassificationsById.Values, item => item.stockClassificationId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<BusinessProductionOwnershipData> ProductionOwnershipRecords => Ordered(productionOwnershipById.Values, item => item.productionOwnershipId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<BusinessFundingAllocationData> FundingAllocations => Ordered(fundingAllocationsById.Values, item => item.allocationId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<BusinessRevenueRecordData> RevenueRecords => Ordered(revenueById.Values, item => item.revenueRecordId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<BusinessExpenseRecordData> ExpenseRecords => Ordered(expensesById.Values, item => item.expenseRecordId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<BusinessCapitalContributionData> CapitalContributions => Ordered(capitalById.Values, item => item.contributionId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<BusinessOwnerWithdrawalData> OwnerWithdrawals => Ordered(withdrawalsById.Values, item => item.withdrawalId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<BusinessAccountingPeriodData> AccountingPeriods => Ordered(periodsById.Values, item => item.accountingPeriodId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<BusinessProfitAndLossStatementData> ProfitAndLossStatements => Ordered(statementsById.Values, item => item.statementId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<BusinessCashFlowSummaryData> CashFlowSummaries => Ordered(cashFlowsById.Values, item => item.summaryId).Select(item => item.Clone()).ToArray();

        public void Configure(DefinitionRegistry definitionRegistry, string world)
        {
            registry = definitionRegistry ?? registry;
            worldId = world ?? worldId ?? string.Empty;
        }

        public bool TryGetBusiness(string businessId, out BusinessInstanceData business)
        {
            if (!string.IsNullOrWhiteSpace(businessId) && businessesById.TryGetValue(businessId, out BusinessInstanceData found))
            {
                business = found.Clone();
                return true;
            }

            business = null;
            return false;
        }

        public bool TryGetOwnership(string ownershipRecordId, out BusinessOwnershipRecordData ownership)
        {
            if (!string.IsNullOrWhiteSpace(ownershipRecordId) && ownershipById.TryGetValue(ownershipRecordId, out BusinessOwnershipRecordData found))
            {
                ownership = found.Clone();
                return true;
            }

            ownership = null;
            return false;
        }

        public bool TryGetProfitAndLossStatement(string statementId, out BusinessProfitAndLossStatementData statement)
        {
            if (!string.IsNullOrWhiteSpace(statementId) && statementsById.TryGetValue(statementId, out BusinessProfitAndLossStatementData found))
            {
                statement = found.Clone();
                return true;
            }

            statement = null;
            return false;
        }

        public BusinessOperationResult CreateBusiness(BusinessInstanceData request, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            BusinessInstanceData business = request?.Clone();
            if (business == null || string.IsNullOrWhiteSpace(business.businessId) || string.IsNullOrWhiteSpace(business.businessDefinitionId))
            {
                return Fail(BusinessOperationCode.InvalidRequest, "Business ID and definition ID are required.");
            }

            if (!TryGetBusinessDefinition(business.businessDefinitionId, out BusinessDefinition definition))
            {
                return Fail(BusinessOperationCode.MissingDefinition, $"Business definition '{business.businessDefinitionId}' was not found.");
            }

            if (businessesById.TryGetValue(business.businessId, out BusinessInstanceData existing))
            {
                return SameBusiness(existing, business)
                    ? BusinessOperationResult.Success("Business already exists.", before, before, duplicate: true).With(business: existing)
                    : Fail(BusinessOperationCode.Duplicate, $"Business '{business.businessId}' already exists with different data.");
            }

            if (business.state == BusinessState.Invalid || !Enum.IsDefined(typeof(BusinessState), business.state))
            {
                return Fail(BusinessOperationCode.InvalidState, "Business state is invalid.");
            }

            if (business.operatingCurrencyIds.Length == 0 && registry != null)
            {
                business.operatingCurrencyIds = registry.DefinitionsById.Values.OfType<CurrencyDefinition>().Select(currency => currency.Id).Take(1).ToArray();
            }

            if (business.operatingCurrencyIds.Any(currencyId => registry != null && !registry.TryGet(currencyId, out CurrencyDefinition _)))
            {
                return Fail(BusinessOperationCode.MissingExternalReference, "Business references an unknown operating currency.");
            }

            if (preview)
            {
                return BusinessOperationResult.PreviewResult("Business creation preview succeeded.", before).With(business: business);
            }

            business.displayName = string.IsNullOrWhiteSpace(business.displayName) ? definition.DisplayName : business.displayName;
            business.legalName = string.IsNullOrWhiteSpace(business.legalName) ? business.displayName : business.legalName;
            business.state = business.state == BusinessState.Planned ? BusinessState.Forming : business.state;
            business.revision = 1L;
            businessesById.Add(business.businessId, business);
            Touch();
            return BusinessOperationResult.Success("Business created.", before, Revision).With(business: business);
        }

        public BusinessOperationResult TransitionBusiness(string businessId, BusinessState targetState, double worldTime = 0d, bool preview = false)
        {
            long before = Revision;
            if (!businessesById.TryGetValue(businessId ?? string.Empty, out BusinessInstanceData business))
            {
                return Fail(BusinessOperationCode.MissingBusiness, $"Business '{businessId}' was not found.");
            }

            if (!CanTransition(business.state, targetState))
            {
                return Fail(BusinessOperationCode.InvalidState, $"Cannot transition business from {business.state} to {targetState}.");
            }

            if (preview)
            {
                BusinessInstanceData projected = business.Clone();
                ApplyBusinessState(projected, targetState, worldTime);
                return BusinessOperationResult.PreviewResult("Business lifecycle preview succeeded.", before).With(business: projected);
            }

            ApplyBusinessState(business, targetState, worldTime);
            business.revision++;
            Touch();
            return BusinessOperationResult.Success("Business lifecycle changed.", before, Revision).With(business: business);
        }

        public BusinessOperationResult AddOwnership(BusinessOwnershipRecordData request, double worldTime = 0d, bool preview = false)
        {
            long before = Revision;
            BusinessOwnershipRecordData ownership = request?.Clone();
            if (!ValidateOwnership(ownership, worldTime, out BusinessOperationCode code, out string failure))
            {
                return Fail(code, failure);
            }

            if (ownershipById.TryGetValue(ownership.ownershipRecordId, out BusinessOwnershipRecordData existing))
            {
                return SameOwnership(existing, ownership)
                    ? BusinessOperationResult.Success("Ownership record already exists.", before, before, duplicate: true).With(ownership: existing)
                    : Fail(BusinessOperationCode.Duplicate, $"Ownership record '{ownership.ownershipRecordId}' already exists with different data.");
            }

            bool overlaps = ownershipById.Values.Any(item => item.businessId == ownership.businessId
                && item.owner.StableKey == ownership.owner.StableKey
                && item.effectiveEndWorldTime < 0d
                && ownership.effectiveEndWorldTime < 0d);
            if (overlaps)
            {
                return Fail(BusinessOperationCode.PolicyViolation, "Duplicate active ownership for the same owner is not allowed.");
            }

            if (preview)
            {
                return BusinessOperationResult.PreviewResult("Ownership preview succeeded.", before).With(ownership: ownership);
            }

            ownership.revision = 1L;
            ownershipById.Add(ownership.ownershipRecordId, ownership);
            Touch();
            return BusinessOperationResult.Success("Ownership added.", before, Revision).With(ownership: ownership);
        }

        public BusinessOperationResult EndOwnership(string ownershipRecordId, double endWorldTime, bool preview = false)
        {
            long before = Revision;
            if (!ownershipById.TryGetValue(ownershipRecordId ?? string.Empty, out BusinessOwnershipRecordData ownership))
            {
                return Fail(BusinessOperationCode.InvalidRequest, $"Ownership record '{ownershipRecordId}' was not found.");
            }

            if (endWorldTime < ownership.effectiveStartWorldTime)
            {
                return Fail(BusinessOperationCode.InvalidRequest, "Ownership end cannot be before start.");
            }

            if (ownership.effectiveEndWorldTime >= 0d && Math.Abs(ownership.effectiveEndWorldTime - endWorldTime) < 0.0001d)
            {
                return BusinessOperationResult.Success("Ownership was already ended at that time.", before, before, duplicate: true).With(ownership: ownership);
            }

            if (preview)
            {
                BusinessOwnershipRecordData projected = ownership.Clone();
                projected.effectiveEndWorldTime = endWorldTime;
                return BusinessOperationResult.PreviewResult("Ownership end preview succeeded.", before).With(ownership: projected);
            }

            ownership.effectiveEndWorldTime = endWorldTime;
            ownership.revision++;
            Touch();
            return BusinessOperationResult.Success("Ownership ended.", before, Revision).With(ownership: ownership);
        }

        public BusinessOperationResult AssignController(BusinessControlRecordData request, double worldTime = 0d, bool preview = false)
        {
            long before = Revision;
            BusinessControlRecordData control = request?.Clone();
            if (control == null || string.IsNullOrWhiteSpace(control.controlRecordId) || string.IsNullOrWhiteSpace(control.businessId) || string.IsNullOrWhiteSpace(control.controllerSubjectId))
            {
                return Fail(BusinessOperationCode.InvalidRequest, "Control record ID, business ID, and controller are required.");
            }

            if (!businessesById.ContainsKey(control.businessId))
            {
                return Fail(BusinessOperationCode.MissingBusiness, $"Business '{control.businessId}' was not found.");
            }

            if (control.authorityKinds.Length == 0)
            {
                control.authorityKinds = new[] { BusinessAuthorityKind.ViewBusinessState };
            }

            if (controlsById.ContainsKey(control.controlRecordId))
            {
                return BusinessOperationResult.Success("Control record already exists.", before, before, duplicate: true).With(control: controlsById[control.controlRecordId]);
            }

            if (preview)
            {
                return BusinessOperationResult.PreviewResult("Control assignment preview succeeded.", before).With(control: control);
            }

            controlsById.Add(control.controlRecordId, control);
            if (businessesById.TryGetValue(control.businessId, out BusinessInstanceData business) && control.ActiveAt(worldTime))
            {
                business.controllerSubjectId = control.controllerSubjectId;
                business.revision++;
            }

            Touch();
            return BusinessOperationResult.Success("Controller assigned.", before, Revision).With(control: control, business: business);
        }

        public BusinessOperationResult AddEstablishment(BusinessEstablishmentData request, bool preview = false)
        {
            long before = Revision;
            BusinessEstablishmentData establishment = request?.Clone();
            if (establishment == null || string.IsNullOrWhiteSpace(establishment.establishmentId) || string.IsNullOrWhiteSpace(establishment.businessId))
            {
                return Fail(BusinessOperationCode.InvalidRequest, "Establishment ID and business ID are required.");
            }

            if (!businessesById.TryGetValue(establishment.businessId, out BusinessInstanceData business))
            {
                return Fail(BusinessOperationCode.MissingBusiness, $"Business '{establishment.businessId}' was not found.");
            }

            if (IsTerminal(business.state))
            {
                return Fail(BusinessOperationCode.InvalidState, "Closed businesses cannot open new establishments.");
            }

            if (!TryGetBusinessDefinition(business.businessDefinitionId, out BusinessDefinition definition) || !definition.PermittedEstablishmentTypes.Contains(establishment.type))
            {
                return Fail(BusinessOperationCode.PolicyViolation, $"Establishment type '{establishment.type}' is not permitted for business '{business.businessId}'.");
            }

            if (establishmentsById.ContainsKey(establishment.establishmentId))
            {
                return BusinessOperationResult.Success("Establishment already exists.", before, before, duplicate: true).With(establishment: establishmentsById[establishment.establishmentId]);
            }

            if (preview)
            {
                return BusinessOperationResult.PreviewResult("Establishment preview succeeded.", before).With(establishment: establishment);
            }

            establishmentsById.Add(establishment.establishmentId, establishment);
            business.establishmentIds = AddId(business.establishmentIds, establishment.establishmentId);
            business.revision++;
            Touch();
            return BusinessOperationResult.Success("Establishment added.", before, Revision).With(establishment: establishment, business: business);
        }

        public BusinessOperationResult AssignAccount(BusinessAccountAssignmentData request, EconomyRuntime economy, bool preview = false)
        {
            long before = Revision;
            BusinessAccountAssignmentData assignment = request?.Clone();
            if (assignment == null || string.IsNullOrWhiteSpace(assignment.assignmentId) || string.IsNullOrWhiteSpace(assignment.businessId) || string.IsNullOrWhiteSpace(assignment.accountId))
            {
                return Fail(BusinessOperationCode.InvalidRequest, "Account assignment ID, business ID, and account ID are required.");
            }

            if (!businessesById.TryGetValue(assignment.businessId, out BusinessInstanceData business))
            {
                return Fail(BusinessOperationCode.MissingBusiness, $"Business '{assignment.businessId}' was not found.");
            }

            if (economy == null || !economy.TryGetAccount(assignment.accountId, out EconomyAccountSnapshot account))
            {
                return Fail(BusinessOperationCode.MissingExternalReference, $"Account '{assignment.accountId}' was not found.");
            }

            if (business.operatingCurrencyIds.Length > 0 && !business.operatingCurrencyIds.Contains(account.CurrencyId, StringComparer.Ordinal))
            {
                return Fail(BusinessOperationCode.CurrencyMismatch, $"Account currency '{account.CurrencyId}' is not an operating currency for business '{business.businessId}'.");
            }

            if (accountAssignmentsById.ContainsKey(assignment.assignmentId))
            {
                return BusinessOperationResult.Success("Account assignment already exists.", before, before, duplicate: true).With(accountAssignment: accountAssignmentsById[assignment.assignmentId]);
            }

            if (preview)
            {
                return BusinessOperationResult.PreviewResult("Account assignment preview succeeded.", before).With(accountAssignment: assignment);
            }

            accountAssignmentsById.Add(assignment.assignmentId, assignment);
            business.accountAssignmentIds = AddId(business.accountAssignmentIds, assignment.assignmentId);
            business.revision++;
            if (!string.IsNullOrWhiteSpace(assignment.establishmentId) && establishmentsById.TryGetValue(assignment.establishmentId, out BusinessEstablishmentData establishment))
            {
                establishment.accountAssignmentIds = AddId(establishment.accountAssignmentIds, assignment.assignmentId);
                establishment.revision++;
            }

            Touch();
            return BusinessOperationResult.Success("Business account assigned.", before, Revision).With(accountAssignment: assignment, business: business);
        }

        public BusinessOperationResult AssignInventory(BusinessInventoryAssignmentData request, bool preview = false)
        {
            long before = Revision;
            BusinessInventoryAssignmentData assignment = request?.Clone();
            if (assignment == null || string.IsNullOrWhiteSpace(assignment.assignmentId) || string.IsNullOrWhiteSpace(assignment.businessId) || string.IsNullOrWhiteSpace(assignment.inventoryId))
            {
                return Fail(BusinessOperationCode.InvalidRequest, "Inventory assignment ID, business ID, and inventory ID are required.");
            }

            if (!businessesById.TryGetValue(assignment.businessId, out BusinessInstanceData business))
            {
                return Fail(BusinessOperationCode.MissingBusiness, $"Business '{assignment.businessId}' was not found.");
            }

            bool incompatible = inventoryAssignmentsById.Values.Any(item => item.inventoryId == assignment.inventoryId
                && item.businessId != assignment.businessId
                && item.effectiveEndWorldTime < 0d
                && IsExclusiveInventoryPurpose(item.purpose)
                && IsExclusiveInventoryPurpose(assignment.purpose));
            if (incompatible)
            {
                return Fail(BusinessOperationCode.PolicyViolation, $"Inventory '{assignment.inventoryId}' is already assigned incompatibly.");
            }

            if (inventoryAssignmentsById.ContainsKey(assignment.assignmentId))
            {
                return BusinessOperationResult.Success("Inventory assignment already exists.", before, before, duplicate: true).With(inventoryAssignment: inventoryAssignmentsById[assignment.assignmentId]);
            }

            if (preview)
            {
                return BusinessOperationResult.PreviewResult("Inventory assignment preview succeeded.", before).With(inventoryAssignment: assignment);
            }

            inventoryAssignmentsById.Add(assignment.assignmentId, assignment);
            business.inventoryAssignmentIds = AddId(business.inventoryAssignmentIds, assignment.assignmentId);
            business.revision++;
            if (!string.IsNullOrWhiteSpace(assignment.establishmentId) && establishmentsById.TryGetValue(assignment.establishmentId, out BusinessEstablishmentData establishment))
            {
                establishment.inventoryAssignmentIds = AddId(establishment.inventoryAssignmentIds, assignment.assignmentId);
                establishment.revision++;
            }

            Touch();
            return BusinessOperationResult.Success("Business inventory assigned.", before, Revision).With(inventoryAssignment: assignment, business: business);
        }

        public BusinessOperationResult ClassifyStock(BusinessStockClassificationData request, ItemInstanceIdentityRuntime items, bool preview = false)
        {
            long before = Revision;
            BusinessStockClassificationData stock = request?.Clone();
            if (stock == null || string.IsNullOrWhiteSpace(stock.stockClassificationId) || string.IsNullOrWhiteSpace(stock.businessId))
            {
                return Fail(BusinessOperationCode.InvalidRequest, "Stock classification ID and business ID are required.");
            }

            if (!businessesById.ContainsKey(stock.businessId))
            {
                return Fail(BusinessOperationCode.MissingBusiness, $"Business '{stock.businessId}' was not found.");
            }

            if (!string.IsNullOrWhiteSpace(stock.itemInstanceId))
            {
                if (items == null || !items.TryGetSnapshot(stock.itemInstanceId, out ItemInstanceSnapshot item))
                {
                    return Fail(BusinessOperationCode.MissingExternalReference, $"Item instance '{stock.itemInstanceId}' was not found.");
                }

                stock.itemDefinitionId = string.IsNullOrWhiteSpace(stock.itemDefinitionId) ? item.ItemDefinitionId : stock.itemDefinitionId;
                stock.owningSubjectId = string.IsNullOrWhiteSpace(stock.owningSubjectId) ? item.OwnerPersonId : stock.owningSubjectId;
                stock.custodianSubjectId = string.IsNullOrWhiteSpace(stock.custodianSubjectId) ? item.CustodianPersonId : stock.custodianSubjectId;
            }
            else if (string.IsNullOrWhiteSpace(stock.itemDefinitionId))
            {
                return Fail(BusinessOperationCode.InvalidRequest, "Stock classification must reference an item instance or item definition.");
            }

            if (stockClassificationsById.ContainsKey(stock.stockClassificationId))
            {
                return BusinessOperationResult.Success("Stock classification already exists.", before, before, duplicate: true).With(stockClassification: stockClassificationsById[stock.stockClassificationId]);
            }

            if (preview)
            {
                return BusinessOperationResult.PreviewResult("Stock classification preview succeeded.", before).With(stockClassification: stock);
            }

            stockClassificationsById.Add(stock.stockClassificationId, stock);
            Touch();
            return BusinessOperationResult.Success("Stock classified.", before, Revision).With(stockClassification: stock);
        }

        public BusinessOperationResult ReleaseExactStock(string stockClassificationId, string expectedItemInstanceId, bool preview = false)
        {
            long before = Revision;
            if (string.IsNullOrWhiteSpace(stockClassificationId)
                || !stockClassificationsById.TryGetValue(stockClassificationId, out BusinessStockClassificationData stock))
            {
                return Fail(BusinessOperationCode.InvalidRequest, $"Stock classification '{stockClassificationId}' was not found.");
            }

            if (string.IsNullOrWhiteSpace(stock.itemInstanceId)
                || !string.Equals(stock.itemInstanceId, expectedItemInstanceId, StringComparison.Ordinal))
            {
                return Fail(BusinessOperationCode.InvalidRequest, "Only the exact classified item can be released from business stock.");
            }

            if (preview)
            {
                return BusinessOperationResult.PreviewResult("Exact business stock release preview succeeded.", before).With(stockClassification: stock);
            }

            stockClassificationsById.Remove(stockClassificationId);
            Touch();
            return BusinessOperationResult.Success("Exact business stock released.", before, Revision).With(stockClassification: stock);
        }

        public BusinessOperationResult ReclassifyExactStock(
            string stockClassificationId,
            string expectedItemInstanceId,
            BusinessStockCategory category,
            string intendedUse,
            bool saleEligible,
            bool productionEligible,
            bool preview = false)
        {
            long before = Revision;
            if (string.IsNullOrWhiteSpace(stockClassificationId)
                || !stockClassificationsById.TryGetValue(stockClassificationId, out BusinessStockClassificationData stock))
            {
                return Fail(BusinessOperationCode.InvalidRequest, $"Stock classification '{stockClassificationId}' was not found.");
            }

            if (string.IsNullOrWhiteSpace(stock.itemInstanceId)
                || !string.Equals(stock.itemInstanceId, expectedItemInstanceId, StringComparison.Ordinal))
            {
                return Fail(BusinessOperationCode.InvalidRequest, "Only the exact classified item can be reclassified.");
            }

            BusinessStockClassificationData updated = stock.Clone();
            updated.category = category;
            updated.intendedUse = intendedUse ?? string.Empty;
            updated.saleEligible = saleEligible;
            updated.productionEligible = productionEligible;
            updated.revision = checked(updated.revision + 1L);
            if (preview)
            {
                return BusinessOperationResult.PreviewResult("Exact business stock reclassification preview succeeded.", before)
                    .With(stockClassification: updated);
            }

            stockClassificationsById[stockClassificationId] = updated;
            Touch();
            return BusinessOperationResult.Success("Exact business stock reclassified.", before, Revision)
                .With(stockClassification: updated);
        }

        public BusinessOperationResult SponsorProduction(BusinessProductionOwnershipData request, ProductionWorkflowRuntime production, EconomyRuntime economy, bool preview = false)
        {
            long before = Revision;
            BusinessProductionOwnershipData ownership = request?.Clone();
            if (ownership == null || string.IsNullOrWhiteSpace(ownership.productionOwnershipId) || string.IsNullOrWhiteSpace(ownership.businessId) || string.IsNullOrWhiteSpace(ownership.productionJobId))
            {
                return Fail(BusinessOperationCode.InvalidRequest, "Production ownership ID, business ID, and production job ID are required.");
            }

            if (!businessesById.ContainsKey(ownership.businessId))
            {
                return Fail(BusinessOperationCode.MissingBusiness, $"Business '{ownership.businessId}' was not found.");
            }

            if (production == null || !production.TryGetJob(ownership.productionJobId, out ProductionJobData job))
            {
                return Fail(BusinessOperationCode.MissingExternalReference, $"Production job '{ownership.productionJobId}' was not found.");
            }

            ownership.productionBatchId = string.IsNullOrWhiteSpace(ownership.productionBatchId) ? job.batchId : ownership.productionBatchId;
            if (!string.IsNullOrWhiteSpace(ownership.fundingAccountId) && (economy == null || !economy.TryGetAccount(ownership.fundingAccountId, out _)))
            {
                return Fail(BusinessOperationCode.MissingExternalReference, $"Funding account '{ownership.fundingAccountId}' was not found.");
            }

            if (productionOwnershipById.ContainsKey(ownership.productionOwnershipId))
            {
                return BusinessOperationResult.Success("Production ownership already exists.", before, before, duplicate: true).With(productionOwnership: productionOwnershipById[ownership.productionOwnershipId]);
            }

            if (preview)
            {
                return BusinessOperationResult.PreviewResult("Production ownership preview succeeded.", before).With(productionOwnership: ownership);
            }

            productionOwnershipById.Add(ownership.productionOwnershipId, ownership);
            Touch();
            return BusinessOperationResult.Success("Production sponsored.", before, Revision).With(productionOwnership: ownership);
        }

        public BusinessOperationResult AuthorizeFunding(BusinessFundingAllocationData request, EconomyRuntime economy, bool preview = false)
        {
            long before = Revision;
            BusinessFundingAllocationData allocation = request?.Clone();
            if (allocation == null || string.IsNullOrWhiteSpace(allocation.allocationId) || string.IsNullOrWhiteSpace(allocation.businessId) || string.IsNullOrWhiteSpace(allocation.accountId) || allocation.maximumAuthorizedAmount == null || allocation.maximumAuthorizedAmount.units < 0L)
            {
                return Fail(BusinessOperationCode.InvalidRequest, "Funding allocation ID, business ID, account ID, and non-negative amount are required.");
            }

            if (!businessesById.ContainsKey(allocation.businessId))
            {
                return Fail(BusinessOperationCode.MissingBusiness, $"Business '{allocation.businessId}' was not found.");
            }

            if (economy == null || !economy.TryGetAccount(allocation.accountId, out EconomyAccountSnapshot account))
            {
                return Fail(BusinessOperationCode.MissingExternalReference, $"Funding account '{allocation.accountId}' was not found.");
            }

            if (!string.Equals(account.CurrencyId, allocation.maximumAuthorizedAmount.currencyId, StringComparison.Ordinal))
            {
                return Fail(BusinessOperationCode.CurrencyMismatch, "Funding allocation currency does not match account currency.");
            }

            if (fundingAllocationsById.ContainsKey(allocation.allocationId))
            {
                return BusinessOperationResult.Success("Funding allocation already exists.", before, before, duplicate: true).With(fundingAllocation: fundingAllocationsById[allocation.allocationId]);
            }

            if (preview)
            {
                return BusinessOperationResult.PreviewResult("Funding allocation preview succeeded.", before).With(fundingAllocation: allocation);
            }

            fundingAllocationsById.Add(allocation.allocationId, allocation);
            Touch();
            return BusinessOperationResult.Success("Funding allocation recorded.", before, Revision).With(fundingAllocation: allocation);
        }

        public BusinessOperationResult RecordRevenue(BusinessRevenueRecordData request, EconomyRuntime economy, TradeRuntime trades = null, bool preview = false)
        {
            long before = Revision;
            BusinessRevenueRecordData revenue = request?.Clone();
            if (!ValidateRevenue(revenue, economy, trades, out BusinessOperationCode code, out string failure))
            {
                return Fail(code, failure);
            }

            if (revenueById.ContainsKey(revenue.revenueRecordId) || SourceAlreadyClassifiedAsRevenue(revenue))
            {
                return Fail(BusinessOperationCode.Duplicate, "Revenue source is already classified.");
            }

            if (preview)
            {
                return BusinessOperationResult.PreviewResult("Revenue preview succeeded.", before).With(revenue: revenue);
            }

            revenueById.Add(revenue.revenueRecordId, revenue);
            Touch();
            return BusinessOperationResult.Success("Revenue classified.", before, Revision).With(revenue: revenue);
        }

        public BusinessOperationResult RecordExpense(BusinessExpenseRecordData request, EconomyRuntime economy, PayrollRuntime payroll = null, bool preview = false)
        {
            long before = Revision;
            BusinessExpenseRecordData expense = request?.Clone();
            if (!ValidateExpense(expense, economy, payroll, out BusinessOperationCode code, out string failure))
            {
                return Fail(code, failure);
            }

            if (expensesById.ContainsKey(expense.expenseRecordId) || SourceAlreadyClassifiedAsExpense(expense))
            {
                return Fail(BusinessOperationCode.Duplicate, "Expense source is already classified.");
            }

            if (preview)
            {
                return BusinessOperationResult.PreviewResult("Expense preview succeeded.", before).With(expense: expense);
            }

            expensesById.Add(expense.expenseRecordId, expense);
            Touch();
            return BusinessOperationResult.Success("Expense classified.", before, Revision).With(expense: expense);
        }

        public BusinessOperationResult AddCapitalContribution(BusinessCapitalContributionData request, EconomyRuntime economy = null, bool preview = false)
        {
            long before = Revision;
            BusinessCapitalContributionData contribution = request?.Clone();
            if (contribution == null || string.IsNullOrWhiteSpace(contribution.contributionId) || string.IsNullOrWhiteSpace(contribution.businessId) || string.IsNullOrWhiteSpace(contribution.contributingSubjectId))
            {
                return Fail(BusinessOperationCode.InvalidRequest, "Capital contribution ID, business ID, and contributor are required.");
            }

            if (!businessesById.ContainsKey(contribution.businessId))
            {
                return Fail(BusinessOperationCode.MissingBusiness, $"Business '{contribution.businessId}' was not found.");
            }

            if (string.IsNullOrWhiteSpace(contribution.transactionOrTransferReferenceId) && contribution.assetReferenceIds.Length == 0)
            {
                return Fail(BusinessOperationCode.MissingExternalReference, "Capital contribution requires an authoritative transaction or asset transfer reference.");
            }

            if (!string.IsNullOrWhiteSpace(contribution.transactionOrTransferReferenceId) && economy != null && !economy.TryGetTransaction(contribution.transactionOrTransferReferenceId, out _))
            {
                return Fail(BusinessOperationCode.MissingExternalReference, $"Capital contribution transaction '{contribution.transactionOrTransferReferenceId}' was not found.");
            }

            if (capitalById.ContainsKey(contribution.contributionId))
            {
                return BusinessOperationResult.Success("Capital contribution already exists.", before, before, duplicate: true).With(capitalContribution: capitalById[contribution.contributionId]);
            }

            if (preview)
            {
                return BusinessOperationResult.PreviewResult("Capital contribution preview succeeded.", before).With(capitalContribution: contribution);
            }

            capitalById.Add(contribution.contributionId, contribution);
            Touch();
            return BusinessOperationResult.Success("Capital contribution recorded.", before, Revision).With(capitalContribution: contribution);
        }

        public BusinessOperationResult RecordOwnerWithdrawal(BusinessOwnerWithdrawalData request, EconomyRuntime economy = null, bool preview = false)
        {
            long before = Revision;
            BusinessOwnerWithdrawalData withdrawal = request?.Clone();
            if (withdrawal == null || string.IsNullOrWhiteSpace(withdrawal.withdrawalId) || string.IsNullOrWhiteSpace(withdrawal.businessId) || string.IsNullOrWhiteSpace(withdrawal.receivingOwnerSubjectId))
            {
                return Fail(BusinessOperationCode.InvalidRequest, "Owner withdrawal ID, business ID, and receiving owner are required.");
            }

            if (!businessesById.ContainsKey(withdrawal.businessId))
            {
                return Fail(BusinessOperationCode.MissingBusiness, $"Business '{withdrawal.businessId}' was not found.");
            }

            if (!IsCurrentOwner(withdrawal.businessId, withdrawal.receivingOwnerSubjectId, withdrawal.worldTime))
            {
                return Fail(BusinessOperationCode.MissingAuthority, "Receiving subject is not a current owner.");
            }

            if (string.IsNullOrWhiteSpace(withdrawal.transactionOrTransferReferenceId) && withdrawal.assetReferenceIds.Length == 0)
            {
                return Fail(BusinessOperationCode.MissingExternalReference, "Owner withdrawal requires an authoritative transaction or asset transfer reference.");
            }

            if (!string.IsNullOrWhiteSpace(withdrawal.transactionOrTransferReferenceId) && economy != null && !economy.TryGetTransaction(withdrawal.transactionOrTransferReferenceId, out _))
            {
                return Fail(BusinessOperationCode.MissingExternalReference, $"Owner withdrawal transaction '{withdrawal.transactionOrTransferReferenceId}' was not found.");
            }

            if (withdrawalsById.ContainsKey(withdrawal.withdrawalId))
            {
                return BusinessOperationResult.Success("Owner withdrawal already exists.", before, before, duplicate: true).With(ownerWithdrawal: withdrawalsById[withdrawal.withdrawalId]);
            }

            if (preview)
            {
                return BusinessOperationResult.PreviewResult("Owner withdrawal preview succeeded.", before).With(ownerWithdrawal: withdrawal);
            }

            withdrawalsById.Add(withdrawal.withdrawalId, withdrawal);
            Touch();
            return BusinessOperationResult.Success("Owner withdrawal recorded.", before, Revision).With(ownerWithdrawal: withdrawal);
        }

        public BusinessOperationResult OpenAccountingPeriod(BusinessAccountingPeriodData request, bool preview = false)
        {
            long before = Revision;
            BusinessAccountingPeriodData period = request?.Clone();
            if (period == null || string.IsNullOrWhiteSpace(period.accountingPeriodId) || string.IsNullOrWhiteSpace(period.businessId) || string.IsNullOrWhiteSpace(period.currencyId))
            {
                return Fail(BusinessOperationCode.InvalidRequest, "Accounting period ID, business ID, and currency are required.");
            }

            if (!businessesById.ContainsKey(period.businessId))
            {
                return Fail(BusinessOperationCode.MissingBusiness, $"Business '{period.businessId}' was not found.");
            }

            if (period.endWorldTime <= period.startWorldTime)
            {
                return Fail(BusinessOperationCode.InvalidRequest, "Accounting period end must be after start.");
            }

            if (periodsById.Values.Any(item => item.businessId == period.businessId && item.currencyId == period.currencyId && RangesOverlap(item.startWorldTime, item.endWorldTime, period.startWorldTime, period.endWorldTime)))
            {
                return Fail(BusinessOperationCode.PolicyViolation, "Accounting period overlaps an existing period.");
            }

            if (periodsById.ContainsKey(period.accountingPeriodId))
            {
                return BusinessOperationResult.Success("Accounting period already exists.", before, before, duplicate: true).With(accountingPeriod: periodsById[period.accountingPeriodId]);
            }

            if (preview)
            {
                return BusinessOperationResult.PreviewResult("Accounting period preview succeeded.", before).With(accountingPeriod: period);
            }

            period.state = AccountingPeriodState.Open;
            periodsById.Add(period.accountingPeriodId, period);
            Touch();
            return BusinessOperationResult.Success("Accounting period opened.", before, Revision).With(accountingPeriod: period);
        }

        public BusinessOperationResult CloseAccountingPeriod(string accountingPeriodId, string statementId, string cashFlowSummaryId, double worldTime = 0d, bool preview = false)
        {
            long before = Revision;
            if (!periodsById.TryGetValue(accountingPeriodId ?? string.Empty, out BusinessAccountingPeriodData period))
            {
                return Fail(BusinessOperationCode.InvalidRequest, $"Accounting period '{accountingPeriodId}' was not found.");
            }

            if (period.state == AccountingPeriodState.Closed && period.statementIds.Contains(statementId ?? string.Empty, StringComparer.Ordinal))
            {
                statementsById.TryGetValue(statementId ?? string.Empty, out BusinessProfitAndLossStatementData existingStatement);
                cashFlowsById.TryGetValue(cashFlowSummaryId ?? string.Empty, out BusinessCashFlowSummaryData existingCashFlow);
                return BusinessOperationResult.Success("Accounting period already closed from the same source state.", before, before, duplicate: true)
                    .With(accountingPeriod: period, profitAndLossStatement: existingStatement, cashFlowSummary: existingCashFlow);
            }

            if (period.state != AccountingPeriodState.Open)
            {
                return Fail(BusinessOperationCode.InvalidState, $"Accounting period '{accountingPeriodId}' is not open.");
            }

            if (string.IsNullOrWhiteSpace(statementId) || string.IsNullOrWhiteSpace(cashFlowSummaryId))
            {
                return Fail(BusinessOperationCode.InvalidRequest, "Statement and cash-flow summary IDs are required.");
            }

            BusinessProfitAndLossStatementData statement = BuildProfitAndLoss(period, statementId, worldTime);
            BusinessCashFlowSummaryData cashFlow = BuildCashFlow(period, cashFlowSummaryId, worldTime);
            BusinessAccountingPeriodData projectedPeriod = period.Clone();
            projectedPeriod.state = AccountingPeriodState.Closed;
            projectedPeriod.includedRevenueRecordIds = statement.sourceRevenueRecordIds;
            projectedPeriod.includedExpenseRecordIds = statement.sourceExpenseRecordIds;
            projectedPeriod.capitalContributionIds = capitalById.Values.Where(item => item.businessId == period.businessId && InRange(item.worldTime, period)).Select(item => item.contributionId).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            projectedPeriod.ownerWithdrawalIds = withdrawalsById.Values.Where(item => item.businessId == period.businessId && InRange(item.worldTime, period)).Select(item => item.withdrawalId).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            projectedPeriod.statementIds = BusinessModelHelpers.CleanIds(new[] { statement.statementId, cashFlow.summaryId });

            if (preview)
            {
                return BusinessOperationResult.PreviewResult("Accounting close preview succeeded.", before).With(accountingPeriod: projectedPeriod, profitAndLossStatement: statement, cashFlowSummary: cashFlow);
            }

            period.state = projectedPeriod.state;
            period.includedRevenueRecordIds = projectedPeriod.includedRevenueRecordIds;
            period.includedExpenseRecordIds = projectedPeriod.includedExpenseRecordIds;
            period.capitalContributionIds = projectedPeriod.capitalContributionIds;
            period.ownerWithdrawalIds = projectedPeriod.ownerWithdrawalIds;
            period.statementIds = projectedPeriod.statementIds;
            period.revision++;
            statementsById.Add(statement.statementId, statement);
            cashFlowsById.Add(cashFlow.summaryId, cashFlow);
            Touch();
            return BusinessOperationResult.Success("Accounting period closed.", before, Revision).With(accountingPeriod: period, profitAndLossStatement: statement, cashFlowSummary: cashFlow);
        }

        public BusinessPerformanceSummary GetPerformanceSummary(string businessId, string currencyId = "")
        {
            if (!businessesById.TryGetValue(businessId ?? string.Empty, out BusinessInstanceData business))
            {
                return null;
            }

            string currency = string.IsNullOrWhiteSpace(currencyId)
                ? business.operatingCurrencyIds.FirstOrDefault() ?? string.Empty
                : currencyId;
            long revenue = revenueById.Values.Where(item => item.businessId == business.businessId && item.amount.currencyId == currency).Sum(item => item.amount.units);
            long expense = expensesById.Values.Where(item => item.businessId == business.businessId && item.amount.currencyId == currency).Sum(item => item.amount.units);
            long cash = cashFlowsById.Values.Where(item => item.businessId == business.businessId && item.currencyId == currency).Sum(item => item.netCashChange.units);
            return new BusinessPerformanceSummary
            {
                businessId = business.businessId,
                state = business.state,
                activeEstablishments = establishmentsById.Values.Count(item => item.businessId == business.businessId && item.state == BusinessEstablishmentState.Open),
                activeOwnershipRecords = ownershipById.Values.Count(item => item.businessId == business.businessId && item.ActiveAt(double.MaxValue / 4d)),
                activeEmployees = business.employmentIds.Length,
                vacantPositions = 0,
                retailStockRecords = stockClassificationsById.Values.Count(item => item.businessId == business.businessId && item.category == BusinessStockCategory.ForSale),
                productionInputRecords = stockClassificationsById.Values.Count(item => item.businessId == business.businessId && item.category == BusinessStockCategory.ProductionInput),
                workInProgressRecords = stockClassificationsById.Values.Count(item => item.businessId == business.businessId && item.category == BusinessStockCategory.WorkInProgress),
                finishedGoodsRecords = stockClassificationsById.Values.Count(item => item.businessId == business.businessId && item.category == BusinessStockCategory.FinishedProduct),
                openProductionJobs = productionOwnershipById.Values.Count(item => item.businessId == business.businessId),
                completedTrades = revenueById.Values.Count(item => item.businessId == business.businessId && !string.IsNullOrWhiteSpace(item.tradeRecordId)),
                revenueUnits = revenue,
                expenseUnits = expense,
                netProfitUnits = revenue - expense,
                netCashChangeUnits = cash,
                currencyId = currency
            };
        }

        public BusinessProjection ProjectBusiness(string businessId, InformationAccessRuntime access, InformationAccessContext context, BusinessProjectionKind projectionKind)
        {
            if (!businessesById.TryGetValue(businessId ?? string.Empty, out BusinessInstanceData business))
            {
                return new BusinessProjection(null, null, false, Array.Empty<string>());
            }

            string[] allDetails =
            {
                "business.public",
                "business.owners",
                "business.control",
                "business.accounts",
                "business.inventories",
                "business.stock",
                "business.production",
                "business.staffing",
                "business.payroll",
                "business.financials"
            };

            if (projectionKind == BusinessProjectionKind.PrivilegedDebug || access == null || string.IsNullOrWhiteSpace(business.accessPolicyId))
            {
                return new BusinessProjection(business.Clone(), null, false, allDetails);
            }

            InformationAccessContext projected = InformationAccessProjectionUtility.BuildContext(context, business.CreateInformationSubject(), InformationAccessMode.Query, InformationAccessPurpose.Gameplay, allDetails, business.accessPolicyId);
            RedactedInformationProjection redaction = access.Project(projected, allDetails);
            if (redaction.Decision == null || redaction.Decision.Denied)
            {
                return new BusinessProjection(null, redaction.Decision, true, Array.Empty<string>());
            }

            BusinessInstanceData clone = business.Clone();
            bool financialsVisible = InformationAccessProjectionUtility.IsVisible(redaction.Details, "business.financials");
            bool accountsVisible = InformationAccessProjectionUtility.IsVisible(redaction.Details, "business.accounts");
            bool ownersVisible = InformationAccessProjectionUtility.IsVisible(redaction.Details, "business.owners");
            bool controlVisible = InformationAccessProjectionUtility.IsVisible(redaction.Details, "business.control");
            if (!financialsVisible)
            {
                clone.operatingCurrencyIds = Array.Empty<string>();
            }

            if (!accountsVisible)
            {
                clone.accountAssignmentIds = Array.Empty<string>();
            }

            if (!ownersVisible)
            {
                clone.founderSubjectIds = Array.Empty<string>();
            }

            if (!controlVisible)
            {
                clone.controllerSubjectId = string.Empty;
            }

            return new BusinessProjection(clone, redaction.Decision, redaction.Decision.RedactedAccess || redaction.Decision.PartialAccess, redaction.Details.Where(pair => pair.Value == InformationRedactionState.Visible).Select(pair => pair.Key).ToArray());
        }

        public BusinessRuntimeSaveData CreateSaveData()
        {
            return new BusinessRuntimeSaveData
            {
                schemaVersion = CurrentSaveSchemaVersion,
                revision = Revision,
                businesses = Businesses.ToArray(),
                ownershipRecords = OwnershipRecords.ToArray(),
                controlRecords = ControlRecords.ToArray(),
                establishments = Establishments.ToArray(),
                accountAssignments = AccountAssignments.ToArray(),
                inventoryAssignments = InventoryAssignments.ToArray(),
                stockClassifications = StockClassifications.ToArray(),
                productionOwnershipRecords = ProductionOwnershipRecords.ToArray(),
                fundingAllocations = FundingAllocations.ToArray(),
                revenueRecords = RevenueRecords.ToArray(),
                expenseRecords = ExpenseRecords.ToArray(),
                capitalContributions = CapitalContributions.ToArray(),
                ownerWithdrawals = OwnerWithdrawals.ToArray(),
                accountingPeriods = AccountingPeriods.ToArray(),
                profitAndLossStatements = ProfitAndLossStatements.ToArray(),
                cashFlowSummaries = CashFlowSummaries.ToArray()
            };
        }

        public BusinessOperationResult RestoreFromSaveData(BusinessRuntimeSaveData saveData, DefinitionRegistry definitionRegistry)
        {
            long before = Revision;
            if (!ValidateSaveData(saveData, definitionRegistry, out string failure))
            {
                return BusinessOperationResult.Failure(BusinessOperationCode.RestoreFailed, failure, before);
            }

            BusinessRuntimeSaveData clean = saveData?.Clone() ?? new BusinessRuntimeSaveData();
            businessesById.Clear();
            ownershipById.Clear();
            controlsById.Clear();
            establishmentsById.Clear();
            accountAssignmentsById.Clear();
            inventoryAssignmentsById.Clear();
            stockClassificationsById.Clear();
            productionOwnershipById.Clear();
            fundingAllocationsById.Clear();
            revenueById.Clear();
            expensesById.Clear();
            capitalById.Clear();
            withdrawalsById.Clear();
            periodsById.Clear();
            statementsById.Clear();
            cashFlowsById.Clear();
            AddAll(clean.businesses, item => item.businessId, businessesById);
            AddAll(clean.ownershipRecords, item => item.ownershipRecordId, ownershipById);
            AddAll(clean.controlRecords, item => item.controlRecordId, controlsById);
            AddAll(clean.establishments, item => item.establishmentId, establishmentsById);
            AddAll(clean.accountAssignments, item => item.assignmentId, accountAssignmentsById);
            AddAll(clean.inventoryAssignments, item => item.assignmentId, inventoryAssignmentsById);
            AddAll(clean.stockClassifications, item => item.stockClassificationId, stockClassificationsById);
            AddAll(clean.productionOwnershipRecords, item => item.productionOwnershipId, productionOwnershipById);
            AddAll(clean.fundingAllocations, item => item.allocationId, fundingAllocationsById);
            AddAll(clean.revenueRecords, item => item.revenueRecordId, revenueById);
            AddAll(clean.expenseRecords, item => item.expenseRecordId, expensesById);
            AddAll(clean.capitalContributions, item => item.contributionId, capitalById);
            AddAll(clean.ownerWithdrawals, item => item.withdrawalId, withdrawalsById);
            AddAll(clean.accountingPeriods, item => item.accountingPeriodId, periodsById);
            AddAll(clean.profitAndLossStatements, item => item.statementId, statementsById);
            AddAll(clean.cashFlowSummaries, item => item.summaryId, cashFlowsById);
            Revision = Math.Max(0L, clean.revision);
            registry = definitionRegistry ?? registry;
            return BusinessOperationResult.Success("Business runtime restored.", before, Revision);
        }

        public static bool ValidateSaveData(BusinessRuntimeSaveData saveData, DefinitionRegistry registry, out string failure)
        {
            failure = string.Empty;
            if (saveData == null)
            {
                return true;
            }

            if (saveData.schemaVersion != CurrentSaveSchemaVersion)
            {
                failure = $"Unsupported business schema version {saveData.schemaVersion}.";
                return false;
            }

            HashSet<string> businessIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (BusinessInstanceData business in saveData.businesses ?? Array.Empty<BusinessInstanceData>())
            {
                if (business == null || string.IsNullOrWhiteSpace(business.businessId) || !businessIds.Add(business.businessId))
                {
                    failure = "Business save data contains an empty or duplicate business ID.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(business.businessDefinitionId) && registry != null && !registry.TryGet(business.businessDefinitionId, out BusinessDefinition _))
                {
                    failure = $"Business '{business.businessId}' references missing Business definition '{business.businessDefinitionId}'.";
                    return false;
                }
            }

            if (!ValidateChildReferences(saveData.ownershipRecords, item => item?.ownershipRecordId, item => item?.businessId, businessIds, "ownership", out failure)
                || !ValidateChildReferences(saveData.controlRecords, item => item?.controlRecordId, item => item?.businessId, businessIds, "control", out failure)
                || !ValidateChildReferences(saveData.establishments, item => item?.establishmentId, item => item?.businessId, businessIds, "establishment", out failure)
                || !ValidateChildReferences(saveData.accountAssignments, item => item?.assignmentId, item => item?.businessId, businessIds, "account assignment", out failure)
                || !ValidateChildReferences(saveData.inventoryAssignments, item => item?.assignmentId, item => item?.businessId, businessIds, "inventory assignment", out failure)
                || !ValidateChildReferences(saveData.stockClassifications, item => item?.stockClassificationId, item => item?.businessId, businessIds, "stock classification", out failure)
                || !ValidateChildReferences(saveData.productionOwnershipRecords, item => item?.productionOwnershipId, item => item?.businessId, businessIds, "production ownership", out failure)
                || !ValidateChildReferences(saveData.fundingAllocations, item => item?.allocationId, item => item?.businessId, businessIds, "funding allocation", out failure)
                || !ValidateChildReferences(saveData.revenueRecords, item => item?.revenueRecordId, item => item?.businessId, businessIds, "revenue", out failure)
                || !ValidateChildReferences(saveData.expenseRecords, item => item?.expenseRecordId, item => item?.businessId, businessIds, "expense", out failure)
                || !ValidateChildReferences(saveData.capitalContributions, item => item?.contributionId, item => item?.businessId, businessIds, "capital contribution", out failure)
                || !ValidateChildReferences(saveData.ownerWithdrawals, item => item?.withdrawalId, item => item?.businessId, businessIds, "owner withdrawal", out failure)
                || !ValidateChildReferences(saveData.accountingPeriods, item => item?.accountingPeriodId, item => item?.businessId, businessIds, "accounting period", out failure)
                || !ValidateChildReferences(saveData.cashFlowSummaries, item => item?.summaryId, item => item?.businessId, businessIds, "cash-flow summary", out failure))
            {
                return false;
            }

            HashSet<string> statementIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (BusinessProfitAndLossStatementData statement in saveData.profitAndLossStatements ?? Array.Empty<BusinessProfitAndLossStatementData>())
            {
                if (statement == null || string.IsNullOrWhiteSpace(statement.statementId) || !statementIds.Add(statement.statementId) || !businessIds.Contains(statement.businessId ?? string.Empty))
                {
                    failure = "Business save data contains an invalid or duplicate profit-and-loss statement.";
                    return false;
                }
            }

            return true;
        }

        private BusinessOperationResult Fail(BusinessOperationCode code, string message)
        {
            return BusinessOperationResult.Failure(code, message, Revision);
        }

        private void Touch()
        {
            Revision++;
        }

        private bool TryGetBusinessDefinition(string definitionId, out BusinessDefinition definition)
        {
            definition = null;
            return registry != null && registry.TryGet(definitionId, out definition);
        }

        private bool ValidateOwnership(BusinessOwnershipRecordData ownership, double worldTime, out BusinessOperationCode code, out string failure)
        {
            code = BusinessOperationCode.InvalidRequest;
            failure = string.Empty;
            if (ownership == null || string.IsNullOrWhiteSpace(ownership.ownershipRecordId) || string.IsNullOrWhiteSpace(ownership.businessId) || ownership.owner == null || string.IsNullOrWhiteSpace(ownership.owner.subjectId))
            {
                failure = "Ownership record ID, business ID, and owner subject are required.";
                return false;
            }

            if (!businessesById.TryGetValue(ownership.businessId, out BusinessInstanceData business))
            {
                code = BusinessOperationCode.MissingBusiness;
                failure = $"Business '{ownership.businessId}' was not found.";
                return false;
            }

            if (!TryGetBusinessDefinition(business.businessDefinitionId, out BusinessDefinition definition) || !definition.PermittedOwnerTypes.Contains(ownership.owner.kind))
            {
                code = BusinessOperationCode.PolicyViolation;
                failure = $"Owner type '{ownership.owner.kind}' is not permitted for business '{ownership.businessId}'.";
                return false;
            }

            if (!ownership.economicShare.IsPositive)
            {
                failure = "Ownership economic share must be positive.";
                return false;
            }

            if (ownership.effectiveEndWorldTime >= 0d && ownership.effectiveEndWorldTime < ownership.effectiveStartWorldTime)
            {
                failure = "Ownership end cannot be before start.";
                return false;
            }

            if (IsTerminal(business.state))
            {
                code = BusinessOperationCode.InvalidState;
                failure = "Closed businesses cannot accept new active ownership records.";
                return false;
            }

            return true;
        }

        private bool ValidateRevenue(BusinessRevenueRecordData revenue, EconomyRuntime economy, TradeRuntime trades, out BusinessOperationCode code, out string failure)
        {
            code = BusinessOperationCode.InvalidRequest;
            failure = string.Empty;
            if (revenue == null || string.IsNullOrWhiteSpace(revenue.revenueRecordId) || string.IsNullOrWhiteSpace(revenue.businessId) || revenue.amount == null || revenue.amount.units == 0L || string.IsNullOrWhiteSpace(revenue.amount.currencyId))
            {
                failure = "Revenue ID, business ID, currency, and non-zero amount are required.";
                return false;
            }

            if (!businessesById.ContainsKey(revenue.businessId))
            {
                code = BusinessOperationCode.MissingBusiness;
                failure = $"Business '{revenue.businessId}' was not found.";
                return false;
            }

            if (revenue.category == BusinessRevenueCategory.CapitalContributionExclusion)
            {
                code = BusinessOperationCode.PolicyViolation;
                failure = "Capital contributions are not operating revenue by default.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(revenue.transactionId) && string.IsNullOrWhiteSpace(revenue.tradeRecordId) && revenue.soldItemOrServiceIds.Length == 0)
            {
                code = BusinessOperationCode.MissingExternalReference;
                failure = "Revenue requires an authoritative transaction, trade, item/service, or approved adjustment reference.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(revenue.transactionId) && economy != null)
            {
                if (!economy.TryGetTransaction(revenue.transactionId, out EconomyTransactionSnapshot transaction))
                {
                    code = BusinessOperationCode.MissingExternalReference;
                    failure = $"Revenue transaction '{revenue.transactionId}' was not found.";
                    return false;
                }

                if (!string.Equals(transaction.CurrencyId, revenue.amount.currencyId, StringComparison.Ordinal) || Math.Abs(transaction.Units) != Math.Abs(revenue.amount.units))
                {
                    code = BusinessOperationCode.CurrencyMismatch;
                    failure = "Revenue amount does not match its authoritative transaction.";
                    return false;
                }
            }

            return true;
        }

        private bool ValidateExpense(BusinessExpenseRecordData expense, EconomyRuntime economy, PayrollRuntime payroll, out BusinessOperationCode code, out string failure)
        {
            code = BusinessOperationCode.InvalidRequest;
            failure = string.Empty;
            if (expense == null || string.IsNullOrWhiteSpace(expense.expenseRecordId) || string.IsNullOrWhiteSpace(expense.businessId) || expense.amount == null || expense.amount.units == 0L || string.IsNullOrWhiteSpace(expense.amount.currencyId))
            {
                failure = "Expense ID, business ID, currency, and non-zero amount are required.";
                return false;
            }

            if (!businessesById.ContainsKey(expense.businessId))
            {
                code = BusinessOperationCode.MissingBusiness;
                failure = $"Business '{expense.businessId}' was not found.";
                return false;
            }

            if (expense.category == BusinessExpenseCategory.OwnerWithdrawalExclusion)
            {
                code = BusinessOperationCode.PolicyViolation;
                failure = "Owner withdrawals are not operating expenses by default.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(expense.transactionId) && string.IsNullOrWhiteSpace(expense.payrollObligationId) && string.IsNullOrWhiteSpace(expense.payrollPaymentRecordId) && string.IsNullOrWhiteSpace(expense.productionJobId) && expense.purchasedItemOrServiceIds.Length == 0)
            {
                code = BusinessOperationCode.MissingExternalReference;
                failure = "Expense requires an authoritative transaction, payroll, production, item/service, or approved adjustment reference.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(expense.transactionId) && economy != null)
            {
                if (!economy.TryGetTransaction(expense.transactionId, out EconomyTransactionSnapshot transaction))
                {
                    code = BusinessOperationCode.MissingExternalReference;
                    failure = $"Expense transaction '{expense.transactionId}' was not found.";
                    return false;
                }

                if (!string.Equals(transaction.CurrencyId, expense.amount.currencyId, StringComparison.Ordinal) || Math.Abs(transaction.Units) != Math.Abs(expense.amount.units))
                {
                    code = BusinessOperationCode.CurrencyMismatch;
                    failure = "Expense amount does not match its authoritative transaction.";
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(expense.payrollObligationId) && payroll != null && !payroll.TryGetObligation(expense.payrollObligationId, out _))
            {
                code = BusinessOperationCode.MissingExternalReference;
                failure = $"Payroll obligation '{expense.payrollObligationId}' was not found.";
                return false;
            }

            return true;
        }

        private BusinessProfitAndLossStatementData BuildProfitAndLoss(BusinessAccountingPeriodData period, string statementId, double worldTime)
        {
            BusinessRevenueRecordData[] revenue = revenueById.Values
                .Where(item => item.businessId == period.businessId && item.amount.currencyId == period.currencyId && InRange(item.recognitionWorldTime, period))
                .OrderBy(item => item.recognitionWorldTime)
                .ThenBy(item => item.revenueRecordId, StringComparer.Ordinal)
                .Select(item => item.Clone())
                .ToArray();
            BusinessExpenseRecordData[] expenses = expensesById.Values
                .Where(item => item.businessId == period.businessId && item.amount.currencyId == period.currencyId && InRange(item.recognitionWorldTime, period))
                .OrderBy(item => item.recognitionWorldTime)
                .ThenBy(item => item.expenseRecordId, StringComparer.Ordinal)
                .Select(item => item.Clone())
                .ToArray();

            long revenueTotal = revenue.Where(item => item.category != BusinessRevenueCategory.RefundAdjustment && item.category != BusinessRevenueCategory.CapitalContributionExclusion).Sum(item => item.amount.units);
            long refunds = revenue.Where(item => item.category == BusinessRevenueCategory.RefundAdjustment).Sum(item => Math.Abs(item.amount.units));
            long inventoryExpense = expenses.Where(item => item.category == BusinessExpenseCategory.InventoryPurchase || item.category == BusinessExpenseCategory.MaterialPurchase || item.category == BusinessExpenseCategory.ToolPurchase).Sum(item => Math.Abs(item.amount.units));
            long payrollExpense = expenses.Where(item => item.category == BusinessExpenseCategory.PayrollExpense).Sum(item => Math.Abs(item.amount.units));
            long operatingExpense = expenses
                .Where(item => item.category != BusinessExpenseCategory.PayrollExpense
                    && item.category != BusinessExpenseCategory.OwnerWithdrawalExclusion
                    && item.category != BusinessExpenseCategory.InventoryPurchase
                    && item.category != BusinessExpenseCategory.MaterialPurchase
                    && item.category != BusinessExpenseCategory.ToolPurchase)
                .Sum(item => Math.Abs(item.amount.units));
            long gross = revenueTotal - refunds - inventoryExpense;
            long net = revenueTotal - refunds - inventoryExpense - payrollExpense - operatingExpense;
            return new BusinessProfitAndLossStatementData
            {
                statementId = statementId,
                businessId = period.businessId,
                accountingPeriodId = period.accountingPeriodId,
                currencyId = period.currencyId,
                revenueTotal = BusinessModelHelpers.Money(period.currencyId, revenueTotal),
                refundAndReductionTotal = BusinessModelHelpers.Money(period.currencyId, refunds),
                inventoryAndMaterialExpenseTotal = BusinessModelHelpers.Money(period.currencyId, inventoryExpense),
                payrollExpenseTotal = BusinessModelHelpers.Money(period.currencyId, payrollExpense),
                operatingExpenseTotal = BusinessModelHelpers.Money(period.currencyId, operatingExpense),
                otherExpenseTotal = BusinessModelHelpers.Money(period.currencyId, 0L),
                grossOperatingResult = BusinessModelHelpers.Money(period.currencyId, gross),
                netOperatingResult = BusinessModelHelpers.Money(period.currencyId, net),
                sourceRevenueRecordIds = revenue.Select(item => item.revenueRecordId).ToArray(),
                sourceExpenseRecordIds = expenses.Select(item => item.expenseRecordId).ToArray(),
                appliedPolicyIds = new[] { "business.pnl.simple-explicit-recognition" },
                calculationDiagnostics = $"Revenue={revenueTotal} Refunds={refunds} Inventory={inventoryExpense} Payroll={payrollExpense} Operating={operatingExpense}",
                creationWorldTime = worldTime,
                revision = 1L
            };
        }

        private BusinessCashFlowSummaryData BuildCashFlow(BusinessAccountingPeriodData period, string summaryId, double worldTime)
        {
            long operatingInflows = revenueById.Values.Where(item => item.businessId == period.businessId && item.amount.currencyId == period.currencyId && InRange(item.recognitionWorldTime, period)).Sum(item => Math.Max(0L, item.amount.units));
            long operatingOutflows = expensesById.Values.Where(item => item.businessId == period.businessId && item.amount.currencyId == period.currencyId && InRange(item.recognitionWorldTime, period) && item.category != BusinessExpenseCategory.PayrollExpense).Sum(item => Math.Abs(item.amount.units));
            long payrollOutflows = expensesById.Values.Where(item => item.businessId == period.businessId && item.amount.currencyId == period.currencyId && InRange(item.recognitionWorldTime, period) && item.category == BusinessExpenseCategory.PayrollExpense).Sum(item => Math.Abs(item.amount.units));
            long capitalInflows = capitalById.Values.Where(item => item.businessId == period.businessId && item.monetaryValue.currencyId == period.currencyId && InRange(item.worldTime, period)).Sum(item => item.monetaryValue.units);
            long withdrawals = withdrawalsById.Values.Where(item => item.businessId == period.businessId && item.amount.currencyId == period.currencyId && InRange(item.worldTime, period)).Sum(item => Math.Abs(item.amount.units));
            long net = operatingInflows + capitalInflows - operatingOutflows - payrollOutflows - withdrawals;
            return new BusinessCashFlowSummaryData
            {
                summaryId = summaryId,
                businessId = period.businessId,
                accountingPeriodId = period.accountingPeriodId,
                currencyId = period.currencyId,
                operatingInflows = BusinessModelHelpers.Money(period.currencyId, operatingInflows),
                operatingOutflows = BusinessModelHelpers.Money(period.currencyId, operatingOutflows),
                payrollOutflows = BusinessModelHelpers.Money(period.currencyId, payrollOutflows),
                capitalInflows = BusinessModelHelpers.Money(period.currencyId, capitalInflows),
                ownerWithdrawals = BusinessModelHelpers.Money(period.currencyId, withdrawals),
                assetPurchases = BusinessModelHelpers.Money(period.currencyId, 0L),
                financingFoundation = BusinessModelHelpers.Money(period.currencyId, 0L),
                netCashChange = BusinessModelHelpers.Money(period.currencyId, net),
                sourceTransactionIds = revenueById.Values.Select(item => item.transactionId).Concat(expensesById.Values.Select(item => item.transactionId)).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                diagnostics = $"OperatingIn={operatingInflows} OperatingOut={operatingOutflows} PayrollOut={payrollOutflows} CapitalIn={capitalInflows} Withdrawals={withdrawals}",
                creationWorldTime = worldTime,
                revision = 1L
            };
        }

        private static bool InRange(double worldTime, BusinessAccountingPeriodData period)
        {
            return worldTime + 0.0001d >= period.startWorldTime && worldTime <= period.endWorldTime + 0.0001d;
        }

        private static bool RangesOverlap(double startA, double endA, double startB, double endB)
        {
            return startA < endB && startB < endA;
        }

        private static bool CanTransition(BusinessState current, BusinessState target)
        {
            if (current == target)
            {
                return true;
            }

            if (IsTerminal(current))
            {
                return false;
            }

            return current switch
            {
                BusinessState.Planned => target is BusinessState.Forming or BusinessState.Active or BusinessState.Closed,
                BusinessState.Forming => target is BusinessState.Active or BusinessState.Suspended or BusinessState.Closed,
                BusinessState.Active => target is BusinessState.Suspended or BusinessState.Dormant or BusinessState.Closing or BusinessState.Closed,
                BusinessState.Suspended => target is BusinessState.Active or BusinessState.Closing or BusinessState.Closed,
                BusinessState.Dormant => target is BusinessState.Active or BusinessState.Closing or BusinessState.Closed,
                BusinessState.Closing => target is BusinessState.Closed,
                _ => false
            };
        }

        private static bool IsTerminal(BusinessState state)
        {
            return state is BusinessState.Closed or BusinessState.DissolvedFoundation or BusinessState.SeizedFoundation;
        }

        private static void ApplyBusinessState(BusinessInstanceData business, BusinessState targetState, double worldTime)
        {
            business.state = targetState;
            if (targetState == BusinessState.Suspended)
            {
                business.suspendedWorldTime = worldTime;
            }
            else if (targetState == BusinessState.Closed)
            {
                business.closedWorldTime = worldTime;
            }
        }

        private bool IsCurrentOwner(string businessId, string ownerSubjectId, double worldTime)
        {
            return ownershipById.Values.Any(item => item.businessId == businessId && item.owner.subjectId == ownerSubjectId && item.ActiveAt(worldTime));
        }

        private static bool IsExclusiveInventoryPurpose(BusinessInventoryPurpose purpose)
        {
            return purpose is BusinessInventoryPurpose.RetailStock or BusinessInventoryPurpose.ProductionInput or BusinessInventoryPurpose.WorkInProgress or BusinessInventoryPurpose.FinishedGoods or BusinessInventoryPurpose.ToolsAndEquipment;
        }

        private bool SourceAlreadyClassifiedAsRevenue(BusinessRevenueRecordData revenue)
        {
            return revenueById.Values.Any(item => item.businessId == revenue.businessId
                && ((HasSameNonEmpty(item.transactionId, revenue.transactionId) || HasSameNonEmpty(item.tradeRecordId, revenue.tradeRecordId))
                    || item.soldItemOrServiceIds.Intersect(revenue.soldItemOrServiceIds, StringComparer.Ordinal).Any()));
        }

        private bool SourceAlreadyClassifiedAsExpense(BusinessExpenseRecordData expense)
        {
            return expensesById.Values.Any(item => item.businessId == expense.businessId
                && (HasSameNonEmpty(item.transactionId, expense.transactionId)
                    || HasSameNonEmpty(item.payrollObligationId, expense.payrollObligationId)
                    || HasSameNonEmpty(item.payrollPaymentRecordId, expense.payrollPaymentRecordId)
                    || HasSameNonEmpty(item.productionJobId, expense.productionJobId)
                    || item.purchasedItemOrServiceIds.Intersect(expense.purchasedItemOrServiceIds, StringComparer.Ordinal).Any()));
        }

        private static bool HasSameNonEmpty(string first, string second)
        {
            return !string.IsNullOrWhiteSpace(first) && string.Equals(first, second, StringComparison.Ordinal);
        }

        private static bool SameBusiness(BusinessInstanceData first, BusinessInstanceData second)
        {
            return first.businessDefinitionId == second.businessDefinitionId && first.linkedOrganizationId == second.linkedOrganizationId && first.state == second.state;
        }

        private static bool SameOwnership(BusinessOwnershipRecordData first, BusinessOwnershipRecordData second)
        {
            return first.businessId == second.businessId
                && first.owner.StableKey == second.owner.StableKey
                && first.economicShare.numerator == second.economicShare.numerator
                && first.economicShare.denominator == second.economicShare.denominator
                && Math.Abs(first.effectiveStartWorldTime - second.effectiveStartWorldTime) < 0.0001d
                && Math.Abs(first.effectiveEndWorldTime - second.effectiveEndWorldTime) < 0.0001d;
        }

        private static string[] AddId(IEnumerable<string> ids, string id)
        {
            return BusinessModelHelpers.CleanIds((ids ?? Array.Empty<string>()).Concat(new[] { id }));
        }

        private static IEnumerable<T> Ordered<T>(IEnumerable<T> source, Func<T, string> key)
        {
            return (source ?? Array.Empty<T>()).OrderBy(key, StringComparer.Ordinal);
        }

        private static void AddAll<T>(IEnumerable<T> source, Func<T, string> key, Dictionary<string, T> target)
        {
            foreach (T item in source ?? Array.Empty<T>())
            {
                target.Add(key(item), item);
            }
        }

        private static bool ValidateChildReferences<T>(IEnumerable<T> records, Func<T, string> recordId, Func<T, string> businessId, HashSet<string> businessIds, string label, out string failure)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (T record in records ?? Array.Empty<T>())
            {
                string id = recordId(record);
                string parent = businessId(record);
                if (string.IsNullOrWhiteSpace(id) || !ids.Add(id) || !businessIds.Contains(parent ?? string.Empty))
                {
                    failure = $"Business save data contains invalid {label} record '{id}'.";
                    return false;
                }
            }

            failure = string.Empty;
            return true;
        }
    }
}
