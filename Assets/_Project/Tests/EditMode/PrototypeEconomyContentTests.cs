using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityIsekaiGame.Economy;
using UnityIsekaiGame.Economy.Businesses;
using UnityIsekaiGame.Economy.InstitutionalRevenue;
using UnityIsekaiGame.Economy.Integration;
using UnityIsekaiGame.Economy.Markets;
using UnityIsekaiGame.Economy.Payroll;
using UnityIsekaiGame.Economy.RegionalFlow;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Inventory.Recipes;
using UnityIsekaiGame.Progression;

namespace UnityIsekaiGame.Tests
{
    public sealed class PrototypeEconomyContentTests
    {
        private const string CatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";
        private const string ScenePath = "Assets/_Project/Scenes/Prototype/PrototypeScene.unity";

        [Test]
        public void ProductionCatalogContainsEveryRequiredEconomyDefinition()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            DefinitionRegistry registry = new DefinitionRegistry(catalog.GetDefinitions());
            EconomyIntegrationFacade facade = new EconomyIntegrationFacade(registry);

            EconomicValidationResult validation = facade.ValidateRequiredDefinitions(PrototypeEconomyContentIds.RequiredDefinitionIds);

            Assert.That(validation.Succeeded, Is.True, validation.Summary);
            Assert.That(PrototypeEconomyContentIds.GetDefinitions(registry).Count, Is.EqualTo(PrototypeEconomyContentIds.RequiredDefinitionIds.Length));
        }

        [Test]
        public void PrototypeTownEconomyUsesRawImportsAndWeaponExports()
        {
            DefinitionRegistry registry = Registry();
            Assert.That(registry.TryGet(PrototypeEconomyContentIds.SubjectIronOre, out MarketSubjectDefinition iron), Is.True);
            Assert.That(registry.TryGet(PrototypeEconomyContentIds.SubjectWoodLog, out MarketSubjectDefinition wood), Is.True);
            Assert.That(registry.TryGet(PrototypeEconomyContentIds.SubjectSword, out MarketSubjectDefinition sword), Is.True);
            Assert.That(registry.TryGet(PrototypeEconomyContentIds.SubjectBow, out MarketSubjectDefinition bow), Is.True);
            Assert.That(iron.BaselinePriceUnits, Is.EqualTo(4L));
            Assert.That(wood.BaselinePriceUnits, Is.EqualTo(3L));
            Assert.That(sword.BaselinePriceUnits, Is.EqualTo(30L));
            Assert.That(bow.BaselinePriceUnits, Is.EqualTo(24L));
            Assert.That(PrototypeEconomyContentIds.IsTownImport(PrototypeEconomyContentIds.ItemIronOre), Is.True);
            Assert.That(PrototypeEconomyContentIds.IsTownImport(PrototypeEconomyContentIds.ItemWoodLog), Is.True);
            Assert.That(PrototypeEconomyContentIds.IsTownExport(PrototypeEconomyContentIds.ItemSword), Is.True);
            Assert.That(PrototypeEconomyContentIds.IsTownExport(PrototypeEconomyContentIds.ItemBow), Is.True);

            Assert.That(registry.TryGet(PrototypeEconomyContentIds.ProductionSword, out AggregateProductionProfileDefinition swordProduction), Is.True);
            Assert.That(swordProduction.Inputs.Single(input => input.CommodityId == PrototypeEconomyContentIds.CommodityIronOre).Quantity, Is.EqualTo(3L));
            Assert.That(swordProduction.Inputs.Single(input => input.CommodityId == PrototypeEconomyContentIds.CommodityLeatherStrip).Quantity, Is.EqualTo(1L));
            Assert.That(swordProduction.Outputs.Single().CommodityId, Is.EqualTo(PrototypeEconomyContentIds.CommoditySword));
            Assert.That(registry.TryGet(PrototypeEconomyContentIds.ProductionBow, out AggregateProductionProfileDefinition bowProduction), Is.True);
            Assert.That(bowProduction.Inputs.Single().CommodityId, Is.EqualTo(PrototypeEconomyContentIds.CommodityWoodLog));
            Assert.That(bowProduction.Inputs.Single().Quantity, Is.EqualTo(4L));
            Assert.That(bowProduction.Outputs.Single().CommodityId, Is.EqualTo(PrototypeEconomyContentIds.CommodityBow));
        }

