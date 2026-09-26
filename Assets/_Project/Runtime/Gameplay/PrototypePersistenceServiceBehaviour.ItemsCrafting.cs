using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.Equipment;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Governments;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Inventory.Composition;
using UnityIsekaiGame.Inventory.Crafting;
using UnityIsekaiGame.Inventory.Durability;
using UnityIsekaiGame.Inventory.Disassembly;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Inventory.Production;
using UnityIsekaiGame.Inventory.Quality;
using UnityIsekaiGame.Inventory.Recipes;
using UnityIsekaiGame.Professions;
using UnityIsekaiGame.Organizations;
using UnityIsekaiGame.Skills;
using UnityIsekaiGame.WorldEntities;

namespace UnityIsekaiGame.Gameplay
{
    public sealed class PrototypeCraftingResult
    {
        private PrototypeCraftingResult(bool succeeded, string message, CraftingOperationRecordData operation)
        {
            Succeeded = succeeded;
            Message = message ?? string.Empty;
            Operation = operation?.Clone();
        }

        public bool Succeeded { get; }
        public string Message { get; }
        public CraftingOperationRecordData Operation { get; }

        public static PrototypeCraftingResult Success(string message, CraftingOperationRecordData operation) => new PrototypeCraftingResult(true, message, operation);
        public static PrototypeCraftingResult Failure(string message) => new PrototypeCraftingResult(false, message, null);
    }

    public sealed class PrototypeActiveCraftSnapshot
    {
        public string OperationId { get; set; } = string.Empty;
        public string RecipeId { get; set; } = string.Empty;
        public string RecipeDisplayName { get; set; } = string.Empty;
        public double StartedAtSeconds { get; set; }
        public float DurationSeconds { get; set; }
        public float RemainingSeconds { get; set; }
        public float ProgressNormalized { get; set; }
        public CraftingScalingCalculation Scaling { get; set; }
        public SlotCraftingRequest SlotRequest { get; set; }
    }

    public sealed class PrototypeCraftingStartResult
    {
        private PrototypeCraftingStartResult(bool succeeded, string message, PrototypeActiveCraftSnapshot craft)
        {
            Succeeded = succeeded;
            Message = message ?? string.Empty;
            Craft = craft;
        }

        public bool Succeeded { get; }
        public string Message { get; }
        public PrototypeActiveCraftSnapshot Craft { get; }

        public static PrototypeCraftingStartResult Success(string message, PrototypeActiveCraftSnapshot craft) => new PrototypeCraftingStartResult(true, message, craft);
        public static PrototypeCraftingStartResult Failure(string message) => new PrototypeCraftingStartResult(false, message, null);
    }

    public sealed class PrototypeCraftingCatalystChoice
    {
        public string ItemInstanceId { get; set; } = string.Empty;
        public string ItemDefinitionId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string RarityName { get; set; } = string.Empty;
        public int RarityRank { get; set; }
        public int AvailableQuantity { get; set; }
    }

    public sealed class PrototypeCraftingMaterialChoice
    {
        public string ItemDefinitionId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int AvailableQuantity { get; set; }
        public IReadOnlyList<string> ResourceCategoryIds { get; set; } = Array.Empty<string>();
    }

    public sealed class PrototypeItemRecoveryChoice
    {
        public string ItemInstanceId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string OperationLabel { get; set; } = string.Empty;
        public DisassemblyOperationRecordData Preview { get; set; }
    }

    public sealed class PrototypeItemDropResult
    {
        private PrototypeItemDropResult(bool succeeded, string message, WorldItemPickup pickup)
        { Succeeded = succeeded; Message = message ?? string.Empty; Pickup = pickup; }
        public bool Succeeded { get; }
        public string Message { get; }
        public WorldItemPickup Pickup { get; }
        public static PrototypeItemDropResult Success(WorldItemPickup pickup) => new PrototypeItemDropResult(true, "Item dropped into the world.", pickup);
        public static PrototypeItemDropResult Failure(string message) => new PrototypeItemDropResult(false, message, null);
    }

    public sealed partial class PrototypePersistenceServiceBehaviour
    {
        public const string PrototypeWorkstationInstanceId = "production-station-instance.prototype.workstation";
        public const string PrototypeWorkstationDefinitionId = "production-station.prototype.workstation";
        public const string PrototypeWorkstationLocationId = "location.prototype.merchant-counter";

        private RecipeRuntime worldRecipes;
        private bool group6BindingsConfigured;
        private bool group6InventoryTransaction;
        private bool resolvingForcedItemDecomposition;
        private readonly Dictionary<string, DurabilityFeedbackState> durabilityFeedbackByItemId = new Dictionary<string, DurabilityFeedbackState>(StringComparer.Ordinal);
        private PrototypeActiveCraftSnapshot activePrototypeCraft;
        [SerializeField, Min(0f)] private float equippedWeaponWearPerAttack = 0.25f;

        public event Action<PrototypeCraftingResult> PrototypeCraftCompleted;

        public RecipeRuntime Recipes => worldRecipes ??= new RecipeRuntime();
        public bool HasActivePrototypeCraft => activePrototypeCraft != null;
        public PrototypeActiveCraftSnapshot ActivePrototypeCraft => SnapshotActiveCraft();

        public IReadOnlyList<RecipeDefinition> AvailableCraftingRecipes
        {
            get
            {
                EnsureInitialized();
                return GetDefinitionRegistry().DefinitionsById.Values
                    .OfType<RecipeDefinition>()
                    .Where(recipe => recipe.State == RecipeLifecycleState.Active)
                    .OrderBy(recipe => recipe.DisplayName, StringComparer.Ordinal)
                    .ThenBy(recipe => recipe.Id, StringComparer.Ordinal)
                    .ToArray();
            }
        }

        public IReadOnlyList<PrototypeItemRecoveryChoice> GetPrototypeItemRecoveryChoices()
        {
            EnsureInitialized();
            ResolvePlayerPersistenceReferences();
            EnsureGroup6GameplayRuntime();
            playerItemIdentitySynchronizer?.SynchronizeNow();
            string personId = playerService?.PlayerId ?? PersistenceService.LocalPlayerId;
            List<PrototypeItemRecoveryChoice> choices = new List<PrototypeItemRecoveryChoice>();
            foreach (ItemInstanceSnapshot item in ItemIdentities.Snapshots.Where(snapshot => IsAvailableCraftingInventoryItem(snapshot, personId)))
            {
                if (!ItemCompositions.TryGetSnapshotForItem(item.ItemInstanceId, out ItemCompositionSnapshot composition)
                    || composition.Data.tags?.Contains("composition.crafted", StringComparer.Ordinal) != true)
                    continue;
                ItemRecoveryOperationKind kind = ItemRecoveryOperationKind.Disassemble;
                DisassemblyResult preview = ItemRecovery.Preview(BuildRecoveryRequest(item.ItemInstanceId, personId, $"preview-{item.ItemInstanceId}", kind), GetDefinitionRegistry(), ItemIdentities, ItemCompositions, ItemQualityAffixes, ItemDurability);
                if (!preview.Succeeded) continue;
                GetDefinitionRegistry().TryGet(item.ItemDefinitionId, out ItemDefinition definition);
                choices.Add(new PrototypeItemRecoveryChoice
                {
                    ItemInstanceId = item.ItemInstanceId,
                    DisplayName = string.IsNullOrWhiteSpace(item.CustomName) ? definition?.DisplayName ?? item.ItemDefinitionId : item.CustomName,
                    OperationLabel = kind.ToString(),
                    Preview = preview.Operation
                });
            }
            return choices.OrderBy(choice => choice.DisplayName, StringComparer.Ordinal).ThenBy(choice => choice.ItemInstanceId, StringComparer.Ordinal).ToArray();
        }

        public DisassemblyResult RecoverPrototypeItem(string itemInstanceId)
        {
            EnsureInitialized();
            ResolvePlayerPersistenceReferences();
            EnsureGroup6GameplayRuntime();
            playerItemIdentitySynchronizer?.SynchronizeNow();
            string personId = playerService?.PlayerId ?? PersistenceService.LocalPlayerId;
            if (!ItemIdentities.TryGetSnapshot(itemInstanceId, out ItemInstanceSnapshot item) || !IsAvailableCraftingInventoryItem(item, personId))
                return DisassemblyResult.Failure(DisassemblyOperationStatus.MissingItem, "The selected crafted item is not available in inventory.");
            ItemRecoveryOperationKind kind = ItemRecoveryOperationKind.Disassemble;
            string operationId = Guid.NewGuid().ToString("D");
            Group6TransactionSnapshot rollback = Group6TransactionSnapshot.Capture(this, playerInventory);
            DisassemblyResult result = ItemRecovery.Execute(BuildRecoveryRequest(itemInstanceId, personId, operationId, kind), GetDefinitionRegistry(), ItemIdentities, ItemCompositions, ItemQualityAffixes, ItemDurability);
            if (!result.Succeeded || result.Operation == null) { rollback.Restore(this, playerInventory, GetDefinitionRegistry()); return result; }

            group6InventoryTransaction = true;
            try
            {
                int sourceSlot = FindInventorySlot(itemInstanceId);
                if (sourceSlot < 0 || !playerInventory.RemoveItemAt(sourceSlot, item.StackQuantity))
                { rollback.Restore(this, playerInventory, GetDefinitionRegistry()); return DisassemblyResult.Failure(DisassemblyOperationStatus.AtomicCommitFailed, "The source item could not be removed from inventory."); }
                foreach (string outputId in result.Operation.outcomes.SelectMany(outcome => outcome.outputItemInstanceIds ?? Array.Empty<string>()))
                {
                    if (!ItemIdentities.TryGetSnapshot(outputId, out ItemInstanceSnapshot output) || !GetDefinitionRegistry().TryGet(output.ItemDefinitionId, out ItemDefinition definition))
                    { rollback.Restore(this, playerInventory, GetDefinitionRegistry()); return DisassemblyResult.Failure(DisassemblyOperationStatus.OutputCreationFailed, "A recovered component could not be projected into inventory."); }
                    InventoryInstanceOperationResult added = playerInventory.AddExistingItemIdentity(definition, outputId, output.StackQuantity);
                    if (!added.Succeeded) { rollback.Restore(this, playerInventory, GetDefinitionRegistry()); return DisassemblyResult.Failure(DisassemblyOperationStatus.OutputCreationFailed, added.Message); }
                }
                playerItemIdentitySynchronizer?.SynchronizeNow();
            }
            finally { group6InventoryTransaction = false; }

            RecordItemRecoverySkillUse(result.Operation, personId);
            ProfessionCoordinator.RecordItemRecovery(result.Operation);
            dirtyTracker?.MarkDirty($"Item recovery completed: {itemInstanceId}.");
            return result;
        }

