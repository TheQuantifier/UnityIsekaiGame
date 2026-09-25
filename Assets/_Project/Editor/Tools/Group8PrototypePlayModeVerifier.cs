using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityIsekaiGame.Contracts;
using UnityIsekaiGame.Economy;
using UnityIsekaiGame.Economy.Businesses;
using UnityIsekaiGame.Economy.InstitutionalRevenue;
using UnityIsekaiGame.Economy.Markets;
using UnityIsekaiGame.Economy.Payroll;
using UnityIsekaiGame.Economy.Properties;
using UnityIsekaiGame.Economy.RegionalFlow;
using UnityIsekaiGame.Economy.Trading;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Inventory.Composition;
using UnityIsekaiGame.Inventory.Durability;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Inventory.Production;
using UnityIsekaiGame.Inventory.Quality;
using UnityIsekaiGame.Progression;

namespace UnityIsekaiGame.Editor
{
    [InitializeOnLoad]
    public static class Group8PrototypePlayModeVerifier
    {
        private const string ActiveKey = "UnityIsekaiGame.Group8.PlayModeVerification.Active";
        private const string ScenePath = "Assets/_Project/Scenes/Prototype/PrototypeScene.unity";
        private const string CatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";
        private static int framesInPlayMode;
        private static bool verificationSucceeded;

        static Group8PrototypePlayModeVerifier()
        {
            if (SessionState.GetBool(ActiveKey, false)) Subscribe();
        }

        public static void VerifyBatch()
        {
            SessionState.SetBool(ActiveKey, true);
            framesInPlayMode = 0;
            verificationSucceeded = false;
            Subscribe();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        private static void Subscribe()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode) framesInPlayMode = 0;
            if (change != PlayModeStateChange.EnteredEditMode) return;
            SessionState.EraseBool(ActiveKey);
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.Exit(verificationSucceeded ? 0 : 1);
        }

        private static void OnEditorUpdate()
        {
            if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying) return;
            if (++framesInPlayMode < 8) return;
            EditorApplication.update -= OnEditorUpdate;
            try
            {
                VerifyLiveEconomy();
                verificationSucceeded = true;
                Debug.Log("Group 8 Prototype play-mode verification passed: persistence, atomic rollback, exact secondhand stock, explicit aggregation, recipe-lot materialization, background trade, tax, and weapon exports verified.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                verificationSucceeded = false;
            }
            finally
            {
                EditorApplication.isPlaying = false;
            }
        }

