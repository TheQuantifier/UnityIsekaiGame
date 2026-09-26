using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Economy
{
    public static class PrototypeEconomyContentIds
    {
        public const string CurrencyGold = "currency.gold";
        public const string MarketTown = "market.prototype-town";
        public const string MarketInstanceTown = "market-instance.prototype-town";
        public const string TradePolicyTownMerchant = "trade-policy.prototype-town-merchant";
        public const string CompensationTownCraft = "compensation.prototype-town-craft";
        public const string BusinessWorkshop = "business.prototype-town-workshop";
        public const string PropertyWorkshop = "property.prototype-town-workshop";
        public const string ContractEmployment = "contract-finance.prototype-town-employment";
        public const string ContractRental = "contract-finance.prototype-town-rental";
        public const string ContractLoan = "contract-finance.prototype-town-loan";
        public const string RevenueSalesTax = "revenue.prototype-town-sales-tax";
        public const string RegionTown = "economic-region.prototype-town";
        public const string RegionInstanceTown = "region.prototype-town";
        public const string CraftLaborCohort = "cohort.prototype-town-craftspeople";

        public const string ItemIronOre = "item.prototype-iron-ore";
        public const string ItemWoodLog = "item.wood-log";
        public const string ItemLeatherStrip = "item.leather-strip";
        public const string ItemSword = "item.prototype-sword";
        public const string ItemBow = "item.prototype-bow";

        public const string SubjectIronOre = "market-subject.prototype-town.iron-ore";
        public const string SubjectWoodLog = "market-subject.prototype-town.wood-log";
        public const string SubjectLeatherStrip = "market-subject.prototype-town.leather-strip";
        public const string SubjectSword = "market-subject.prototype-town.iron-sword";
        public const string SubjectBow = "market-subject.prototype-town.wood-bow";

        public const string CommodityIronOre = "commodity.prototype-town.iron-ore";
        public const string CommodityWoodLog = "commodity.prototype-town.wood-log";
        public const string CommodityLeatherStrip = "commodity.prototype-town.leather-strip";
        public const string CommoditySword = "commodity.prototype-town.iron-sword";
        public const string CommodityBow = "commodity.prototype-town.wood-bow";
        public const string ProductionSword = "production-profile.prototype-town.iron-sword";
        public const string ProductionBow = "production-profile.prototype-town.wood-bow";
        public const string ConsumptionInputs = "consumption-profile.prototype-town.raw-inputs";
        public const string RecipeSword = "recipe.prototype-sword";
        public const string RecipeSwordVersion = "recipe-version.prototype-sword.v1";
        public const string RecipeBow = "recipe.prototype-bow";
        public const string RecipeBowVersion = "recipe-version.prototype-bow.v1";

        public const string PlayerAccountPrefix = "economy-account.person.";
        public const string MerchantAccount = "economy-account.business.prototype-town-workshop";
        public const string ExternalTradeAccount = "economy-account.region.prototype-town-external-trade";
        public const string TreasuryAccount = "economy-account.organization.prototype.government";
        public const string RevenueAuthority = "revenue-authority.prototype-town";
        public const string BusinessInstance = "business-instance.prototype-town-workshop";
        public const string EstablishmentInstance = "business-establishment.prototype-town-workshop";
        public const string PropertyInstance = "property-instance.prototype-town-workshop";
        public const string IronImportPool = "commodity-pool.prototype-town.iron-import";
        public const string WoodImportPool = "commodity-pool.prototype-town.wood-import";
        public const string LeatherInputPool = "commodity-pool.prototype-town.leather-input";
        public const string SwordExportPool = "commodity-pool.prototype-town.sword-export";
        public const string BowExportPool = "commodity-pool.prototype-town.bow-export";

        public static string PoolForItem(string itemDefinitionId) => itemDefinitionId switch
        {
            ItemIronOre => IronImportPool,
            ItemWoodLog => WoodImportPool,
            ItemLeatherStrip => LeatherInputPool,
            ItemSword => SwordExportPool,
            ItemBow => BowExportPool,
            _ => string.Empty
        };

        public static string CommodityForItem(string itemDefinitionId) => itemDefinitionId switch
        {
            ItemIronOre => CommodityIronOre,
            ItemWoodLog => CommodityWoodLog,
            ItemLeatherStrip => CommodityLeatherStrip,
            ItemSword => CommoditySword,
            ItemBow => CommodityBow,
            _ => string.Empty
        };

        public static readonly string[] RequiredDefinitionIds =
        {
            CurrencyGold, MarketTown, TradePolicyTownMerchant, CompensationTownCraft, BusinessWorkshop,
            PropertyWorkshop, ContractEmployment, ContractRental, ContractLoan, RevenueSalesTax, RegionTown,
            SubjectIronOre, SubjectWoodLog, SubjectLeatherStrip, SubjectSword, SubjectBow,
            CommodityIronOre, CommodityWoodLog, CommodityLeatherStrip, CommoditySword, CommodityBow,
            ProductionSword, ProductionBow, ConsumptionInputs
        };

        public static string PlayerAccount(string personId) => PlayerAccountPrefix + Normalize(personId);

        public static string SubjectForItem(string itemDefinitionId) => itemDefinitionId switch
        {
            ItemIronOre => SubjectIronOre,
            ItemWoodLog => SubjectWoodLog,
            ItemLeatherStrip => SubjectLeatherStrip,
            ItemSword => SubjectSword,
            ItemBow => SubjectBow,
            _ => string.Empty
        };

        public static bool IsTownImport(string itemDefinitionId) => itemDefinitionId is ItemIronOre or ItemWoodLog;
        public static bool IsTownExport(string itemDefinitionId) => itemDefinitionId is ItemSword or ItemBow;

        public static IReadOnlyList<IGameDefinition> GetDefinitions(DefinitionRegistry registry)
        {
            if (registry == null)
            {
                return Array.Empty<IGameDefinition>();
            }

            return RequiredDefinitionIds
                .Select(id => registry.DefinitionsById.TryGetValue(id, out IGameDefinition definition) ? definition : null)
                .Where(definition => definition != null)
                .ToArray();
        }

        private static string Normalize(string value) => string.IsNullOrWhiteSpace(value)
            ? "unknown"
            : new string(value.Trim().ToLowerInvariant().Select(character => char.IsLetterOrDigit(character) || character is '.' or '-' ? character : '-').ToArray());
    }
}
