using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Inventory.Durability;
using UnityIsekaiGame.Inventory.Disassembly;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Inventory.Quality;
using UnityIsekaiGame.Interaction;

namespace UnityIsekaiGame.Inventory
{
    public sealed class WorldItemPickup : MonoBehaviour, IInteractable
    {
        [SerializeField] private ItemDefinition item;
        [SerializeField, Min(1)] private int quantity = 1;
        [SerializeField] private bool disableOnCollected;
        [SerializeField] private bool salvagePickup;
        [SerializeField] private string runtimeItemInstanceId;
        private bool salvageBonusResolved;

        public string InteractionPrompt => item == null ? "Pick up" : $"Pick up {quantity} x {item.DisplayName}";

        public int Quantity => quantity;
        public ItemDefinition Item => item;
        public bool DisableOnCollected => disableOnCollected;
        public string RuntimeItemInstanceId => runtimeItemInstanceId ?? string.Empty;
        public bool IsSalvagePickup => salvagePickup;

        private void OnValidate()
        {
            quantity = Mathf.Max(1, quantity);
        }

        public void Configure(ItemDefinition itemDefinition, int pickupQuantity, bool disableWhenCollected = false, bool isSalvagePickup = false)
        {
            item = itemDefinition;
            quantity = Mathf.Max(1, pickupQuantity);
            disableOnCollected = disableWhenCollected;
            salvagePickup = isSalvagePickup;
            runtimeItemInstanceId = string.Empty;
            salvageBonusResolved = false;
        }

        public void ConfigureTrackedInstance(string itemInstanceId)
        {
            runtimeItemInstanceId = itemInstanceId?.Trim() ?? string.Empty;
        }

        public void ResetPickupState(int pickupQuantity, bool active)
        {
            quantity = Mathf.Max(1, pickupQuantity);
            salvageBonusResolved = false;
            gameObject.SetActive(active);
        }

        public bool CanInteract(in InteractionContext context)
        {
            return enabled && isActiveAndEnabled && item != null && quantity > 0;
        }

        public void Interact(in InteractionContext context)
        {
            PlayerInventory inventory = FindInventory(context.Interactor);
            if (inventory == null)
            {
                Debug.LogWarning($"{name} could not find a PlayerInventory on the interactor.");
                return;
            }

            PrototypePersistenceServiceBehaviour services = FindAnyObjectByType<PrototypePersistenceServiceBehaviour>(FindObjectsInactive.Include);
            if (TryCollectTrackedRuntimeInstance(inventory, services) || TryCollectSceneAuthoredInstance(context, inventory))
            {
                return;
            }

            SalvagePickupCalculation salvage = null;
            if (!salvageBonusResolved && IsSalvageResourcePickup() && item.InstanceMode != ItemInstanceMode.AlwaysInstanced && services != null)
            {
                salvage = services.CalculateSalvagePickup(item, quantity, SalvageSourceId());
            }

            // Consume only the physical world quantity first. A successful Salvager roll then
            // grants extra recovered material without ever increasing the amount left on the ground.
            InventoryAddResult result = inventory.AddItemOrInstances(item, quantity);
            int bonusAdded = 0;
            if (result.AddedAny && salvage?.BonusApplied == true)
            {
                int requestedBonus = result.AddedQuantity * (salvage.QuantityMultiplier - 1);
                bonusAdded = inventory.AddItemOrInstances(item, requestedBonus).AddedQuantity;
            }
            int totalAdded = result.AddedQuantity + bonusAdded;

            if (result.AddedAny && salvage != null)
            {
                salvageBonusResolved = true;
                DisassemblyResult recorded = services.RecordSalvagePickup(item, salvage, result.AddedQuantity, totalAdded, SalvageSourceId());
                if (!recorded.Succeeded) Debug.LogWarning($"{name} could not record salvage pickup: {recorded.Message}");
            }

            if (result.AddedAll)
            {
                Debug.Log($"Collected all {totalAdded} x {item.ItemId} from {name}.");
                string bonus = bonusAdded > 0 ? $" (Salvager bonus: +{bonusAdded})" : string.Empty;
                PrototypeHudMessageBus.Show($"Picked up {totalAdded} x {item.DisplayName}{bonus}");
                CompletePickup();
                return;
            }

            if (result.AddedAny)
            {
                quantity = result.RemainingQuantity;
                Debug.Log($"Partial pickup from {name}. {quantity} x {item.ItemId} remain in the world.");
                PrototypeHudMessageBus.Show($"Picked up {totalAdded} x {item.DisplayName}. Inventory full.");
                return;
            }

            Debug.Log($"Inventory full. {name} remains in the world with {quantity} x {item.ItemId}.");
            PrototypeHudMessageBus.Show("Inventory full");
        }