        public PrototypeItemDropResult DropPrototypeItemToWorld(string itemInstanceId, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            EnsureInitialized();
            ResolvePlayerPersistenceReferences();
            EnsureGroup6GameplayRuntime();
            playerItemIdentitySynchronizer?.SynchronizeNow();
            string personId = playerService?.PlayerId ?? PersistenceService.LocalPlayerId;
            if (playerInventory == null || !ItemIdentities.TryGetSnapshot(itemInstanceId, out ItemInstanceSnapshot item)
                || !IsAvailableCraftingInventoryItem(item, personId)
                || !GetDefinitionRegistry().TryGet(item.ItemDefinitionId, out ItemDefinition definition))
                return PrototypeItemDropResult.Failure("The selected item is not available to drop.");

            int sourceSlot = FindInventorySlot(itemInstanceId);
            if (sourceSlot < 0) return PrototypeItemDropResult.Failure("The selected item has no inventory slot.");
            Group6TransactionSnapshot rollback = Group6TransactionSnapshot.Capture(this, playerInventory);
            WorldItemPickup pickup = null;
            group6InventoryTransaction = true;
            try
            {
                if (!playerInventory.RemoveItemAt(sourceSlot, item.StackQuantity))
                    return PrototypeItemDropResult.Failure("The selected item could not be removed from inventory.");
                bool rawResource = string.Equals(definition.PrimaryCategory?.Id, "category.item.material", StringComparison.Ordinal)
                    || definition.Tags.Any(tag => tag != null && string.Equals(tag.Id, "tag.general.material", StringComparison.Ordinal));
                bool decomposable = !rawResource
                    && ItemCompositions.TryGetSnapshotForItem(itemInstanceId, out ItemCompositionSnapshot composition)
                    && composition.Materials.Count > 0;
                pickup = WorldItemPickupFactory.CreateTrackedDrop(definition, item.StackQuantity, itemInstanceId, position, rotation, parent, enableNaturalDecomposition: decomposable);
                if (pickup == null) { rollback.Restore(this, playerInventory, GetDefinitionRegistry()); return PrototypeItemDropResult.Failure("The dropped world object could not be created."); }
                string sceneKey = pickup.gameObject.scene.name;
                string worldId = playerService?.WorldId ?? PersistenceService.LocalWorldId;
                WorldEntitySpawnResult entity = WorldEntityIdentityFactory.CreateRuntimeIdentity(pickup.gameObject, sceneKey, worldId, definition.Id);
                if (!entity.Succeeded)
                {
                    Destroy(pickup.gameObject);
                    rollback.Restore(this, playerInventory, GetDefinitionRegistry());
                    return PrototypeItemDropResult.Failure(entity.Message);
                }
                ItemInstanceOperationResult placed = ItemIdentities.SetWorldPlacement(itemInstanceId, $"placement.{entity.Identity.EntityId}", entity.Identity.EntityId, sceneKey);
                if (!placed.Succeeded)
                {
                    Destroy(pickup.gameObject);
                    rollback.Restore(this, playerInventory, GetDefinitionRegistry());
                    return PrototypeItemDropResult.Failure(placed.Message);
                }
            }
            finally { group6InventoryTransaction = false; }

            dirtyTracker?.MarkDirty($"Item dropped into world: {itemInstanceId}.");
            bool decomposedImmediately = pickup.GetComponent<WorldItemDecomposition>()?.EvaluateNow() ?? false;
            if (!decomposedImmediately)
            {
                NaturalDecompositionRecordData schedule = ItemRecovery.NaturalDecompositions
                    .FirstOrDefault(entry => string.Equals(entry.itemInstanceId, itemInstanceId, StringComparison.Ordinal)
                        && entry.state == NaturalDecompositionState.Scheduled);
                if (schedule != null)
                {
                    string itemName = ItemDisplayName(item);
                    string rate = schedule.rate == NaturalDecompositionRate.Fast ? "quickly" : "slowly";
                    PrototypeHudMessageBus.Show($"{itemName} was dropped and will decay {rate} while left on the ground");
                }
            }
            return PrototypeItemDropResult.Success(pickup);
        }

        public PrototypeItemDropResult CreateTrackedWorldDrop(ItemDefinition definition, Vector3 position, Quaternion rotation, Transform parent = null, Material fallbackMaterial = null)
        {
            EnsureInitialized();
            ResolvePlayerPersistenceReferences();
            EnsureGroup6GameplayRuntime();
            if (definition == null || playerInventory == null) return PrototypeItemDropResult.Failure("A tracked world drop requires an item definition and initialized inventory services.");

            Group6TransactionSnapshot rollback = Group6TransactionSnapshot.Capture(this, playerInventory);
            ItemInstanceClassification classification = definition.InstanceMode == ItemInstanceMode.AlwaysInstanced
                ? ItemInstanceClassification.IndividuallyTracked
                : ItemInstanceClassification.Fungible;
            ItemInstanceOperationResult created = ItemIdentities.CreateItem(definition, classification, creationSourceId: "world-drop.runtime");
            if (!created.Succeeded) return PrototypeItemDropResult.Failure(created.Message);
            string itemInstanceId = created.Snapshot.ItemInstanceId;
            ItemCompositionOperationResult composition = ItemCompositions.EnsureCompositionForItem(ItemIdentities, GetDefinitionRegistry(), itemInstanceId);
            ItemQualityAffixOperationResult quality = ItemQualityAffixes.EnsureDefaultQuality(ItemIdentities, ItemCompositions, GetDefinitionRegistry(), itemInstanceId);
            ItemDurabilityOperationResult durability = ItemDurability.EnsureDefaultDurability(ItemIdentities, ItemCompositions, ItemQualityAffixes, GetDefinitionRegistry(), itemInstanceId);
            if (!composition.Succeeded || !quality.Succeeded || !durability.Succeeded)
            {
                rollback.Restore(this, playerInventory, GetDefinitionRegistry());
                return PrototypeItemDropResult.Failure(!composition.Succeeded ? composition.Message : !quality.Succeeded ? quality.Message : durability.Message);
            }

            bool rawResource = string.Equals(definition.PrimaryCategory?.Id, "category.item.material", StringComparison.Ordinal)
                || definition.Tags.Any(tag => tag != null && string.Equals(tag.Id, "tag.general.material", StringComparison.Ordinal));
            WorldItemPickup pickup = WorldItemPickupFactory.CreateTrackedDrop(definition, 1, itemInstanceId, position, rotation, parent, fallbackMaterial, !rawResource && composition.Snapshot.Materials.Count > 0);
            if (pickup == null)
            {
                rollback.Restore(this, playerInventory, GetDefinitionRegistry());
                return PrototypeItemDropResult.Failure("The tracked world drop could not be created.");
            }
            string sceneKey = pickup.gameObject.scene.name;
            string worldId = playerService?.WorldId ?? PersistenceService.LocalWorldId;
            WorldEntitySpawnResult entity = WorldEntityIdentityFactory.CreateRuntimeIdentity(pickup.gameObject, sceneKey, worldId, definition.Id);
            ItemInstanceOperationResult placed = entity.Succeeded
                ? ItemIdentities.SetWorldPlacement(itemInstanceId, $"placement.{entity.Identity.EntityId}", entity.Identity.EntityId, sceneKey)
                : ItemInstanceOperationResult.Failure(ItemInstanceOperationStatus.InvalidLocation, entity.Message);
            if (!placed.Succeeded)
            {
                Destroy(pickup.gameObject);
                rollback.Restore(this, playerInventory, GetDefinitionRegistry());
                return PrototypeItemDropResult.Failure(placed.Message);
            }
            dirtyTracker?.MarkDirty($"Tracked item spawned in world: {itemInstanceId}.");
            return PrototypeItemDropResult.Success(pickup);
        }

        public bool TryAdvanceNaturalDecomposition(WorldItemDecomposition source)
        {
            if (source == null || string.IsNullOrWhiteSpace(source.ItemInstanceId)) return false;
            EnsureInitialized();
            EnsureGroup6GameplayRuntime();
            if (!ItemIdentities.TryGetSnapshot(source.ItemInstanceId, out ItemInstanceSnapshot item)
                || item.LocationKind != ItemLocationKind.WorldPlacement
                || !ItemCompositions.TryGetSnapshotForItem(source.ItemInstanceId, out ItemCompositionSnapshot composition)
                || composition.Materials.Count == 0)
                return false;

            if (!ItemDurability.TryGetDurabilityForItem(source.ItemInstanceId, out ItemDurabilitySnapshot durability))
            {
                ItemDurabilityOperationResult ensured = ItemDurability.EnsureDefaultDurability(ItemIdentities, ItemCompositions, ItemQualityAffixes, GetDefinitionRegistry(), source.ItemInstanceId);
                if (!ensured.Succeeded) return false;
                durability = ensured.Snapshot;
            }

            double now = CurrentCraftingElapsedSeconds();
            ItemDegradationPolicyDefinition policy = ResolveItemDegradationPolicy();
            if (durability.HasBroken && !durability.PendingForcedDecomposition && !policy.BrokenWorldItemsContinueDecay)
            {
                ItemRecovery.CancelNaturalDecomposition(source.ItemInstanceId);
                return false;
            }

            float decompositionCondition = durability.HasBroken && !durability.PendingForcedDecomposition
                ? Mathf.Max(durability.NormalizedDurability, policy.ImmediateDecompositionThreshold + 0.0001f)
                : durability.NormalizedDurability;
            NaturalDecompositionRecordData schedule = ItemRecovery.ScheduleNaturalDecomposition(
                source.ItemInstanceId, source.WorldEntityId, source.SceneKey, now, decompositionCondition,
                new NaturalDecompositionSettings
                {
                    fastThreshold = policy.FastDecompositionThreshold,
                    immediateThreshold = policy.ImmediateDecompositionThreshold,
                    slowDurationSeconds = policy.SlowDecompositionSeconds,
                    fastDurationSeconds = policy.FastDecompositionSeconds
                });
            if (schedule == null || !ItemRecovery.IsNaturalDecompositionDue(source.ItemInstanceId, now, out schedule)) return false;

            DisassemblyResult result = ItemRecovery.Execute(new DisassemblyRequest
            {
                operationId = schedule.operationId,
                itemInstanceId = source.ItemInstanceId,
                actorPersonId = "actor.nature",
                ownerPersonId = string.Empty,
                worldTime = now.ToString("0.###", CultureInfo.InvariantCulture),
                deterministicSeed = schedule.operationId,
                operationKind = ItemRecoveryOperationKind.NaturalDecomposition,
                skillId = string.Empty,
                skillGrade = 0,
                skillUsed = false,
                materializeOutputIdentities = false
            }, GetDefinitionRegistry(), ItemIdentities, ItemCompositions, ItemQualityAffixes, ItemDurability);
            if (!result.Succeeded || result.Operation == null)
            {
                Debug.LogWarning($"Natural decomposition failed for '{source.ItemInstanceId}': {result.Message}");
                return false;
            }

            int recoveredKinds = SpawnSalvageableRecoveryOutputs(result.Operation, source.transform.position, source.transform.parent);
            ItemRecovery.CompleteNaturalDecomposition(source.ItemInstanceId, now);
            dirtyTracker?.MarkDirty($"Item naturally decomposed: {source.ItemInstanceId}.");
            PrototypeHudMessageBus.Show(recoveredKinds > 0 ? "The dropped item broke down into salvageable parts" : "The dropped item decomposed");
            return true;
        }

