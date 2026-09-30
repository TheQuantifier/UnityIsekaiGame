using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityIsekaiGame.Combat;

namespace UnityIsekaiGame.Editor
{
    public static class VisualGameplayPipelineValidation
    {
        public const string PrototypeScenePath = "Assets/_Project/Scenes/Prototype/PrototypeScene.unity";

        private static readonly string[] AuthoredRoots =
        {
            "Assets/_Project",
            "Packages/com.thequantifier.isekai.content"
        };

        private const string DedicatedServerCollisionRoot =
            "Packages/com.thequantifier.isekai.content/Content/World/Terrain/ServerCollision";

        private static readonly Regex GuidReferencePattern = new Regex(
            @"guid:\s*([0-9a-fA-F]{32})",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        [MenuItem("Tools/Project Maintenance/Validate Visual Gameplay Pipeline")]
        public static void ValidateFromMenu()
        {
            VisualGameplayPipelineValidationReport report = Validate();
            if (report.ErrorCount > 0) Debug.LogError(report.GetSummary());
            else if (report.WarningCount > 0) Debug.LogWarning(report.GetSummary());
            else Debug.Log(report.GetSummary());
        }

        public static VisualGameplayPipelineValidationReport Validate()
        {
            VisualGameplayPipelineValidationReport report = new VisualGameplayPipelineValidationReport();
            ValidateBuildScenes(report);
            ValidateAuthoredAssets(report);
            ValidateBuildDependencies(report);
            ValidatePrefabs(report);
            ValidateRuntimeResources(report);
            ValidatePrototypeScene(report);
            if (report.ErrorCount == 0 && report.WarningCount == 0)
            {
                report.AddInfo("Visual gameplay pipeline validation completed without findings.");
            }

            return report;
        }

        private static void ValidateBuildScenes(VisualGameplayPipelineValidationReport report)
        {
            EditorBuildSettingsScene[] enabled = EditorBuildSettings.scenes.Where(scene => scene.enabled).ToArray();
            if (enabled.Length == 0)
            {
                report.AddError("The client build has no enabled scenes.");
                return;
            }

            HashSet<string> paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (EditorBuildSettingsScene scene in enabled)
            {
                if (string.IsNullOrWhiteSpace(scene.path) || !paths.Add(scene.path))
                {
                    report.AddError($"The build contains a missing or duplicate scene path '{scene.path}'.");
                    continue;
                }

                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path) == null)
                {
                    report.AddError($"Enabled build scene '{scene.path}' cannot be loaded.");
                }
            }

            if (!enabled.Any(scene => string.Equals(scene.path, PrototypeScenePath, StringComparison.Ordinal)))
            {
                report.AddError($"The playable scene '{PrototypeScenePath}' is not enabled in build settings.");
            }
        }