        private bool IsSalvageResourcePickup()
        {
            if (salvagePickup) return true;
            return item != null && (string.Equals(item.PrimaryCategory?.Id, "category.item.material", System.StringComparison.Ordinal)
                || item.Tags.Any(tag => tag != null && string.Equals(tag.Id, "tag.general.material", System.StringComparison.Ordinal)));
        }

        private string SalvageSourceId()
        {
            return $"salvage-pickup.{gameObject.scene.name}.{transform.GetSiblingIndex()}.{name}.{item?.Id}";
        }

        private static PlayerInventory FindInventory(GameObject interactor)
        {
            if (interactor == null)
            {
                return null;
            }

            PlayerInventory inventory = interactor.GetComponentInParent<PlayerInventory>();
            return inventory != null ? inventory : interactor.GetComponentInChildren<PlayerInventory>();
        }

        private bool TryCollectSceneAuthoredInstance(in InteractionContext context, PlayerInventory inventory)
        {
            WorldItemQualityAffixPreset qualityPreset = GetComponent<WorldItemQualityAffixPreset>();
            WorldItemDurabilityPreset durabilityPreset = GetComponent<WorldItemDurabilityPreset>();
            if ((qualityPreset == null && durabilityPreset == null) || quantity != 1)
            {
                return false;
            }

            IItemDurabilityRuntimeProvider provider = FindItemRuntimeProvider(context.Interactor);
            if (provider == null)
            {
                Debug.LogWarning($"{name} could not prepare scene-authored item instance because no item runtime provider was found.");
                return false;
            }

            ItemInstanceRuntimeSaveData identityRollback = provider.ItemIdentities?.CreateSaveData();
            ItemQualityAffixRuntimeSaveData qualityRollback = provider.ItemQualityAffixes?.CreateSaveData();
            ItemDurabilityRuntimeSaveData durabilityRollback = provider.ItemDurability?.CreateSaveData();
            string itemInstanceId = string.Empty;
            if (qualityPreset != null && !qualityPreset.TryPreparePickupInstance(item, provider, out itemInstanceId, out string failureReason))
            {
                RestorePickupRollback(provider, identityRollback, qualityRollback, durabilityRollback);
                Debug.LogWarning($"{name} could not prepare scene-authored item quality: {failureReason}");
                return false;
            }

            if (string.IsNullOrWhiteSpace(itemInstanceId) && qualityPreset == null)
            {
                itemInstanceId = ResolveSceneAuthoredItemInstanceId();
                if (!provider.ItemIdentities.TryGetSnapshot(itemInstanceId, out _))
                {
                    ItemInstanceOperationResult create = provider.ItemIdentities.CreateItem(item, ItemInstanceClassification.WorldFixture, itemInstanceId, creationSourceId: $"scene-authored.pickup.{name}");
                    if (!create.Succeeded)
                    {
                        RestorePickupRollback(provider, identityRollback, qualityRollback, durabilityRollback);
                        Debug.LogWarning($"{name} could not prepare scene-authored item identity: {create.Message}");
                        return false;
                    }
                }
            }

            if (durabilityPreset != null && !durabilityPreset.TryPreparePickupInstance(item, provider, itemInstanceId, out string durabilityFailure))
            {
                RestorePickupRollback(provider, identityRollback, qualityRollback, durabilityRollback);
                Debug.LogWarning($"{name} could not prepare scene-authored item durability: {durabilityFailure}");
                return false;
            }

            if (!inventory.CanAddExistingItemIdentity(item, itemInstanceId))
            {
                Debug.Log($"Inventory full. {name} remains in the world with {quantity} x {item.ItemId}.");
                PrototypeHudMessageBus.Show("Inventory full");
                return true;
            }

            InventoryInstanceOperationResult result = inventory.AddExistingItemIdentity(item, itemInstanceId);
            if (!result.Succeeded)
            {
                Debug.LogWarning($"{name} could not add scene-authored item instance '{itemInstanceId}' to inventory: {result.Message}");
                PrototypeHudMessageBus.Show("Inventory full");
                return true;
            }

            Debug.Log($"Collected scene-authored {item.ItemId} instance {itemInstanceId} from {name}.");
            PrototypeHudMessageBus.Show($"Picked up {item.DisplayName}");
            CompletePickup();
            return true;
        }