        private int SpawnSalvageableRecoveryOutputs(DisassemblyOperationRecordData operation, Vector3 position, Transform parent)
        {
            var recovered = operation.outcomes
                .Where(outcome => outcome.returnedQuantity > 0)
                .GroupBy(outcome => outcome.sourceItemDefinitionId, StringComparer.Ordinal)
                .Select(group => new { DefinitionId = group.Key, Quantity = group.Sum(outcome => outcome.returnedQuantity) })
                .OrderBy(entry => entry.DefinitionId, StringComparer.Ordinal)
                .ToArray();
            for (int index = 0; index < recovered.Length; index++)
            {
                if (!GetDefinitionRegistry().TryGet(recovered[index].DefinitionId, out ItemDefinition recoveredDefinition)) continue;
                float angle = recovered.Length <= 1 ? 0f : Mathf.PI * 2f * index / recovered.Length;
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.35f;
                WorldItemPickup part = WorldItemPickupFactory.Create(recoveredDefinition, recovered[index].Quantity, position + offset, Quaternion.identity, parent);
                part?.Configure(recoveredDefinition, recovered[index].Quantity, isSalvagePickup: true);
            }
            return recovered.Length;
        }

        public void CancelNaturalDecomposition(string itemInstanceId)
        {
            if (ItemRecovery.CancelNaturalDecomposition(itemInstanceId)) dirtyTracker?.MarkDirty($"Natural decomposition cancelled: {itemInstanceId}.");
        }

        public SalvagePickupCalculation CalculateSalvagePickup(ItemDefinition item, int baseQuantity, string sourceId)
        {
            EnsureInitialized();
            ResolvePlayerPersistenceReferences();
            RuntimeSkillRecord skill = null;
            bool learned = playerSkills != null && playerSkills.TryGetSkill("skill.salvaging", out skill);
            return SalvagePickupScalingCalculator.Calculate(baseQuantity, learned ? skill.currentGrade : 0, learned, sourceId);
        }

        public DisassemblyResult RecordSalvagePickup(ItemDefinition item, SalvagePickupCalculation calculation, int physicalCollectedQuantity, int totalCollectedQuantity, string sourceId)
        {
            EnsureInitialized();
            ResolvePlayerPersistenceReferences();
            string personId = playerService?.PlayerId ?? PersistenceService.LocalPlayerId;
            string operationId = Guid.NewGuid().ToString("D");
            DisassemblyResult result = ItemRecovery.RecordSalvagePickup(operationId, sourceId, personId, CurrentCraftingWorldTime(), item, calculation, physicalCollectedQuantity, totalCollectedQuantity);
            if (result.Succeeded && result.Operation != null)
            {
                ProfessionCoordinator.RecordItemRecovery(result.Operation);
            }
            if (result.Succeeded && playerSkills != null && GetDefinitionRegistry().TryGet("skill.salvaging", out SkillDefinition skill))
            {
                playerSkills.RecordQualifyingAction(new SkillActionExecutionEvent
                {
                    EventId = $"salvage-pickup.{operationId}", ActorId = personId, ActionDefinitionId = skill.NaturalLearning.QualifyingEventId,
                    ActionCategory = SkillActionEventCategory.CraftingAction, ItemDefinitionId = item?.Id ?? string.Empty,
                    ItemCategory = item?.PrimaryCategory, ItemTags = item?.Tags ?? Array.Empty<TagDefinition>(), Executed = true,
                    IntendedResultSucceeded = true, PlaytimeSeconds = playTimeTracker?.CumulativeSeconds ?? 0d,
                    SourceSystem = "item-recovery.salvage-pickup", ServerAuthoritative = true
                });
                dirtyTracker?.MarkDirty($"Salvage pickup collected: {item?.Id}.");
            }
            return result;
        }

        private DisassemblyRequest BuildRecoveryRequest(string itemInstanceId, string personId, string operationId, ItemRecoveryOperationKind kind)
        {
            RuntimeSkillRecord skill = null;
            bool learned = playerSkills != null && playerSkills.TryGetSkill("skill.disassembly", out skill);
            return new DisassemblyRequest
            {
                operationId = operationId, itemInstanceId = itemInstanceId, actorPersonId = personId, ownerPersonId = personId,
                worldTime = CurrentCraftingWorldTime(), deterministicSeed = operationId, operationKind = kind,
                skillId = "skill.disassembly", skillGrade = learned ? (int)(SkillGrade)skill.currentGrade : 0, skillUsed = learned
            };
        }

        private void RecordItemRecoverySkillUse(DisassemblyOperationRecordData operation, string personId)
        {
            if (playerSkills == null || operation == null || !GetDefinitionRegistry().TryGet("skill.disassembly", out SkillDefinition skill)) return;
            GetDefinitionRegistry().TryGet(operation.itemDefinitionId, out ItemDefinition item);
            playerSkills.RecordQualifyingAction(new SkillActionExecutionEvent
            {
                EventId = $"item-recovery.{operation.operationId}", ActorId = personId, ActionDefinitionId = skill.NaturalLearning.QualifyingEventId,
                ActionCategory = SkillActionEventCategory.CraftingAction, ItemDefinitionId = item?.Id ?? string.Empty, ItemCategory = item?.PrimaryCategory,
                ItemTags = item?.Tags ?? Array.Empty<TagDefinition>(), Executed = true, IntendedResultSucceeded = operation.state == DisassemblyOperationState.Completed,
                PlaytimeSeconds = playTimeTracker?.CumulativeSeconds ?? 0d, SourceSystem = "item-recovery", ServerAuthoritative = true
            });
        }

        public string DescribeRecipeRequirements(RecipeDefinition recipe)
        {
            if (recipe == null)
            {
                return string.Empty;
            }

            return string.Join(", ", recipe.Inputs
                .Where(input => input != null && input.requirementState == RecipeRequirementState.Required)
                .Select(input =>
                {
                    return $"{input.quantity:0.##} {RecipeInputMatcher.Describe(input, GetDefinitionRegistry())}";
                }));
        }

        public string DescribeRecipeInput(RecipeInputSpecificationData input)
        {
            EnsureInitialized();
            return RecipeInputMatcher.Describe(input, GetDefinitionRegistry());
        }

        public PrototypeCraftingResult CraftAtPrototypeWorkstation(string recipeId)
        {
            SlotCraftingRequest slots = BuildAutoFilledSlotCraftingRequest(recipeId);
            return CompleteCraftAtPrototypeWorkstation(recipeId, null, string.Empty, slots);
        }

        public SlotCraftingRequest BuildAutoFilledSlotCraftingRequest(string recipeId)
        {
            EnsureInitialized();
            ResolvePlayerPersistenceReferences();
            DefinitionRegistry registry = GetDefinitionRegistry();
            if (string.IsNullOrWhiteSpace(recipeId) || !registry.TryGet(recipeId, out RecipeDefinition recipe))
            {
                return null;
            }

            EnsureGroup6GameplayRuntime();
            playerItemIdentitySynchronizer?.SynchronizeNow();
            string personId = playerService?.PlayerId ?? PersistenceService.LocalPlayerId;
            Dictionary<string, int> available = ItemIdentities.Snapshots
                .Where(snapshot => IsAvailableCraftingInventoryItem(snapshot, personId))
                .ToDictionary(snapshot => snapshot.ItemInstanceId, snapshot => snapshot.StackQuantity, StringComparer.Ordinal);
            SlotCraftingRequest request = new SlotCraftingRequest { recipeId = recipe.Id };
            int slotIndex = 0;
            foreach (RecipeInputSpecificationData input in recipe.Inputs
                         .Where(entry => entry != null && entry.requirementState == RecipeRequirementState.Required)
                         .OrderBy(entry => entry.inputId, StringComparer.Ordinal))
            {
                int remaining = Math.Max(1, (int)Math.Round(input.quantity));
                IGrouping<string, ItemInstanceSnapshot> selectedResource = ItemIdentities.Snapshots
                    .Where(snapshot => IsAvailableCraftingInventoryItem(snapshot, personId)
                        && available.TryGetValue(snapshot.ItemInstanceId, out int stackAvailable)
                        && stackAvailable > 0
                        && registry.TryGet(snapshot.ItemDefinitionId, out ItemDefinition definition)
                        && RecipeInputMatcher.Matches(input, definition, registry))
                    .GroupBy(snapshot => snapshot.ItemDefinitionId, StringComparer.Ordinal)
                    .OrderByDescending(group => group.Sum(snapshot => available[snapshot.ItemInstanceId]) >= remaining)
                    .ThenBy(group => group.Key, StringComparer.Ordinal)
                    .FirstOrDefault();

                foreach (ItemInstanceSnapshot item in selectedResource?.OrderBy(snapshot => snapshot.ItemInstanceId, StringComparer.Ordinal)
                             ?? Enumerable.Empty<ItemInstanceSnapshot>())
                {
                    if (remaining <= 0 || !available.TryGetValue(item.ItemInstanceId, out int stackAvailable) || stackAvailable <= 0)
                    {
                        continue;
                    }

                    int assigned = Math.Min(remaining, stackAvailable);
                    request.slots.Add(new CraftingSlotEntryData
                    {
                        slotId = $"required.{++slotIndex}",
                        kind = CraftingSlotKind.RequiredInput,
                        recipeInputId = input.inputId,
                        itemDefinitionId = item.ItemDefinitionId,
                        itemInstanceId = item.ItemInstanceId,
                        quantity = assigned
                    });
                    available[item.ItemInstanceId] = stackAvailable - assigned;
                    remaining -= assigned;
                }
            }

            return request;
        }

