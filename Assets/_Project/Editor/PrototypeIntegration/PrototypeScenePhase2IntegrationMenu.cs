#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.PrototypeIntegration;
using UnityIsekaiGame.WorldLocations;
using UnityIsekaiGame.WorldLocations.SceneBinding;

namespace UnityIsekaiGame.Editor.PrototypeIntegration
{
    public static class PrototypeScenePhase2IntegrationMenu
    {
        public const string PrototypeScenePath = "Assets/_Project/Scenes/Prototype/PrototypeScene.unity";
        private const string AdventurerGuildPrefabPath = "Assets/_Project/Prototype/Prefabs/Buildings/PrototypeAdventurerGuild/AdventurerGuild.prefab";
        private const string ToolRootName = "Phase 2 Production Bindings";
        private const string InteractionAreaName = "Interaction Area";
        private static readonly string[] LegacyCounterBindingObjectNames =
        {
            "Interact - Adventurer Guild Counter",
            "Interact - Merchant Guild Counter",
            "Adventurer Guild Counter Source",
            "Merchant Guild Counter Source",
            "Adventurer Guild Counter",
            "Merchant Guild Counter",
            "Adventurer Guild Quest Board"
        };

        [MenuItem("Tools/Project Maintenance/Phase 2 Prototype Integration/Apply Prototype Scene Integration")]
        public static void ApplyPrototypeSceneIntegrationMenu()
        {
            PrototypeSceneIntegrationApplyResult result = ApplyPrototypeSceneIntegration();
            Debug.Log($"Phase 2 prototype scene integration applied. SceneChanges={result.SceneChanges} PlaceholdersCreated={result.PlaceholdersCreated} MissingScriptsRemoved={result.MissingScriptsRemoved}.");
        }

        [MenuItem("Tools/Project Maintenance/Phase 2 Prototype Integration/Validate Prototype Scene Integration")]
        public static void ValidatePrototypeSceneIntegrationMenu()
        {
            PrototypeSceneIntegrationValidationReport report = ValidateOpenOrPrototypeScene();
            string details = report.Issues.Count == 0
                ? "No Phase 2 prototype integration issues found."
                : string.Join(Environment.NewLine, report.Issues.Select(issue => issue.ToString()));
            string message = $"Phase 2 prototype integration validation finished. {report.Summary}{Environment.NewLine}{details}";
            if (report.Succeeded)
            {
                Debug.Log(message);
            }
            else
            {
                Debug.LogWarning(message);
            }
        }

        public static PrototypeSceneIntegrationApplyResult ApplyPrototypeSceneIntegration()
        {
            int prefabChanges = ApplyAdventurerGuildPrefabBindings();
            Scene scene = EditorSceneManager.OpenScene(PrototypeScenePath, OpenSceneMode.Single);
            GameObject[] roots = scene.GetRootGameObjects();
            int missingScripts = RemoveMissingScripts(roots);
            GameObject root = EnsurePath(scene, "PrototypeScene/Gameplay/" + ToolRootName);
            int sceneChanges = missingScripts + prefabChanges;
            int placeholders = 0;

            sceneChanges += EnsureBootstrap(root, WorldSceneBindingBootstrapMode.DevelopmentFixtureImport);
            sceneChanges += RemoveLegacyCounterBindingComponents();
            sceneChanges += RemoveLegacyCounterBindingObjects(root);
            foreach (PrototypeSceneWorldBindingExpectation expected in PrototypeSceneIntegrationContract.WorldBindings)
            {
                GameObject target = ResolveTarget(root, expected, out bool created);
                if (created)
                {
                    placeholders++;
                    sceneChanges++;
                }

                sceneChanges += ApplyWorldBinding(target, expected);
            }

            foreach (PrototypeQuestSourceBindingExpectation expected in PrototypeSceneIntegrationContract.QuestSourceBindings)
            {
                GameObject target = ResolveQuestSourceTarget(root, expected, out bool created);
                if (created)
                {
                    placeholders++;
                    sceneChanges++;
                }

                sceneChanges += ApplyQuestSourceBinding(target, expected);
            }

            if (sceneChanges > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
            }

            return new PrototypeSceneIntegrationApplyResult(sceneChanges, placeholders, missingScripts);
        }

        public static PrototypeSceneIntegrationValidationReport ValidateOpenOrPrototypeScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !string.Equals(scene.path, PrototypeScenePath, StringComparison.Ordinal))
            {
                scene = EditorSceneManager.OpenScene(PrototypeScenePath, OpenSceneMode.Single);
            }

