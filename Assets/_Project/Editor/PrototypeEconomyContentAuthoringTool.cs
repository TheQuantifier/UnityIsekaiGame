using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
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
using UnityIsekaiGame.Progression;

namespace UnityIsekaiGame.Editor
{
    public static class PrototypeEconomyContentAuthoringTool
    {
        private const string Root = "Assets/_Project/Content/Economy";
        private const string CatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";
        private const string ScenePath = "Assets/_Project/Scenes/Prototype/PrototypeScene.unity";

        [MenuItem("Tools/Unity Isekai Game/Author Group 8 Economy Content")]
        public static void Generate()
        {
            CurrencyDefinition gold = Load<CurrencyDefinition>("Assets/_Project/Content/Core/Currencies/GoldCurrency.asset");
            ItemDefinition ironOre = Load<ItemDefinition>("Assets/_Project/Prototype/Content/Items/PrototypeIronOre.asset");
            ItemDefinition woodLog = Load<ItemDefinition>("Assets/_Project/Prototype/Content/Items/WoodLog.asset");
            ItemDefinition leatherStrip = Load<ItemDefinition>("Assets/_Project/Prototype/Content/Items/LeatherStrip.asset");
            ItemDefinition sword = Load<ItemDefinition>("Assets/_Project/Prototype/Content/Items/PrototypeSword.asset");
            ItemDefinition bow = Load<ItemDefinition>("Assets/_Project/Prototype/Content/Items/PrototypeBow.asset");

            MarketDefinition market = GetOrCreate<MarketDefinitionAsset>($"{Root}/Markets/PrototypeTownMarket.asset");
            market.Initialize(PrototypeEconomyContentIds.MarketTown, "Prototype Town Market", gold, MarketCategory.LocalSettlement, MarketScopeType.Settlement);
            Save(market);

            MarketSubjectDefinition ironSubject = Subject("Iron Ore", PrototypeEconomyContentIds.SubjectIronOre, ironOre, gold, 4L);
            MarketSubjectDefinition woodSubject = Subject("Wood Log", PrototypeEconomyContentIds.SubjectWoodLog, woodLog, gold, 3L);
            MarketSubjectDefinition leatherSubject = Subject("Leather Strip", PrototypeEconomyContentIds.SubjectLeatherStrip, leatherStrip, gold, 2L);
            MarketSubjectDefinition swordSubject = Subject("Low-Quality Iron Sword", PrototypeEconomyContentIds.SubjectSword, sword, gold, 30L);
            MarketSubjectDefinition bowSubject = Subject("Low-Quality Wooden Bow", PrototypeEconomyContentIds.SubjectBow, bow, gold, 24L);

            TradePolicyDefinition trade = GetOrCreate<TradePolicyDefinition>($"{Root}/Trade/PrototypeTownMerchantTrade.asset");
            trade.Initialize(PrototypeEconomyContentIds.TradePolicyTownMerchant, "Prototype Town Merchant Trade", TradePolicyCategory.FixedPriceRetail,
                new[] { TradeAssetKind.ItemInstance, TradeAssetKind.StackQuantity, TradeAssetKind.Money });
            Save(trade);

            CompensationDefinition compensation = GetOrCreate<CompensationDefinitionAsset>($"{Root}/Payroll/PrototypeTownCraftCompensation.asset");
            compensation.Initialize(PrototypeEconomyContentIds.CompensationTownCraft, "Prototype Town Craft Wage", gold,
                CompensationCategory.PieceRate, CompensationRateBasis.PerOutputQuantity, 8L, 1L, PayrollDurationUnit.OutputUnit, PayScheduleKind.PerOutputBatch);
            Save(compensation);

            BusinessDefinition business = GetOrCreate<BusinessDefinitionAsset>($"{Root}/Businesses/PrototypeTownWorkshop.asset");
            business.Initialize(PrototypeEconomyContentIds.BusinessWorkshop, "Prototype Town Arms Workshop", BusinessCategory.Workshop);
            business.ConfigureSecondhandStock(new MerchantSecondhandStockPolicyData
            {
                enabled = true,
                minimumStockBasisPoints = 1500,
                maximumStockBasisPoints = 5000,
                minimumEligibleStockQuantity = 2,
                minimumConditionBasisPoints = 4500,
                maximumConditionBasisPoints = 9000
            });
            business.ConfigureSimulation(new MerchantSimulationPolicyData
            {
                marketIntervalSeconds = 60,
                maximumCatchUpIntervalsPerFrame = 8,
                openingOperatingFunds = 1000L,
                externalMarketLiquidity = 100000L,
                rawResourceOpeningStock = 120L,
                leatherOpeningStock = 40L,
                exportOpeningStock = 12L,
                rawResourceTarget = 120L,
                leatherTarget = 40L,
                exportStockTarget = 12L,
                exportReserve = 4L,
                productionBaseQualityBasisPoints = 2800,
                productionQualityVariationBasisPoints = 800,
                minimumProductionMarginBasisPoints = 1000,
                retainedMarketIntervals = 10080
            });
            SerializedObject businessSerialized = new SerializedObject(business);
            SetStrings(businessSerialized.FindProperty("permittedGoodsAndServiceCategories"), new[] { "goods.raw.ore", "goods.raw.wood", "goods.raw.leather", "goods.weapon.sword", "goods.weapon.bow" });
            businessSerialized.ApplyModifiedPropertiesWithoutUndo();
            Save(business);

            PropertyDefinition property = GetOrCreate<PropertyDefinitionAsset>($"{Root}/Properties/PrototypeTownWorkshopProperty.asset");
            property.Initialize(PrototypeEconomyContentIds.PropertyWorkshop, "Prototype Town Workshop Property", PropertyCategory.Workshop);
            property.SetPolicies(ownershipModels: new[] { PropertyOwnershipModel.Business, PropertyOwnershipModel.Sole },
                useCategories: new[] { PropertyUseCategory.Production, PropertyUseCategory.Retail, PropertyUseCategory.Storage }, currencyId: gold.Id);
            Save(property);

            ContractFinanceDefinition employment = Contract("Employment", PrototypeEconomyContentIds.ContractEmployment, EconomicContractCategory.EmploymentReference,
                new[] { ContractPartyRole.Offeror, ContractPartyRole.Offeree }, ContractTermCategory.Service, 8L, gold.Id);
            ContractFinanceDefinition rental = Contract("Rental", PrototypeEconomyContentIds.ContractRental, EconomicContractCategory.Rental,
                new[] { ContractPartyRole.Creditor, ContractPartyRole.Debtor }, ContractTermCategory.Rent, 20L, gold.Id);
            ContractFinanceDefinition loan = Contract("Loan", PrototypeEconomyContentIds.ContractLoan, EconomicContractCategory.Loan,
                new[] { ContractPartyRole.Lender, ContractPartyRole.Borrower }, ContractTermCategory.Repayment, 100L, gold.Id);

            RevenueRatePolicyData taxRate = new RevenueRatePolicyData
            {
                ratePolicyId = "revenue-rate.prototype-town.sales-tax",
                rateKind = RevenueRateKind.FlatProportional,
                currencyOrUnitId = gold.Id,
                rate = new RevenueRationalData { numerator = 5L, denominator = 100L },
                smallestChargeableUnit = 1L,
                roundingMode = RevenueRoundingMode.Down
            };
            InstitutionalRevenueDefinition revenue = GetOrCreate<InstitutionalRevenueDefinition>($"{Root}/Revenue/PrototypeTownSalesTax.asset");
            revenue.Initialize(PrototypeEconomyContentIds.RevenueSalesTax, "Prototype Town Sales Tax", InstitutionalRevenueCategory.SalesTaxFoundation,
                InstitutionKind.SettlementFoundation, InstitutionalRevenueAuthorityCategory.Assess, gold, TaxBaseKind.TransactionGrossAmount,
                taxRate, AssessmentPeriodKind.PerTransaction, new[] { RevenueSubjectKind.Buyer, RevenueSubjectKind.Seller },
                new[] { TaxableEventCategory.CompletedTrade }, allowsRefunds: true);
            Save(revenue);

            EconomicRegionDefinition region = GetOrCreate<EconomicRegionDefinitionAsset>($"{Root}/Regional/PrototypeTownRegion.asset");
            region.Initialize(PrototypeEconomyContentIds.RegionTown, "Prototype Town", EconomicRegionCategory.SettlementEconomy,
                new[] { LaborCategory.GeneralLabor, LaborCategory.CraftLabor, LaborCategory.MerchantLabor });
            Save(region);

            CommodityDefinition ironCommodity = Commodity("Iron Ore", PrototypeEconomyContentIds.CommodityIronOre, CommodityCategory.Ore, PrototypeEconomyContentIds.SubjectIronOre, ironOre);
            CommodityDefinition woodCommodity = Commodity("Wood Log", PrototypeEconomyContentIds.CommodityWoodLog, CommodityCategory.Wood, PrototypeEconomyContentIds.SubjectWoodLog, woodLog);
            CommodityDefinition leatherCommodity = Commodity("Leather Strip", PrototypeEconomyContentIds.CommodityLeatherStrip, CommodityCategory.Leather, PrototypeEconomyContentIds.SubjectLeatherStrip, leatherStrip);
            CommodityDefinition swordCommodity = Commodity("Low-Quality Iron Sword", PrototypeEconomyContentIds.CommoditySword, CommodityCategory.Weapons, PrototypeEconomyContentIds.SubjectSword, sword);
            CommodityDefinition bowCommodity = Commodity("Low-Quality Wooden Bow", PrototypeEconomyContentIds.CommodityBow, CommodityCategory.Weapons, PrototypeEconomyContentIds.SubjectBow, bow);
            AggregateProductionProfileDefinition swordProduction = Production("Iron Sword Export", PrototypeEconomyContentIds.ProductionSword,
                new[] { Quantity(PrototypeEconomyContentIds.CommodityIronOre, 3L), Quantity(PrototypeEconomyContentIds.CommodityLeatherStrip, 1L) },
                PrototypeEconomyContentIds.CommoditySword, 1L, ProductionProfileCategory.Smithing);
            AggregateProductionProfileDefinition bowProduction = Production("Wooden Bow Export", PrototypeEconomyContentIds.ProductionBow,
                new[] { Quantity(PrototypeEconomyContentIds.CommodityWoodLog, 4L) },
                PrototypeEconomyContentIds.CommodityBow, 1L, ProductionProfileCategory.WorkshopManufacturing);
            RegionalCommodityQuantityDefinitionData ironNeed = Quantity(PrototypeEconomyContentIds.CommodityIronOre, 3L);
            RegionalCommodityQuantityDefinitionData leatherNeed = Quantity(PrototypeEconomyContentIds.CommodityLeatherStrip, 1L);
            RegionalCommodityQuantityDefinitionData woodNeed = Quantity(PrototypeEconomyContentIds.CommodityWoodLog, 4L);
            AggregateConsumptionProfileDefinition inputs = GetOrCreate<AggregateConsumptionProfileDefinitionAsset>($"{Root}/Regional/PrototypeTownRawInputConsumption.asset");
            inputs.Initialize(PrototypeEconomyContentIds.ConsumptionInputs, "Prototype Town Raw Input Demand", ConsumptionProfileCategory.ProducerInput, new[] { ironNeed, leatherNeed, woodNeed });
            Save(inputs);

            AddCatalogDefinitions(new ScriptableObject[]
            {
                market, ironSubject, woodSubject, leatherSubject, swordSubject, bowSubject, trade, compensation, business, property,
                employment, rental, loan, revenue, region, ironCommodity, woodCommodity, leatherCommodity, swordCommodity, bowCommodity,
                swordProduction, bowProduction, inputs
            });
            AddMarketStallToScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Group 8 economy content authored and Prototype market stall ensured.");
        }

