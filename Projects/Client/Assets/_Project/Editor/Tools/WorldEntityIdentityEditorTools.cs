using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityIsekaiGame.WorldEntities;

namespace UnityIsekaiGame.Editor
{
    public static class WorldEntityIdentityEditorTools
    {
        private const string PrototypeScenePath = "Assets/_Project/Scenes/Prototype/PrototypeScene.unity";

        [MenuItem("Tools/World Entities/Validate Current Scene")]
        public static void ValidateCurrentScene()
        {
            WorldEntityIdentity[] identities = Object.FindObjectsByType<WorldEntityIdentity>(FindObjectsInactive.Include);
            List<string> errors = new List<string>();
            Dictionary<string, WorldEntityIdentity> byEntityId = new Dictionary<string, WorldEntityIdentity>();
            Dictionary<string, WorldEntityIdentity> bySceneLocal = new Dictionary<string, WorldEntityIdentity>();

            foreach (WorldEntityIdentity identity in identities)
            {
                if (identity == null || identity.IdentityKind == WorldEntityIdentityKind.Transient)
                {
                    continue;
                }

                if (!identity.ValidateIdentity(out string failureReason))
                {
                    errors.Add($"{identity.name}: {failureReason}");
                    continue;
                }

                if (byEntityId.TryGetValue(identity.EntityId, out WorldEntityIdentity duplicate))
                {
                    errors.Add($"Duplicate world entity ID '{identity.EntityId}' on '{duplicate.name}' and '{identity.name}'.");
                }
                else
                {
                    byEntityId.Add(identity.EntityId, identity);
                }

                if (identity.IdentityKind == WorldEntityIdentityKind.Authored)
                {
                    string localKey = $"{identity.SceneKey}:{identity.LocalAuthoredId}";
                    if (bySceneLocal.TryGetValue(localKey, out WorldEntityIdentity localDuplicate))
                    {
                        errors.Add($"Duplicate authored local ID '{identity.LocalAuthoredId}' in scene '{identity.SceneKey}' on '{localDuplicate.name}' and '{identity.name}'.");
                    }
                    else
                    {
                        bySceneLocal.Add(localKey, identity);
                    }
                }
            }

            if (errors.Count == 0)
            {
                Debug.Log($"World entity validation passed for {identities.Length} identity component(s).");
                return;
            }

            Debug.LogError($"World entity validation failed with {errors.Count} error(s):\n{string.Join("\n", errors)}");
        }

        public static void ValidatePrototypeScene()
        {
            EditorSceneManager.OpenScene(PrototypeScenePath, OpenSceneMode.Single);
            ValidateCurrentScene();
        }

        [MenuItem("Tools/World Entities/Assign Missing Authored IDs")]
        public static void AssignMissingAuthoredIds()
        {
            WorldEntityIdentity[] identities = Object.FindObjectsByType<WorldEntityIdentity>(FindObjectsInactive.Include);
            HashSet<string> usedIds = identities
                .Where(identity => identity != null && !string.IsNullOrWhiteSpace(identity.EntityId))
                .Select(identity => identity.EntityId)
                .ToHashSet();
            int assigned = 0;

            foreach (WorldEntityIdentity identity in identities)
            {
                if (identity == null
                    || identity.IdentityKind != WorldEntityIdentityKind.Authored
                    || !string.IsNullOrWhiteSpace(identity.LocalAuthoredId))
                {
                    continue;
                }

                string baseId = Slug(identity.name);
                string candidate = baseId;
                int suffix = 1;
                while (usedIds.Contains(WorldEntityIdUtility.ComposeAuthoredId(identity.SceneKey, candidate)))
                {
                    candidate = $"{baseId}.{suffix:00}";
                    suffix++;
                }

                Undo.RecordObject(identity, "Assign World Entity ID");
                identity.TrySetAuthoredIdentity(candidate, identity.SceneKey, identity.OwnerScope, identity.DefinitionId, out _);
                usedIds.Add(identity.EntityId);
                EditorUtility.SetDirty(identity);
                assigned++;
            }

            if (assigned > 0)
            {
                EditorSceneManager.MarkAllScenesDirty();
            }

            Debug.Log($"Assigned {assigned} missing authored world entity ID(s).");
        }

        [MenuItem("Tools/World Entities/List Registered Runtime Entities")]
        public static void ListRegisteredRuntimeEntities()
        {
            Debug.Log(WorldEntityRegistry.BuildDiagnosticReport());
        }

        private static string Slug(string value)
        {
            string lower = string.IsNullOrWhiteSpace(value) ? "entity" : value.Trim().ToLowerInvariant();
            lower = Regex.Replace(lower, "[^a-z0-9]+", ".");
            lower = Regex.Replace(lower, "\\.+", ".");
            return lower.Trim('.');
        }
    }
}
