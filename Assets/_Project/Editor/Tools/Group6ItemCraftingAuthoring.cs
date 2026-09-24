using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Inventory.Composition;
using UnityIsekaiGame.Inventory.Crafting;
using UnityIsekaiGame.Inventory.Experimentation;
using UnityIsekaiGame.Inventory.Production;
using UnityIsekaiGame.Inventory.Quality;
using UnityIsekaiGame.Inventory.Recipes;
using UnityIsekaiGame.Stats;
using UnityIsekaiGame.Skills;

namespace UnityIsekaiGame.Editor
{
    public static class Group6ItemCraftingAuthoring
    {
        private const string Root = "Assets/_Project/Content/Items/Crafting";
        private const string PrototypeItems = "Assets/_Project/Prototype/Content/Items";
        private const string PickupRoot = "Assets/_Project/Prototype/Prefabs/Items/Pickup";
        private const string ScenePath = "Assets/_Project/Scenes/Prototype/PrototypeScene.unity";
        private const string CompositionPolicyId = "composition-policy.input-derived";
        private const string QualityPolicyId = "quality-policy.crafting.standard";
        private const string AffixPolicyId = "affix-policy.crafting.standard";
        private const string DurabilityPolicyId = "durability-policy.crafting.standard";

        [MenuItem("Tools/Unity Isekai Game/Phase 3/Author Group 6 Item and Crafting Content")]
        public static void Author()
        {
            EnsureFolders();
            AuthorDisassemblySkill();
            AuthorSalvagingSkill();

            MaterialDefinition ironOre = Material("IronOre", "material.iron-ore", "Iron Ore", MaterialCategory.Mineral, 3.5f, 0.55f, 0.55f, 0.15f, "material.mineral", "material.iron-bearing", "material.metal");
            MaterialDefinition iron = Material("Iron", "material.iron", "Iron", MaterialCategory.Metal, 7.85f, 0.75f, 0.78f, 0.2f, "material.metal", "material.iron");
            MaterialDefinition wood = Material("Wood", "material.wood", "Wood", MaterialCategory.Wood, 0.7f, 0.35f, 0.55f, 0.75f, "material.wood", "material.organic");
            MaterialDefinition leather = Material("Leather", "material.leather", "Leather", MaterialCategory.Leather, 0.86f, 0.3f, 0.62f, 0.72f, "material.leather", "material.organic");
            MaterialDefinition glass = Material("Glass", "material.glass", "Glass", MaterialCategory.Glass, 2.5f, 0.65f, 0.25f, 0.05f, "material.glass", "material.brittle");
            MaterialDefinition liquid = Material("PotionLiquid", "material.potion-liquid", "Potion Liquid", MaterialCategory.Liquid, 1f, 0f, 0.1f, 1f, "material.liquid", "material.alchemical");
            Compatibility("IronWoodCompatibility", "material-compatibility.iron-wood", "Iron and Wood", iron, wood, MaterialCompatibilityOutcome.Compatible, 1f);

            ItemDefinition woodLog = EnsureWoodLogItem(wood);
            ItemDefinition leatherStrip = EnsureLeatherStripItem(leather);
            Dictionary<string, ItemDefinition> items = LoadPrototypeItems().ToDictionary(item => item.Id, StringComparer.Ordinal);
            items[woodLog.Id] = woodLog;
            items[leatherStrip.Id] = leatherStrip;

            ConfigureItem(items, "item.prototype-iron-ore", ItemInstanceMode.DefinitionOnly, Composition(ironOre, 1f));
            ConfigureItem(items, "item.wood-log", ItemInstanceMode.DefinitionOnly, Composition(wood, 1f));
            ConfigureItem(items, "item.leather-strip", ItemInstanceMode.DefinitionOnly, Composition(leather, 1f));
            ConfigureItem(items, "item.prototype-sword", ItemInstanceMode.AlwaysInstanced, Composition(iron, 3f, leather, 1f));
            ConfigureItem(items, "item.prototype-helmet", ItemInstanceMode.AlwaysInstanced, Composition(iron, 4f));
            ConfigureItem(items, "item.prototype-shield", ItemInstanceMode.AlwaysInstanced, Composition(iron, 3f, wood, 2f));
            ConfigureItem(items, "item.prototype-bow", ItemInstanceMode.AlwaysInstanced, Composition(wood, 4f));
            ConfigureItem(items, "item.prototype-arrow", ItemInstanceMode.DefinitionOnly, Composition(wood, 0.8f, iron, 0.2f));
            ConfigureItem(items, "item.health-potion", ItemInstanceMode.DefinitionOnly, Composition(glass, 0.2f, liquid, 0.8f));
            ConfigureItem(items, "item.mana-potion", ItemInstanceMode.DefinitionOnly, Composition(glass, 0.2f, liquid, 0.8f));
            ConfigureItem(items, "item.stamina-potion", ItemInstanceMode.DefinitionOnly, Composition(glass, 0.2f, liquid, 0.8f));
            SetDisplayName(items, "item.prototype-sword", "Iron Sword with Leather Grip");
            SetDisplayName(items, "item.prototype-helmet", "Iron Helmet");
            SetDisplayName(items, "item.prototype-shield", "Wood Shield with Iron Rim");
            SetDisplayName(items, "item.prototype-bow", "Wood Bow with Wood Grip");
            SetDisplayName(items, "item.prototype-arrow", "Wood Arrows with Iron Heads");

            ItemAffixDefinition keen = Affix("KeenAffix", "affix.keen", "Keen", ItemAffixClassification.Prefix, CalculatedStat("PhysicalPowerCalculatedStat.asset"), 2f, "affix-group.weapon-edge", items["item.prototype-sword"], items["item.prototype-bow"]);
            ItemAffixDefinition sturdy = Affix("SturdyAffix", "affix.sturdy", "Sturdy", ItemAffixClassification.Prefix, CalculatedStat("PhysicalDefenseCalculatedStat.asset"), 2f, "affix-group.structural", items["item.prototype-helmet"], items["item.prototype-shield"]);
            ItemDefinition[] catalystTargets = { items["item.prototype-sword"], items["item.prototype-helmet"], items["item.prototype-shield"], items["item.prototype-bow"] };
            ItemAffixDefinition vitalityInfused = CatalystAffix("VitalityInfusedAffix", "affix.catalyst-vitality", "Vitality Infused", CalculatedStat("MaximumHealthCalculatedStat.asset"), "affix-group.catalyst-vitality", catalystTargets);
            ItemAffixDefinition manaInfused = CatalystAffix("ManaInfusedAffix", "affix.catalyst-mana", "Mana Infused", CalculatedStat("MaximumManaCalculatedStat.asset"), "affix-group.catalyst-mana", catalystTargets);
            ItemAffixDefinition staminaInfused = CatalystAffix("StaminaInfusedAffix", "affix.catalyst-stamina", "Stamina Infused", CalculatedStat("MaximumStaminaCalculatedStat.asset"), "affix-group.catalyst-stamina", catalystTargets);
            CatalystEffect("HealthPotionCatalyst", "crafting-catalyst.health-potion", "Health Potion Infusion", items["item.health-potion"], vitalityInfused);
            CatalystEffect("ManaPotionCatalyst", "crafting-catalyst.mana-potion", "Mana Potion Infusion", items["item.mana-potion"], manaInfused);
            CatalystEffect("StaminaPotionCatalyst", "crafting-catalyst.stamina-potion", "Stamina Potion Infusion", items["item.stamina-potion"], staminaInfused);
            _ = keen;
            _ = sturdy;

            ProductionToolDefinition hammer = Tool();
            ProductionStationDefinition station = Station();
            ProductionRequirementDefinition stationRequirement = StationRequirement(station);
            Policy("InputDerivedCompositionPolicy", CompositionPolicyId, "Input-Derived Composition", CraftingOutputPolicyKind.CompositionTransfer, 0.5f, 1f, 0f, 0);
            Policy("StandardCraftingQualityPolicy", QualityPolicyId, "Standard Crafting Quality", CraftingOutputPolicyKind.QualityGeneration, 0.55f, 1f, 0f, 0);
            Policy("StandardCraftingAffixPolicy", AffixPolicyId, "Standard Crafting Affixes", CraftingOutputPolicyKind.AffixGeneration, 0.5f, 1f, 0.25f, 1);
            Policy("StandardCraftingDurabilityPolicy", DurabilityPolicyId, "Standard Crafting Durability", CraftingOutputPolicyKind.DurabilityInitialization, 0.5f, 1f, 0f, 0);
            _ = hammer;

            List<RecipeDefinition> recipes = new List<RecipeDefinition>
            {
                Recipe("Sword", "recipe.prototype-sword", "Forge Prototype Sword", RecipeCategory.Forging, stationRequirement, items["item.prototype-sword"], iron,
                    MaterialFamilyInput("input.blade", "material.metal", 1f, "component.blade"),
                    MaterialFamilyInput("input.guard", "material.metal", 1f, "component.guard"),
                    MaterialFamilyInput("input.hilt", "material.leather", 1f, "component.hilt"),
                    MaterialFamilyInput("input.pommel", "material.metal", 1f, "component.pommel")),
                Recipe("Helmet", "recipe.prototype-helmet", "Forge Prototype Helmet", RecipeCategory.Forging, stationRequirement, items["item.prototype-helmet"], iron,
                    MaterialFamilyInput("input.shell", "material.metal", 3f, "component.shell"),
                    MaterialFamilyInput("input.fittings", "material.metal", 1f, "component.fittings")),
                Recipe("Shield", "recipe.prototype-shield", "Assemble Prototype Shield", RecipeCategory.Assembly, stationRequirement, items["item.prototype-shield"], iron,
                    MaterialFamilyInput("input.face", "material.wood", 2f, "component.face"),
                    MaterialFamilyInput("input.rim", "material.metal", 2f, "component.rim"),
                    MaterialFamilyInput("input.boss", "material.metal", 1f, "component.boss")),
                Recipe("Bow", "recipe.prototype-bow", "Craft Prototype Bow", RecipeCategory.Woodworking, stationRequirement, items["item.prototype-bow"], wood,
                    MaterialFamilyInput("input.limbs", "material.wood", 2f, "component.limbs"),
                    MaterialFamilyInput("input.grip", "material.wood", 1f, "component.grip"),
                    MaterialFamilyInput("input.fittings", "material.wood", 1f, "component.fittings")),
                Recipe("Arrows", "recipe.prototype-arrows", "Craft Prototype Arrows", RecipeCategory.Assembly, stationRequirement, items["item.prototype-arrow"], wood,
                    new[]
                    {
                        MaterialFamilyInput("input.shafts", "material.wood", 1f, "component.shafts"),
                        MaterialFamilyInput("input.heads", "material.metal", 1f, "component.heads")
                    }, 10f)
            };

            ProductionChain(recipes, stationRequirement);
            Experiment(stationRequirement);
            CreateWoodPickupPrefab(woodLog);
            AuthorPrototypeScene();
            DefinitionCatalogBuilder.RebuildPrototypeCatalog();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Authored Group 6 item, crafting, production, experimentation, and Prototype workstation content.");
        }