        [Test]
        public void TownProductionTotalsMatchPlayerWeaponRecipes()
        {
            DefinitionRegistry registry = Registry();
            Assert.That(registry.TryGet("recipe.prototype-sword", out RecipeDefinition playerSword), Is.True);
            Assert.That(registry.TryGet("recipe.prototype-bow", out RecipeDefinition playerBow), Is.True);
            Assert.That(registry.TryGet(PrototypeEconomyContentIds.ProductionSword, out AggregateProductionProfileDefinition townSword), Is.True);
            Assert.That(registry.TryGet(PrototypeEconomyContentIds.ProductionBow, out AggregateProductionProfileDefinition townBow), Is.True);

            float playerSwordMetal = playerSword.Inputs.Where(input => input.materialTagIds.Contains("material.metal")).Sum(input => input.quantity);
            float playerSwordLeather = playerSword.Inputs.Where(input => input.materialTagIds.Contains("material.leather")).Sum(input => input.quantity);
            float playerBowWood = playerBow.Inputs.Where(input => input.materialTagIds.Contains("material.wood")).Sum(input => input.quantity);

            Assert.That(townSword.Inputs.Single(input => input.CommodityId == PrototypeEconomyContentIds.CommodityIronOre).Quantity, Is.EqualTo((long)playerSwordMetal));
            Assert.That(townSword.Inputs.Single(input => input.CommodityId == PrototypeEconomyContentIds.CommodityLeatherStrip).Quantity, Is.EqualTo((long)playerSwordLeather));
            Assert.That(townBow.Inputs.Single(input => input.CommodityId == PrototypeEconomyContentIds.CommodityWoodLog).Quantity, Is.EqualTo((long)playerBowWood));
        }

        [Test]
        public void PrototypeBusinessPayrollPropertyAndTaxPoliciesAreAuthored()
        {
            DefinitionRegistry registry = Registry();
            Assert.That(registry.TryGet(PrototypeEconomyContentIds.BusinessWorkshop, out BusinessDefinition business), Is.True);
            Assert.That(business.Category, Is.EqualTo(BusinessCategory.Workshop));
            Assert.That(business.SecondhandStockPolicy.enabled, Is.True);
            Assert.That(business.SecondhandStockPolicy.minimumStockBasisPoints, Is.EqualTo(1500));
            Assert.That(business.SecondhandStockPolicy.maximumStockBasisPoints, Is.EqualTo(5000));
            Assert.That(business.SecondhandStockPolicy.minimumConditionBasisPoints, Is.EqualTo(4500));
            Assert.That(business.SecondhandStockPolicy.maximumConditionBasisPoints, Is.EqualTo(9000));
            Assert.That(business.SimulationPolicy.marketIntervalSeconds, Is.EqualTo(60));
            Assert.That(business.SimulationPolicy.maximumCatchUpIntervalsPerFrame, Is.EqualTo(8));
            Assert.That(business.SimulationPolicy.rawResourceTarget, Is.EqualTo(120L));
            Assert.That(business.SimulationPolicy.exportStockTarget, Is.EqualTo(12L));
            Assert.That(business.SimulationPolicy.exportReserve, Is.EqualTo(4L));
            Assert.That(business.SimulationPolicy.minimumProductionMarginBasisPoints, Is.EqualTo(1000));
            Assert.That(business.SimulationPolicy.retainedMarketIntervals, Is.EqualTo(10080));
            Assert.That(registry.TryGet(PrototypeEconomyContentIds.CompensationTownCraft, out CompensationDefinition compensation), Is.True);
            Assert.That(compensation.RateBasis, Is.EqualTo(CompensationRateBasis.PerOutputQuantity));
            Assert.That(compensation.RateUnits, Is.EqualTo(8L));
            Assert.That(registry.TryGet(PrototypeEconomyContentIds.RevenueSalesTax, out InstitutionalRevenueDefinition tax), Is.True);
            Assert.That(tax.Category, Is.EqualTo(InstitutionalRevenueCategory.SalesTaxFoundation));
        }