        private static void ValidateAuthoredAssets(VisualGameplayPipelineValidationReport report)
        {
            HashSet<string> visitedPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets(string.Empty, AuthoredRoots))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrWhiteSpace(path)
                    || path.StartsWith(DedicatedServerCollisionRoot, StringComparison.Ordinal)
                    || !visitedPaths.Add(path))
                {
                    continue;
                }

                if (AssetDatabase.IsValidFolder(path)) continue;

                ValidateYamlReferences(path, report);

                UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset == null)
                {
                    if (HasUnityAssetExtension(path))
                    {
                        report.AddError($"Authored Unity asset '{path}' has a GUID but cannot be loaded.");
                    }

                    continue;
                }

                if (asset is Material material) ValidateMaterial(material, path, report);
                if (asset is AnimatorController controller) ValidateAnimatorController(controller, path, report);
            }
        }

        private static void ValidateYamlReferences(string path, VisualGameplayPipelineValidationReport report)
        {
            string extension = Path.GetExtension(path);
            if (!string.Equals(extension, ".asset", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(extension, ".controller", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(extension, ".mat", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(extension, ".prefab", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(extension, ".unity", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                string yaml = File.ReadAllText(path);
                foreach (Match match in GuidReferencePattern.Matches(yaml))
                {
                    string guid = match.Groups[1].Value;
                    if (string.Equals(guid, "00000000000000000000000000000000", StringComparison.Ordinal)) continue;
                    if (!string.IsNullOrWhiteSpace(AssetDatabase.GUIDToAssetPath(guid))) continue;

                    report.AddError($"Asset '{path}' references missing GUID '{guid}'.");
                }
            }
            catch (Exception exception)
            {
                report.AddError($"Could not inspect serialized references in '{path}': {exception.Message}");
            }
        }

        private static bool HasUnityAssetExtension(string path)
        {
            string extension = Path.GetExtension(path);
            return string.Equals(extension, ".asset", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".anim", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".controller", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".mat", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".prefab", StringComparison.OrdinalIgnoreCase);
        }

        private static void ValidateMaterial(Material material, string path, VisualGameplayPipelineValidationReport report)
        {
            if (material.shader == null)
            {
                report.AddError($"Material '{path}' has no shader.");
                return;
            }

            if (string.Equals(material.shader.name, "Hidden/InternalErrorShader", StringComparison.Ordinal))
            {
                report.AddError($"Material '{path}' resolves to Unity's error shader.");
            }
        }

        private static void ValidateAnimatorController(
            AnimatorController controller,
            string path,
            VisualGameplayPipelineValidationReport report)
        {
            string[] duplicateParameters = controller.parameters
                .GroupBy(parameter => parameter.name, StringComparer.Ordinal)
                .Where(group => string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();
            foreach (string parameter in duplicateParameters)
            {
                report.AddError($"Animator controller '{path}' has a missing or duplicate parameter '{parameter}'.");
            }

            if (controller.animationClips.Any(clip => clip == null))
            {
                report.AddError($"Animator controller '{path}' contains a missing animation clip.");
            }

            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                if (layer?.stateMachine == null)
                {
                    report.AddError($"Animator controller '{path}' contains a layer without a state machine.");
                    continue;
                }

                ValidateStateMachine(controller, layer.stateMachine, path, report);
            }
        }

        private static void ValidateStateMachine(
            AnimatorController controller,
            AnimatorStateMachine stateMachine,
            string path,
            VisualGameplayPipelineValidationReport report)
        {
            foreach (ChildAnimatorState child in stateMachine.states)
            {
                AnimatorState state = child.state;
                if (state == null)
                {
                    report.AddError($"Animator controller '{path}' contains a missing state.");
                    continue;
                }

                foreach (AnimatorStateTransition transition in state.transitions)
                {
                    if (transition == null || transition.isExit) continue;
                    if (transition.destinationState == null && transition.destinationStateMachine == null)
                    {
                        report.AddError($"Animator controller '{path}' state '{state.name}' has a transition with no destination.");
                    }

                    foreach (AnimatorCondition condition in transition.conditions)
                    {
                        if (!controller.parameters.Any(parameter => string.Equals(parameter.name, condition.parameter, StringComparison.Ordinal)))
                        {
                            report.AddError($"Animator controller '{path}' transition from '{state.name}' references missing parameter '{condition.parameter}'.");
                        }
                    }
                }
            }

            foreach (ChildAnimatorStateMachine child in stateMachine.stateMachines)
            {
                if (child.stateMachine == null) report.AddError($"Animator controller '{path}' contains a missing child state machine.");
                else ValidateStateMachine(controller, child.stateMachine, path, report);
            }
        }

        private static void ValidateBuildDependencies(VisualGameplayPipelineValidationReport report)
        {
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes.Where(value => value.enabled))
            {
                foreach (string dependency in AssetDatabase.GetDependencies(scene.path, true))
                {
                    if (string.IsNullOrWhiteSpace(dependency))
                    {
                        report.AddError($"Build scene '{scene.path}' has an empty dependency path.");
                        continue;
                    }

                    if (AssetDatabase.LoadMainAssetAtPath(dependency) == null)
                    {
                        report.AddError($"Build scene '{scene.path}' dependency '{dependency}' cannot be loaded.");
                    }
                }
            }
        }

        private static void ValidatePrefabs(VisualGameplayPipelineValidationReport report)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", AuthoredRoots))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith(DedicatedServerCollisionRoot, StringComparison.Ordinal)) continue;
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    report.AddError($"Prefab '{path}' cannot be loaded.");
                    continue;
                }

                int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab);
                if (missing > 0) report.AddError($"Prefab '{path}' contains {missing} missing script component(s).");

            }
        }

        private static void ValidateRuntimeResources(VisualGameplayPipelineValidationReport report)
        {
            Sprite roundedSurface = Resources.Load<Sprite>("Theme/GameUiRoundedSurface");
            if (roundedSurface == null)
            {
                report.AddError("Required Resources sprite 'Theme/GameUiRoundedSurface' is missing or is not imported as a Sprite.");
            }
        }

        private static void ValidatePrototypeScene(VisualGameplayPipelineValidationReport report)
        {
            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Scene scene = EditorSceneManager.OpenScene(PrototypeScenePath, OpenSceneMode.Single);
                GameObject[] roots = scene.GetRootGameObjects();
                Component[] components = roots.SelectMany(root => root.GetComponentsInChildren<Component>(true)).ToArray();
                int missingScripts = components.Count(component => component == null);
                if (missingScripts > 0)
                {
                    report.AddError($"Prototype Scene contains {missingScripts} missing script component(s).");
                }

                Camera[] cameras = roots.SelectMany(root => root.GetComponentsInChildren<Camera>(true)).ToArray();
                if (!cameras.Any(camera => camera != null && camera.gameObject.activeInHierarchy && camera.enabled))
                {
                    report.AddError("Prototype Scene has no active enabled camera.");
                }

                foreach (MeshFilter filter in roots.SelectMany(root => root.GetComponentsInChildren<MeshFilter>(true)))
                {
                    MeshRenderer renderer = filter == null ? null : filter.GetComponent<MeshRenderer>();
                    if (renderer != null && renderer.enabled && filter.sharedMesh == null)
                    {
                        report.AddError($"Enabled MeshRenderer '{HierarchyPath(renderer.transform)}' has no mesh.");
                    }
                }

                foreach (SkinnedMeshRenderer renderer in roots.SelectMany(root => root.GetComponentsInChildren<SkinnedMeshRenderer>(true)))
                {
                    if (renderer != null && renderer.enabled && renderer.sharedMesh == null)
                    {
                        report.AddError($"Enabled SkinnedMeshRenderer '{HierarchyPath(renderer.transform)}' has no mesh.");
                    }
                }

                foreach (Renderer renderer in roots.SelectMany(root => root.GetComponentsInChildren<Renderer>(true)))
                {
                    if (renderer == null || !renderer.enabled || renderer.sharedMaterials.Length == 0) continue;
                    if (renderer.sharedMaterials.Any(material => material == null))
                    {
                        report.AddError($"Enabled renderer '{HierarchyPath(renderer.transform)}' contains a missing material.");
                    }
                }

                foreach (PrototypeEnemyAnimationDriver driver in roots.SelectMany(root => root.GetComponentsInChildren<PrototypeEnemyAnimationDriver>(true)))
                {
                    Animator animator = driver == null ? null : driver.GetComponentInChildren<Animator>(true);
                    if (animator == null || animator.runtimeAnimatorController == null)
                    {
                        report.AddError($"Enemy animation driver '{HierarchyPath(driver.transform)}' has no Animator controller.");
                    }
                }

                foreach (CanvasScaler scaler in roots.SelectMany(root => root.GetComponentsInChildren<CanvasScaler>(true)))
                {
                    if (scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize
                        && (scaler.referenceResolution.x <= 0f || scaler.referenceResolution.y <= 0f))
                    {
                        report.AddError($"CanvasScaler '{HierarchyPath(scaler.transform)}' has an invalid reference resolution.");
                    }
                }

                foreach (RectTransform rect in roots.SelectMany(root => root.GetComponentsInChildren<RectTransform>(true)))
                {
                    if (!IsFinite(rect.anchorMin) || !IsFinite(rect.anchorMax) || !IsFinite(rect.anchoredPosition) || !IsFinite(rect.sizeDelta))
                    {
                        report.AddError($"UI transform '{HierarchyPath(rect)}' contains non-finite layout values.");
                    }
                }
            }
            finally
            {
                if (setup.Any(value => value.isLoaded))
                {
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                }
                else
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
            }
        }

        private static string HierarchyPath(Transform transform)
        {
            if (transform == null) return "<missing>";
            Stack<string> names = new Stack<string>();
            for (Transform current = transform; current != null; current = current.parent) names.Push(current.name);
            return string.Join("/", names);
        }

        private static bool IsFinite(Vector2 value)
            => !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y);
    }

    public sealed class VisualGameplayPipelineValidationReport
    {
        private readonly List<string> errors = new List<string>();
        private readonly List<string> warnings = new List<string>();
        private readonly List<string> infos = new List<string>();

        public int ErrorCount => errors.Count;
        public int WarningCount => warnings.Count;
        public IReadOnlyList<string> Errors => errors;
        public IReadOnlyList<string> Warnings => warnings;

        public void AddError(string message) => errors.Add(message);
        public void AddWarning(string message) => warnings.Add(message);
        public void AddInfo(string message) => infos.Add(message);

        public string GetSummary()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"Visual gameplay pipeline validation: {ErrorCount} error(s), {WarningCount} warning(s), {infos.Count} info message(s).");
            foreach (string error in errors) builder.AppendLine($"Error: {error}");
            foreach (string warning in warnings) builder.AppendLine($"Warning: {warning}");
            foreach (string info in infos) builder.AppendLine($"Info: {info}");
            return builder.ToString();
        }
    }
}