        public static void AuthorBatch()
        {
            Author();
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>("Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset");
            DefinitionValidationReport report = DefinitionCatalogValidator.Validate(catalog);
            foreach (DefinitionIdValidationMessage message in report.Messages)
            {
                Debug.Log($"[{message.Severity}] {message.Message}");
            }

            if (report.ErrorCount > 0)
            {
                throw new InvalidOperationException($"Group 6 authoring produced {report.ErrorCount} catalog error(s).");
            }
        }

        private static void AuthorDisassemblySkill()
        {
            SkillDefinition skill = Asset<SkillDefinition>("Assets/_Project/Content/Characters/Skills/DisassemblySkill.asset");
            SkillNaturalLearningDefinition learning = new SkillNaturalLearningDefinition();
            Set(learning, "enabled", true);
            Set(learning, "qualifyingEventId", "action.item-recovery");
            Set(learning, "actionCategory", SkillActionEventCategory.CraftingAction);
            Set(learning, "requiredCount", 25);
            Set(learning, "grantedStartingGrade", SkillGrade.F);
            SkillXpThresholdDefinition[] thresholds = Enumerable.Range(0, 7).Select(index =>
            {
                SkillXpThresholdDefinition threshold = new SkillXpThresholdDefinition();
                Set(threshold, "fromGrade", (SkillGrade)index);
                Set(threshold, "xpRequired", 25 * (1 << index));
                return threshold;
            }).ToArray();
            Set(skill, "skillId", "skill.disassembly");
            Set(skill, "displayName", "Disassembly");
            Set(skill, "description", "Learned proficiency at dismantling crafted goods and salvaging broken items while preserving useful components.");
            Set(skill, "primaryCategory", AssetDatabase.LoadAssetAtPath<CategoryDefinition>("Assets/_Project/Content/Core/Categories/SkillCategory.asset"));
            Set(skill, "tags", Array.Empty<TagDefinition>());
            Set(skill, "alphaEnabled", true);
            Set(skill, "naturalLearning", learning);
            Set(skill, "defaultNaturalStartingGrade", SkillGrade.F);
            Set(skill, "xpThresholds", thresholds);
            Set(skill, "gradePackages", Array.Empty<SkillGradeEffectPackageDefinition>());
            Set(skill, "abilityUnlocks", Array.Empty<SkillAbilityUnlockDefinition>());
            Set(skill, "directGrantDefaultGrade", SkillGrade.F);
            Dirty(skill);
        }

