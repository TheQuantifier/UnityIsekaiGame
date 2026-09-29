using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory.Composition;
using UnityIsekaiGame.Inventory.Identity;

namespace UnityIsekaiGame.Inventory.Quality
{
    public sealed class ItemQualityAffixCreationRequest
    {
        public IInventoryItemDefinition Definition { get; set; }
        public ItemInstanceClassification Classification { get; set; } = ItemInstanceClassification.IndividuallyTracked;
        public string ItemInstanceId { get; set; } = string.Empty;
        public string CreatorPersonId { get; set; } = string.Empty;
        public string OwnerPersonId { get; set; } = string.Empty;
        public string CustodianPersonId { get; set; } = string.Empty;
        public string CreationSourceId { get; set; } = string.Empty;
        public string DeterministicItemSeed { get; set; } = string.Empty;
        public bool RequireComposition { get; set; }
        public bool UseDefaultTemplate { get; set; } = true;
        public ItemCompositionRecordData ExplicitComposition { get; set; }
        public ItemCompositionMutationPurpose Purpose { get; set; } = ItemCompositionMutationPurpose.RuntimeGameplay;
        public bool Preview { get; set; }
        public ItemQualityRecordData ExplicitQuality { get; set; }
        public IReadOnlyList<ItemAffixDefinition> AuthoredAffixes { get; set; }
        public ItemAffixGenerationRequest GenerationRequest { get; set; }
        public bool RequireQuality { get; set; } = true;
    }

    public sealed class ItemQualityAffixCreationResult
    {
        private ItemQualityAffixCreationResult(bool succeeded, string message, ItemInstanceSnapshot item, ItemCompositionSnapshot composition, ItemQualitySnapshot quality, IReadOnlyList<ItemAffixSnapshot> affixes)
        {
            Succeeded = succeeded;
            Message = message ?? string.Empty;
            Item = item;
            Composition = composition;
            Quality = quality;
            Affixes = (affixes ?? System.Array.Empty<ItemAffixSnapshot>()).ToArray();
        }

        public bool Succeeded { get; }
        public string Message { get; }
        public ItemInstanceSnapshot Item { get; }
        public ItemCompositionSnapshot Composition { get; }
        public ItemQualitySnapshot Quality { get; }
        public IReadOnlyList<ItemAffixSnapshot> Affixes { get; }

        public static ItemQualityAffixCreationResult Success(ItemInstanceSnapshot item, ItemCompositionSnapshot composition, ItemQualitySnapshot quality, IReadOnlyList<ItemAffixSnapshot> affixes, string message)
        {
            return new ItemQualityAffixCreationResult(true, message, item, composition, quality, affixes);
        }

        public static ItemQualityAffixCreationResult Failure(string message)
        {
            return new ItemQualityAffixCreationResult(false, message, null, null, null, System.Array.Empty<ItemAffixSnapshot>());
        }
    }

