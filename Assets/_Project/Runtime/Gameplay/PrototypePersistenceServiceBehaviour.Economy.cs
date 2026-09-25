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
using UnityIsekaiGame.Economy.Markets;
using UnityIsekaiGame.Economy.Payroll;
using UnityIsekaiGame.Economy.Properties;
using UnityIsekaiGame.Economy.RegionalFlow;
using UnityIsekaiGame.Economy.Trading;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Inventory.Composition;
using UnityIsekaiGame.Inventory.Crafting;
using UnityIsekaiGame.Inventory.Durability;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Inventory.Production;
using UnityIsekaiGame.Inventory.Quality;
using UnityIsekaiGame.Inventory.Recipes;
using UnityIsekaiGame.Progression;
using UnityIsekaiGame.Professions;
using UnityIsekaiGame.CharacterSystem;

namespace UnityIsekaiGame.Gameplay
{
    public sealed partial class PrototypePersistenceServiceBehaviour
    {
        private bool prototypeEconomyInitialized;
        private long lastPrototypeMarketBoundary = -1L;
        private PrototypeMarketChangePlan lastPrototypeMarketChange;

        public string PlayerEconomyAccountId => PrototypeEconomyContentIds.PlayerAccount(ResolvePlayerPersonId());
        public string PrototypeMerchantAccountId => PrototypeEconomyContentIds.MerchantAccount;
        public PrototypeMarketChangePlan LastPrototypeMarketChange => lastPrototypeMarketChange;

        public long GetPlayerBalance(string currencyId = PrototypeEconomyContentIds.CurrencyGold)
        {
            EnsureInitialized();
            return Economy.TryGetAccount(PlayerEconomyAccountId, out EconomyAccountSnapshot account)
                && string.Equals(account.CurrencyId, currencyId, StringComparison.Ordinal)
                ? account.BalanceUnits
                : 0L;
        }

        public PrototypeEconomyOperation AddDevelopmentCurrency(CurrencyDefinition currency, long amount)
        {
            EnsurePrototypeEconomyInitialized();
            if (currency == null || amount <= 0L || !string.Equals(currency.Id, PrototypeEconomyContentIds.CurrencyGold, StringComparison.Ordinal))
            {
                return PrototypeEconomyOperation.Failure("A positive amount of the prototype Gold currency is required.");
            }

            string transactionId = $"economy-development.issue.{Guid.NewGuid():N}";
            EconomyOperationResult result = Economy.Issue(transactionId, PlayerEconomyAccountId, new MoneyAmount(currency.Id, amount),
                actorId: ResolvePlayerPersonId(), worldTime: CurrentEconomyWorldTime);
            if (result.Succeeded) dirtyTracker?.MarkDirty("Development currency issued through EconomyRuntime.");
            return result.Succeeded
                ? PrototypeEconomyOperation.Success($"Added {amount} {currency.DisplayName} through the authoritative economy ledger.", amount)
                : PrototypeEconomyOperation.Failure(result.Message);
        }

        public PrototypeEconomyOperation SpendDevelopmentCurrency(CurrencyDefinition currency, long amount)
        {
            EnsurePrototypeEconomyInitialized();
            if (currency == null || amount <= 0L || !string.Equals(currency.Id, PrototypeEconomyContentIds.CurrencyGold, StringComparison.Ordinal))
            {
                return PrototypeEconomyOperation.Failure("A positive amount of the prototype Gold currency is required.");
            }

            string transactionId = $"economy-development.destroy.{Guid.NewGuid():N}";
            EconomyOperationResult result = Economy.Destroy(transactionId, PlayerEconomyAccountId, new MoneyAmount(currency.Id, amount),
                actorId: ResolvePlayerPersonId(), worldTime: CurrentEconomyWorldTime);
            if (result.Succeeded) dirtyTracker?.MarkDirty("Development currency destroyed through EconomyRuntime.");
            return result.Succeeded
                ? PrototypeEconomyOperation.Success($"Spent {amount} {currency.DisplayName} through the authoritative economy ledger.", amount)
                : PrototypeEconomyOperation.Failure(result.Message);
        }

        public IReadOnlyList<PrototypeMarketListing> GetPrototypeMarketListings()
        {
            EnsurePrototypeEconomyInitialized();
            List<PrototypeMarketListing> listings = new List<PrototypeMarketListing>();
            AddListing(listings, PrototypeEconomyContentIds.ItemIronOre, buyable: true, sellable: false);
            AddListing(listings, PrototypeEconomyContentIds.ItemWoodLog, buyable: true, sellable: false);
            AddListing(listings, PrototypeEconomyContentIds.ItemSword, buyable: true, sellable: true);
            AddListing(listings, PrototypeEconomyContentIds.ItemBow, buyable: true, sellable: true);
            return listings;
        }

        public PrototypeEconomyOperation BuyPrototypeMarketGood(string itemDefinitionId, int quantity)
        {
            EnsurePrototypeEconomyInitialized();
            bool rawResource = PrototypeEconomyContentIds.IsTownImport(itemDefinitionId);
            bool finishedGood = PrototypeEconomyContentIds.IsTownExport(itemDefinitionId);
            if ((!rawResource && !finishedGood) || quantity <= 0)
            {
                return PrototypeEconomyOperation.Failure("Choose a positive quantity of a good sold by the prototype town market.");
            }

            DefinitionRegistry registry = GetDefinitionRegistry();
            if (!registry.TryGet(itemDefinitionId, out ItemDefinition item))
            {
                return PrototypeEconomyOperation.Failure($"Item definition '{itemDefinitionId}' is unavailable.");
            }

            if (playerInventory == null || !playerInventory.CanAddItemOrInstances(item, quantity))
            {
                return PrototypeEconomyOperation.Failure("There is not enough inventory space for that purchase.");
            }

            IReadOnlyList<BusinessStockClassificationData> exactStock = finishedGood
                ? GetExactPrototypeExportStock(itemDefinitionId)
                : Array.Empty<BusinessStockClassificationData>();
            long aggregateAvailable = PoolSupply(PrototypeEconomyContentIds.PoolForItem(itemDefinitionId));
            if (quantity > exactStock.Count + aggregateAvailable)
            {
                return PrototypeEconomyOperation.Failure($"The market has only {exactStock.Count + aggregateAvailable} available.");
            }

            double worldTime = CurrentEconomyWorldTime;
            int quotedExactQuantity = finishedGood ? Math.Min(quantity, exactStock.Count) : 0;
            string[] quotedExactIds = exactStock.Take(quotedExactQuantity).Select(stock => stock.itemInstanceId).ToArray();
            List<MerchantQuoteRecordData> purchaseQuotes = new List<MerchantQuoteRecordData>();
            for (int index = 0; index < quotedExactQuantity; index++)
            {
                string exactId = quotedExactIds[index];
                ItemIdentities.TryGetSnapshot(exactId, out ItemInstanceSnapshot quotedExactItem);
                ItemQualityAffixes.TryGetQualityForItem(exactId, out ItemQualitySnapshot quotedExactQuality);
                ItemDurability.TryGetDurabilityForItem(exactId, out ItemDurabilitySnapshot quotedExactDurability);
                MarketOperationResult exactQuote = CreatePrototypeQuote(itemDefinitionId, MerchantQuoteDirection.MerchantSells, 1, worldTime,
                    quotedExactItem, quotedExactQuality, quotedExactDurability);
                if (!exactQuote.Succeeded || exactQuote.Quote == null)
                {
                    return PrototypeEconomyOperation.Failure(exactQuote.Message);
                }
                purchaseQuotes.Add(exactQuote.Quote);
            }

            int quotedAggregateQuantity = quantity - quotedExactQuantity;
            if (quotedAggregateQuantity > 0)
            {
                MarketOperationResult aggregateQuote = CreatePrototypeQuote(itemDefinitionId, MerchantQuoteDirection.MerchantSells,
                    quotedAggregateQuantity, worldTime);
                if (!aggregateQuote.Succeeded || aggregateQuote.Quote == null)
                {
                    return PrototypeEconomyOperation.Failure(aggregateQuote.Message);
                }
                purchaseQuotes.Add(aggregateQuote.Quote);
            }

            if (purchaseQuotes.Count == 0)
            {
                return PrototypeEconomyOperation.Failure("The merchant could not quote the requested stock.");
            }

            long quotedAmount;
            try { quotedAmount = purchaseQuotes.Sum(entry => entry.finalAmountUnits); }
            catch (OverflowException) { return PrototypeEconomyOperation.Failure("The quoted purchase total is too large."); }
            MerchantQuoteRecordData primaryQuote = purchaseQuotes[0];

            PrototypeTradeRollback rollback = CapturePrototypeTradeRollback();
            string operationId = $"prototype-market.buy.{Guid.NewGuid():N}";
            if (!BeginPrototypeTradeAudit(operationId, itemDefinitionId, quantity, quotedAmount, purchaseQuotes.Select(entry => entry.quoteId).ToArray(),
                    PrototypeMerchantTradeParticipant(TradeParticipantRole.Seller), PrototypePlayerTradeParticipant(TradeParticipantRole.Buyer),
                    quotedExactIds, worldTime, out string tradeSessionId, out string tradeFailure))
            {
                RestorePrototypeTrade(rollback);
                return PrototypeEconomyOperation.Failure($"The purchase could not open an authoritative trade: {tradeFailure}");
            }

            EconomyOperationResult payment = Economy.Transfer(operationId, PlayerEconomyAccountId, PrototypeMerchantAccountId,
                new MoneyAmount(primaryQuote.currencyId, quotedAmount), EconomyTransactionKind.Payment,
                actorId: ResolvePlayerPersonId(), priceSnapshotId: primaryQuote.marketPriceId, worldTime: worldTime);
            if (!payment.Succeeded)
            {
                return PrototypeEconomyOperation.Failure(payment.Message);
            }

            List<string> purchasedItemInstanceIds = new List<string>();
            int aggregateQuantity = quantity;
            if (rawResource)
            {
                InventoryAddResult added = playerInventory.AddItemOrInstances(item, quantity);
                if (added.AddedQuantity != quantity)
                {
                    RestorePrototypeTrade(rollback);
                    return PrototypeEconomyOperation.Failure("The purchase was rolled back because the inventory could not accept every item.");
                }
            }
            else
            {
                int exactQuantity = Math.Min(quantity, exactStock.Count);
                for (int index = 0; index < exactQuantity; index++)
                {
                    BusinessStockClassificationData stockRecord = exactStock[index];
                    if (!TransferExactPrototypeStockToPlayer(item, stockRecord, out string transferFailure))
                    {
                        RestorePrototypeTrade(rollback);
                        return PrototypeEconomyOperation.Failure($"The purchase was rolled back: {transferFailure}");
                    }

                    purchasedItemInstanceIds.Add(stockRecord.itemInstanceId);
                }

                aggregateQuantity -= exactQuantity;
                if (aggregateQuantity > 0 && !MaterializePrototypeProductionStock(item, aggregateQuantity, operationId, worldTime, purchasedItemInstanceIds, out string materializationFailure))
                {
                    RestorePrototypeTrade(rollback);
                    return PrototypeEconomyOperation.Failure($"The purchase was rolled back: {materializationFailure}");
                }
            }

            if (rawResource || aggregateQuantity > 0)
            {
                long poolQuantity = rawResource ? quantity : aggregateQuantity;
                RegionalFlowOperationResult stock = ApplyPrototypePoolChange(itemDefinitionId, poolQuantity, add: false, operationId, worldTime);
                if (!stock.Succeeded)
                {
                    RestorePrototypeTrade(rollback);
                    return PrototypeEconomyOperation.Failure($"The purchase was rolled back because town stock changed: {stock.Message}");
                }
            }

            if (!TryApplyPrototypeSalesTax(operationId, payment, worldTime, out long taxUnits, out string taxFailure))
            {
                RestorePrototypeTrade(rollback);
                return PrototypeEconomyOperation.Failure($"The purchase was rolled back because its tax record failed: {taxFailure}");
            }

            List<string> transferReferences = purchasedItemInstanceIds
                .Select(id => $"item-transfer.{operationId}.{id}")
                .ToList();
            if (rawResource || aggregateQuantity > 0)
            {
                transferReferences.Add($"aggregate-transfer.{operationId}.{itemDefinitionId}.{(rawResource ? quantity : aggregateQuantity)}");
            }
            if (!CompletePrototypeTradeAudit(operationId, tradeSessionId, payment, transferReferences, worldTime,
                    merchantRevenue: true, itemDefinitionId, purchasedItemInstanceIds, out _, out tradeFailure))
            {
                RestorePrototypeTrade(rollback);
                return PrototypeEconomyOperation.Failure($"The purchase was rolled back because its trade/accounting record failed: {tradeFailure}");
            }

            playerItemIdentitySynchronizer?.SynchronizeNow();
            Markets.AddTransactionObservation($"market-observation.{operationId}", payment.Transaction, PrototypeEconomyContentIds.MarketInstanceTown,
                PrototypeEconomyContentIds.SubjectForItem(itemDefinitionId), MarketTransactionObservationPolicy.IncludeCommitted, true, worldTime);
            dirtyTracker?.MarkDirty("Prototype market purchase committed.");
            long totalPaid = checked(quotedAmount + taxUnits);
            string taxSummary = taxUnits > 0L ? $" plus {taxUnits} Gold sales tax" : string.Empty;
            return PrototypeEconomyOperation.Success($"Bought {quantity} {item.DisplayName} for {quotedAmount} Gold{taxSummary}.", totalPaid, purchasedItemInstanceIds);
        }

