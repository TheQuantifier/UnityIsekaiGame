using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory.Identity;

namespace UnityIsekaiGame.Inventory.Durability
{
    public sealed class WorldItemDurabilityPreset : MonoBehaviour
    {
        [SerializeField] private ItemDurabilityRecordData durabilityPreset;
        [SerializeField] private bool requireDurability = true;

        public bool TryPreparePickupInstance(
            ItemDefinition item,
            IItemDurabilityRuntimeProvider provider,
            string itemInstanceId,
            out string failureReason)
        {
            failureReason = string.Empty;
            if (!requireDurability && durabilityPreset == null)
            {
                return true;
            }

            if (item == null)
            {
                failureReason = "Scene-authored durability pickup has no item definition.";
                return false;
            }

            if (provider == null || provider.ItemIdentities == null || provider.ItemDurability == null)
            {
                failureReason = "Scene-authored durability pickup has no item durability runtime provider.";
                return false;
            }

            DefinitionRegistry registry = provider.ItemDurabilityDefinitionRegistry;
            if (durabilityPreset != null)
            {
                ItemDurabilityRecordData record = durabilityPreset.Clone();
                record.itemInstanceId = itemInstanceId;
                record.itemDefinitionId = item.Id;
                record.source = ItemDurabilityRecordSource.SceneAuthored;
                ItemDurabilityOperationResult set = provider.ItemDurability.SetDurabilityRecord(
                    provider.ItemIdentities,
                    provider.ItemCompositions,
                    provider.ItemQualityAffixes,
                    registry,
                    record);
                if (!set.Succeeded)
                {
                    failureReason = set.Message;
                    return false;
                }

                return true;
            }

            ItemDurabilityOperationResult ensured = provider.ItemDurability.EnsureDefaultDurability(
                provider.ItemIdentities,
                provider.ItemCompositions,
                provider.ItemQualityAffixes,
                registry,
                itemInstanceId);
            if (!ensured.Succeeded)
            {
                failureReason = ensured.Message;
                return false;
            }

            return true;
        }
    }
}
