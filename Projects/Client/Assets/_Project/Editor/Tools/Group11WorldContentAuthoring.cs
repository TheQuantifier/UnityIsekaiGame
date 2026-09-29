using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.People;
using UnityIsekaiGame.WorldLocations;

namespace UnityIsekaiGame.Editor
{
    public static class Group11WorldContentAuthoring
    {
        private const string Root = "Packages/com.thequantifier.isekai.content/Content/Generated/WorldLocations";

        [MenuItem("Tools/Unity Isekai Game/Phase 3/Author Group 11 World Content")]
        public static void Generate() => GenerateInternal(overwriteExisting: false);

        [MenuItem("Tools/Unity Isekai Game/Phase 3/Reset Group 11 World Content To Defaults")]
        public static void ResetToDefaults() => GenerateInternal(overwriteExisting: true);

        public static void GenerateBatch() => Generate();

        public static void ValidateBatch()
        {
            string[] paths = AssetDatabase.FindAssets("t:ScriptableObject", new[] { Root })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            foreach (string path in paths)
            {
                if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(path) is not IGameDefinition definition
                    || string.IsNullOrWhiteSpace(definition.Id))
                {
                    throw new InvalidOperationException($"Generated world definition at '{path}' cannot be loaded in a clean editor session.");
                }
            }

            if (paths.Length < 40)
            {
                throw new InvalidOperationException($"Only {paths.Length} Group 11 world definitions could be reloaded.");
            }

            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>("Packages/com.thequantifier.isekai.content/Content/Prototype/GameData/PrototypeDefinitionCatalog.asset");
            DefinitionValidationReport report = DefinitionCatalogValidator.Validate(catalog);
            if (report.ErrorCount > 0 || report.WarningCount > 0)
            {
                throw new InvalidOperationException($"Group 11 catalog validation reported {report.ErrorCount} error(s) and {report.WarningCount} warning(s):\n{report.GetSummary()}");
            }

            Debug.Log($"Validated {paths.Length} persistent Group 11 world definition assets in a clean editor session.");
        }

        private static void GenerateInternal(bool overwriteExisting)
        {
            EnsureFolder("Packages/com.thequantifier.isekai.content/Content", "Generated");
            EnsureFolder("Packages/com.thequantifier.isekai.content/Content/Generated", "WorldLocations");

            DefinitionRegistry registry = new DefinitionRegistry(Array.Empty<IGameDefinition>());
            registry = PrototypeLocationDefinitionFactory.AddMissingPrototypeLocationDefinitions(registry);
            registry = PrototypeInteractionPointDefinitionFactory.AddMissingPrototypeInteractionDefinitions(registry);
            registry = PrototypeLocationConnectionDefinitionFactory.AddMissingPrototypeConnectionDefinitions(registry);
            registry = PrototypeLocationRouteDefinitionFactory.AddMissingPrototypeRouteDefinitions(registry);
            registry = PrototypeTravelConditionDefinitionFactory.AddMissingPrototypeTravelConditionDefinitions(registry);

            List<ScriptableObject> generatedDefinitions = registry.DefinitionsById.Values.OfType<ScriptableObject>().ToList();
            PersonDefinition recordsClerk = ScriptableObject.CreateInstance<PersonDefinition>();
            recordsClerk.DevelopmentConfigure(PrototypeEntityLocationFactory.RecordsClerkPersonId, "Prototype Records Clerk", 38, PersonLifeStage.Adult, PersonImportance.Standard, "Records Clerk");
            generatedDefinitions.Add(recordsClerk);

            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (ScriptableObject generated in generatedDefinitions.OrderBy(value => ((IGameDefinition)value).Id, StringComparer.Ordinal))
            {
                ScriptableObject authored = CreateAuthoredAsset(generated);
                if (authored != generated) UnityEngine.Object.DestroyImmediate(generated);
                IGameDefinition definition = (IGameDefinition)authored;
                ValidateId(definition.Id);
                if (!ids.Add(definition.Id)) throw new InvalidOperationException($"Duplicate Group 11 definition ID '{definition.Id}'.");

                string path = $"{Root}/{Sanitize(definition.Id)}.asset";
                ScriptableObject existing = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (existing != null && existing.GetType() != authored.GetType())
                {
                    AssetDatabase.DeleteAsset(path);
                    existing = null;
                }

                if (existing == null && !string.IsNullOrWhiteSpace(AssetDatabase.AssetPathToGUID(path)))
                {
                    AssetDatabase.DeleteAsset(path);
                }

                if (existing == null)
                {
                    authored.name = Sanitize(definition.Id);
                    AssetDatabase.CreateAsset(authored, path);
                }
                else if (overwriteExisting)
                {
                    EditorUtility.CopySerialized(authored, existing);
                    existing.name = Sanitize(definition.Id);
                    EditorUtility.SetDirty(existing);
                    UnityEngine.Object.DestroyImmediate(authored);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(authored);
                }
            }

            foreach (string path in AssetDatabase.FindAssets("t:ScriptableObject", new[] { Root }).Select(AssetDatabase.GUIDToAssetPath))
            {
                ScriptableObject asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (asset is not IGameDefinition definition || !ids.Contains(definition.Id)) AssetDatabase.DeleteAsset(path);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            DefinitionCatalogBuilder.RebuildPrototypeCatalog();
            Debug.Log($"Authored {ids.Count} Group 11 world definitions and rebuilt the prototype catalog (overwrite existing: {overwriteExisting}).");
        }

        private static ScriptableObject CreateAuthoredAsset(ScriptableObject generated)
        {
            Type generatedType = generated.GetType();
            Type authoredType = generatedType.Assembly.GetType($"{generatedType.Namespace}.Authored{generatedType.Name}");
            if (authoredType == null) return generated;
            ScriptableObject authored = ScriptableObject.CreateInstance(authoredType);
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(generated), authored);
            return authored;
        }

        private static void ValidateId(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Any(char.IsWhiteSpace) || !id.Contains('.') || id.IndexOf("placeholder", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                throw new InvalidOperationException($"World definition ID '{id}' is not normalized or is still a placeholder.");
            }
        }

        private static string Sanitize(string id) => new string((id ?? string.Empty).Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray());

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
        }
    }
}