        private static void VerifyLiveEconomy()
        {
            PrototypePersistenceServiceBehaviour services = UnityEngine.Object.FindAnyObjectByType<PrototypePersistenceServiceBehaviour>();
            PrototypeMarketStall stall = UnityEngine.Object.FindAnyObjectByType<PrototypeMarketStall>();
            PlayerInventory inventory = UnityEngine.Object.FindAnyObjectByType<PlayerInventory>();
            Require(services != null, "Prototype persistence/economy services were not found.");
            Require(stall != null, "Prototype market stall was not found.");
            Require(inventory != null, "Player inventory was not found.");
            Collider stallCollider = stall.GetComponent<Collider>();
            Require(stallCollider != null && stallCollider.enabled && stallCollider.isTrigger, "Prototype market stall needs an enabled trigger collider.");

            services.EnsureInitialized();
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            DefinitionRegistry registry = catalog?.CreateRegistry();
            Require(registry != null, "Prototype catalog could not create a definition registry.");
            foreach (string id in PrototypeEconomyContentIds.RequiredDefinitionIds)
            {
                Require(registry.DefinitionsById.ContainsKey(id), $"Required economy definition '{id}' is missing.");
            }

            Require(services.Economy.TryGetAccount(services.PlayerEconomyAccountId, out _), "Player EconomyRuntime account is missing.");
            Require(services.Economy.TryGetAccount(PrototypeEconomyContentIds.MerchantAccount, out _), "Merchant EconomyRuntime account is missing.");
            Require(services.Economy.TryGetAccount(PrototypeEconomyContentIds.ExternalTradeAccount, out _), "External aggregate-trade account is missing.");
            Require(services.Businesses.TryGetBusiness(PrototypeEconomyContentIds.BusinessInstance, out _), "Prototype workshop business is missing.");
            Require(services.Properties.TryGetProperty(PrototypeEconomyContentIds.PropertyInstance, out _), "Prototype workshop property is missing.");
            Require(services.InstitutionalRevenue.Authorities.Any(item => item.authorityId == PrototypeEconomyContentIds.RevenueAuthority), "Prototype town revenue authority is missing.");
            PrototypeMarketListing[] listings = services.GetPrototypeMarketListings().ToArray();
            Require(listings.Length == 4, "Prototype market should expose exactly two imports and two exports.");
            foreach (PrototypeMarketListing listing in listings.Where(entry => PrototypeEconomyContentIds.IsTownExport(entry.ItemDefinitionId) && entry.AvailableStock >= 2L))
            {
                double secondhandShare = listing.ExactSecondhandStock / (double)listing.AvailableStock;
                Require(secondhandShare >= 0.15d && secondhandShare <= 0.50d,
                    $"{listing.DisplayName} secondhand stock share {secondhandShare:P1} is outside the configured 15%-50% range.");
            }
            BusinessStockClassificationData generatedSecondhand = services.Businesses.StockClassifications.FirstOrDefault(stock =>
                stock.saleEligible && stock.intendedUse == "secondhand-retail" && !string.IsNullOrWhiteSpace(stock.itemInstanceId));
            Require(generatedSecondhand != null
                && services.ItemIdentities.TryGetSnapshot(generatedSecondhand.itemInstanceId, out ItemInstanceSnapshot generatedIdentity)
                && generatedIdentity.Data.provenance.priorOwnerIds.Length > 0,
                "Generated secondhand stock did not retain a simulated prior-owner history.");
            Require(services.ItemDurability.TryGetDurabilityForItem(generatedSecondhand.itemInstanceId, out ItemDurabilitySnapshot generatedCondition)
                && generatedCondition.NormalizedDurability >= 0.45f && generatedCondition.NormalizedDurability <= 0.90f,
                "Generated secondhand stock did not receive a plausible used condition.");
            Require(services.LastPrototypeMarketChange != null, "The background market calculator did not produce an interval plan.");
            Require(services.LastPrototypeMarketChange.TotalTradeUnits > 0L, "The initial background market interval did not trade any goods.");

            registry.TryGet(PrototypeEconomyContentIds.CurrencyGold, out CurrencyDefinition gold);
            registry.TryGet(PrototypeEconomyContentIds.ItemIronOre, out ItemDefinition ironOre);
            registry.TryGet(PrototypeEconomyContentIds.ItemSword, out ItemDefinition sword);
            Require(gold != null && ironOre != null && sword != null, "Prototype economy item or currency definitions are missing.");
            if (services.GetPlayerBalance() < 100L)
            {
                Require(services.AddDevelopmentCurrency(gold, 100L).Succeeded, "Could not fund the play-mode market verification.");
            }

            long playerBefore = services.GetPlayerBalance();
            long treasuryBefore = Balance(services, PrototypeEconomyContentIds.TreasuryAccount);
            int ironBefore = inventory.CountItem(ironOre);
            long ironPoolBefore = Pool(services, PrototypeEconomyContentIds.IronImportPool);
            int tradesBeforePurchase = services.Trades.TradeRecords.Count;
            int revenueBeforePurchase = services.Businesses.RevenueRecords.Count;
            PrototypeEconomyOperation purchase = services.BuyPrototypeMarketGood(PrototypeEconomyContentIds.ItemIronOre, 10);
            Require(purchase.Succeeded, $"Raw iron purchase failed: {purchase.Message}");
            Require(purchase.Amount > 0L, "The market purchase did not report its dynamically quoted total.");
            Require(inventory.CountItem(ironOre) == ironBefore + 10, "Purchased iron ore was not added to inventory.");
            Require(services.GetPlayerBalance() == playerBefore - purchase.Amount, "Purchase did not debit the authoritative player account by its full amount.");
            Require(Balance(services, PrototypeEconomyContentIds.TreasuryAccount) == treasuryBefore + 1L, "Sales tax did not reach the town treasury.");
            Require(Pool(services, PrototypeEconomyContentIds.IronImportPool) == ironPoolBefore - 10L, "Iron purchase did not consume regional import stock.");
            Require(services.Trades.TradeRecords.Count == tradesBeforePurchase + 1 && services.Trades.Receipts.Count > 0,
                "Player purchase bypassed the authoritative trade record and receipt ledger.");
            Require(services.Businesses.RevenueRecords.Count == revenueBeforePurchase + 1,
                "Player purchase was not recognized as business revenue.");

            long swordPoolBefore = Pool(services, PrototypeEconomyContentIds.SwordExportPool);
            Require(inventory.AddItemOrInstances(sword, 1).AddedQuantity == 1, "Could not add a sword for export verification.");
            PrototypeExportChoice export = services.GetPrototypeExportChoices().FirstOrDefault(choice => choice.ItemDefinitionId == sword.Id);
            Require(export != null, "The sword was not offered as a town export.");
            long balanceBeforeSale = services.GetPlayerBalance();
            int tradesBeforeSale = services.Trades.TradeRecords.Count;
            int expensesBeforeSale = services.Businesses.ExpenseRecords.Count;
            PrototypeEconomyOperation sale = services.SellPrototypeExport(export.ItemInstanceId);
            Require(sale.Succeeded, $"Sword export sale failed: {sale.Message}");
            Require(services.GetPlayerBalance() == balanceBeforeSale + sale.Amount, "Export sale did not credit the authoritative player account.");
            Require(Pool(services, PrototypeEconomyContentIds.SwordExportPool) == swordPoolBefore, "Exact secondhand sword stock was also counted in the aggregate export pool.");
            Require(services.Businesses.StockClassifications.Any(stock => stock.itemInstanceId == export.ItemInstanceId && stock.saleEligible),
                "Sold sword was not retained as exact secondhand business stock.");
            Require(services.Trades.TradeRecords.Count == tradesBeforeSale + 1, "Player sale bypassed the authoritative trade ledger.");
            Require(services.Businesses.ExpenseRecords.Count == expensesBeforeSale + 1,
                "Merchant inventory purchase was not recognized as a business expense.");

            long balanceAfterSale = services.GetPlayerBalance();
            long swordPoolAfterSale = Pool(services, PrototypeEconomyContentIds.SwordExportPool);
            PrototypeEconomyOperation duplicateSale = services.SellPrototypeExport(export.ItemInstanceId);
            Require(!duplicateSale.Succeeded, "The same exact sword instance was sold twice.");
            Require(services.GetPlayerBalance() == balanceAfterSale, "Rejected duplicate sale changed the player balance.");
            Require(Pool(services, PrototypeEconomyContentIds.SwordExportPool) == swordPoolAfterSale, "Rejected duplicate sale changed export stock.");

            string[] exactSwordIdsBeforePurchase = services.Businesses.StockClassifications
                .Where(stock => stock.saleEligible && stock.itemDefinitionId == sword.Id && !string.IsNullOrWhiteSpace(stock.itemInstanceId))
                .Select(stock => stock.itemInstanceId)
                .ToArray();
            long poolBeforeBuyback = Pool(services, PrototypeEconomyContentIds.SwordExportPool);
            PrototypeEconomyOperation buyback = services.BuyPrototypeMarketGood(PrototypeEconomyContentIds.ItemSword, 1);
            Require(buyback.Succeeded && buyback.ItemInstanceIds.Count == 1, $"Exact secondhand sword purchase failed: {buyback.Message}");
            Require(exactSwordIdsBeforePurchase.Contains(buyback.ItemInstanceIds[0]), "Buying secondhand stock did not preserve an existing exact sword identity.");
            Require(Pool(services, PrototypeEconomyContentIds.SwordExportPool) == poolBeforeBuyback, "Exact secondhand purchase incorrectly consumed aggregate stock.");
            Require(!services.Businesses.StockClassifications.Any(stock => stock.itemInstanceId == buyback.ItemInstanceIds[0]), "Purchased secondhand stock remained classified as business inventory.");

            foreach (string exactStockId in services.Businesses.StockClassifications
                .Where(stock => stock.saleEligible && stock.itemDefinitionId == sword.Id && !string.IsNullOrWhiteSpace(stock.itemInstanceId))
                .Select(stock => stock.itemInstanceId)
                .ToArray())
            {
                Require(services.AggregatePrototypeSecondhandStock(exactStockId).Succeeded, "Could not clear exact sword stock before aggregate materialization verification.");
            }

            long poolBeforeMaterialization = Pool(services, PrototypeEconomyContentIds.SwordExportPool);
            long lotsBeforeMaterialization = LotStock(services, PrototypeEconomyContentIds.ItemSword);
            PrototypeEconomyOperation nativePurchase = services.BuyPrototypeMarketGood(PrototypeEconomyContentIds.ItemSword, 1);
            Require(nativePurchase.Succeeded && nativePurchase.ItemInstanceIds.Count == 1, $"Native aggregate sword purchase failed: {nativePurchase.Message}");
            string nativeSwordId = nativePurchase.ItemInstanceIds[0];
            Require(nativeSwordId != export.ItemInstanceId, "Aggregate stock reused a secondhand item identity.");
            Require(Pool(services, PrototypeEconomyContentIds.SwordExportPool) == poolBeforeMaterialization - 1L, "Native weapon purchase did not consume aggregate regional stock.");
            Require(LotStock(services, PrototypeEconomyContentIds.ItemSword) == lotsBeforeMaterialization - 1L, "Native weapon purchase did not consume its recipe-linked production lot.");
            Require(services.ItemIdentities.TryGetSnapshot(nativeSwordId, out ItemInstanceSnapshot nativeIdentity)
                && nativeIdentity.OwnershipKind == ItemOwnershipKind.PersonOwned, "Materialized sword does not have a player-owned unique identity.");
            Require(services.ItemCompositions.TryGetSnapshotForItem(nativeSwordId, out ItemCompositionSnapshot nativeComposition)
                && nativeComposition.Materials.Any(material => material.materialDefinitionId == "material.iron")
                && nativeComposition.Materials.Any(material => material.materialDefinitionId == "material.leather"),
                "Materialized sword did not inherit its iron-and-leather composition.");
            Require(services.ItemQualityAffixes.TryGetQualityForItem(nativeSwordId, out ItemQualitySnapshot nativeQuality)
                && nativeQuality.Data.source == ItemQualityRecordSource.ProductionGenerated,
                "Materialized sword did not receive production-generated quality.");
            Require(services.ItemDurability.TryGetDurabilityForItem(nativeSwordId, out _), "Materialized sword did not receive durability.");

            Require(inventory.AddItemOrInstances(sword, 1).AddedQuantity == 1, "Could not add a sword for explicit aggregation verification.");
            PrototypeExportChoice aggregateChoice = services.GetPrototypeExportChoices().Last(choice => choice.ItemDefinitionId == sword.Id);
            Require(services.SellPrototypeExport(aggregateChoice.ItemInstanceId).Succeeded, "Could not place the explicit aggregation sword into exact secondhand stock.");
            long poolBeforeAggregation = Pool(services, PrototypeEconomyContentIds.SwordExportPool);
            long lotsBeforeAggregation = LotStock(services, PrototypeEconomyContentIds.ItemSword);
            PrototypeEconomyOperation aggregation = services.AggregatePrototypeSecondhandStock(aggregateChoice.ItemInstanceId);
            Require(aggregation.Succeeded, $"Explicit exact-to-aggregate conversion failed: {aggregation.Message}");
            Require(Pool(services, PrototypeEconomyContentIds.SwordExportPool) == poolBeforeAggregation + 1L
                && LotStock(services, PrototypeEconomyContentIds.ItemSword) == lotsBeforeAggregation + 1L,
                "Explicit aggregation did not add exactly one synchronized pool and production-lot unit.");
            Require(services.ItemIdentities.TryGetSnapshot(aggregateChoice.ItemInstanceId, out ItemInstanceSnapshot aggregatedIdentity)
                && aggregatedIdentity.LifecycleState == ItemLifecycleState.Consumed,
                "Explicit aggregation did not retire the original exact item identity.");
            Require(!services.Businesses.StockClassifications.Any(stock => stock.itemInstanceId == aggregateChoice.ItemInstanceId),
                "Explicitly aggregated stock remained available as the same exact item.");

            VerifyFailureRollback(services, inventory, registry, gold, ironOre, sword);
            VerifyPersistenceRoundTrip(services, registry);
        }

