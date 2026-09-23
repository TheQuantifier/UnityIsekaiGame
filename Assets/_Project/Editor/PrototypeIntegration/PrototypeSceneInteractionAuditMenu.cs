#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityIsekaiGame.Interaction;
using UnityIsekaiGame.WorldLocations.SceneBinding;

namespace UnityIsekaiGame.Editor.PrototypeIntegration
{
    public static class PrototypeSceneInteractionAuditMenu
    {
        private const string PrototypeScenePath = "Assets/_Project/Scenes/Prototype/PrototypeScene.unity";

        [MenuItem("Tools/Project Maintenance/Phase 2 Prototype Integration/Audit Prototype Interactions")]
        public static void AuditPrototypeInteractionsMenu()
        {
            Scene scene = EditorSceneManager.OpenScene(PrototypeScenePath, OpenSceneMode.Single);
            Physics.SyncTransforms();

            MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
            IInteractable[] interactables = behaviours
                .Where(item => item is IInteractable)
                .Cast<IInteractable>()
                .OrderBy(item => HierarchyPath(((MonoBehaviour)item).transform), StringComparer.Ordinal)
                .ThenBy(item => item.GetType().FullName, StringComparer.Ordinal)
                .ToArray();

            List<string> issues = new List<string>();
            List<string> details = new List<string>();
            foreach (IInteractable interactable in interactables)
            {
                MonoBehaviour component = (MonoBehaviour)interactable;
                Collider[] colliders = FindOwnedColliders(component);
                string path = HierarchyPath(component.transform);
                string colliderSummary = colliders.Length == 0
                    ? "none"
                    : string.Join("; ", colliders.Select(DescribeCollider));
                details.Add($"{path} | {component.GetType().Name} | active={component.isActiveAndEnabled} | prompt='{interactable.InteractionPrompt}' | colliders={colliderSummary}");

                if (colliders.Length == 0)
                {
                    issues.Add($"{path}: {component.GetType().Name} has no collider that resolves to it.");
                    continue;
                }

                if (colliders.All(item => !item.enabled || !item.gameObject.activeInHierarchy))
                {
                    issues.Add($"{path}: every interaction collider is disabled or inactive.");
                }

                if (colliders.Any(item => item.bounds.size.sqrMagnitude <= 0.0001f))
                {
                    issues.Add($"{path}: an interaction collider has effectively zero size.");
                }

                if (string.IsNullOrWhiteSpace(interactable.InteractionPrompt))
                {
                    issues.Add($"{path}: interaction prompt is empty.");
                }

                if (component is InteractionPointSceneBinding point)
                {
                    if (!colliders.Any(item => item.enabled && item.isTrigger))
                    {
                        issues.Add($"{path}: interaction point has no enabled trigger collider.");
                    }

                    if (!point.RequiresPhysicalRange || point.InteractionRange <= 0f)
                    {
                        issues.Add($"{path}: interaction point does not enforce a valid physical range.");
                    }
                }

                if (component is ConnectionSceneBinding connection)
                {
                    if (!connection.InteractionEnabled)
                    {
                        issues.Add($"{path}: connection displays as an interactable but interaction toggling is disabled.");
                    }

                    if (!colliders.Any(item => item.enabled && item.isTrigger))
                    {
                        issues.Add($"{path}: connection has no persistent trigger area for interaction while open.");
                    }
                }

                foreach (IGrouping<Transform, Collider> group in colliders.GroupBy(item => item.transform))
                {
                    if (group.Count() > 1)
                    {
                        issues.Add($"{HierarchyPath(group.Key)}: multiple interaction colliders overlap on the same transform.");
                    }
                }
            }

            CameraInteractionDetector[] detectors = UnityEngine.Object.FindObjectsByType<CameraInteractionDetector>(FindObjectsInactive.Include);
            if (detectors.Length != 1)
            {
                issues.Add($"Expected exactly one CameraInteractionDetector, found {detectors.Length}.");
            }
            else
            {
                CameraInteractionDetector detector = detectors[0];
                if (detector.TriggerInteraction == QueryTriggerInteraction.Ignore)
                {
                    issues.Add("CameraInteractionDetector ignores trigger colliders.");
                }

                if (detector.MaxDistance <= 0f)
                {
                    issues.Add("CameraInteractionDetector has no usable interaction distance.");
                }
            }

            string report = $"Prototype interaction audit: Scene='{scene.path}' Interactables={interactables.Length} Detectors={detectors.Length} Issues={issues.Count}{Environment.NewLine}"
                + string.Join(Environment.NewLine, details)
                + (issues.Count == 0 ? string.Empty : $"{Environment.NewLine}ISSUES:{Environment.NewLine}{string.Join(Environment.NewLine, issues)}");

            if (issues.Count == 0)
            {
                Debug.Log(report);
            }
            else
            {
                Debug.LogWarning(report);
            }
        }

        private static Collider[] FindOwnedColliders(MonoBehaviour interactable)
        {
            return interactable.GetComponentsInChildren<Collider>(true)
                .Where(collider => ReferenceEquals(ResolveInteractable(collider), interactable))
                .ToArray();
        }

        private static MonoBehaviour ResolveInteractable(Collider collider)
        {
            if (collider == null)
            {
                return null;
            }

            Transform current = collider.transform;
            while (current != null)
            {
                MonoBehaviour match = current.GetComponents<MonoBehaviour>().FirstOrDefault(item => item is IInteractable);
                if (match != null)
                {
                    return match;
                }

                current = current.parent;
            }

            return null;
        }

        private static string DescribeCollider(Collider collider)
        {
            string layer = LayerMask.LayerToName(collider.gameObject.layer);
            Bounds bounds = collider.bounds;
            return $"{HierarchyPath(collider.transform)}[{collider.GetType().Name}, enabled={collider.enabled}, trigger={collider.isTrigger}, layer={layer}({collider.gameObject.layer}), center={bounds.center:F2}, size={bounds.size:F2}]";
        }

        private static string HierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            Stack<string> names = new Stack<string>();
            Transform current = transform;
            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", names);
        }
    }
}
#endif