        private static MarketSubjectDefinition Subject(string name, string id, ItemDefinition item, CurrencyDefinition gold, long price)
        {
            MarketSubjectDefinition asset = GetOrCreate<MarketSubjectDefinitionAsset>($"{Root}/Markets/{name.Replace(" ", string.Empty)}.asset");
            asset.Initialize(id, name, MarketSubjectKind.ItemDefinition, item.Id, gold, price);
            Save(asset);
            return asset;
        }

        private static ContractFinanceDefinition Contract(string name, string id, EconomicContractCategory category, ContractPartyRole[] roles, ContractTermCategory termCategory, long amount, string currencyId)
        {
            ContractFinanceDefinition asset = GetOrCreate<ContractFinanceDefinition>($"{Root}/Contracts/PrototypeTown{name}.asset");
            asset.Initialize(id, $"Prototype Town {name}", category, roles, new[]
            {
                new ContractTermData { termId = $"term.{id}.primary", category = termCategory, currencyId = currencyId, amountUnits = amount, maxOccurrences = 1 }
            });
            Save(asset);
            return asset;
        }

        private static CommodityDefinition Commodity(string name, string id, CommodityCategory category, string subjectId, ItemDefinition item)
        {
            CommodityDefinition asset = GetOrCreate<CommodityDefinitionAsset>($"{Root}/Regional/{name.Replace(" ", string.Empty)}Commodity.asset");
            asset.Initialize(id, name, category, CommodityUnit.Each, subjectId);
            SerializedObject serialized = new SerializedObject(asset);
            serialized.FindProperty("itemDefinition").objectReferenceValue = item;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Save(asset);
            return asset;
        }

