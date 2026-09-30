#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.WorldLocations.SceneBinding;

namespace UnityIsekaiGame.Editor.WorldLocations
{
    public static class WorldSceneBindingValidationMenu
    {
        [MenuItem("Tools/World Locations/Scene Binding/Validate Current Scene")]
        public static void ValidateCurrentScene()
        {
            WorldSceneBindingComponent[] bindings = FindSceneBindings();
            WorldSceneBindingValidationReport report = ValidateBindings(bindings);
            string details = report.Issues.Count == 0
                ? "No scene binding issues found."
                : string.Join(Environment.NewLine, report.Issues.Select(issue => issue.ToString()));

            if (report.Succeeded)
            {
                Debug.Log($"World scene binding validation succeeded. {report.Summary}{Environment.NewLine}{details}");
            }
            else
            {
                Debug.LogWarning($"World scene binding validation failed. {report.Summary}{Environment.NewLine}{details}");
            }
        }

        public static WorldSceneBindingValidationReport ValidateBindings(IEnumerable<WorldSceneBindingComponent> bindings)
        {
            WorldSceneBindingComponent[] live = (bindings ?? Array.Empty<WorldSceneBindingComponent>())
                .Where(binding => binding != null)
                .ToArray();
            List<WorldSceneBindingIssue> issues = new List<WorldSceneBindingIssue>();
            foreach (WorldSceneBindingComponent binding in live)
            {
                if (binding.Role != WorldSceneBindingRole.PresentationOnly && binding.Category != WorldSceneBindingCategory.PresentationOnly && string.IsNullOrWhiteSpace(binding.LogicalId))
                {
                    issues.Add(new WorldSceneBindingIssue(WorldSceneBindingIssueSeverity.Error, binding.Category, binding.LogicalId, binding.BindingKey, $"Scene binding on '{binding.gameObject.name}' has no logical ID."));
                }

                if (string.IsNullOrWhiteSpace(binding.BindingKey))
                {
                    issues.Add(new WorldSceneBindingIssue(binding.Required ? WorldSceneBindingIssueSeverity.Error : WorldSceneBindingIssueSeverity.Warning, binding.Category, binding.LogicalId, binding.BindingKey, $"Scene binding on '{binding.gameObject.name}' has no binding key."));
                }
            }

            foreach (IGrouping<string, WorldSceneBindingComponent> duplicateGroup in live
                .Where(binding => binding.Role == WorldSceneBindingRole.Primary && binding.Category != WorldSceneBindingCategory.PresentationOnly)
                .GroupBy(binding => $"{binding.Category}:{binding.WorldId}:{binding.SceneKey}:{binding.LogicalId}", StringComparer.Ordinal)
                .Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() > 1))
            {
                WorldSceneBindingComponent first = duplicateGroup.First();
                issues.Add(new WorldSceneBindingIssue(WorldSceneBindingIssueSeverity.Error, first.Category, first.LogicalId, first.BindingKey, $"Duplicate primary scene bindings: {string.Join(", ", duplicateGroup.Select(binding => binding.gameObject.name).OrderBy(value => value, StringComparer.Ordinal))}."));
            }

            return new WorldSceneBindingValidationReport(live.Select(binding => binding.CreateSnapshot()), issues);
        }

        private static WorldSceneBindingComponent[] FindSceneBindings()
        {
            return UnityEngine.Object.FindObjectsByType<WorldSceneBindingComponent>(FindObjectsInactive.Include)
                .OrderBy(binding => binding.gameObject.scene.name, StringComparer.Ordinal)
                .ThenBy(binding => binding.name, StringComparer.Ordinal)
                .ToArray();
        }
    }
}
#endif