        private bool TryCollectTrackedRuntimeInstance(PlayerInventory inventory, PrototypePersistenceServiceBehaviour services)
        {
            if (string.IsNullOrWhiteSpace(runtimeItemInstanceId)) return false;
            if (services == null || !services.ItemIdentities.TryGetSnapshot(runtimeItemInstanceId, out ItemInstanceSnapshot snapshot))
            {
                Debug.LogWarning($"{name} could not resolve tracked dropped item '{runtimeItemInstanceId}'.");
                return true;
            }
            if (!inventory.CanAddExistingItemIdentity(item, runtimeItemInstanceId, snapshot.StackQuantity))
            {
                PrototypeHudMessageBus.Show("Inventory full");
                return true;
            }
            InventoryInstanceOperationResult result = inventory.AddExistingItemIdentity(item, runtimeItemInstanceId, snapshot.StackQuantity);
            if (!result.Succeeded)
            {
                Debug.LogWarning($"{name} could not collect tracked dropped item '{runtimeItemInstanceId}': {result.Message}");
                return true;
            }
            if (snapshot.OwnershipKind == ItemOwnershipKind.PersonOwned
                && !string.IsNullOrWhiteSpace(snapshot.OwnerPersonId)
                && !string.Equals(snapshot.OwnerPersonId, services.PlayerPersonId, System.StringComparison.Ordinal))
            {
                services.RecordTheftCrime(services.PlayerPersonId, snapshot.OwnerPersonId, snapshot.ItemInstanceId, $"pickup.{snapshot.ItemInstanceId}");
            }
            services.CancelNaturalDecomposition(runtimeItemInstanceId);
            PrototypeHudMessageBus.Show($"Picked up {item.DisplayName}");
            CompletePickup();
            return true;
        }

        private static IItemDurabilityRuntimeProvider FindItemRuntimeProvider(GameObject interactor)
        {
            if (interactor != null)
            {
                IItemDurabilityRuntimeProvider provider = interactor.GetComponentInParent<IItemDurabilityRuntimeProvider>();
                if (provider != null)
                {
                    return provider;
                }

                provider = interactor.GetComponentInChildren<IItemDurabilityRuntimeProvider>();
                if (provider != null)
                {
                    return provider;
                }
            }

            MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IItemDurabilityRuntimeProvider found)
                {
                    return found;
                }
            }

            return null;
        }

        private string ResolveSceneAuthoredItemInstanceId()
        {
            string itemId = item == null ? "item.unknown" : item.Id;
            string stable = $"{gameObject.scene.name}.{transform.GetSiblingIndex()}.{name}.{itemId}";
            using System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create();
            byte[] hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(stable));
            return new System.Guid(hash).ToString("D");
        }

        private static void RestorePickupRollback(
            IItemDurabilityRuntimeProvider provider,
            ItemInstanceRuntimeSaveData identityRollback,
            ItemQualityAffixRuntimeSaveData qualityRollback,
            ItemDurabilityRuntimeSaveData durabilityRollback)
        {
            provider?.ItemDurability?.RestoreFromSaveData(durabilityRollback, provider.ItemDurabilityDefinitionRegistry, provider.ItemIdentities, provider.ItemCompositions);
            provider?.ItemQualityAffixes?.RestoreFromSaveData(qualityRollback, provider.ItemQualityDefinitionRegistry, provider.ItemIdentities);
            provider?.ItemIdentities?.RestoreFromSaveData(identityRollback, provider.ItemDurabilityDefinitionRegistry);
        }

        private void CompletePickup()
        {
            if (disableOnCollected)
            {
                gameObject.SetActive(false);
                return;
            }

            Destroy(gameObject);
        }
    }
}
