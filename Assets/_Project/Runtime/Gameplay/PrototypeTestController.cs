using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.ActorLifecycle;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.Equipment;
using UnityIsekaiGame.Input;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Loot;
using UnityIsekaiGame.Magic;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.ResourceSystem;
using UnityIsekaiGame.StatusEffects;

namespace UnityIsekaiGame.Gameplay
{
    public sealed class PrototypeTestController : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private Transform player;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private PlayerStamina playerStamina;
        [SerializeField] private PlayerMana playerMana;
        [SerializeField] private PlayerMeleeCombat playerMeleeCombat;
        [SerializeField] private PlayerSpellcaster playerSpellcaster;
        [SerializeField] private StatusEffectController playerStatusEffects;
        [SerializeField] private CharacterResourceCollection playerResources;
        [SerializeField] private ActorLifecycleController playerLifecycle;
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private PlayerEquipment playerEquipment;
        [SerializeField] private MonoBehaviour inventoryScreenController;
        [SerializeField] private Transform playerSpawnPoint;
        [SerializeField] private Transform prototypeEnemy;
        [SerializeField] private EnemyHealth enemyHealth;
        [SerializeField] private EnemyMeleeAttack enemyAttack;
        [SerializeField] private PrototypeEnemyController enemyController;
        [SerializeField] private EnemyLootDrop enemyLootDrop;
        [SerializeField] private StatusEffectController enemyStatusEffects;
        [SerializeField] private CharacterResourceCollection enemyResources;
        [SerializeField] private ActorLifecycleController enemyLifecycle;

        private readonly List<PickupResetState> pickupResetStates = new List<PickupResetState>();
        private Vector3 fallbackPlayerSpawnPosition;
        private Quaternion fallbackPlayerSpawnRotation;
        private Vector3 enemyStartPosition;
        private Quaternion enemyStartRotation;
        private bool enemyStartActive;

        private void Awake()
        {
            ResolveRuntimeReferences();
            CaptureSceneResetState();
        }

        private void Start()
        {
            GroundPlayerAtSceneEntry();
        }

        private void Update()
        {
            if (input != null && input.ConsumePrototypeReset())
            {
                ResetPrototypeState();
            }
        }

        public void ResetPrototypeState()
        {
            FindAnyObjectByType<PrototypeDialoguePanel>()?.Close();
            FindAnyObjectByType<PrototypeQuestSourcePanel>()?.Close();
            (inventoryScreenController as IPlayerMenuController)?.CloseForPrototypeReset();
            RestoreSceneResetState();
            ResolveRuntimeReferences();
            ResetPlayerPosition();
            ResetPlayerState();
            input?.SetDefeatedInputBlocked(false);
            input?.ClearGameplayActionQueues();

            ResetEnemy();
            Debug.Log("Prototype reset complete.");
            GameHudMessageBus.Show("Prototype reset complete");
        }

        private void GroundPlayerAtSceneEntry()
        {
            if (LocationRestoreGuard.IsRestoringLocation)
            {
                return;
            }

            ResolveRuntimeReferences();
            MovePlayerToSpawnTarget();
        }

        private void ResolveRuntimeReferences()
        {
            if (input == null)
            {
                input = FindAnyObjectByType<PlayerInputReader>();
            }

            if (player == null && playerHealth != null)
            {
                player = playerHealth.transform;
            }

            if (playerHealth == null && player != null)
            {
                playerHealth = player.GetComponent<PlayerHealth>();
            }

            if (playerStamina == null && player != null)
            {
                playerStamina = player.GetComponent<PlayerStamina>();
            }

            if (playerMana == null && player != null)
            {
                playerMana = player.GetComponent<PlayerMana>();
            }

            if (playerMeleeCombat == null && player != null)
            {
                playerMeleeCombat = player.GetComponent<PlayerMeleeCombat>();
            }

            if (playerSpellcaster == null && player != null)
            {
                playerSpellcaster = player.GetComponent<PlayerSpellcaster>();
            }

            if (playerStatusEffects == null && player != null)
            {
                playerStatusEffects = player.GetComponent<StatusEffectController>();
            }

            if (playerResources == null && player != null)
            {
                playerResources = player.GetComponent<CharacterResourceCollection>();
            }

            if (playerLifecycle == null && player != null)
            {
                playerLifecycle = player.GetComponent<ActorLifecycleController>();
            }

            if (playerInventory == null && player != null)
            {
                playerInventory = player.GetComponent<PlayerInventory>();
            }

            if (playerEquipment == null && player != null)
            {
                playerEquipment = player.GetComponent<PlayerEquipment>();
            }

            if (inventoryScreenController == null)
            {
                inventoryScreenController = FindMenuController();
            }

            if (prototypeEnemy == null && enemyHealth != null)
            {
                prototypeEnemy = enemyHealth.transform;
            }

            if (prototypeEnemy == null)
            {
                EnemyHealth[] enemies = FindObjectsByType<EnemyHealth>(FindObjectsInactive.Include);
                if (enemies.Length > 0)
                {
                    prototypeEnemy = enemies[0].transform;
                    enemyHealth = enemies[0];
                }
            }

            if (enemyHealth == null && prototypeEnemy != null)
            {
                enemyHealth = prototypeEnemy.GetComponent<EnemyHealth>();
            }

            if (enemyAttack == null && prototypeEnemy != null)
            {
                enemyAttack = prototypeEnemy.GetComponent<EnemyMeleeAttack>();
            }

            if (enemyController == null && prototypeEnemy != null)
            {
                enemyController = prototypeEnemy.GetComponent<PrototypeEnemyController>();
            }

            if (enemyLootDrop == null && prototypeEnemy != null)
            {
                enemyLootDrop = prototypeEnemy.GetComponent<EnemyLootDrop>();
            }

            if (enemyStatusEffects == null && prototypeEnemy != null)
            {
                enemyStatusEffects = prototypeEnemy.GetComponent<StatusEffectController>();
            }

            if (enemyResources == null && prototypeEnemy != null)
            {
                enemyResources = prototypeEnemy.GetComponent<CharacterResourceCollection>();
            }

            if (enemyLifecycle == null && prototypeEnemy != null)
            {
                enemyLifecycle = prototypeEnemy.GetComponent<ActorLifecycleController>();
            }
        }