        public IReadOnlyList<SlotCraftingRequest> BuildAutoFilledSlotCraftingRequests(RecipeCategory category)
        {
            return AvailableCraftingRecipes
                .Where(recipe => recipe.Category == category)
                .Select(recipe => BuildAutoFilledSlotCraftingRequest(recipe.Id))
                .Where(request => request != null)
                .ToArray();
        }

        public IReadOnlyList<PrototypeCraftingMaterialChoice> GetPrototypeCraftingMaterialChoices(string recipeId, string inputId)
        {
            EnsureInitialized();
            ResolvePlayerPersistenceReferences();
            EnsureGroup6GameplayRuntime();
            playerItemIdentitySynchronizer?.SynchronizeNow();
            DefinitionRegistry registry = GetDefinitionRegistry();
            if (!registry.TryGet(recipeId, out RecipeDefinition recipe))
            {
                return Array.Empty<PrototypeCraftingMaterialChoice>();
            }

            RecipeInputSpecificationData input = recipe.Inputs.FirstOrDefault(candidate => string.Equals(candidate.inputId, inputId, StringComparison.Ordinal));
            if (input == null)
            {
                return Array.Empty<PrototypeCraftingMaterialChoice>();
            }

            string personId = playerService?.PlayerId ?? PersistenceService.LocalPlayerId;
            return ItemIdentities.Snapshots
                .Where(snapshot => IsAvailableCraftingInventoryItem(snapshot, personId)
                    && registry.TryGet(snapshot.ItemDefinitionId, out ItemDefinition definition)
                    && RecipeInputMatcher.Matches(input, definition, registry))
                .GroupBy(snapshot => snapshot.ItemDefinitionId, StringComparer.Ordinal)
                .Select(group =>
                {
                    registry.TryGet(group.Key, out ItemDefinition definition);
                    return new PrototypeCraftingMaterialChoice
                    {
                        ItemDefinitionId = group.Key,
                        DisplayName = definition?.DisplayName ?? group.Key,
                        AvailableQuantity = group.Sum(snapshot => snapshot.StackQuantity),
                        ResourceCategoryIds = RecipeInputMatcher.GetResourceCategoryIds(definition, registry)
                    };
                })
                .OrderBy(choice => choice.DisplayName, StringComparer.Ordinal)
                .ThenBy(choice => choice.ItemDefinitionId, StringComparer.Ordinal)
                .ToArray();
        }

        public SlotCraftingRequest AssignPrototypeCraftingMaterial(SlotCraftingRequest request, string inputId, string itemDefinitionId)
        {
            EnsureInitialized();
            ResolvePlayerPersistenceReferences();
            EnsureGroup6GameplayRuntime();
            playerItemIdentitySynchronizer?.SynchronizeNow();
            DefinitionRegistry registry = GetDefinitionRegistry();
            if (request == null
                || !registry.TryGet(request.recipeId, out RecipeDefinition recipe)
                || !registry.TryGet(itemDefinitionId, out ItemDefinition selectedDefinition))
            {
                return request?.Clone();
            }

            RecipeInputSpecificationData input = recipe.Inputs.FirstOrDefault(candidate => string.Equals(candidate.inputId, inputId, StringComparison.Ordinal));
            if (input == null || !RecipeInputMatcher.Matches(input, selectedDefinition, registry))
            {
                return request.Clone();
            }

            SlotCraftingRequest updated = request.Clone();
            updated.slots.RemoveAll(slot => slot.kind == CraftingSlotKind.RequiredInput && string.Equals(slot.recipeInputId, inputId, StringComparison.Ordinal));
            Dictionary<string, int> reservedElsewhere = updated.slots
                .Where(slot => slot.kind == CraftingSlotKind.RequiredInput)
                .GroupBy(slot => slot.itemInstanceId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Sum(slot => slot.quantity), StringComparer.Ordinal);
            string personId = playerService?.PlayerId ?? PersistenceService.LocalPlayerId;
            int remaining = Math.Max(1, (int)Math.Round(input.quantity));
            int index = 0;
            foreach (ItemInstanceSnapshot snapshot in ItemIdentities.Snapshots
                         .Where(candidate => IsAvailableCraftingInventoryItem(candidate, personId)
                             && string.Equals(candidate.ItemDefinitionId, itemDefinitionId, StringComparison.Ordinal))
                         .OrderBy(candidate => candidate.ItemInstanceId, StringComparer.Ordinal))
            {
                reservedElsewhere.TryGetValue(snapshot.ItemInstanceId, out int reserved);
                int available = Math.Max(0, snapshot.StackQuantity - reserved);
                if (available <= 0 || remaining <= 0)
                {
                    continue;
                }

                int assigned = Math.Min(remaining, available);
                updated.slots.Add(new CraftingSlotEntryData
                {
                    slotId = $"required.{inputId}.{++index}",
                    kind = CraftingSlotKind.RequiredInput,
                    recipeInputId = inputId,
                    itemDefinitionId = itemDefinitionId,
                    itemInstanceId = snapshot.ItemInstanceId,
                    quantity = assigned
                });
                remaining -= assigned;
            }

            return updated;
        }

        public IReadOnlyList<PrototypeCraftingCatalystChoice> GetPrototypeCraftingCatalystChoices()
        {
            EnsureInitialized();
            ResolvePlayerPersistenceReferences();
            EnsureGroup6GameplayRuntime();
            playerItemIdentitySynchronizer?.SynchronizeNow();
            DefinitionRegistry registry = GetDefinitionRegistry();
            string personId = playerService?.PlayerId ?? PersistenceService.LocalPlayerId;
            return ItemIdentities.Snapshots
                .Where(snapshot => IsAvailableCraftingInventoryItem(snapshot, personId))
                .Select(snapshot =>
                {
                    registry.TryGet(snapshot.ItemDefinitionId, out ItemDefinition definition);
                    return new PrototypeCraftingCatalystChoice
                    {
                        ItemInstanceId = snapshot.ItemInstanceId,
                        ItemDefinitionId = snapshot.ItemDefinitionId,
                        DisplayName = definition?.DisplayName ?? snapshot.ItemDefinitionId,
                        RarityName = definition?.Rarity?.DisplayName ?? "Unrated",
                        RarityRank = definition?.Rarity?.Rank ?? 0,
                        AvailableQuantity = snapshot.StackQuantity
                    };
                })
                .OrderBy(choice => choice.DisplayName, StringComparer.Ordinal)
                .ThenBy(choice => choice.ItemInstanceId, StringComparer.Ordinal)
                .ToArray();
        }

        public string DescribePrototypeCraftingCatalyst(string recipeId, string itemDefinitionId, int quantity)
        {
            EnsureInitialized();
            DefinitionRegistry registry = GetDefinitionRegistry();
            if (!registry.TryGet(itemDefinitionId, out ItemDefinition catalyst))
            {
                return "The selected catalyst item is unavailable.";
            }

            CraftingScalingCalculation scaling = GetCraftingScaling(recipeId);
            CraftingCatalystEffectDefinition[] effects = registry.DefinitionsById.Values
                .OfType<CraftingCatalystEffectDefinition>()
                .Where(effect => effect.Matches(catalyst))
                .OrderBy(effect => effect.Id, StringComparer.Ordinal)
                .ToArray();
            if (effects.Length == 0)
            {
                return $"{catalyst.DisplayName} has no authored catalyst effect. It will still be consumed.";
            }

            int skillTier = scaling?.SkillUsed == true ? scaling.SkillGradeTier : 0;
            return string.Join(" | ", effects.Select(effect =>
            {
                float chance = effect.CalculateChance(catalyst, Math.Max(1, quantity), skillTier);
                string tier = effect.SelectAffixTierId(catalyst, Math.Max(1, quantity));
                return $"{effect.DisplayName}: {chance:P0} chance, {FormatAffixTier(tier)}";
            }));
        }

        public CraftingScalingCalculation GetCraftingScaling(string recipeId)
        {
            EnsureInitialized();
            ResolvePlayerPersistenceReferences();
            DefinitionRegistry registry = GetDefinitionRegistry();
            return !string.IsNullOrWhiteSpace(recipeId) && registry.TryGet(recipeId, out RecipeDefinition recipe)
                ? CraftingScalingCalculator.Calculate(recipe, registry, playerSkills)
                : null;
        }

        public PrototypeCraftingStartResult BeginCraftAtPrototypeWorkstation(string recipeId)
        {
            return BeginSlotCraftAtPrototypeWorkstation(BuildAutoFilledSlotCraftingRequest(recipeId));
        }

        public PrototypeCraftingStartResult BeginSlotCraftAtPrototypeWorkstation(SlotCraftingRequest slotRequest)
        {
            EnsureInitialized();
            ResolvePlayerPersistenceReferences();
            if (activePrototypeCraft != null)
            {
                return PrototypeCraftingStartResult.Failure($"{activePrototypeCraft.RecipeDisplayName} is already being crafted.");
            }

            DefinitionRegistry registry = GetDefinitionRegistry();
            if (playerInventory == null)
            {
                return PrototypeCraftingStartResult.Failure("Player inventory is unavailable.");
            }

            GovernmentPermitCheckResult craftPermit = Governments.EvaluateRegulatedAction(
                ResolvePlayerPersonId(),
                GovernmentPermitHolderCategory.Person,
                "government.action.operate-regulated-workshop",
                PrototypeInstitutionalContentIds.TownJurisdiction,
                playTimeTracker?.CumulativeSeconds ?? Time.realtimeSinceStartupAsDouble);
            if (!craftPermit.Allowed) return PrototypeCraftingStartResult.Failure($"Crafting is not permitted here: {craftPermit.Message}");

            string recipeId = slotRequest?.recipeId;
            if (string.IsNullOrWhiteSpace(recipeId) || !registry.TryGet(recipeId, out RecipeDefinition recipe))
            {
                return PrototypeCraftingStartResult.Failure($"Recipe '{recipeId}' is unavailable.");
            }

            EnsureGroup6GameplayRuntime();
            ItemIdentityInventoryBridgeResult synchronized = playerItemIdentitySynchronizer?.SynchronizeNow();
            if (synchronized != null && !synchronized.Succeeded)
            {
                return PrototypeCraftingStartResult.Failure(synchronized.Message);
            }

            EnsureExtendedItemState();
            string personId = playerService?.PlayerId ?? PersistenceService.LocalPlayerId;
            if (!ValidateSlotCraftingRequest(slotRequest, recipe, personId, out string slotFailure))
            {
                return PrototypeCraftingStartResult.Failure(slotFailure);
            }

            CraftingScalingCalculation scaling = CraftingScalingCalculator.Calculate(recipe, registry, playerSkills);
            string operationId = Guid.NewGuid().ToString("D");
            CraftingExecutionRequest request = BuildCraftingExecutionRequest(recipe, personId, operationId, scaling, slotRequest);
            CraftingExecutionResult preview = CraftingExecution.Preview(
                request,
                registry,
                Recipes,
                ProductionRequirements,
                ItemIdentities,
                ItemDurability);
            if (preview == null || !preview.Succeeded)
            {
                return PrototypeCraftingStartResult.Failure(preview?.Message ?? "Crafting requirements could not be prepared.");
            }

            double now = CurrentCraftingElapsedSeconds();
            activePrototypeCraft = new PrototypeActiveCraftSnapshot
            {
                OperationId = operationId,
                RecipeId = recipe.Id,
                RecipeDisplayName = recipe.DisplayName,
                StartedAtSeconds = now,
                DurationSeconds = scaling.FinalDurationSeconds,
                RemainingSeconds = scaling.FinalDurationSeconds,
                ProgressNormalized = 0f,
                Scaling = scaling,
                SlotRequest = slotRequest.Clone()
            };
            dirtyTracker?.MarkDirty($"Crafting started: {recipe.Id}.");
            return PrototypeCraftingStartResult.Success($"Started {recipe.DisplayName} ({scaling.FinalDurationSeconds:0.0}s, {scaling.SkillLabel}).", SnapshotActiveCraft());
        }