        public PrototypeEconomyOperation SellPrototypeExport(string itemInstanceId)
        {
            EnsurePrototypeEconomyInitialized();
            if (string.IsNullOrWhiteSpace(itemInstanceId) || playerInventory == null)
            {
                return PrototypeEconomyOperation.Failure("Choose a sword or bow from inventory.");
            }

            int slotIndex = -1;
            InventorySlot slot = null;
            for (int index = 0; index < playerInventory.Slots.Count; index++)
            {
                InventorySlot candidate = playerInventory.Slots[index];
                if (candidate != null && string.Equals(candidate.ItemInstanceId, itemInstanceId, StringComparison.Ordinal))
                {
                    slotIndex = index;
                    slot = candidate;
                    break;
                }
            }

            if (slot == null || slot.Item == null || !PrototypeEconomyContentIds.IsTownExport(slot.Item.Id))
            {
                return PrototypeEconomyOperation.Failure("The prototype town currently exports only iron swords and wooden bows.");
            }

            playerItemIdentitySynchronizer?.SynchronizeNow();
            ItemIdentities.TryGetSnapshot(itemInstanceId, out ItemInstanceSnapshot identity);
            ItemQualityAffixes.TryGetQualityForItem(itemInstanceId, out ItemQualitySnapshot quality);
            ItemDurability.TryGetDurabilityForItem(itemInstanceId, out ItemDurabilitySnapshot durability);
            double worldTime = CurrentEconomyWorldTime;
            MarketOperationResult quote = CreatePrototypeQuote(slot.Item.Id, MerchantQuoteDirection.MerchantBuys, 1, worldTime, identity, quality, durability);
            if (!quote.Succeeded || quote.Quote == null)
            {
                return PrototypeEconomyOperation.Failure(quote.Message);
            }

            PrototypeTradeRollback rollback = CapturePrototypeTradeRollback();
            string operationId = $"prototype-market.sell.{Guid.NewGuid():N}";
            if (!BeginPrototypeTradeAudit(operationId, slot.Item.Id, 1, quote.Quote.finalAmountUnits, new[] { quote.Quote.quoteId },
                    PrototypePlayerTradeParticipant(TradeParticipantRole.Seller), PrototypeMerchantTradeParticipant(TradeParticipantRole.Buyer),
                    new[] { itemInstanceId }, worldTime, out string tradeSessionId, out string tradeFailure))
            {
                RestorePrototypeTrade(rollback);
                return PrototypeEconomyOperation.Failure($"The sale could not open an authoritative trade: {tradeFailure}");
            }

            using IDisposable synchronizationPause = playerItemIdentitySynchronizer?.PauseAutomaticSynchronization();
            if (!playerInventory.TryExtractSlotIdentity(slotIndex, out ItemDefinition soldItem, out string extractedId, out string extractionFailure))
            {
                return PrototypeEconomyOperation.Failure(extractionFailure);
            }

            ItemInstanceOperationResult ownership = ItemIdentities.TransferOwnership(extractedId, ItemOwnershipKind.OrganizationOwned,
                ownerOrganizationId: PrototypeEconomyContentIds.BusinessInstance);
            ItemInstanceOperationResult custody = ownership.Succeeded
                ? ItemIdentities.TransferCustody(extractedId, custodianContainerId: "inventory.prototype-town.finished-goods")
                : ItemInstanceOperationResult.Failure(ItemInstanceOperationStatus.ValidationFailed, ownership.Message);
            EconomyOperationResult payment = custody.Succeeded
                ? Economy.Transfer(operationId, PrototypeMerchantAccountId, PlayerEconomyAccountId,
                    new MoneyAmount(quote.Quote.currencyId, quote.Quote.finalAmountUnits), EconomyTransactionKind.Payment,
                    actorId: PrototypeEconomyContentIds.BusinessInstance, priceSnapshotId: quote.Quote.marketPriceId, worldTime: worldTime)
                : EconomyOperationResult.Failure(EconomyResultCode.ValidationFailed, custody.Message, Economy.Revision, false);

            if (!ownership.Succeeded || !custody.Succeeded || !payment.Succeeded)
            {
                RestorePrototypeTrade(rollback);
                return PrototypeEconomyOperation.Failure(payment?.Message ?? custody?.Message ?? ownership.Message);
            }

            BusinessOperationResult classified = Businesses.ClassifyStock(new BusinessStockClassificationData
            {
                stockClassificationId = $"business-stock.{extractedId}",
                businessId = PrototypeEconomyContentIds.BusinessInstance,
                establishmentId = PrototypeEconomyContentIds.EstablishmentInstance,
                inventoryId = "inventory.prototype-town.finished-goods",
                itemInstanceId = extractedId,
                category = BusinessStockCategory.FinishedProduct,
                intendedUse = "customer-resale",
                saleEligible = true,
                productionEligible = false
            }, ItemIdentities);
            if (!classified.Succeeded)
            {
                RestorePrototypeTrade(rollback);
                return PrototypeEconomyOperation.Failure($"The sale was rolled back because exact secondhand stock could not be recorded: {classified.Message}");
            }
            if (!CompletePrototypeTradeAudit(operationId, tradeSessionId, payment,
                    new[] { $"item-transfer.{operationId}.{extractedId}" }, worldTime,
                    merchantRevenue: false, soldItem.Id, new[] { extractedId }, out _, out tradeFailure))
            {
                RestorePrototypeTrade(rollback);
                return PrototypeEconomyOperation.Failure($"The sale was rolled back because its trade/accounting record failed: {tradeFailure}");
            }
            Markets.AddTransactionObservation($"market-observation.{operationId}", payment.Transaction, PrototypeEconomyContentIds.MarketInstanceTown,
                PrototypeEconomyContentIds.SubjectForItem(soldItem.Id), MarketTransactionObservationPolicy.IncludeCommitted, true, worldTime);
            dirtyTracker?.MarkDirty("Prototype market export sale committed.");
            return PrototypeEconomyOperation.Success($"Sold {soldItem.DisplayName} for {quote.Quote.finalAmountUnits} Gold.", quote.Quote.finalAmountUnits);
        }

        public IReadOnlyList<PrototypeExportChoice> GetPrototypeExportChoices()
        {
            if (playerInventory == null)
            {
                return Array.Empty<PrototypeExportChoice>();
            }

            playerItemIdentitySynchronizer?.SynchronizeNow();
            return playerInventory.Slots
                .Where(slot => slot != null && slot.Item != null && slot.IsStateful && PrototypeEconomyContentIds.IsTownExport(slot.Item.Id))
                .Select(slot => new PrototypeExportChoice(slot.ItemInstanceId, slot.Item.Id, slot.Item.DisplayName))
                .ToArray();
        }

        public PrototypeEconomyOperation AggregatePrototypeSecondhandStock(string itemInstanceId)
        {
            EnsurePrototypeEconomyInitialized();
            BusinessStockClassificationData stock = Businesses.StockClassifications.FirstOrDefault(entry => entry != null
                && string.Equals(entry.itemInstanceId, itemInstanceId, StringComparison.Ordinal)
                && PrototypeEconomyContentIds.IsTownExport(entry.itemDefinitionId));
            if (stock == null || !ItemIdentities.TryGetSnapshot(itemInstanceId, out ItemInstanceSnapshot item))
            {
                return PrototypeEconomyOperation.Failure("Only exact secondhand sword or bow stock can be explicitly aggregated.");
            }

            PrototypeTradeRollback rollback = CapturePrototypeTradeRollback();
            string operationId = $"prototype-market.aggregate.{itemInstanceId}";
            BusinessOperationResult release = Businesses.ReleaseExactStock(stock.stockClassificationId, itemInstanceId);
            if (!release.Succeeded)
            {
                return PrototypeEconomyOperation.Failure(release.Message);
            }

            RegionalFlowOperationResult aggregate = RegionalFlow.ApplyQuantityOperation(new AggregateQuantityOperationData
            {
                operationId = $"regional-market.aggregate.{itemInstanceId}",
                operationKind = AggregateQuantityOperationKind.AggregateExactItems,
                commodityId = PrototypeEconomyContentIds.CommodityForItem(item.ItemDefinitionId),
                destinationPoolId = PrototypeEconomyContentIds.PoolForItem(item.ItemDefinitionId),
                unit = CommodityUnit.Each,
                quantity = 1L,
                purpose = "explicit-secondhand-stock-aggregation",
                sourceEventId = operationId,
                worldTime = CurrentEconomyWorldTime,
                provenance = "prototype-market-explicit-aggregation"
            }, operationId);
            if (!aggregate.Succeeded)
            {
                RestorePrototypeTrade(rollback);
                return PrototypeEconomyOperation.Failure(aggregate.Message);
            }

            ProductionLotData lot = BuildPrototypeProductionLot(item.ItemDefinitionId, 1L, $"aggregated.{itemInstanceId}", CurrentEconomyWorldTime, "prototype-market-explicit-aggregation");
            lot.sourceItemIds = new[] { itemInstanceId };
            if (ItemQualityAffixes.TryGetQualityForItem(itemInstanceId, out ItemQualitySnapshot quality))
            {
                lot.baseQuality = Math.Clamp(quality.Data.overallQuality, 0f, 1f);
                lot.qualityVariation = 0f;
                lot.qualitySummary = $"Aggregated exact stock at quality {lot.baseQuality:0.###}";
            }

            ProductionWorkflowResult lotResult = ProductionWorkflow.CreateLot(lot);
            ItemInstanceOperationResult consumed = lotResult.Succeeded
                ? ItemIdentities.DestroyOrConsume(itemInstanceId, consumed: true)
                : ItemInstanceOperationResult.Failure(ItemInstanceOperationStatus.ValidationFailed, lotResult.Message);
            if (!lotResult.Succeeded || !consumed.Succeeded)
            {
                RestorePrototypeTrade(rollback);
                return PrototypeEconomyOperation.Failure(consumed.Message);
            }

            dirtyTracker?.MarkDirty("Exact secondhand stock explicitly aggregated into regional inventory.");
            return PrototypeEconomyOperation.Success("Converted the exact secondhand item into one aggregate production-lot unit.", 1L);
        }

        private double CurrentEconomyWorldTime => Math.Max(0d, playTimeTracker == null ? Time.timeAsDouble : playTimeTracker.CumulativeSeconds);

        private MerchantSimulationPolicyData PrototypeSimulationPolicy
        {
            get
            {
                DefinitionRegistry registry = GetDefinitionRegistry();
                return registry != null && registry.TryGet(PrototypeEconomyContentIds.BusinessWorkshop, out BusinessDefinition business)
                    ? business.SimulationPolicy
                    : new MerchantSimulationPolicyData();
            }
        }

        private void EnsurePrototypeEconomyInitialized()
        {
            if (prototypeEconomyInitialized)
            {
                return;
            }

            DefinitionRegistry registry = GetDefinitionRegistry();
            if (registry == null || !registry.TryGet(PrototypeEconomyContentIds.CurrencyGold, out CurrencyDefinition gold))
            {
                return;
            }

            string personId = ResolvePlayerPersonId();
            MerchantSimulationPolicyData simulation = PrototypeSimulationPolicy;
            long originGold = Math.Max(0L, playerIdentityProgression?.Origin?.startingGoldAmount ?? 0L);
            bool originGrantPending = playerIdentityProgression?.Origin is { assigned: true, startingCurrencyApplied: false };
            bool createdPlayerAccount = false;
            if (!Economy.TryGetAccount(PlayerEconomyAccountId, out _))
            {
                EconomyOperationResult accountResult = Economy.CreateAccount(PlayerEconomyAccountId, gold, personId, EconomyAccountKind.PersonWallet,
                    originGrantPending ? originGold : 0L,
                    $"economy-origin-grant.{personId}");
                if (!accountResult.Succeeded)
                {
                    Debug.LogWarning($"Could not create the player's economy account: {accountResult.Message}", this);
                    return;
                }
                createdPlayerAccount = true;
            }

            if (originGrantPending && !createdPlayerAccount && originGold > 0L)
            {
                EconomyOperationResult grant = Economy.Issue($"economy-origin-grant-existing-account.{personId}", PlayerEconomyAccountId,
                    new MoneyAmount(gold.Id, originGold), actorId: personId);
                if (!grant.Succeeded)
                {
                    Debug.LogWarning($"Could not apply the player's origin currency grant: {grant.Message}", this);
                    return;
                }
            }

            if (originGrantPending)
            {
                playerIdentityProgression.MarkStartingCurrencyApplied();
            }
            if (!Economy.TryGetAccount(PrototypeEconomyContentIds.MerchantAccount, out _))
            {
                Economy.CreateAccount(PrototypeEconomyContentIds.MerchantAccount, gold, PrototypeEconomyContentIds.BusinessInstance,
                    EconomyAccountKind.OrganizationAccount, simulation.openingOperatingFunds, "economy-opening.prototype-town-workshop");
            }

            if (!Economy.TryGetAccount(PrototypeEconomyContentIds.ExternalTradeAccount, out _))
            {
                Economy.CreateAccount(PrototypeEconomyContentIds.ExternalTradeAccount, gold, PrototypeEconomyContentIds.RegionInstanceTown,
                    EconomyAccountKind.OrganizationAccount, simulation.externalMarketLiquidity, "economy-opening.prototype-town-external-trade");
            }

            if (!Economy.TryGetAccount(PrototypeEconomyContentIds.TreasuryAccount, out _))
            {
                Economy.CreateAccount(PrototypeEconomyContentIds.TreasuryAccount, gold, "organization.prototype-town",
                    EconomyAccountKind.SystemTreasury, 0L, "economy-opening.prototype-town-treasury");
            }

            foreach (CharacterSystemCoordinator character in FindObjectsByType<CharacterSystemCoordinator>(FindObjectsInactive.Include))
            {
                if (string.Equals(character.PersonId, personId, StringComparison.Ordinal))
                {
                    character.ConfigureCurrencyBalanceProvider(GetPlayerBalance);
                }
            }

            SeedPrototypeMarket(registry);
            SeedPrototypeBusiness(registry, gold);
            SeedPrototypeProperty();
            SeedPrototypeRevenue();
            SeedPrototypeRegionalFlow(registry);
            SeedPrototypeProductionLots();
            prototypeEconomyInitialized = PrototypeEconomyBootstrapReady();
            if (!prototypeEconomyInitialized)
            {
                Debug.LogWarning("Prototype economy bootstrap is incomplete; simulation will retry instead of advancing partial state.", this);
                return;
            }
            UpdatePrototypeMarket(force: true);
        }

        private bool PrototypeEconomyBootstrapReady()
        {
            return Economy.TryGetAccount(PlayerEconomyAccountId, out _)
                && Economy.TryGetAccount(PrototypeEconomyContentIds.MerchantAccount, out _)
                && Economy.TryGetAccount(PrototypeEconomyContentIds.ExternalTradeAccount, out _)
                && Economy.TryGetAccount(PrototypeEconomyContentIds.TreasuryAccount, out _)
                && Markets.TryGetMarket(PrototypeEconomyContentIds.MarketInstanceTown, out _)
                && Businesses.TryGetBusiness(PrototypeEconomyContentIds.BusinessInstance, out _)
                && Properties.TryGetProperty(PrototypeEconomyContentIds.PropertyInstance, out _)
                && RegionalFlow.TryGetRegion(PrototypeEconomyContentIds.RegionInstanceTown, out _)
                && RegionalFlow.TryGetCohort(PrototypeEconomyContentIds.CraftLaborCohort, out _)
                && RegionalFlow.TryGetPool(PrototypeEconomyContentIds.IronImportPool, out _)
                && RegionalFlow.TryGetPool(PrototypeEconomyContentIds.WoodImportPool, out _)
                && RegionalFlow.TryGetPool(PrototypeEconomyContentIds.LeatherInputPool, out _)
                && RegionalFlow.TryGetPool(PrototypeEconomyContentIds.SwordExportPool, out _)
                && RegionalFlow.TryGetPool(PrototypeEconomyContentIds.BowExportPool, out _)
                && InstitutionalRevenue.Authorities.Any(item => string.Equals(item.authorityId, PrototypeEconomyContentIds.RevenueAuthority, StringComparison.Ordinal))
                && InstitutionalRevenue.AccountAssignments.Any(item => string.Equals(item.assignmentId, "revenue-account.prototype-town.sales-tax", StringComparison.Ordinal));
        }

        private void SeedPrototypeMarket(DefinitionRegistry registry)
        {
            if (!registry.TryGet(PrototypeEconomyContentIds.MarketTown, out MarketDefinition marketDefinition))
            {
                return;
            }

            if (!Markets.TryGetMarket(PrototypeEconomyContentIds.MarketInstanceTown, out _))
            {
                Markets.CreateMarketInstance(marketDefinition, PrototypeEconomyContentIds.MarketInstanceTown,
                    PrototypeEconomyContentIds.RegionInstanceTown, settlementId: "settlement.prototype-town");
            }
        }

