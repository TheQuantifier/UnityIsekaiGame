using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Social.Attitudes;
using UnityIsekaiGame.Social.Decisions;
using UnityIsekaiGame.Social.Emotions;
using UnityIsekaiGame.Social.Family;
using UnityIsekaiGame.Social.Influence;
using UnityIsekaiGame.Social.Interactions;
using UnityIsekaiGame.Social.Networks;
using UnityIsekaiGame.Social.Norms;
using UnityIsekaiGame.Social.Relationships;
using UnityIsekaiGame.Social.Reputation;
using UnityIsekaiGame.Social.Rumors;
using UnityIsekaiGame.People;

namespace UnityIsekaiGame.Editor
{
    public static class Group9SocialContentAuthoring
    {
        private const string Root = "Assets/_Project/Content/Generated/Social";

        [MenuItem("Tools/Unity Isekai Game/Phase 3/Author Group 9 Social Content")]
        public static void Generate()
        {
            EnsureFolder("Assets/_Project/Content", "Generated");
            EnsureFolder("Assets/_Project/Content/Generated", "Social");

            List<ScriptableObject> definitions = new List<ScriptableObject>();
            definitions.AddRange(PrototypeRelationshipDefinitionFactory.CreateDefinitions());
            definitions.AddRange(PrototypeAttitudeDefinitionFactory.CreateDefinitions());
            definitions.AddRange(PrototypeReputationDefinitionFactory.CreateDefinitions());
            definitions.AddRange(PrototypeRumorDefinitionFactory.CreateDefinitions());
            definitions.AddRange(PrototypeSocialInteractionDefinitionFactory.CreateDefinitions());
            definitions.AddRange(PrototypeSocialNormDefinitionFactory.CreateDefinitions());
            definitions.AddRange(PrototypeSocialNetworkDefinitionFactory.CreateDefinitions().OfType<ScriptableObject>());
            definitions.AddRange(PrototypeSocialDecisionDefinitionFactory.CreateDefinitions());
            definitions.AddRange(PrototypeSocialInfluenceDefinitionFactory.CreateDefinitions());
            definitions.AddRange(PrototypeSocialEmotionDefinitionFactory.CreateDefinitions());
            definitions.AddRange(PrototypeFamilyRelationshipDefinitionFactory.CreateDefinitions());
            definitions.Add(Person("person.prototype.prisoner", "Prototype Prisoner", 31, "Prisoner"));
            definitions.Add(Person("person.prototype.merchant", "Merchant Clerk", 38, "Merchant"));
            definitions.Add(Person("person.prototype.guildmaster", "Guild Master", 52, "Guild Master"));

            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (ScriptableObject generated in definitions.Where(value => value is IGameDefinition).OrderBy(value => ((IGameDefinition)value).Id, StringComparer.Ordinal))
            {
                IGameDefinition definition = (IGameDefinition)generated;
                ValidateId(definition.Id);
                if (!ids.Add(definition.Id))
                {
                    throw new InvalidOperationException($"Duplicate Group 9 definition ID '{definition.Id}'.");
                }

                string path = $"{Root}/{Sanitize(definition.Id)}.asset";
                ScriptableObject existing = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (existing != null && existing.GetType() != generated.GetType())
                {
                    AssetDatabase.DeleteAsset(path);
                    existing = null;
                }

                // A ScriptableObject whose class was moved into its own Unity script can
                // remain as a missing-script asset until it is authored again. Replace it
                // instead of letting CreateAsset fail against the stale path.
                if (existing == null && !string.IsNullOrWhiteSpace(AssetDatabase.AssetPathToGUID(path)))
                {
                    AssetDatabase.DeleteAsset(path);
                }

                if (existing == null)
                {
                    generated.name = Sanitize(definition.Id);
                    AssetDatabase.CreateAsset(generated, path);
                }
                else
                {
                    EditorUtility.CopySerialized(generated, existing);
                    existing.name = Sanitize(definition.Id);
                    EditorUtility.SetDirty(existing);
                    UnityEngine.Object.DestroyImmediate(generated);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            DefinitionCatalogBuilder.RebuildPrototypeCatalog();
            Debug.Log($"Authored {ids.Count} Group 9 social definition assets and rebuilt the prototype catalog.");
        }

        private static void ValidateId(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Any(char.IsWhiteSpace) || !id.Contains('.'))
            {
                throw new InvalidOperationException($"Social definition ID '{id}' must use the normalized <main-category>.<sub-category> form and contain no whitespace.");
            }
        }

        private static PersonDefinition Person(string id, string displayName, int age, string role)
        {
            PersonDefinition definition = ScriptableObject.CreateInstance<PersonDefinition>();
            definition.DevelopmentConfigure(id, displayName, age, PersonLifeStage.Adult, PersonImportance.Standard, role);
            return definition;
        }

        private static string Sanitize(string id)
        {
            return new string((id ?? string.Empty).Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray());
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