        public bool CancelActivePrototypeCraft()
        {
            if (activePrototypeCraft == null)
            {
                return false;
            }

            string recipeId = activePrototypeCraft.RecipeId;
            activePrototypeCraft = null;
            dirtyTracker?.MarkDirty($"Crafting cancelled: {recipeId}.");
            return true;
        }

        private PrototypeCraftingResult CompleteCraftAtPrototypeWorkstation(
            string recipeId,
            CraftingScalingCalculation lockedScaling,
            string lockedOperationId,
            SlotCraftingRequest lockedSlotRequest)
        {
            EnsureInitialized();
            ResolvePlayerPersistenceReferences();
            DefinitionRegistry registry = GetDefinitionRegistry();
            if (playerInventory == null)
            {
                return PrototypeCraftingResult.Failure("Player inventory is unavailable.");
            }

            if (string.IsNullOrWhiteSpace(recipeId) || !registry.TryGet(recipeId, out RecipeDefinition recipe))
            {
                return PrototypeCraftingResult.Failure($"Recipe '{recipeId}' is unavailable.");
            }

            EnsureGroup6GameplayRuntime();
            ItemIdentityInventoryBridgeResult synchronized = playerItemIdentitySynchronizer?.SynchronizeNow();
            if (synchronized != null && !synchronized.Succeeded)
            {
                return PrototypeCraftingResult.Failure(synchronized.Message);
            }

            EnsureExtendedItemState();
            string personId = playerService?.PlayerId ?? PersistenceService.LocalPlayerId;
            string operationId = string.IsNullOrWhiteSpace(lockedOperationId) ? Guid.NewGuid().ToString("D") : lockedOperationId;
            CraftingScalingCalculation scaling = lockedScaling ?? CraftingScalingCalculator.Calculate(recipe, registry, playerSkills);
            SlotCraftingRequest slotRequest = lockedSlotRequest ?? BuildAutoFilledSlotCraftingRequest(recipe.Id);
            if (!ValidateSlotCraftingRequest(slotRequest, recipe, personId, out string slotFailure))
            {
                return PrototypeCraftingResult.Failure(slotFailure);
            }

            Group6TransactionSnapshot rollback = Group6TransactionSnapshot.Capture(this, playerInventory);
            CraftingExecutionResult execution = CraftingExecution.Execute(
                BuildCraftingExecutionRequest(recipe, personId, operationId, scaling, slotRequest),
                registry,
                Recipes,
                ProductionRequirements,
                ItemIdentities,
                ItemCompositions,
                ItemQualityAffixes,
                ItemDurability);

            if (execution == null || !execution.Succeeded || execution.Operation == null)
            {
                rollback.Restore(this, playerInventory, registry);
                return PrototypeCraftingResult.Failure(execution?.Message ?? "Crafting failed.");
            }

            group6InventoryTransaction = true;
            bool projectionApplied;
            string projectionFailure;
            try
            {
                projectionApplied = ApplyCraftingInventoryProjection(execution.Operation, registry, out projectionFailure);
            }
            finally
            {
                group6InventoryTransaction = false;
            }

            if (!projectionApplied)
            {
                rollback.Restore(this, playerInventory, registry);
                return PrototypeCraftingResult.Failure(projectionFailure);
            }

            EnsureExtendedItemState();
            playerStats?.RefreshEquipmentModifiers();
            RecordCraftingSkillUse(recipe, execution.Operation, personId);
            IReadOnlyList<ProfessionalActivityOperationResult> professionalActivities = ProfessionCoordinator.RecordCrafting(
                execution.Operation,
                ResolveCraftingProfessionalQuality(execution.Operation, scaling.ExpectedQuality),
                ResolveCraftingProfessionalDifficulty(execution.Operation));
            RecordPrototypeCraftPayroll(execution.Operation, professionalActivities, out string payrollMessage);
            string outputSummary = string.Join(", ", execution.Operation.outputs
                .Where(output => output.createdItemInstance)
                .Select(output =>
                {
                    if (ItemIdentities.TryGetSnapshot(output.itemInstanceId, out ItemInstanceSnapshot snapshot)
                        && !string.IsNullOrWhiteSpace(snapshot.CustomName))
                    {
                        return $"{output.quantity:0} x {snapshot.CustomName}";
                    }

                    return registry.TryGet(output.itemDefinitionId, out ItemDefinition definition)
                        ? $"{output.quantity:0} x {definition.DisplayName}"
                        : $"{output.quantity:0} x {output.itemDefinitionId}";
                }));
            string skillSummary = scaling.SkillUsed ? $" using {scaling.SkillId} {scaling.SkillGrade}" : " without a learned crafting Skill";
            string catalystSummary = DescribeCatalystResult(execution.Operation, registry);
            string payrollSummary = string.IsNullOrWhiteSpace(payrollMessage) ? string.Empty : $" {payrollMessage}";
            return PrototypeCraftingResult.Success($"Crafted {outputSummary}{skillSummary} at {scaling.ExpectedQuality:P0} expected quality.{catalystSummary}{payrollSummary}", execution.Operation);
        }

        private int ResolveCraftingProfessionalQuality(CraftingOperationRecordData operation, float fallbackQuality)
        {
            float[] qualities = (operation?.outputs ?? new List<CraftingOutputItemData>())
                .Where(output => output != null && !string.IsNullOrWhiteSpace(output.itemInstanceId))
                .Select(output => ItemQualityAffixes.TryGetQualityForItem(output.itemInstanceId, out ItemQualitySnapshot snapshot) ? snapshot.OverallQuality : -1f)
                .Where(value => value >= 0f)
                .ToArray();
            float measured = qualities.Length > 0 ? qualities.Average() : Mathf.Clamp01(fallbackQuality);
            return Mathf.Clamp(Mathf.RoundToInt(measured * 1000f), 0, 1000);
        }

        private static ProfessionalActivityDifficulty ResolveCraftingProfessionalDifficulty(CraftingOperationRecordData operation)
        {
            float score = (operation?.craftDurationSeconds ?? 0f) / 30f
                + Math.Max(0, (operation?.consumedInputs?.Count ?? 0) - 1)
                + Math.Max(0, operation?.catalysts?.Count ?? 0);
            if (score >= 8f) return ProfessionalActivityDifficulty.Advanced;
            if (score >= 4f) return ProfessionalActivityDifficulty.Skilled;
            if (score >= 1f) return ProfessionalActivityDifficulty.Routine;
            return ProfessionalActivityDifficulty.Trivial;
        }

        private static string DescribeCatalystResult(CraftingOperationRecordData operation, DefinitionRegistry registry)
        {
            CraftingCatalystUseData catalyst = operation?.catalysts?.FirstOrDefault();
            if (catalyst == null)
            {
                return string.Empty;
            }

            string itemName = registry != null && registry.TryGet(catalyst.itemDefinitionId, out ItemDefinition item)
                ? item.DisplayName
                : catalyst.itemDefinitionId;
            if (string.IsNullOrWhiteSpace(catalyst.effectDefinitionId))
            {
                return $" {itemName} was consumed but has no authored catalyst effect.";
            }

            string effectName = registry != null && registry.TryGet(catalyst.effectDefinitionId, out CraftingCatalystEffectDefinition effect)
                ? effect.DisplayName
                : catalyst.effectDefinitionId;
            return catalyst.effectApplied
                ? $" Catalyst succeeded: {effectName} ({FormatAffixTier(catalyst.affixTierId)}, roll {catalyst.effectRoll:P0} vs {catalyst.effectChance:P0})."
                : $" Catalyst did not activate: {effectName} (roll {catalyst.effectRoll:P0} vs {catalyst.effectChance:P0}).";
        }

        private static string FormatAffixTier(string tierId)
        {
            if (string.IsNullOrWhiteSpace(tierId))
            {
                return "no affix tier";
            }

            int separator = tierId.LastIndexOf('.');
            return separator >= 0 && separator < tierId.Length - 1
                ? $"affix tier {tierId.Substring(separator + 1)}"
                : tierId;
        }

        private CraftingExecutionRequest BuildCraftingExecutionRequest(
            RecipeDefinition recipe,
            string personId,
            string operationId,
            CraftingScalingCalculation scaling,
            SlotCraftingRequest slotRequest)
        {
            return new CraftingExecutionRequest
            {
                operationId = operationId,
                recipeId = recipe.Id,
                actorPersonId = personId,
                ownerPersonId = personId,
                custodianPersonId = personId,
                locationId = PrototypeWorkstationLocationId,
                worldTime = CurrentCraftingWorldTime(),
                deterministicSeed = operationId,
                craftingSkillId = scaling?.SkillId ?? string.Empty,
                craftingSkillGrade = scaling == null ? 0 : (int)scaling.SkillGrade,
                craftingSkillUsed = scaling?.SkillUsed ?? false,
                craftDurationSeconds = scaling?.FinalDurationSeconds ?? 0f,
                craftingQualityAdjustment = scaling?.QualityAdjustment ?? 0f,
                craftingAffixChanceBonus = scaling?.AffixChanceBonus ?? 0f,
                catalysts = (slotRequest?.slots ?? new List<CraftingSlotEntryData>())
                    .Where(slot => slot.kind == CraftingSlotKind.OptionalCatalyst)
                    .Select(slot => new CraftingCatalystUseData
                    {
                        slotId = slot.slotId,
                        itemDefinitionId = slot.itemDefinitionId,
                        itemInstanceId = slot.itemInstanceId,
                        quantity = slot.quantity
                    })
                    .ToList(),
                productionContext = BuildProductionContext(personId, slotRequest),
                personFacing = true
            };
        }