        private void SeedPrototypeBusiness(DefinitionRegistry registry, CurrencyDefinition gold)
        {
            if (!registry.TryGet(PrototypeEconomyContentIds.BusinessWorkshop, out BusinessDefinition _))
            {
                return;
            }

            if (!Businesses.TryGetBusiness(PrototypeEconomyContentIds.BusinessInstance, out _))
            {
                Businesses.CreateBusiness(new BusinessInstanceData
                {
                    businessId = PrototypeEconomyContentIds.BusinessInstance,
                    businessDefinitionId = PrototypeEconomyContentIds.BusinessWorkshop,
                    displayName = "Prototype Town Arms Workshop",
                    linkedOrganizationId = "organization.prototype-town",
                    founderSubjectIds = new[] { "person.prototype.merchant" },
                    operatingCurrencyIds = new[] { gold.Id },
                    state = BusinessState.Planned
                }, "business-create.prototype-town-workshop");
                Businesses.AddOwnership(new BusinessOwnershipRecordData
                {
                    ownershipRecordId = "business-ownership.prototype-town-workshop",
                    businessId = PrototypeEconomyContentIds.BusinessInstance,
                    owner = new BusinessSubjectReferenceData { kind = BusinessOwnerSubjectKind.Person, subjectId = "person.prototype.merchant" },
                    category = BusinessOwnershipCategory.SoleOwner,
                    economicShare = new BusinessRationalData { numerator = 10000L, denominator = 10000L },
                    votingShare = new BusinessRationalData { numerator = 10000L, denominator = 10000L }
                });
                Businesses.AddEstablishment(new BusinessEstablishmentData
                {
                    establishmentId = PrototypeEconomyContentIds.EstablishmentInstance,
                    businessId = PrototypeEconomyContentIds.BusinessInstance,
                    type = BusinessEstablishmentType.Workshop,
                    displayName = "Prototype Town Arms Workshop",
                    state = BusinessEstablishmentState.Open,
                    locationReferenceId = "place.prototype-town"
                });
                Businesses.AssignAccount(new BusinessAccountAssignmentData
                {
                    assignmentId = "business-account.prototype-town-workshop",
                    businessId = PrototypeEconomyContentIds.BusinessInstance,
                    accountId = PrototypeEconomyContentIds.MerchantAccount,
                    purpose = BusinessAccountPurpose.OperatingFunds,
                    establishmentId = PrototypeEconomyContentIds.EstablishmentInstance,
                    authorizedSpenderSubjectIds = new[] { "person.prototype.merchant" }
                }, Economy);
                Businesses.AssignInventory(new BusinessInventoryAssignmentData
                {
                    assignmentId = "business-inventory.prototype-town-workshop",
                    businessId = PrototypeEconomyContentIds.BusinessInstance,
                    inventoryId = "inventory.prototype-town.finished-goods",
                    establishmentId = PrototypeEconomyContentIds.EstablishmentInstance,
                    purpose = BusinessInventoryPurpose.FinishedGoods,
                    responsibleCustodianSubjectId = "person.prototype.merchant"
                });
                Businesses.TransitionBusiness(PrototypeEconomyContentIds.BusinessInstance, BusinessState.Active, 0d);
            }
        }

        private void SeedPrototypeProperty()
        {
            if (Properties.TryGetProperty(PrototypeEconomyContentIds.PropertyInstance, out _))
            {
                return;
            }

            Properties.RegisterProperty(new PropertyInstanceData
            {
                propertyId = PrototypeEconomyContentIds.PropertyInstance,
                propertyDefinitionId = PrototypeEconomyContentIds.PropertyWorkshop,
                displayName = "Prototype Town Workshop",
                state = PropertyState.Occupied,
                currentUses = new[] { PropertyUseCategory.Production, PropertyUseCategory.Retail },
                ownershipModel = PropertyOwnershipModel.Business,
                sceneObjectReferenceId = "Prototype Market Stall"
            });
            Properties.CreateOwnership(new PropertyOwnershipInterestData
            {
                ownershipInterestId = "property-ownership.prototype-town-workshop",
                propertyId = PrototypeEconomyContentIds.PropertyInstance,
                owner = new PropertySubjectReferenceData { kind = PropertySubjectKind.Business, subjectId = PrototypeEconomyContentIds.BusinessInstance },
                ownershipModel = PropertyOwnershipModel.Business,
                ownershipShare = PropertyShareData.Full(),
                votingShare = PropertyShareData.Full(),
                economicBenefitShare = PropertyShareData.Full()
            });
            Properties.LinkBusinessEstablishment(PrototypeEconomyContentIds.PropertyInstance, PrototypeEconomyContentIds.EstablishmentInstance, Businesses);
        }

        private void SeedPrototypeRevenue()
        {
            if (!InstitutionalRevenue.Authorities.Any(item => string.Equals(item.authorityId, PrototypeEconomyContentIds.RevenueAuthority, StringComparison.Ordinal)))
            {
                InstitutionalRevenue.RegisterAuthority(new InstitutionalRevenueAuthorityData
                {
                    authorityId = PrototypeEconomyContentIds.RevenueAuthority,
                    institutionId = "organization.prototype-town",
                    institutionKind = InstitutionKind.SettlementFoundation,
                    authorityCategory = InstitutionalRevenueAuthorityCategory.Assess,
                    sourceReferenceId = "law.prototype-town.sales-tax",
                    sourceRuntime = "PrototypeEconomy",
                    permittedRevenueDefinitionIds = new[] { PrototypeEconomyContentIds.RevenueSalesTax },
                    permittedRevenueCategories = new[] { InstitutionalRevenueCategory.SalesTaxFoundation },
                    permittedSubjectKinds = new[] { RevenueSubjectKind.Buyer, RevenueSubjectKind.Seller },
                    permittedCurrencyIds = new[] { PrototypeEconomyContentIds.CurrencyGold },
                    scopeReferenceId = PrototypeEconomyContentIds.RegionInstanceTown,
                    canAssess = true,
                    canCollect = true,
                    canReceiveRemittance = true,
                    canIssueRefund = true,
                    canAdjust = true,
                    canAudit = true,
                    canAllocateRevenue = true,
                    provenance = "prototype-town-economic-bootstrap"
                }, "revenue-authority-create.prototype-town");
            }

            if (!InstitutionalRevenue.AccountAssignments.Any(item => string.Equals(item.assignmentId, "revenue-account.prototype-town.sales-tax", StringComparison.Ordinal)))
            {
                InstitutionalRevenue.AssignRevenueAccount(new InstitutionalRevenueAccountAssignmentData
                {
                    assignmentId = "revenue-account.prototype-town.sales-tax",
                    institutionId = "organization.prototype-town",
                    institutionKind = InstitutionKind.SettlementFoundation,
                    accountId = PrototypeEconomyContentIds.TreasuryAccount,
                    purpose = RevenueAccountPurpose.TaxCollection,
                    currencyId = PrototypeEconomyContentIds.CurrencyGold,
                    receivingAuthorityId = PrototypeEconomyContentIds.RevenueAuthority,
                    provenance = "prototype-town-economic-bootstrap"
                }, "revenue-account-create.prototype-town");
            }
        }

        private void SeedPrototypeRegionalFlow(DefinitionRegistry registry)
        {
            if (!registry.TryGet(PrototypeEconomyContentIds.RegionTown, out EconomicRegionDefinition regionDefinition))
            {
                return;
            }

            if (!RegionalFlow.TryGetRegion(PrototypeEconomyContentIds.RegionInstanceTown, out _))
            {
                RegionalFlow.RegisterRegion(new EconomicRegionData
                {
                    regionId = PrototypeEconomyContentIds.RegionInstanceTown,
                    regionDefinitionId = regionDefinition.Id,
                    displayName = "Prototype Town",
                    state = EconomicRegionState.Active,
                    simulationFidelity = RegionalSimulationFidelity.AggregatePools
                }, "regional-create.prototype-town");
            }

            if (!RegionalFlow.TryGetCohort(PrototypeEconomyContentIds.CraftLaborCohort, out _))
            {
                RegionalFlow.RegisterCohort(new EconomicCohortData
                {
                    cohortId = PrototypeEconomyContentIds.CraftLaborCohort,
                    regionId = PrototypeEconomyContentIds.RegionInstanceTown,
                    category = EconomicCohortCategory.Craftspeople,
                    populationQuantity = 2L,
                    laborDistribution = new[]
                    {
                        new RegionalLaborQuantityData { laborCategory = LaborCategory.CraftLabor, units = 2L }
                    },
                    productionProfileIds = new[] { PrototypeEconomyContentIds.ProductionSword, PrototypeEconomyContentIds.ProductionBow },
                    accountId = PrototypeEconomyContentIds.MerchantAccount,
                    provenance = "prototype-town-aggregate-craftspeople"
                }, "regional-cohort-create.prototype-town-craftspeople");
            }

            MerchantSimulationPolicyData simulation = PrototypeSimulationPolicy;
            RegisterPool(PrototypeEconomyContentIds.IronImportPool, PrototypeEconomyContentIds.CommodityIronOre, simulation.rawResourceOpeningStock, CommodityPoolPurpose.ImportBuffer);
            RegisterPool(PrototypeEconomyContentIds.WoodImportPool, PrototypeEconomyContentIds.CommodityWoodLog, simulation.rawResourceOpeningStock, CommodityPoolPurpose.ImportBuffer);
            RegisterPool(PrototypeEconomyContentIds.LeatherInputPool, PrototypeEconomyContentIds.CommodityLeatherStrip, simulation.leatherOpeningStock, CommodityPoolPurpose.ProducerInput);
            RegisterPool(PrototypeEconomyContentIds.SwordExportPool, PrototypeEconomyContentIds.CommoditySword, simulation.exportOpeningStock, CommodityPoolPurpose.ExportStock);
            RegisterPool(PrototypeEconomyContentIds.BowExportPool, PrototypeEconomyContentIds.CommodityBow, simulation.exportOpeningStock, CommodityPoolPurpose.ExportStock);
        }

        private void RegisterPool(string poolId, string commodityId, long quantity, CommodityPoolPurpose purpose)
        {
            if (RegionalFlow.TryGetPool(poolId, out _))
            {
                return;
            }

            RegionalFlow.RegisterCommodityPool(new CommodityPoolData
            {
                poolId = poolId,
                regionId = PrototypeEconomyContentIds.RegionInstanceTown,
                commodityId = commodityId,
                unit = CommodityUnit.Each,
                totalQuantity = quantity,
                purpose = purpose,
                owner = new RegionalSubjectReferenceData { subjectKind = "business", subjectId = PrototypeEconomyContentIds.BusinessInstance }
            }, $"regional-pool-create.{poolId}");
        }

        private void SeedPrototypeProductionLots()
        {
            ReconcileOpeningProductionLot(PrototypeEconomyContentIds.ItemSword, "opening-sword", 0d);
            ReconcileOpeningProductionLot(PrototypeEconomyContentIds.ItemBow, "opening-bow", 0d);
        }

        private void ReconcileOpeningProductionLot(string itemDefinitionId, string suffix, double worldTime)
        {
            long represented = AggregatePrototypeLotStock(itemDefinitionId);
            long pooled = PoolSupply(PrototypeEconomyContentIds.PoolForItem(itemDefinitionId));
            long missing = Math.Max(0L, pooled - represented);
            if (missing <= 0L)
            {
                return;
            }

            RecordPrototypeProductionLot(itemDefinitionId, missing, suffix, worldTime, "prototype-town-opening-stock");
        }

        private bool RecordPrototypeProductionLot(string itemDefinitionId, long quantity, string suffix, double worldTime, string provenance = "prototype-town-aggregate-production")
        {
            if (quantity <= 0L || quantity > int.MaxValue)
            {
                return false;
            }

            if (string.Equals(provenance, "prototype-town-aggregate-production", StringComparison.Ordinal))
            {
                ProductionLotData existing = ProductionWorkflow.Lots.FirstOrDefault(lot => lot != null
                    && lot.state == ProductionLotState.Active
                    && lot.unit == ProductionQuantityUnit.Count
                    && string.Equals(lot.definitionOrMaterialId, itemDefinitionId, StringComparison.Ordinal)
                    && string.Equals(lot.provenance, provenance, StringComparison.Ordinal));
                if (existing != null)
                {
                    return ProductionWorkflow.AddLotQuantity(existing.lotId, quantity,
                        $"production-lot-add.prototype-town.{itemDefinitionId}.{suffix}").Succeeded;
                }
            }

            ProductionLotData lot = BuildPrototypeProductionLot(itemDefinitionId, quantity, suffix, worldTime, provenance);
            return lot != null && ProductionWorkflow.CreateLot(lot).Succeeded;
        }

        private ProductionLotData BuildPrototypeProductionLot(string itemDefinitionId, long quantity, string suffix, double worldTime, string provenance)
        {
            bool sword = string.Equals(itemDefinitionId, PrototypeEconomyContentIds.ItemSword, StringComparison.Ordinal);
            bool bow = string.Equals(itemDefinitionId, PrototypeEconomyContentIds.ItemBow, StringComparison.Ordinal);
            if (!sword && !bow)
            {
                return null;
            }

            string safeSuffix = string.IsNullOrWhiteSpace(suffix) ? Guid.NewGuid().ToString("N") : suffix.Trim();
            MerchantSimulationPolicyData simulation = PrototypeSimulationPolicy;
            return new ProductionLotData
            {
                lotId = $"production-lot.prototype-town.{(sword ? "sword" : "bow")}.{safeSuffix}",
                definitionOrMaterialId = itemDefinitionId,
                ownerId = PrototypeEconomyContentIds.BusinessInstance,
                custodianId = "inventory.prototype-town.finished-goods",
                quantity = (float)quantity,
                discreteQuantity = quantity,
                unit = ProductionQuantityUnit.Count,
                batchSourceId = $"aggregate-production.{safeSuffix}",
                locationId = PrototypeEconomyContentIds.EstablishmentInstance,
                compositionSummary = sword ? "3 iron and 1 leather" : "4 wood",
                qualitySummary = "Low-quality town production",
                recipeDefinitionId = sword ? PrototypeEconomyContentIds.RecipeSword : PrototypeEconomyContentIds.RecipeBow,
                recipeVersionId = sword ? PrototypeEconomyContentIds.RecipeSwordVersion : PrototypeEconomyContentIds.RecipeBowVersion,
                materialAssignments = PrototypeLotMaterials(itemDefinitionId),
                deterministicSeed = $"prototype-town-production:{itemDefinitionId}:{safeSuffix}",
                createdWorldTime = Math.Max(0d, worldTime),
                baseQuality = simulation.productionBaseQualityBasisPoints / 10000f,
                qualityVariation = simulation.productionQualityVariationBasisPoints / 10000f,
                provenance = provenance ?? string.Empty,
                state = ProductionLotState.Active
            };
        }

        private static ProductionLotMaterialAssignmentData[] PrototypeLotMaterials(string itemDefinitionId)
        {
            if (string.Equals(itemDefinitionId, PrototypeEconomyContentIds.ItemSword, StringComparison.Ordinal))
            {
                return new[]
                {
                    PrototypeLotMaterial("input.blade", "component.blade", PrototypeEconomyContentIds.ItemIronOre, "material.iron", 1f),
                    PrototypeLotMaterial("input.guard", "component.guard", PrototypeEconomyContentIds.ItemIronOre, "material.iron", 1f),
                    PrototypeLotMaterial("input.hilt", "component.hilt", PrototypeEconomyContentIds.ItemLeatherStrip, "material.leather", 1f),
                    PrototypeLotMaterial("input.pommel", "component.pommel", PrototypeEconomyContentIds.ItemIronOre, "material.iron", 1f)
                };
            }

            return new[]
            {
                PrototypeLotMaterial("input.limbs", "component.limbs", PrototypeEconomyContentIds.ItemWoodLog, "material.wood", 2f),
                PrototypeLotMaterial("input.grip", "component.grip", PrototypeEconomyContentIds.ItemWoodLog, "material.wood", 1f),
                PrototypeLotMaterial("input.fittings", "component.fittings", PrototypeEconomyContentIds.ItemWoodLog, "material.wood", 1f)
            };
        }

        private static ProductionLotMaterialAssignmentData PrototypeLotMaterial(string inputId, string componentId, string itemId, string materialId, float quantity)
        {
            return new ProductionLotMaterialAssignmentData
            {
                recipeInputId = inputId,
                componentRoleId = componentId,
                itemDefinitionId = itemId,
                materialDefinitionId = materialId,
                quantity = quantity
            };
        }

