using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory.Durability;
using UnityIsekaiGame.Inventory.Quality;

namespace UnityIsekaiGame.Tests
{
    internal static class ClassificationTestFactory
    {
        public static CategoryDefinition CreateCategory(
            string id,
            string displayName,
            CategoryDomain domain = CategoryDomain.General,
            CategoryDefinition parentCategory = null)
        {
            CategoryDefinition category = ScriptableObject.CreateInstance<CategoryDefinition>();
            SerializedObject serializedCategory = new SerializedObject(category);
            serializedCategory.FindProperty("categoryId").stringValue = id;
            serializedCategory.FindProperty("displayName").stringValue = displayName;
            serializedCategory.FindProperty("domain").enumValueIndex = (int)domain;
            serializedCategory.FindProperty("parentCategory").objectReferenceValue = parentCategory;
            serializedCategory.ApplyModifiedPropertiesWithoutUndo();
            return category;
        }

        public static TagDefinition CreateTag(
            string id,
            string displayName,
            CategoryDomain domain = CategoryDomain.General)
        {
            TagDefinition tag = ScriptableObject.CreateInstance<TagDefinition>();
            SerializedObject serializedTag = new SerializedObject(tag);
            serializedTag.FindProperty("tagId").stringValue = id;
            serializedTag.FindProperty("displayName").stringValue = displayName;
            serializedTag.FindProperty("domain").enumValueIndex = (int)domain;
            serializedTag.ApplyModifiedPropertiesWithoutUndo();
            return tag;
        }

        public static RarityDefinition CreateRarity(
            string id,
            string displayName,
            int rank,
            bool isDefault = false)
        {
            RarityDefinition rarity = ScriptableObject.CreateInstance<RarityDefinition>();
            SerializedObject serializedRarity = new SerializedObject(rarity);
            serializedRarity.FindProperty("rarityId").stringValue = id;
            serializedRarity.FindProperty("displayName").stringValue = displayName;
            serializedRarity.FindProperty("rank").intValue = rank;
            serializedRarity.FindProperty("defaultRarity").boolValue = isDefault;
            serializedRarity.ApplyModifiedPropertiesWithoutUndo();
            return rarity;
        }

        public static QualityTierDefinition CreateQualityTier(string id, string displayName, float minimum, float maximum, int sortOrder)
        {
            QualityTierDefinition quality = ScriptableObject.CreateInstance<QualityTierDefinition>();
            SerializedObject serializedQuality = new SerializedObject(quality);
            serializedQuality.FindProperty("tierId").stringValue = id;
            serializedQuality.FindProperty("displayName").stringValue = displayName;
            serializedQuality.FindProperty("minimumQuality").floatValue = minimum;
            serializedQuality.FindProperty("maximumQuality").floatValue = maximum;
            serializedQuality.FindProperty("sortOrder").intValue = sortOrder;
            serializedQuality.ApplyModifiedPropertiesWithoutUndo();
            return quality;
        }

        public static ItemConditionScaleDefinition CreateConditionScale(params ItemConditionBandData[] bands)
        {
            ItemConditionScaleDefinition scale = ScriptableObject.CreateInstance<ItemConditionScaleDefinition>();
            SerializedObject serialized = new SerializedObject(scale);
            SerializedProperty values = serialized.FindProperty("bands");
            values.arraySize = bands.Length;
            for (int i = 0; i < bands.Length; i++)
            {
                SerializedProperty value = values.GetArrayElementAtIndex(i);
                value.FindPropertyRelative("bandId").stringValue = bands[i].bandId;
                value.FindPropertyRelative("displayName").stringValue = bands[i].displayName;
                value.FindPropertyRelative("minimumNormalized").floatValue = bands[i].minimumNormalized;
                value.FindPropertyRelative("maximumNormalized").floatValue = bands[i].maximumNormalized;
                value.FindPropertyRelative("equipmentContribution").floatValue = bands[i].equipmentContribution;
                value.FindPropertyRelative("functionalState").enumValueIndex = (int)bands[i].functionalState;
                value.FindPropertyRelative("breakageState").enumValueIndex = (int)bands[i].breakageState;
                value.FindPropertyRelative("salvageEligible").boolValue = bands[i].salvageEligible;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return scale;
        }

        public static DefinitionCatalog CreateCatalog(params ScriptableObject[] definitions)
        {
            DefinitionCatalog catalog = ScriptableObject.CreateInstance<DefinitionCatalog>();
            SerializedObject serializedCatalog = new SerializedObject(catalog);
            serializedCatalog.FindProperty("catalogId").stringValue = "catalog.test";
            SerializedProperty sections = serializedCatalog.FindProperty("sections");
            sections.arraySize = 1;
            SerializedProperty section = sections.GetArrayElementAtIndex(0);
            section.FindPropertyRelative("domainId").stringValue = "domain.test";
            SerializedProperty definitionsProperty = section.FindPropertyRelative("definitions");
            definitionsProperty.arraySize = definitions.Length;

            for (int i = 0; i < definitions.Length; i++)
            {
                definitionsProperty.GetArrayElementAtIndex(i).objectReferenceValue = definitions[i];
            }

            serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
            return catalog;
        }
    }
}