        private void AdvanceGroup6Crafting()
        {
            if (activePrototypeCraft == null)
            {
                return;
            }

            float elapsed = Mathf.Max(0f, (float)(CurrentCraftingElapsedSeconds() - activePrototypeCraft.StartedAtSeconds));
            activePrototypeCraft.RemainingSeconds = Mathf.Max(0f, activePrototypeCraft.DurationSeconds - elapsed);
            activePrototypeCraft.ProgressNormalized = activePrototypeCraft.DurationSeconds <= 0f
                ? 1f
                : Mathf.Clamp01(elapsed / activePrototypeCraft.DurationSeconds);
            if (activePrototypeCraft.RemainingSeconds > 0f)
            {
                return;
            }

            PrototypeActiveCraftSnapshot completed = SnapshotActiveCraft();
            activePrototypeCraft = null;
            PrototypeCraftingResult result = CompleteCraftAtPrototypeWorkstation(
                completed.RecipeId,
                completed.Scaling,
                completed.OperationId,
                completed.SlotRequest);
            dirtyTracker?.MarkDirty(result.Succeeded
                ? $"Crafting completed: {completed.RecipeId}."
                : $"Crafting failed at completion: {completed.RecipeId}.");
            PrototypeCraftCompleted?.Invoke(result);
        }

        private PrototypeActiveCraftSnapshot SnapshotActiveCraft()
        {
            if (activePrototypeCraft == null)
            {
                return null;
            }

            return new PrototypeActiveCraftSnapshot
            {
                OperationId = activePrototypeCraft.OperationId,
                RecipeId = activePrototypeCraft.RecipeId,
                RecipeDisplayName = activePrototypeCraft.RecipeDisplayName,
                StartedAtSeconds = activePrototypeCraft.StartedAtSeconds,
                DurationSeconds = activePrototypeCraft.DurationSeconds,
                RemainingSeconds = activePrototypeCraft.RemainingSeconds,
                ProgressNormalized = activePrototypeCraft.ProgressNormalized,
                Scaling = CloneScaling(activePrototypeCraft.Scaling),
                SlotRequest = activePrototypeCraft.SlotRequest?.Clone()
            };
        }

        private static CraftingScalingCalculation CloneScaling(CraftingScalingCalculation value)
        {
            if (value == null)
            {
                return null;
            }

            return new CraftingScalingCalculation
            {
                RecipeId = value.RecipeId,
                BaseDurationSeconds = value.BaseDurationSeconds,
                FinalDurationSeconds = value.FinalDurationSeconds,
                SkillId = value.SkillId,
                SkillGrade = value.SkillGrade,
                SkillUsed = value.SkillUsed,
                SkillGradeTier = value.SkillGradeTier,
                HighestOutputRarityRank = value.HighestOutputRarityRank,
                OutputStatComplexity = value.OutputStatComplexity,
                DurationSkillMultiplier = value.DurationSkillMultiplier,
                DurationRarityMultiplier = value.DurationRarityMultiplier,
                DurationStatMultiplier = value.DurationStatMultiplier,
                QualityAdjustment = value.QualityAdjustment,
                ExpectedQuality = value.ExpectedQuality,
                AffixChanceBonus = value.AffixChanceBonus
            };
        }

        private void RecordCraftingSkillUse(RecipeDefinition recipe, CraftingOperationRecordData operation, string personId)
        {
            if (playerSkills == null || recipe == null || operation == null)
            {
                return;
            }

            DefinitionRegistry registry = GetDefinitionRegistry();
            SkillDefinition skill = recipe.CraftingScaling.eligibleSkillIds
                .Select(id => registry.TryGet(id, out SkillDefinition definition) ? definition : null)
                .FirstOrDefault(definition => definition != null);
            if (skill == null)
            {
                return;
            }

            CraftingOutputItemData primary = operation.outputs.FirstOrDefault(output => output.createdItemInstance);
            registry.TryGet(primary?.itemDefinitionId ?? string.Empty, out ItemDefinition outputItem);
            playerSkills.RecordQualifyingAction(new SkillActionExecutionEvent
            {
                EventId = $"crafting.{operation.operationId}",
                ActorId = personId,
                ActionDefinitionId = skill.NaturalLearning.QualifyingEventId,
                ActionCategory = SkillActionEventCategory.CraftingAction,
                ItemDefinitionId = outputItem?.Id ?? string.Empty,
                ItemCategory = outputItem?.PrimaryCategory,
                ItemTags = outputItem?.Tags ?? Array.Empty<TagDefinition>(),
                Executed = true,
                IntendedResultSucceeded = true,
                PlaytimeSeconds = playTimeTracker?.CumulativeSeconds ?? 0d,
                SourceSystem = "crafting",
                ServerAuthoritative = true
            });
        }

        private double CurrentCraftingElapsedSeconds()
        {
            return playTimeTracker?.CumulativeSeconds ?? Time.realtimeSinceStartupAsDouble;
        }

        private void EnsureGroup6GameplayRuntime()
        {
            ResolvePlayerPersistenceReferences();
            DefinitionRegistry registry = GetDefinitionRegistry();
            if (!group6BindingsConfigured)
            {
                playerStats?.ConfigureItemRuntimeProvider(this);
                if (playerInventory != null)
                {
                    playerInventory.InventoryChanged -= OnGroup6InventoryChanged;
                    playerInventory.InventoryChanged += OnGroup6InventoryChanged;
                }


                ItemDurability.ItemDurabilityStateChanged -= OnGroup6DurabilityChanged;
                ItemDurability.ItemDurabilityStateChanged += OnGroup6DurabilityChanged;
                PlayerMeleeCombat meleeCombat = playerEquipment == null ? null : playerEquipment.GetComponent<PlayerMeleeCombat>();
                if (meleeCombat != null)
                {
                    meleeCombat.AttackResolved -= OnGroup6AttackResolved;
                    meleeCombat.AttackResolved += OnGroup6AttackResolved;
                }

                group6BindingsConfigured = true;
            }

            if (registry.TryGet(PrototypeWorkstationDefinitionId, out ProductionStationDefinition station))
            {
                if (!ProductionRequirements.TryGetStation(PrototypeWorkstationInstanceId, out _))
                {
                    ProductionRequirements.RegisterStation(
                        station,
                        PrototypeWorkstationInstanceId,
                        PrototypeWorkstationLocationId,
                        ownerId: playerService?.WorldId ?? PersistenceService.LocalWorldId);
                }
            }

            string personId = playerService?.PlayerId ?? PersistenceService.LocalPlayerId;
            foreach (RecipeDefinition recipe in registry.DefinitionsById.Values.OfType<RecipeDefinition>().Where(recipe => recipe.State == RecipeLifecycleState.Active))
            {
                string recordId = $"recipe-knowledge.{personId}.{recipe.Id}";
                if (RecipeKnowledge.TryGet(recordId, out _))
                {
                    continue;
                }

                RecipeKnowledge.LearnOrUpdate(new RecipeKnowledgeRecordData
                {
                    recordId = recordId,
                    personId = personId,
                    recipeId = recipe.Id,
                    versionId = recipe.CurrentVersionId,
                    completeness = RecipeKnowledgeCompleteness.Complete,
                    knownInputIds = recipe.Inputs.Select(input => input.inputId).ToArray(),
                    knownOutputIds = recipe.Outputs.Select(output => output.outputId).ToArray(),
                    knownStepIds = recipe.ProcedureSteps.Select(step => step.stepId).ToArray(),
                    sourceIds = new[] { "source.prototype.starting-crafting-knowledge" }
                });
            }

            playerItemIdentitySynchronizer?.SynchronizeNow();
            EnsureExtendedItemState();
        }

        private void OnGroup6InventoryChanged()
        {
            if (group6InventoryTransaction)
            {
                return;
            }

            playerItemIdentitySynchronizer?.SynchronizeNow();
            EnsureExtendedItemState();
        }

        private void OnGroup6DurabilityChanged(string itemInstanceId)
        {
            dirtyTracker?.MarkDirty($"Item durability changed: {itemInstanceId}.");
            ShowDurabilityFeedback(itemInstanceId);
            if (!resolvingForcedItemDecomposition)
            {
                TryResolveForcedItemDecomposition(itemInstanceId);
            }
        }