    public static class ItemQualityAffixCoordinator
    {
        public static ItemQualityAffixCreationResult CreateItem(
            ItemInstanceIdentityRuntime itemRuntime,
            ItemCompositionRuntime compositionRuntime,
            ItemQualityAffixRuntime qualityRuntime,
            DefinitionRegistry registry,
            ItemQualityAffixCreationRequest request)
        {
            if (itemRuntime == null || compositionRuntime == null || qualityRuntime == null)
            {
                return ItemQualityAffixCreationResult.Failure("Item identity, composition, and quality runtimes are required.");
            }

            if (request?.Definition == null)
            {
                return ItemQualityAffixCreationResult.Failure("Item creation requires an item definition.");
            }

            string resolvedItemInstanceId = ResolveItemInstanceId(request);
            if (string.IsNullOrWhiteSpace(resolvedItemInstanceId))
            {
                return ItemQualityAffixCreationResult.Failure("Quality/affix item creation requires an explicit item instance ID or deterministic creation seed.");
            }

            ItemInstanceRuntimeSaveData originalItems = itemRuntime.CreateSaveData();
            ItemCompositionRuntimeSaveData originalCompositions = compositionRuntime.CreateSaveData();
            ItemQualityAffixRuntimeSaveData originalQuality = qualityRuntime.CreateSaveData();
            ItemInstanceIdentityRuntime targetItems = itemRuntime;
            ItemCompositionRuntime targetCompositions = compositionRuntime;
            ItemQualityAffixRuntime targetQuality = qualityRuntime;
            if (request.Preview)
            {
                targetItems = new ItemInstanceIdentityRuntime();
                targetItems.RestoreFromSaveData(originalItems, registry);
                targetCompositions = new ItemCompositionRuntime();
                targetCompositions.RestoreFromSaveData(originalCompositions, registry, targetItems);
                targetQuality = new ItemQualityAffixRuntime();
                targetQuality.RestoreFromSaveData(originalQuality, registry, targetItems);
            }

            ItemCompositionCreationResult created = ItemCompositionCoordinator.CreateItem(targetItems, targetCompositions, registry, new ItemCompositionCreationRequest
            {
                Definition = request.Definition,
                Classification = request.Classification,
                ItemInstanceId = resolvedItemInstanceId,
                CreatorPersonId = request.CreatorPersonId,
                OwnerPersonId = request.OwnerPersonId,
                CustodianPersonId = request.CustodianPersonId,
                CreationSourceId = request.CreationSourceId,
                RequireComposition = request.RequireComposition,
                UseDefaultTemplate = request.UseDefaultTemplate,
                ExplicitComposition = request.ExplicitComposition?.Clone(),
                Purpose = request.Purpose,
                Preview = false
            });
            if (!created.Succeeded)
            {
                return ItemQualityAffixCreationResult.Failure(created.Message);
            }

            string itemInstanceId = created.Item.ItemInstanceId;
            ItemQualityAffixOperationResult qualityResult;
            if (request.ExplicitQuality != null)
            {
                ItemQualityRecordData explicitQuality = request.ExplicitQuality.Clone();
                explicitQuality.itemInstanceId = itemInstanceId;
                explicitQuality.itemDefinitionId = created.Item.ItemDefinitionId;
                qualityResult = targetQuality.SetQualityRecord(targetItems, targetCompositions, registry, explicitQuality);
            }
            else
            {
                qualityResult = targetQuality.EnsureDefaultQuality(targetItems, targetCompositions, registry, itemInstanceId);
            }

            if (!qualityResult.Succeeded && request.RequireQuality)
            {
                if (!request.Preview)
                {
                    Rollback(itemRuntime, compositionRuntime, qualityRuntime, registry, originalItems, originalCompositions, originalQuality);
                }

                return ItemQualityAffixCreationResult.Failure(qualityResult.Message);
            }

            List<ItemAffixSnapshot> affixes = new List<ItemAffixSnapshot>();
            foreach (ItemAffixDefinition affix in request.AuthoredAffixes ?? System.Array.Empty<ItemAffixDefinition>())
            {
                ItemQualityAffixOperationResult applied = targetQuality.ApplyAffix(targetItems, targetCompositions, registry, itemInstanceId, affix, source: ItemAffixSource.Authored);
                if (!applied.Succeeded)
                {
                    if (!request.Preview)
                    {
                        Rollback(itemRuntime, compositionRuntime, qualityRuntime, registry, originalItems, originalCompositions, originalQuality);
                    }

                    return ItemQualityAffixCreationResult.Failure(applied.Message);
                }

                affixes.AddRange(applied.Affixes);
            }

            if (request.GenerationRequest != null)
            {
                request.GenerationRequest.ItemInstanceId = itemInstanceId;
                ItemQualityAffixOperationResult generated = targetQuality.GenerateAffixes(targetItems, targetCompositions, registry, request.GenerationRequest);
                if (!generated.Succeeded)
                {
                    if (!request.Preview)
                    {
                        Rollback(itemRuntime, compositionRuntime, qualityRuntime, registry, originalItems, originalCompositions, originalQuality);
                    }

                    return ItemQualityAffixCreationResult.Failure(generated.Message);
                }

                affixes.AddRange(generated.Affixes);
            }

            if (request.Preview)
            {
                return ItemQualityAffixCreationResult.Success(created.Item, created.Composition, qualityResult.Quality, affixes, "Item, composition, quality, and affixes preview prepared.");
            }

            return ItemQualityAffixCreationResult.Success(created.Item, created.Composition, qualityResult.Quality, affixes, "Item, composition, quality, and affixes committed atomically.");
        }

        private static string ResolveItemInstanceId(ItemQualityAffixCreationRequest request)
        {
            if (ItemInstanceId.IsValid(request.ItemInstanceId) && !string.IsNullOrWhiteSpace(request.ItemInstanceId))
            {
                return request.ItemInstanceId;
            }

            string seed = !string.IsNullOrWhiteSpace(request.DeterministicItemSeed)
                ? request.DeterministicItemSeed
                : !string.IsNullOrWhiteSpace(request.GenerationRequest?.Seed)
                    ? request.GenerationRequest.Seed
                    : request.CreationSourceId;
            if (string.IsNullOrWhiteSpace(seed))
            {
                return string.Empty;
            }

            using MD5 md5 = MD5.Create();
            byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes($"{request.Definition.Id}:{seed}"));
            return new System.Guid(hash.Take(16).ToArray()).ToString("D");
        }

        private static void Rollback(
            ItemInstanceIdentityRuntime itemRuntime,
            ItemCompositionRuntime compositionRuntime,
            ItemQualityAffixRuntime qualityRuntime,
            DefinitionRegistry registry,
            ItemInstanceRuntimeSaveData items,
            ItemCompositionRuntimeSaveData compositions,
            ItemQualityAffixRuntimeSaveData quality)
        {
            itemRuntime.RestoreFromSaveData(items, registry);
            compositionRuntime.RestoreFromSaveData(compositions, registry, itemRuntime);
            qualityRuntime.RestoreFromSaveData(quality, registry, itemRuntime);
        }
    }
}