        private void AdvancePrototypeEconomy()
        {
            EnsurePrototypeEconomyInitialized();
            UpdatePrototypeMarket(force: false);
        }

        private void UpdatePrototypeMarket(bool force)
        {
            if (!prototypeEconomyInitialized && !force)
            {
                return;
            }

            MerchantSimulationPolicyData simulation = PrototypeSimulationPolicy;
            long boundary = (long)Math.Floor(CurrentEconomyWorldTime / simulation.marketIntervalSeconds);
            lastPrototypeMarketBoundary = LatestPersistedPrototypeMarketBoundary();
            if (boundary <= lastPrototypeMarketBoundary)
            {
                if (lastPrototypeMarketChange == null)
                {
                    lastPrototypeMarketChange = CalculatePrototypeMarketChange(boundary);
                }
                return;
            }

            int processed = 0;
            for (long next = Math.Max(0L, lastPrototypeMarketBoundary + 1L);
                 next <= boundary && processed < simulation.maximumCatchUpIntervalsPerFrame;
                 next++, processed++)
            {
                if (!ProcessPrototypeMarketBoundary(next))
                {
                    break;
                }

                lastPrototypeMarketBoundary = next;
            }
        }

        private PrototypeMarketChangePlan CalculatePrototypeMarketChange(long boundary)
        {
            return PrototypeMarketChangeCalculator.Calculate(
                boundary,
                PoolSupply(PrototypeEconomyContentIds.IronImportPool),
                PoolSupply(PrototypeEconomyContentIds.WoodImportPool),
                PoolSupply(PrototypeEconomyContentIds.LeatherInputPool),
                PoolSupply(PrototypeEconomyContentIds.SwordExportPool),
                PoolSupply(PrototypeEconomyContentIds.BowExportPool), PrototypeSimulationPolicy);
        }

        private bool ProcessPrototypeMarketBoundary(long boundary)
        {
            MerchantSimulationPolicyData simulation = PrototypeSimulationPolicy;
            double observed = boundary * simulation.marketIntervalSeconds;
            double expires = observed + simulation.marketIntervalSeconds;
            ExecutePrototypeProduction(boundary, observed);
            lastPrototypeMarketChange = CalculatePrototypeMarketChange(boundary);
            ExecutePrototypeBackgroundTrade(lastPrototypeMarketChange, observed);
            EnsurePrototypeSecondhandStockMix(boundary, observed);
            bool recorded = RecordMarketState(PrototypeEconomyContentIds.SubjectIronOre, boundary, PoolSupply(PrototypeEconomyContentIds.IronImportPool), lastPrototypeMarketChange.IronDemand, MarketSupplySourceCategory.ImportedAggregate, MarketDemandCategory.ProductionInput, observed, expires)
                && RecordMarketState(PrototypeEconomyContentIds.SubjectWoodLog, boundary, PoolSupply(PrototypeEconomyContentIds.WoodImportPool), lastPrototypeMarketChange.WoodDemand, MarketSupplySourceCategory.ImportedAggregate, MarketDemandCategory.ProductionInput, observed, expires)
                && RecordMarketState(PrototypeEconomyContentIds.SubjectLeatherStrip, boundary, PoolSupply(PrototypeEconomyContentIds.LeatherInputPool), lastPrototypeMarketChange.LeatherDemand, MarketSupplySourceCategory.ImportedAggregate, MarketDemandCategory.ProductionInput, observed, expires)
                && RecordMarketState(PrototypeEconomyContentIds.SubjectSword, boundary, AvailablePrototypeMarketStock(PrototypeEconomyContentIds.ItemSword), lastPrototypeMarketChange.SwordDemand, MarketSupplySourceCategory.ProductionOutput, MarketDemandCategory.OrganizationRequest, observed, expires)
                && RecordMarketState(PrototypeEconomyContentIds.SubjectBow, boundary, AvailablePrototypeMarketStock(PrototypeEconomyContentIds.ItemBow), lastPrototypeMarketChange.BowDemand, MarketSupplySourceCategory.ProductionOutput, MarketDemandCategory.OrganizationRequest, observed, expires);
            if (!recorded)
            {
                return false;
            }

            MarketOperationResult checkpoint = Markets.RecordDemand(new MarketObservationRecordData
            {
                observationId = $"market-checkpoint.prototype-town.{boundary}",
                marketInstanceId = PrototypeEconomyContentIds.MarketInstanceTown,
                marketSubjectId = PrototypeEconomyContentIds.SubjectBow,
                unit = MarketQuantityUnit.Each,
                quantity = 0L,
                availableNowQuantity = 0L,
                demandCategory = MarketDemandCategory.OrganizationRequest,
                sourceReferenceId = boundary.ToString(),
                observedWorldTime = observed,
                expiresWorldTime = observed,
                provenance = "prototype-market-boundary-complete"
            });
            if (checkpoint.Succeeded)
            {
                double cutoff = observed - simulation.retainedMarketIntervals * (double)simulation.marketIntervalSeconds;
                Markets.PruneExpiredSimulationHistory(cutoff);
                dirtyTracker?.MarkDirty($"Prototype market boundary {boundary} completed.");
            }
            return checkpoint.Succeeded;
        }

        private long LatestPersistedPrototypeMarketBoundary()
        {
            return Markets.DemandRecords
                .Where(record => string.Equals(record.provenance, "prototype-market-boundary-complete", StringComparison.Ordinal))
                .Select(record => long.TryParse(record.sourceReferenceId, out long parsed) ? parsed : -1L)
                .DefaultIfEmpty(-1L)
                .Max();
        }

        private long PoolSupply(string poolId) => RegionalFlow.TryGetPool(poolId, out CommodityPoolData pool) ? pool.AvailableQuantity : 0L;

        private void ExecutePrototypeProduction(long boundary, double worldTime)
        {
            DefinitionRegistry registry = GetDefinitionRegistry();
            if (registry.TryGet(PrototypeEconomyContentIds.ProductionSword, out AggregateProductionProfileDefinition sword))
            {
                ProductionCapacityResultData capacity = RegionalFlow.EvaluateProductionCapacity(
                    $"capacity.prototype-town.sword.{boundary}", PrototypeEconomyContentIds.RegionInstanceTown,
                    PrototypeEconomyContentIds.CraftLaborCohort, sword, worldTime,
                    new[] { PrototypeEconomyContentIds.IronImportPool, PrototypeEconomyContentIds.LeatherInputPool },
                    infrastructureLimitUnits: sword.CapacityLimitUnits);
                if (capacity.effectiveOutputCapacity > 0L
                    && PoolSupply(PrototypeEconomyContentIds.SwordExportPool) < PrototypeSimulationPolicy.exportStockTarget
                    && IsPrototypeProductionProfitable(sword))
                {
                    RegionalFlowRuntimeSaveData regionalRollback = RegionalFlow.CreateSaveData();
                    MarketRuntimeSaveData marketRollback = Markets.CreateSaveData();
                    ProductionWorkflowRuntimeSaveData productionRollback = ProductionWorkflow.CreateSaveData();
                    RegionalFlowOperationResult result = RegionalFlow.ExecuteAggregateProduction($"production.prototype-town.sword.{boundary}", PrototypeEconomyContentIds.RegionInstanceTown,
                        PrototypeEconomyContentIds.CraftLaborCohort, sword, new[] { PrototypeEconomyContentIds.IronImportPool, PrototypeEconomyContentIds.LeatherInputPool },
                        new[] { PrototypeEconomyContentIds.SwordExportPool }, boundary, worldTime, Markets,
                        PrototypeEconomyContentIds.MarketInstanceTown, $"regional-production.sword.{boundary}");
                    if (result.Succeeded && !result.Duplicate)
                    {
                        long quantity = sword.Outputs.Where(output => output.CommodityId == PrototypeEconomyContentIds.CommoditySword)
                            .Sum(output => output.Quantity * sword.YieldBasisPoints / 10000L);
                        if (!RecordPrototypeProductionLot(PrototypeEconomyContentIds.ItemSword, quantity, $"sword.{boundary}", worldTime))
                        {
                            RegionalFlow.RestoreFromSaveData(regionalRollback, registry);
                            Markets.RestoreFromSaveData(marketRollback, registry);
                            ProductionWorkflow.RestoreFromSaveData(productionRollback, registry);
                        }
                    }
                }
            }

            if (registry.TryGet(PrototypeEconomyContentIds.ProductionBow, out AggregateProductionProfileDefinition bow))
            {
                ProductionCapacityResultData capacity = RegionalFlow.EvaluateProductionCapacity(
                    $"capacity.prototype-town.bow.{boundary}", PrototypeEconomyContentIds.RegionInstanceTown,
                    PrototypeEconomyContentIds.CraftLaborCohort, bow, worldTime,
                    new[] { PrototypeEconomyContentIds.WoodImportPool }, infrastructureLimitUnits: bow.CapacityLimitUnits);
                if (capacity.effectiveOutputCapacity <= 0L
                    || PoolSupply(PrototypeEconomyContentIds.BowExportPool) >= PrototypeSimulationPolicy.exportStockTarget
                    || !IsPrototypeProductionProfitable(bow))
                {
                    return;
                }
                RegionalFlowRuntimeSaveData regionalRollback = RegionalFlow.CreateSaveData();
                MarketRuntimeSaveData marketRollback = Markets.CreateSaveData();
                ProductionWorkflowRuntimeSaveData productionRollback = ProductionWorkflow.CreateSaveData();
                RegionalFlowOperationResult result = RegionalFlow.ExecuteAggregateProduction($"production.prototype-town.bow.{boundary}", PrototypeEconomyContentIds.RegionInstanceTown,
                    PrototypeEconomyContentIds.CraftLaborCohort, bow, new[] { PrototypeEconomyContentIds.WoodImportPool },
                    new[] { PrototypeEconomyContentIds.BowExportPool }, boundary, worldTime, Markets,
                    PrototypeEconomyContentIds.MarketInstanceTown, $"regional-production.bow.{boundary}");
                if (result.Succeeded && !result.Duplicate)
                {
                    long quantity = bow.Outputs.Where(output => output.CommodityId == PrototypeEconomyContentIds.CommodityBow)
                        .Sum(output => output.Quantity * bow.YieldBasisPoints / 10000L);
                    if (!RecordPrototypeProductionLot(PrototypeEconomyContentIds.ItemBow, quantity, $"bow.{boundary}", worldTime))
                    {
                        RegionalFlow.RestoreFromSaveData(regionalRollback, registry);
                        Markets.RestoreFromSaveData(marketRollback, registry);
                        ProductionWorkflow.RestoreFromSaveData(productionRollback, registry);
                    }
                }
            }
        }

