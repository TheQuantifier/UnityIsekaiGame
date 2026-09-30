using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory.Identity;

namespace UnityIsekaiGame.Inventory.Quality
{
    public sealed class WorldItemQualityAffixPreset : MonoBehaviour
    {
        [SerializeField] private string sceneItemInstanceId;
        [SerializeField] private ItemQualityRecordData qualityPreset;
        [SerializeField] private ItemAffixDefinition[] authoredAffixes = Array.Empty<ItemAffixDefinition>();
        [SerializeField] private bool generateAffixes;
        [SerializeField] private string generationPolicyId = "affix-policy.scene-authored";
        [SerializeField] private string generationSeed;
        [SerializeField, Min(0)] private int generatedAffixCount;

        public string SceneItemInstanceId => sceneItemInstanceId ?? string.Empty;

        private void OnValidate()
        {
            generatedAffixCount = Mathf.Max(0, generatedAffixCount);
        }

        public bool TryPreparePickupInstance(
            ItemDefinition item,
            IItemQualityAffixRuntimeProvider provider,
            out string itemInstanceId,
            out string failureReason)
        {
            itemInstanceId = ResolveItemInstanceId(item);
            failureReason = string.Empty;
            if (item == null)
            {
                failureReason = "Scene-authored quality pickup has no item definition.";
                return false;
            }

            if (provider == null || provider.ItemIdentities == null || provider.ItemQualityAffixes == null)
            {
                failureReason = "Scene-authored quality pickup has no item runtime provider.";
                return false;
            }

            DefinitionRegistry registry = provider.ItemQualityDefinitionRegistry;
            ItemInstanceIdentityRuntime identities = provider.ItemIdentities;
            ItemInstanceRuntimeSaveData identityRollback = identities.CreateSaveData();
            ItemQualityAffixRuntimeSaveData qualityRollback = provider.ItemQualityAffixes.CreateSaveData();
            if (!identities.TryGetSnapshot(itemInstanceId, out _))
            {
                ItemInstanceOperationResult created = identities.CreateItem(
                    item,
                    ItemInstanceClassification.WorldFixture,
                    itemInstanceId,
                    creationSourceId: $"scene-authored.pickup.{name}");
                if (!created.Succeeded)
                {
                    failureReason = created.Message;
                    RestoreRollback(identities, provider.ItemQualityAffixes, identityRollback, qualityRollback, registry);
                    return false;
                }
            }

            if (qualityPreset != null)
            {
                ItemQualityRecordData quality = qualityPreset.Clone();
                quality.itemInstanceId = itemInstanceId;
                quality.itemDefinitionId = item.Id;
                quality.source = ItemQualityRecordSource.SceneAuthored;
                ItemQualityAffixOperationResult set = provider.ItemQualityAffixes.SetQualityRecord(
                    identities,
                    provider.ItemCompositions,
                    registry,
                    quality);
                if (!set.Succeeded)
                {
                    failureReason = set.Message;
                    RestoreRollback(identities, provider.ItemQualityAffixes, identityRollback, qualityRollback, registry);
                    return false;
                }
            }
            else
            {
                ItemQualityAffixOperationResult ensured = provider.ItemQualityAffixes.EnsureDefaultQuality(
                    identities,
                    provider.ItemCompositions,
                    registry,
                    itemInstanceId);
                if (!ensured.Succeeded)
                {
                    failureReason = ensured.Message;
                    RestoreRollback(identities, provider.ItemQualityAffixes, identityRollback, qualityRollback, registry);
                    return false;
                }
            }

            foreach (ItemAffixDefinition affix in authoredAffixes ?? Array.Empty<ItemAffixDefinition>())
            {
                if (affix == null)
                {
                    continue;
                }

                bool alreadyApplied = provider.ItemQualityAffixes
                    .GetAffixesForItem(itemInstanceId)
                    .Any(snapshot => string.Equals(snapshot.AffixDefinitionId, affix.Id, StringComparison.Ordinal));
                if (alreadyApplied)
                {
                    continue;
                }

                ItemQualityAffixOperationResult applied = provider.ItemQualityAffixes.ApplyAffix(
                    identities,
                    provider.ItemCompositions,
                    registry,
                    itemInstanceId,
                    affix,
                    seed: ResolveSeed(itemInstanceId, affix.Id),
                    source: ItemAffixSource.Authored);
                if (!applied.Succeeded)
                {
                    failureReason = applied.Message;
                    RestoreRollback(identities, provider.ItemQualityAffixes, identityRollback, qualityRollback, registry);
                    return false;
                }
            }

            if (generateAffixes && generatedAffixCount > 0 && provider.ItemQualityAffixes.GetAffixesForItem(itemInstanceId).Count == 0)
            {
                ItemQualityAffixOperationResult generated = provider.ItemQualityAffixes.GenerateAffixes(
                    identities,
                    provider.ItemCompositions,
                    registry,
                    new ItemAffixGenerationRequest
                    {
                        ItemInstanceId = itemInstanceId,
                        PolicyId = string.IsNullOrWhiteSpace(generationPolicyId) ? "affix-policy.scene-authored" : generationPolicyId,
                        Seed = ResolveSeed(itemInstanceId, "generated"),
                        RequestedAffixCount = generatedAffixCount,
                        Source = ItemAffixSource.Generated
                    });
                if (!generated.Succeeded)
                {
                    failureReason = generated.Message;
                    RestoreRollback(identities, provider.ItemQualityAffixes, identityRollback, qualityRollback, registry);
                    return false;
                }
            }

            return true;
        }

        private static void RestoreRollback(
            ItemInstanceIdentityRuntime identities,
            ItemQualityAffixRuntime qualityAffixes,
            ItemInstanceRuntimeSaveData identityRollback,
            ItemQualityAffixRuntimeSaveData qualityRollback,
            DefinitionRegistry registry)
        {
            qualityAffixes?.RestoreFromSaveData(qualityRollback, registry, identities);
            identities?.RestoreFromSaveData(identityRollback, registry);
        }

        private string ResolveItemInstanceId(ItemDefinition item)
        {
            if (ItemInstanceId.IsValid(sceneItemInstanceId))
            {
                return sceneItemInstanceId;
            }

            string itemId = item == null ? "item.unknown" : item.Id;
            string path = $"{gameObject.scene.name}.{transform.GetSiblingIndex()}.{name}.{itemId}";
            return DeterministicGuid(path);
        }

        private string ResolveSeed(string itemInstanceId, string suffix)
        {
            return string.IsNullOrWhiteSpace(generationSeed)
                ? $"{itemInstanceId}:{suffix}"
                : $"{generationSeed}:{suffix}";
        }

        private static string DeterministicGuid(string stableKey)
        {
            using MD5 md5 = MD5.Create();
            byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(stableKey ?? string.Empty));
            return new Guid(hash.Take(16).ToArray()).ToString("D");
        }
    }
}
