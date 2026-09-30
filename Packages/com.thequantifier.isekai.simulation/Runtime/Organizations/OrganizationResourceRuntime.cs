using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Economy;
using UnityIsekaiGame.Economy.Businesses;
using UnityIsekaiGame.Economy.Payroll;
using UnityIsekaiGame.Economy.Properties;
using UnityIsekaiGame.Contracts;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Progression;

namespace UnityIsekaiGame.Organizations
{
    public sealed class OrganizationResourceRuntime : IDisposable
    {
        private readonly Dictionary<string, OrganizationTreasuryRecordData> treasuriesById = new Dictionary<string, OrganizationTreasuryRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, OrganizationAccountRecordData> accountsById = new Dictionary<string, OrganizationAccountRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, OrganizationFundRestrictionRecordData> restrictionsById = new Dictionary<string, OrganizationFundRestrictionRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, OrganizationBudgetRecordData> budgetsById = new Dictionary<string, OrganizationBudgetRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, OrganizationResourceReservationRecordData> reservationsById = new Dictionary<string, OrganizationResourceReservationRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, OrganizationInventoryAssociationRecordData> inventoryAssociationsById = new Dictionary<string, OrganizationInventoryAssociationRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, OrganizationPropertyAssociationRecordData> propertyAssociationsById = new Dictionary<string, OrganizationPropertyAssociationRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, OrganizationBusinessAssociationRecordData> businessAssociationsById = new Dictionary<string, OrganizationBusinessAssociationRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, OrganizationAssetCustodyRecordData> custodyById = new Dictionary<string, OrganizationAssetCustodyRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, OrganizationRevenueRoutingRuleData> routingById = new Dictionary<string, OrganizationRevenueRoutingRuleData>(StringComparer.Ordinal);
        private readonly Dictionary<string, OrganizationResourceTransactionRecordData> transactionsById = new Dictionary<string, OrganizationResourceTransactionRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, OrganizationDissolutionResourcePlanData> dissolutionPlansById = new Dictionary<string, OrganizationDissolutionResourcePlanData>(StringComparer.Ordinal);
        private readonly List<string> eventDeliveryDiagnostics = new List<string>();

        private DefinitionRegistry registry;
        private OrganizationRuntime organizations;
        private OrganizationAuthorityRuntime authority;
        private EconomyRuntime economy;
        private PropertyRuntime properties;
        private BusinessRuntime businesses;
        private ItemInstanceIdentityRuntime items;
        private ContractEconomyRuntime contracts;
        private PayrollRuntime payroll;
        private string worldId = string.Empty;
        private bool disposed;

        public long Revision { get; private set; }
        public bool IsDirty { get; private set; }
        public bool IsReady => !disposed && registry != null && organizations != null && authority != null && economy != null && !string.IsNullOrWhiteSpace(worldId);
        public int TreasuryCount => treasuriesById.Count;
        public int AccountCount => accountsById.Count;
        public int RestrictionCount => restrictionsById.Count;
        public int BudgetCount => budgetsById.Count;
        public int ReservationCount => reservationsById.Count;