        private static void AuthorSalvagingSkill()
        {
            SkillDefinition skill = Asset<SkillDefinition>("Assets/_Project/Content/Characters/Skills/SalvagingSkill.asset");
            SkillNaturalLearningDefinition learning = new SkillNaturalLearningDefinition();
            Set(learning, "enabled", true);
            Set(learning, "qualifyingEventId", "action.salvage-pickup");
            Set(learning, "actionCategory", SkillActionEventCategory.CraftingAction);
            Set(learning, "requiredCount", 25);
            Set(learning, "grantedStartingGrade", SkillGrade.F);
            SkillXpThresholdDefinition[] thresholds = Enumerable.Range(0, 7).Select(index =>
            {
                SkillXpThresholdDefinition threshold = new SkillXpThresholdDefinition();
                Set(threshold, "fromGrade", (SkillGrade)index);
                Set(threshold, "xpRequired", 25 * (1 << index));
                return threshold;
            }).ToArray();
            Set(skill, "skillId", "skill.salvaging");
            Set(skill, "displayName", "Salvaging");
            Set(skill, "description", "Learned proficiency at spotting and gathering extra usable pieces from fallen debris and resource drops.");
            Set(skill, "primaryCategory", AssetDatabase.LoadAssetAtPath<CategoryDefinition>("Assets/_Project/Content/Core/Categories/SkillCategory.asset"));
            Set(skill, "tags", Array.Empty<TagDefinition>());
            Set(skill, "alphaEnabled", true);
            Set(skill, "naturalLearning", learning);
            Set(skill, "defaultNaturalStartingGrade", SkillGrade.F);
            Set(skill, "xpThresholds", thresholds);
            Set(skill, "gradePackages", Array.Empty<SkillGradeEffectPackageDefinition>());
            Set(skill, "abilityUnlocks", Array.Empty<SkillAbilityUnlockDefinition>());
            Set(skill, "directGrantDefaultGrade", SkillGrade.F);
            Dirty(skill);
        }

        private static MaterialDefinition Material(string file, string id, string display, MaterialCategory category, float density, float hardness, float durability, float flexibility, params string[] tags)
        {
            MaterialDefinition asset = Asset<MaterialDefinition>($"{Root}/Materials/{file}.asset");
            Set(asset, "materialId", id);
            Set(asset, "displayName", display);
            Set(asset, "description", $"Authored {display.ToLowerInvariant()} material used by the Prototype crafting system.");
            Set(asset, "category", category);
            Set(asset, "materialTags", tags);
            Set(asset, "canBeStructural", true);
            Set(asset, "canBeCoating", category is MaterialCategory.Metal or MaterialCategory.Glass or MaterialCategory.Liquid);
            Set(asset, "canBeBinding", category is MaterialCategory.Wood or MaterialCategory.Liquid);
            Set(asset, "physicalProperties", new MaterialPhysicalPropertySet
            {
                densityKgPerLiter = density,
                hardness = hardness,
                durability = durability,
                flexibility = flexibility,
                conductivity = category == MaterialCategory.Metal ? 0.8f : 0.1f,
                flammability = category == MaterialCategory.Wood ? 0.8f : 0f,
                biologicalCompatibility = category == MaterialCategory.Liquid ? 0.8f : 0.2f,
                propertyProfileId = $"material-profile.{id.Substring(id.IndexOf('.') + 1)}"
            });
            Set(asset, "constituents", Array.Empty<CompositeMaterialConstituentDefinition>());
            Dirty(asset);
            return asset;
        }

        private static MaterialCompatibilityRuleDefinition Compatibility(string file, string id, string display, MaterialDefinition source, MaterialDefinition target, MaterialCompatibilityOutcome outcome, float durabilityMultiplier)
        {
            MaterialCompatibilityRuleDefinition asset = Asset<MaterialCompatibilityRuleDefinition>($"{Root}/Materials/{file}.asset");
            Set(asset, "ruleId", id);
            Set(asset, "displayName", display);
            Set(asset, "sourceMaterial", source);
            Set(asset, "targetMaterial", target);
            Set(asset, "outcome", outcome);
            Set(asset, "durabilityMultiplier", durabilityMultiplier);
            Set(asset, "message", "Iron fittings and wooden structures are compatible in ordinary prototype construction.");
            Dirty(asset);
            return asset;
        }

