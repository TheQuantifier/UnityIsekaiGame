using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityIsekaiGame.ActorLifecycle;
using UnityIsekaiGame.Beings;
using UnityIsekaiGame.Beings.Biology;
using UnityIsekaiGame.CharacterSystem;
using UnityIsekaiGame.Combat.OngoingEffects;
using UnityIsekaiGame.Equipment;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.Progression;
using UnityIsekaiGame.ResourceSystem;
using UnityIsekaiGame.Skills;
using UnityIsekaiGame.Stats;
using UnityIsekaiGame.StatusEffects;
using UnityIsekaiGame.Traits;
using UnityIsekaiGame.UI.Inventory;
using UnityIsekaiGame.WorldEntities;
using UnityIsekaiGame.WorldLocations.SceneBinding;

namespace UnityIsekaiGame.Editor
{
    public static class PrototypePersistenceSceneAuthoring
    {
        private const string PrototypeScenePath = "Assets/_Project/Scenes/Prototype/PrototypeScene.unity";
        private const string PrototypeCatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";
        private const string HumanSpeciesPath = "Assets/_Project/Content/Actors/Beings/Species/HumanSpecies.asset";

        [MenuItem("Tools/Persistence/Author Prototype Persistence Dependencies")]
        public static void AuthorPrototypePersistenceDependencies()
        {
            Scene scene = EditorSceneManager.OpenScene(PrototypeScenePath, OpenSceneMode.Single);
            PrototypePersistenceServiceBehaviour persistence = UnityEngine.Object.FindAnyObjectByType<PrototypePersistenceServiceBehaviour>(FindObjectsInactive.Include);
            PlayerInventory inventory = UnityEngine.Object.FindAnyObjectByType<PlayerInventory>(FindObjectsInactive.Include);
            if (persistence == null || inventory == null)
            {
                throw new InvalidOperationException("Prototype scene must contain the persistence coordinator and player inventory.");
            }

            GameObject player = inventory.gameObject;
            PlayerEquipment equipment = Required<PlayerEquipment>(player);
            PlayerStats playerStats = Required<PlayerStats>(player);
            PlayerHealth health = Required<PlayerHealth>(player);
            PlayerMana mana = Required<PlayerMana>(player);
            PlayerStamina stamina = Required<PlayerStamina>(player);
            StatusEffectController statuses = Required<StatusEffectController>(player);
            CharacterAttributes attributes = Required<CharacterAttributes>(player);
            CalculatedStatCollection calculatedStats = Required<CalculatedStatCollection>(player);
            CharacterResourceCollection resources = Required<CharacterResourceCollection>(player);
            ActorLifecycleController lifecycle = Required<ActorLifecycleController>(player);
            OngoingEffectService ongoingEffects = Required<OngoingEffectService>(player);
            CharacterSkillCollection skills = Required<CharacterSkillCollection>(player);
            CharacterTraitCollection traits = Required<CharacterTraitCollection>(player);
            ActorBodyRuntime body = Required<ActorBodyRuntime>(player);
            PersonKnowledgeRuntime knowledge = Required<PersonKnowledgeRuntime>(player);
            PlayerIdentityProgression identity = Required<PlayerIdentityProgression>(player);
            PlayerSkillActionEventSource skillEvents = Required<PlayerSkillActionEventSource>(player);
            PlayerItemIdentitySynchronizer identitySynchronizer = Required<PlayerItemIdentitySynchronizer>(player);
            CharacterSystemCoordinator character = Required<CharacterSystemCoordinator>(player);
            WorldEntityIdentity worldEntity = Required<WorldEntityIdentity>(player);

            GameObject coordinator = persistence.gameObject;
            PlayTimeTracker playTime = Required<PlayTimeTracker>(coordinator);
            GameSaveDirtyTracker dirty = Required<GameSaveDirtyTracker>(coordinator);
            AutosaveCoordinator autosave = Required<AutosaveCoordinator>(coordinator);
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(PrototypeCatalogPath);
            SpeciesDefinition humanSpecies = AssetDatabase.LoadAssetAtPath<SpeciesDefinition>(HumanSpeciesPath);

            Wire(calculatedStats, "attributes", attributes);
            Wire(resources, "calculatedStats", calculatedStats);
            Wire(skills, "calculatedStats", calculatedStats);
            Wire(traits, "calculatedStats", calculatedStats);
            Wire(traits, "skills", skills);
            Wire(lifecycle, "resources", resources);
            Wire(lifecycle, "traits", traits);
            Wire(body, "defaultSpecies", humanSpecies);
            Wire(identity, "characterAttributes", attributes);
            Wire(identity, "calculatedStats", calculatedStats);
            Wire(identity, "skillCollection", skills);
            Wire(identity, "playTimeTracker", playTime);
            Wire(skillEvents, "skills", skills);
            Wire(skillEvents, "identity", identity);
            Wire(skillEvents, "playTimeTracker", playTime);
            Wire(playerStats, "characterAttributes", attributes);
            Wire(playerStats, "calculatedStats", calculatedStats);
            Wire(health, "resources", resources);
            Wire(mana, "resources", resources);
            Wire(stamina, "resources", resources);
            Wire(identitySynchronizer, "inventory", inventory);
            Wire(identitySynchronizer, "equipment", equipment);
            Wire(identitySynchronizer, "definitionCatalog", catalog);

            Wire(character, "identity", identity);
            Wire(character, "actorStats", playerStats);
            Wire(character, "attributes", attributes);
            Wire(character, "calculatedStats", calculatedStats);
            Wire(character, "resources", resources);
            Wire(character, "skills", skills);
            Wire(character, "traits", traits);
            Wire(character, "body", body);
            Wire(character, "statuses", statuses);
            Wire(character, "inventory", inventory);
            Wire(character, "equipment", equipment);
            Wire(character, "worldEntityIdentity", worldEntity);

            Wire(persistence, "definitionCatalog", catalog);
            Wire(persistence, "playerInventory", inventory);
            Wire(persistence, "playerEquipment", equipment);
            Wire(persistence, "playerStats", playerStats);
            Wire(persistence, "playerHealth", health);
            Wire(persistence, "playerMana", mana);
            Wire(persistence, "playerStamina", stamina);
            Wire(persistence, "playerAttributes", attributes);
            Wire(persistence, "playerCalculatedStats", calculatedStats);
            Wire(persistence, "playerResources", resources);
            Wire(persistence, "playerActorLifecycle", lifecycle);
            Wire(persistence, "playerOngoingEffects", ongoingEffects);
            Wire(persistence, "playerSkills", skills);
            Wire(persistence, "playerTraits", traits);
            Wire(persistence, "playerBody", body);
            Wire(persistence, "playerKnowledge", knowledge);
            Wire(persistence, "playerSkillActionEventSource", skillEvents);
            Wire(persistence, "statusEffectController", statuses);
            Wire(persistence, "playerIdentityProgression", identity);
            Wire(persistence, "playerRoot", player.transform);
            Wire(persistence, "playTimeTracker", playTime);
            Wire(persistence, "dirtyTracker", dirty);
            Wire(persistence, "autosaveCoordinator", autosave);

            InventoryScreenController inventoryScreen = UnityEngine.Object.FindAnyObjectByType<InventoryScreenController>(FindObjectsInactive.Include);
            if (inventoryScreen != null)
            {
                Wire(inventoryScreen, "identityProgression", identity);
                Wire(inventoryScreen, "playerSkills", skills);
                Wire(inventoryScreen, "playerTraits", traits);
                Wire(persistence, "inventoryScreenController", inventoryScreen);
            }

            foreach (WorldSceneBindingBootstrap bootstrap in UnityEngine.Object.FindObjectsByType<WorldSceneBindingBootstrap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                SetEnum(bootstrap, "bootstrapMode", (int)WorldSceneBindingBootstrapMode.ProductionBindOnly);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Authored and wired explicit Prototype player and persistence dependencies.");
        }

        private static T Required<T>(GameObject owner) where T : Component
        {
            T component = owner.GetComponent<T>();
            return component == null ? Undo.AddComponent<T>(owner) : component;
        }

        private static void Wire(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            if (target == null)
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new MissingFieldException(target.GetType().FullName, propertyName);
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetEnum(UnityEngine.Object target, string propertyName, int value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName) ?? throw new MissingFieldException(target.GetType().FullName, propertyName);
            property.enumValueIndex = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }
}
