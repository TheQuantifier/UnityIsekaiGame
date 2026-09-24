using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Inventory.Durability;
using UnityIsekaiGame.Inventory.Quality;

namespace UnityIsekaiGame.Editor
{
    public static class DefinitionCatalogBuilder
    {
        private const string CatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";
        private const string GeneratedRoot = "Assets/_Project/Content/Generated/GameData";

        [MenuItem("Tools/Unity Isekai Game/Game Data/Rebuild Prototype Catalog")]
        public static void RebuildPrototypeCatalog()
        {
            EnsureFolder("Assets/_Project/Content", "Generated");
            EnsureFolder("Assets/_Project/Content/Generated", "GameData");

            QualityTierDefinition[] tiers = CreateQualityTiers();
            ItemConditionScaleDefinition scale = CreateConditionScale();
            CreateItemDegradationPolicy();
            GameDataDefaultsDefinition defaults = CreateDefaults(tiers, scale);
            foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/_Project/Content", "Assets/_Project/Prototype/Content" }))
            {
                ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                item?.PruneInactiveCapabilities();
                if (item != null) EditorUtility.SetDirty(item);
            }
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            if (catalog == null) throw new InvalidOperationException($"Definition catalog not found at {CatalogPath}.");

            string[] roots = { "Assets/_Project/Content", "Assets/_Project/Prototype/Content" };
            List<ScriptableObject> assets = AssetDatabase.FindAssets("t:ScriptableObject", roots)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.Equals(path, CatalogPath, StringComparison.Ordinal))
                .Select(AssetDatabase.LoadAssetAtPath<ScriptableObject>)
                .Where(asset => asset is IGameDefinition)
                .OrderBy(asset => ((IGameDefinition)asset).Id, StringComparer.Ordinal)
                .ThenBy(asset => AssetDatabase.GetAssetPath(asset), StringComparer.Ordinal)
                .ToList();