        private static ItemDefinition EnsureWoodLogItem(MaterialDefinition wood)
        {
            ItemDefinition item = Asset<ItemDefinition>($"{PrototypeItems}/WoodLog.asset");
            Set(item, "itemId", "item.wood-log");
            Set(item, "displayName", "Wood Log");
            Set(item, "description", "A basic wooden resource used for bows, shields, arrows, and future construction recipes.");
            Set(item, "primaryCategory", AssetDatabase.LoadAssetAtPath<CategoryDefinition>("Assets/_Project/Content/Core/Categories/ItemMaterialCategory.asset"));
            Set(item, "tags", new[] { AssetDatabase.LoadAssetAtPath<TagDefinition>("Assets/_Project/Content/Core/Tags/MaterialTag.asset") });
            Set(item, "rarity", AssetDatabase.LoadAssetAtPath<RarityDefinition>("Assets/_Project/Content/Items/Rarities/CommonRarity.asset"));
            Set(item, "instanceMode", ItemInstanceMode.DefinitionOnly);
            Set(item, "stackable", true);
            Set(item, "maximumStackSize", 20);
            Set(item, "useEffects", Array.Empty<ItemUseEffect>());
            Set(item, "equipment", null);
            Set(item, "defaultCompositionTemplate", Composition(wood, 1f));
            Dirty(item);
            return item;
        }

        private static ItemDefinition EnsureLeatherStripItem(MaterialDefinition leather)
        {
            ItemDefinition item = Asset<ItemDefinition>($"{PrototypeItems}/LeatherStrip.asset");
            Set(item, "itemId", "item.leather-strip");
            Set(item, "displayName", "Leather Strip");
            Set(item, "description", "A prepared leather resource used for grips, bindings, armor, and future crafting recipes.");
            Set(item, "primaryCategory", AssetDatabase.LoadAssetAtPath<CategoryDefinition>("Assets/_Project/Content/Core/Categories/ItemMaterialCategory.asset"));
            Set(item, "tags", new[] { AssetDatabase.LoadAssetAtPath<TagDefinition>("Assets/_Project/Content/Core/Tags/MaterialTag.asset") });
            Set(item, "rarity", AssetDatabase.LoadAssetAtPath<RarityDefinition>("Assets/_Project/Content/Items/Rarities/CommonRarity.asset"));
            Set(item, "instanceMode", ItemInstanceMode.DefinitionOnly);
            Set(item, "stackable", true);
            Set(item, "maximumStackSize", 20);
            Set(item, "useEffects", Array.Empty<ItemUseEffect>());
            Set(item, "equipment", null);
            Set(item, "defaultCompositionTemplate", Composition(leather, 1f));
            Dirty(item);
            return item;
        }

        private static IEnumerable<ItemDefinition> LoadPrototypeItems()
        {
            return AssetDatabase.FindAssets("t:ItemDefinition", new[] { PrototypeItems })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ItemDefinition>)
                .Where(item => item != null);
        }

        private static void ConfigureItem(IReadOnlyDictionary<string, ItemDefinition> items, string id, ItemInstanceMode mode, ItemCompositionTemplateData composition)
        {
            if (!items.TryGetValue(id, out ItemDefinition item))
            {
                throw new InvalidOperationException($"Required Prototype item '{id}' was not found.");
            }

            Set(item, "instanceMode", mode);
            Set(item, "defaultCompositionTemplate", composition);
            Dirty(item);
        }

        private static void SetDisplayName(IReadOnlyDictionary<string, ItemDefinition> items, string id, string displayName)
        {
            if (!items.TryGetValue(id, out ItemDefinition item))
            {
                throw new InvalidOperationException($"Required Prototype item '{id}' was not found.");
            }

            Set(item, "displayName", displayName);
            Dirty(item);
        }

        private static ItemCompositionTemplateData Composition(MaterialDefinition primary, float primaryQuantity, MaterialDefinition secondary = null, float secondaryQuantity = 0f)
        {
            ItemCompositionTemplateData template = new ItemCompositionTemplateData
            {
                templateVersionId = "template.group-6.v1",
                required = true,
                massAuthority = ItemCompositionMassAuthority.CompositionProjection,
                completeness = ItemCompositionCompleteness.Complete,
                tags = new[] { "item.composition", "composition.authored" }
            };
            AddMaterial(template, primary, primaryQuantity, MaterialEntryRole.PrimaryStructure, 0);
            if (secondary != null && secondaryQuantity > 0f)
            {
                AddMaterial(template, secondary, secondaryQuantity, MaterialEntryRole.Binding, 1);
            }

            template.components.Add(new ItemComponentEntryData
            {
                componentEntryId = "component.main",
                componentName = "Main Body",
                kind = ItemComponentKind.AbstractComponent,
                materialEntryIds = template.materials.Select(entry => entry.entryId).ToArray(),
                count = 1
            });
            return template;
        }

        private static void AddMaterial(ItemCompositionTemplateData template, MaterialDefinition material, float quantity, MaterialEntryRole role, int index)
        {
            template.materials.Add(new ItemMaterialEntryData
            {
                entryId = $"material.{index + 1}",
                materialDefinitionId = material.Id,
                role = role,
                quantity = new MaterialQuantityData
                {
                    value = quantity,
                    unit = Mathf.Approximately(quantity, Mathf.Round(quantity))
                        ? MaterialQuantityUnit.Count
                        : MaterialQuantityUnit.Ratio
                },
                purity = 1f,
                componentEntryId = "component.main",
                tags = material.MaterialTags.ToArray()
            });
        }

        private static ItemAffixDefinition Affix(string file, string id, string display, ItemAffixClassification classification, CalculatedStatDefinition stat, float value, string exclusiveGroup, params ItemDefinition[] applicableItems)
        {
            ItemAffixDefinition asset = Asset<ItemAffixDefinition>($"{Root}/Affixes/{file}.asset");
            CalculatedStatModifierDefinition modifier = new CalculatedStatModifierDefinition();
            Set(modifier, "stat", stat);
            Set(modifier, "operation", StatModifierOperation.FlatAdd);
            Set(modifier, "value", value);
            Set(modifier, "scaleWithStacks", false);
            ItemAffixTierData tier = new ItemAffixTierData
            {
                tierId = $"{id}.tier.1",
                minimumItemQuality = 0f,
                maximumItemQuality = 1f,
                valueMinimum = value,
                valueMaximum = value,
                modifierTemplates = new[] { modifier },
                tags = new[] { "affix.crafting", "affix.prototype" }
            };
            Set(asset, "affixId", id);
            Set(asset, "displayName", display);
            Set(asset, "classification", classification);
            Set(asset, "applicableItemDefinitions", applicableItems ?? Array.Empty<ItemDefinition>());
            Set(asset, "exclusiveGroups", new[] { exclusiveGroup });
            Set(asset, "maximumOccurrences", 1);
            Set(asset, "maximumPrefixCount", 2);
            Set(asset, "maximumSuffixCount", 2);
            Set(asset, "maximumTotalAffixCount", 3);
            Set(asset, "tiers", new[] { tier });
            Set(asset, "generationWeight", 1f);
            Set(asset, "allowedSources", new[] { ItemAffixSource.Generated, ItemAffixSource.Crafted });
            Set(asset, "version", 1);
            Dirty(asset);
            return asset;
        }

