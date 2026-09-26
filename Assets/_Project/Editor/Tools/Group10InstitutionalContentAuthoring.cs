using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.Crimes;
using UnityIsekaiGame.Diplomacy;
using UnityIsekaiGame.Factions;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Governments;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Justice;
using UnityIsekaiGame.Laws;
using UnityIsekaiGame.Organizations;
using UnityIsekaiGame.People;

namespace UnityIsekaiGame.Editor
{
    public static class Group10InstitutionalContentAuthoring
    {
        private const string Root = "Assets/_Project/Content/Generated/Institutions";

        [MenuItem("Tools/Unity Isekai Game/Phase 3/Author Group 10 Institutional Content")]
        public static void Generate() => GenerateInternal(overwriteExisting: false);

        [MenuItem("Tools/Unity Isekai Game/Phase 3/Reset Group 10 Institutional Content To Defaults")]
        public static void ResetToDefaults() => GenerateInternal(overwriteExisting: true);

        private static void GenerateInternal(bool overwriteExisting)
        {
            EnsureFolder("Assets/_Project/Content", "Generated");
            EnsureFolder("Assets/_Project/Content/Generated", "Institutions");

            DefinitionRegistry registry = new DefinitionRegistry(Array.Empty<IGameDefinition>());
            registry = PrototypeOrganizationDefinitionFactory.AddMissingPrototypeOrganizationDefinitions(registry);
            registry = PrototypeOrganizationMembershipDefinitionFactory.AddMissingPrototypeOrganizationMembershipDefinitions(registry);
            registry = PrototypeOrganizationAuthorityDefinitionFactory.AddMissingPrototypeOrganizationAuthorityDefinitions(registry);
            registry = PrototypeOrganizationResourceDefinitionFactory.AddMissingPrototypeOrganizationResourceDefinitions(registry);
            registry = PrototypeOrganizationDecisionDefinitionFactory.AddMissingPrototypeOrganizationDecisionDefinitions(registry);
            registry = PrototypeFactionDefinitionFactory.AddMissingPrototypeFactionDefinitions(registry);
            registry = PrototypeDiplomacyDefinitionFactory.AddMissingPrototypeDiplomacyDefinitions(registry);
            registry = PrototypeGovernmentDefinitionFactory.AddMissingPrototypeGovernmentDefinitions(registry);
            registry = PrototypeLegalDefinitionFactory.AddMissingPrototypeLegalDefinitions(registry);
            registry = PrototypeCrimeDefinitionFactory.AddMissingPrototypeCrimeDefinitions(registry);
            registry = PrototypeJusticeDefinitionFactory.AddMissingPrototypeJusticeDefinitions(registry);

            List<ScriptableObject> definitions = registry.DefinitionsById.Values.OfType<ScriptableObject>().ToList();
            definitions.Add(Person("person.prototype.mayor", "Prototype Mayor", 46, "Mayor"));
            definitions.Add(Person("person.prototype.guard", "Prototype Guard", 34, "City Guard"));
            definitions.Add(Person("person.prototype.magistrate", "Prototype Magistrate", 49, "Magistrate"));
            definitions.Add(Person("person.prototype.merchant-guildmaster", "Prototype Merchant Guildmaster", 47, "Merchant Guildmaster"));
            definitions.Add(Person("person.prototype.adventurers-guild-receptionist", "Prototype Adventurers Guild Receptionist", 29, "Adventurers Guild Receptionist"));
            definitions.Add(Person("person.prototype.merchant-guild-receptionist", "Prototype Merchant Guild Receptionist", 33, "Merchant Guild Receptionist"));
            definitions.Add(Person("person.prototype.temple-priest", "Prototype Temple Priest", 41, "Priest"));
            definitions.Add(Person("person.prototype.university-headmaster", "Prototype University Headmaster", 56, "Headmaster"));
            definitions.Add(Person("person.prototype.manor-lord", "Prototype Manor Lord", 45, "Manor Lord"));
            definitions.Add(Person("person.prototype.duke", "Prototype Duke", 36, "Duke"));
            definitions.Add(Person("person.prototype.monarch", "Prototype Monarch", 62, "Monarch"));

            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (ScriptableObject generated in definitions.OrderBy(value => ((IGameDefinition)value).Id, StringComparer.Ordinal))
            {
                ScriptableObject authored = CreateAuthoredAsset(generated);
                if (authored != generated) UnityEngine.Object.DestroyImmediate(generated);
                IGameDefinition definition = (IGameDefinition)authored;
                ValidateId(definition.Id);
                if (!ids.Add(definition.Id)) throw new InvalidOperationException($"Duplicate Group 10 definition ID '{definition.Id}'.");

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
                    // Existing assets are the authored source of truth. The ordinary authoring command
                    // may add new defaults, but must never erase Inspector edits.
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
            ConfigureAdventurerGuildRegistrationDesk();
            DefinitionCatalogBuilder.RebuildPrototypeCatalog();
            Debug.Log($"Authored {ids.Count} Group 10 institutional definitions and rebuilt the prototype catalog (overwrite existing: {overwriteExisting}).");
        }

        public static void GenerateBatch() => Generate();

        public static void ValidateBatch()
        {
            string[] paths = AssetDatabase.FindAssets("t:ScriptableObject", new[] { Root })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            int valid = 0;
            foreach (string path in paths)
            {
                ScriptableObject definition = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (definition is not IGameDefinition gameDefinition || string.IsNullOrWhiteSpace(gameDefinition.Id))
                {
                    throw new InvalidOperationException($"Generated institutional definition at '{path}' cannot be loaded in a clean editor session.");
                }
                valid++;
            }
            if (valid < 300) throw new InvalidOperationException($"Only {valid} generated institutional definitions could be reloaded.");
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>("Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset");
            DefinitionValidationReport report = DefinitionCatalogValidator.Validate(catalog);
            if (report.ErrorCount > 0 || report.WarningCount > 0)
            {
                throw new InvalidOperationException($"Group 10 catalog validation reported {report.ErrorCount} error(s) and {report.WarningCount} warning(s):\n{report.GetSummary()}");
            }
            Debug.Log($"Validated {valid} persistent Group 10 institutional definition assets in a clean editor session.");
        }

        private static PersonDefinition Person(string id, string displayName, int age, string role)
        {
            PersonDefinition definition = ScriptableObject.CreateInstance<PersonDefinition>();
            definition.DevelopmentConfigure(id, displayName, age, PersonLifeStage.Adult, PersonImportance.Standard, role);
            return definition;
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
                throw new InvalidOperationException($"Institutional definition ID '{id}' is not normalized or is still a placeholder.");
            }
        }

        private static string Sanitize(string id) => new string((id ?? string.Empty).Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray());

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
        }

        private static void ConfigureAdventurerGuildRegistrationDesk()
        {
            const string prefabPath = "Assets/_Project/Prototype/Prefabs/Buildings/PrototypeAdventurerGuild/AdventurerGuild.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Transform counter = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(value => value.name == "AdventurerGuildCounter");
                if (counter == null) throw new InvalidOperationException($"AdventurerGuildCounter was not found in '{prefabPath}'.");
                if (counter.GetComponent<PrototypeAdventurerGuildRegistrationDesk>() == null) counter.gameObject.AddComponent<PrototypeAdventurerGuildRegistrationDesk>();
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