            var groups = assets.GroupBy(asset => SectionId((IGameDefinition)asset), StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .ToArray();
            SerializedObject serialized = new SerializedObject(catalog);
            serialized.FindProperty("contentVersion").stringValue = "phase-3.group-6.items-crafting-production";
            serialized.FindProperty("defaults").objectReferenceValue = defaults;
            SerializedProperty sections = serialized.FindProperty("sections");
            sections.arraySize = groups.Length;
            for (int i = 0; i < groups.Length; i++)
            {
                SerializedProperty section = sections.GetArrayElementAtIndex(i);
                section.FindPropertyRelative("domainId").stringValue = groups[i].Key;
                ScriptableObject[] values = groups[i].ToArray();
                SerializedProperty definitions = section.FindPropertyRelative("definitions");
                definitions.arraySize = values.Length;
                for (int j = 0; j < values.Length; j++) definitions.GetArrayElementAtIndex(j).objectReferenceValue = values[j];
            }

            string manifest = string.Join("\n", assets.Select(asset => $"{((IGameDefinition)asset).Id}|{AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset))}"));
            using SHA256 sha = SHA256.Create();
            serialized.FindProperty("contentHash").stringValue = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(manifest))).Replace("-", string.Empty).ToLowerInvariant();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"Rebuilt {catalog.name}: {assets.Count} definitions in {groups.Length} deterministic sections ({catalog.ContentHash}).");
        }

        public static void RebuildAndValidateBatch()
        {
            RebuildPrototypeCatalog();
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            DefinitionValidationReport report = DefinitionCatalogValidator.Validate(catalog);
            foreach (DefinitionIdValidationMessage message in report.Messages) Debug.Log($"[{message.Severity}] {message.Message}");
            if (report.ErrorCount > 0) throw new InvalidOperationException($"Catalog validation failed with {report.ErrorCount} error(s).");
        }

        private static QualityTierDefinition[] CreateQualityTiers()
        {
            return new[]
            {
                CreateQualityTier("Ruined", "quality.ruined", 0f, 0.2f, 0),
                CreateQualityTier("Poor", "quality.poor", 0.2f, 0.35f, 10),
                CreateQualityTier("Common", "quality.common", 0.35f, 0.55f, 20),
                CreateQualityTier("Serviceable", "quality.serviceable", 0.55f, 0.7f, 30),
                CreateQualityTier("Fine", "quality.fine", 0.7f, 0.85f, 40),
                CreateQualityTier("Masterwork", "quality.masterwork", 0.85f, 0.95f, 50),
                CreateQualityTier("Legendary", "quality.legendary", 0.95f, 1f, 60)
            };
        }

        private static QualityTierDefinition CreateQualityTier(string name, string id, float min, float max, int order)
        {
            string path = $"{GeneratedRoot}/{name}QualityTier.asset";
            QualityTierDefinition asset = AssetDatabase.LoadAssetAtPath<QualityTierDefinition>(path);
            if (asset == null) { asset = ScriptableObject.CreateInstance<QualityTierDefinition>(); AssetDatabase.CreateAsset(asset, path); }
            SerializedObject serialized = new SerializedObject(asset);
            serialized.FindProperty("tierId").stringValue = id;
            serialized.FindProperty("displayName").stringValue = name;
            serialized.FindProperty("minimumQuality").floatValue = min;
            serialized.FindProperty("maximumQuality").floatValue = max;
            serialized.FindProperty("sortOrder").intValue = order;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static ItemConditionScaleDefinition CreateConditionScale()
        {
            string path = $"{GeneratedRoot}/StandardItemConditionScale.asset";
            ItemConditionScaleDefinition asset = AssetDatabase.LoadAssetAtPath<ItemConditionScaleDefinition>(path);
            if (asset == null) { asset = ScriptableObject.CreateInstance<ItemConditionScaleDefinition>(); AssetDatabase.CreateAsset(asset, path); }
            var bands = new[]
            {
                Band("condition.destroyed", "Destroyed", 0f, 0.0001f, 0f, ItemFunctionalState.Destroyed, ItemBreakageState.Destroyed),
                Band("condition.near-failure", "Near Failure", 0.0001f, 0.1f, 0.1f, ItemFunctionalState.PartiallyDisabled, ItemBreakageState.Major),
                Band("condition.severely-damaged", "Severely Damaged", 0.1f, 0.25f, 0.25f, ItemFunctionalState.PartiallyDisabled, ItemBreakageState.Major),
                Band("condition.damaged", "Damaged", 0.25f, 0.5f, 0.5f, ItemFunctionalState.Impaired, ItemBreakageState.Minor),
                Band("condition.worn", "Worn", 0.5f, 0.7f, 1f, ItemFunctionalState.FullyFunctional, ItemBreakageState.None),
                Band("condition.used", "Used", 0.7f, 0.85f, 1f, ItemFunctionalState.FullyFunctional, ItemBreakageState.None),
                Band("condition.good", "Good", 0.85f, 0.95f, 1f, ItemFunctionalState.FullyFunctional, ItemBreakageState.None),
                Band("condition.pristine", "Pristine", 0.95f, 1f, 1f, ItemFunctionalState.FullyFunctional, ItemBreakageState.None)
            };
            SerializedObject serialized = new SerializedObject(asset);
            serialized.FindProperty("scaleId").stringValue = "condition.scale.standard";
            serialized.FindProperty("displayName").stringValue = "Standard Item Condition";
            SerializedProperty values = serialized.FindProperty("bands");
            values.arraySize = bands.Length;
            for (int i = 0; i < bands.Length; i++)
            {
                SerializedProperty value = values.GetArrayElementAtIndex(i);
                ItemConditionBandData band = bands[i];
                value.FindPropertyRelative("bandId").stringValue = band.bandId;
                value.FindPropertyRelative("displayName").stringValue = band.displayName;
                value.FindPropertyRelative("minimumNormalized").floatValue = band.minimumNormalized;
                value.FindPropertyRelative("maximumNormalized").floatValue = band.maximumNormalized;
                value.FindPropertyRelative("equipmentContribution").floatValue = band.equipmentContribution;
                value.FindPropertyRelative("functionalState").enumValueIndex = (int)band.functionalState;
                value.FindPropertyRelative("breakageState").enumValueIndex = (int)band.breakageState;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static GameDataDefaultsDefinition CreateDefaults(QualityTierDefinition[] tiers, ItemConditionScaleDefinition scale)
        {
            string path = $"{GeneratedRoot}/GameDataDefaults.asset";
            GameDataDefaultsDefinition asset = AssetDatabase.LoadAssetAtPath<GameDataDefaultsDefinition>(path);
            if (asset == null) { asset = ScriptableObject.CreateInstance<GameDataDefaultsDefinition>(); AssetDatabase.CreateAsset(asset, path); }
            RarityDefinition rarity = AssetDatabase.LoadAssetAtPath<RarityDefinition>("Assets/_Project/Content/Items/Rarities/CommonRarity.asset");
            SerializedObject serialized = new SerializedObject(asset);
            serialized.FindProperty("defaultRarity").objectReferenceValue = rarity;
            serialized.FindProperty("defaultQualityTier").objectReferenceValue = tiers.First(value => value.Id == "quality.common");
            serialized.FindProperty("itemConditionScale").objectReferenceValue = scale;
            serialized.FindProperty("defaultQualityNormalized").floatValue = 0.5f;
            serialized.FindProperty("initialDurabilityNormalized").floatValue = 1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static ItemDegradationPolicyDefinition CreateItemDegradationPolicy()
        {
            string path = $"{GeneratedRoot}/StandardItemDegradationPolicy.asset";
            ItemDegradationPolicyDefinition asset = AssetDatabase.LoadAssetAtPath<ItemDegradationPolicyDefinition>(path);
            if (asset == null) { asset = ScriptableObject.CreateInstance<ItemDegradationPolicyDefinition>(); AssetDatabase.CreateAsset(asset, path); }
            SerializedObject serialized = new SerializedObject(asset);
            serialized.FindProperty("policyId").stringValue = ItemDegradationPolicyDefinition.StandardPolicyId;
            serialized.FindProperty("displayName").stringValue = "Standard Item Degradation";
            serialized.FindProperty("fastDecompositionThreshold").floatValue = ItemDegradationPolicyDefinition.StandardFastThreshold;
            serialized.FindProperty("immediateDecompositionThreshold").floatValue = ItemDegradationPolicyDefinition.StandardImmediateThreshold;
            serialized.FindProperty("slowDecompositionSeconds").floatValue = ItemDegradationPolicyDefinition.StandardSlowDurationSeconds;
            serialized.FindProperty("fastDecompositionSeconds").floatValue = ItemDegradationPolicyDefinition.StandardFastDurationSeconds;
            serialized.FindProperty("brokenWorldItemsContinueDecay").boolValue = true;
            ItemBreakChanceEntryData[] breakChances = ItemDegradationPolicyDefinition.CreateStandardBreakChances();
            SerializedProperty values = serialized.FindProperty("breakChances");
            values.arraySize = breakChances.Length;
            for (int i = 0; i < breakChances.Length; i++)
            {
                SerializedProperty value = values.GetArrayElementAtIndex(i);
                value.FindPropertyRelative("durabilityPercent").intValue = breakChances[i].durabilityPercent;
                value.FindPropertyRelative("breakChance").floatValue = breakChances[i].breakChance;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static ItemConditionBandData Band(string id, string name, float min, float max, float contribution, ItemFunctionalState functional, ItemBreakageState breakage)
        {
            return new ItemConditionBandData { bandId = id, displayName = name, minimumNormalized = min, maximumNormalized = max, equipmentContribution = contribution, functionalState = functional, breakageState = breakage };
        }

        private static string SectionId(IGameDefinition definition)
        {
            string id = definition?.Id ?? "definition.unknown";
            int separator = id.IndexOf('.');
            return $"domain.{(separator > 0 ? id.Substring(0, separator) : "general")}";
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
        }
    }
}