        private static CalculatedStatDefinition CalculatedStat(string file)
        {
            CalculatedStatDefinition stat = AssetDatabase.LoadAssetAtPath<CalculatedStatDefinition>($"Assets/_Project/Content/Characters/CalculatedStats/Definitions/{file}");
            if (stat == null)
            {
                throw new InvalidOperationException($"Calculated stat asset '{file}' was not found.");
            }

            return stat;
        }

        private static ItemAffixDefinition CatalystAffix(
            string file,
            string id,
            string display,
            CalculatedStatDefinition stat,
            string exclusiveGroup,
            params ItemDefinition[] applicableItems)
        {
            ItemAffixDefinition asset = Asset<ItemAffixDefinition>($"{Root}/Catalysts/{file}.asset");
            float[] values = { 2f, 4f, 7f, 11f, 16f };
            ItemAffixTierData[] tiers = values.Select((value, index) =>
            {
                CalculatedStatModifierDefinition modifier = new CalculatedStatModifierDefinition();
                Set(modifier, "stat", stat);
                Set(modifier, "operation", StatModifierOperation.FlatAdd);
                Set(modifier, "value", value);
                Set(modifier, "scaleWithStacks", false);
                return new ItemAffixTierData
                {
                    tierId = $"{id}.tier.{index + 1}",
                    sortOrder = index,
                    minimumItemQuality = 0f,
                    maximumItemQuality = 1f,
                    valueMinimum = value,
                    valueMaximum = value,
                    rarityContribution = index * 0.025f,
                    modifierTemplates = new[] { modifier },
                    tags = new[] { "affix.crafting", "affix.catalyst" }
                };
            }).ToArray();
            Set(asset, "affixId", id);
            Set(asset, "displayName", display);
            Set(asset, "classification", ItemAffixClassification.Crafted);
            Set(asset, "applicableItemDefinitions", applicableItems ?? Array.Empty<ItemDefinition>());
            Set(asset, "exclusiveGroups", new[] { exclusiveGroup });
            Set(asset, "maximumOccurrences", 1);
            Set(asset, "maximumPrefixCount", 3);
            Set(asset, "maximumSuffixCount", 3);
            Set(asset, "maximumTotalAffixCount", 6);
            Set(asset, "tiers", tiers);
            Set(asset, "generationWeight", 0f);
            Set(asset, "allowedSources", new[] { ItemAffixSource.Crafted });
            Set(asset, "version", 1);
            Dirty(asset);
            return asset;
        }

        private static CraftingCatalystEffectDefinition CatalystEffect(
            string file,
            string id,
            string display,
            ItemDefinition catalyst,
            ItemAffixDefinition affix)
        {
            CraftingCatalystEffectDefinition asset = Asset<CraftingCatalystEffectDefinition>($"{Root}/Catalysts/{file}.asset");
            Set(asset, "effectId", id);
            Set(asset, "displayName", display);
            Set(asset, "description", $"Consumes {catalyst.DisplayName} for a deterministic weighted chance to apply {affix.DisplayName} to the crafted item.");
            Set(asset, "catalystItems", new[] { catalyst });
            Set(asset, "catalystCategories", Array.Empty<CategoryDefinition>());
            Set(asset, "catalystTags", Array.Empty<TagDefinition>());
            Set(asset, "resultingAffix", affix);
            Set(asset, "generationWeight", 1f);
            Set(asset, "baseChance", 0.08f);
            Set(asset, "chancePerRarityRank", 0.1f);
            Set(asset, "chancePerAdditionalItem", 0.07f);
            Set(asset, "chancePerSkillGrade", 0.02f);
            Set(asset, "maximumChance", 0.9f);
            Set(asset, "additionalItemsPerAffixTier", 2);
            Dirty(asset);
            return asset;
        }

        private static ProductionToolDefinition Tool()
        {
            ProductionToolDefinition asset = Asset<ProductionToolDefinition>($"{Root}/Production/PrototypeSmithingHammer.asset");
            Set(asset, "toolId", "production-tool.smithing-hammer");
            Set(asset, "displayName", "Smithing Hammer");
            Set(asset, "description", "A hammering tool definition reserved for advanced smithing recipes.");
            Set(asset, "category", ProductionToolCategory.Hammering);
            Set(asset, "roles", new[] { ProductionToolRole.Primary });
            Set(asset, "capabilityIds", new[] { "production-capability.hammering" });
            Set(asset, "minimumDurability", 0.05f);
            Set(asset, "durabilityWearPerUse", 1f);
            Dirty(asset);
            return asset;
        }

        private static ProductionStationDefinition Station()
        {
            ProductionStationDefinition asset = Asset<ProductionStationDefinition>($"{Root}/Production/PrototypeWorkstation.asset");
            Set(asset, "stationId", PrototypePersistenceServiceBehaviour.PrototypeWorkstationDefinitionId);
            Set(asset, "displayName", "Prototype Workstation");
            Set(asset, "description", "A shared-world crafting station supporting basic assembly, woodworking, and forging.");
            Set(asset, "category", ProductionStationCategory.Workbench);
            Set(asset, "capabilityIds", new[] { "production-capability.assembly", "production-capability.forging", "production-capability.woodworking" });
            Set(asset, "supportedToolRoles", new[] { ProductionToolRole.Primary, ProductionToolRole.Finishing });
            Set(asset, "concurrentReservationLimit", 1);
            Set(asset, "portable", false);
            Set(asset, "priority", 100);
            Dirty(asset);
            return asset;
        }