        public IReadOnlyList<OrganizationTreasuryRecordData> Treasuries => Ordered(treasuriesById.Values, item => item.creationWorldTime, item => item.treasuryId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<OrganizationAccountRecordData> Accounts => Ordered(accountsById.Values, item => item.creationWorldTime, item => item.accountId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<OrganizationFundRestrictionRecordData> Restrictions => Ordered(restrictionsById.Values, item => item.startWorldTime, item => item.restrictionId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<OrganizationBudgetRecordData> Budgets => Ordered(budgetsById.Values, item => item.startWorldTime, item => item.budgetId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<OrganizationResourceReservationRecordData> Reservations => reservationsById.Values.OrderByDescending(item => item.priority).ThenBy(item => item.startWorldTime).ThenBy(item => item.reservationId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<OrganizationInventoryAssociationRecordData> InventoryAssociations => Ordered(inventoryAssociationsById.Values, item => item.startWorldTime, item => item.associationId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<OrganizationPropertyAssociationRecordData> PropertyAssociations => Ordered(propertyAssociationsById.Values, item => item.startWorldTime, item => item.associationId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<OrganizationBusinessAssociationRecordData> BusinessAssociations => Ordered(businessAssociationsById.Values, item => item.startWorldTime, item => item.associationId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<OrganizationAssetCustodyRecordData> CustodyRecords => Ordered(custodyById.Values, item => item.startWorldTime, item => item.custodyId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<OrganizationRevenueRoutingRuleData> RevenueRoutingRules => routingById.Values.OrderByDescending(item => item.priority).ThenBy(item => item.startWorldTime).ThenBy(item => item.routingRuleId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<OrganizationDissolutionResourcePlanData> DissolutionPlans => Ordered(dissolutionPlansById.Values, item => item.createdWorldTime, item => item.planId).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<string> EventDeliveryDiagnostics => eventDeliveryDiagnostics.ToArray();
        public event Action<OrganizationResourceCommittedEvent> OperationCommitted;

        public void Configure(DefinitionRegistry definitionRegistry, OrganizationRuntime organizationRuntime, OrganizationAuthorityRuntime authorityRuntime, EconomyRuntime economyRuntime, string world, PropertyRuntime propertyRuntime = null, BusinessRuntime businessRuntime = null, ItemInstanceIdentityRuntime itemRuntime = null, ContractEconomyRuntime contractRuntime = null, PayrollRuntime payrollRuntime = null)
        {
            registry = definitionRegistry ?? registry;
            organizations = organizationRuntime ?? organizations;
            authority = authorityRuntime ?? authority;
            economy = economyRuntime ?? economy;
            properties = propertyRuntime ?? properties;
            businesses = businessRuntime ?? businesses;
            items = itemRuntime ?? items;
            contracts = contractRuntime ?? contracts;
            payroll = payrollRuntime ?? payroll;
            worldId = string.IsNullOrWhiteSpace(world) ? worldId : world.Trim();
            disposed = false;
        }

        public OrganizationResourceOperationResult CreateTreasury(OrganizationTreasuryRequest request)
        {
            request ??= new OrganizationTreasuryRequest();
            long before = Revision;
            if (!CanMutate(out OrganizationResourceOperationResult dependencyFailure)) return dependencyFailure;
            if (!ValidateRequired(request.transactionId, request.treasuryId, request.organizationId, out string failure)) return Fail(OrganizationResourceOperationCode.InvalidRequest, failure, request.preview);
            if (!TryOrganization(request.organizationId, request.worldTime, ordinaryOperation: true, out _, out failure)) return Fail(OrganizationResourceOperationCode.MissingOrganization, failure, request.preview);
            if (!registry.TryGet(request.resourceTypeDefinitionId, out OrganizationResourceTypeDefinition resourceType) || resourceType.Category != OrganizationResourceCategory.Currency) return Fail(OrganizationResourceOperationCode.ValidationFailed, $"Treasury resource type '{request.resourceTypeDefinitionId}' is missing or is not Currency.", request.preview);
            if (treasuriesById.TryGetValue(request.treasuryId, out OrganizationTreasuryRecordData existing)) return DuplicateOrConflict(request.transactionId, "create-treasury", request.treasuryId, existing.officialName == (request.officialName ?? string.Empty), before, request.preview, treasury: existing);

            OrganizationAuthorizationResult authorization = Authorize(request.transactionId, request.actorPersonId, request.organizationId, Action(request.actionDefinitionId, PrototypeOrganizationAuthorityDefinitionFactory.CreateTreasuryActionId), request.treasuryId, request.approvalPersonIds, request.worldTime, preview: true, consume: false);
            if (!authorization.Succeeded) return Unauthorized(authorization, request.preview);
            OrganizationTreasuryRecordData record = new OrganizationTreasuryRecordData
            {
                treasuryId = request.treasuryId.Trim(), organizationId = request.organizationId.Trim(), resourceTypeDefinitionId = request.resourceTypeDefinitionId.Trim(),
                officialName = request.officialName ?? string.Empty, category = request.category == OrganizationTreasuryCategory.Unknown ? OrganizationTreasuryCategory.GeneralTreasury : request.category,
                lifecycleState = OrganizationTreasuryLifecycleState.Active, creationWorldTime = request.worldTime, branchOrganizationId = request.branchOrganizationId ?? string.Empty,
                visibility = request.visibility, provenanceId = request.provenanceId ?? string.Empty
            };
            if (request.preview) return OrganizationResourceOperationResult.Success("Treasury creation preview succeeded.", before, before, preview: true, authorization: authorization, treasury: record, subjectId: record.treasuryId);
            authorization = Authorize(request.transactionId, request.actorPersonId, request.organizationId, Action(request.actionDefinitionId, PrototypeOrganizationAuthorityDefinitionFactory.CreateTreasuryActionId), request.treasuryId, request.approvalPersonIds, request.worldTime, preview: false, consume: true);
            if (!authorization.Succeeded) return Unauthorized(authorization, false);
            treasuriesById.Add(record.treasuryId, record);
            Commit(request.transactionId, "create-treasury", record.treasuryId, string.Empty, request.organizationId, worldTime: request.worldTime);
            return OrganizationResourceOperationResult.Success("Treasury created.", before, Revision, authorization: authorization, treasury: record, subjectId: record.treasuryId);
        }

        public OrganizationResourceOperationResult CreateAccount(OrganizationAccountRequest request)
        {
            request ??= new OrganizationAccountRequest();
            long before = Revision;
            if (!CanMutate(out OrganizationResourceOperationResult dependencyFailure)) return dependencyFailure;
            if (!ValidateRequired(request.transactionId, request.accountId, request.treasuryId, out string failure) || string.IsNullOrWhiteSpace(request.organizationId) || string.IsNullOrWhiteSpace(request.currencyDefinitionId)) return Fail(OrganizationResourceOperationCode.InvalidRequest, string.IsNullOrWhiteSpace(failure) ? "Organization and currency IDs are required." : failure, request.preview);
            if (!treasuriesById.TryGetValue(request.treasuryId, out OrganizationTreasuryRecordData treasury) || treasury.organizationId != request.organizationId) return Fail(OrganizationResourceOperationCode.MissingTreasury, $"Treasury '{request.treasuryId}' does not belong to Organization '{request.organizationId}'.", request.preview);
            if (treasury.lifecycleState != OrganizationTreasuryLifecycleState.Active) return Fail(OrganizationResourceOperationCode.InvalidLifecycle, $"Treasury '{treasury.treasuryId}' is {treasury.lifecycleState}.", request.preview);
            if (!TryOrganization(request.organizationId, request.worldTime, true, out _, out failure)) return Fail(OrganizationResourceOperationCode.MissingOrganization, failure, request.preview);
            if (!registry.TryGet(request.currencyDefinitionId, out CurrencyDefinition currency)) return Fail(OrganizationResourceOperationCode.MissingCurrency, $"Currency '{request.currencyDefinitionId}' is not active.", request.preview);
            if (request.openingBalanceUnits < 0L) return Fail(OrganizationResourceOperationCode.InvalidRequest, "Opening balance cannot be negative.", request.preview);
            if (accountsById.TryGetValue(request.accountId, out OrganizationAccountRecordData existing)) return DuplicateOrConflict(request.transactionId, "create-account", request.accountId, existing.treasuryId == request.treasuryId && existing.currencyDefinitionId == request.currencyDefinitionId, before, request.preview, account: existing);
            string economyAccountId = string.IsNullOrWhiteSpace(request.economyAccountId) ? $"economy.organization.{request.organizationId}.{request.accountId}" : request.economyAccountId.Trim();
            bool adoptEconomyAccount = economy.TryGetAccount(economyAccountId, out EconomyAccountSnapshot existingEconomyAccount);
            if (adoptEconomyAccount && (!string.Equals(existingEconomyAccount.OwnerId, request.organizationId, StringComparison.Ordinal) || !string.Equals(existingEconomyAccount.CurrencyId, request.currencyDefinitionId, StringComparison.Ordinal))) return Fail(OrganizationResourceOperationCode.ValidationFailed, $"Economy account '{economyAccountId}' belongs to another owner or currency and cannot be adopted.", request.preview);
            if (adoptEconomyAccount && request.openingBalanceUnits != 0L) return Fail(OrganizationResourceOperationCode.ValidationFailed, "An existing economy account can only be adopted with a zero opening balance.", request.preview);
            string action = Action(request.actionDefinitionId, PrototypeOrganizationAuthorityDefinitionFactory.CreateTreasuryAccountActionId);
            OrganizationAuthorizationResult authorization = Authorize(request.transactionId, request.actorPersonId, request.organizationId, action, request.accountId, request.approvalPersonIds, request.worldTime, true, false);
            if (!authorization.Succeeded) return Unauthorized(authorization, request.preview);
            EconomyOperationResult economyPreview = adoptEconomyAccount ? null : economy.CreateAccount(economyAccountId, currency, request.organizationId, EconomyAccountKind.OrganizationAccount, request.openingBalanceUnits, $"{request.transactionId}.opening", preview: true);
            if (!adoptEconomyAccount && !economyPreview.Succeeded) return FinancialFailure(economyPreview, request.preview, authorization);
            OrganizationAccountRecordData record = new OrganizationAccountRecordData
            {
                accountId = request.accountId.Trim(), treasuryId = request.treasuryId.Trim(), organizationId = request.organizationId.Trim(), economyAccountId = economyAccountId,
                officialName = request.officialName ?? string.Empty, category = request.category == OrganizationAccountCategory.Unknown ? OrganizationAccountCategory.GeneralOperating : request.category,
                lifecycleState = OrganizationAccountLifecycleState.Active, currencyDefinitionId = request.currencyDefinitionId.Trim(), branchOrganizationId = request.branchOrganizationId ?? string.Empty,
                projectReferenceId = request.projectReferenceId ?? string.Empty, creationWorldTime = request.worldTime, visibility = request.visibility, provenanceId = request.provenanceId ?? string.Empty
            };
            if (request.preview) return OrganizationResourceOperationResult.Success("Account creation preview succeeded.", before, before, preview: true, authorization: authorization, account: record, subjectId: record.accountId);
            EconomyRuntimeSaveData economyRollback = economy.CreateSaveData();
            OrganizationAuthorityRuntimeSaveData authorityRollback = authority.CreateSaveData();
            authorization = Authorize(request.transactionId, request.actorPersonId, request.organizationId, action, request.accountId, request.approvalPersonIds, request.worldTime, false, true);
            if (!authorization.Succeeded) return Unauthorized(authorization, false);
            EconomyOperationResult economyResult = adoptEconomyAccount ? null : economy.CreateAccount(economyAccountId, currency, request.organizationId, EconomyAccountKind.OrganizationAccount, request.openingBalanceUnits, $"{request.transactionId}.opening", preview: false);
            if (!adoptEconomyAccount && !economyResult.Succeeded)
            {
                RestoreDependencies(economyRollback, authorityRollback);
                return FinancialFailure(economyResult, false, authorization);
            }
            accountsById.Add(record.accountId, record);
            Commit(request.transactionId, adoptEconomyAccount ? "adopt-account" : "create-account", record.accountId, economyResult?.Transaction?.TransactionId, request.organizationId, destinationAccountId: record.accountId, units: request.openingBalanceUnits, currencyId: request.currencyDefinitionId, worldTime: request.worldTime);
            return OrganizationResourceOperationResult.Success(adoptEconomyAccount ? "Existing economy account adopted by the organization treasury." : "Organization account created.", before, Revision, authorization: authorization, account: record, destinationBalance: GetBalance(record.accountId, request.worldTime), transaction: economyResult?.Transaction, subjectId: record.accountId);
        }

        public OrganizationResourceOperationResult ChangeAccountLifecycle(OrganizationAccountLifecycleRequest request)
        {
            request ??= new OrganizationAccountLifecycleRequest();
            long before = Revision;
            if (!CanMutate(out OrganizationResourceOperationResult dependencyFailure)) return dependencyFailure;
            if (!accountsById.TryGetValue(request.accountId ?? string.Empty, out OrganizationAccountRecordData account)) return Fail(OrganizationResourceOperationCode.MissingAccount, $"Account '{request.accountId}' was not found.", request.preview);
            if (account.lifecycleState == request.targetState) return DuplicateOrConflict(request.transactionId, "account-lifecycle", request.accountId, true, before, request.preview, account: account);
            if (!ValidAccountTransition(account.lifecycleState, request.targetState)) return Fail(OrganizationResourceOperationCode.InvalidLifecycle, $"Account cannot transition from {account.lifecycleState} to {request.targetState}.", request.preview);
            EconomyAccountState economyState = request.targetState == OrganizationAccountLifecycleState.Active ? EconomyAccountState.Active : request.targetState == OrganizationAccountLifecycleState.Frozen ? EconomyAccountState.Frozen : EconomyAccountState.Closed;
            string action = Action(request.actionDefinitionId, PrototypeOrganizationAuthorityDefinitionFactory.ManageTreasuryAccountActionId);
            OrganizationAuthorizationResult authorization = Authorize(request.transactionId, request.actorPersonId, account.organizationId, action, account.accountId, request.approvalPersonIds, request.worldTime, true, false);
            if (!authorization.Succeeded) return Unauthorized(authorization, request.preview);
            if (request.targetState == OrganizationAccountLifecycleState.Closed && (GetBalance(account.accountId, request.worldTime)?.BalanceUnits ?? 0L) != 0L) return Fail(OrganizationResourceOperationCode.FinanciallyInvalid, "An account must have a zero balance before closure.", request.preview, authorization);
            EconomyOperationResult economyPreview = economy.ChangeAccountState(request.transactionId, account.economyAccountId, economyState, preview: true);
            if (!economyPreview.Succeeded) return FinancialFailure(economyPreview, request.preview, authorization);
            if (request.preview)
            {
                OrganizationAccountRecordData projected = account.Clone(); projected.lifecycleState = request.targetState;
                return OrganizationResourceOperationResult.Success("Account lifecycle preview succeeded.", before, before, preview: true, authorization: authorization, account: projected, subjectId: projected.accountId);
            }
            EconomyRuntimeSaveData economyRollback = economy.CreateSaveData(); OrganizationAuthorityRuntimeSaveData authorityRollback = authority.CreateSaveData();
            authorization = Authorize(request.transactionId, request.actorPersonId, account.organizationId, action, account.accountId, request.approvalPersonIds, request.worldTime, false, true);
            EconomyOperationResult economyResult = authorization.Succeeded ? economy.ChangeAccountState(request.transactionId, account.economyAccountId, economyState) : null;
            if (!authorization.Succeeded || economyResult == null || !economyResult.Succeeded)
            {
                RestoreDependencies(economyRollback, authorityRollback);
                return !authorization.Succeeded ? Unauthorized(authorization, false) : FinancialFailure(economyResult, false, authorization);
            }
            account.lifecycleState = request.targetState; account.closingWorldTime = request.targetState == OrganizationAccountLifecycleState.Closed ? request.worldTime : -1d; account.revision++;
            Commit(request.transactionId, "account-lifecycle", account.accountId, string.Empty, account.organizationId, worldTime: request.worldTime);
            return OrganizationResourceOperationResult.Success("Account lifecycle changed.", before, Revision, authorization: authorization, account: account, subjectId: account.accountId);
        }

        public OrganizationResourceOperationResult AddFundRestriction(OrganizationFundRestrictionRequest request)
        {
            request ??= new OrganizationFundRestrictionRequest(); long before = Revision;
            if (!CanMutate(out OrganizationResourceOperationResult dependencyFailure)) return dependencyFailure;
            if (!accountsById.TryGetValue(request.accountId ?? string.Empty, out OrganizationAccountRecordData account) || account.organizationId != request.organizationId) return Fail(OrganizationResourceOperationCode.MissingAccount, "The organization account was not found.", request.preview);
            if (string.IsNullOrWhiteSpace(request.transactionId) || string.IsNullOrWhiteSpace(request.restrictionId) || request.units <= 0L) return Fail(OrganizationResourceOperationCode.InvalidRequest, "Transaction ID, restriction ID, and a positive amount are required.", request.preview);
            if (request.currencyDefinitionId != account.currencyDefinitionId || request.endWorldTime >= 0d && request.endWorldTime <= request.startWorldTime) return Fail(OrganizationResourceOperationCode.ValidationFailed, "Restriction currency or time range is invalid.", request.preview);
            if (restrictionsById.TryGetValue(request.restrictionId, out OrganizationFundRestrictionRecordData existing)) return DuplicateOrConflict(request.transactionId, "add-restriction", request.restrictionId, existing.accountId == request.accountId && existing.originalUnits == request.units, before, request.preview);
            long alreadyRestricted = ActiveRestrictions(account.accountId, request.startWorldTime).Sum(item => item.remainingUnits);
            if ((GetBalance(account.accountId, request.startWorldTime)?.BalanceUnits ?? 0L) - alreadyRestricted < request.units) return Fail(OrganizationResourceOperationCode.InsufficientFunds, "The account does not contain enough unrestricted funds for this restriction.", request.preview);
            OrganizationAuthorizationResult authorization = Authorize(request.transactionId, request.actorPersonId, request.organizationId, Action(request.actionDefinitionId, PrototypeOrganizationAuthorityDefinitionFactory.ManageRestrictedFundsActionId), request.restrictionId, Array.Empty<string>(), request.startWorldTime, true, false);
            if (!authorization.Succeeded) return Unauthorized(authorization, request.preview);
            OrganizationFundRestrictionRecordData record = new OrganizationFundRestrictionRecordData
            {
                restrictionId = request.restrictionId.Trim(), organizationId = request.organizationId.Trim(), accountId = request.accountId.Trim(), currencyDefinitionId = request.currencyDefinitionId.Trim(),
                originalUnits = request.units, remainingUnits = request.units, allowedPurpose = request.allowedPurpose ?? string.Empty, allowedActionDefinitionId = request.allowedActionDefinitionId ?? string.Empty,
                allowedRecipientIds = OrganizationModelUtility.Clean(request.allowedRecipientIds), startWorldTime = request.startWorldTime, endWorldTime = request.endWorldTime,
                sourceReferenceId = request.sourceReferenceId ?? string.Empty, visibility = request.visibility, provenanceId = request.provenanceId ?? string.Empty
            };
            if (request.preview) return OrganizationResourceOperationResult.Success("Restriction preview succeeded.", before, before, preview: true, authorization: authorization, subjectId: record.restrictionId);
            authorization = Authorize(request.transactionId, request.actorPersonId, request.organizationId, Action(request.actionDefinitionId, PrototypeOrganizationAuthorityDefinitionFactory.ManageRestrictedFundsActionId), request.restrictionId, Array.Empty<string>(), request.startWorldTime, false, true);
            if (!authorization.Succeeded) return Unauthorized(authorization, false);
            restrictionsById.Add(record.restrictionId, record);
            Commit(request.transactionId, "add-restriction", record.restrictionId, string.Empty, request.organizationId, restrictionId: record.restrictionId, units: record.originalUnits, currencyId: record.currencyDefinitionId, worldTime: request.startWorldTime);
            return OrganizationResourceOperationResult.Success("Fund restriction created.", before, Revision, authorization: authorization, subjectId: record.restrictionId);
        }

        public OrganizationResourceOperationResult CreateBudget(OrganizationBudgetRequest request)
        {
            request ??= new OrganizationBudgetRequest(); long before = Revision;
            if (!CanMutate(out OrganizationResourceOperationResult dependencyFailure)) return dependencyFailure;
            if (string.IsNullOrWhiteSpace(request.transactionId) || string.IsNullOrWhiteSpace(request.budgetId) || request.authorizedUnits < 0L) return Fail(OrganizationResourceOperationCode.InvalidRequest, "Transaction ID, budget ID, and a nonnegative authorization are required.", request.preview);
            if (!accountsById.TryGetValue(request.accountId ?? string.Empty, out OrganizationAccountRecordData account) || account.organizationId != request.organizationId || account.treasuryId != request.treasuryId) return Fail(OrganizationResourceOperationCode.MissingAccount, "Budget account scope is invalid.", request.preview);
            if (account.currencyDefinitionId != request.currencyDefinitionId || request.endWorldTime >= 0d && request.endWorldTime <= request.startWorldTime) return Fail(OrganizationResourceOperationCode.ValidationFailed, "Budget currency or period is invalid.", request.preview);
            if (budgetsById.TryGetValue(request.budgetId, out OrganizationBudgetRecordData existing)) return DuplicateOrConflict(request.transactionId, "create-budget", request.budgetId, existing.accountId == request.accountId && existing.authorizedUnits == request.authorizedUnits, before, request.preview);
            OrganizationAuthorizationResult authorization = Authorize(request.transactionId, request.actorPersonId, request.organizationId, Action(request.actionDefinitionId, PrototypeOrganizationAuthorityDefinitionFactory.ManageOrganizationBudgetActionId), request.budgetId, Array.Empty<string>(), request.startWorldTime, true, false);
            if (!authorization.Succeeded) return Unauthorized(authorization, request.preview);
            OrganizationBudgetRecordData record = new OrganizationBudgetRecordData
            {
                budgetId = request.budgetId.Trim(), organizationId = request.organizationId.Trim(), treasuryId = request.treasuryId.Trim(), accountId = request.accountId.Trim(), category = request.category,
                enforcementPolicy = request.enforcementPolicy, currencyDefinitionId = request.currencyDefinitionId.Trim(), authorizedUnits = request.authorizedUnits, purpose = request.purpose ?? string.Empty,
                fundingSourceId = request.fundingSourceId ?? string.Empty, startWorldTime = request.startWorldTime, endWorldTime = request.endWorldTime, sourceAuthorityId = request.sourceAuthorityId ?? string.Empty,
                visibility = request.visibility, provenanceId = request.provenanceId ?? string.Empty
            };
            if (request.preview) return OrganizationResourceOperationResult.Success("Budget preview succeeded.", before, before, preview: true, authorization: authorization, subjectId: record.budgetId);
            authorization = Authorize(request.transactionId, request.actorPersonId, request.organizationId, Action(request.actionDefinitionId, PrototypeOrganizationAuthorityDefinitionFactory.ManageOrganizationBudgetActionId), request.budgetId, Array.Empty<string>(), request.startWorldTime, false, true);
            if (!authorization.Succeeded) return Unauthorized(authorization, false);
            budgetsById.Add(record.budgetId, record);
            Commit(request.transactionId, "create-budget", record.budgetId, string.Empty, request.organizationId, budgetId: record.budgetId, units: record.authorizedUnits, currencyId: record.currencyDefinitionId, worldTime: request.startWorldTime);
            return OrganizationResourceOperationResult.Success("Budget record created.", before, Revision, authorization: authorization, subjectId: record.budgetId);
        }

        public OrganizationResourceOperationResult ReserveResource(OrganizationReservationRequest request)
        {
            request ??= new OrganizationReservationRequest(); long before = Revision;
            if (!CanMutate(out OrganizationResourceOperationResult dependencyFailure)) return dependencyFailure;
            if (string.IsNullOrWhiteSpace(request.transactionId) || string.IsNullOrWhiteSpace(request.reservationId) || string.IsNullOrWhiteSpace(request.organizationId)) return Fail(OrganizationResourceOperationCode.InvalidRequest, "Transaction, reservation, and Organization IDs are required.", request.preview);
            if (request.expirationWorldTime >= 0d && request.expirationWorldTime <= request.startWorldTime) return Fail(OrganizationResourceOperationCode.ValidationFailed, "Reservation expiration must be after its start.", request.preview);
            if (reservationsById.TryGetValue(request.reservationId, out OrganizationResourceReservationRecordData existing)) return DuplicateOrConflict(request.transactionId, "reserve-resource", request.reservationId, existing.organizationId == request.organizationId && existing.amountUnits == request.amountUnits && existing.quantity == request.quantity, before, request.preview);
            bool currencyReservation = !string.IsNullOrWhiteSpace(request.accountId);
            OrganizationAccountRecordData account = null;
            EconomyOperationResult economyPreview = null;
            if (currencyReservation)
            {
                if (!accountsById.TryGetValue(request.accountId, out account) || account.organizationId != request.organizationId) return Fail(OrganizationResourceOperationCode.MissingAccount, "Reservation account is missing or owned by another Organization.", request.preview);
                if (account.lifecycleState != OrganizationAccountLifecycleState.Active) return Fail(account.lifecycleState == OrganizationAccountLifecycleState.Frozen ? OrganizationResourceOperationCode.AccountFrozen : OrganizationResourceOperationCode.AccountClosed, $"Account '{account.accountId}' is {account.lifecycleState}.", request.preview);
                if (request.amountUnits <= 0L || request.currencyDefinitionId != account.currencyDefinitionId) return Fail(OrganizationResourceOperationCode.InvalidRequest, "A positive amount in the account currency is required.", request.preview);
                economyPreview = economy.Reserve(request.reservationId, account.economyAccountId, new MoneyAmount(account.currencyDefinitionId, request.amountUnits), request.requestingOperationId, request.startWorldTime, request.expirationWorldTime, preview: true);
                if (!economyPreview.Succeeded) return FinancialFailure(economyPreview, request.preview, null);
            }
            else
            {
                if (request.resource == null || request.resource.kind == OrganizationAssetReferenceKind.Unknown || string.IsNullOrWhiteSpace(request.resource.resourceId) || request.quantity <= 0) return Fail(OrganizationResourceOperationCode.InvalidRequest, "A typed resource and positive quantity are required for a non-currency reservation.", request.preview);
                if (!ValidAssetReference(request.resource, out string assetFailure)) return Fail(OrganizationResourceOperationCode.MissingResource, assetFailure, request.preview);
            }
            OrganizationAuthorizationResult authorization = Authorize(request.transactionId, request.actorPersonId, request.organizationId, Action(request.actionDefinitionId, PrototypeOrganizationAuthorityDefinitionFactory.ManageResourceReservationActionId), request.reservationId, Array.Empty<string>(), request.startWorldTime, true, false);
            if (!authorization.Succeeded) return Unauthorized(authorization, request.preview);
            OrganizationResourceReservationRecordData record = new OrganizationResourceReservationRecordData
            {
                reservationId = request.reservationId.Trim(), organizationId = request.organizationId.Trim(), resource = currencyReservation ? new OrganizationAssetReferenceData { kind = OrganizationAssetReferenceKind.CurrencyBalance, resourceId = request.accountId, definitionId = request.currencyDefinitionId, worldId = worldId } : request.resource.Clone(),
                accountId = request.accountId ?? string.Empty, inventoryId = request.inventoryId ?? string.Empty, economyReservationId = currencyReservation ? request.reservationId.Trim() : string.Empty,
                currencyDefinitionId = request.currencyDefinitionId ?? string.Empty, amountUnits = request.amountUnits, quantity = request.quantity, category = request.category,
                purpose = request.purpose ?? string.Empty, requestingOperationId = request.requestingOperationId ?? string.Empty, priority = request.priority,
                startWorldTime = request.startWorldTime, expirationWorldTime = request.expirationWorldTime, visibility = request.visibility, provenanceId = request.provenanceId ?? string.Empty
            };
            if (request.preview) return OrganizationResourceOperationResult.Success("Resource reservation preview succeeded.", before, before, preview: true, authorization: authorization, sourceBalance: account == null ? null : GetBalance(account.accountId, request.startWorldTime), subjectId: record.reservationId);
            EconomyRuntimeSaveData economyRollback = economy.CreateSaveData(); OrganizationAuthorityRuntimeSaveData authorityRollback = authority.CreateSaveData();
            authorization = Authorize(request.transactionId, request.actorPersonId, request.organizationId, Action(request.actionDefinitionId, PrototypeOrganizationAuthorityDefinitionFactory.ManageResourceReservationActionId), request.reservationId, Array.Empty<string>(), request.startWorldTime, false, true);
            if (!authorization.Succeeded) return Unauthorized(authorization, false);
            if (currencyReservation)
            {
                EconomyOperationResult economyResult = economy.Reserve(request.reservationId, account.economyAccountId, new MoneyAmount(account.currencyDefinitionId, request.amountUnits), request.requestingOperationId, request.startWorldTime, request.expirationWorldTime);
                if (!economyResult.Succeeded) { RestoreDependencies(economyRollback, authorityRollback); return FinancialFailure(economyResult, false, authorization); }
            }
            reservationsById.Add(record.reservationId, record);
            Commit(request.transactionId, "reserve-resource", record.reservationId, string.Empty, request.organizationId, sourceAccountId: record.accountId, units: record.amountUnits, currencyId: record.currencyDefinitionId, purpose: record.purpose, worldTime: record.startWorldTime);
            return OrganizationResourceOperationResult.Success("Resource reserved.", before, Revision, authorization: authorization, sourceBalance: account == null ? null : GetBalance(account.accountId, request.startWorldTime), subjectId: record.reservationId);
        }

        public OrganizationResourceOperationResult ReleaseReservation(string reservationId, string transactionId, string actorPersonId, string actionDefinitionId, double worldTime, bool preview = false)
        {
            long before = Revision;
            if (!CanMutate(out OrganizationResourceOperationResult dependencyFailure)) return dependencyFailure;
            if (!reservationsById.TryGetValue(reservationId ?? string.Empty, out OrganizationResourceReservationRecordData record)) return Fail(OrganizationResourceOperationCode.MissingReservation, $"Reservation '{reservationId}' was not found.", preview);
            if (record.lifecycleState != OrganizationReservationLifecycleState.Active) return Fail(OrganizationResourceOperationCode.ReservationUnavailable, $"Reservation '{reservationId}' is {record.lifecycleState}.", preview);
            OrganizationAuthorizationResult authorization = Authorize(transactionId, actorPersonId, record.organizationId, Action(actionDefinitionId, PrototypeOrganizationAuthorityDefinitionFactory.ManageResourceReservationActionId), record.reservationId, Array.Empty<string>(), worldTime, true, false);
            if (!authorization.Succeeded) return Unauthorized(authorization, preview);
            EconomyOperationResult economyPreview = string.IsNullOrWhiteSpace(record.economyReservationId) ? null : economy.ReleaseReservation(record.economyReservationId, transactionId, preview: true);
            if (economyPreview != null && !economyPreview.Succeeded) return FinancialFailure(economyPreview, preview, authorization);
            if (preview) return OrganizationResourceOperationResult.Success("Reservation release preview succeeded.", before, before, preview: true, authorization: authorization, subjectId: record.reservationId);
            EconomyRuntimeSaveData economyRollback = economy.CreateSaveData(); OrganizationAuthorityRuntimeSaveData authorityRollback = authority.CreateSaveData();
            authorization = Authorize(transactionId, actorPersonId, record.organizationId, Action(actionDefinitionId, PrototypeOrganizationAuthorityDefinitionFactory.ManageResourceReservationActionId), record.reservationId, Array.Empty<string>(), worldTime, false, true);
            if (!authorization.Succeeded) return Unauthorized(authorization, false);
            if (!string.IsNullOrWhiteSpace(record.economyReservationId))
            {
                EconomyOperationResult released = economy.ReleaseReservation(record.economyReservationId, transactionId);
                if (!released.Succeeded) { RestoreDependencies(economyRollback, authorityRollback); return FinancialFailure(released, false, authorization); }
            }
            record.lifecycleState = OrganizationReservationLifecycleState.Released; record.revision++;
            Commit(transactionId, "release-reservation", record.reservationId, string.Empty, record.organizationId, sourceAccountId: record.accountId, units: record.amountUnits, currencyId: record.currencyDefinitionId, worldTime: worldTime);
            return OrganizationResourceOperationResult.Success("Reservation released.", before, Revision, authorization: authorization, sourceBalance: string.IsNullOrWhiteSpace(record.accountId) ? null : GetBalance(record.accountId, worldTime), subjectId: record.reservationId);
        }

        public OrganizationResourceOperationResult DepositFunds(OrganizationFinancialTransactionRequest request)
        {
            request ??= new OrganizationFinancialTransactionRequest();
            if (string.IsNullOrWhiteSpace(request.destinationAccountId)) return Fail(OrganizationResourceOperationCode.InvalidRequest, "A destination organization account is required.", request.preview);
            if (string.IsNullOrWhiteSpace(request.relatedRecordId)) return Fail(OrganizationResourceOperationCode.InvalidRequest, "Deposits that create abstract value require an explicit external source record.", request.preview);
            return ExecuteFinancial(request, issue: true, destroy: false);
        }

        public OrganizationResourceOperationResult WithdrawFunds(OrganizationFinancialTransactionRequest request)
        {
            request ??= new OrganizationFinancialTransactionRequest();
            if (string.IsNullOrWhiteSpace(request.sourceAccountId)) return Fail(OrganizationResourceOperationCode.InvalidRequest, "A source organization account is required.", request.preview);
            if (string.IsNullOrWhiteSpace(request.relatedRecordId)) return Fail(OrganizationResourceOperationCode.InvalidRequest, "Withdrawals that remove abstract value require an explicit destination or settlement record.", request.preview);
            return ExecuteFinancial(request, issue: false, destroy: true);
        }

        public OrganizationResourceOperationResult TransferFunds(OrganizationFinancialTransactionRequest request)
        {
            request ??= new OrganizationFinancialTransactionRequest();
            if (string.IsNullOrWhiteSpace(request.sourceAccountId) || string.IsNullOrWhiteSpace(request.destinationAccountId)) return Fail(OrganizationResourceOperationCode.InvalidRequest, "Source and destination organization accounts are required.", request.preview);
            return ExecuteFinancial(request, issue: false, destroy: false);
        }

        private OrganizationResourceOperationResult ExecuteFinancial(OrganizationFinancialTransactionRequest request, bool issue, bool destroy)
        {
            long before = Revision;
            if (!CanMutate(out OrganizationResourceOperationResult dependencyFailure)) return dependencyFailure;
            if (string.IsNullOrWhiteSpace(request.transactionId) || string.IsNullOrWhiteSpace(request.organizationId) || request.units <= 0L || string.IsNullOrWhiteSpace(request.currencyDefinitionId)) return Fail(OrganizationResourceOperationCode.InvalidRequest, "Transaction ID, Organization ID, currency, and a positive amount are required.", request.preview);
            string operation = issue ? "deposit" : destroy ? "withdraw" : "transfer";
            string subject = $"{request.sourceAccountId}>{request.destinationAccountId}:{request.currencyDefinitionId}:{request.units}";
            if (transactionsById.TryGetValue(request.transactionId, out OrganizationResourceTransactionRecordData prior))
            {
                return prior.operation == operation && prior.subjectId == subject
                    ? OrganizationResourceOperationResult.Success("Organization financial transaction already committed.", before, before, duplicate: true, sourceBalance: GetBalance(request.sourceAccountId, request.worldTime), destinationBalance: GetBalance(request.destinationAccountId, request.worldTime), transaction: EconomyTransaction(prior.economyTransactionId), subjectId: subject)
                    : Fail(OrganizationResourceOperationCode.InvalidRequest, $"Transaction '{request.transactionId}' was already used for another operation.", request.preview);
            }
            OrganizationAccountRecordData source = null; OrganizationAccountRecordData destination = null;
            if (!issue && (!accountsById.TryGetValue(request.sourceAccountId, out source) || source.organizationId != request.organizationId)) return Fail(OrganizationResourceOperationCode.MissingAccount, "Source account is missing or not owned by the initiating Organization.", request.preview);
            if (!destroy && !accountsById.TryGetValue(request.destinationAccountId, out destination)) return Fail(OrganizationResourceOperationCode.MissingAccount, "Destination account is missing.", request.preview);
            if (source != null && source.currencyDefinitionId != request.currencyDefinitionId || destination != null && destination.currencyDefinitionId != request.currencyDefinitionId) return Fail(OrganizationResourceOperationCode.MissingCurrency, "Transaction currency does not match both accounts.", request.preview);
            if (source != null && source.lifecycleState != OrganizationAccountLifecycleState.Active) return Fail(source.lifecycleState == OrganizationAccountLifecycleState.Frozen ? OrganizationResourceOperationCode.AccountFrozen : OrganizationResourceOperationCode.AccountClosed, $"Source account is {source.lifecycleState}.", request.preview);
            if (destination != null && destination.lifecycleState != OrganizationAccountLifecycleState.Active) return Fail(destination.lifecycleState == OrganizationAccountLifecycleState.Frozen ? OrganizationResourceOperationCode.AccountFrozen : OrganizationResourceOperationCode.AccountClosed, $"Destination account is {destination.lifecycleState}.", request.preview);
            if (!TryOrganization(request.organizationId, request.worldTime, true, out _, out string lifecycleFailure)) return Fail(OrganizationResourceOperationCode.InvalidLifecycle, lifecycleFailure, request.preview);
            string defaultAction = issue ? PrototypeOrganizationAuthorityDefinitionFactory.DepositOrganizationFundsActionId : destroy ? PrototypeOrganizationAuthorityDefinitionFactory.WithdrawOrganizationFundsActionId : PrototypeOrganizationAuthorityDefinitionFactory.TransferOrganizationFundsActionId;
            string action = Action(request.actionDefinitionId, defaultAction);
            OrganizationAuthorizationResult authorization = Authorize(request.transactionId, request.actorPersonId, request.organizationId, action, source?.accountId ?? destination?.accountId, request.approvalPersonIds, request.worldTime, true, false);
            if (!authorization.Succeeded) return Unauthorized(authorization, request.preview);
            if (!ValidateSpend(request, source, out OrganizationResourceOperationResult spendFailure)) return spendFailure;
            EconomyOperationResult economyPreview = issue
                ? economy.Issue(request.transactionId, destination.economyAccountId, new MoneyAmount(request.currencyDefinitionId, request.units), request.actorPersonId, request.relatedRecordId, preview: true)
                : destroy
                    ? economy.Destroy(request.transactionId, source.economyAccountId, new MoneyAmount(request.currencyDefinitionId, request.units), request.actorPersonId, request.relatedRecordId, preview: true)
                    : economy.Transfer(request.transactionId, source.economyAccountId, destination.economyAccountId, new MoneyAmount(request.currencyDefinitionId, request.units), request.transactionKind, EconomyReservationId(request.reservationId), request.actorPersonId, preview: true);
            if (!economyPreview.Succeeded) return FinancialFailure(economyPreview, request.preview, authorization);
            if (request.preview) return OrganizationResourceOperationResult.Success("Organization financial operation preview succeeded.", before, before, preview: true, authorization: authorization, sourceBalance: source == null ? null : GetBalance(source.accountId, request.worldTime), destinationBalance: destination == null ? null : GetBalance(destination.accountId, request.worldTime), subjectId: subject);

            EconomyRuntimeSaveData economyRollback = economy.CreateSaveData(); OrganizationAuthorityRuntimeSaveData authorityRollback = authority.CreateSaveData(); OrganizationResourceRuntimeSaveData localRollback = CreateSaveData();
            authorization = Authorize(request.transactionId, request.actorPersonId, request.organizationId, action, source?.accountId ?? destination?.accountId, request.approvalPersonIds, request.worldTime, false, true);
            if (!authorization.Succeeded) return Unauthorized(authorization, false);
            EconomyOperationResult economyResult = issue
                ? economy.Issue(request.transactionId, destination.economyAccountId, new MoneyAmount(request.currencyDefinitionId, request.units), request.actorPersonId, request.relatedRecordId)
                : destroy
                    ? economy.Destroy(request.transactionId, source.economyAccountId, new MoneyAmount(request.currencyDefinitionId, request.units), request.actorPersonId, request.relatedRecordId)
                    : economy.Transfer(request.transactionId, source.economyAccountId, destination.economyAccountId, new MoneyAmount(request.currencyDefinitionId, request.units), request.transactionKind, EconomyReservationId(request.reservationId), request.actorPersonId);
            if (!economyResult.Succeeded)
            {
                RestoreDependencies(economyRollback, authorityRollback);
                return FinancialFailure(economyResult, false, authorization);
            }
            try
            {
                if (!string.IsNullOrWhiteSpace(request.restrictionId) && restrictionsById.TryGetValue(request.restrictionId, out OrganizationFundRestrictionRecordData restriction))
                {
                    restriction.remainingUnits -= request.units; restriction.revision++;
                    if (restriction.remainingUnits == 0L) restriction.lifecycleState = OrganizationFundRestrictionLifecycleState.Satisfied;
                }
                if (!string.IsNullOrWhiteSpace(request.reservationId) && reservationsById.TryGetValue(request.reservationId, out OrganizationResourceReservationRecordData reservation))
                {
                    reservation.lifecycleState = OrganizationReservationLifecycleState.Consumed; reservation.revision++;
                }
                Commit(request.transactionId, operation, subject, economyResult.Transaction?.TransactionId, request.organizationId, request.sourceAccountId, request.destinationAccountId, request.budgetId, request.restrictionId, request.purpose, request.currencyDefinitionId, request.units, request.worldTime);
            }
            catch (Exception exception)
            {
                RestoreLocal(localRollback); RestoreDependencies(economyRollback, authorityRollback);
                return Fail(OrganizationResourceOperationCode.RestoreFailed, $"Financial operation rolled back after a coordination failure: {exception.Message}", false, authorization);
            }
            return OrganizationResourceOperationResult.Success("Organization financial operation committed.", before, Revision, authorization: authorization, sourceBalance: source == null ? null : GetBalance(source.accountId, request.worldTime), destinationBalance: destination == null ? null : GetBalance(destination.accountId, request.worldTime), transaction: economyResult.Transaction, subjectId: subject);
        }

        public OrganizationResourceOperationResult AssociateInventory(OrganizationAssociationRequest request)
        {
            request ??= new OrganizationAssociationRequest(); long before = Revision;
            if (!CanMutate(out OrganizationResourceOperationResult dependencyFailure)) return dependencyFailure;
            if (!ValidateAssociationRequest(request, out string failure)) return Fail(OrganizationResourceOperationCode.InvalidRequest, failure, request.preview);
            if (inventoryAssociationsById.TryGetValue(request.associationId, out OrganizationInventoryAssociationRecordData existing)) return DuplicateOrConflict(request.transactionId, "associate-inventory", request.associationId, existing.inventoryId == request.resourceId, before, request.preview);
            OrganizationAuthorizationResult authorization = Authorize(request.transactionId, request.actorPersonId, request.organizationId, Action(request.actionDefinitionId, PrototypeOrganizationAuthorityDefinitionFactory.ManageOrganizationInventoryActionId), request.resourceId, Array.Empty<string>(), request.startWorldTime, true, false);
            if (!authorization.Succeeded) return Unauthorized(authorization, request.preview);
            OrganizationInventoryAssociationRecordData record = new OrganizationInventoryAssociationRecordData
            {
                associationId = request.associationId.Trim(), organizationId = request.organizationId.Trim(), inventoryId = request.resourceId.Trim(), category = Enum.IsDefined(typeof(OrganizationInventoryCategory), request.category) ? (OrganizationInventoryCategory)request.category : OrganizationInventoryCategory.GeneralStores,
                propertyId = request.propertyId ?? string.Empty, owningOrganizationId = request.organizationId.Trim(), operatingOrganizationId = string.IsNullOrWhiteSpace(request.secondaryOrganizationId) ? request.organizationId.Trim() : request.secondaryOrganizationId.Trim(),
                startWorldTime = request.startWorldTime, endWorldTime = request.endWorldTime, visibility = request.visibility, provenanceId = request.provenanceId ?? string.Empty
            };
            if (request.preview) return OrganizationResourceOperationResult.Success("Inventory association preview succeeded.", before, before, preview: true, authorization: authorization, subjectId: record.associationId);
            authorization = Authorize(request.transactionId, request.actorPersonId, request.organizationId, Action(request.actionDefinitionId, PrototypeOrganizationAuthorityDefinitionFactory.ManageOrganizationInventoryActionId), request.resourceId, Array.Empty<string>(), request.startWorldTime, false, true);
            if (!authorization.Succeeded) return Unauthorized(authorization, false);
            inventoryAssociationsById.Add(record.associationId, record);
            Commit(request.transactionId, "associate-inventory", record.associationId, string.Empty, request.organizationId, worldTime: request.startWorldTime);
            return OrganizationResourceOperationResult.Success("Organization inventory associated. Step 9 remains authoritative for inventory contents.", before, Revision, authorization: authorization, subjectId: record.associationId);
        }

        public OrganizationResourceOperationResult AssociateProperty(OrganizationAssociationRequest request)
        {
            request ??= new OrganizationAssociationRequest(); long before = Revision;
            if (!CanMutate(out OrganizationResourceOperationResult dependencyFailure)) return dependencyFailure;
            if (!ValidateAssociationRequest(request, out string failure)) return Fail(OrganizationResourceOperationCode.InvalidRequest, failure, request.preview);
            if (properties == null || !properties.TryGetProperty(request.resourceId, out _)) return Fail(OrganizationResourceOperationCode.MissingDependency, "PropertyRuntime is unavailable or the property does not exist.", request.preview);
            OrganizationPropertyAssociationCategory category = Enum.IsDefined(typeof(OrganizationPropertyAssociationCategory), request.category) ? (OrganizationPropertyAssociationCategory)request.category : OrganizationPropertyAssociationCategory.Operator;
            if (propertyAssociationsById.TryGetValue(request.associationId, out OrganizationPropertyAssociationRecordData existing)) return DuplicateOrConflict(request.transactionId, "associate-property", request.associationId, existing.propertyId == request.resourceId && existing.category == category, before, request.preview);
            if ((category == OrganizationPropertyAssociationCategory.Owner || category == OrganizationPropertyAssociationCategory.CoOwner) && !HasPropertyOwnership(request.organizationId, request.resourceId, request.sourceRecordId, request.startWorldTime)) return Fail(OrganizationResourceOperationCode.ValidationFailed, "Owner and co-owner associations require a matching active Step 11 ownership record.", request.preview);
            OrganizationAuthorizationResult authorization = Authorize(request.transactionId, request.actorPersonId, request.organizationId, Action(request.actionDefinitionId, PrototypeOrganizationAuthorityDefinitionFactory.ManagePropertyAssociationActionId), request.resourceId, Array.Empty<string>(), request.startWorldTime, true, false);
            if (!authorization.Succeeded) return Unauthorized(authorization, request.preview);
            OrganizationPropertyAssociationRecordData record = new OrganizationPropertyAssociationRecordData
            {
                associationId = request.associationId.Trim(), organizationId = request.organizationId.Trim(), propertyId = request.resourceId.Trim(), category = category,
                ownershipRecordId = request.sourceRecordId ?? string.Empty, contractReferenceId = request.secondaryOrganizationId ?? string.Empty, rightIds = OrganizationModelUtility.Clean(request.rightIds), responsibilityIds = OrganizationModelUtility.Clean(request.responsibilityIds),
                startWorldTime = request.startWorldTime, endWorldTime = request.endWorldTime, visibility = request.visibility, provenanceId = request.provenanceId ?? string.Empty
            };
            if (request.preview) return OrganizationResourceOperationResult.Success("Property association preview succeeded.", before, before, preview: true, authorization: authorization, subjectId: record.associationId);
            authorization = Authorize(request.transactionId, request.actorPersonId, request.organizationId, Action(request.actionDefinitionId, PrototypeOrganizationAuthorityDefinitionFactory.ManagePropertyAssociationActionId), request.resourceId, Array.Empty<string>(), request.startWorldTime, false, true);
            if (!authorization.Succeeded) return Unauthorized(authorization, false);
            propertyAssociationsById.Add(record.associationId, record);
            Commit(request.transactionId, "associate-property", record.associationId, string.Empty, request.organizationId, worldTime: request.startWorldTime);
            return OrganizationResourceOperationResult.Success("Organization property association created; ownership remains in PropertyRuntime.", before, Revision, authorization: authorization, subjectId: record.associationId);
        }

        public OrganizationResourceOperationResult AssociateBusiness(OrganizationAssociationRequest request)
        {
            request ??= new OrganizationAssociationRequest(); long before = Revision;
            if (!CanMutate(out OrganizationResourceOperationResult dependencyFailure)) return dependencyFailure;
            if (!ValidateAssociationRequest(request, out string failure)) return Fail(OrganizationResourceOperationCode.InvalidRequest, failure, request.preview);
            if (businesses == null || !businesses.TryGetBusiness(request.resourceId, out _)) return Fail(OrganizationResourceOperationCode.MissingDependency, "BusinessRuntime is unavailable or the business does not exist.", request.preview);
            OrganizationBusinessAssociationCategory category = Enum.IsDefined(typeof(OrganizationBusinessAssociationCategory), request.category) ? (OrganizationBusinessAssociationCategory)request.category : OrganizationBusinessAssociationCategory.Operator;
            if (businessAssociationsById.TryGetValue(request.associationId, out OrganizationBusinessAssociationRecordData existing)) return DuplicateOrConflict(request.transactionId, "associate-business", request.associationId, existing.businessId == request.resourceId && existing.category == category, before, request.preview);
            if ((category == OrganizationBusinessAssociationCategory.Owner || category == OrganizationBusinessAssociationCategory.PartialOwner) && !HasBusinessOwnership(request.organizationId, request.resourceId, request.sourceRecordId, request.startWorldTime)) return Fail(OrganizationResourceOperationCode.ValidationFailed, "Owner associations require a matching active Step 11 business ownership record.", request.preview);
            OrganizationAuthorizationResult authorization = Authorize(request.transactionId, request.actorPersonId, request.organizationId, Action(request.actionDefinitionId, PrototypeOrganizationAuthorityDefinitionFactory.ManageBusinessAssociationActionId), request.resourceId, Array.Empty<string>(), request.startWorldTime, true, false);
            if (!authorization.Succeeded) return Unauthorized(authorization, request.preview);
            OrganizationBusinessAssociationRecordData record = new OrganizationBusinessAssociationRecordData
            {
                associationId = request.associationId.Trim(), organizationId = request.organizationId.Trim(), businessId = request.resourceId.Trim(), category = category,
                ownershipRecordId = request.sourceRecordId ?? string.Empty, shareBasisPoints = request.shareBasisPoints, startWorldTime = request.startWorldTime, endWorldTime = request.endWorldTime,
                visibility = request.visibility, provenanceId = request.provenanceId ?? string.Empty
            };
            if (request.preview) return OrganizationResourceOperationResult.Success("Business association preview succeeded.", before, before, preview: true, authorization: authorization, subjectId: record.associationId);
            authorization = Authorize(request.transactionId, request.actorPersonId, request.organizationId, Action(request.actionDefinitionId, PrototypeOrganizationAuthorityDefinitionFactory.ManageBusinessAssociationActionId), request.resourceId, Array.Empty<string>(), request.startWorldTime, false, true);
            if (!authorization.Succeeded) return Unauthorized(authorization, false);
            businessAssociationsById.Add(record.associationId, record);
            Commit(request.transactionId, "associate-business", record.associationId, string.Empty, request.organizationId, worldTime: request.startWorldTime);
            return OrganizationResourceOperationResult.Success("Organization business association created; ownership remains in BusinessRuntime.", before, Revision, authorization: authorization, subjectId: record.associationId);
        }

        public OrganizationResourceOperationResult AssignCustody(OrganizationCustodyRequest request)
        {
            request ??= new OrganizationCustodyRequest(); long before = Revision;
            if (!CanMutate(out OrganizationResourceOperationResult dependencyFailure)) return dependencyFailure;
            if (string.IsNullOrWhiteSpace(request.transactionId) || string.IsNullOrWhiteSpace(request.custodyId) || string.IsNullOrWhiteSpace(request.organizationId) || request.asset == null || string.IsNullOrWhiteSpace(request.asset.resourceId)) return Fail(OrganizationResourceOperationCode.InvalidRequest, "Transaction, custody, Organization, and typed asset identities are required.", request.preview);
            if (string.IsNullOrWhiteSpace(request.custodianPersonId) == string.IsNullOrWhiteSpace(request.custodianOrganizationId)) return Fail(OrganizationResourceOperationCode.InvalidRequest, "Exactly one Person or Organization custodian is required.", request.preview);
            if (!ValidAssetReference(request.asset, out string failure)) return Fail(OrganizationResourceOperationCode.MissingResource, failure, request.preview);
            if (custodyById.TryGetValue(request.custodyId, out OrganizationAssetCustodyRecordData existing)) return DuplicateOrConflict(request.transactionId, "assign-custody", request.custodyId, existing.asset.StableKey == request.asset.StableKey, before, request.preview);
            if (custodyById.Values.Any(item => item.asset.StableKey == request.asset.StableKey && (item.lifecycleState == OrganizationCustodyLifecycleState.InCustody || item.lifecycleState == OrganizationCustodyLifecycleState.CheckedOut))) return Fail(OrganizationResourceOperationCode.ValidationFailed, "The asset already has an active custody record.", request.preview);
            OrganizationAuthorizationResult authorization = Authorize(request.transactionId, request.actorPersonId, request.organizationId, Action(request.actionDefinitionId, PrototypeOrganizationAuthorityDefinitionFactory.AssignAssetCustodyActionId), request.asset.resourceId, Array.Empty<string>(), request.startWorldTime, true, false);
            if (!authorization.Succeeded) return Unauthorized(authorization, request.preview);
            OrganizationAssetCustodyRecordData record = new OrganizationAssetCustodyRecordData
            {
                custodyId = request.custodyId.Trim(), organizationId = request.organizationId.Trim(), asset = request.asset.Clone(), custodianPersonId = request.custodianPersonId ?? string.Empty,
                custodianOrganizationId = request.custodianOrganizationId ?? string.Empty, sourceInventoryId = request.sourceInventoryId ?? string.Empty, destinationInventoryId = request.destinationInventoryId ?? string.Empty,
                startWorldTime = request.startWorldTime, expectedReturnWorldTime = request.expectedReturnWorldTime, lifecycleState = OrganizationCustodyLifecycleState.InCustody,
                sourceOperationId = request.sourceOperationId ?? string.Empty, conditionSnapshotReferenceId = request.conditionSnapshotReferenceId ?? string.Empty, visibility = request.visibility, provenanceId = request.provenanceId ?? string.Empty
            };
            if (request.preview) return OrganizationResourceOperationResult.Success("Custody preview succeeded.", before, before, preview: true, authorization: authorization, subjectId: record.custodyId);
            authorization = Authorize(request.transactionId, request.actorPersonId, request.organizationId, Action(request.actionDefinitionId, PrototypeOrganizationAuthorityDefinitionFactory.AssignAssetCustodyActionId), request.asset.resourceId, Array.Empty<string>(), request.startWorldTime, false, true);
            if (!authorization.Succeeded) return Unauthorized(authorization, false);
            custodyById.Add(record.custodyId, record);
            Commit(request.transactionId, "assign-custody", record.custodyId, string.Empty, request.organizationId, worldTime: request.startWorldTime);
            return OrganizationResourceOperationResult.Success("Asset custody assigned without changing ownership.", before, Revision, authorization: authorization, subjectId: record.custodyId);
        }

        public OrganizationResourceOperationResult ReturnCustody(string custodyId, string transactionId, string actorPersonId, double worldTime, string conditionSnapshotReferenceId = "", bool preview = false)
        {
            long before = Revision;
            if (!CanMutate(out OrganizationResourceOperationResult dependencyFailure)) return dependencyFailure;
            if (!custodyById.TryGetValue(custodyId ?? string.Empty, out OrganizationAssetCustodyRecordData record)) return Fail(OrganizationResourceOperationCode.MissingCustody, $"Custody '{custodyId}' was not found.", preview);
            if (record.lifecycleState != OrganizationCustodyLifecycleState.InCustody && record.lifecycleState != OrganizationCustodyLifecycleState.Overdue) return Fail(OrganizationResourceOperationCode.InvalidLifecycle, $"Custody '{custodyId}' is {record.lifecycleState}.", preview);
            OrganizationAuthorizationResult authorization = Authorize(transactionId, actorPersonId, record.organizationId, PrototypeOrganizationAuthorityDefinitionFactory.AssignAssetCustodyActionId, record.asset.resourceId, Array.Empty<string>(), worldTime, true, false);
            if (!authorization.Succeeded) return Unauthorized(authorization, preview);
            if (preview) return OrganizationResourceOperationResult.Success("Custody return preview succeeded.", before, before, preview: true, authorization: authorization, subjectId: record.custodyId);
            authorization = Authorize(transactionId, actorPersonId, record.organizationId, PrototypeOrganizationAuthorityDefinitionFactory.AssignAssetCustodyActionId, record.asset.resourceId, Array.Empty<string>(), worldTime, false, true);
            if (!authorization.Succeeded) return Unauthorized(authorization, false);
            record.lifecycleState = OrganizationCustodyLifecycleState.Returned; record.returnWorldTime = worldTime; record.conditionSnapshotReferenceId = string.IsNullOrWhiteSpace(conditionSnapshotReferenceId) ? record.conditionSnapshotReferenceId : conditionSnapshotReferenceId; record.revision++;
            Commit(transactionId, "return-custody", record.custodyId, string.Empty, record.organizationId, worldTime: worldTime);
            return OrganizationResourceOperationResult.Success("Asset custody returned; ownership was unchanged.", before, Revision, authorization: authorization, subjectId: record.custodyId);
        }

        public OrganizationResourceOperationResult CreateRevenueRoutingRule(OrganizationRevenueRoutingRequest request)
        {
            request ??= new OrganizationRevenueRoutingRequest(); long before = Revision;
            if (!CanMutate(out OrganizationResourceOperationResult dependencyFailure)) return dependencyFailure;
            if (string.IsNullOrWhiteSpace(request.transactionId) || string.IsNullOrWhiteSpace(request.routingRuleId) || string.IsNullOrWhiteSpace(request.organizationId) || string.IsNullOrWhiteSpace(request.revenueSourceId)) return Fail(OrganizationResourceOperationCode.InvalidRequest, "Transaction, routing-rule, Organization, and source IDs are required.", request.preview);
            if (!accountsById.TryGetValue(request.destinationAccountId ?? string.Empty, out OrganizationAccountRecordData account)) return Fail(OrganizationResourceOperationCode.MissingAccount, "Revenue destination account was not found.", request.preview);
            if (request.percentageBasisPoints < 0L || request.percentageBasisPoints > 10000L || request.fixedUnits < 0L || request.percentageBasisPoints == 0L && request.fixedUnits == 0L) return Fail(OrganizationResourceOperationCode.ValidationFailed, "Routing requires a valid percentage or fixed amount.", request.preview);
            if (routingById.TryGetValue(request.routingRuleId, out OrganizationRevenueRoutingRuleData existing)) return DuplicateOrConflict(request.transactionId, "create-routing", request.routingRuleId, existing.destinationAccountId == request.destinationAccountId, before, request.preview);
            OrganizationAuthorizationResult authorization = Authorize(request.transactionId, request.actorPersonId, request.organizationId, Action(request.actionDefinitionId, PrototypeOrganizationAuthorityDefinitionFactory.TransferOrganizationFundsActionId), request.routingRuleId, Array.Empty<string>(), request.startWorldTime, true, false);
            if (!authorization.Succeeded) return Unauthorized(authorization, request.preview);
            OrganizationRevenueRoutingRuleData record = new OrganizationRevenueRoutingRuleData
            {
                routingRuleId = request.routingRuleId.Trim(), organizationId = request.organizationId.Trim(), revenueSourceId = request.revenueSourceId.Trim(), destinationAccountId = account.accountId,
                percentageBasisPoints = request.percentageBasisPoints, fixedUnits = request.fixedUnits, priority = request.priority, purpose = request.purpose ?? string.Empty,
                branchOrganizationId = request.branchOrganizationId ?? string.Empty, startWorldTime = request.startWorldTime, endWorldTime = request.endWorldTime, visibility = request.visibility, provenanceId = request.provenanceId ?? string.Empty
            };
            if (request.preview) return OrganizationResourceOperationResult.Success("Revenue routing preview succeeded.", before, before, preview: true, authorization: authorization, subjectId: record.routingRuleId);
            authorization = Authorize(request.transactionId, request.actorPersonId, request.organizationId, Action(request.actionDefinitionId, PrototypeOrganizationAuthorityDefinitionFactory.TransferOrganizationFundsActionId), request.routingRuleId, Array.Empty<string>(), request.startWorldTime, false, true);
            if (!authorization.Succeeded) return Unauthorized(authorization, false);
            routingById.Add(record.routingRuleId, record);
            Commit(request.transactionId, "create-routing", record.routingRuleId, string.Empty, request.organizationId, destinationAccountId: record.destinationAccountId, purpose: record.purpose, worldTime: request.startWorldTime);
            return OrganizationResourceOperationResult.Success("Revenue routing rule created. Source revenue remains owned by its Step 11 runtime.", before, Revision, authorization: authorization, subjectId: record.routingRuleId);
        }

        public OrganizationResourceOperationResult ApplyRevenueRouting(OrganizationRevenueRoutingExecutionRequest request)
        {
            request ??= new OrganizationRevenueRoutingExecutionRequest();
            long before = Revision;
            if (!CanMutate(out OrganizationResourceOperationResult dependencyFailure)) return dependencyFailure;
            if (string.IsNullOrWhiteSpace(request.transactionId) || string.IsNullOrWhiteSpace(request.organizationId) || string.IsNullOrWhiteSpace(request.revenueSourceId) || request.grossUnits <= 0L)
                return Fail(OrganizationResourceOperationCode.InvalidRequest, "Transaction, Organization, revenue source, and positive gross units are required.", request.preview);
            OrganizationRevenueRoutingRuleData[] rules = routingById.Values
                .Where(item => item.organizationId == request.organizationId && item.revenueSourceId == request.revenueSourceId
                    && item.lifecycleState == OrganizationRevenueRoutingLifecycleState.Active
                    && request.worldTime >= item.startWorldTime && (item.endWorldTime < 0d || request.worldTime < item.endWorldTime))
                .OrderByDescending(item => item.priority).ThenBy(item => item.routingRuleId, StringComparer.Ordinal).ToArray();
            if (rules.Length == 0) return Fail(OrganizationResourceOperationCode.MissingAssociation, "No active revenue routing rule matches the source.", request.preview);
            long[] routedUnits = rules.Select(item => checked(item.fixedUnits + request.grossUnits * item.percentageBasisPoints / 10000L)).ToArray();
            if (routedUnits.Any(item => item <= 0L) || routedUnits.Sum() > request.grossUnits) return Fail(OrganizationResourceOperationCode.FinanciallyInvalid, "Revenue routing allocations are zero or exceed gross revenue.", request.preview);

            EconomyRuntimeSaveData economyRollback = economy.CreateSaveData();
            OrganizationAuthorityRuntimeSaveData authorityRollback = authority.CreateSaveData();
            OrganizationResourceRuntimeSaveData localRollback = CreateSaveData();
            try
            {
                OrganizationResourceOperationResult last = null;
                for (int index = 0; index < rules.Length; index++)
                {
                    last = TransferFunds(new OrganizationFinancialTransactionRequest
                    {
                        transactionId = $"{request.transactionId}.route.{index:D4}", organizationId = request.organizationId,
                        sourceAccountId = request.sourceAccountId, destinationAccountId = rules[index].destinationAccountId,
                        currencyDefinitionId = request.currencyDefinitionId, units = routedUnits[index], transactionKind = EconomyTransactionKind.Transfer,
                        actorPersonId = request.actorPersonId, purpose = rules[index].purpose, relatedRecordId = request.revenueSourceId,
                        approvalPersonIds = request.approvalPersonIds, worldTime = request.worldTime, preview = request.preview
                    });
                    if (!last.Succeeded) throw new InvalidOperationException(last.Message);
                }
                if (request.preview)
                {
                    RestoreLocal(localRollback); RestoreDependencies(economyRollback, authorityRollback);
                    return OrganizationResourceOperationResult.Success("Revenue routing preview succeeded.", before, before, preview: true, subjectId: request.revenueSourceId);
                }
                return OrganizationResourceOperationResult.Success("Revenue routed through authoritative Economy accounts.", before, Revision, destinationBalance: last?.DestinationBalance, subjectId: request.revenueSourceId);
            }
            catch (Exception exception)
            {
                RestoreLocal(localRollback); RestoreDependencies(economyRollback, authorityRollback);
                return Fail(OrganizationResourceOperationCode.FinanciallyInvalid, $"Revenue routing rolled back atomically: {exception.Message}", request.preview);
            }
        }

        public OrganizationResourceOperationResult CreateDissolutionResourcePlan(OrganizationDissolutionResourcePlanRequest request)
        {
            request ??= new OrganizationDissolutionResourcePlanRequest();
            long before = Revision;
            if (!CanMutate(out OrganizationResourceOperationResult dependencyFailure)) return dependencyFailure;
            if (!ValidateRequired(request.transactionId, request.planId, request.organizationId, out string failure)) return Fail(OrganizationResourceOperationCode.InvalidRequest, failure, request.preview);
            if (!TryOrganization(request.organizationId, request.worldTime, true, out _, out failure)) return Fail(OrganizationResourceOperationCode.InvalidLifecycle, failure, request.preview);
            if (dissolutionPlansById.ContainsKey(request.planId)) return DuplicateOrConflict(request.transactionId, "create-dissolution-plan", request.planId, true, before, request.preview);
            string[] accountIds = OrganizationModelUtility.Clean(request.accountIdsToFreeze);
            if (accountIds.Any(id => !accountsById.TryGetValue(id, out OrganizationAccountRecordData account) || account.organizationId != request.organizationId)) return Fail(OrganizationResourceOperationCode.MissingAccount, "Every dissolution account must belong to the Organization.", request.preview);
            OrganizationDissolutionAssetInstructionData[] instructions = (request.assetInstructions ?? Array.Empty<OrganizationDissolutionAssetInstructionData>()).Where(item => item != null).Select(item => item.Clone()).ToArray();
            foreach (OrganizationDissolutionAssetInstructionData instruction in instructions)
            {
                if (!ValidAssetReference(instruction.asset, out failure)) return Fail(OrganizationResourceOperationCode.MissingResource, failure, request.preview);
                if (instruction.kind != OrganizationDissolutionAssetInstructionKind.PreserveUnresolved && string.IsNullOrWhiteSpace(instruction.destinationId)) return Fail(OrganizationResourceOperationCode.ValidationFailed, "Transfer instructions require an explicit destination.", request.preview);
            }
            string action = Action(request.actionDefinitionId, PrototypeOrganizationAuthorityDefinitionFactory.ManageTreasuryAccountActionId);
            OrganizationAuthorizationResult authorization = Authorize(request.transactionId, request.actorPersonId, request.organizationId, action, request.planId, request.approvalPersonIds, request.worldTime, true, false);
            if (!authorization.Succeeded) return Unauthorized(authorization, request.preview);
            OrganizationDissolutionResourcePlanData record = new OrganizationDissolutionResourcePlanData
            {
                planId = request.planId.Trim(), organizationId = request.organizationId.Trim(), lifecycleState = OrganizationDissolutionPlanLifecycleState.Approved,
                accountIdsToFreeze = accountIds, preservedObligationIds = OrganizationModelUtility.Clean(request.preservedObligationIds), assetInstructions = instructions,
                approvedByPersonId = request.actorPersonId ?? string.Empty, createdWorldTime = request.worldTime, visibility = request.visibility, provenanceId = request.provenanceId ?? string.Empty
            };
            if (request.preview) return OrganizationResourceOperationResult.Success("Dissolution resource plan preview succeeded.", before, before, preview: true, authorization: authorization, subjectId: record.planId);
            authorization = Authorize(request.transactionId, request.actorPersonId, request.organizationId, action, request.planId, request.approvalPersonIds, request.worldTime, false, true);
            if (!authorization.Succeeded) return Unauthorized(authorization, false);
            dissolutionPlansById.Add(record.planId, record);
            Commit(request.transactionId, "create-dissolution-plan", record.planId, string.Empty, record.organizationId, worldTime: request.worldTime);
            return OrganizationResourceOperationResult.Success("Dissolution resource plan recorded without selecting beneficiaries or moving assets.", before, Revision, authorization: authorization, subjectId: record.planId);
        }

        public OrganizationResourceOperationResult ExecuteDissolutionResourcePlan(string planId, string transactionId, string actorPersonId, string[] approvalPersonIds, double worldTime, bool preview = false)
        {
            long before = Revision;
            if (!CanMutate(out OrganizationResourceOperationResult dependencyFailure)) return dependencyFailure;
            if (!dissolutionPlansById.TryGetValue(planId ?? string.Empty, out OrganizationDissolutionResourcePlanData plan)) return Fail(OrganizationResourceOperationCode.MissingAssociation, $"Dissolution plan '{planId}' was not found.", preview);
            if (plan.lifecycleState == OrganizationDissolutionPlanLifecycleState.Executed) return DuplicateOrConflict(transactionId, "execute-dissolution-plan", plan.planId, true, before, preview);
            if (!TryOrganization(plan.organizationId, worldTime, true, out _, out string lifecycleFailure)) return Fail(OrganizationResourceOperationCode.InvalidLifecycle, lifecycleFailure, preview);
            OrganizationAuthorizationResult authorization = Authorize(transactionId, actorPersonId, plan.organizationId, PrototypeOrganizationAuthorityDefinitionFactory.ManageTreasuryAccountActionId, plan.planId, approvalPersonIds, worldTime, true, false);
            if (!authorization.Succeeded) return Unauthorized(authorization, preview);
            EconomyRuntimeSaveData economyRollback = economy.CreateSaveData(); OrganizationAuthorityRuntimeSaveData authorityRollback = authority.CreateSaveData(); OrganizationResourceRuntimeSaveData localRollback = CreateSaveData();
            if (preview) return OrganizationResourceOperationResult.Success("Dissolution plan execution preview succeeded.", before, before, preview: true, authorization: authorization, subjectId: plan.planId);
            try
            {
                authorization = Authorize(transactionId, actorPersonId, plan.organizationId, PrototypeOrganizationAuthorityDefinitionFactory.ManageTreasuryAccountActionId, plan.planId, approvalPersonIds, worldTime, false, true);
                if (!authorization.Succeeded) return Unauthorized(authorization, false);
                foreach (string accountId in plan.accountIdsToFreeze)
                {
                    OrganizationAccountRecordData account = accountsById[accountId];
                    EconomyOperationResult result = economy.ChangeAccountState($"{transactionId}.freeze.{accountId}", account.economyAccountId, EconomyAccountState.Frozen);
                    if (!result.Succeeded && result.Code != EconomyResultCode.Duplicate) throw new InvalidOperationException(result.Message);
                    account.lifecycleState = OrganizationAccountLifecycleState.Frozen; account.revision++;
                }
                plan.lifecycleState = OrganizationDissolutionPlanLifecycleState.Executed; plan.executedWorldTime = worldTime; plan.revision++;
                Commit(transactionId, "execute-dissolution-plan", plan.planId, string.Empty, plan.organizationId, worldTime: worldTime);
                return OrganizationResourceOperationResult.Success("Dissolution resource plan executed; accounts were frozen and unresolved assets were preserved.", before, Revision, authorization: authorization, subjectId: plan.planId);
            }
            catch (Exception exception)
            {
                RestoreLocal(localRollback); RestoreDependencies(economyRollback, authorityRollback);
                return Fail(OrganizationResourceOperationCode.RestoreFailed, $"Dissolution plan execution rolled back: {exception.Message}", false, authorization);
            }
        }

        public OrganizationResourceOperationResult ReleaseRestriction(string restrictionId, string transactionId, string actorPersonId, double worldTime, bool preview = false)
        {
            long before = Revision;
            if (!CanMutate(out OrganizationResourceOperationResult dependencyFailure)) return dependencyFailure;
            if (!restrictionsById.TryGetValue(restrictionId ?? string.Empty, out OrganizationFundRestrictionRecordData record)) return Fail(OrganizationResourceOperationCode.MissingRestriction, $"Restriction '{restrictionId}' was not found.", preview);
            if (record.lifecycleState != OrganizationFundRestrictionLifecycleState.Active) return Fail(OrganizationResourceOperationCode.InvalidLifecycle, $"Restriction '{restrictionId}' is {record.lifecycleState}.", preview);
            OrganizationAuthorizationResult authorization = Authorize(transactionId, actorPersonId, record.organizationId, PrototypeOrganizationAuthorityDefinitionFactory.ManageRestrictedFundsActionId, record.restrictionId, Array.Empty<string>(), worldTime, true, false);
            if (!authorization.Succeeded) return Unauthorized(authorization, preview);
            if (preview) return OrganizationResourceOperationResult.Success("Restriction release preview succeeded.", before, before, preview: true, authorization: authorization, subjectId: record.restrictionId);
            authorization = Authorize(transactionId, actorPersonId, record.organizationId, PrototypeOrganizationAuthorityDefinitionFactory.ManageRestrictedFundsActionId, record.restrictionId, Array.Empty<string>(), worldTime, false, true);
            if (!authorization.Succeeded) return Unauthorized(authorization, false);
            record.lifecycleState = OrganizationFundRestrictionLifecycleState.Released; record.revision++;
            Commit(transactionId, "release-restriction", record.restrictionId, string.Empty, record.organizationId, restrictionId: record.restrictionId, worldTime: worldTime);
            return OrganizationResourceOperationResult.Success("Restriction released without converting or moving funds.", before, Revision, authorization: authorization, subjectId: record.restrictionId);
        }

        public OrganizationResourceOperationResult EvaluateTime(double worldTime)
        {
            long before = Revision;
            if (!CanMutate(out OrganizationResourceOperationResult dependencyFailure)) return dependencyFailure;
            foreach (OrganizationFundRestrictionRecordData restriction in restrictionsById.Values.OrderBy(item => item.restrictionId, StringComparer.Ordinal))
            {
                if (restriction.lifecycleState == OrganizationFundRestrictionLifecycleState.Active && restriction.endWorldTime >= 0d && worldTime >= restriction.endWorldTime)
                {
                    restriction.lifecycleState = OrganizationFundRestrictionLifecycleState.Expired; restriction.revision++; Revision++;
                }
            }
            foreach (OrganizationBudgetRecordData budget in budgetsById.Values.OrderBy(item => item.budgetId, StringComparer.Ordinal))
            {
                if (budget.lifecycleState == OrganizationBudgetLifecycleState.Active && budget.endWorldTime >= 0d && worldTime >= budget.endWorldTime)
                {
                    budget.lifecycleState = OrganizationBudgetLifecycleState.Expired; budget.revision++; Revision++;
                }
            }
            foreach (OrganizationResourceReservationRecordData reservation in reservationsById.Values.OrderBy(item => item.reservationId, StringComparer.Ordinal))
            {
                if (reservation.lifecycleState == OrganizationReservationLifecycleState.Active && reservation.expirationWorldTime >= 0d && worldTime >= reservation.expirationWorldTime)
                {
                    if (!string.IsNullOrWhiteSpace(reservation.economyReservationId))
                    {
                        EconomyOperationResult result = economy.ExpireReservation(reservation.economyReservationId, worldTime, $"organization-resource.expire.{reservation.reservationId}.{reservation.expirationWorldTime:R}");
                        if (!result.Succeeded && result.Code != EconomyResultCode.Duplicate) return FinancialFailure(result, false, null);
                    }
                    reservation.lifecycleState = OrganizationReservationLifecycleState.Expired; reservation.revision++; Revision++;
                }
            }
            foreach (OrganizationAssetCustodyRecordData custody in custodyById.Values.OrderBy(item => item.custodyId, StringComparer.Ordinal))
            {
                if (custody.lifecycleState == OrganizationCustodyLifecycleState.InCustody && custody.expectedReturnWorldTime >= 0d && worldTime >= custody.expectedReturnWorldTime)
                {
                    custody.lifecycleState = OrganizationCustodyLifecycleState.Overdue; custody.revision++; Revision++;
                }
            }
            IsDirty |= Revision != before;
            return OrganizationResourceOperationResult.Success(Revision == before ? "No organization resource time boundary changed." : "Organization resource time boundaries evaluated.", before, Revision, duplicate: Revision == before);
        }

        public bool TryGetTreasury(string treasuryId, out OrganizationTreasuryRecordData treasury)
        {
            treasury = null;
            if (!string.IsNullOrWhiteSpace(treasuryId) && treasuriesById.TryGetValue(treasuryId, out OrganizationTreasuryRecordData found)) { treasury = found.Clone(); return true; }
            return false;
        }

        public bool TryGetAccount(string accountId, out OrganizationAccountRecordData account)
        {
            account = null;
            if (!string.IsNullOrWhiteSpace(accountId) && accountsById.TryGetValue(accountId, out OrganizationAccountRecordData found)) { account = found.Clone(); return true; }
            return false;
        }

        public IReadOnlyList<OrganizationTreasuryRecordData> QueryTreasuries(string organizationId, bool activeOnly = false) => Treasuries.Where(item => item.organizationId == organizationId && (!activeOnly || item.lifecycleState == OrganizationTreasuryLifecycleState.Active)).ToArray();
        public IReadOnlyList<OrganizationAccountRecordData> QueryAccounts(string organizationId = "", string treasuryId = "", OrganizationAccountLifecycleState? state = null) => Accounts.Where(item => (string.IsNullOrWhiteSpace(organizationId) || item.organizationId == organizationId) && (string.IsNullOrWhiteSpace(treasuryId) || item.treasuryId == treasuryId) && (!state.HasValue || item.lifecycleState == state.Value)).ToArray();
        public IReadOnlyList<OrganizationFundRestrictionRecordData> QueryRestrictions(string accountId, double worldTime, bool activeOnly = false) => Restrictions.Where(item => item.accountId == accountId && (!activeOnly || item.IsActiveAt(worldTime))).ToArray();
        public IReadOnlyList<OrganizationBudgetRecordData> QueryBudgets(string organizationId, double worldTime, bool activeOnly = false) => Budgets.Where(item => item.organizationId == organizationId && (!activeOnly || item.IsActiveAt(worldTime))).ToArray();
        public IReadOnlyList<OrganizationResourceReservationRecordData> QueryReservations(string organizationId, double worldTime, bool activeOnly = false) => Reservations.Where(item => item.organizationId == organizationId && (!activeOnly || item.IsActiveAt(worldTime))).ToArray();
        public IReadOnlyList<OrganizationPropertyAssociationRecordData> QueryPropertyAssociations(string organizationId, double worldTime, bool activeOnly = true) => PropertyAssociations.Where(item => item.organizationId == organizationId && (!activeOnly || item.IsActiveAt(worldTime))).ToArray();
        public IReadOnlyList<OrganizationBusinessAssociationRecordData> QueryBusinessAssociations(string organizationId, double worldTime, bool activeOnly = true) => BusinessAssociations.Where(item => item.organizationId == organizationId && (!activeOnly || item.IsActiveAt(worldTime))).ToArray();
        public IReadOnlyList<OrganizationAssetCustodyRecordData> QueryCustody(string organizationId, bool activeOnly = false) => CustodyRecords.Where(item => item.organizationId == organizationId && (!activeOnly || item.lifecycleState == OrganizationCustodyLifecycleState.InCustody || item.lifecycleState == OrganizationCustodyLifecycleState.Overdue)).ToArray();

        public OrganizationAccountBalanceSnapshot GetBalance(string accountId, double worldTime)
        {
            if (string.IsNullOrWhiteSpace(accountId) || !accountsById.TryGetValue(accountId, out OrganizationAccountRecordData account) || !economy.TryGetAccount(account.economyAccountId, out EconomyAccountSnapshot economyAccount)) return null;
            long restricted = ActiveRestrictions(accountId, worldTime).Sum(item => item.remainingUnits);
            long encumbered = reservationsById.Values.Where(item => item.accountId == accountId && item.IsActiveAt(worldTime) && IsEncumbrance(item.category)).Sum(item => item.amountUnits);
            long reserved = Math.Max(0L, economyAccount.ReservedUnits - encumbered);
            return new OrganizationAccountBalanceSnapshot(account, economyAccount, restricted, reserved, encumbered);
        }

        public long GetBudgetSpentUnits(string budgetId)
        {
            return transactionsById.Values.Where(item => item.budgetId == budgetId && item.code == OrganizationResourceOperationCode.Success).Sum(item => item.units);
        }

        public IReadOnlyList<OrganizationLiabilitySnapshot> QueryLiabilities(string organizationId)
        {
            List<OrganizationLiabilitySnapshot> result = new List<OrganizationLiabilitySnapshot>();
            if (contracts != null)
            {
                foreach (ContractObligationData item in contracts.Obligations.Where(item => item.OutstandingUnits > 0L && (item.obligorPartyId == organizationId || item.beneficiaryPartyId == organizationId)))
                    result.Add(new OrganizationLiabilitySnapshot(OrganizationLiabilitySourceKind.ContractObligation, item.obligationId, organizationId, item.currencyId, item.obligorPartyId == organizationId ? item.OutstandingUnits : 0L, item.beneficiaryPartyId == organizationId ? item.OutstandingUnits : 0L, item.dueWorldTime));
                foreach (LoanData item in contracts.Loans.Where(item => item.outstandingPrincipalUnits + item.accruedInterestOutstandingUnits > 0L && (item.borrowerPartyId == organizationId || item.lenderPartyId == organizationId)))
                {
                    long outstanding = checked(item.outstandingPrincipalUnits + item.accruedInterestOutstandingUnits);
                    result.Add(new OrganizationLiabilitySnapshot(OrganizationLiabilitySourceKind.Loan, item.loanId, organizationId, item.currencyId, item.borrowerPartyId == organizationId ? outstanding : 0L, item.lenderPartyId == organizationId ? outstanding : 0L, -1d));
                }
            }
            if (payroll != null)
            {
                HashSet<string> represented = new HashSet<string>(StringComparer.Ordinal);
                foreach (PayrollObligationData item in payroll.Obligations.Where(item => item.employerSubjectId == organizationId && item.amountOutstandingUnits > 0L))
                {
                    represented.Add(item.obligationId);
                    result.Add(new OrganizationLiabilitySnapshot(OrganizationLiabilitySourceKind.PayrollObligation, item.obligationId, organizationId, item.currencyId, item.amountOutstandingUnits, 0L, item.dueWorldTime));
                }
                foreach (WageDebtData item in payroll.WageDebts.Where(item => item.employerSubjectId == organizationId && !item.resolved && item.outstandingUnits > 0L && !represented.Contains(item.obligationId)))
                    result.Add(new OrganizationLiabilitySnapshot(OrganizationLiabilitySourceKind.WageDebt, item.wageDebtId, organizationId, item.currencyId, item.outstandingUnits, 0L, item.createdWorldTime));
            }
            return result.OrderBy(item => item.DueWorldTime).ThenBy(item => item.SourceKind).ThenBy(item => item.SourceId, StringComparer.Ordinal).ToArray();
        }

        public OrganizationResourceValuationSnapshot GetKnownValuation(string organizationId, string currencyId, double worldTime)
        {
            long cash = QueryAccounts(organizationId).Where(item => item.currencyDefinitionId == currencyId).Select(item => GetBalance(item.accountId, worldTime)).Where(item => item != null).Sum(item => item.BalanceUnits);
            OrganizationLiabilitySnapshot[] liabilities = QueryLiabilities(organizationId).Where(item => item.CurrencyId == currencyId).ToArray();
            IEnumerable<string> unvalued = QueryPropertyAssociations(organizationId, worldTime).Select(item => item.propertyId)
                .Concat(QueryBusinessAssociations(organizationId, worldTime).Select(item => item.businessId))
                .Concat(inventoryAssociationsById.Values.Where(item => item.organizationId == organizationId && item.IsActiveAt(worldTime)).Select(item => item.inventoryId));
            return new OrganizationResourceValuationSnapshot(organizationId, currencyId, cash, liabilities.Sum(item => item.ReceivableUnits), liabilities.Sum(item => item.PayableUnits), unvalued);
        }

        public OrganizationConsolidatedResourceSnapshot GetConsolidatedView(string rootOrganizationId, double worldTime)
        {
            HashSet<string> included = new HashSet<string>(StringComparer.Ordinal) { rootOrganizationId ?? string.Empty };
            Queue<string> pending = new Queue<string>(); pending.Enqueue(rootOrganizationId ?? string.Empty);
            while (pending.Count > 0)
            {
                foreach (OrganizationSnapshot child in organizations.QueryByParent(pending.Dequeue()))
                    if (included.Add(child.OrganizationId)) pending.Enqueue(child.OrganizationId);
            }
            OrganizationAccountBalanceSnapshot[] balances = accountsById.Values.Where(item => included.Contains(item.organizationId)).Select(item => GetBalance(item.accountId, worldTime)).Where(item => item != null).ToArray();
            return new OrganizationConsolidatedResourceSnapshot(rootOrganizationId, included, balances);
        }

        public OrganizationResourceProjection ProjectAccount(string accountId, OrganizationResourceProjectionAccess access, double worldTime)
        {
            if (!accountsById.TryGetValue(accountId ?? string.Empty, out OrganizationAccountRecordData account) || access == OrganizationResourceProjectionAccess.Denied) return new OrganizationResourceProjection(OrganizationResourceProjectionAccess.Denied, string.Empty, true, null, null);
            OrganizationTreasuryRecordData treasury = treasuriesById.TryGetValue(account.treasuryId, out OrganizationTreasuryRecordData foundTreasury) ? foundTreasury : null;
            if (access == OrganizationResourceProjectionAccess.Concealed) return new OrganizationResourceProjection(access, string.Empty, true, null, null);
            if (access == OrganizationResourceProjectionAccess.Redacted)
            {
                OrganizationAccountRecordData redacted = account.Clone(); redacted.officialName = string.Empty; redacted.economyAccountId = string.Empty; redacted.provenanceId = string.Empty;
                OrganizationAccountBalanceSnapshot hiddenBalance = new OrganizationAccountBalanceSnapshot(redacted, null, 0L, 0L, 0L);
                return new OrganizationResourceProjection(access, account.accountId, true, treasury, hiddenBalance);
            }
            return new OrganizationResourceProjection(access, account.accountId, false, treasury, GetBalance(account.accountId, worldTime));
        }

        public OrganizationReconciliationResult Reconcile(string organizationId, double worldTime)
        {
            List<OrganizationReconciliationDiscrepancy> discrepancies = new List<OrganizationReconciliationDiscrepancy>();
            foreach (OrganizationAccountRecordData account in accountsById.Values.Where(item => string.IsNullOrWhiteSpace(organizationId) || item.organizationId == organizationId).OrderBy(item => item.accountId, StringComparer.Ordinal))
            {
                if (!treasuriesById.TryGetValue(account.treasuryId, out OrganizationTreasuryRecordData treasury) || treasury.organizationId != account.organizationId) discrepancies.Add(Error("MissingTreasury", account.accountId, "Account references a missing or foreign treasury."));
                if (!economy.TryGetAccount(account.economyAccountId, out EconomyAccountSnapshot economyAccount)) { discrepancies.Add(Error("MissingEconomyAccount", account.accountId, "Organization account has no authoritative EconomyRuntime account.")); continue; }
                if (economyAccount.OwnerId != account.organizationId || economyAccount.CurrencyId != account.currencyDefinitionId) discrepancies.Add(Error("EconomyAccountMismatch", account.accountId, "Economy account owner or currency differs from organization metadata."));
                OrganizationAccountBalanceSnapshot balance = GetBalance(account.accountId, worldTime);
                if (balance != null && balance.RestrictedUnits + balance.ReservedUnits + balance.EncumberedUnits > balance.BalanceUnits) discrepancies.Add(Error("OverAllocated", account.accountId, "Restricted, reserved, and encumbered amounts exceed the authoritative balance."));
                if (account.lifecycleState == OrganizationAccountLifecycleState.Closed && (balance?.BalanceUnits ?? 0L) != 0L) discrepancies.Add(Error("ClosedAccountBalance", account.accountId, "Closed account retains a nonzero balance."));
            }
            foreach (OrganizationFundRestrictionRecordData restriction in restrictionsById.Values.OrderBy(item => item.restrictionId, StringComparer.Ordinal))
            {
                if (!accountsById.ContainsKey(restriction.accountId)) discrepancies.Add(Error("MissingRestrictionAccount", restriction.restrictionId, "Restriction references a missing account."));
                if (restriction.remainingUnits > restriction.originalUnits) discrepancies.Add(Error("InvalidRestrictionAmount", restriction.restrictionId, "Restriction remaining amount exceeds its original amount."));
            }
            foreach (OrganizationResourceReservationRecordData reservation in reservationsById.Values.OrderBy(item => item.reservationId, StringComparer.Ordinal))
            {
                if (!string.IsNullOrWhiteSpace(reservation.accountId) && !accountsById.ContainsKey(reservation.accountId)) discrepancies.Add(Error("MissingReservationAccount", reservation.reservationId, "Reservation references a missing account."));
            }
            EconomyRuntimeSaveData economyData = economy.CreateSaveData();
            foreach (IGrouping<string, EconomyLedgerEntryData> group in economyData.ledgerEntries.GroupBy(item => item.transactionId, StringComparer.Ordinal))
            {
                long debits = group.Where(item => item.kind == EconomyLedgerEntryKind.Debit).Sum(item => item.units);
                long credits = group.Where(item => item.kind == EconomyLedgerEntryKind.Credit).Sum(item => item.units);
                EconomyTransactionData transaction = economyData.transactions.FirstOrDefault(item => item.transactionId == group.Key);
                if (transaction != null && transaction.kind == EconomyTransactionKind.Transfer && debits != credits) discrepancies.Add(Error("UnbalancedLedger", group.Key, $"Transfer ledger debits {debits} do not equal credits {credits}."));
            }
            return new OrganizationReconciliationResult(discrepancies);
        }

        public OrganizationResourceRuntimeSaveData CreateSaveData()
        {
            return new OrganizationResourceRuntimeSaveData
            {
                worldId = worldId, revision = Revision,
                treasuries = Treasuries.ToList(), accounts = Accounts.ToList(), restrictions = Restrictions.ToList(), budgets = Budgets.ToList(), reservations = Reservations.ToList(),
                inventoryAssociations = InventoryAssociations.ToList(), propertyAssociations = PropertyAssociations.ToList(), businessAssociations = BusinessAssociations.ToList(), custodyRecords = CustodyRecords.ToList(), revenueRoutingRules = RevenueRoutingRules.ToList(),
                transactions = transactionsById.Values.OrderBy(item => item.worldTime).ThenBy(item => item.transactionId, StringComparer.Ordinal).Select(item => item.Clone()).RetainNewestTransactions().ToList(),
                dissolutionPlans = DissolutionPlans.ToList()
            };
        }

        public OrganizationResourceOperationResult RestoreFromSaveData(OrganizationResourceRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, OrganizationRuntime organizationRuntime, OrganizationAuthorityRuntime authorityRuntime, EconomyRuntime economyRuntime, string world, PropertyRuntime propertyRuntime = null, BusinessRuntime businessRuntime = null, ItemInstanceIdentityRuntime itemRuntime = null, bool restoring = true, ContractEconomyRuntime contractRuntime = null, PayrollRuntime payrollRuntime = null)
        {
            long before = Revision;
            if (!ValidateSaveData(saveData, definitionRegistry, organizationRuntime, economyRuntime, world, propertyRuntime, businessRuntime, itemRuntime, out string failure)) return Fail(OrganizationResourceOperationCode.RestoreFailed, failure, false);
            OrganizationResourceRuntimeSaveData rollback = CreateSaveData();
            DefinitionRegistry priorRegistry = registry; OrganizationRuntime priorOrganizations = organizations; OrganizationAuthorityRuntime priorAuthority = authority; EconomyRuntime priorEconomy = economy; PropertyRuntime priorProperties = properties; BusinessRuntime priorBusinesses = businesses; ItemInstanceIdentityRuntime priorItems = items; ContractEconomyRuntime priorContracts = contracts; PayrollRuntime priorPayroll = payroll; string priorWorld = worldId;
            try
            {
                Configure(definitionRegistry, organizationRuntime, authorityRuntime, economyRuntime, world, propertyRuntime, businessRuntime, itemRuntime, contractRuntime ?? priorContracts, payrollRuntime ?? priorPayroll);
                RestoreLocal(saveData);
                IsDirty = !restoring;
                return OrganizationResourceOperationResult.Success("Organization resources restored without replaying economic mutations.", before, Revision);
            }
            catch (Exception exception)
            {
                registry = priorRegistry; organizations = priorOrganizations; authority = priorAuthority; economy = priorEconomy; properties = priorProperties; businesses = priorBusinesses; items = priorItems; contracts = priorContracts; payroll = priorPayroll; worldId = priorWorld;
                RestoreLocal(rollback);
                return Fail(OrganizationResourceOperationCode.RestoreFailed, $"Organization resource restore failed and rolled back: {exception.Message}", false);
            }
        }

        public void Reset()
        {
            ClearOwnedState(); eventDeliveryDiagnostics.Clear(); Revision = 0L; IsDirty = false; disposed = false;
        }

        public void Dispose()
        {
            ClearOwnedState(); eventDeliveryDiagnostics.Clear(); OperationCommitted = null; registry = null; organizations = null; authority = null; economy = null; properties = null; businesses = null; items = null; contracts = null; payroll = null; worldId = string.Empty; Revision = 0L; IsDirty = false; disposed = true;
        }

        public static bool ValidateSaveData(OrganizationResourceRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, OrganizationRuntime organizationRuntime, EconomyRuntime economyRuntime, string world, PropertyRuntime propertyRuntime, BusinessRuntime businessRuntime, ItemInstanceIdentityRuntime itemRuntime, out string failure)
        {
            failure = string.Empty;
            if (saveData == null) return Invalid("Organization resource save data is missing.", out failure);
            if (saveData.schemaVersion != OrganizationResourceRuntimeSaveData.CurrentSchemaVersion) return Invalid($"Organization resource schema version '{saveData.schemaVersion}' is unsupported.", out failure);
            if (definitionRegistry == null || organizationRuntime == null || economyRuntime == null) return Invalid("Definition, Organization, and Economy runtimes are required to validate organization resources.", out failure);
            if (string.IsNullOrWhiteSpace(world) || !string.Equals(saveData.worldId ?? string.Empty, world, StringComparison.Ordinal)) return Invalid("Organization resource save world does not match the active world.", out failure);

            OrganizationResourceRuntimeSaveData clean = saveData.Clone();
            if (!Unique(clean.treasuries, item => item.treasuryId, "treasury", out failure)
                || !Unique(clean.accounts, item => item.accountId, "account", out failure)
                || !Unique(clean.restrictions, item => item.restrictionId, "restriction", out failure)
                || !Unique(clean.budgets, item => item.budgetId, "budget", out failure)
                || !Unique(clean.reservations, item => item.reservationId, "reservation", out failure)
                || !Unique(clean.inventoryAssociations, item => item.associationId, "inventory association", out failure)
                || !Unique(clean.propertyAssociations, item => item.associationId, "property association", out failure)
                || !Unique(clean.businessAssociations, item => item.associationId, "business association", out failure)
                || !Unique(clean.custodyRecords, item => item.custodyId, "custody record", out failure)
                || !Unique(clean.revenueRoutingRules, item => item.routingRuleId, "routing rule", out failure)
                || !Unique(clean.dissolutionPlans, item => item.planId, "dissolution plan", out failure)
                || !Unique(clean.transactions, item => item.transactionId, "transaction", out failure)) return false;

            Dictionary<string, OrganizationTreasuryRecordData> treasuries = clean.treasuries.ToDictionary(item => item.treasuryId, StringComparer.Ordinal);
            Dictionary<string, OrganizationAccountRecordData> accounts = clean.accounts.ToDictionary(item => item.accountId, StringComparer.Ordinal);
            foreach (OrganizationTreasuryRecordData treasury in clean.treasuries)
            {
                if (!ExistsOrganization(organizationRuntime, treasury.organizationId)) return Invalid($"Treasury '{treasury.treasuryId}' references a missing Organization.", out failure);
                if (!definitionRegistry.TryGet(treasury.resourceTypeDefinitionId, out OrganizationResourceTypeDefinition type) || type.Category != OrganizationResourceCategory.Currency) return Invalid($"Treasury '{treasury.treasuryId}' has an invalid currency resource type.", out failure);
            }
            foreach (OrganizationAccountRecordData account in clean.accounts)
            {
                if (!treasuries.TryGetValue(account.treasuryId, out OrganizationTreasuryRecordData treasury) || treasury.organizationId != account.organizationId) return Invalid($"Account '{account.accountId}' references a missing or foreign treasury.", out failure);
                if (!definitionRegistry.TryGet(account.currencyDefinitionId, out CurrencyDefinition _)) return Invalid($"Account '{account.accountId}' references missing currency '{account.currencyDefinitionId}'.", out failure);
                if (!economyRuntime.TryGetAccount(account.economyAccountId, out EconomyAccountSnapshot economyAccount) || economyAccount.OwnerId != account.organizationId || economyAccount.CurrencyId != account.currencyDefinitionId || economyAccount.Data.kind != EconomyAccountKind.OrganizationAccount) return Invalid($"Account '{account.accountId}' does not match its authoritative Economy account.", out failure);
                EconomyAccountState expectedState = account.lifecycleState == OrganizationAccountLifecycleState.Active ? EconomyAccountState.Active : account.lifecycleState == OrganizationAccountLifecycleState.Frozen ? EconomyAccountState.Frozen : account.lifecycleState == OrganizationAccountLifecycleState.Closed ? EconomyAccountState.Closed : economyAccount.Data.state;
                if (economyAccount.Data.state != expectedState) return Invalid($"Account '{account.accountId}' lifecycle differs from its authoritative Economy account.", out failure);
            }
            foreach (OrganizationFundRestrictionRecordData restriction in clean.restrictions)
            {
                if (!accounts.TryGetValue(restriction.accountId, out OrganizationAccountRecordData account) || account.organizationId != restriction.organizationId || account.currencyDefinitionId != restriction.currencyDefinitionId || restriction.originalUnits < 0L || restriction.remainingUnits < 0L || restriction.remainingUnits > restriction.originalUnits) return Invalid($"Restriction '{restriction.restrictionId}' has an invalid account, currency, or amount.", out failure);
            }
            foreach (OrganizationBudgetRecordData budget in clean.budgets)
            {
                if (!accounts.TryGetValue(budget.accountId, out OrganizationAccountRecordData account) || account.organizationId != budget.organizationId || account.treasuryId != budget.treasuryId || account.currencyDefinitionId != budget.currencyDefinitionId || budget.authorizedUnits < 0L) return Invalid($"Budget '{budget.budgetId}' has an invalid account scope or amount.", out failure);
            }
            foreach (OrganizationResourceReservationRecordData reservation in clean.reservations)
            {
                if (!string.IsNullOrWhiteSpace(reservation.accountId) && (!accounts.TryGetValue(reservation.accountId, out OrganizationAccountRecordData account) || account.organizationId != reservation.organizationId || account.currencyDefinitionId != reservation.currencyDefinitionId || reservation.amountUnits <= 0L)) return Invalid($"Reservation '{reservation.reservationId}' has an invalid monetary account scope.", out failure);
                if (string.IsNullOrWhiteSpace(reservation.accountId) && !ValidateAssetReference(reservation.resource, world, propertyRuntime, businessRuntime, itemRuntime, treasuries, accounts, out failure)) return false;
            }
            foreach (OrganizationPropertyAssociationRecordData association in clean.propertyAssociations)
            {
                if (!ExistsOrganization(organizationRuntime, association.organizationId) || propertyRuntime == null || !propertyRuntime.TryGetProperty(association.propertyId, out _)) return Invalid($"Property association '{association.associationId}' has a missing owner or property.", out failure);
                if ((association.category == OrganizationPropertyAssociationCategory.Owner || association.category == OrganizationPropertyAssociationCategory.CoOwner) && !PropertyOwnershipMatches(propertyRuntime, association.organizationId, association.propertyId, association.ownershipRecordId, association.startWorldTime)) return Invalid($"Property association '{association.associationId}' lacks a matching Step 11 ownership record.", out failure);
            }
            foreach (OrganizationBusinessAssociationRecordData association in clean.businessAssociations)
            {
                if (!ExistsOrganization(organizationRuntime, association.organizationId) || businessRuntime == null || !businessRuntime.TryGetBusiness(association.businessId, out _)) return Invalid($"Business association '{association.associationId}' has a missing owner or business.", out failure);
                if ((association.category == OrganizationBusinessAssociationCategory.Owner || association.category == OrganizationBusinessAssociationCategory.PartialOwner) && !BusinessOwnershipMatches(businessRuntime, association.organizationId, association.businessId, association.ownershipRecordId, association.startWorldTime)) return Invalid($"Business association '{association.associationId}' lacks a matching Step 11 ownership record.", out failure);
            }
            foreach (OrganizationAssetCustodyRecordData custody in clean.custodyRecords)
            {
                if (!ExistsOrganization(organizationRuntime, custody.organizationId) || !ValidateAssetReference(custody.asset, world, propertyRuntime, businessRuntime, itemRuntime, treasuries, accounts, out failure)) return false;
                if (string.IsNullOrWhiteSpace(custody.custodianPersonId) == string.IsNullOrWhiteSpace(custody.custodianOrganizationId)) return Invalid($"Custody '{custody.custodyId}' must identify exactly one custodian.", out failure);
            }
            foreach (OrganizationRevenueRoutingRuleData rule in clean.revenueRoutingRules)
            {
                if (!accounts.TryGetValue(rule.destinationAccountId, out OrganizationAccountRecordData account) || account.organizationId != rule.organizationId || rule.percentageBasisPoints < 0L || rule.percentageBasisPoints > 10000L || rule.fixedUnits < 0L) return Invalid($"Routing rule '{rule.routingRuleId}' is invalid.", out failure);
            }
            foreach (OrganizationDissolutionResourcePlanData plan in clean.dissolutionPlans)
            {
                if (!ExistsOrganization(organizationRuntime, plan.organizationId) || plan.accountIdsToFreeze.Any(id => !accounts.TryGetValue(id, out OrganizationAccountRecordData account) || account.organizationId != plan.organizationId)) return Invalid($"Dissolution plan '{plan.planId}' has an invalid Organization or account scope.", out failure);
                foreach (OrganizationDissolutionAssetInstructionData instruction in plan.assetInstructions ?? Array.Empty<OrganizationDissolutionAssetInstructionData>())
                    if (instruction == null || !ValidateAssetReference(instruction.asset, world, propertyRuntime, businessRuntime, itemRuntime, treasuries, accounts, out failure)) return false;
            }
            return true;
        }

        private bool CanMutate(out OrganizationResourceOperationResult failure)
        {
            failure = null;
            if (disposed) { failure = Fail(OrganizationResourceOperationCode.Disposed, "Organization resource runtime is disposed.", false); return false; }
            if (!IsReady) { failure = Fail(OrganizationResourceOperationCode.MissingDependency, "Organization resource dependencies are not configured.", false); return false; }
            return true;
        }

        private bool TryOrganization(string organizationId, double worldTime, bool ordinaryOperation, out OrganizationSnapshot snapshot, out string failure)
        {
            failure = string.Empty;
            if (!organizations.TryGetSnapshot(organizationId ?? string.Empty, out snapshot)) { failure = $"Organization '{organizationId}' was not found."; return false; }
            if (ordinaryOperation && snapshot.LifecycleState != OrganizationLifecycleState.Active) { failure = $"Organization '{organizationId}' is {snapshot.LifecycleState}; ordinary resource operations require Active."; return false; }
            return true;
        }

        private OrganizationAuthorizationResult Authorize(string operationId, string actorPersonId, string organizationId, string actionDefinitionId, string targetRecordId, IEnumerable<string> approvalPersonIds, double worldTime, bool preview, bool consume)
        {
            return authority.EvaluateAuthorization(new OrganizationAuthorizationRequest
            {
                operationId = operationId ?? string.Empty,
                actorPersonId = actorPersonId ?? string.Empty,
                organizationId = organizationId ?? string.Empty,
                actionDefinitionId = actionDefinitionId ?? string.Empty,
                scope = OrganizationAuthorityScopeData.ForOrganization(organizationId),
                targetRecordId = targetRecordId ?? string.Empty,
                approvalPersonIds = OrganizationModelUtility.Clean(approvalPersonIds),
                consumeApprovals = consume,
                worldTime = worldTime,
                preview = preview
            });
        }

        private static string Action(string requested, string fallback) => string.IsNullOrWhiteSpace(requested) ? fallback : requested.Trim();

        private OrganizationResourceOperationResult Unauthorized(OrganizationAuthorizationResult authorization, bool preview) => Fail(OrganizationResourceOperationCode.Unauthorized, authorization?.Message ?? "Organization resource authorization failed.", preview, authorization);

        private OrganizationResourceOperationResult FinancialFailure(EconomyOperationResult result, bool preview, OrganizationAuthorizationResult authorization)
        {
            OrganizationResourceOperationCode code = result?.Code == EconomyResultCode.InsufficientFunds ? OrganizationResourceOperationCode.InsufficientFunds
                : result?.Code == EconomyResultCode.AccountFrozen ? OrganizationResourceOperationCode.AccountFrozen
                : result?.Code == EconomyResultCode.AccountClosed ? OrganizationResourceOperationCode.AccountClosed
                : result?.Code == EconomyResultCode.MissingAccount ? OrganizationResourceOperationCode.MissingAccount
                : result?.Code == EconomyResultCode.MissingCurrency || result?.Code == EconomyResultCode.CurrencyMismatch ? OrganizationResourceOperationCode.MissingCurrency
                : result?.Code == EconomyResultCode.ReservationUnavailable || result?.Code == EconomyResultCode.MissingReservation ? OrganizationResourceOperationCode.ReservationUnavailable
                : OrganizationResourceOperationCode.FinanciallyInvalid;
            return Fail(code, result?.Message ?? "Economy operation failed.", preview, authorization);
        }

        private OrganizationResourceOperationResult Fail(OrganizationResourceOperationCode code, string message, bool preview, OrganizationAuthorizationResult authorization = null) => OrganizationResourceOperationResult.Failure(code, message, Revision, preview, authorization);

        private OrganizationResourceOperationResult DuplicateOrConflict(string transactionId, string operation, string subjectId, bool same, long before, bool preview, OrganizationTreasuryRecordData treasury = null, OrganizationAccountRecordData account = null)
        {
            if (!same) return Fail(OrganizationResourceOperationCode.InvalidRequest, $"'{subjectId}' already exists with different data.", preview);
            if (string.IsNullOrWhiteSpace(transactionId) || !transactionsById.TryGetValue(transactionId, out OrganizationResourceTransactionRecordData transaction)) return Fail(OrganizationResourceOperationCode.InvalidRequest, $"'{subjectId}' already exists; replay requires its original transaction ID.", preview);
            if (transaction.operation != operation || transaction.subjectId != subjectId) return Fail(OrganizationResourceOperationCode.InvalidRequest, $"Transaction '{transactionId}' was already used for another operation.", preview);
            return OrganizationResourceOperationResult.Success("Organization resource operation already applied.", before, before, duplicate: true, treasury: treasury, account: account, subjectId: subjectId);
        }

        private void Commit(string transactionId, string operation, string subjectId, string economyTransactionId, string organizationId, string sourceAccountId = "", string destinationAccountId = "", string budgetId = "", string restrictionId = "", string purpose = "", string currencyId = "", long units = 0L, double worldTime = 0d)
        {
            if (string.IsNullOrWhiteSpace(transactionId)) throw new InvalidOperationException("A stable transaction ID is required.");
            if (transactionsById.ContainsKey(transactionId)) throw new InvalidOperationException($"Transaction '{transactionId}' was already committed.");
            OrganizationResourceTransactionRecordData committed = new OrganizationResourceTransactionRecordData
            {
                transactionId = transactionId.Trim(), operation = operation ?? string.Empty, subjectId = subjectId ?? string.Empty, economyTransactionId = economyTransactionId ?? string.Empty,
                organizationId = organizationId ?? string.Empty, sourceAccountId = sourceAccountId ?? string.Empty, destinationAccountId = destinationAccountId ?? string.Empty,
                budgetId = budgetId ?? string.Empty, restrictionId = restrictionId ?? string.Empty, purpose = purpose ?? string.Empty, currencyDefinitionId = currencyId ?? string.Empty,
                units = Math.Max(0L, units), worldTime = worldTime, code = OrganizationResourceOperationCode.Success
            };
            transactionsById.Add(transactionId, committed);
            Revision++;
            IsDirty = true;
            PublishCommitted(committed);
        }

        private void RestoreDependencies(EconomyRuntimeSaveData economySave, OrganizationAuthorityRuntimeSaveData authoritySave)
        {
            EconomyOperationResult economyResult = economy.RestoreFromSaveData(economySave, registry);
            OrganizationAuthorityOperationResult authorityResult = authority.RestoreCheckpoint(authoritySave);
            if (!economyResult.Succeeded || !authorityResult.Succeeded) throw new InvalidOperationException($"Cross-runtime rollback failed. Economy={economyResult.Message} Authority={authorityResult.Message}");
        }

        private void PublishCommitted(OrganizationResourceTransactionRecordData transaction)
        {
            Action<OrganizationResourceCommittedEvent> handlers = OperationCommitted;
            if (handlers == null) return;
            OrganizationResourceCommittedEvent payload = new OrganizationResourceCommittedEvent(transaction, Revision);
            foreach (Action<OrganizationResourceCommittedEvent> handler in handlers.GetInvocationList().Cast<Action<OrganizationResourceCommittedEvent>>())
            {
                try { handler(payload); }
                catch (Exception exception)
                {
                    eventDeliveryDiagnostics.Add($"{transaction.transactionId}:{handler.Method.DeclaringType?.FullName}.{handler.Method.Name}:{exception.GetType().Name}:{exception.Message}");
                    if (eventDeliveryDiagnostics.Count > 32) eventDeliveryDiagnostics.RemoveAt(0);
                }
            }
        }

        private void RestoreLocal(OrganizationResourceRuntimeSaveData saveData)
        {
            ClearOwnedState();
            OrganizationResourceRuntimeSaveData clean = saveData?.Clone() ?? new OrganizationResourceRuntimeSaveData();
            foreach (OrganizationTreasuryRecordData item in clean.treasuries) treasuriesById.Add(item.treasuryId, item);
            foreach (OrganizationAccountRecordData item in clean.accounts) accountsById.Add(item.accountId, item);
            foreach (OrganizationFundRestrictionRecordData item in clean.restrictions) restrictionsById.Add(item.restrictionId, item);
            foreach (OrganizationBudgetRecordData item in clean.budgets) budgetsById.Add(item.budgetId, item);
            foreach (OrganizationResourceReservationRecordData item in clean.reservations) reservationsById.Add(item.reservationId, item);
            foreach (OrganizationInventoryAssociationRecordData item in clean.inventoryAssociations) inventoryAssociationsById.Add(item.associationId, item);
            foreach (OrganizationPropertyAssociationRecordData item in clean.propertyAssociations) propertyAssociationsById.Add(item.associationId, item);
            foreach (OrganizationBusinessAssociationRecordData item in clean.businessAssociations) businessAssociationsById.Add(item.associationId, item);
            foreach (OrganizationAssetCustodyRecordData item in clean.custodyRecords) custodyById.Add(item.custodyId, item);
            foreach (OrganizationRevenueRoutingRuleData item in clean.revenueRoutingRules) routingById.Add(item.routingRuleId, item);
            foreach (OrganizationResourceTransactionRecordData item in clean.transactions) transactionsById.Add(item.transactionId, item);
            foreach (OrganizationDissolutionResourcePlanData item in clean.dissolutionPlans) dissolutionPlansById.Add(item.planId, item);
            Revision = Math.Max(0L, clean.revision);
        }

        private void ClearOwnedState()
        {
            treasuriesById.Clear(); accountsById.Clear(); restrictionsById.Clear(); budgetsById.Clear(); reservationsById.Clear(); inventoryAssociationsById.Clear(); propertyAssociationsById.Clear(); businessAssociationsById.Clear(); custodyById.Clear(); routingById.Clear(); transactionsById.Clear(); dissolutionPlansById.Clear();
        }

        private static bool ValidateRequired(string transactionId, string subjectId, string organizationId, out string failure)
        {
            failure = string.Empty;
            if (!string.IsNullOrWhiteSpace(transactionId) && !string.IsNullOrWhiteSpace(subjectId) && !string.IsNullOrWhiteSpace(organizationId)) return true;
            failure = "Transaction, subject, and Organization IDs are required.";
            return false;
        }

        private static bool ValidAccountTransition(OrganizationAccountLifecycleState current, OrganizationAccountLifecycleState target) =>
            current == OrganizationAccountLifecycleState.Active && (target == OrganizationAccountLifecycleState.Frozen || target == OrganizationAccountLifecycleState.Closed)
            || current == OrganizationAccountLifecycleState.Frozen && (target == OrganizationAccountLifecycleState.Active || target == OrganizationAccountLifecycleState.Closed);

        private IEnumerable<OrganizationFundRestrictionRecordData> ActiveRestrictions(string accountId, double worldTime) => restrictionsById.Values.Where(item => item.accountId == accountId && item.IsActiveAt(worldTime));

        private bool ValidateSpend(OrganizationFinancialTransactionRequest request, OrganizationAccountRecordData source, out OrganizationResourceOperationResult failure)
        {
            failure = null;
            if (source == null) return true;
            OrganizationAccountBalanceSnapshot balance = GetBalance(source.accountId, request.worldTime);
            if (balance == null || balance.AvailableUnits < request.units)
            {
                bool usingRestriction = !string.IsNullOrWhiteSpace(request.restrictionId) && restrictionsById.TryGetValue(request.restrictionId, out OrganizationFundRestrictionRecordData restriction) && restriction.IsActiveAt(request.worldTime);
                bool usingReservation = !string.IsNullOrWhiteSpace(request.reservationId) && reservationsById.TryGetValue(request.reservationId, out OrganizationResourceReservationRecordData reservation) && reservation.IsActiveAt(request.worldTime);
                if (!usingRestriction && !usingReservation) { failure = Fail(OrganizationResourceOperationCode.InsufficientFunds, "Available organization funds are insufficient after restrictions and reservations.", request.preview); return false; }
            }
            if (!string.IsNullOrWhiteSpace(request.restrictionId))
            {
                if (!restrictionsById.TryGetValue(request.restrictionId, out OrganizationFundRestrictionRecordData restriction) || restriction.accountId != source.accountId || !restriction.IsActiveAt(request.worldTime) || restriction.remainingUnits < request.units) { failure = Fail(OrganizationResourceOperationCode.RestrictionMismatch, "The selected restriction cannot fund this transaction.", request.preview); return false; }
                if (!string.IsNullOrWhiteSpace(restriction.allowedPurpose) && !string.Equals(restriction.allowedPurpose, request.purpose ?? string.Empty, StringComparison.Ordinal)) { failure = Fail(OrganizationResourceOperationCode.RestrictionMismatch, "The transaction purpose does not match the fund restriction.", request.preview); return false; }
                if (restriction.allowedRecipientIds.Length > 0 && !restriction.allowedRecipientIds.Contains(request.destinationAccountId ?? string.Empty, StringComparer.Ordinal)) { failure = Fail(OrganizationResourceOperationCode.RestrictionMismatch, "The destination is not permitted by the fund restriction.", request.preview); return false; }
            }
            if (!string.IsNullOrWhiteSpace(request.budgetId))
            {
                if (!budgetsById.TryGetValue(request.budgetId, out OrganizationBudgetRecordData budget) || budget.accountId != source.accountId || !budget.IsActiveAt(request.worldTime)) { failure = Fail(OrganizationResourceOperationCode.MissingBudget, "The selected budget is not active for the source account.", request.preview); return false; }
                long projected = GetBudgetSpentUnits(budget.budgetId) + request.units;
                if (budget.enforcementPolicy == OrganizationBudgetEnforcementPolicy.HardMaximum && projected > budget.authorizedUnits) { failure = Fail(OrganizationResourceOperationCode.BudgetExceeded, "The transaction exceeds the hard budget maximum.", request.preview); return false; }
                if (budget.enforcementPolicy == OrganizationBudgetEnforcementPolicy.RestrictedToPurpose && !string.Equals(budget.purpose ?? string.Empty, request.purpose ?? string.Empty, StringComparison.Ordinal)) { failure = Fail(OrganizationResourceOperationCode.BudgetExceeded, "The transaction purpose does not match the budget.", request.preview); return false; }
            }
            return true;
        }

        private string EconomyReservationId(string reservationId) => !string.IsNullOrWhiteSpace(reservationId) && reservationsById.TryGetValue(reservationId, out OrganizationResourceReservationRecordData reservation) ? reservation.economyReservationId : string.Empty;

        private bool ValidAssetReference(OrganizationAssetReferenceData asset, out string failure) => ValidateAssetReference(asset, worldId, properties, businesses, items, treasuriesById, accountsById, out failure);

        private bool ValidateAssociationRequest(OrganizationAssociationRequest request, out string failure)
        {
            failure = string.Empty;
            if (request == null || string.IsNullOrWhiteSpace(request.transactionId) || string.IsNullOrWhiteSpace(request.associationId) || string.IsNullOrWhiteSpace(request.organizationId) || string.IsNullOrWhiteSpace(request.resourceId)) { failure = "Transaction, association, Organization, and resource IDs are required."; return false; }
            if (request.endWorldTime >= 0d && request.endWorldTime <= request.startWorldTime) { failure = "Association end must be after its start."; return false; }
            if (!TryOrganization(request.organizationId, request.startWorldTime, true, out _, out failure)) return false;
            return true;
        }

        private bool HasPropertyOwnership(string organizationId, string propertyId, string ownershipRecordId, double worldTime) => PropertyOwnershipMatches(properties, organizationId, propertyId, ownershipRecordId, worldTime);
        private bool HasBusinessOwnership(string organizationId, string businessId, string ownershipRecordId, double worldTime) => BusinessOwnershipMatches(businesses, organizationId, businessId, ownershipRecordId, worldTime);

        private EconomyTransactionSnapshot EconomyTransaction(string transactionId)
        {
            if (string.IsNullOrWhiteSpace(transactionId)) return null;
            EconomyRuntimeSaveData save = economy.CreateSaveData();
            EconomyTransactionData transaction = save.transactions.FirstOrDefault(item => item.transactionId == transactionId);
            return transaction == null ? null : new EconomyTransactionSnapshot(transaction, save.ledgerEntries.Where(item => item.transactionId == transactionId));
        }

        private static bool IsEncumbrance(OrganizationReservationCategory category) => category == OrganizationReservationCategory.Contract || category == OrganizationReservationCategory.Loan || category == OrganizationReservationCategory.Obligation;
        private static OrganizationReconciliationDiscrepancy Error(string code, string subjectId, string message) => new OrganizationReconciliationDiscrepancy(code, OrganizationReconciliationSeverity.Error, subjectId, message);
        private static IEnumerable<T> Ordered<T>(IEnumerable<T> source, Func<T, double> worldTime, Func<T, string> id) => (source ?? Array.Empty<T>()).OrderBy(worldTime).ThenBy(id, StringComparer.Ordinal);

        private static bool ActiveOrganization(OrganizationRuntime runtime, string organizationId) => runtime.TryGetSnapshot(organizationId ?? string.Empty, out OrganizationSnapshot snapshot) && snapshot.LifecycleState == OrganizationLifecycleState.Active;
        private static bool ExistsOrganization(OrganizationRuntime runtime, string organizationId) => runtime != null && runtime.TryGetSnapshot(organizationId ?? string.Empty, out _);

        private static bool PropertyOwnershipMatches(PropertyRuntime runtime, string organizationId, string propertyId, string ownershipRecordId, double worldTime)
        {
            if (runtime == null) return false;
            return runtime.OwnershipInterests.Any(item => item.propertyId == propertyId && item.owner != null && item.owner.kind == PropertySubjectKind.Organization && item.owner.subjectId == organizationId && item.IsActiveAt(worldTime) && (string.IsNullOrWhiteSpace(ownershipRecordId) || item.ownershipInterestId == ownershipRecordId));
        }

        private static bool BusinessOwnershipMatches(BusinessRuntime runtime, string organizationId, string businessId, string ownershipRecordId, double worldTime)
        {
            if (runtime == null) return false;
            return runtime.OwnershipRecords.Any(item => item.businessId == businessId && item.owner != null && item.owner.kind == BusinessOwnerSubjectKind.Organization && item.owner.subjectId == organizationId && item.ActiveAt(worldTime) && (string.IsNullOrWhiteSpace(ownershipRecordId) || item.ownershipRecordId == ownershipRecordId));
        }

        private static bool ValidateAssetReference(OrganizationAssetReferenceData asset, string world, PropertyRuntime propertyRuntime, BusinessRuntime businessRuntime, ItemInstanceIdentityRuntime itemRuntime, IReadOnlyDictionary<string, OrganizationTreasuryRecordData> treasuries, IReadOnlyDictionary<string, OrganizationAccountRecordData> accounts, out string failure)
        {
            failure = string.Empty;
            if (asset == null || asset.kind == OrganizationAssetReferenceKind.Unknown || string.IsNullOrWhiteSpace(asset.resourceId)) return Invalid("Typed asset reference is incomplete.", out failure);
            if (!string.IsNullOrWhiteSpace(asset.worldId) && !string.Equals(asset.worldId, world, StringComparison.Ordinal)) return Invalid($"Asset '{asset.resourceId}' belongs to another world.", out failure);
            bool exists = asset.kind == OrganizationAssetReferenceKind.Treasury ? treasuries.ContainsKey(asset.resourceId)
                : asset.kind == OrganizationAssetReferenceKind.Account || asset.kind == OrganizationAssetReferenceKind.CurrencyBalance ? accounts.ContainsKey(asset.resourceId)
                : asset.kind == OrganizationAssetReferenceKind.ItemInstance ? itemRuntime != null && itemRuntime.TryGetSnapshot(asset.resourceId, out _)
                : asset.kind == OrganizationAssetReferenceKind.Property || asset.kind == OrganizationAssetReferenceKind.Building || asset.kind == OrganizationAssetReferenceKind.LandParcel ? propertyRuntime != null && propertyRuntime.TryGetProperty(asset.resourceId, out _)
                : asset.kind == OrganizationAssetReferenceKind.Business ? businessRuntime != null && businessRuntime.TryGetBusiness(asset.resourceId, out _)
                : asset.kind == OrganizationAssetReferenceKind.Inventory || asset.kind == OrganizationAssetReferenceKind.Contract || asset.kind == OrganizationAssetReferenceKind.Loan || asset.kind == OrganizationAssetReferenceKind.Receivable || asset.kind == OrganizationAssetReferenceKind.Obligation || asset.kind == OrganizationAssetReferenceKind.Custom;
            if (!exists) return Invalid($"Typed asset '{asset.StableKey}' does not resolve in its authoritative runtime.", out failure);
            return true;
        }

        private static bool Unique<T>(IEnumerable<T> source, Func<T, string> id, string label, out string failure) where T : class
        {
            failure = string.Empty;
            T[] values = (source ?? Array.Empty<T>()).ToArray();
            if (values.Any(item => item == null || string.IsNullOrWhiteSpace(id(item)))) return Invalid($"Every {label} requires a stable ID.", out failure);
            string duplicate = values.GroupBy(id, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1)?.Key;
            return string.IsNullOrWhiteSpace(duplicate) || Invalid($"Duplicate {label} ID '{duplicate}'.", out failure);
        }

        private static bool Invalid(string message, out string failure) { failure = message; return false; }
    }
}