            List<PrototypeSceneIntegrationIssue> precomputed = new List<PrototypeSceneIntegrationIssue>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root);
                if (missing > 0)
                {
                    precomputed.Add(new PrototypeSceneIntegrationIssue(PrototypeSceneIntegrationIssueSeverity.Error, PrototypeSceneIntegrationIssueDomain.MissingScript, root.name, $"Root '{root.name}' contains {missing} missing script reference(s)."));
                }
            }

            WorldSceneBindingComponent[] worldBindings = UnityEngine.Object.FindObjectsByType<WorldSceneBindingComponent>(FindObjectsInactive.Include);
            QuestSourceSceneBinding[] questSourceBindings = UnityEngine.Object.FindObjectsByType<QuestSourceSceneBinding>(FindObjectsInactive.Include);
            return PrototypeSceneIntegrationValidator.ValidateComponents(worldBindings, questSourceBindings, null, precomputed);
        }

        private static int EnsureBootstrap(GameObject root, WorldSceneBindingBootstrapMode desiredMode)
        {
            bool added = EnsureComponent<WorldSceneBindingBootstrap>(root, out WorldSceneBindingBootstrap bootstrap);
            bool modeChanged = bootstrap.BootstrapMode != desiredMode;
            if (modeChanged)
            {
                bootstrap.ConfigureMode(desiredMode);
                EditorUtility.SetDirty(bootstrap);
            }

            return added || modeChanged ? 1 : 0;
        }

        private static GameObject ResolveTarget(GameObject scaffoldRoot, PrototypeSceneWorldBindingExpectation expected, out bool created)
        {
            string existingName = PreferredExistingName(expected);
            GameObject existing = FindLoadedObject(existingName);
            if (existing != null)
            {
                created = false;
                return existing;
            }

            if (ExpectedPhysicalInteractionObjectName(expected.LogicalId) != null)
            {
                created = false;
                return null;
            }

            WorldSceneBindingComponent binding = UnityEngine.Object.FindObjectsByType<WorldSceneBindingComponent>(FindObjectsInactive.Include)
                .FirstOrDefault(item => item.Category == expected.Category && string.Equals(item.LogicalId, expected.LogicalId, StringComparison.Ordinal));
            if (binding != null)
            {
                created = false;
                return binding.gameObject;
            }

            Transform parent = EnsureChild(scaffoldRoot.transform, CategoryFolder(expected.Category));
            created = true;
            return CreatePlaceholder(parent, PlaceholderName(expected), expected.Category);
        }

        private static GameObject ResolveQuestSourceTarget(GameObject scaffoldRoot, PrototypeQuestSourceBindingExpectation expected, out bool created)
        {
            string preferredName = PreferredExistingQuestSourceName(expected);
            GameObject preferred = FindLoadedObject(preferredName);
            if (preferred != null)
            {
                created = false;
                return preferred;
            }

            string expectedPhysicalObjectName = ExpectedPhysicalInteractionObjectName(expected.InteractionPointId);
            if (expectedPhysicalObjectName != null)
            {
                InteractionPointSceneBinding physicalInteractionPoint = UnityEngine.Object.FindObjectsByType<InteractionPointSceneBinding>(FindObjectsInactive.Include)
                    .FirstOrDefault(item => string.Equals(item.LogicalId, expected.InteractionPointId, StringComparison.Ordinal)
                        && string.Equals(item.gameObject.name, expectedPhysicalObjectName, StringComparison.Ordinal));
                created = false;
                return physicalInteractionPoint != null ? physicalInteractionPoint.gameObject : null;
            }

            QuestSourceSceneBinding existing = UnityEngine.Object.FindObjectsByType<QuestSourceSceneBinding>(FindObjectsInactive.Include)
                .FirstOrDefault(item => string.Equals(item.QuestSourceId, expected.QuestSourceId, StringComparison.Ordinal));
            if (existing != null)
            {
                created = false;
                return existing.gameObject;
            }

            InteractionPointSceneBinding interactionPoint = UnityEngine.Object.FindObjectsByType<InteractionPointSceneBinding>(FindObjectsInactive.Include)
                .FirstOrDefault(item => string.Equals(item.LogicalId, expected.InteractionPointId, StringComparison.Ordinal));
            if (interactionPoint != null)
            {
                created = false;
                return interactionPoint.gameObject;
            }

            string objectName = expected.DisplayName;
            GameObject sceneObject = FindLoadedObject(objectName);
            if (sceneObject != null)
            {
                created = false;
                return sceneObject;
            }

            Transform parent = EnsureChild(scaffoldRoot.transform, "Quest Sources");
            created = true;
            return CreatePlaceholder(parent, objectName, WorldSceneBindingCategory.Custom);
        }

        private static int ApplyWorldBinding(GameObject target, PrototypeSceneWorldBindingExpectation expected)
        {
            if (target == null)
            {
                return 0;
            }

            int removedDuplicates = RemoveDuplicateWorldBindingsOutsideTarget(target, expected);
            int layoutChanges = EnsurePlaceholderLayout(target, expected);
            return layoutChanges + (expected.Category switch
            {
                WorldSceneBindingCategory.Location => ApplyLocationBinding(target, expected) + removedDuplicates,
                WorldSceneBindingCategory.InteractionPoint => ApplyInteractionBinding(target, expected) + removedDuplicates,
                WorldSceneBindingCategory.Connection => ApplyConnectionBinding(target, expected) + removedDuplicates,
                WorldSceneBindingCategory.Entity => ApplyEntityBinding(target, expected) + removedDuplicates,
                _ => 0
            });
        }

        private static int EnsurePlaceholderLayout(GameObject target, PrototypeSceneWorldBindingExpectation expected)
        {
            if (target == null || !HasAncestorNamed(target.transform, ToolRootName))
            {
                return 0;
            }

            PrototypeSceneWorldBindingExpectation[] categoryBindings = PrototypeSceneIntegrationContract.WorldBindings
                .Where(item => item.Category == expected.Category)
                .ToArray();
            int index = Array.FindIndex(categoryBindings, item => item.LogicalId == expected.LogicalId);
            if (index < 0)
            {
                return 0;
            }

            float rowOffset = expected.Category switch
            {
                WorldSceneBindingCategory.Location => -10f,
                WorldSceneBindingCategory.Connection => 0f,
                WorldSceneBindingCategory.InteractionPoint => 8f,
                WorldSceneBindingCategory.Entity => 16f,
                _ => 20f
            };
            Vector3 desired = new Vector3((index % 5) * 3f, 0.5f, rowOffset + (index / 5) * 3f);
            if (target.transform.localPosition == desired)
            {
                return 0;
            }

            target.transform.localPosition = desired;
            EditorUtility.SetDirty(target.transform);
            return 1;
        }

        private static bool HasAncestorNamed(Transform transform, string objectName)
        {
            Transform current = transform;
            while (current != null)
            {
                if (string.Equals(current.name, objectName, StringComparison.Ordinal))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static int ApplyLocationBinding(GameObject target, PrototypeSceneWorldBindingExpectation expected)
        {
            int removedConflicts = RemoveConflictingWorldBindings<LocationSceneBinding>(target, expected);
            LocationSceneBinding binding = GetOrAdd<LocationSceneBinding>(target, out bool added);
            bool changed = added || binding.LogicalId != expected.LogicalId || binding.BindingKey != expected.BindingKey || binding.SceneKey != PrototypeSceneIntegrationIds.SceneKey || binding.WorldId != PersistenceService.LocalWorldId || binding.Role != expected.Role || binding.Required != expected.Required || binding.LocationDefinitionId != expected.ExpectedDefinitionId;
            if (changed)
            {
                binding.ConfigureLocation(expected.LogicalId, expected.BindingKey, PrototypeSceneIntegrationIds.SceneKey, PersistenceService.LocalWorldId, expected.ExpectedDefinitionId, expected.Role, expected.Required);
                EditorUtility.SetDirty(binding);
            }

            return (changed ? 1 : 0) + removedConflicts;
        }

        private static int ApplyInteractionBinding(GameObject target, PrototypeSceneWorldBindingExpectation expected)
        {
            int removedConflicts = RemoveConflictingWorldBindings<InteractionPointSceneBinding>(target, expected);
            InteractionPointSceneBinding binding = GetOrAdd<InteractionPointSceneBinding>(target, out bool added);
            bool changed = added || binding.LogicalId != expected.LogicalId || binding.BindingKey != expected.BindingKey || binding.SceneKey != PrototypeSceneIntegrationIds.SceneKey || binding.WorldId != PersistenceService.LocalWorldId || binding.Role != expected.Role || binding.Required != expected.Required || binding.AuthoredDisplayName != expected.DisplayName || !Mathf.Approximately(binding.InteractionRange, 3f) || !binding.RequiresPhysicalRange;
            if (changed)
            {
                binding.ConfigureBinding(expected.LogicalId, expected.BindingKey, PrototypeSceneIntegrationIds.SceneKey, PersistenceService.LocalWorldId, expected.Role, expected.Required, expected.DisplayName);
                binding.ConfigureInteraction();
                EditorUtility.SetDirty(binding);
            }

            return (changed ? 1 : 0) + removedConflicts + EnsureInteractionTriggerColliders(target, expected.LogicalId);
        }

        private static int ApplyConnectionBinding(GameObject target, PrototypeSceneWorldBindingExpectation expected)
        {
            int removedConflicts = RemoveConflictingWorldBindings<ConnectionSceneBinding>(target, expected);
            BoxCollider collider = target.GetComponent<BoxCollider>();
            ConnectionSceneBinding binding = GetOrAdd<ConnectionSceneBinding>(target, out bool added);
            bool changed = added || binding.LogicalId != expected.LogicalId || binding.BindingKey != expected.BindingKey || binding.SceneKey != PrototypeSceneIntegrationIds.SceneKey || binding.WorldId != PersistenceService.LocalWorldId || binding.Required != expected.Required || binding.AuthoredDisplayName != expected.DisplayName || !binding.InteractionEnabled || !Mathf.Approximately(binding.InteractionRange, 3f);
            if (changed)
            {
                binding.ConfigureConnection(expected.LogicalId, expected.BindingKey, expected.SourceLocationId, expected.DestinationLocationId, PrototypeSceneIntegrationIds.SceneKey, PersistenceService.LocalWorldId, collider, expected.Required, true, expected.DisplayName);
                binding.ConfigureInteractionRange();
                EditorUtility.SetDirty(binding);
            }

            return (changed ? 1 : 0) + removedConflicts + EnsureConnectionInteractionArea(target);
        }

        private static int ApplyEntityBinding(GameObject target, PrototypeSceneWorldBindingExpectation expected)
        {
            if (!TryParseEntityKey(expected.LogicalId, out EntityLocationReferenceData entity))
            {
                return 0;
            }

            int removedConflicts = RemoveConflictingWorldBindings<WorldEntitySceneBinding>(target, expected);
            WorldEntitySceneBinding binding = GetOrAdd<WorldEntitySceneBinding>(target, out bool added);
            bool changed = added || binding.LogicalId != expected.LogicalId || binding.BindingKey != expected.BindingKey || binding.SceneKey != PrototypeSceneIntegrationIds.SceneKey || binding.WorldId != PersistenceService.LocalWorldId;
            if (changed)
            {
                binding.ConfigureEntity(entity.entityType, entity.entityId, expected.BindingKey, PrototypeSceneIntegrationIds.SceneKey, PersistenceService.LocalWorldId);
                EditorUtility.SetDirty(binding);
            }

            return (changed ? 1 : 0) + removedConflicts;
        }

        private static int ApplyQuestSourceBinding(GameObject target, PrototypeQuestSourceBindingExpectation expected)
        {
            if (target == null)
            {
                return 0;
            }

            int removedDuplicates = RemoveDuplicateQuestSourceBindingsOutsideTarget(target, expected);
            QuestSourceSceneBinding binding = GetOrAdd<QuestSourceSceneBinding>(target, out bool added);
            bool changed = added
                || binding.QuestSourceId != expected.QuestSourceId
                || binding.QuestSourceDefinitionId != expected.DefinitionId
                || binding.SceneBindingKey != expected.BindingKey
                || binding.HostLocationId != expected.HostLocationId
                || binding.InteractionPointId != expected.InteractionPointId
                || binding.Required != expected.Required;
            if (changed)
            {
                binding.ConfigureQuestSource(expected.QuestSourceId, expected.DefinitionId, expected.BindingKey, expected.DisplayName, expected.HostLocationId, expected.InteractionPointId, PrototypeSceneIntegrationIds.SceneKey, PersistenceService.LocalWorldId, expected.Required);
                EditorUtility.SetDirty(binding);
            }

            return (changed ? 1 : 0) + removedDuplicates;
        }

        private static int ApplyAdventurerGuildPrefabBindings()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(AdventurerGuildPrefabPath);
            if (root == null)
            {
                return 0;
            }

            int changes = 0;
            try
            {
                changes += RemoveLegacyCounterBindingComponents(root);
                changes += RemoveLegacyCounterBindingObjects(root);
                changes += EnsureBootstrap(root, WorldSceneBindingBootstrapMode.ProductionBindOnly);
                foreach (PrototypeSceneWorldBindingExpectation expected in PrototypeSceneIntegrationContract.WorldBindings)
                {
                    if (TryResolveAdventurerGuildPrefabTarget(root, expected, out GameObject target))
                    {
                        changes += ApplyWorldBinding(target, expected);
                    }
                }

                foreach (PrototypeQuestSourceBindingExpectation expected in PrototypeSceneIntegrationContract.QuestSourceBindings)
                {
                    if (TryResolveAdventurerGuildPrefabQuestSourceTarget(root, expected, out GameObject target))
                    {
                        changes += ApplyQuestSourceBinding(target, expected);
                    }
                }

                if (changes > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, AdventurerGuildPrefabPath);
                    AssetDatabase.SaveAssets();
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return changes;
        }

        private static T GetOrAdd<T>(GameObject target, out bool added) where T : Component
        {
            RemoveDuplicateComponents<T>(target);
            T component = target.GetComponent<T>();
            if (component != null)
            {
                added = false;
                return component;
            }

            added = true;
            component = target.AddComponent<T>();
            EditorUtility.SetDirty(target);
            return component;
        }

        private static bool EnsureComponent<T>(GameObject target, out T component) where T : Component
        {
            component = GetOrAdd<T>(target, out bool added);
            return added;
        }

        private static void RemoveDuplicateComponents<T>(GameObject target) where T : Component
        {
            T[] components = target.GetComponents<T>();
            for (int i = components.Length - 1; i >= 1; i--)
            {
                UnityEngine.Object.DestroyImmediate(components[i], true);
                EditorUtility.SetDirty(target);
            }
        }

        private static int RemoveConflictingWorldBindings<TExpected>(GameObject target, PrototypeSceneWorldBindingExpectation expected) where TExpected : WorldSceneBindingComponent
        {
            int removed = 0;
            foreach (WorldSceneBindingComponent component in target.GetComponents<WorldSceneBindingComponent>())
            {
                if (component.Category != expected.Category)
                {
                    continue;
                }

                bool keep = component is TExpected
                    && string.Equals(component.LogicalId, expected.LogicalId, StringComparison.Ordinal);
                if (keep)
                {
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(component, true);
                removed++;
                EditorUtility.SetDirty(target);
            }

            return removed;
        }

        private static int RemoveDuplicateWorldBindingsOutsideTarget(GameObject target, PrototypeSceneWorldBindingExpectation expected)
        {
            int removed = 0;
            if (target.scene.IsValid() && string.Equals(target.scene.path, PrototypeScenePath, StringComparison.Ordinal))
            {
                foreach (WorldSceneBindingComponent binding in UnityEngine.Object.FindObjectsByType<WorldSceneBindingComponent>(FindObjectsInactive.Include))
                {
                    if (ShouldRemoveDuplicateWorldBinding(binding, target, expected))
                    {
                        UnityEngine.Object.DestroyImmediate(binding, true);
                        removed++;
                    }
                }
            }

            foreach (WorldSceneBindingComponent binding in target.transform.root.GetComponentsInChildren<WorldSceneBindingComponent>(true))
            {
                if (ShouldRemoveDuplicateWorldBinding(binding, target, expected))
                {
                    UnityEngine.Object.DestroyImmediate(binding, true);
                    removed++;
                }
            }

            return removed;
        }

        private static bool ShouldRemoveDuplicateWorldBinding(WorldSceneBindingComponent binding, GameObject target, PrototypeSceneWorldBindingExpectation expected)
        {
            return binding != null
                && binding.gameObject != target
                && binding.Category == expected.Category
                && string.Equals(binding.LogicalId, expected.LogicalId, StringComparison.Ordinal);
        }

        private static int RemoveDuplicateQuestSourceBindingsOutsideTarget(GameObject target, PrototypeQuestSourceBindingExpectation expected)
        {
            int removed = 0;
            if (target.scene.IsValid() && string.Equals(target.scene.path, PrototypeScenePath, StringComparison.Ordinal))
            {
                foreach (QuestSourceSceneBinding binding in UnityEngine.Object.FindObjectsByType<QuestSourceSceneBinding>(FindObjectsInactive.Include))
                {
                    if (ShouldRemoveDuplicateQuestSourceBinding(binding, target, expected))
                    {
                        UnityEngine.Object.DestroyImmediate(binding, true);
                        removed++;
                    }
                }
            }

            foreach (QuestSourceSceneBinding binding in target.transform.root.GetComponentsInChildren<QuestSourceSceneBinding>(true))
            {
                if (ShouldRemoveDuplicateQuestSourceBinding(binding, target, expected))
                {
                    UnityEngine.Object.DestroyImmediate(binding, true);
                    removed++;
                }
            }

            return removed;
        }

        private static bool ShouldRemoveDuplicateQuestSourceBinding(QuestSourceSceneBinding binding, GameObject target, PrototypeQuestSourceBindingExpectation expected)
        {
            return binding != null
                && binding.gameObject != target
                && string.Equals(binding.QuestSourceId, expected.QuestSourceId, StringComparison.Ordinal);
        }

        private static int RemoveLegacyCounterBindingComponents(GameObject root = null)
        {
            IEnumerable<WorldSceneBindingComponent> worldBindings = root != null
                ? root.GetComponentsInChildren<WorldSceneBindingComponent>(true)
                : UnityEngine.Object.FindObjectsByType<WorldSceneBindingComponent>(FindObjectsInactive.Include);
            IEnumerable<QuestSourceSceneBinding> questSources = root != null
                ? root.GetComponentsInChildren<QuestSourceSceneBinding>(true)
                : UnityEngine.Object.FindObjectsByType<QuestSourceSceneBinding>(FindObjectsInactive.Include);

            int removed = 0;
            foreach (WorldSceneBindingComponent binding in worldBindings.ToArray())
            {
                if (binding != null && IsLegacyPhysicalInteractionWorldBinding(binding))
                {
                    UnityEngine.Object.DestroyImmediate(binding, true);
                    removed++;
                }
            }

            foreach (QuestSourceSceneBinding binding in questSources.ToArray())
            {
                if (binding != null && IsLegacyPhysicalInteractionQuestSourceBinding(binding))
                {
                    UnityEngine.Object.DestroyImmediate(binding, true);
                    removed++;
                }
            }

            if (removed > 0 && root != null)
            {
                EditorUtility.SetDirty(root);
            }

            return removed;
        }

        private static bool IsLegacyPhysicalInteractionWorldBinding(WorldSceneBindingComponent binding)
        {
            string targetName = ExpectedPhysicalInteractionObjectName(binding.LogicalId);
            return targetName != null
                && (!string.Equals(binding.gameObject.name, targetName, StringComparison.Ordinal) || binding.GetComponentInChildren<Collider>(true) == null);
        }

        private static bool IsLegacyPhysicalInteractionQuestSourceBinding(QuestSourceSceneBinding binding)
        {
            string targetName = ExpectedPhysicalInteractionObjectName(binding.InteractionPointId);
            return targetName != null
                && (!string.Equals(binding.gameObject.name, targetName, StringComparison.Ordinal) || binding.GetComponentInChildren<Collider>(true) == null);
        }

        private static string ExpectedPhysicalInteractionObjectName(string interactionPointId)
        {
            return interactionPointId switch
            {
                PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterPointId => "AdventurerGuildCounter",
                PrototypeInteractionPointDefinitionFactory.MerchantGuildCounterPointId => "MerchantGuildCounter",
                PrototypeInteractionPointDefinitionFactory.QuestBoardPointId => "Quest Board",
                _ => null
            };
        }

        private static int EnsureInteractionTriggerColliders(GameObject target, string interactionPointId)
        {
            if (target == null)
            {
                return 0;
            }

            int changes = 0;
            bool useAuthoredChildColliders = interactionPointId == PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterPointId
                || interactionPointId == PrototypeInteractionPointDefinitionFactory.MerchantGuildCounterPointId;
            Collider[] authoredChildren = target.GetComponentsInChildren<Collider>(true)
                .Where(item => item.transform != target.transform)
                .ToArray();
            if (useAuthoredChildColliders && authoredChildren.Length > 0)
            {
                foreach (BoxCollider rootCollider in target.GetComponents<BoxCollider>())
                {
                    UnityEngine.Object.DestroyImmediate(rootCollider, true);
                    changes++;
                }

                foreach (Collider collider in authoredChildren)
                {
                    if (!collider.isTrigger)
                    {
                        collider.isTrigger = true;
                        changes++;
                    }

                    if (collider.providesContacts)
                    {
                        collider.providesContacts = false;
                        changes++;
                    }

                    EditorUtility.SetDirty(collider);
                }

                if (changes > 0)
                {
                    EditorUtility.SetDirty(target);
                }

                return changes;
            }

            BoxCollider[] rootColliders = target.GetComponents<BoxCollider>();
            BoxCollider interactionCollider = rootColliders
                .OrderBy(item => item.size.x * item.size.y * item.size.z)
                .FirstOrDefault();
            if (interactionCollider == null)
            {
                interactionCollider = target.AddComponent<BoxCollider>();
                changes++;
            }

            foreach (BoxCollider duplicate in rootColliders)
            {
                if (duplicate == interactionCollider)
                {
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(duplicate, true);
                changes++;
            }

            if (TryGetInteractionColliderProfile(interactionPointId, out Vector3 center, out Vector3 size))
            {
                if (interactionCollider.center != center)
                {
                    interactionCollider.center = center;
                    changes++;
                }

                if (interactionCollider.size != size)
                {
                    interactionCollider.size = size;
                    changes++;
                }
            }

            if (!interactionCollider.isTrigger)
            {
                interactionCollider.isTrigger = true;
                changes++;
            }

            if (interactionCollider.providesContacts)
            {
                interactionCollider.providesContacts = false;
                changes++;
            }

            // These authored counters and board are interaction surfaces, not movement blockers.
            // Keep imported child meshes queryable without letting their generated colliders stop
            // the player. Purpose-built blockers can still be authored outside this binding root.
            if (ExpectedPhysicalInteractionObjectName(interactionPointId) != null)
            {
                foreach (Collider collider in target.GetComponentsInChildren<Collider>(true))
                {
                    if (!collider.isTrigger)
                    {
                        collider.isTrigger = true;
                        changes++;
                    }

                    if (collider.providesContacts)
                    {
                        collider.providesContacts = false;
                        changes++;
                    }

                    EditorUtility.SetDirty(collider);
                }
            }

            if (changes > 0)
            {
                EditorUtility.SetDirty(interactionCollider);
                EditorUtility.SetDirty(target);
            }

            return changes;
        }

        private static bool TryGetInteractionColliderProfile(string interactionPointId, out Vector3 center, out Vector3 size)
        {
            center = Vector3.zero;
            size = Vector3.one;
            switch (interactionPointId)
            {
                case PrototypeInteractionPointDefinitionFactory.MayorDeskPointId:
                case PrototypeInteractionPointDefinitionFactory.GuildHeadDeskPointId:
                case PrototypeInteractionPointDefinitionFactory.RecordsDeskPointId:
                    center = new Vector3(0f, 0.4f, 0f);
                    size = new Vector3(2.2f, 1.6f, 1.2f);
                    return true;
                case PrototypeInteractionPointDefinitionFactory.PrisonCellPointId:
                    center = new Vector3(0f, 1f, 0f);
                    size = new Vector3(1.25f, 2.4f, 0.6f);
                    return true;
                default:
                    return false;
            }
        }

        private static int EnsureConnectionInteractionArea(GameObject target)
        {
            if (target == null)
            {
                return 0;
            }

            int changes = 0;
            Transform areaTransform = target.transform.Find(InteractionAreaName);
            if (areaTransform == null)
            {
                areaTransform = new GameObject(InteractionAreaName).transform;
                areaTransform.SetParent(target.transform, false);
                changes++;
            }

            BoxCollider area = areaTransform.GetComponent<BoxCollider>();
            if (area == null)
            {
                area = areaTransform.gameObject.AddComponent<BoxCollider>();
                changes++;
            }

            Vector3 desiredCenter = new Vector3(0f, 0.75f, 0f);
            Vector3 desiredSize = new Vector3(1.6f, 2.5f, 1f);
            if (area.center != desiredCenter)
            {
                area.center = desiredCenter;
                changes++;
            }

            if (area.size != desiredSize)
            {
                area.size = desiredSize;
                changes++;
            }

            if (!area.isTrigger)
            {
                area.isTrigger = true;
                changes++;
            }

            if (area.providesContacts)
            {
                area.providesContacts = false;
                changes++;
            }

            if (changes > 0)
            {
                EditorUtility.SetDirty(areaTransform.gameObject);
                EditorUtility.SetDirty(area);
            }

            return changes;
        }

        private static int RemoveLegacyCounterBindingObjects(GameObject root)
        {
            if (root == null)
            {
                return 0;
            }

            int removed = 0;
            foreach (string objectName in LegacyCounterBindingObjectNames)
            {
                foreach (GameObject legacy in FindChildrenByName(root.transform, objectName).ToArray())
                {
                    UnityEngine.Object.DestroyImmediate(legacy, true);
                    removed++;
                }
            }

            if (removed > 0)
            {
                EditorUtility.SetDirty(root);
            }

            return removed;
        }

        private static int RemoveMissingScripts(IEnumerable<GameObject> roots)
        {
            int removed = 0;
            foreach (GameObject root in roots ?? Array.Empty<GameObject>())
            {
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);
                }
            }

            return removed;
        }

        private static GameObject EnsurePath(Scene scene, string path)
        {
            string[] parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            GameObject current = scene.GetRootGameObjects().FirstOrDefault(item => string.Equals(item.name, parts[0], StringComparison.Ordinal));
            if (current == null)
            {
                current = new GameObject(parts[0]);
                SceneManager.MoveGameObjectToScene(current, scene);
            }

            Transform parent = current.transform;
            for (int i = 1; i < parts.Length; i++)
            {
                parent = EnsureChild(parent, parts[i]);
            }

            return parent.gameObject;
        }

        private static Transform EnsureChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (string.Equals(child.name, name, StringComparison.Ordinal))
                {
                    return child;
                }
            }

            GameObject created = new GameObject(name);
            created.transform.SetParent(parent, false);
            EditorUtility.SetDirty(created);
            return created.transform;
        }

        private static GameObject CreatePlaceholder(Transform parent, string name, WorldSceneBindingCategory category)
        {
            GameObject placeholder = GameObject.CreatePrimitive(PrimitiveType.Cube);
            placeholder.name = name;
            placeholder.transform.SetParent(parent, false);
            placeholder.transform.localScale = category == WorldSceneBindingCategory.Location ? new Vector3(2f, 0.15f, 2f) : new Vector3(0.75f, 0.75f, 0.75f);
            placeholder.transform.localPosition = NextPlaceholderPosition(parent);
            EditorUtility.SetDirty(placeholder);
            return placeholder;
        }

        private static Vector3 NextPlaceholderPosition(Transform parent)
        {
            int index = parent == null ? 0 : parent.childCount;
            return new Vector3((index % 5) * 2.0f, 0.5f, (index / 5) * 2.0f);
        }

        private static string PreferredExistingName(PrototypeSceneWorldBindingExpectation expected)
        {
            return expected.DisplayName switch
            {
                "Prototype Player" => "Prototype Player",
                "Dungeon Entry" => "Dungeon1",
                "Adventurer Guild" => "AdventurerGuildBuilding",
                "Adventurer Guild Counter" => "AdventurerGuildCounter",
                "Merchant Guild Counter" => "MerchantGuildCounter",
                "Adventurer Guild Quest Board" => "Quest Board",
                "Mayor Desk" => "Interact - Mayor Desk",
                "Guild Head Desk" => "Interact - Guild Head Desk",
                "City Records Desk" => "Interact - City Office Records Desk",
                "Prison Cell" => "Interact - Prison Cell A Door",
                _ => expected.DisplayName
            };
        }

        private static string PreferredExistingQuestSourceName(PrototypeQuestSourceBindingExpectation expected)
        {
            return expected.InteractionPointId switch
            {
                PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterPointId => "AdventurerGuildCounter",
                PrototypeInteractionPointDefinitionFactory.MerchantGuildCounterPointId => "MerchantGuildCounter",
                PrototypeInteractionPointDefinitionFactory.MayorDeskPointId => "Interact - Mayor Desk",
                PrototypeInteractionPointDefinitionFactory.RecordsDeskPointId => "Interact - City Office Records Desk",
                PrototypeInteractionPointDefinitionFactory.QuestBoardPointId => "Quest Board",
                _ => expected.DisplayName
            };
        }

        private static string PlaceholderName(PrototypeSceneWorldBindingExpectation expected)
        {
            return expected.Category switch
            {
                WorldSceneBindingCategory.Connection => $"Connection - {expected.DisplayName}",
                WorldSceneBindingCategory.Entity => $"Entity - {expected.DisplayName}",
                _ => expected.DisplayName
            };
        }

        private static string CategoryFolder(WorldSceneBindingCategory category)
        {
            return category switch
            {
                WorldSceneBindingCategory.Location => "Locations",
                WorldSceneBindingCategory.InteractionPoint => "Interaction Points",
                WorldSceneBindingCategory.Connection => "Connections",
                WorldSceneBindingCategory.Entity => "Entities",
                _ => "Other Bindings"
            };
        }

        private static GameObject FindLoadedObject(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            return UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include)
                .Select(item => item.gameObject)
                .FirstOrDefault(item => string.Equals(item.name, name, StringComparison.Ordinal));
        }

        private static bool TryResolveAdventurerGuildPrefabTarget(GameObject root, PrototypeSceneWorldBindingExpectation expected, out GameObject target)
        {
            target = FindChildByName(root != null ? root.transform : null, PreferredExistingName(expected));
            return target != null;
        }

        private static bool TryResolveAdventurerGuildPrefabQuestSourceTarget(GameObject root, PrototypeQuestSourceBindingExpectation expected, out GameObject target)
        {
            target = FindChildByName(root != null ? root.transform : null, PreferredExistingQuestSourceName(expected));
            return target != null;
        }

        private static GameObject FindChildByName(Transform root, string name)
        {
            if (root == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (string.Equals(child.name, name, StringComparison.Ordinal))
                {
                    return child.gameObject;
                }
            }

            return null;
        }

        private static IEnumerable<GameObject> FindChildrenByName(Transform root, string name)
        {
            if (root == null || string.IsNullOrWhiteSpace(name))
            {
                yield break;
            }

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (string.Equals(child.name, name, StringComparison.Ordinal))
                {
                    yield return child.gameObject;
                }
            }
        }

        private static bool TryParseEntityKey(string logicalId, out EntityLocationReferenceData entity)
        {
            entity = new EntityLocationReferenceData();
            string[] parts = (logicalId ?? string.Empty).Split(':');
            if (parts.Length < 3 || !Enum.TryParse(parts[0], out LocationOccupantEntityType type))
            {
                return false;
            }

            entity = new EntityLocationReferenceData
            {
                entityType = type,
                worldId = parts[1],
                entityId = string.Join(":", parts.Skip(2))
            };
            return true;
        }
    }

    public readonly struct PrototypeSceneIntegrationApplyResult
    {
        public PrototypeSceneIntegrationApplyResult(int sceneChanges, int placeholdersCreated, int missingScriptsRemoved)
        {
            SceneChanges = sceneChanges;
            PlaceholdersCreated = placeholdersCreated;
            MissingScriptsRemoved = missingScriptsRemoved;
        }

        public int SceneChanges { get; }
        public int PlaceholdersCreated { get; }
        public int MissingScriptsRemoved { get; }
    }
}
#endif