        [Test]
        public void MerchantSecondhandRetailShareAccountsForAggregateAndReservedExactStock()
        {
            MerchantSecondhandStockPolicyData policy = new MerchantSecondhandStockPolicyData
            {
                minimumStockBasisPoints = 1500,
                maximumStockBasisPoints = 5000,
                minimumEligibleStockQuantity = 2
            };

            int fromAggregate = MerchantSecondhandStockCalculator.CalculateRetailTargetCount(12L, 0, policy, 0u);
            int withLargeReserve = MerchantSecondhandStockCalculator.CalculateRetailTargetCount(12L, 100, policy, 3500u);
            int replay = MerchantSecondhandStockCalculator.CalculateRetailTargetCount(12L, 100, policy, 3500u);

            Assert.That(MerchantSecondhandStockCalculator.RetailShareBasisPoints(12L, 0, fromAggregate), Is.InRange(1500, 5000));
            Assert.That(MerchantSecondhandStockCalculator.RetailShareBasisPoints(12L, 100, withLargeReserve), Is.InRange(1500, 5000));
            Assert.That(replay, Is.EqualTo(withLargeReserve));
        }

        [Test]
        public void MerchantSecondhandStockTargetsAreDeterministicAndStayWithinPolicyRange()
        {
            MerchantSecondhandStockPolicyData policy = new MerchantSecondhandStockPolicyData
            {
                minimumStockBasisPoints = 1500,
                maximumStockBasisPoints = 5000,
                minimumEligibleStockQuantity = 2
            };

            int lowRoll = MerchantSecondhandStockCalculator.CalculateTargetCount(20L, policy, 0u);
            int highRoll = MerchantSecondhandStockCalculator.CalculateTargetCount(20L, policy, 3500u);
            int replay = MerchantSecondhandStockCalculator.CalculateTargetCount(20L, policy, 3500u);

            Assert.That(lowRoll, Is.EqualTo(3));
            Assert.That(highRoll, Is.EqualTo(10));
            Assert.That(replay, Is.EqualTo(highRoll));
            Assert.That(MerchantSecondhandStockCalculator.CalculateTargetCount(1L, policy, 1000u), Is.Zero);
        }

        [Test]
        public void ConservationAuditExcludesExplicitIssuanceButChecksTransfers()
        {
            CurrencyDefinition gold = ScriptableObject.CreateInstance<CurrencyDefinition>();
            gold.Initialize("currency.test-gold", "Test Gold", "G");
            DefinitionRegistry registry = new DefinitionRegistry(new IGameDefinition[] { gold });
            EconomyRuntime economy = new EconomyRuntime();
            economy.Configure(registry, "world.test");
            economy.CreateAccount("account.test.a", gold, "person.a", EconomyAccountKind.PersonWallet, 50L, "open.a");
            economy.CreateAccount("account.test.b", gold, "person.b", EconomyAccountKind.PersonWallet, 0L, "open.b");
            EconomyOperationResult transfer = economy.Transfer("transfer.test", "account.test.a", "account.test.b", new MoneyAmount(gold.Id, 12L));
            EconomyIntegrationFacade facade = new EconomyIntegrationFacade(registry, economy: economy);

            EconomicConservationAuditResult audit = facade.AuditExactArithmeticAndConservation();

            Assert.That(transfer.Succeeded, Is.True, transfer.Message);
            Assert.That(audit.succeeded, Is.True, audit.message);
            Assert.That(audit.monetaryLedgerNet, Is.Zero);
        }

        [Test]
        public void PrototypeSceneContainsReachableMarketStallCollider()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            PrototypeMarketStall stall = UnityEngine.Object.FindAnyObjectByType<PrototypeMarketStall>(FindObjectsInactive.Include);