        private static ProductionRequirementDefinition StationRequirement(ProductionStationDefinition station)
        {
            ProductionRequirementDefinition asset = Asset<ProductionRequirementDefinition>($"{Root}/Production/PrototypeWorkstationRequirement.asset");
            Set(asset, "requirementId", "production-requirement.prototype-workstation");
            Set(asset, "displayName", "Prototype Workstation");
            Set(asset, "description", "Requires access to the Prototype Workstation at its authoritative location.");
            Set(asset, "requirementGroupId", "requirement-group.station");
            Set(asset, "requirementType", ProductionRequirementType.Station);
            Set(asset, "strictness", ProductionRequirementStrictness.Required);
            Set(asset, "allowSubstitution", false);
            Set(asset, "stationDefinition", station);
            Set(asset, "stationCategory", ProductionStationCategory.Workbench);
            Set(asset, "stationCapabilityId", "production-capability.assembly");
            Set(asset, "quantity", 1f);
            Dirty(asset);
            return asset;
        }

        private static RecipeInputSpecificationData Input(string id, ItemDefinition item, float quantity)
        {
            return new RecipeInputSpecificationData
            {
                inputId = id,
                role = RecipeInputRole.PrimaryMaterial,
                requirementState = RecipeRequirementState.Required,
                classification = RecipeInputClassification.Consumable,
                itemDefinitionId = item.Id,
                quantity = quantity,
                unit = ProductionQuantityUnit.Count,
                allowPartialStacks = true,
                allowMultipleSources = true,
                transferPolicy = RecipeTransferPolicy.InputDerived
            };
        }

        private static RecipeInputSpecificationData MaterialFamilyInput(string id, string materialCategoryId, float quantity, string componentRoleId)
        {
            return new RecipeInputSpecificationData
            {
                inputId = id,
                role = RecipeInputRole.PrimaryMaterial,
                requirementState = RecipeRequirementState.Required,
                classification = RecipeInputClassification.Consumable,
                materialTagIds = new[] { materialCategoryId },
                componentRoleId = componentRoleId,
                quantity = quantity,
                unit = ProductionQuantityUnit.Count,
                allowPartialStacks = true,
                allowMultipleSources = true,
                transferPolicy = RecipeTransferPolicy.InputDerived
            };
        }

        private static CraftingOutputPolicyDefinition Policy(string file, string id, string display, CraftingOutputPolicyKind kind, float quality, float durability, float affixChance, int affixCount)
        {
            CraftingOutputPolicyDefinition asset = Asset<CraftingOutputPolicyDefinition>($"{Root}/Policies/{file}.asset");
            Set(asset, "policyId", id);
            Set(asset, "displayName", display);
            Set(asset, "description", $"Authored {display.ToLowerInvariant()} used by Prototype recipes.");
            Set(asset, "kind", kind);
            Set(asset, "baseQualityNormalized", quality);
            Set(asset, "initialDurabilityNormalized", durability);
            Set(asset, "affixChance", affixChance);
            Set(asset, "maximumAffixCount", affixCount);
            Dirty(asset);
            return asset;
        }

        private static RecipeDefinition Recipe(string file, string id, string display, RecipeCategory category, ProductionRequirementDefinition station, ItemDefinition outputItem, MaterialDefinition outputMaterial, params RecipeInputSpecificationData[] inputs)
        {
            return Recipe(file, id, display, category, station, outputItem, outputMaterial, inputs, 1f);
        }