        private static void VerifyFailureRollback(PrototypePersistenceServiceBehaviour services, PlayerInventory inventory, DefinitionRegistry registry,
            CurrencyDefinition gold, ItemDefinition ironOre, ItemDefinition sword)
        {
            long playerBalance = services.GetPlayerBalance();
            int ironQuantity = inventory.CountItem(ironOre);
            long ironStock = Pool(services, PrototypeEconomyContentIds.IronImportPool);
            PrototypeEconomyOperation overCapacity = services.BuyPrototypeMarketGood(PrototypeEconomyContentIds.ItemIronOre, 1000000);
            Require(!overCapacity.Succeeded, "An impossible inventory-sized purchase unexpectedly succeeded.");
            Require(services.GetPlayerBalance() == playerBalance && inventory.CountItem(ironOre) == ironQuantity
                && Pool(services, PrototypeEconomyContentIds.IronImportPool) == ironStock,
                "Rejected over-capacity purchase mutated economy state.");

            EconomyRuntimeSaveData fundedEconomy = services.Economy.CreateSaveData();
            long spendable = services.GetPlayerBalance();
            if (spendable > 0L)
            {
                Require(services.SpendDevelopmentCurrency(gold, spendable).Succeeded, "Could not prepare the insufficient-funds check.");
            }
            int ironBeforeUnfundedBuy = inventory.CountItem(ironOre);
            long stockBeforeUnfundedBuy = Pool(services, PrototypeEconomyContentIds.IronImportPool);
            PrototypeEconomyOperation unfunded = services.BuyPrototypeMarketGood(PrototypeEconomyContentIds.ItemIronOre, 1);
            Require(!unfunded.Succeeded, "An unfunded purchase unexpectedly succeeded.");
            Require(inventory.CountItem(ironOre) == ironBeforeUnfundedBuy && Pool(services, PrototypeEconomyContentIds.IronImportPool) == stockBeforeUnfundedBuy,
                "Rejected unfunded purchase mutated goods or regional stock.");
            Require(services.Economy.RestoreFromSaveData(fundedEconomy, registry).Succeeded, "Could not restore economy after insufficient-funds check.");

            RegionalFlowRuntimeSaveData stockedRegion = services.RegionalFlow.CreateSaveData();
            long available = Pool(services, PrototypeEconomyContentIds.IronImportPool);
            if (available > 0L)
            {
                RegionalFlowOperationResult emptied = services.RegionalFlow.ApplyQuantityOperation(new AggregateQuantityOperationData
                {
                    operationId = "verification.prototype-market.empty-iron",
                    operationKind = AggregateQuantityOperationKind.Consume,
                    commodityId = PrototypeEconomyContentIds.CommodityIronOre,
                    sourcePoolId = PrototypeEconomyContentIds.IronImportPool,
                    unit = CommodityUnit.Each,
                    quantity = available,
                    purpose = "verification",
                    sourceEventId = "verification.prototype-market.empty-iron",
                    worldTime = 1d,
                    provenance = "group8-playmode-verifier"
                }, "verification.prototype-market.empty-iron");
                Require(emptied.Succeeded, "Could not prepare the insufficient-stock check.");
            }
            long balanceBeforeNoStock = services.GetPlayerBalance();
            int ironBeforeNoStock = inventory.CountItem(ironOre);
            PrototypeEconomyOperation noStock = services.BuyPrototypeMarketGood(PrototypeEconomyContentIds.ItemIronOre, 1);
            Require(!noStock.Succeeded, "A purchase with no regional stock unexpectedly succeeded.");
            Require(services.GetPlayerBalance() == balanceBeforeNoStock && inventory.CountItem(ironOre) == ironBeforeNoStock
                && Pool(services, PrototypeEconomyContentIds.IronImportPool) == 0L,
                "Insufficient-stock rollback did not restore payment and inventory atomically.");
            Require(services.RegionalFlow.RestoreFromSaveData(stockedRegion, registry).Succeeded, "Could not restore regional stock after failure check.");

            Require(inventory.AddItemOrInstances(sword, 1).AddedQuantity == 1, "Could not add a sword for merchant-insolvency verification.");
            PrototypeExportChoice insolventExport = services.GetPrototypeExportChoices().LastOrDefault(choice => choice.ItemDefinitionId == sword.Id);
            Require(insolventExport != null, "Could not identify the merchant-insolvency test sword.");
            EconomyRuntimeSaveData solventEconomy = services.Economy.CreateSaveData();
            ItemInstanceRuntimeSaveData identitySave = services.ItemIdentities.CreateSaveData();
            InventorySaveData inventorySave = inventory.CreateSaveData();
            BusinessRuntimeSaveData businessSave = services.Businesses.CreateSaveData();
            RegionalFlowRuntimeSaveData regionSave = services.RegionalFlow.CreateSaveData();
            long merchantFunds = Balance(services, PrototypeEconomyContentIds.MerchantAccount);
            if (merchantFunds > 0L)
            {
                EconomyOperationResult drain = services.Economy.Transfer("verification.prototype-market.drain-merchant",
                    PrototypeEconomyContentIds.MerchantAccount, PrototypeEconomyContentIds.ExternalTradeAccount,
                    new MoneyAmount(PrototypeEconomyContentIds.CurrencyGold, merchantFunds), EconomyTransactionKind.Transfer,
                    worldTime: 1d);
                Require(drain.Succeeded, "Could not prepare the merchant-insolvency check.");
            }
            long balanceBeforeRejectedSale = services.GetPlayerBalance();
            long stockBeforeRejectedSale = Pool(services, PrototypeEconomyContentIds.SwordExportPool);
            PrototypeEconomyOperation insolventSale = services.SellPrototypeExport(insolventExport.ItemInstanceId);
            Require(!insolventSale.Succeeded, "An insolvent merchant unexpectedly purchased a sword.");
            Require(services.GetPlayerBalance() == balanceBeforeRejectedSale
                && Pool(services, PrototypeEconomyContentIds.SwordExportPool) == stockBeforeRejectedSale
                && services.GetPrototypeExportChoices().Any(choice => choice.ItemInstanceId == insolventExport.ItemInstanceId),
                "Merchant-insolvency rollback lost currency, stock, or the exact item instance.");
            Require(services.Economy.RestoreFromSaveData(solventEconomy, registry).Succeeded, "Could not restore merchant funds after failure check.");
            Require(services.ItemIdentities.RestoreFromSaveData(identitySave, registry).Succeeded, "Could not restore item identities after failure check.");
            Require(inventory.TryRestoreFromSaveData(inventorySave, registry).Succeeded, "Could not restore inventory after failure check.");
            Require(services.Businesses.RestoreFromSaveData(businessSave, registry).Succeeded, "Could not restore business state after failure check.");
            Require(services.RegionalFlow.RestoreFromSaveData(regionSave, registry).Succeeded, "Could not restore regional state after failure check.");

            long externalBefore = Balance(services, PrototypeEconomyContentIds.ExternalTradeAccount);
            long merchantBefore = Balance(services, PrototypeEconomyContentIds.MerchantAccount);
            EconomyOperationResult first = services.Economy.Transfer("verification.prototype-market.duplicate-transfer",
                PrototypeEconomyContentIds.ExternalTradeAccount, PrototypeEconomyContentIds.MerchantAccount,
                new MoneyAmount(PrototypeEconomyContentIds.CurrencyGold, 1L), EconomyTransactionKind.Transfer, worldTime: 1d);
            EconomyOperationResult replay = services.Economy.Transfer("verification.prototype-market.duplicate-transfer",
                PrototypeEconomyContentIds.ExternalTradeAccount, PrototypeEconomyContentIds.MerchantAccount,
                new MoneyAmount(PrototypeEconomyContentIds.CurrencyGold, 1L), EconomyTransactionKind.Transfer, worldTime: 1d);
            Require(first.Succeeded && replay.Succeeded && replay.Duplicate, "Duplicate economy transaction was not replay-safe.");
            Require(Balance(services, PrototypeEconomyContentIds.ExternalTradeAccount) == externalBefore - 1L
                && Balance(services, PrototypeEconomyContentIds.MerchantAccount) == merchantBefore + 1L,
                "Duplicate economy transaction moved currency more than once.");
        }

