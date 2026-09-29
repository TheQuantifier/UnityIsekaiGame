using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityIsekaiGame.Dialogue;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Narrative;
using UnityIsekaiGame.Quests;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Organizations;
using UnityIsekaiGame.PrototypeIntegration;
using UnityIsekaiGame.WorldLocations;
using UnityIsekaiGame.WorldLocations.SceneBinding;

namespace UnityIsekaiGame.Editor
{
    public static class Group12NarrativeContentAuthoring
    {
        private const string Root = "Packages/com.thequantifier.isekai.content/Content/Generated/Narrative";
        private const string AdventurerGuildPrefab = "Assets/_Project/Prototype/Prefabs/Buildings/PrototypeAdventurerGuild/AdventurerGuild.prefab";

        [MenuItem("Tools/Unity Isekai Game/Phase 3/Author Group 12 Narrative Content")]
        public static void Generate() => GenerateInternal(false);

        [MenuItem("Tools/Unity Isekai Game/Phase 3/Reset Group 12 Narrative Content To Defaults")]
        public static void ResetToDefaults() => GenerateInternal(true);

        public static void GenerateBatch() => Generate();
        public static void ResetBatch() => ResetToDefaults();

        public static void ValidateBatch()
        {
            string[] paths = AssetDatabase.FindAssets("t:ScriptableObject", new[] { Root }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            if (paths.Length < 41) throw new InvalidOperationException($"Only {paths.Length} Group 12 narrative definition assets could be reloaded.");
            foreach (string path in paths)
            {
                if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(path) is not IGameDefinition definition || string.IsNullOrWhiteSpace(definition.Id))
                    throw new InvalidOperationException($"Narrative definition at '{path}' is invalid.");
            }
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>("Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset");
            DefinitionValidationReport report = DefinitionCatalogValidator.Validate(catalog);
            if (report.ErrorCount > 0 || report.WarningCount > 0)
                throw new InvalidOperationException($"Group 12 catalog validation reported {report.ErrorCount} error(s) and {report.WarningCount} warning(s):\n{report.GetSummary()}");
            ValidatePrototypeScene();
            Debug.Log($"Validated {paths.Length} persistent Group 12 narrative definition assets and the Prototype scene bindings.");
        }

        private static void GenerateInternal(bool overwriteExisting)
        {
            EnsureFolder("Packages/com.thequantifier.isekai.content/Content", "Generated");
            EnsureFolder("Packages/com.thequantifier.isekai.content/Content/Generated", "Narrative");
            DefinitionRegistry registry = new DefinitionRegistry(Array.Empty<IGameDefinition>());
            registry = PrototypeQuestDefinitionFactory.AddMissingPrototypeQuestDefinitions(registry);
            registry = PrototypeQuestSourceDefinitionFactory.AddMissingPrototypeQuestSourceDefinitions(registry);
            registry = PrototypeConversationDefinitionFactory.AddMissingPrototypeConversationDefinitions(registry);
            registry = PrototypeDialogueGraphDefinitionFactory.AddMissingPrototypeDialogueGraphDefinitions(registry);
            registry = PrototypeNarrativeEventDefinitionFactory.AddMissingPrototypeNarrativeEventDefinitions(registry);
            registry = PrototypeNarrativeStateDefinitionFactory.AddMissingPrototypeNarrativeStateDefinitions(registry);
            registry = PrototypeNarrativeArcDefinitionFactory.AddMissingPrototypeNarrativeArcDefinitions(registry);

            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (ScriptableObject generated in registry.DefinitionsById.Values.OfType<ScriptableObject>().OrderBy(value => ((IGameDefinition)value).Id, StringComparer.Ordinal))
            {
                IGameDefinition definition = (IGameDefinition)generated;
                ValidateId(definition.Id);
                if (!ids.Add(definition.Id)) throw new InvalidOperationException($"Duplicate Group 12 definition ID '{definition.Id}'.");
                string path = $"{Root}/{Sanitize(definition.Id)}.asset";
                ScriptableObject existing = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (existing != null && existing.GetType() != generated.GetType())
                {
                    AssetDatabase.DeleteAsset(path);
                    existing = null;
                }
                if (existing == null && !string.IsNullOrWhiteSpace(AssetDatabase.AssetPathToGUID(path))) AssetDatabase.DeleteAsset(path);
                if (existing == null)
                {
                    generated.name = Sanitize(definition.Id);
                    AssetDatabase.CreateAsset(generated, path);
                }
                else if (overwriteExisting)
                {
                    EditorUtility.CopySerialized(generated, existing);
                    existing.name = Sanitize(definition.Id);
                    EditorUtility.SetDirty(existing);
                    UnityEngine.Object.DestroyImmediate(generated);
                }
                else UnityEngine.Object.DestroyImmediate(generated);
            }

            EnsureMerchantParcel(ids, overwriteExisting);
            ConfigurePrototypeNarrativeBindings();

            foreach (string path in AssetDatabase.FindAssets("t:ScriptableObject", new[] { Root }).Select(AssetDatabase.GUIDToAssetPath))
            {
                ScriptableObject asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (asset is not IGameDefinition definition || !ids.Contains(definition.Id)) AssetDatabase.DeleteAsset(path);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            DefinitionCatalogBuilder.RebuildPrototypeCatalog();
            Debug.Log($"Authored {ids.Count} Group 12 narrative definitions and rebuilt the prototype catalog (overwrite existing: {overwriteExisting}).");
        }

        private static void ValidateId(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Any(char.IsWhiteSpace) || !id.Contains('.') || id.IndexOf("placeholder", StringComparison.OrdinalIgnoreCase) >= 0)
                throw new InvalidOperationException($"Narrative definition ID '{id}' is not normalized or is still a placeholder.");
        }