        private static RecipeDefinition Recipe(string file, string id, string display, RecipeCategory category, ProductionRequirementDefinition station, ItemDefinition outputItem, MaterialDefinition outputMaterial, RecipeInputSpecificationData[] inputs, float outputQuantity)
        {
            RecipeDefinition asset = Asset<RecipeDefinition>($"{Root}/Recipes/{file}Recipe.asset");
            string versionId = $"recipe-version.{id.Substring(id.IndexOf('.') + 1)}.v1";
            RecipeOutputSpecificationData output = new RecipeOutputSpecificationData
            {
                outputId = "output.primary",
                role = RecipeOutputRole.PrimaryOutput,
                itemDefinitionId = outputItem.Id,
                materialDefinitionId = outputMaterial.Id,
                quantity = outputQuantity,
                unit = ProductionQuantityUnit.Count,
                compositionTransferPolicy = RecipeTransferPolicy.InputDerived,
                qualityPolicy = RecipeQualityPolicy.SkillToolStationInfluenced,
                qualityPolicyId = QualityPolicyId,
                affixPolicy = outputItem.Stackable ? RecipeAffixPolicy.None : RecipeAffixPolicy.PolicyReference,
                affixPolicyId = outputItem.Stackable ? string.Empty : AffixPolicyId,
                durabilityPolicy = RecipeDurabilityPolicy.MaterialDerived,
                durabilityPolicyId = DurabilityPolicyId
            };
            Set(asset, "recipeId", id);
            Set(asset, "displayName", display);
            Set(asset, "description", $"Produces {outputQuantity:0} x {outputItem.DisplayName} from collected Prototype resources.");
            Set(asset, "category", category);
            Set(asset, "state", RecipeLifecycleState.Active);
            Set(asset, "currentVersionId", versionId);
            string craftableTag = $"craftable.{outputItem.Id.Split('.').Last().Replace("prototype-", string.Empty)}";
            Set(asset, "tags", new[] { "recipe.prototype", "recipe.crafting", craftableTag });
            Set(asset, "versions", new[] { new RecipeVersionData { versionId = versionId, versionLabel = "1.0", state = RecipeLifecycleState.Active, authorOrSourceId = "source.prototype.workstation" } });
            Set(asset, "variants", Array.Empty<RecipeVariantData>());
            Set(asset, "inputs", inputs);
            Set(asset, "outputs", new[] { output });
            Set(asset, "transferMappings", inputs.Select(input => new RecipeTransferMappingData
            {
                mappingId = $"mapping.{input.inputId}",
                sourceInputId = input.inputId,
                targetOutputId = output.outputId,
                targetComponentId = input.componentRoleId,
                quantityTransferPolicy = RecipeTransferPolicy.InputDerived,
                transferProvenance = true
            }).ToArray());
            Set(asset, "procedureSteps", new[]
            {
                new RecipeProcedureStepData { stepId = "step.prepare", stepKind = RecipeProcedureStepKind.PrepareInput, displayName = "Prepare materials" },
                new RecipeProcedureStepData { stepId = "step.shape", stepKind = category == RecipeCategory.Forging ? RecipeProcedureStepKind.Heat : RecipeProcedureStepKind.Shape, displayName = "Shape components", dependsOnStepIds = new[] { "step.prepare" } },
                new RecipeProcedureStepData { stepId = "step.finish", stepKind = RecipeProcedureStepKind.Finish, displayName = "Finish and inspect", dependsOnStepIds = new[] { "step.shape" } }
            });
            Set(asset, "recipeRequirementIds", new[] { station.Id });
            Set(asset, "batchPolicy", new RecipeBatchPolicyData { scalingPolicy = RecipeBatchScalingPolicy.Fixed, baseBatchSize = 1f, minimumBatchSize = 1f, maximumBatchSize = 1f, batchIncrement = 1f });
            string[] craftingSkills = category switch
            {
                RecipeCategory.Forging => new[] { "skill.smithing" },
                RecipeCategory.Woodworking => new[] { "skill.woodworking" },
                _ when outputItem.Id == "item.prototype-shield" => new[] { "skill.smithing", "skill.woodworking" },
                _ => new[] { "skill.woodworking" }
            };
            float baseDuration = outputItem.Id switch
            {
                "item.prototype-sword" => 12f,
                "item.prototype-helmet" => 14f,
                "item.prototype-shield" => 12f,
                "item.prototype-bow" => 10f,
                "item.prototype-arrow" => 5f,
                _ => 8f
            };
            Set(asset, "craftingScaling", new RecipeCraftingScalingData
            {
                baseDurationSeconds = baseDuration,
                minimumDurationSeconds = outputItem.Id == "item.prototype-arrow" ? 1f : 2f,
                eligibleSkillIds = craftingSkills,
                unskilledDurationMultiplier = 1.25f,
                durationReductionPerSkillGrade = 0.06f,
                minimumSkillDurationMultiplier = 0.55f,
                rarityDurationMultiplierPerRank = 0.12f,
                statDurationMultiplierPerComplexityPoint = 0.03f,
                unskilledQualityAdjustment = -0.05f,
                qualityBonusPerSkillGrade = 0.04f,
                maximumSkillQualityBonus = 0.35f,
                affixChanceBonusPerSkillGrade = 0.025f
            });
            Set(asset, "compositionTransferPolicyId", CompositionPolicyId);
            Set(asset, "qualityGenerationPolicyId", QualityPolicyId);
            Set(asset, "affixGenerationPolicyId", AffixPolicyId);
            Set(asset, "durabilityInitializationPolicyId", DurabilityPolicyId);
            Set(asset, "knowledgeDifficulty", 1);
            Set(asset, "teachingDifficulty", 1);
            Set(asset, "sourceId", "source.prototype.workstation");
            Set(asset, "schemaVersion", 1);
            Dirty(asset);
            return asset;
        }

        private static void ProductionChain(IReadOnlyList<RecipeDefinition> recipes, ProductionRequirementDefinition station)
        {
            RecipeDefinition bow = recipes.First(recipe => recipe.Id == "recipe.prototype-bow");
            RecipeDefinition arrows = recipes.First(recipe => recipe.Id == "recipe.prototype-arrows");
            ProductionChainDefinition asset = Asset<ProductionChainDefinition>($"{Root}/Production/PrototypeRangedKitChain.asset");
            string id = "production-chain.prototype-ranged-kit";
            string version = "production-chain-version.prototype-ranged-kit.v1";
            Set(asset, "chainId", id);
            Set(asset, "displayName", "Prototype Ranged Kit");
            Set(asset, "description", "Produces a bow followed by a bundle of arrows.");
            Set(asset, "category", "production-category.weapon-kit");
            Set(asset, "currentVersionId", version);
            Set(asset, "state", ProductionChainLifecycleState.Active);
            Set(asset, "provenance", "source.prototype.workstation");
            Set(asset, "schemaVersion", 1);
            Set(asset, "versions", new[]
            {
                new ProductionChainVersionData
                {
                    versionId = version,
                    chainDefinitionId = id,
                    state = ProductionChainLifecycleState.Active,
                    stages = new[]
                    {
                        new ProductionStageDefinitionData { stageId = "stage.bow", displayName = "Craft Bow", category = ProductionStageCategory.Assembly, recipeDefinitionId = bow.Id, recipeVersionId = bow.CurrentVersionId, requirementIds = new[] { station.Id }, estimatedDuration = 10f, requiredWorkUnits = 1f },
                        new ProductionStageDefinitionData { stageId = "stage.arrows", displayName = "Craft Arrows", category = ProductionStageCategory.Assembly, recipeDefinitionId = arrows.Id, recipeVersionId = arrows.CurrentVersionId, requirementIds = new[] { station.Id }, dependencyStageIds = new[] { "stage.bow" }, estimatedDuration = 5f, requiredWorkUnits = 1f }
                    }
                }
            });
            Dirty(asset);
        }