        private bool TryResolveForcedItemDecomposition(string itemInstanceId)
        {
            if (!ItemDurability.TryGetDurabilityForItem(itemInstanceId, out ItemDurabilitySnapshot durability)
                || !durability.PendingForcedDecomposition
                || !ItemIdentities.TryGetSnapshot(itemInstanceId, out ItemInstanceSnapshot item)
                || !ItemCompositions.TryGetSnapshotForItem(itemInstanceId, out ItemCompositionSnapshot composition)
                || composition.Materials.Count == 0)
            {
                return false;
            }

            if (item.LocationKind == ItemLocationKind.WorldPlacement)
            {
                return false;
            }

            bool storedInInventory = item.LocationKind == ItemLocationKind.Inventory;
            bool storedInContainer = item.LocationKind == ItemLocationKind.Container;
            bool equipped = item.LocationKind == ItemLocationKind.Equipped;
            if (!storedInInventory && !storedInContainer && !equipped)
            {
                return false;
            }

            string playerId = playerService?.PlayerId ?? PersistenceService.LocalPlayerId;
            string inventoryOwnerId = item.Data.location?.inventoryOwnerId ?? string.Empty;
            string containerId = item.Data.location?.containerId ?? string.Empty;
            bool projectsToPlayerInventory = storedInInventory && string.Equals(inventoryOwnerId, playerId, StringComparison.Ordinal);
            Group6TransactionSnapshot rollback = Group6TransactionSnapshot.Capture(this, playerInventory);
            string operationId = $"natural-forced.{itemInstanceId}.{durability.Data.breakCheckSequence}";
            DisassemblyResult result;
            IDisposable synchronizationPause = playerItemIdentitySynchronizer?.PauseAutomaticSynchronization();
            resolvingForcedItemDecomposition = true;
            group6InventoryTransaction = true;
            try
            {
                result = ItemRecovery.Execute(new DisassemblyRequest
                {
                    operationId = operationId,
                    itemInstanceId = itemInstanceId,
                    actorPersonId = "actor.nature",
                    ownerPersonId = storedInInventory ? inventoryOwnerId : item.OwnerPersonId,
                    worldTime = CurrentCraftingWorldTime(),
                    deterministicSeed = operationId,
                    operationKind = ItemRecoveryOperationKind.NaturalDecomposition,
                    skillUsed = false,
                    materializeOutputIdentities = storedInInventory || storedInContainer
                }, GetDefinitionRegistry(), ItemIdentities, ItemCompositions, ItemQualityAffixes, ItemDurability);
                if (!result.Succeeded || result.Operation == null)
                {
                    rollback.Restore(this, playerInventory, GetDefinitionRegistry());
                    Debug.LogWarning($"Forced item decomposition failed for '{itemInstanceId}': {result.Message}");
                    return false;
                }

                if (equipped)
                {
                    if (playerEquipment == null || !playerEquipment.RemoveItemForDecomposition(itemInstanceId, notifyChange: false))
                    {
                        rollback.Restore(this, playerInventory, GetDefinitionRegistry());
                        Debug.LogWarning($"Forced item decomposition could not remove equipped item '{itemInstanceId}'.");
                        return false;
                    }
                    SpawnSalvageableRecoveryOutputs(result.Operation, playerEquipment.transform.position, playerEquipment.transform.parent);
                    playerEquipment.NotifyEquipmentStateChanged();
                }
                else if (projectsToPlayerInventory)
                {
                    int sourceSlot = FindInventorySlot(itemInstanceId);
                    string inventoryFailure = string.Empty;
                    if (sourceSlot < 0 || !playerInventory.RemoveItemAt(sourceSlot, item.StackQuantity)
                        || !TryAddRecoveryOutputsToPlayerInventory(result.Operation, out inventoryFailure))
                    {
                        rollback.Restore(this, playerInventory, GetDefinitionRegistry());
                        Debug.LogWarning(string.IsNullOrWhiteSpace(inventoryFailure) ? $"Forced item decomposition could not remove stored item '{itemInstanceId}'." : inventoryFailure);
                        return false;
                    }
                }
                else if (storedInContainer)
                {
                    foreach (string outputId in result.Operation.outcomes.SelectMany(outcome => outcome.outputItemInstanceIds ?? Array.Empty<string>()))
                    {
                        ItemInstanceOperationResult moved = ItemIdentities.SetContainerLocation(outputId, containerId);
                        if (!moved.Succeeded)
                        {
                            rollback.Restore(this, playerInventory, GetDefinitionRegistry());
                            Debug.LogWarning($"Forced item decomposition could not retain recovered material in container '{containerId}': {moved.Message}");
                            return false;
                        }
                    }
                }

                ItemIdentityInventoryBridgeResult synchronized = playerItemIdentitySynchronizer?.SynchronizeNow();
                if (synchronized != null && !synchronized.Succeeded)
                {
                    rollback.Restore(this, playerInventory, GetDefinitionRegistry());
                    Debug.LogWarning($"Forced item decomposition could not synchronize inventory/equipment identity state: {synchronized.Message}");
                    return false;
                }

                ItemInstanceOperationResult finalizedSource = ItemIdentities.MarkDisassembled(itemInstanceId);
                if (!finalizedSource.Succeeded)
                {
                    rollback.Restore(this, playerInventory, GetDefinitionRegistry());
                    Debug.LogWarning($"Forced item decomposition could not finalize source item '{itemInstanceId}': {finalizedSource.Message}");
                    return false;
                }
            }
            finally
            {
                group6InventoryTransaction = false;
                resolvingForcedItemDecomposition = false;
                synchronizationPause?.Dispose();
            }

            dirtyTracker?.MarkDirty($"Item forcibly decomposed at five percent durability: {itemInstanceId}.");
            PrototypeHudMessageBus.Show(equipped ? "Your item collapsed into salvageable parts" : "A stored item decomposed into recovered materials");
            return true;
        }

        private void ShowDurabilityFeedback(string itemInstanceId)
        {
            if (!ItemDurability.TryGetDurabilityForItem(itemInstanceId, out ItemDurabilitySnapshot current)
                || !ItemIdentities.TryGetSnapshot(itemInstanceId, out ItemInstanceSnapshot item)
                || !IsPlayerRelevantItem(item))
            {
                return;
            }

            durabilityFeedbackByItemId.TryGetValue(itemInstanceId, out DurabilityFeedbackState previous);
            durabilityFeedbackByItemId[itemInstanceId] = new DurabilityFeedbackState(current);
            string itemName = ItemDisplayName(item);
            int percent = Mathf.RoundToInt(current.NormalizedDurability * 100f);
            if (current.PendingForcedDecomposition && (previous == null || !previous.PendingForcedDecomposition))
            {
                PrototypeHudMessageBus.Show($"{itemName} reached {percent}% durability without breaking and is decomposing");
            }
            else if (current.HasBroken && (previous == null || !previous.HasBroken))
            {
                PrototypeHudMessageBus.Show($"{itemName} broke at {percent}% durability and no longer functions");
            }
            else if (current.NormalizedDurability <= ItemDurability.BreakCheckStartNormalized
                && (previous == null || previous.NormalizedDurability > ItemDurability.BreakCheckStartNormalized))
            {
                PrototypeHudMessageBus.Show($"{itemName} is critically damaged ({percent}% durability); each further percent can break it");
            }
        }

        private bool IsPlayerRelevantItem(ItemInstanceSnapshot item)
        {
            string playerId = playerService?.PlayerId ?? PersistenceService.LocalPlayerId;
            string inventoryOwnerId = item.Data.location?.inventoryOwnerId ?? string.Empty;
            return string.Equals(item.OwnerPersonId, playerId, StringComparison.Ordinal)
                || string.Equals(item.CustodianPersonId, playerId, StringComparison.Ordinal)
                || string.Equals(inventoryOwnerId, playerId, StringComparison.Ordinal);
        }

        private string ItemDisplayName(ItemInstanceSnapshot item)
        {
            if (!string.IsNullOrWhiteSpace(item?.CustomName))
            {
                return item.CustomName;
            }

            return item != null && GetDefinitionRegistry().TryGet(item.ItemDefinitionId, out ItemDefinition definition)
                ? definition.DisplayName
                : item?.ItemDefinitionId ?? "Item";
        }

        private ItemDegradationPolicyDefinition ResolveItemDegradationPolicy()
        {
            return ItemDegradationPolicyDefinition.Resolve(GetDefinitionRegistry())
                ?? throw new InvalidOperationException($"Catalog definition '{ItemDegradationPolicyDefinition.StandardPolicyId}' is required for item degradation.");
        }

        private sealed class DurabilityFeedbackState
        {
            public DurabilityFeedbackState(ItemDurabilitySnapshot snapshot)
            {
                NormalizedDurability = snapshot?.NormalizedDurability ?? 1f;
                HasBroken = snapshot?.HasBroken ?? false;
                PendingForcedDecomposition = snapshot?.PendingForcedDecomposition ?? false;
            }

            public float NormalizedDurability { get; }
            public bool HasBroken { get; }
            public bool PendingForcedDecomposition { get; }
        }

        private bool TryAddRecoveryOutputsToPlayerInventory(DisassemblyOperationRecordData operation, out string failure)
        {
            failure = string.Empty;
            foreach (string outputId in operation.outcomes.SelectMany(outcome => outcome.outputItemInstanceIds ?? Array.Empty<string>()))
            {
                if (!ItemIdentities.TryGetSnapshot(outputId, out ItemInstanceSnapshot output)
                    || !GetDefinitionRegistry().TryGet(output.ItemDefinitionId, out ItemDefinition definition))
                {
                    failure = "A forced-decomposition output could not be resolved.";
                    return false;
                }

                InventoryInstanceOperationResult added = playerInventory.AddExistingItemIdentity(definition, outputId, output.StackQuantity);
                if (!added.Succeeded)
                {
                    failure = $"Recovered materials could not remain in inventory: {added.Message}";
                    return false;
                }
            }
            return true;
        }

        private void OnGroup6AttackResolved(MeleeAttackResult result)
        {
            if (!result.Started || equippedWeaponWearPerAttack <= 0f || playerEquipment == null)
            {
                return;
            }

            EquipmentSlotState mainHand = playerEquipment.GetSlot(EquipmentSlotType.MainHand);
            if (mainHand == null || mainHand.IsEmpty || string.IsNullOrWhiteSpace(mainHand.ItemInstanceId))
            {
                return;
            }

            EnsureExtendedItemState();
            ItemDurabilityOperationResult wear = ItemDurability.ApplyWear(
                ItemIdentities,
                ItemCompositions,
                ItemQualityAffixes,
                GetDefinitionRegistry(),
                mainHand.ItemInstanceId,
                equippedWeaponWearPerAttack,
                $"combat.attack.{result.AttackName}");
            if (!wear.Succeeded)
            {
                Debug.LogWarning($"Equipped weapon wear was not applied: {wear.Message}");
            }
        }

        private void EnsureExtendedItemState()
        {
            DefinitionRegistry registry = GetDefinitionRegistry();
            foreach (ItemInstanceSnapshot item in ItemIdentities.Snapshots
                .Where(snapshot => snapshot.LifecycleState is not (ItemLifecycleState.Consumed or ItemLifecycleState.Destroyed or ItemLifecycleState.Disassembled))
                .ToArray())
            {
                ItemCompositions.EnsureCompositionForItem(ItemIdentities, registry, item.ItemInstanceId);
                if (!registry.TryGet(item.ItemDefinitionId, out ItemDefinition definition) || definition.InstanceMode == ItemInstanceMode.DefinitionOnly)
                {
                    continue;
                }

                ItemQualityAffixes.EnsureDefaultQuality(ItemIdentities, ItemCompositions, registry, item.ItemInstanceId);
                ItemDurability.EnsureDefaultDurability(ItemIdentities, ItemCompositions, ItemQualityAffixes, registry, item.ItemInstanceId);
            }
        }