            Assert.That(stall, Is.Not.Null);
            Assert.That(stall.GetComponent<Collider>(), Is.Not.Null);
            Assert.That(stall.gameObject.activeInHierarchy, Is.True);
            Assert.That(stall.transform.localScale.x, Is.GreaterThanOrEqualTo(1f));
            Assert.That(stall.transform.localScale.y, Is.GreaterThanOrEqualTo(1f));
        }

        [Test]
        public void MarketChangeCalculatorIsDeterministicAndNeverOversellsStock()
        {
            PrototypeMarketChangePlan first = PrototypeMarketChangeCalculator.Calculate(37L, 83L, 91L, 31L, 7L, 6L);
            PrototypeMarketChangePlan replay = PrototypeMarketChangeCalculator.Calculate(37L, 83L, 91L, 31L, 7L, 6L);

            Assert.That(replay.IronImports, Is.EqualTo(first.IronImports));
            Assert.That(replay.WoodImports, Is.EqualTo(first.WoodImports));
            Assert.That(replay.LeatherImports, Is.EqualTo(first.LeatherImports));
            Assert.That(replay.SwordExports, Is.EqualTo(first.SwordExports));
            Assert.That(replay.BowExports, Is.EqualTo(first.BowExports));
            Assert.That(first.IronImports, Is.InRange(0L, 7L));
            Assert.That(first.WoodImports, Is.InRange(0L, 5L));
            Assert.That(first.LeatherImports, Is.InRange(0L, 2L));
            Assert.That(first.SwordExports, Is.InRange(0L, 3L));
            Assert.That(first.BowExports, Is.InRange(0L, 2L));
            Assert.That(first.SwordExports, Is.LessThanOrEqualTo(7L - PrototypeMarketChangeCalculator.ExportReserve));
            Assert.That(first.BowExports, Is.LessThanOrEqualTo(6L - PrototypeMarketChangeCalculator.ExportReserve));
        }

        [Test]
        public void MarketChangeCalculatorKeepsAFullDayOfAggregateTradeBounded()
        {
            long iron = 120L;
            long wood = 120L;
            long leather = 40L;
            long swords = 12L;
            long bows = 12L;
            long imported = 0L;
            long exported = 0L;

            for (long boundary = 1L; boundary <= 1440L; boundary++)
            {
                if (iron >= 3L && leather >= 1L) { iron -= 3L; leather -= 1L; swords += 1L; }
                if (wood >= 4L) { wood -= 4L; bows += 1L; }
                PrototypeMarketChangePlan plan = PrototypeMarketChangeCalculator.Calculate(boundary, iron, wood, leather, swords, bows);
                iron += plan.IronImports;
                wood += plan.WoodImports;
                leather += plan.LeatherImports;
                swords -= plan.SwordExports;
                bows -= plan.BowExports;
                imported += plan.IronImports + plan.WoodImports + plan.LeatherImports;
                exported += plan.SwordExports + plan.BowExports;

                Assert.That(iron, Is.InRange(0L, PrototypeMarketChangeCalculator.RawResourceTarget));
                Assert.That(wood, Is.InRange(0L, PrototypeMarketChangeCalculator.RawResourceTarget));
                Assert.That(leather, Is.InRange(0L, PrototypeMarketChangeCalculator.LeatherTarget));
                Assert.That(swords, Is.GreaterThanOrEqualTo(PrototypeMarketChangeCalculator.ExportReserve));
                Assert.That(bows, Is.GreaterThanOrEqualTo(PrototypeMarketChangeCalculator.ExportReserve));
                Assert.That(plan.IronDemand, Is.InRange(10L, 80L));
                Assert.That(plan.WoodDemand, Is.InRange(10L, 80L));
                Assert.That(plan.LeatherDemand, Is.InRange(10L, 80L));
                Assert.That(plan.SwordDemand, Is.InRange(10L, 80L));
                Assert.That(plan.BowDemand, Is.InRange(10L, 80L));
            }

            Assert.That(imported, Is.GreaterThan(0L));
            Assert.That(exported, Is.GreaterThan(0L));
            Assert.That(swords, Is.LessThan(50L), "Sword production should not run away from aggregate export demand.");
            Assert.That(bows, Is.LessThan(50L), "Bow production should not run away from aggregate export demand.");
        }

        private static DefinitionRegistry Registry()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            return new DefinitionRegistry(catalog.GetDefinitions());
        }
    }
}