        private static string Sanitize(string id) => new string((id ?? string.Empty).Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray());

        private static void EnsureMerchantParcel(ISet<string> ids, bool overwriteExisting)
        {
            const string id = "item.prototype.merchant-parcel";
            const string path = Root + "/item_prototype_merchant_parcel.asset";
            ids.Add(id);
            ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ItemDefinition>();
                item.name = "item_prototype_merchant_parcel";
                AssetDatabase.CreateAsset(item, path);
            }
            if (!overwriteExisting && item.Id == id) return;
            SerializedObject serialized = new SerializedObject(item);
            serialized.FindProperty("itemId").stringValue = id;
            serialized.FindProperty("displayName").stringValue = "Sealed Merchant Parcel";
            serialized.FindProperty("description").stringValue = "A sealed parcel entrusted to the carrier for delivery at the merchant counter.";
            serialized.FindProperty("primaryCategory").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CategoryDefinition>("Packages/com.thequantifier.isekai.content/Content/Core/Categories/ItemQuestItemCategory.asset");
            serialized.FindProperty("rarity").objectReferenceValue = AssetDatabase.LoadAssetAtPath<RarityDefinition>("Packages/com.thequantifier.isekai.content/Content/Items/Rarities/CommonRarity.asset");
            serialized.FindProperty("instanceMode").enumValueIndex = (int)ItemInstanceMode.DefinitionOnly;
            serialized.FindProperty("stackable").boolValue = false;
            serialized.FindProperty("maximumStackSize").intValue = 1;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
        }

        private static void ValidatePrototypeScene()
        {
            const string scenePath = "Assets/_Project/Scenes/Prototype/PrototypeScene.unity";
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            GameObject[] roots = scene.GetRootGameObjects();
            int missingScripts = roots.Sum(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount);
            if (missingScripts > 0) throw new InvalidOperationException($"Prototype scene contains {missingScripts} missing script component(s).");

            QuestSourceSceneBinding[] sources = roots.SelectMany(root => root.GetComponentsInChildren<QuestSourceSceneBinding>(true)).ToArray();
            if (sources.Length < 5) throw new InvalidOperationException($"Prototype scene only exposes {sources.Length} quest-source binding(s); at least five are required.");
            foreach (QuestSourceSceneBinding source in sources)
            {
                if (source.GetComponent<InteractionPointSceneBinding>() == null)
                    throw new InvalidOperationException($"Quest source '{source.name}' has no authoritative InteractionPointSceneBinding.");
                if (string.IsNullOrWhiteSpace(source.QuestSourceId) || string.IsNullOrWhiteSpace(source.QuestSourceDefinitionId))
                    throw new InvalidOperationException($"Quest source '{source.name}' has incomplete IDs.");
            }

            int dialogueSources = sources.Count(source => source.OpensConversation);
            if (dialogueSources < 4) throw new InvalidOperationException($"Prototype scene only exposes {dialogueSources} dialogue-enabled institutional quest source(s); four are required.");
            ConversationSceneBinding[] dialogue = roots.SelectMany(root => root.GetComponentsInChildren<ConversationSceneBinding>(true)).ToArray();
            if (dialogue.Length < 2) throw new InvalidOperationException($"Prototype scene only exposes {dialogue.Length} standalone conversation binding(s); two are required.");
            foreach (ConversationSceneBinding binding in dialogue)
            {
                if (binding.GetComponent<InteractionPointSceneBinding>() == null || string.IsNullOrWhiteSpace(binding.ConversationDefinitionId) || string.IsNullOrWhiteSpace(binding.ProviderPersonId))
                    throw new InvalidOperationException($"Conversation binding '{binding.name}' is incomplete or has no authoritative interaction point.");
            }
        }