        private static AggregateProductionProfileDefinition Production(string name, string id, IReadOnlyList<RegionalCommodityQuantityDefinitionData> productionInputs, string outputId, long outputQuantity, ProductionProfileCategory category)
        {
            AggregateProductionProfileDefinition asset = GetOrCreate<AggregateProductionProfileDefinitionAsset>($"{Root}/Regional/{name.Replace(" ", string.Empty)}Production.asset");
            asset.Initialize(id, name, category, new[] { Quantity(outputId, outputQuantity) }, productionInputs,
                new[] { Labor(LaborCategory.CraftLabor, 1L) });
            Save(asset);
            return asset;
        }

        private static RegionalCommodityQuantityDefinitionData Quantity(string id, long amount)
        {
            RegionalCommodityQuantityDefinitionData value = new RegionalCommodityQuantityDefinitionData();
            value.Initialize(id, CommodityUnit.Each, amount);
            return value;
        }

        private static RegionalLaborQuantityDefinitionData Labor(LaborCategory category, long amount)
        {
            RegionalLaborQuantityDefinitionData value = new RegionalLaborQuantityDefinitionData();
            value.Initialize(category, amount);
            return value;
        }

        private static void AddCatalogDefinitions(IReadOnlyList<ScriptableObject> definitions)
        {
            DefinitionCatalog catalog = Load<DefinitionCatalog>(CatalogPath);
            SerializedObject serialized = new SerializedObject(catalog);
            SerializedProperty sections = serialized.FindProperty("sections");
            int index = -1;
            for (int i = 0; i < sections.arraySize; i++)
            {
                if (sections.GetArrayElementAtIndex(i).FindPropertyRelative("domainId").stringValue == "domain.economy") { index = i; break; }
            }
            if (index < 0) { index = sections.arraySize; sections.InsertArrayElementAtIndex(index); }
            SerializedProperty section = sections.GetArrayElementAtIndex(index);
            section.FindPropertyRelative("domainId").stringValue = "domain.economy";
            SerializedProperty values = section.FindPropertyRelative("definitions");
            values.arraySize = definitions.Count;
            for (int i = 0; i < definitions.Count; i++) values.GetArrayElementAtIndex(i).objectReferenceValue = definitions[i];
            serialized.FindProperty("contentVersion").stringValue = "phase-3.group-8";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            string manifest = string.Join("\n", catalog.GetDefinitions().Where(item => item != null).OrderBy(item => item.Id, StringComparer.Ordinal)
                .Select(item => $"{item.Id}|{AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath((UnityEngine.Object)item))}"));
            using SHA256 sha = SHA256.Create();
            serialized.Update();
            serialized.FindProperty("contentHash").stringValue = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(manifest))).Replace("-", string.Empty).ToLowerInvariant();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        private static void AddMarketStallToScene()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject stall = GameObject.Find("Prototype Market Stall");
            if (stall == null)
            {
                stall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stall.name = "Prototype Market Stall";
                stall.transform.SetPositionAndRotation(new Vector3(4f, 0.75f, 4f), Quaternion.identity);
                stall.transform.localScale = new Vector3(2.4f, 1.5f, 1.2f);
            }
            if (stall.GetComponent<PrototypeMarketStall>() == null) stall.AddComponent<PrototypeMarketStall>();
            BoxCollider collider = stall.GetComponent<BoxCollider>();
            if (collider != null)
            {
                collider.isTrigger = true;
                collider.providesContacts = false;
            }
            Renderer renderer = stall.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = CreateMarketMaterial();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static Material CreateMarketMaterial()
        {
            const string path = Root + "/PrototypeMarketStall.mat";
            EnsureFolder(Root);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { name = "Prototype Market Stall", color = new Color(0.34f, 0.19f, 0.08f) };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static T Load<T>(string path) where T : UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException($"Required asset missing: {path}");
        private static T GetOrCreate<T>(string path) where T : ScriptableObject
        {
            EnsureFolder(System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/'));
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
            asset = ScriptableObject.CreateInstance<T>(); asset.name = System.IO.Path.GetFileNameWithoutExtension(path); AssetDatabase.CreateAsset(asset, path); return asset;
        }
        private static void EnsureFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/'); EnsureFolder(parent); AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
        private static void Save(UnityEngine.Object asset) { EditorUtility.SetDirty(asset); }
        private static void SetStrings(SerializedProperty property, IReadOnlyList<string> values)
        { property.arraySize = values.Count; for (int i = 0; i < values.Count; i++) property.GetArrayElementAtIndex(i).stringValue = values[i]; }
    }
}