        private static void VerifyPersistenceRoundTrip(PrototypePersistenceServiceBehaviour services, DefinitionRegistry registry)
        {
            long playerBalance = services.GetPlayerBalance();
            long ironStock = Pool(services, PrototypeEconomyContentIds.IronImportPool);
            int prices = services.Markets.PriceHistory.Count;
            int ownership = services.Businesses.OwnershipRecords.Count;
            int propertyOwnership = services.Properties.OwnershipInterests.Count;
            int taxPayments = services.InstitutionalRevenue.Payments.Count;
            int contracts = services.ContractEconomy.Contracts.Count;
            int trades = services.Trades.TradeRecords.Count;
            int receipts = services.Trades.Receipts.Count;
            int payrollObligations = services.Payroll.Obligations.Count;
            int businessRevenue = services.Businesses.RevenueRecords.Count;
            int businessExpenses = services.Businesses.ExpenseRecords.Count;
            long swordLots = LotStock(services, PrototypeEconomyContentIds.ItemSword);

            EconomyRuntimeSaveData economy = services.Economy.CreateSaveData();
            MarketRuntimeSaveData markets = services.Markets.CreateSaveData();
            BusinessRuntimeSaveData businesses = services.Businesses.CreateSaveData();
            PropertyRuntimeSaveData properties = services.Properties.CreateSaveData();
            InstitutionalRevenueRuntimeSaveData revenue = services.InstitutionalRevenue.CreateSaveData();
            RegionalFlowRuntimeSaveData regional = services.RegionalFlow.CreateSaveData();
            ProductionWorkflowRuntimeSaveData production = services.ProductionWorkflow.CreateSaveData();
            ContractRuntimeSaveData contractData = services.ContractEconomy.CreateSaveData();
            TradeRuntimeSaveData tradeData = services.Trades.CreateSaveData();
            PayrollRuntimeSaveData payrollData = services.Payroll.CreateSaveData();

            Require(services.Economy.RestoreFromSaveData(economy, registry).Succeeded, "Economy save/load round trip failed.");
            Require(services.Markets.RestoreFromSaveData(markets, registry).Succeeded, "Market save/load round trip failed.");
            Require(services.Businesses.RestoreFromSaveData(businesses, registry).Succeeded, "Business save/load round trip failed.");
            Require(services.Properties.RestoreFromSaveData(properties, registry).Succeeded, "Property save/load round trip failed.");
            Require(services.InstitutionalRevenue.RestoreFromSaveData(revenue, registry).Succeeded, "Revenue save/load round trip failed.");
            Require(services.RegionalFlow.RestoreFromSaveData(regional, registry).Succeeded, "Regional-flow save/load round trip failed.");
            Require(services.ProductionWorkflow.RestoreFromSaveData(production, registry).Succeeded, "Production-lot save/load round trip failed.");
            Require(services.ContractEconomy.RestoreFromSaveData(contractData, registry).Succeeded, "Contract save/load round trip failed.");
            Require(services.Trades.RestoreFromSaveData(tradeData, registry).Succeeded, "Trade save/load round trip failed.");
            Require(services.Payroll.RestoreFromSaveData(payrollData, registry).Succeeded, "Payroll save/load round trip failed.");

            Require(services.GetPlayerBalance() == playerBalance, "Player balance changed across save/load.");
            Require(Pool(services, PrototypeEconomyContentIds.IronImportPool) == ironStock, "Market stock changed across save/load.");
            Require(services.Markets.PriceHistory.Count == prices, "Market price history changed across save/load.");
            Require(services.Businesses.OwnershipRecords.Count == ownership, "Business ownership changed across save/load.");
            Require(services.Properties.OwnershipInterests.Count == propertyOwnership, "Property ownership changed across save/load.");
            Require(services.InstitutionalRevenue.Payments.Count == taxPayments, "Tax payment history changed across save/load.");
            Require(services.ContractEconomy.Contracts.Count == contracts, "Contract state changed across save/load.");
            Require(services.Trades.TradeRecords.Count == trades && services.Trades.Receipts.Count == receipts, "Trade audit state changed across save/load.");
            Require(services.Payroll.Obligations.Count == payrollObligations, "Payroll obligations changed across save/load.");
            Require(services.Businesses.RevenueRecords.Count == businessRevenue && services.Businesses.ExpenseRecords.Count == businessExpenses,
                "Business accounting changed across save/load.");
            Require(LotStock(services, PrototypeEconomyContentIds.ItemSword) == swordLots, "Recipe-linked production-lot stock changed across save/load.");
        }

        private static long Balance(PrototypePersistenceServiceBehaviour services, string accountId)
        {
            return services.Economy.TryGetAccount(accountId, out EconomyAccountSnapshot account) ? account.BalanceUnits : -1L;
        }

        private static long Pool(PrototypePersistenceServiceBehaviour services, string poolId)
        {
            return services.RegionalFlow.TryGetPool(poolId, out CommodityPoolData pool) ? pool.AvailableQuantity : -1L;
        }

        private static long LotStock(PrototypePersistenceServiceBehaviour services, string itemDefinitionId)
        {
            return services.ProductionWorkflow.Lots
                .Where(lot => lot.state == ProductionLotState.Active && lot.definitionOrMaterialId == itemDefinitionId)
                .Sum(lot => lot.WholeQuantity);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