        private static void ConfigurePrototypeNarrativeBindings()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(AdventurerGuildPrefab);
            if (root == null) throw new InvalidOperationException($"Could not load '{AdventurerGuildPrefab}'.");
            try
            {
                foreach (QuestSourceSceneBinding source in root.GetComponentsInChildren<QuestSourceSceneBinding>(true))
                {
                    switch (source.InteractionPointId)
                    {
                        case PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterPointId:
                            source.ConfigureConversation(PrototypeConversationDefinitionFactory.AdventurerGuildCounterDefinitionId, PrototypeEntityLocationFactory.AdventurersGuildReceptionistPersonId);
                            break;
                        case PrototypeInteractionPointDefinitionFactory.MerchantGuildCounterPointId:
                            source.ConfigureConversation(PrototypeConversationDefinitionFactory.MerchantGuildCounterDefinitionId, PrototypeEntityLocationFactory.MerchantGuildReceptionistPersonId);
                            break;
                        case PrototypeInteractionPointDefinitionFactory.MayorDeskPointId:
                            source.ConfigureConversation(PrototypeConversationDefinitionFactory.MayorDeskDefinitionId, PrototypeInstitutionalContentIds.MayorPerson);
                            break;
                        case PrototypeInteractionPointDefinitionFactory.RecordsDeskPointId:
                            source.ConfigureConversation(PrototypeConversationDefinitionFactory.RecordsDeskDefinitionId, PrototypeEntityLocationFactory.RecordsClerkPersonId);
                            break;
                        default:
                            source.ConfigureConversation(string.Empty, string.Empty);
                            break;
                    }
                    EditorUtility.SetDirty(source);
                }

                foreach (InteractionPointSceneBinding point in root.GetComponentsInChildren<InteractionPointSceneBinding>(true))
                {
                    string definitionId = string.Empty;
                    string providerId = string.Empty;
                    string locationId = string.Empty;
                    string speaker = string.Empty;
                    if (point.LogicalId == PrototypeInteractionPointDefinitionFactory.GuildHeadDeskPointId)
                    {
                        definitionId = PrototypeConversationDefinitionFactory.GuildHeadOfficeDefinitionId;
                        providerId = PrototypeEntityLocationFactory.GuildMasterPersonId;
                        locationId = "location.prototype.guildmaster-office";
                        speaker = "Guildmaster";
                    }
                    else if (point.LogicalId == PrototypeInteractionPointDefinitionFactory.PrisonCellPointId)
                    {
                        definitionId = PrototypeConversationDefinitionFactory.PrisonerInterviewDefinitionId;
                        providerId = PrototypeEntityLocationFactory.PrisonerPersonId;
                        locationId = "location.prototype.basement-prison";
                        speaker = "Prisoner";
                    }
                    if (string.IsNullOrWhiteSpace(definitionId)) continue;
                    ConversationSceneBinding binding = point.GetComponent<ConversationSceneBinding>() ?? point.gameObject.AddComponent<ConversationSceneBinding>();
                    binding.Configure(definitionId, providerId, locationId, speaker);
                    EditorUtility.SetDirty(binding);
                }

                PrefabUtility.SaveAsPrefabAsset(root, AdventurerGuildPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
        }

    }
}