        private bool ValidateSlotCraftingRequest(SlotCraftingRequest request, RecipeDefinition recipe, string personId, out string failure)
        {
            failure = string.Empty;
            if (request == null || recipe == null || !string.Equals(request.recipeId, recipe.Id, StringComparison.Ordinal))
            {
                failure = "Slot crafting request does not identify the selected recipe.";
                return false;
            }

            List<CraftingSlotEntryData> slots = request.slots ?? new List<CraftingSlotEntryData>();
            if (slots.Any(slot => slot == null))
            {
                failure = "Slot crafting request contains an empty slot record.";
                return false;
            }

            if (slots.Select(slot => slot.slotId).Any(string.IsNullOrWhiteSpace)
                || slots.Select(slot => slot.slotId).Distinct(StringComparer.Ordinal).Count() != slots.Count)
            {
                failure = "Crafting slots require unique, non-empty slot IDs.";
                return false;
            }

            if (slots.Count(slot => slot.kind == CraftingSlotKind.OptionalCatalyst) > 1)
            {
                failure = "This crafting workflow supports one optional catalyst slot; combine identical catalyst items into that slot's quantity.";
                return false;
            }

            Dictionary<string, RecipeInputSpecificationData> requiredInputs = recipe.Inputs
                .Where(input => input != null && input.requirementState == RecipeRequirementState.Required)
                .ToDictionary(input => input.inputId, input => input, StringComparer.Ordinal);
            foreach (CraftingSlotEntryData slot in slots)
            {
                if (slot.quantity <= 0
                    || !ItemIdentities.TryGetSnapshot(slot.itemInstanceId, out ItemInstanceSnapshot item)
                    || !IsAvailableCraftingInventoryItem(item, personId)
                    || !string.Equals(item.ItemDefinitionId, slot.itemDefinitionId, StringComparison.Ordinal))
                {
                    failure = $"Crafting slot '{slot.slotId}' does not reference an available inventory item stack.";
                    return false;
                }

                if (slot.kind == CraftingSlotKind.RequiredInput)
                {
                    if (!requiredInputs.TryGetValue(slot.recipeInputId, out RecipeInputSpecificationData input)
                        || !GetDefinitionRegistry().TryGet(slot.itemDefinitionId, out ItemDefinition selectedDefinition)
                        || !RecipeInputMatcher.Matches(input, selectedDefinition, GetDefinitionRegistry()))
                    {
                        failure = $"Required slot '{slot.slotId}' does not match recipe input '{slot.recipeInputId}'.";
                        return false;
                    }
                }
                else if (slot.kind != CraftingSlotKind.OptionalCatalyst)
                {
                    failure = $"Crafting slot '{slot.slotId}' has an unsupported slot kind.";
                    return false;
                }
            }

            foreach (RecipeInputSpecificationData input in requiredInputs.Values)
            {
                if (input.unit != ProductionQuantityUnit.Count || Math.Abs(input.quantity - Math.Round(input.quantity)) > 0.0001f)
                {
                    failure = $"Prototype slot crafting currently requires whole-count input '{input.inputId}'.";
                    return false;
                }

                CraftingSlotEntryData[] assignedSlots = slots
                    .Where(slot => slot.kind == CraftingSlotKind.RequiredInput && string.Equals(slot.recipeInputId, input.inputId, StringComparison.Ordinal))
                    .ToArray();
                int assigned = assignedSlots.Sum(slot => slot.quantity);
                if (assigned != (int)Math.Round(input.quantity))
                {
                    failure = $"Recipe input '{input.inputId}' requires {(int)Math.Round(input.quantity)} items, but {assigned} are assigned.";
                    return false;
                }

                if (assignedSlots.Select(slot => slot.itemDefinitionId).Distinct(StringComparer.Ordinal).Count() > 1)
                {
                    failure = $"Recipe input '{input.inputId}' must use one resource type; multiple stacks of that same resource are allowed.";
                    return false;
                }
            }

            foreach (IGrouping<string, CraftingSlotEntryData> group in slots.GroupBy(slot => slot.itemInstanceId, StringComparer.Ordinal))
            {
                ItemIdentities.TryGetSnapshot(group.Key, out ItemInstanceSnapshot item);
                int assigned = group.Sum(slot => slot.quantity);
                if (item == null || assigned > item.StackQuantity)
                {
                    failure = $"Item stack '{group.Key}' has {item?.StackQuantity ?? 0} items, but {assigned} are assigned to crafting slots.";
                    return false;
                }
            }

            return true;
        }

        private static bool IsAvailableCraftingInventoryItem(ItemInstanceSnapshot item, string personId)
        {
            return item != null
                && item.LifecycleState is not (ItemLifecycleState.Consumed or ItemLifecycleState.Destroyed or ItemLifecycleState.Disassembled)
                && item.LocationKind == ItemLocationKind.Inventory
                && string.Equals(item.Data.location?.inventoryOwnerId, personId, StringComparison.Ordinal);
        }

        private ProductionContextData BuildProductionContext(string personId, SlotCraftingRequest slotRequest = null)
        {
            ProductionContextData context = new ProductionContextData
            {
                actorPersonId = personId,
                locationId = PrototypeWorkstationLocationId,
                worldTime = CurrentCraftingWorldTime(),
                perspective = ProductionEvaluationPerspective.Authoritative
            };

            Dictionary<string, int> assignedRequired = (slotRequest?.slots ?? new List<CraftingSlotEntryData>())
                .Where(slot => slot != null && slot.kind == CraftingSlotKind.RequiredInput)
                .GroupBy(slot => slot.itemInstanceId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Sum(slot => slot.quantity), StringComparer.Ordinal);
            bool restrictToSlots = slotRequest != null;
            foreach (ItemInstanceSnapshot item in ItemIdentities.Snapshots.Where(snapshot => IsAvailableCraftingInventoryItem(snapshot, personId)))
            {
                int assignedQuantity = item.StackQuantity;
                if (restrictToSlots && !assignedRequired.TryGetValue(item.ItemInstanceId, out assignedQuantity))
                {
                    continue;
                }

                int quantity = restrictToSlots ? assignedQuantity : item.StackQuantity;
                context.itemQuantities.Add(new ProductionQuantityData
                {
                    definitionId = item.ItemDefinitionId,
                    itemInstanceId = item.ItemInstanceId,
                    locationId = PrototypeWorkstationLocationId,
                    quantity = quantity,
                    sourceTotalQuantity = quantity,
                    unit = ProductionQuantityUnit.Count,
                    expectedRuntimeRevision = ItemIdentities.Revision,
                    expectedStackRevision = item.Revision,
                    perceived = true,
                    authoritative = true
                });
            }

            return context;
        }

        private bool ApplyCraftingInventoryProjection(CraftingOperationRecordData operation, DefinitionRegistry registry, out string failure)
        {
            failure = string.Empty;
            foreach (CraftingConsumedInputData consumed in operation.consumedInputs.Where(input => input.consumed))
            {
                int quantity = Math.Max(1, (int)Math.Round(consumed.quantity));
                int slotIndex = FindInventorySlot(consumed.itemInstanceId);
                if (slotIndex < 0 || !playerInventory.RemoveItemAt(slotIndex, quantity))
                {
                    failure = $"Crafting could not remove consumed input '{consumed.itemInstanceId}' from inventory.";
                    return false;
                }
            }

            foreach (CraftingCatalystUseData catalyst in (operation.catalysts ?? new List<CraftingCatalystUseData>()).Where(entry => entry.consumed))
            {
                int slotIndex = FindInventorySlot(catalyst.itemInstanceId);
                if (slotIndex < 0 || !playerInventory.RemoveItemAt(slotIndex, catalyst.quantity))
                {
                    failure = $"Crafting could not remove catalyst '{catalyst.itemInstanceId}' from inventory.";
                    return false;
                }
            }

            foreach (CraftingOutputItemData output in operation.outputs.Where(item => item.createdItemInstance))
            {
                if (!registry.TryGet(output.itemDefinitionId, out ItemDefinition definition))
                {
                    failure = $"Crafting output definition '{output.itemDefinitionId}' is unavailable.";
                    return false;
                }

                int quantity = Math.Max(1, (int)Math.Round(output.quantity));
                InventoryInstanceOperationResult added = playerInventory.AddExistingItemIdentity(definition, output.itemInstanceId, quantity);
                if (!added.Succeeded)
                {
                    failure = $"Crafted output could not be placed in inventory: {added.Message}";
                    return false;
                }
            }

            ItemIdentityInventoryBridgeResult synchronization = playerItemIdentitySynchronizer?.SynchronizeNow();
            if (synchronization != null && !synchronization.Succeeded)
            {
                failure = synchronization.Message;
                return false;
            }

            return true;
        }

        private int FindInventorySlot(string itemInstanceId)
        {
            for (int i = 0; i < playerInventory.Slots.Count; i++)
            {
                InventorySlot slot = playerInventory.Slots[i];
                if (slot != null && string.Equals(slot.ItemInstanceId, itemInstanceId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private string CurrentCraftingWorldTime()
        {
            return (playTimeTracker?.CumulativeSeconds ?? Time.realtimeSinceStartupAsDouble).ToString("0.###", CultureInfo.InvariantCulture);
        }

        private sealed class Group6TransactionSnapshot
        {
            private InventorySaveData inventory;
            private EquipmentSaveData equipment;
            private ItemInstanceRuntimeSaveData identities;
            private ItemCompositionRuntimeSaveData compositions;
            private ItemQualityAffixRuntimeSaveData quality;
            private ItemDurabilityRuntimeSaveData durability;
            private ProductionRequirementRuntimeSaveData requirements;
            private CraftingExecutionRuntimeSaveData crafting;
            private DisassemblyRuntimeSaveData disassembly;

            public static Group6TransactionSnapshot Capture(PrototypePersistenceServiceBehaviour service, PlayerInventory playerInventory)
            {
                return new Group6TransactionSnapshot
                {
                    inventory = playerInventory.CreateSaveData(),
                    equipment = service.playerEquipment?.CreateSaveData(),
                    identities = service.ItemIdentities.CreateSaveData(),
                    compositions = service.ItemCompositions.CreateSaveData(),
                    quality = service.ItemQualityAffixes.CreateSaveData(),
                    durability = service.ItemDurability.CreateSaveData(),
                    requirements = service.ProductionRequirements.CreateSaveData(),
                    crafting = service.CraftingExecution.CreateSaveData(),
                    disassembly = service.ItemRecovery.CreateSaveData()
                };
            }

            public void Restore(PrototypePersistenceServiceBehaviour service, PlayerInventory playerInventory, DefinitionRegistry registry)
            {
                playerInventory.TryRestoreFromSaveData(inventory, registry);
                if (equipment != null) service.playerEquipment?.TryRestoreFromSaveData(equipment, registry);
                service.ItemIdentities.RestoreFromSaveData(identities, registry);
                service.ItemCompositions.RestoreFromSaveData(compositions, registry, service.ItemIdentities);
                service.ItemQualityAffixes.RestoreFromSaveData(quality, registry, service.ItemIdentities);
                service.ItemDurability.RestoreFromSaveData(durability, registry, service.ItemIdentities, service.ItemCompositions);
                service.ProductionRequirements.RestoreFromSaveData(requirements);
                service.CraftingExecution.RestoreFromSaveData(crafting, registry);
                service.ItemRecovery.RestoreFromSaveData(disassembly, registry);
                service.playerItemIdentitySynchronizer?.SynchronizeNow();
            }
        }
    }
}