        private bool IsPrototypeProductionProfitable(AggregateProductionProfileDefinition profile)
        {
            if (profile == null)
            {
                return false;
            }

            long inputCost = 0L;
            long outputRevenue = 0L;
            try
            {
                foreach (RegionalCommodityQuantityDefinitionData input in profile.Inputs)
                {
                    inputCost = checked(inputCost + checked(ResolvePrototypeReferencePrice(PrototypeSubjectForCommodity(input.CommodityId)) * input.Quantity));
                }

                foreach (RegionalCommodityQuantityDefinitionData output in profile.Outputs)
                {
                    long yieldedQuantity = checked(output.Quantity * profile.YieldBasisPoints / 10000L);
                    outputRevenue = checked(outputRevenue + checked(ResolvePrototypeReferencePrice(PrototypeSubjectForCommodity(output.CommodityId)) * yieldedQuantity));
                }

                long requiredRevenue = checked(inputCost + checked(inputCost * PrototypeSimulationPolicy.minimumProductionMarginBasisPoints / 10000L));
                return outputRevenue >= requiredRevenue;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        private static string PrototypeSubjectForCommodity(string commodityId)
        {
            if (string.Equals(commodityId, PrototypeEconomyContentIds.CommodityIronOre, StringComparison.Ordinal)) return PrototypeEconomyContentIds.SubjectIronOre;
            if (string.Equals(commodityId, PrototypeEconomyContentIds.CommodityWoodLog, StringComparison.Ordinal)) return PrototypeEconomyContentIds.SubjectWoodLog;
            if (string.Equals(commodityId, PrototypeEconomyContentIds.CommodityLeatherStrip, StringComparison.Ordinal)) return PrototypeEconomyContentIds.SubjectLeatherStrip;
            if (string.Equals(commodityId, PrototypeEconomyContentIds.CommoditySword, StringComparison.Ordinal)) return PrototypeEconomyContentIds.SubjectSword;
            if (string.Equals(commodityId, PrototypeEconomyContentIds.CommodityBow, StringComparison.Ordinal)) return PrototypeEconomyContentIds.SubjectBow;
            return string.Empty;
        }

        private void ExecutePrototypeBackgroundTrade(PrototypeMarketChangePlan plan, double worldTime)
        {
            if (plan == null)
            {
                return;
            }

            bool changed = false;
            changed |= TryExecutePrototypeBackgroundTrade(PrototypeEconomyContentIds.ItemIronOre, plan.IronImports, townImports: true, plan.Boundary, worldTime);
            changed |= TryExecutePrototypeBackgroundTrade(PrototypeEconomyContentIds.ItemWoodLog, plan.WoodImports, townImports: true, plan.Boundary, worldTime);
            changed |= TryExecutePrototypeBackgroundTrade(PrototypeEconomyContentIds.ItemLeatherStrip, plan.LeatherImports, townImports: true, plan.Boundary, worldTime);
            changed |= TryExecutePrototypeBackgroundTrade(PrototypeEconomyContentIds.ItemSword, plan.SwordExports, townImports: false, plan.Boundary, worldTime);
            changed |= TryExecutePrototypeBackgroundTrade(PrototypeEconomyContentIds.ItemBow, plan.BowExports, townImports: false, plan.Boundary, worldTime);
            if (changed)
            {
                dirtyTracker?.MarkDirty($"Prototype aggregate market changed at boundary {plan.Boundary}.");
            }
        }

        private bool TryExecutePrototypeBackgroundTrade(string itemDefinitionId, long quantity, bool townImports, long boundary, double worldTime)
        {
            if (quantity <= 0L)
            {
                return false;
            }

            string subjectId = PrototypeEconomyContentIds.SubjectForItem(itemDefinitionId);
            string poolId = PrototypeEconomyContentIds.PoolForItem(itemDefinitionId);
            string commodityId = PrototypeEconomyContentIds.CommodityForItem(itemDefinitionId);
            if (string.IsNullOrWhiteSpace(subjectId) || string.IsNullOrWhiteSpace(poolId) || string.IsNullOrWhiteSpace(commodityId))
            {
                return false;
            }

            long unitPrice = ResolvePrototypeReferencePrice(subjectId);
            long amount;
            try
            {
                amount = checked(unitPrice * quantity);
            }
            catch (OverflowException)
            {
                return false;
            }

            string direction = townImports ? "import" : "export";
            string operationId = $"prototype-market.background.{direction}.{itemDefinitionId}.{boundary}";
            PrototypeBackgroundTradeRollback rollback = CapturePrototypeBackgroundTradeRollback();
            string payer = townImports ? PrototypeEconomyContentIds.MerchantAccount : PrototypeEconomyContentIds.ExternalTradeAccount;
            string recipient = townImports ? PrototypeEconomyContentIds.ExternalTradeAccount : PrototypeEconomyContentIds.MerchantAccount;
            TradeParticipantData seller = townImports
                ? PrototypeExternalTradeParticipant(TradeParticipantRole.Seller)
                : PrototypeMerchantTradeParticipant(TradeParticipantRole.Seller);
            TradeParticipantData buyer = townImports
                ? PrototypeMerchantTradeParticipant(TradeParticipantRole.Buyer)
                : PrototypeExternalTradeParticipant(TradeParticipantRole.Buyer);
            if (!BeginPrototypeTradeAudit(operationId, itemDefinitionId, checked((int)quantity), amount, Array.Empty<string>(),
                    seller, buyer, Array.Empty<string>(), worldTime, out string tradeSessionId, out _))
            {
                RestorePrototypeBackgroundTrade(rollback);
                return false;
            }
            EconomyOperationResult payment = Economy.Transfer(operationId, payer, recipient,
                new MoneyAmount(PrototypeEconomyContentIds.CurrencyGold, amount), EconomyTransactionKind.Payment,
                actorId: PrototypeEconomyContentIds.BusinessInstance, priceSnapshotId: $"aggregate-price.{subjectId}.{boundary}", worldTime: worldTime);
            if (!payment.Succeeded)
            {
                RestorePrototypeBackgroundTrade(rollback);
                return false;
            }

            RegionalFlowOperationResult stock = RegionalFlow.ApplyQuantityOperation(new AggregateQuantityOperationData
            {
                operationId = $"regional-background.{direction}.{itemDefinitionId}.{boundary}",
                operationKind = townImports ? AggregateQuantityOperationKind.Add : AggregateQuantityOperationKind.Consume,
                commodityId = commodityId,
                sourcePoolId = townImports ? string.Empty : poolId,
                destinationPoolId = townImports ? poolId : string.Empty,
                unit = CommodityUnit.Each,
                quantity = quantity,
                purpose = townImports ? "aggregate-import-arrival" : "aggregate-export-sale",
                sourceEventId = operationId,
                worldTime = worldTime,
                provenance = "prototype-market-change-calculator"
            }, $"regional-background.{operationId}");
            if (!stock.Succeeded)
            {
                RestorePrototypeBackgroundTrade(rollback);
                return false;
            }

            if (!townImports && !ConsumePrototypeProductionLots(itemDefinitionId, checked((int)quantity), operationId, out _))
            {
                RestorePrototypeBackgroundTrade(rollback);
                return false;
            }

            if (!CompletePrototypeTradeAudit(operationId, tradeSessionId, payment,
                    new[] { $"aggregate-transfer.{operationId}.{itemDefinitionId}.{quantity}" }, worldTime,
                    merchantRevenue: !townImports, itemDefinitionId, Array.Empty<string>(), out _, out _))
            {
                RestorePrototypeBackgroundTrade(rollback);
                return false;
            }

            Markets.AddTransactionObservation($"market-observation.{operationId}", payment.Transaction,
                PrototypeEconomyContentIds.MarketInstanceTown, subjectId,
                MarketTransactionObservationPolicy.IncludeCommitted, true, worldTime);
            return true;
        }

        private long ResolvePrototypeReferencePrice(string subjectId)
        {
            if (Markets.TryGetCurrentPrice(PrototypeEconomyContentIds.MarketInstanceTown, subjectId, out MarketPriceRecordData price)
                && price != null && price.referenceAmountUnits > 0L)
            {
                return price.referenceAmountUnits;
            }

            DefinitionRegistry registry = GetDefinitionRegistry();
            return registry != null && registry.TryGet(subjectId, out MarketSubjectDefinition subject)
                ? Math.Max(1L, subject.BaselinePriceUnits)
                : 1L;
        }

        private void EnsurePrototypeSecondhandStockMix(long boundary, double worldTime)
        {
            EnsurePrototypeSecondhandStockMix(PrototypeEconomyContentIds.ItemSword, boundary, worldTime);
            EnsurePrototypeSecondhandStockMix(PrototypeEconomyContentIds.ItemBow, boundary, worldTime);
        }

        private void EnsurePrototypeSecondhandStockMix(string itemDefinitionId, long boundary, double worldTime)
        {
            DefinitionRegistry registry = GetDefinitionRegistry();
            if (registry == null
                || !registry.TryGet(PrototypeEconomyContentIds.BusinessWorkshop, out BusinessDefinition businessDefinition)
                || !registry.TryGet(itemDefinitionId, out ItemDefinition definition))
            {
                return;
            }

            MerchantSecondhandStockPolicyData policy = businessDefinition.SecondhandStockPolicy;
            long aggregate = PoolSupply(PrototypeEconomyContentIds.PoolForItem(itemDefinitionId));
            IReadOnlyList<BusinessStockClassificationData> allExact = GetAllExactPrototypeExportStock(itemDefinitionId);
            int exact = allExact.Count;
            long totalLong = aggregate + exact;
            if (!policy.enabled || totalLong < policy.minimumEligibleStockQuantity || totalLong > int.MaxValue)
            {
                return;
            }

            string targetSeed = $"{PrototypeEconomyContentIds.BusinessInstance}:{itemDefinitionId}:secondhand-mix:{boundary}";
            int targetExact = MerchantSecondhandStockCalculator.CalculateRetailTargetCount(aggregate, exact, policy, DeterministicPrototypeUInt(targetSeed));
            PrototypeTradeRollback rollback = CapturePrototypeTradeRollback();
            bool classificationChanged = false;
            HashSet<string> saleIds = allExact
                .OrderBy(stock => DeterministicPrototypeUInt($"{targetSeed}:{stock.itemInstanceId}"))
                .ThenBy(stock => stock.stockClassificationId, StringComparer.Ordinal)
                .Take(Math.Min(targetExact, allExact.Count))
                .Select(stock => stock.itemInstanceId)
                .ToHashSet(StringComparer.Ordinal);
            foreach (BusinessStockClassificationData stock in allExact)
            {
                bool shouldSell = saleIds.Contains(stock.itemInstanceId);
                bool customerResale = (stock.intendedUse ?? string.Empty).StartsWith("customer-resale", StringComparison.Ordinal);
                string intendedUse = shouldSell
                    ? customerResale ? "customer-resale" : "secondhand-retail"
                    : customerResale ? "customer-resale-reserve" : "secondhand-reserve";
                BusinessStockCategory category = shouldSell ? BusinessStockCategory.ForSale : BusinessStockCategory.FinishedProduct;
                if (stock.saleEligible != shouldSell
                    || stock.category != category
                    || !string.Equals(stock.intendedUse, intendedUse, StringComparison.Ordinal))
                {
                    BusinessOperationResult reclassified = Businesses.ReclassifyExactStock(stock.stockClassificationId, stock.itemInstanceId,
                        category, intendedUse, shouldSell, productionEligible: false);
                    if (!reclassified.Succeeded)
                    {
                        RestorePrototypeTrade(rollback);
                        Debug.LogWarning($"Could not rebalance secondhand stock '{stock.itemInstanceId}': {reclassified.Message}");
                        return;
                    }

                    classificationChanged = true;
                }
            }

            int requested = Math.Min(targetExact - exact, (int)Math.Min(aggregate, AggregatePrototypeLotStock(itemDefinitionId)));
            if (requested <= 0)
            {
                if (classificationChanged)
                {
                    dirtyTracker?.MarkDirty($"Balanced exact secondhand {definition.DisplayName} storefront stock.");
                }
                return;
            }

            string operationId = $"prototype-market.secondhand-stock.{itemDefinitionId}.{boundary}.{Businesses.Revision}.{ProductionWorkflow.Revision}";
            if (!MaterializePrototypeSecondhandStock(definition, policy, requested, operationId, boundary, worldTime, out string failure))
            {
                RestorePrototypeTrade(rollback);
                Debug.LogWarning($"Could not prepare the prototype merchant's secondhand stock mix: {failure}");
                return;
            }

            RegionalFlowOperationResult consumed = ApplyPrototypePoolChange(itemDefinitionId, requested, add: false, operationId, worldTime);
            if (!consumed.Succeeded)
            {
                RestorePrototypeTrade(rollback);
                Debug.LogWarning($"Could not synchronize the prototype merchant's secondhand stock mix: {consumed.Message}");
                return;
            }

            dirtyTracker?.MarkDirty($"Prepared {requested} exact secondhand {definition.DisplayName} stock units.");
        }

        private bool RecordPrototypeCraftPayroll(CraftingOperationRecordData operation, IReadOnlyList<ProfessionalActivityOperationResult> activityResults, out string payrollMessage)
        {
            payrollMessage = string.Empty;
            if (operation == null || string.IsNullOrWhiteSpace(operation.operationId))
            {
                payrollMessage = "Payroll could not identify the completed crafting operation.";
                return false;
            }

            EnsurePrototypeEconomyInitialized();
            string personId = ResolvePlayerPersonId();
            EmploymentRecordData employment = PositionEmployment.QueryEmploymentByPerson(personId, activeOnly: true)
                .Where(record => string.Equals(record.employerOrganizationId, "organization.prototype-town", StringComparison.Ordinal))
                .Where(record => string.IsNullOrWhiteSpace(record.compensationPolicyId)
                    || string.Equals(record.compensationPolicyId, PrototypeEconomyContentIds.CompensationTownCraft, StringComparison.Ordinal))
                .OrderBy(record => record.employmentId, StringComparer.Ordinal)
                .FirstOrDefault();
            if (employment == null)
            {
                return true;
            }

            if (!EnsurePrototypeEmploymentContract(employment, out string contractFailure))
            {
                payrollMessage = $"Payroll could not verify the employment contract: {contractFailure}";
                return false;
            }

            PayrollRuntimeSaveData payrollRollback = Payroll.CreateSaveData();
            EconomyRuntimeSaveData economyRollback = Economy.CreateSaveData();
            BusinessRuntimeSaveData businessRollback = Businesses.CreateSaveData();
            string suffix = operation.operationId;
            string agreementId = $"compensation-agreement.{employment.employmentId}.prototype-craft";
            double end = Math.Max(0.001d, CurrentEconomyWorldTime);
            double start = Math.Max(0d, end - Math.Max(0.001d, operation.craftDurationSeconds));
            if (end <= start) end = start + 0.001d;
            long output = Math.Max(1L, operation.outputs?.Where(item => item != null && item.createdItemInstance).Sum(item => (long)item.quantity) ?? 1L);
            string workSessionId = $"work-session.{suffix}";
            string timesheetId = $"timesheet.{suffix}";
            string periodId = $"pay-period.{suffix}";
            string calculationId = $"payroll-calculation.{suffix}";
            string obligationId = $"payroll-obligation.{suffix}";
            string payRunId = $"payroll-run.{suffix}";

            PayrollOperationResult step = Payroll.ActivateAgreement(new CompensationAgreementData
            {
                agreementId = agreementId,
                compensationDefinitionId = PrototypeEconomyContentIds.CompensationTownCraft,
                employmentId = employment.employmentId,
                employeePersonId = personId,
                employerSubjectId = employment.employerOrganizationId,
                employerFundingAccountId = PrototypeEconomyContentIds.MerchantAccount,
                employeeAccountId = PlayerEconomyAccountId,
                positionInstanceId = employment.positionInstanceId,
                effectiveStartWorldTime = start,
                effectiveEndWorldTime = -1d
            }, PositionEmployment, Economy, $"payroll-agreement.{employment.employmentId}");
            if (!step.Succeeded) return RestorePrototypePayroll(payrollRollback, economyRollback, businessRollback, step.Message, out payrollMessage);

            step = Payroll.RecordWorkSession(new WorkSessionData
            {
                workSessionId = workSessionId,
                agreementId = agreementId,
                startWorldTime = start,
                endWorldTime = end,
                durationMinutes = Math.Max(1L, (long)Math.Ceiling(Math.Max(0d, operation.craftDurationSeconds) / 60d)),
                creditedOutputQuantity = output,
                taskDefinitionId = operation.recipeId,
                evidenceIds = (activityResults ?? Array.Empty<ProfessionalActivityOperationResult>())
                    .Where(result => result != null && result.Succeeded && result.Activity != null)
                    .Select(result => result.Activity.activityId)
                    .ToArray(),
                sourceId = operation.operationId
            }, PositionEmployment, $"payroll-work.{suffix}");
            if (!step.Succeeded) return RestorePrototypePayroll(payrollRollback, economyRollback, businessRollback, step.Message, out payrollMessage);

            step = Payroll.SubmitTimesheet(new TimesheetData
            {
                timesheetId = timesheetId,
                agreementId = agreementId,
                workSessionIds = new[] { workSessionId },
                submittedByPersonId = personId,
                submittedWorldTime = end
            }, $"payroll-timesheet.{suffix}");
            if (!step.Succeeded) return RestorePrototypePayroll(payrollRollback, economyRollback, businessRollback, step.Message, out payrollMessage);
            step = Payroll.ApproveTimesheet(timesheetId, "authority.prototype-town-workshop", end, $"payroll-approve.{suffix}");
            if (!step.Succeeded) return RestorePrototypePayroll(payrollRollback, economyRollback, businessRollback, step.Message, out payrollMessage);
            step = Payroll.CreatePayPeriod(new PayPeriodData
            {
                payPeriodId = periodId,
                agreementId = agreementId,
                startWorldTime = start,
                endWorldTime = end,
                dueWorldTime = end
            }, $"payroll-period.{suffix}");
            if (!step.Succeeded) return RestorePrototypePayroll(payrollRollback, economyRollback, businessRollback, step.Message, out payrollMessage);
            step = Payroll.CalculatePay(calculationId, periodId, new[] { workSessionId }, Array.Empty<string>(), $"payroll-calculate.{suffix}");
            if (!step.Succeeded) return RestorePrototypePayroll(payrollRollback, economyRollback, businessRollback, step.Message, out payrollMessage);
            step = Payroll.CreateObligation(obligationId, calculationId, end, $"payroll-obligation.{suffix}");
            if (!step.Succeeded) return RestorePrototypePayroll(payrollRollback, economyRollback, businessRollback, step.Message, out payrollMessage);
            step = Payroll.CreatePayrollRun(payRunId, employment.employerOrganizationId, PrototypeEconomyContentIds.MerchantAccount,
                new[] { obligationId }, PayrollPaymentPolicy.AllOrNothing, end, $"payroll-run.{suffix}");
            if (!step.Succeeded) return RestorePrototypePayroll(payrollRollback, economyRollback, businessRollback, step.Message, out payrollMessage);
            step = Payroll.ExecutePayrollRun(payRunId, Economy, $"payroll-execute.{suffix}");
            if (!step.Succeeded)
            {
                payrollMessage = $"The workshop could not pay this job immediately; wage obligation {obligationId} remains outstanding ({step.Message}).";
                dirtyTracker?.MarkDirty($"Outstanding crafting wage recorded for operation {operation.operationId}.");
                return false;
            }

            PayrollPaymentRecordData paymentRecord = Payroll.Payments
                .Where(payment => string.Equals(payment.obligationId, obligationId, StringComparison.Ordinal))
                .OrderByDescending(payment => payment.paidWorldTime)
                .FirstOrDefault();
            long paidUnits = paymentRecord?.units ?? 0L;
            BusinessOperationResult expense = Businesses.RecordExpense(new BusinessExpenseRecordData
            {
                expenseRecordId = $"business-expense.payroll.{suffix}",
                businessId = PrototypeEconomyContentIds.BusinessInstance,
                establishmentId = PrototypeEconomyContentIds.EstablishmentInstance,
                category = BusinessExpenseCategory.PayrollExpense,
                amount = new BusinessMoneyData { currencyId = PrototypeEconomyContentIds.CurrencyGold, units = paidUnits },
                transactionId = paymentRecord?.economyTransactionId ?? string.Empty,
                payrollObligationId = obligationId,
                payrollPaymentRecordId = paymentRecord?.paymentRecordId ?? string.Empty,
                purchasedItemOrServiceIds = new[] { operation.recipeId },
                recognitionWorldTime = end,
                provenance = "prototype-employed-crafting"
            }, Economy, Payroll);
            if (!expense.Succeeded)
            {
                return RestorePrototypePayroll(payrollRollback, economyRollback, businessRollback, expense.Message, out payrollMessage);
            }

            dirtyTracker?.MarkDirty($"Paid employed crafting work for operation {operation.operationId}.");
            payrollMessage = $"Workshop payroll paid {paidUnits} Gold for the completed employed work.";
            return true;
        }

        private bool EnsurePrototypeEmploymentContract(EmploymentRecordData employment, out string failure)
        {
            failure = string.Empty;
            if (employment == null || string.IsNullOrWhiteSpace(employment.employmentId))
            {
                failure = "An authoritative employment record is required.";
                return false;
            }

            string contractId = $"contract.employment.{employment.employmentId}";
            if (ContractEconomy.TryGetContract(contractId, out EconomyContractData existing))
            {
                if (string.Equals(existing.definitionId, PrototypeEconomyContentIds.ContractEmployment, StringComparison.Ordinal)
                    && string.Equals(existing.terms?.FirstOrDefault()?.externalReferenceId, employment.employmentId, StringComparison.Ordinal))
                {
                    return true;
                }

                failure = $"Contract '{contractId}' exists but does not represent employment '{employment.employmentId}'.";
                return false;
            }

            DefinitionRegistry registry = GetDefinitionRegistry();
            if (registry == null || !registry.TryGet(PrototypeEconomyContentIds.ContractEmployment, out ContractFinanceDefinition definition))
            {
                failure = $"Employment contract definition '{PrototypeEconomyContentIds.ContractEmployment}' is missing.";
                return false;
            }

            ContractRuntimeSaveData rollback = ContractEconomy.CreateSaveData();
            string proposalId = $"proposal.{contractId}";
            string employerPartyId = $"party.employer.{employment.employmentId}";
            string employeePartyId = $"party.employee.{employment.employmentId}";
            ContractProposalData proposal = ContractEconomy.Proposals.FirstOrDefault(item => string.Equals(item.proposalId, proposalId, StringComparison.Ordinal));
            if (proposal == null)
            {
                ContractTermData[] terms = definition.RequiredTerms.Select(source =>
                {
                    ContractTermData term = source.Clone();
                    term.responsiblePartyId = employeePartyId;
                    term.beneficiaryPartyId = employerPartyId;
                    term.externalReferenceId = employment.employmentId;
                    term.description = $"Work performed under employment {employment.employmentId}.";
                    return term;
                }).ToArray();
                ContractEconomyOperationResult created = ContractEconomy.CreateProposal(new ContractProposalData
                {
                    proposalId = proposalId,
                    definitionId = definition.Id,
                    category = definition.Category,
                    state = ContractProposalState.Offered,
                    createdByPartyId = employerPartyId,
                    createdWorldTime = CurrentEconomyWorldTime,
                    parties = new[]
                    {
                        new ContractPartyData
                        {
                            partyId = employerPartyId,
                            role = ContractPartyRole.Offeror,
                            reference = new ContractPartyReferenceData { kind = ContractPartyKind.Organization, subjectId = employment.employerOrganizationId },
                            accountId = PrototypeEconomyContentIds.MerchantAccount
                        },
                        new ContractPartyData
                        {
                            partyId = employeePartyId,
                            role = ContractPartyRole.Offeree,
                            reference = ContractPartyReferenceData.Person(employment.personId),
                            accountId = PlayerEconomyAccountId
                        }
                    },
                    terms = terms
                }, $"contract-employment.propose.{employment.employmentId}");
                if (!created.Succeeded)
                {
                    ContractEconomy.RestoreFromSaveData(rollback, registry);
                    failure = created.Message;
                    return false;
                }
                proposal = created.Proposal;
            }

            foreach (ContractPartyData party in proposal.parties.Where(item => item != null && !item.accepted))
            {
                ContractEconomyOperationResult accepted = ContractEconomy.AcceptProposal(proposalId, party.partyId, CurrentEconomyWorldTime,
                    $"contract-employment.accept.{employment.employmentId}.{party.partyId}");
                if (!accepted.Succeeded)
                {
                    ContractEconomy.RestoreFromSaveData(rollback, registry);
                    failure = accepted.Message;
                    return false;
                }
                proposal = accepted.Proposal;
            }

            ContractEconomyOperationResult activated = ContractEconomy.ActivateProposal(proposalId, contractId, CurrentEconomyWorldTime,
                $"contract-employment.activate.{employment.employmentId}");
            if (!activated.Succeeded)
            {
                ContractEconomy.RestoreFromSaveData(rollback, registry);
                failure = activated.Message;
                return false;
            }

            dirtyTracker?.MarkDirty($"Activated employment contract for {employment.employmentId}.");
            return true;
        }

        private bool RestorePrototypePayroll(PayrollRuntimeSaveData payroll, EconomyRuntimeSaveData economy, BusinessRuntimeSaveData businesses, string failure, out string payrollMessage)
        {
            DefinitionRegistry registry = GetDefinitionRegistry();
            Payroll.RestoreFromSaveData(payroll, registry);
            Economy.RestoreFromSaveData(economy, registry);
            Businesses.RestoreFromSaveData(businesses, registry);
            payrollMessage = $"Payroll could not record this job: {failure}";
            return false;
        }

        private long AvailablePrototypeMarketStock(string itemDefinitionId)
        {
            long aggregate = PoolSupply(PrototypeEconomyContentIds.PoolForItem(itemDefinitionId));
            return PrototypeEconomyContentIds.IsTownExport(itemDefinitionId)
                ? checked(aggregate + GetExactPrototypeExportStock(itemDefinitionId).Count)
                : aggregate;
        }

        private long AggregatePrototypeLotStock(string itemDefinitionId)
        {
            return ProductionWorkflow.Lots
                .Where(lot => lot != null
                    && lot.state == ProductionLotState.Active
                    && lot.unit == ProductionQuantityUnit.Count
                    && string.Equals(lot.definitionOrMaterialId, itemDefinitionId, StringComparison.Ordinal))
                .Sum(lot => lot.WholeQuantity);
        }

        private IReadOnlyList<BusinessStockClassificationData> GetExactPrototypeExportStock(string itemDefinitionId)
        {
            return GetAllExactPrototypeExportStock(itemDefinitionId)
                .Where(stock => stock.saleEligible)
                .ToArray();
        }

        private IReadOnlyList<BusinessStockClassificationData> GetAllExactPrototypeExportStock(string itemDefinitionId)
        {
            return Businesses.StockClassifications
                .Where(stock => stock != null
                    && !string.IsNullOrWhiteSpace(stock.itemInstanceId)
                    && string.Equals(stock.businessId, PrototypeEconomyContentIds.BusinessInstance, StringComparison.Ordinal)
                    && string.Equals(stock.itemDefinitionId, itemDefinitionId, StringComparison.Ordinal))
                .Where(stock => ItemIdentities.TryGetSnapshot(stock.itemInstanceId, out ItemInstanceSnapshot item)
                    && item.LifecycleState is not (ItemLifecycleState.Consumed or ItemLifecycleState.Destroyed or ItemLifecycleState.Disassembled)
                    && item.OwnershipKind == ItemOwnershipKind.OrganizationOwned
                    && string.Equals(item.Data.ownership?.ownerOrganizationId, PrototypeEconomyContentIds.BusinessInstance, StringComparison.Ordinal))
                .OrderBy(stock => stock.stockClassificationId, StringComparer.Ordinal)
                .ToArray();
        }

        private bool TransferExactPrototypeStockToPlayer(ItemDefinition definition, BusinessStockClassificationData stock, out string failure)
        {
            failure = string.Empty;
            BusinessOperationResult released = Businesses.ReleaseExactStock(stock.stockClassificationId, stock.itemInstanceId);
            if (!released.Succeeded)
            {
                failure = released.Message;
                return false;
            }

            string personId = ResolvePlayerPersonId();
            ItemInstanceOperationResult ownership = ItemIdentities.TransferOwnership(stock.itemInstanceId, ItemOwnershipKind.PersonOwned, ownerPersonId: personId);
            ItemInstanceOperationResult custody = ownership.Succeeded
                ? ItemIdentities.TransferCustody(stock.itemInstanceId, custodianPersonId: personId)
                : ItemInstanceOperationResult.Failure(ItemInstanceOperationStatus.ValidationFailed, ownership.Message);
            ItemInstanceOperationResult location = custody.Succeeded
                ? ItemIdentities.SetInventoryLocation(stock.itemInstanceId, personId)
                : ItemInstanceOperationResult.Failure(ItemInstanceOperationStatus.ValidationFailed, custody.Message);
            InventoryInstanceOperationResult added = location.Succeeded
                ? playerInventory.AddExistingItemIdentity(definition, stock.itemInstanceId)
                : InventoryInstanceOperationResult.Failure(location.Message);
            if (!ownership.Succeeded || !custody.Succeeded || !location.Succeeded || !added.Succeeded)
            {
                failure = !added.Succeeded ? added.Message : location?.Message ?? custody?.Message ?? ownership.Message;
                return false;
            }

            return true;
        }

        private bool MaterializePrototypeProductionStock(ItemDefinition definition, int quantity, string operationId, double worldTime, ICollection<string> createdIds, out string failure)
        {
            failure = string.Empty;
            if (quantity <= 0)
            {
                return true;
            }

            List<ProductionLotData> lots = ActivePrototypeProductionLots(definition.Id).ToList();
            if (lots.Sum(lot => lot.WholeQuantity) < quantity)
            {
                failure = "The aggregate stock has no matching recipe/material production lots.";
                return false;
            }

            int remaining = quantity;
            foreach (ProductionLotData lot in lots)
            {
                int fromLot = Math.Min(remaining, checked((int)lot.WholeQuantity));
                for (int offset = 0; offset < fromLot; offset++)
                {
                    long materializationIndex = checked(lot.nextMaterializationIndex + offset);
                    string seed = $"{lot.deterministicSeed}:materialization:{materializationIndex}";
                    string itemInstanceId = StablePrototypeGuid(seed);
                    float quality = DeterministicPrototypeQuality(seed, lot.baseQuality, lot.qualityVariation);
                    ItemQualityAffixCreationResult created = ItemQualityAffixCoordinator.CreateItem(
                        ItemIdentities,
                        ItemCompositions,
                        ItemQualityAffixes,
                        GetDefinitionRegistry(),
                        new ItemQualityAffixCreationRequest
                        {
                            Definition = definition,
                            Classification = ItemInstanceClassification.Unique,
                            ItemInstanceId = itemInstanceId,
                            CreatorPersonId = "person.prototype-town-crafter",
                            OwnerPersonId = ResolvePlayerPersonId(),
                            CustodianPersonId = ResolvePlayerPersonId(),
                            CreationSourceId = lot.lotId,
                            RequireComposition = true,
                            UseDefaultTemplate = true,
                            Purpose = ItemCompositionMutationPurpose.CraftingProduction,
                            ExplicitQuality = PrototypeProductionQuality(itemInstanceId, definition.Id, lot, seed, quality, worldTime)
                        });
                    if (!created.Succeeded)
                    {
                        failure = created.Message;
                        return false;
                    }

                    ItemDurabilityOperationResult durability = ItemDurability.EnsureDefaultDurability(
                        ItemIdentities, ItemCompositions, ItemQualityAffixes, GetDefinitionRegistry(), itemInstanceId);
                    if (!durability.Succeeded)
                    {
                        failure = durability.Message;
                        return false;
                    }

                    InventoryInstanceOperationResult added = playerInventory.AddExistingItemIdentity(definition, itemInstanceId);
                    if (!added.Succeeded)
                    {
                        failure = added.Message;
                        return false;
                    }

                    createdIds?.Add(itemInstanceId);
                }

                ProductionWorkflowResult consumed = ProductionWorkflow.ConsumeLotQuantity(lot.lotId, fromLot, $"{operationId}.lot.{lot.lotId}");
                if (!consumed.Succeeded)
                {
                    failure = consumed.Message;
                    return false;
                }

                remaining -= fromLot;
                if (remaining <= 0)
                {
                    return true;
                }
            }

            failure = "Not enough recipe-linked aggregate stock could be materialized.";
            return false;
        }

        private bool MaterializePrototypeSecondhandStock(ItemDefinition definition, MerchantSecondhandStockPolicyData policy, int quantity, string operationId, long boundary, double worldTime, out string failure)
        {
            failure = string.Empty;
            int remaining = Math.Max(0, quantity);
            foreach (ProductionLotData lot in ActivePrototypeProductionLots(definition.Id))
            {
                int fromLot = Math.Min(remaining, checked((int)lot.WholeQuantity));
                for (int offset = 0; offset < fromLot; offset++)
                {
                    long materializationIndex = checked(lot.nextMaterializationIndex + offset);
                    string seed = $"{lot.deterministicSeed}:secondhand:{materializationIndex}";
                    string itemInstanceId = StablePrototypeGuid(seed);
                    string priorOwnerId = $"person.simulated-secondhand.{StablePrototypeGuid(seed + ":prior-owner"):N}";
                    float quality = DeterministicPrototypeQuality(seed, lot.baseQuality, lot.qualityVariation);
                    ItemQualityAffixCreationResult created = ItemQualityAffixCoordinator.CreateItem(
                        ItemIdentities,
                        ItemCompositions,
                        ItemQualityAffixes,
                        GetDefinitionRegistry(),
                        new ItemQualityAffixCreationRequest
                        {
                            Definition = definition,
                            Classification = ItemInstanceClassification.Unique,
                            ItemInstanceId = itemInstanceId,
                            CreatorPersonId = "person.prototype-town-crafter",
                            OwnerPersonId = priorOwnerId,
                            CustodianPersonId = priorOwnerId,
                            CreationSourceId = lot.lotId,
                            RequireComposition = true,
                            UseDefaultTemplate = true,
                            Purpose = ItemCompositionMutationPurpose.CraftingProduction,
                            ExplicitQuality = PrototypeProductionQuality(itemInstanceId, definition.Id, lot, seed, quality, worldTime, secondhand: true)
                        });
                    if (!created.Succeeded)
                    {
                        failure = created.Message;
                        return false;
                    }

                    ItemDurabilityOperationResult durability = ItemDurability.EnsureDefaultDurability(
                        ItemIdentities, ItemCompositions, ItemQualityAffixes, GetDefinitionRegistry(), itemInstanceId);
                    if (!durability.Succeeded || !ApplyPrototypeSecondhandCondition(itemInstanceId, seed, lot.lotId, policy, out failure))
                    {
                        failure = string.IsNullOrWhiteSpace(failure) ? durability.Message : failure;
                        return false;
                    }

                    ItemInstanceOperationResult ownership = ItemIdentities.TransferOwnership(itemInstanceId, ItemOwnershipKind.OrganizationOwned,
                        ownerOrganizationId: PrototypeEconomyContentIds.BusinessInstance);
                    ItemInstanceOperationResult custody = ownership.Succeeded
                        ? ItemIdentities.TransferCustody(itemInstanceId, custodianContainerId: "inventory.prototype-town.finished-goods")
                        : ItemInstanceOperationResult.Failure(ItemInstanceOperationStatus.ValidationFailed, ownership.Message);
                    ItemInstanceOperationResult location = custody.Succeeded
                        ? ItemIdentities.SetInventoryLocation(itemInstanceId, "inventory.prototype-town.finished-goods")
                        : ItemInstanceOperationResult.Failure(ItemInstanceOperationStatus.ValidationFailed, custody.Message);
                    BusinessOperationResult classified = location.Succeeded
                        ? Businesses.ClassifyStock(new BusinessStockClassificationData
                        {
                            stockClassificationId = $"business-stock.{itemInstanceId}",
                            businessId = PrototypeEconomyContentIds.BusinessInstance,
                            establishmentId = PrototypeEconomyContentIds.EstablishmentInstance,
                            inventoryId = "inventory.prototype-town.finished-goods",
                            itemInstanceId = itemInstanceId,
                            category = BusinessStockCategory.ForSale,
                            intendedUse = "secondhand-retail",
                            saleEligible = true,
                            productionEligible = false,
                            qualityReferenceId = $"item-quality.{itemInstanceId}",
                            durabilityReferenceId = $"item-durability.{itemInstanceId}",
                            marketReferenceId = PrototypeEconomyContentIds.MarketInstanceTown,
                            provenance = $"prototype-secondhand-stock.boundary.{boundary}"
                        }, ItemIdentities)
                        : null;
                    if (!ownership.Succeeded || !custody.Succeeded || !location.Succeeded || classified == null || !classified.Succeeded)
                    {
                        failure = classified?.Message ?? location?.Message ?? custody?.Message ?? ownership.Message;
                        return false;
                    }
                }

                ProductionWorkflowResult consumed = ProductionWorkflow.ConsumeLotQuantity(lot.lotId, fromLot, $"{operationId}.lot.{lot.lotId}");
                if (!consumed.Succeeded)
                {
                    failure = consumed.Message;
                    return false;
                }

                remaining -= fromLot;
                if (remaining <= 0)
                {
                    return true;
                }
            }

            failure = "Not enough recipe-linked aggregate stock could be converted into secondhand inventory.";
            return false;
        }

        private bool ApplyPrototypeSecondhandCondition(string itemInstanceId, string seed, string lotId, MerchantSecondhandStockPolicyData policy, out string failure)
        {
            failure = string.Empty;
            if (!ItemDurability.TryGetDurabilityForItem(itemInstanceId, out ItemDurabilitySnapshot durability))
            {
                failure = "The generated secondhand item has no durability record.";
                return false;
            }

            ItemDurabilityRecordData record = durability.Data.Clone();
            float minimumCondition = policy.minimumConditionBasisPoints / 10000f;
            float conditionRange = (policy.maximumConditionBasisPoints - policy.minimumConditionBasisPoints) / 10000f;
            float normalized = minimumCondition + DeterministicPrototypeUnit(seed + ":condition") * conditionRange;
            record.currentDurability = record.maximumDurability * normalized;
            record.recoverableDamage = Math.Max(0f, record.maximumDurability - record.currentDurability);
            record.wear = record.recoverableDamage;
            record.source = ItemDurabilityRecordSource.Generated;
            record.provenanceId = lotId;
            record.tags = (record.tags ?? Array.Empty<string>())
                .Concat(new[] { "item.secondhand", "condition.used" })
                .Distinct(StringComparer.Ordinal)
                .OrderBy(tag => tag, StringComparer.Ordinal)
                .ToArray();
            foreach (ItemComponentDurabilityData component in record.components)
            {
                component.currentDurability = component.maximumDurability * normalized;
            }

            ItemDurabilityOperationResult set = ItemDurability.SetDurabilityRecord(
                ItemIdentities, ItemCompositions, ItemQualityAffixes, GetDefinitionRegistry(), record);
            failure = set.Message;
            return set.Succeeded;
        }

        private bool ConsumePrototypeProductionLots(string itemDefinitionId, int quantity, string operationId, out string failure)
        {
            failure = string.Empty;
            int remaining = Math.Max(0, quantity);
            foreach (ProductionLotData lot in ActivePrototypeProductionLots(itemDefinitionId))
            {
                int consumedQuantity = Math.Min(remaining, checked((int)lot.WholeQuantity));
                if (consumedQuantity <= 0)
                {
                    continue;
                }

                ProductionWorkflowResult consumed = ProductionWorkflow.ConsumeLotQuantity(lot.lotId, consumedQuantity, $"{operationId}.lot.{lot.lotId}");
                if (!consumed.Succeeded)
                {
                    failure = consumed.Message;
                    return false;
                }

                remaining -= consumedQuantity;
                if (remaining <= 0)
                {
                    return true;
                }
            }

            failure = "Aggregate export stock has no matching production lot quantity.";
            return remaining <= 0;
        }

        private IEnumerable<ProductionLotData> ActivePrototypeProductionLots(string itemDefinitionId)
        {
            return ProductionWorkflow.Lots
                .Where(lot => lot != null
                    && lot.state == ProductionLotState.Active
                    && lot.unit == ProductionQuantityUnit.Count
                    && lot.quantity >= 1f
                    && string.Equals(lot.definitionOrMaterialId, itemDefinitionId, StringComparison.Ordinal))
                .OrderBy(lot => lot.createdWorldTime)
                .ThenBy(lot => lot.lotId, StringComparer.Ordinal);
        }

        private static ItemQualityRecordData PrototypeProductionQuality(string itemInstanceId, string itemDefinitionId, ProductionLotData lot, string seed, float quality, double worldTime, bool secondhand = false)
        {
            return new ItemQualityRecordData
            {
                qualityRecordId = $"item-quality.{itemInstanceId}",
                itemInstanceId = itemInstanceId,
                itemDefinitionId = itemDefinitionId,
                overallQuality = quality,
                source = ItemQualityRecordSource.ProductionGenerated,
                generationPolicyId = "quality-policy.prototype-town-low-quality",
                deterministicSeed = seed,
                creationWorldTime = Math.Max(0d, worldTime).ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                provenanceId = lot.lotId,
                workmanship = new List<ItemWorkmanshipEntryData>
                {
                    new ItemWorkmanshipEntryData
                    {
                        entryId = "workmanship.overall",
                        dimension = WorkmanshipDimension.Overall,
                        value = new ItemQualityValueData { state = QualityValueState.Known, value = quality },
                        sourceId = lot.recipeDefinitionId,
                        provenanceId = lot.lotId,
                        tags = new[] { "production.town", "quality.low" }
                    }
                },
                dimensions = new List<ItemQualityDimensionEntryData>
                {
                    new ItemQualityDimensionEntryData
                    {
                        entryId = "quality.workmanship",
                        dimension = ItemQualityDimension.Workmanship,
                        value = new ItemQualityValueData { state = QualityValueState.Known, value = quality },
                        weight = 1f,
                        sourceId = lot.recipeDefinitionId,
                        provenanceId = lot.lotId,
                        tags = new[] { "production.town", "quality.low" }
                    }
                },
                tags = secondhand
                    ? new[] { "item.quality", "quality.production-generated", "production.prototype-town", "item.secondhand" }
                    : new[] { "item.quality", "quality.production-generated", "production.prototype-town" }
            };
        }

        private static string StablePrototypeGuid(string seed)
        {
            using MD5 algorithm = MD5.Create();
            return new Guid(algorithm.ComputeHash(Encoding.UTF8.GetBytes(seed ?? string.Empty))).ToString("D");
        }

        private static float DeterministicPrototypeQuality(string seed, float baseQuality, float variation)
        {
            float normalized = DeterministicPrototypeUnit(seed);
            float offset = (normalized * 2f - 1f) * Math.Clamp(variation, 0f, 1f);
            return Math.Clamp(baseQuality + offset, 0f, 1f);
        }

        private static float DeterministicPrototypeUnit(string seed) => DeterministicPrototypeUInt(seed) / (float)uint.MaxValue;

        private static uint DeterministicPrototypeUInt(string seed)
        {
            using SHA256 algorithm = SHA256.Create();
            byte[] hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(seed ?? string.Empty));
            return BitConverter.ToUInt32(hash, 0);
        }

        private RegionalFlowOperationResult ApplyPrototypePoolChange(string itemDefinitionId, long quantity, bool add, string sourceId, double worldTime)
        {
            string poolId = PrototypeEconomyContentIds.PoolForItem(itemDefinitionId);
            string commodityId = PrototypeEconomyContentIds.CommodityForItem(itemDefinitionId);
            return RegionalFlow.ApplyQuantityOperation(new AggregateQuantityOperationData
            {
                operationId = $"regional-market.{(add ? "add" : "consume")}.{sourceId}",
                operationKind = add ? AggregateQuantityOperationKind.Add : AggregateQuantityOperationKind.Consume,
                commodityId = commodityId,
                sourcePoolId = add ? string.Empty : poolId,
                destinationPoolId = add ? poolId : string.Empty,
                unit = CommodityUnit.Each,
                quantity = Math.Max(0L, quantity),
                purpose = add
                    ? "prototype-export-stock"
                    : PrototypeEconomyContentIds.IsTownExport(itemDefinitionId) ? "prototype-export-materialization" : "prototype-import-purchase",
                sourceEventId = sourceId,
                worldTime = worldTime,
                provenance = "prototype-market-transaction"
            }, $"regional-market.{sourceId}");
        }

        private bool TryApplyPrototypeSalesTax(string operationId, EconomyOperationResult purchase, double worldTime, out long taxUnits, out string failure)
        {
            taxUnits = 0L;
            failure = string.Empty;
            if (purchase?.Transaction == null)
            {
                failure = "The committed purchase transaction is missing.";
                return false;
            }

            string eventId = $"taxable-event.{operationId}";
            InstitutionalRevenueOperationResult step = InstitutionalRevenue.RegisterTaxableEvent(new TaxableEventData
            {
                taxableEventId = eventId,
                revenueDefinitionId = PrototypeEconomyContentIds.RevenueSalesTax,
                eligibleCategory = InstitutionalRevenueCategory.SalesTaxFoundation,
                sourceRuntime = nameof(EconomyRuntime),
                sourceRecordId = purchase.Transaction.TransactionId,
                eventCategory = TaxableEventCategory.CompletedTrade,
                assessedSubject = new RevenueSubjectReferenceData
                {
                    subjectKind = RevenueSubjectKind.Buyer,
                    role = RevenueSubjectRole.AssessedParty,
                    subjectId = ResolvePlayerPersonId(),
                    personId = ResolvePlayerPersonId(),
                    accountId = PlayerEconomyAccountId
                },
                otherSubjects = new[]
                {
                    new RevenueSubjectReferenceData
                    {
                        subjectKind = RevenueSubjectKind.Seller,
                        role = RevenueSubjectRole.EconomicBearer,
                        subjectId = PrototypeEconomyContentIds.BusinessInstance,
                        organizationId = "organization.prototype-town",
                        accountId = PrototypeEconomyContentIds.MerchantAccount
                    }
                },
                institutionId = "organization.prototype-town",
                currencyId = PrototypeEconomyContentIds.CurrencyGold,
                eventWorldTime = worldTime,
                monetaryValueUnits = purchase.Transaction.Units,
                businessId = PrototypeEconomyContentIds.BusinessInstance,
                tradeRecordId = operationId,
                transactionId = purchase.Transaction.TransactionId,
                provenance = "prototype-market-purchase"
            }, PrototypeEconomyContentIds.RevenueAuthority, $"revenue-event.{operationId}");
            if (!step.Succeeded) { failure = step.Message; return false; }

            step = InstitutionalRevenue.GenerateAssessment($"assessment.{operationId}", PrototypeEconomyContentIds.RevenueSalesTax,
                new[] { eventId }, PrototypeEconomyContentIds.RevenueAuthority, $"period.{operationId}", worldTime,
                approve: true, transactionId: $"revenue-assess.{operationId}");
            if (!step.Succeeded) { failure = step.Message; return false; }
            taxUnits = step.Obligation?.amountDueUnits ?? 0L;
            if (taxUnits <= 0L)
            {
                return true;
            }

            InstitutionalRevenueOperationResult paid = InstitutionalRevenue.PayObligation(step.Obligation.obligationId, Economy,
                $"revenue-pay.{operationId}", taxUnits, worldTime);
            if (!paid.Succeeded) { failure = paid.Message; return false; }
            return true;
        }

        private bool RecordMarketState(string subjectId, long boundary, long supply, long demand, MarketSupplySourceCategory supplyKind, MarketDemandCategory demandKind, double observed, double expires)
        {
            MarketOperationResult supplyResult = Markets.RecordSupply(new MarketObservationRecordData
            {
                observationId = $"market-supply.{subjectId}.{boundary}", marketInstanceId = PrototypeEconomyContentIds.MarketInstanceTown,
                marketSubjectId = subjectId, unit = MarketQuantityUnit.Each, quantity = supply, availableNowQuantity = supply,
                supplySourceCategory = supplyKind, sourceReferenceId = $"prototype-town.{boundary}", observedWorldTime = observed,
                expiresWorldTime = expires, provenance = "prototype-regional-economy"
            });
            MarketOperationResult demandResult = Markets.RecordDemand(new MarketObservationRecordData
            {
                observationId = $"market-demand.{subjectId}.{boundary}", marketInstanceId = PrototypeEconomyContentIds.MarketInstanceTown,
                marketSubjectId = subjectId, unit = MarketQuantityUnit.Each, quantity = demand, availableNowQuantity = demand,
                demandCategory = demandKind, sourceReferenceId = $"prototype-town.{boundary}", observedWorldTime = observed,
                expiresWorldTime = expires, provenance = "prototype-regional-economy"
            });
            MarketOperationResult priceResult = Markets.UpdateMarketSubject(PrototypeEconomyContentIds.MarketInstanceTown, subjectId, observed);
            return supplyResult.Succeeded && demandResult.Succeeded && priceResult.Succeeded;
        }

        private bool BeginPrototypeTradeAudit(
            string operationId,
            string itemDefinitionId,
            int quantity,
            long amountUnits,
            IReadOnlyList<string> quoteIds,
            TradeParticipantData seller,
            TradeParticipantData buyer,
            IReadOnlyList<string> exactItemIds,
            double worldTime,
            out string sessionId,
            out string failure)
        {
            sessionId = $"trade-session.{operationId}";
            failure = string.Empty;
            if (Trades.TryGetSession(sessionId, out _))
            {
                return true;
            }

            DefinitionRegistry registry = GetDefinitionRegistry();
            if (registry == null || !registry.TryGet(PrototypeEconomyContentIds.TradePolicyTownMerchant, out TradePolicyDefinition policy))
            {
                failure = "The prototype merchant trade policy is unavailable.";
                return false;
            }

            seller ??= new TradeParticipantData();
            buyer ??= new TradeParticipantData();
            string[] resolvedQuoteIds = (quoteIds ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToArray();
            string quoteId = resolvedQuoteIds.FirstOrDefault() ?? string.Empty;
            TradeOperationResult step = Trades.OpenSession(policy, new TradeSessionData
            {
                tradeSessionId = sessionId,
                participants = new List<TradeParticipantData> { seller, buyer },
                initiatorParticipantId = seller.participantId,
                hostMerchantId = PrototypeEconomyContentIds.BusinessInstance,
                hostOrganizationId = "organization.prototype-town",
                marketInstanceId = PrototypeEconomyContentIds.MarketInstanceTown,
                locationReferenceId = "place.prototype-town",
                state = TradeSessionState.Open,
                createdWorldTime = worldTime,
                lastActivityWorldTime = worldTime,
                provenance = "prototype-market"
            }, $"{operationId}.trade.open");
            if (!step.Succeeded)
            {
                failure = step.Message;
                return false;
            }

            List<TradeAssetEntryData> goods = new List<TradeAssetEntryData>();
            string[] exact = (exactItemIds ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToArray();
            for (int index = 0; index < exact.Length; index++)
            {
                goods.Add(new TradeAssetEntryData
                {
                    assetEntryId = $"trade-asset.{operationId}.item.{index}",
                    assetKind = TradeAssetKind.ItemInstance,
                    sourceParticipantId = seller.participantId,
                    destinationParticipantId = buyer.participantId,
                    itemInstanceId = exact[index],
                    itemDefinitionId = itemDefinitionId,
                    quantity = 1,
                    quoteId = quoteId,
                    provenance = "prototype-market-exact-item"
                });
            }

            int aggregateQuantity = Math.Max(0, quantity - exact.Length);
            if (aggregateQuantity > 0)
            {
                goods.Add(new TradeAssetEntryData
                {
                    assetEntryId = $"trade-asset.{operationId}.aggregate",
                    assetKind = TradeAssetKind.StackQuantity,
                    sourceParticipantId = seller.participantId,
                    destinationParticipantId = buyer.participantId,
                    itemInstanceId = $"aggregate:{PrototypeEconomyContentIds.PoolForItem(itemDefinitionId)}",
                    itemDefinitionId = itemDefinitionId,
                    quantity = aggregateQuantity,
                    quoteId = quoteId,
                    provenance = "prototype-market-aggregate-commodity"
                });
            }

            string offerId = $"trade-offer.{operationId}";
            TradeOfferData offer = new TradeOfferData
            {
                offerId = offerId,
                proposingParticipantId = seller.participantId,
                respondingParticipantIds = new[] { buyer.participantId },
                bundles = new List<TradeBundleData>
                {
                    new TradeBundleData
                    {
                        bundleId = $"trade-bundle.{operationId}.goods",
                        contributingParticipantId = seller.participantId,
                        receivingParticipantId = buyer.participantId,
                        assets = goods,
                        provenance = "prototype-market-goods"
                    },
                    new TradeBundleData
                    {
                        bundleId = $"trade-bundle.{operationId}.payment",
                        contributingParticipantId = buyer.participantId,
                        receivingParticipantId = seller.participantId,
                        assets = new List<TradeAssetEntryData>
                        {
                            new TradeAssetEntryData
                            {
                                assetEntryId = $"trade-asset.{operationId}.payment",
                                assetKind = TradeAssetKind.Money,
                                sourceParticipantId = buyer.participantId,
                                destinationParticipantId = seller.participantId,
                                currencyId = PrototypeEconomyContentIds.CurrencyGold,
                                units = Math.Max(1L, amountUnits),
                                sourceAccountId = buyer.accountId,
                                destinationAccountId = seller.accountId,
                                quoteId = quoteId,
                                provenance = "prototype-market-payment"
                            }
                        },
                        provenance = "prototype-market-payment"
                    }
                },
                merchantQuoteIds = resolvedQuoteIds,
                createdWorldTime = worldTime,
                sourceRuntimeRevision = Markets.Revision,
                provenance = "prototype-market"
            };
            step = Trades.SubmitOffer(sessionId, offer, $"{operationId}.trade.offer");
            if (!step.Succeeded)
            {
                failure = step.Message;
                return false;
            }

            step = Trades.AcceptOffer(sessionId, offerId, buyer.participantId, worldTime, $"{operationId}.trade.accept");
            if (!step.Succeeded)
            {
                failure = step.Message;
                return false;
            }

            return true;
        }

        private bool CompletePrototypeTradeAudit(
            string operationId,
            string sessionId,
            EconomyOperationResult payment,
            IEnumerable<string> transferReferences,
            double worldTime,
            bool merchantRevenue,
            string itemDefinitionId,
            IEnumerable<string> exactItemIds,
            out string tradeRecordId,
            out string failure)
        {
            tradeRecordId = string.Empty;
            failure = string.Empty;
            if (payment?.Transaction == null)
            {
                failure = "The authoritative payment transaction is missing.";
                return false;
            }

            TradeOperationResult settled = Trades.RecordExternallySettledDeal(sessionId,
                new[] { payment.Transaction.TransactionId }, transferReferences?.ToArray() ?? Array.Empty<string>(), worldTime,
                $"{operationId}.trade.settle");
            if (!settled.Succeeded || settled.TradeRecord == null)
            {
                failure = settled.Message;
                return false;
            }

            tradeRecordId = settled.TradeRecord.tradeRecordId;
            string[] itemReferences = (exactItemIds ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)).ToArray();
            if (itemReferences.Length == 0 && !string.IsNullOrWhiteSpace(itemDefinitionId))
            {
                itemReferences = new[] { itemDefinitionId };
            }

            BusinessOperationResult accounting = merchantRevenue
                ? Businesses.RecordRevenue(new BusinessRevenueRecordData
                {
                    revenueRecordId = $"business-revenue.{operationId}",
                    businessId = PrototypeEconomyContentIds.BusinessInstance,
                    establishmentId = PrototypeEconomyContentIds.EstablishmentInstance,
                    category = BusinessRevenueCategory.RetailSale,
                    amount = new BusinessMoneyData { currencyId = payment.Transaction.CurrencyId, units = payment.Transaction.Units },
                    transactionId = payment.Transaction.TransactionId,
                    tradeRecordId = tradeRecordId,
                    soldItemOrServiceIds = itemReferences,
                    marketOrQuoteReferenceId = settled.TradeRecord.quoteIds.FirstOrDefault() ?? string.Empty,
                    recognitionWorldTime = worldTime,
                    provenance = "prototype-market-trade"
                }, Economy, Trades)
                : Businesses.RecordExpense(new BusinessExpenseRecordData
                {
                    expenseRecordId = $"business-expense.{operationId}",
                    businessId = PrototypeEconomyContentIds.BusinessInstance,
                    establishmentId = PrototypeEconomyContentIds.EstablishmentInstance,
                    category = BusinessExpenseCategory.InventoryPurchase,
                    amount = new BusinessMoneyData { currencyId = payment.Transaction.CurrencyId, units = payment.Transaction.Units },
                    transactionId = payment.Transaction.TransactionId,
                    purchasedItemOrServiceIds = itemReferences,
                    recognitionWorldTime = worldTime,
                    provenance = "prototype-market-trade"
                }, Economy);
            if (!accounting.Succeeded)
            {
                failure = accounting.Message;
                return false;
            }

            return true;
        }

        private TradeParticipantData PrototypePlayerTradeParticipant(TradeParticipantRole role)
        {
            string personId = ResolvePlayerPersonId();
            return new TradeParticipantData
            {
                participantId = $"trade-participant.player.{personId}",
                kind = TradeParticipantKind.Person,
                role = role,
                subjectId = personId,
                representedOwnerId = personId,
                sourceInventoryId = personId,
                receivingInventoryId = personId,
                accountId = PlayerEconomyAccountId
            };
        }

        private static TradeParticipantData PrototypeMerchantTradeParticipant(TradeParticipantRole role)
        {
            return new TradeParticipantData
            {
                participantId = "trade-participant.prototype-town-workshop",
                kind = TradeParticipantKind.Organization,
                role = role,
                subjectId = "organization.prototype-town",
                representedOwnerId = PrototypeEconomyContentIds.BusinessInstance,
                sourceInventoryId = "inventory.prototype-town.finished-goods",
                receivingInventoryId = "inventory.prototype-town.finished-goods",
                accountId = PrototypeEconomyContentIds.MerchantAccount
            };
        }

        private static TradeParticipantData PrototypeExternalTradeParticipant(TradeParticipantRole role)
        {
            return new TradeParticipantData
            {
                participantId = "trade-participant.prototype-external-market",
                kind = TradeParticipantKind.Organization,
                role = role,
                subjectId = "organization.prototype-external-market",
                representedOwnerId = "organization.prototype-external-market",
                sourceInventoryId = "inventory.prototype-external-market",
                receivingInventoryId = "inventory.prototype-external-market",
                accountId = PrototypeEconomyContentIds.ExternalTradeAccount
            };
        }

        private MarketOperationResult CreatePrototypeQuote(string itemDefinitionId, MerchantQuoteDirection direction, int quantity, double worldTime,
            ItemInstanceSnapshot item = null, ItemQualitySnapshot quality = null, ItemDurabilitySnapshot durability = null)
        {
            string subjectId = PrototypeEconomyContentIds.SubjectForItem(itemDefinitionId);
            return Markets.CreateMerchantQuote($"market-quote.{Guid.NewGuid():N}", PrototypeEconomyContentIds.BusinessInstance,
                PrototypeEconomyContentIds.MarketInstanceTown, subjectId, direction, quantity, worldTime, worldTime + 30d,
                itemInstanceId: item?.ItemInstanceId, item: item, quality: quality, durability: durability);
        }

        private void AddListing(List<PrototypeMarketListing> listings, string itemId, bool buyable, bool sellable)
        {
            DefinitionRegistry registry = GetDefinitionRegistry();
            if (!registry.TryGet(itemId, out ItemDefinition item))
            {
                return;
            }

            string subjectId = PrototypeEconomyContentIds.SubjectForItem(itemId);
            Markets.TryGetCurrentPrice(PrototypeEconomyContentIds.MarketInstanceTown, subjectId, out MarketPriceRecordData price);
            long aggregateStock = PoolSupply(PrototypeEconomyContentIds.PoolForItem(itemId));
            int exactSecondhandStock = PrototypeEconomyContentIds.IsTownExport(itemId) ? GetExactPrototypeExportStock(itemId).Count : 0;
            listings.Add(new PrototypeMarketListing(item.Id, item.DisplayName, subjectId, price?.referenceAmountUnits ?? 0L,
                aggregateStock, exactSecondhandStock, buyable, sellable,
                PrototypeEconomyContentIds.IsTownImport(itemId) ? "Imported raw resource" : "Town-made and exact secondhand stock"));
        }

        private PrototypeTradeRollback CapturePrototypeTradeRollback()
        {
            return new PrototypeTradeRollback
            {
                Economy = Economy.CreateSaveData(),
                Identities = ItemIdentities.CreateSaveData(),
                Compositions = ItemCompositions.CreateSaveData(),
                Quality = ItemQualityAffixes.CreateSaveData(),
                Durability = ItemDurability.CreateSaveData(),
                Inventory = playerInventory?.CreateSaveData(),
                Businesses = Businesses.CreateSaveData(),
                Trades = Trades.CreateSaveData(),
                RegionalFlow = RegionalFlow.CreateSaveData(),
                Production = ProductionWorkflow.CreateSaveData(),
                Revenue = InstitutionalRevenue.CreateSaveData()
            };
        }

        private PrototypeBackgroundTradeRollback CapturePrototypeBackgroundTradeRollback()
        {
            return new PrototypeBackgroundTradeRollback
            {
                Economy = Economy.CreateSaveData(),
                Businesses = Businesses.CreateSaveData(),
                Trades = Trades.CreateSaveData(),
                RegionalFlow = RegionalFlow.CreateSaveData(),
                Production = ProductionWorkflow.CreateSaveData()
            };
        }

        private void RestorePrototypeBackgroundTrade(PrototypeBackgroundTradeRollback rollback)
        {
            if (rollback == null)
            {
                return;
            }

            DefinitionRegistry registry = GetDefinitionRegistry();
            Economy.RestoreFromSaveData(rollback.Economy, registry);
            Businesses.RestoreFromSaveData(rollback.Businesses, registry);
            Trades.RestoreFromSaveData(rollback.Trades, registry);
            RegionalFlow.RestoreFromSaveData(rollback.RegionalFlow, registry);
            ProductionWorkflow.RestoreFromSaveData(rollback.Production, registry);
        }

        private void RestorePrototypeTrade(PrototypeTradeRollback rollback)
        {
            if (rollback == null)
            {
                return;
            }

            DefinitionRegistry registry = GetDefinitionRegistry();
            Economy.RestoreFromSaveData(rollback.Economy, registry);
            ItemIdentities.RestoreFromSaveData(rollback.Identities, registry);
            ItemCompositions.RestoreFromSaveData(rollback.Compositions, registry, ItemIdentities);
            ItemQualityAffixes.RestoreFromSaveData(rollback.Quality, registry, ItemIdentities);
            ItemDurability.RestoreFromSaveData(rollback.Durability, registry, ItemIdentities, ItemCompositions);
            if (rollback.Inventory != null) playerInventory?.TryRestoreFromSaveData(rollback.Inventory, registry);
            Businesses.RestoreFromSaveData(rollback.Businesses, registry);
            Trades.RestoreFromSaveData(rollback.Trades, registry);
            RegionalFlow.RestoreFromSaveData(rollback.RegionalFlow, registry);
            ProductionWorkflow.RestoreFromSaveData(rollback.Production, registry);
            InstitutionalRevenue.RestoreFromSaveData(rollback.Revenue, registry);
        }

        private sealed class PrototypeTradeRollback
        {
            public EconomyRuntimeSaveData Economy;
            public ItemInstanceRuntimeSaveData Identities;
            public ItemCompositionRuntimeSaveData Compositions;
            public ItemQualityAffixRuntimeSaveData Quality;
            public ItemDurabilityRuntimeSaveData Durability;
            public InventorySaveData Inventory;
            public BusinessRuntimeSaveData Businesses;
            public TradeRuntimeSaveData Trades;
            public RegionalFlowRuntimeSaveData RegionalFlow;
            public ProductionWorkflowRuntimeSaveData Production;
            public InstitutionalRevenueRuntimeSaveData Revenue;
        }

        private sealed class PrototypeBackgroundTradeRollback
        {
            public EconomyRuntimeSaveData Economy;
            public BusinessRuntimeSaveData Businesses;
            public TradeRuntimeSaveData Trades;
            public RegionalFlowRuntimeSaveData RegionalFlow;
            public ProductionWorkflowRuntimeSaveData Production;
        }
    }

    public sealed class PrototypeMarketListing
    {
        public PrototypeMarketListing(string itemDefinitionId, string displayName, string marketSubjectId, long referencePrice, long aggregateStock, long exactSecondhandStock, bool buyable, bool sellable, string role)
        {
            ItemDefinitionId = itemDefinitionId; DisplayName = displayName; MarketSubjectId = marketSubjectId;
            ReferencePrice = referencePrice;
            AggregateStock = Math.Max(0L, aggregateStock);
            ExactSecondhandStock = Math.Max(0L, exactSecondhandStock);
            AvailableStock = checked(AggregateStock + ExactSecondhandStock);
            Buyable = buyable; Sellable = sellable; EconomicRole = role;
        }
        public string ItemDefinitionId { get; }
        public string DisplayName { get; }
        public string MarketSubjectId { get; }
        public long ReferencePrice { get; }
        public long AvailableStock { get; }
        public long AggregateStock { get; }
        public long ExactSecondhandStock { get; }
        public bool Buyable { get; }
        public bool Sellable { get; }
        public string EconomicRole { get; }
    }

    public sealed class PrototypeExportChoice
    {
        public PrototypeExportChoice(string itemInstanceId, string itemDefinitionId, string displayName)
        { ItemInstanceId = itemInstanceId; ItemDefinitionId = itemDefinitionId; DisplayName = displayName; }
        public string ItemInstanceId { get; }
        public string ItemDefinitionId { get; }
        public string DisplayName { get; }
    }

    public sealed class PrototypeEconomyOperation
    {
        private PrototypeEconomyOperation(bool succeeded, string message, long amount, IReadOnlyList<string> itemInstanceIds)
        {
            Succeeded = succeeded;
            Message = message;
            Amount = amount;
            ItemInstanceIds = (itemInstanceIds ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)).ToArray();
        }
        public bool Succeeded { get; }
        public string Message { get; }
        public long Amount { get; }
        public IReadOnlyList<string> ItemInstanceIds { get; }
        public static PrototypeEconomyOperation Success(string message, long amount, IReadOnlyList<string> itemInstanceIds = null) => new PrototypeEconomyOperation(true, message, amount, itemInstanceIds);
        public static PrototypeEconomyOperation Failure(string message) => new PrototypeEconomyOperation(false, message, 0L, Array.Empty<string>());
    }
}
