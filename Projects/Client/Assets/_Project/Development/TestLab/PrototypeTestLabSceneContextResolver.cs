#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityIsekaiGame.ActorLifecycle;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.Combat.CombatState;
using UnityIsekaiGame.Combat.OngoingEffects;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Magic;
using UnityIsekaiGame.Progression;
using UnityIsekaiGame.ResourceSystem;
using UnityIsekaiGame.StatusEffects;
using UnityIsekaiGame.UI.Inventory;

namespace UnityIsekaiGame.Development
{
    public static class PrototypeTestLabSceneContextResolver
    {
        public static PrototypeTestLabContext Resolve(InventoryScreenController menuController = null)
        {
            if (menuController == null)
            {
                menuController = Object.FindAnyObjectByType<InventoryScreenController>(FindObjectsInactive.Include);
            }

            DefinitionCatalog catalog = menuController == null ? Object.FindAnyObjectByType<DefinitionCatalog>() : menuController.RuntimeDefinitionCatalog;
            PrototypePersistenceServiceBehaviour persistence = menuController == null ? Object.FindAnyObjectByType<PrototypePersistenceServiceBehaviour>() : menuController.ResolveRuntimePersistence();
            EnemyHealth enemyHealth = Object.FindAnyObjectByType<EnemyHealth>();
            Transform playerTransform = ResolvePlayerTransform(menuController);
            Transform enemyTransform = enemyHealth == null ? Object.FindAnyObjectByType<PrototypeEnemyController>()?.transform : enemyHealth.transform;
            PlayerIdentityProgression identityProgression = ResolveIdentityProgression(menuController, playerTransform);

            CombatStateService combatState = playerTransform == null ? Object.FindAnyObjectByType<CombatStateService>() : playerTransform.GetComponentInParent<CombatStateService>(includeInactive: true);
            if (combatState == null && playerTransform != null && playerTransform.gameObject.activeInHierarchy)
            {
                combatState = playerTransform.gameObject.AddComponent<CombatStateService>();
            }

            OngoingEffectService playerOngoingEffects = ResolveOrAddOngoingEffects(playerTransform);
            OngoingEffectService enemyOngoingEffects = ResolveOrAddOngoingEffects(enemyTransform);

            return new PrototypeTestLabContext
            {
                DefinitionCatalog = catalog,
                Inventory = menuController?.Inventory,
                Equipment = menuController?.Equipment,
                PlayerStats = menuController?.PlayerStats,
                PlayerHealth = menuController?.PlayerHealth,
                PlayerMana = menuController?.PlayerMana,
                PlayerStamina = menuController?.PlayerStamina,
                PlayerAttributes = menuController?.PlayerStats == null ? null : menuController.PlayerStats.CharacterAttributes,
                PlayerCalculatedStats = menuController?.PlayerStats == null ? null : menuController.PlayerStats.CalculatedStats,
                PlayerResources = playerTransform == null ? null : playerTransform.GetComponentInParent<CharacterResourceCollection>(),
                PlayerLifecycle = playerTransform == null ? null : playerTransform.GetComponentInParent<ActorLifecycleController>(),
                CombatState = combatState,
                PlayerOngoingEffects = playerOngoingEffects,
                PlayerSkills = menuController?.RuntimeSkills,
                PlayerTraits = menuController?.RuntimeTraits,
                PlayerKnowledge = playerTransform == null ? null : playerTransform.GetComponentInParent<PersonKnowledgeRuntime>(),
                InformationAccess = persistence?.InformationAccess,
                KnowledgeRecords = persistence?.KnowledgeRecords,
                CharacterSystem = menuController?.RuntimeCharacterSystem,
                PlayerStatuses = menuController?.StatusEffects,
                IdentityProgression = identityProgression,
                Spellcaster = playerTransform == null ? null : playerTransform.GetComponentInParent<PlayerSpellcaster>(),
                SpellLoadout = menuController?.SpellLoadout,
                Narrative = persistence?.NarrativeCoordinator,
                TestController = Object.FindAnyObjectByType<PrototypeTestController>(),
                Persistence = persistence,
                PlayerTransform = playerTransform,
                EnemyHealth = enemyHealth,
                EnemyController = enemyTransform == null ? null : enemyTransform.GetComponent<PrototypeEnemyController>(),
                EnemyAttack = enemyTransform == null ? null : enemyTransform.GetComponent<EnemyMeleeAttack>(),
                EnemyLifecycle = enemyTransform == null ? null : enemyTransform.GetComponent<ActorLifecycleController>(),
                EnemyOngoingEffects = enemyOngoingEffects,
                EnemyStatuses = enemyTransform == null ? null : enemyTransform.GetComponent<StatusEffectController>(),
                EnemyTransform = enemyTransform
            };
        }

        private static Transform ResolvePlayerTransform(InventoryScreenController menuController)
        {
            if (menuController != null)
            {
                if (menuController.ItemUser != null)
                {
                    return menuController.ItemUser.transform;
                }

                if (menuController.Inventory != null)
                {
                    return menuController.Inventory.transform;
                }
            }

            GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            if (taggedPlayer != null)
            {
                return taggedPlayer.transform;
            }

            PlayerSpellcaster spellcaster = Object.FindAnyObjectByType<PlayerSpellcaster>();
            return spellcaster == null ? null : spellcaster.transform;
        }

        private static PlayerIdentityProgression ResolveIdentityProgression(InventoryScreenController menuController, Transform playerTransform)
        {
            if (menuController?.IdentityProgression != null)
            {
                return menuController.IdentityProgression;
            }

            PlayerIdentityProgression identity = playerTransform == null ? null : playerTransform.GetComponentInParent<PlayerIdentityProgression>(includeInactive: true);
            if (identity == null)
            {
                identity = Object.FindAnyObjectByType<PlayerIdentityProgression>(FindObjectsInactive.Include);
            }

            if (identity == null && playerTransform != null && playerTransform.gameObject.activeInHierarchy)
            {
                identity = playerTransform.gameObject.AddComponent<PlayerIdentityProgression>();
            }

            return identity;
        }

        private static OngoingEffectService ResolveOrAddOngoingEffects(Transform owner)
        {
            if (owner == null)
            {
                return null;
            }

            OngoingEffectService service = owner.GetComponent<OngoingEffectService>() ?? owner.GetComponentInParent<OngoingEffectService>(includeInactive: true);
            if (service == null && owner.gameObject.activeInHierarchy)
            {
                service = owner.gameObject.AddComponent<OngoingEffectService>();
            }

            return service;
        }
    }
}
#endif