        private void CaptureSceneResetState()
        {
            fallbackPlayerSpawnPosition = player == null ? Vector3.zero : player.position;
            fallbackPlayerSpawnRotation = player == null ? Quaternion.identity : player.rotation;
            enemyStartPosition = prototypeEnemy == null ? Vector3.zero : prototypeEnemy.position;
            enemyStartRotation = prototypeEnemy == null ? Quaternion.identity : prototypeEnemy.rotation;
            enemyStartActive = prototypeEnemy == null || prototypeEnemy.gameObject.activeSelf;

            pickupResetStates.Clear();
            WorldItemPickup[] pickups = FindObjectsByType<WorldItemPickup>(FindObjectsInactive.Include);
            for (int i = 0; i < pickups.Length; i++)
            {
                if (pickups[i] == null)
                {
                    continue;
                }

                pickupResetStates.Add(new PickupResetState(pickups[i]));
            }
        }

        private void ResetPlayerPosition()
        {
            MovePlayerToSpawnTarget();
        }

        private void MovePlayerToSpawnTarget()
        {
            if (player == null)
            {
                return;
            }

            CharacterController characterController = player.GetComponent<CharacterController>();
            if (characterController != null)
            {
                characterController.enabled = false;
            }

            Transform spawn = playerSpawnPoint;
            Vector3 targetPosition = spawn == null ? fallbackPlayerSpawnPosition : spawn.position;
            if (!SpawnGroundingUtility.TryGroundWhenUnsupported(targetPosition, player, out targetPosition, out _))
            {
                targetPosition = spawn == null ? fallbackPlayerSpawnPosition : spawn.position;
            }

            player.SetPositionAndRotation(
                targetPosition,
                spawn == null ? fallbackPlayerSpawnRotation : spawn.rotation);

            if (characterController != null)
            {
                characterController.enabled = true;
            }

            ClearRigidbodyMotion(player);
        }

        private void ResetPlayerState()
        {
            playerStatusEffects?.ClearAllStatuses();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            playerEquipment?.DevelopmentClearEquipment();
            playerInventory?.DevelopmentClearInventory();
#endif

            playerResources?.ResetToDefinitionDefaults("prototype.reset.player", "Prototype reset.", restoration: true);
            playerLifecycle?.ResetToActiveForRestore();
            playerHealth?.ResetToMaximum();
            playerStamina?.RestoreToMaximum();
            playerMana?.RestoreToMaximum();
            playerMeleeCombat?.ResetCooldown();
            playerSpellcaster?.ResetSpellcasting();
        }

        private static MonoBehaviour FindMenuController()
        {
            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IPlayerMenuController)
                {
                    return behaviours[i];
                }
            }

            return null;
        }

        private void ResetEnemy()
        {
            if (prototypeEnemy != null)
            {
                prototypeEnemy.gameObject.SetActive(enemyStartActive);
                prototypeEnemy.SetPositionAndRotation(enemyStartPosition, enemyStartRotation);
                ClearRigidbodyMotion(prototypeEnemy);
            }

            enemyAttack?.ResetCooldown();
            enemyController?.ResetControllerState();
            enemyLootDrop?.ResetLootState();
            enemyStatusEffects?.ClearAllStatuses();
            enemyResources?.ResetToDefinitionDefaults("prototype.reset.enemy", "Prototype reset.", restoration: true);
            enemyLifecycle?.ResetToActiveForRestore();
            enemyHealth?.ResetToMaximum();
        }

        private void RestoreSceneResetState()
        {
            for (int i = 0; i < pickupResetStates.Count; i++)
            {
                pickupResetStates[i].Restore();
            }
        }

        private static void ClearRigidbodyMotion(Transform target)
        {
            if (target == null || !target.TryGetComponent(out Rigidbody body))
            {
                return;
            }

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        private readonly struct PickupResetState
        {
            private readonly WorldItemPickup pickup;
            private readonly Vector3 position;
            private readonly Quaternion rotation;
            private readonly Vector3 localScale;
            private readonly int quantity;
            private readonly bool activeSelf;

            public PickupResetState(WorldItemPickup pickup)
            {
                this.pickup = pickup;
                Transform pickupTransform = pickup.transform;
                position = pickupTransform.position;
                rotation = pickupTransform.rotation;
                localScale = pickupTransform.localScale;
                quantity = pickup.Quantity;
                activeSelf = pickup.gameObject.activeSelf;
            }

            public void Restore()
            {
                if (pickup == null)
                {
                    return;
                }

                Transform pickupTransform = pickup.transform;
                pickupTransform.SetPositionAndRotation(position, rotation);
                pickupTransform.localScale = localScale;
                pickup.ResetPickupState(quantity, activeSelf);
            }
        }
    }
}