        private static void Experiment(ProductionRequirementDefinition station)
        {
            ExperimentDefinition asset = Asset<ExperimentDefinition>($"{Root}/Experiments/IronWoodSubstitutionExperiment.asset");
            Set(asset, "experimentId", "experiment.iron-wood-substitution");
            Set(asset, "displayName", "Iron and Wood Substitution Study");
            Set(asset, "description", "Tests how changing the balance of iron and wood affects crafted equipment.");
            Set(asset, "category", ExperimentCategory.MaterialCompatibilityTesting);
            Set(asset, "defaultPlanMode", ExperimentPlanMode.Controlled);
            Set(asset, "tags", new[] { "experiment.prototype", "experiment.crafting" });
            Set(asset, "supportedTargetTypes", new[] { "recipe", "material" });
            Set(asset, "variables", new[]
            {
                new ExperimentVariableDefinitionData { variableId = "variable.material-ratio", category = ExperimentVariableCategory.IngredientQuantity, targetSubjectId = "material.iron", valueType = ExperimentValueType.Range, minimumValue = 0.1f, maximumValue = 10f, unit = "count", role = ExperimentVariableRole.Independent }
            });
            Set(asset, "requiredControls", new[]
            {
                new ExperimentControlDefinitionData { controlId = "control.standard-recipe", baselineType = "recipe", baselineReferenceId = "recipe.prototype-shield" }
            });
            Set(asset, "productionRequirementIds", new[] { station.Id });
            Set(asset, "procedureTemplateId", "procedure.experiment.crafting-comparison");
            Set(asset, "provenance", "source.prototype.workstation");
            Set(asset, "evidencePolicy", new ExperimentPolicyData { minimumTrials = 1, independentReproductionThreshold = 1, confirmationEvidenceThreshold = 2, minimumEvidenceStrength = 500, allowAccidentalOutputs = true });
            Set(asset, "reproducibilityPolicy", new ExperimentPolicyData { minimumTrials = 2, independentReproductionThreshold = 2, confirmationEvidenceThreshold = 2, minimumEvidenceStrength = 500 });
            Set(asset, "confirmationPolicy", new ExperimentPolicyData { minimumTrials = 2, independentReproductionThreshold = 2, confirmationEvidenceThreshold = 2, minimumEvidenceStrength = 500, allowAuthoritativeRegistrationProposal = true });
            Set(asset, "schemaVersion", 1);
            Dirty(asset);
        }

        private static void CreateWoodPickupPrefab(ItemDefinition woodLog)
        {
            string materialPath = "Assets/_Project/Prototype/Materials/Items/MAT_PrototypeWoodLog.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader) { name = "MAT_PrototypeWoodLog", color = new Color(0.34f, 0.16f, 0.055f, 1f) };
                AssetDatabase.CreateAsset(material, materialPath);
            }

            string prefabPath = $"{PickupRoot}/Pickup - Wood Log.prefab";
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            root.name = "Pickup - Wood Log";
            root.transform.localScale = new Vector3(0.28f, 0.65f, 0.28f);
            root.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            root.GetComponent<Renderer>().sharedMaterial = material;
            root.GetComponent<Collider>().isTrigger = true;
            WorldItemPickup pickup = root.AddComponent<WorldItemPickup>();
            pickup.Configure(woodLog, 3, disableWhenCollected: true);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            WorldItemPickup prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)?.GetComponent<WorldItemPickup>();
            Set(woodLog, "worldPickupPrefab", prefab);
            Dirty(woodLog);
        }

        private static void AuthorPrototypeScene()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject workstation = scene.GetRootGameObjects().SelectMany(Flatten).FirstOrDefault(item => item.name == "Prototype Workstation");
            if (workstation == null)
            {
                throw new InvalidOperationException("Prototype Workstation scene object was not found.");
            }

            if (workstation.GetComponent<PrototypeCraftingWorkstation>() == null)
            {
                workstation.AddComponent<PrototypeCraftingWorkstation>();
            }

            CreateCraftingBlockVisual(workstation);

            bool woodExists = scene.GetRootGameObjects().SelectMany(Flatten).Any(item => item.name == "Pickup - Wood Log");
            if (!woodExists)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PickupRoot}/Pickup - Wood Log.prefab");
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.name = "Pickup - Wood Log";
                instance.transform.position = workstation.transform.position + new Vector3(2f, 0.35f, 0f);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void CreateCraftingBlockVisual(GameObject workstation)
        {
            const string visualName = "Crafting Block Visual";
            Transform existing = workstation.transform.Find(visualName);
            GameObject visual = existing == null ? GameObject.CreatePrimitive(PrimitiveType.Cube) : existing.gameObject;
            visual.name = visualName;
            visual.transform.SetParent(workstation.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = new Vector3(1.6f, 1.15f, 1.4f);

            Collider visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null)
            {
                UnityEngine.Object.DestroyImmediate(visualCollider);
            }

            const string materialPath = "Assets/_Project/Prototype/Materials/Items/MAT_PrototypeCraftingBlock.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader)
                {
                    name = "MAT_PrototypeCraftingBlock",
                    color = new Color(0.42f, 0.23f, 0.08f, 1f)
                };
                AssetDatabase.CreateAsset(material, materialPath);
            }

            visual.GetComponent<Renderer>().sharedMaterial = material;
            BoxCollider interactionArea = workstation.GetComponent<BoxCollider>();
            if (interactionArea != null)
            {
                interactionArea.isTrigger = true;
                interactionArea.center = Vector3.zero;
                interactionArea.size = visual.transform.localScale;
            }
        }

        private static IEnumerable<GameObject> Flatten(GameObject root)
        {
            yield return root;
            for (int i = 0; i < root.transform.childCount; i++)
            {
                foreach (GameObject child in Flatten(root.transform.GetChild(i).gameObject))
                {
                    yield return child;
                }
            }
        }

        private static T Asset<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            if (AssetDatabase.LoadMainAssetAtPath(path) != null || System.IO.File.Exists(path))
            {
                AssetDatabase.DeleteAsset(path);
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void Set(object target, string field, object value)
        {
            FieldInfo info = target?.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (info == null)
            {
                throw new MissingFieldException(target?.GetType().FullName, field);
            }

            info.SetValue(target, value);
        }

        private static void Dirty(UnityEngine.Object asset)
        {
            EditorUtility.SetDirty(asset);
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/_Project/Content/Items", "Crafting");
            EnsureFolder(Root, "Materials");
            EnsureFolder(Root, "Affixes");
            EnsureFolder(Root, "Production");
            EnsureFolder(Root, "Recipes");
            EnsureFolder(Root, "Experiments");
            EnsureFolder(Root, "Policies");
            EnsureFolder(Root, "Catalysts");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
