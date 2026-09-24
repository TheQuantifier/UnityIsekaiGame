using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityIsekaiGame.Equipment;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Inventory.Disassembly;
using UnityIsekaiGame.Inventory.Durability;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Inventory.Recipes;
using UnityIsekaiGame.Professions;
using UnityIsekaiGame.Skills;

namespace UnityIsekaiGame.Editor
{
    [InitializeOnLoad]
    public static class Group6PrototypePlayModeVerifier
    {
        private const string ActiveKey = "UnityIsekaiGame.Group6.PlayModeVerification.Active";
        private const string ScenePath = "Assets/_Project/Scenes/Prototype/PrototypeScene.unity";
        private static int framesInPlayMode;
        private static bool verificationSucceeded;

        static Group6PrototypePlayModeVerifier()
        {
            if (SessionState.GetBool(ActiveKey, false))
            {
                Subscribe();
            }
        }

        public static void VerifyBatch()
        {
            SessionState.SetBool(ActiveKey, true);
            framesInPlayMode = 0;
            verificationSucceeded = false;
            Subscribe();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        private static void Subscribe()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(ActiveKey, false))
            {
                return;
            }

            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                framesInPlayMode = 0;
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.EraseBool(ActiveKey);
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                EditorApplication.update -= OnEditorUpdate;
                EditorApplication.Exit(verificationSucceeded ? 0 : 1);
            }
        }

        private static void OnEditorUpdate()
        {
            if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying)
            {
                return;
            }

            framesInPlayMode++;
            if (framesInPlayMode < 5)
            {
                return;
            }

            EditorApplication.update -= OnEditorUpdate;
            try
            {
                VerifyLiveScene();
                verificationSucceeded = true;
                Debug.Log("Group 6 Prototype play-mode verification passed: workstation, resource pickup, crafting, disassembly, natural decomposition, salvage scaling, and recovery roles verified.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                verificationSucceeded = false;
            }
            finally
            {
                EditorApplication.isPlaying = false;
            }
        }

        private static void VerifyLiveScene()
        {
            PrototypePersistenceServiceBehaviour services = UnityEngine.Object.FindAnyObjectByType<PrototypePersistenceServiceBehaviour>();
            PlayerInventory inventory = UnityEngine.Object.FindAnyObjectByType<PlayerInventory>();
            PrototypeCraftingWorkstation workstation = UnityEngine.Object.FindAnyObjectByType<PrototypeCraftingWorkstation>();
            WorldItemPickup woodPickup = UnityEngine.Object.FindObjectsByType<WorldItemPickup>(FindObjectsInactive.Include)
                .FirstOrDefault(candidate => candidate != null && candidate.Item != null && candidate.Item.Id == "item.wood-log");
            Require(services != null, "Prototype persistence services were not found.");
            Require(inventory != null, "Player inventory was not found.");
            Require(workstation != null, "Prototype crafting workstation was not found.");
            Transform craftingBlockVisual = workstation.transform.Find("Crafting Block Visual");
            Require(craftingBlockVisual != null, "Visible crafting-block cube was not found.");
            Require(craftingBlockVisual.GetComponent<MeshRenderer>() != null && craftingBlockVisual.GetComponent<MeshFilter>() != null, "Crafting-block cube is missing its visible mesh.");
            Require(craftingBlockVisual.GetComponent<Collider>() == null, "Crafting-block visual should not add a second collider that can obstruct interaction targeting.");
            Require(woodPickup != null, "Wood-log pickup was not found.");
            Require(woodPickup.GetComponentsInChildren<Collider>(true).Any(collider => collider.enabled && collider.isTrigger), "Wood-log pickup has no enabled trigger collider.");

            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>("Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset");
            DefinitionRegistry registry = catalog == null ? null : catalog.CreateRegistry();
            Require(registry != null, "Prototype definition catalog could not create a registry.");
            Require(registry.TryGet(ItemDegradationPolicyDefinition.StandardPolicyId, out ItemDegradationPolicyDefinition degradationPolicy), "Standard item-degradation policy is missing from the catalog.");
            Require(degradationPolicy.BreakChances.Count == 6, "Standard item-degradation policy does not contain the six authored break checks.");
            Require(registry.TryGet("item.prototype-iron-ore", out ItemDefinition ironOre), "Iron ore definition is missing.");
            Require(registry.TryGet("item.wood-log", out ItemDefinition woodLog), "Wood-log definition is missing.");
            Require(registry.TryGet("item.leather-strip", out ItemDefinition leatherStrip), "Leather-strip definition is missing.");
            Require(registry.TryGet("item.prototype-sword", out ItemDefinition swordDefinition), "Sword definition is missing.");
            Require(inventory.AddItem(ironOre, 20).AddedQuantity == 20, "Could not add iron ore for crafting verification.");
            Require(inventory.AddItem(woodLog, 10).AddedQuantity == 10, "Could not add wood logs for crafting verification.");
            Require(inventory.AddItem(leatherStrip, 5).AddedQuantity == 5, "Could not add leather strips for crafting verification.");

            RecipeDefinition[] recipes = services.AvailableCraftingRecipes.ToArray();
            Require(recipes.Length == 5, $"Expected 5 authored Prototype recipes, found {recipes.Length}.");
            PrototypeCraftingResult[] results = new PrototypeCraftingResult[recipes.Length];
            for (int index = 0; index < recipes.Length; index++)
            {
                results[index] = services.CraftAtPrototypeWorkstation(recipes[index].Id);
                Require(results[index].Succeeded, $"Recipe '{recipes[index].Id}' failed in play mode: {results[index].Message}");
            }

            Require(inventory.CountItem(woodLog) == 3, "Wood-log consumption did not match authored recipe costs.");
            Require(inventory.CountItem(ironOre) == 9, "Iron-ore consumption did not match authored recipe costs.");
            Require(inventory.CountItem(leatherStrip) == 4, "Leather-strip consumption did not match authored recipe costs.");
            string[] craftedNames = results
                .SelectMany(result => result.Operation.outputs)
                .Select(output => services.ItemIdentities.TryGetSnapshot(output.itemInstanceId, out var snapshot) ? snapshot.CustomName : string.Empty)
                .ToArray();
            Require(craftedNames.Contains("Iron Sword with Leather Grip"), "Crafted sword did not receive its material-derived name.");
            Require(craftedNames.Contains("Wood Bow with Wood Grip"), "Crafted bow did not receive its material-derived name.");
            Require(registry.TryGet("item.prototype-arrow", out ItemDefinition arrows), "Arrow definition is missing.");
            Require(inventory.CountItem(arrows) >= 10, "Crafted arrow bundle was not placed in inventory.");
            PrototypeCraftingResult containerCraft = services.CraftAtPrototypeWorkstation("recipe.prototype-sword");
            Require(containerCraft.Succeeded, $"Could not craft the additional container-decomposition item: {containerCraft.Message}");
            string containerItemId = containerCraft.Operation.outputs.First(output => output.itemDefinitionId == "item.prototype-sword").itemInstanceId;

            Require(registry.TryGet("skill.disassembly", out SkillDefinition _), "Disassembly skill definition is missing.");
            Require(registry.TryGet("skill.salvaging", out SkillDefinition _), "Salvaging skill definition is missing.");
            string craftedBowId = CraftedItemId(results, "item.prototype-bow");
            string craftedHelmetId = CraftedItemId(results, "item.prototype-helmet");
            string craftedSwordId = results.SelectMany(result => result.Operation.outputs)
                .First(output => output.itemDefinitionId == "item.prototype-sword").itemInstanceId;
            PrototypeItemRecoveryChoice swordRecovery = services.GetPrototypeItemRecoveryChoices()
                .FirstOrDefault(choice => choice.ItemInstanceId == craftedSwordId);
            Require(swordRecovery != null, "Crafted sword was not offered for disassembly.");
            Require(swordRecovery.Preview.outcomes.Any(outcome => outcome.sourceItemDefinitionId == "item.prototype-iron-ore"), "Sword disassembly did not preserve iron-resource provenance.");
            Require(swordRecovery.Preview.outcomes.Any(outcome => outcome.sourceItemDefinitionId == "item.leather-strip"), "Sword disassembly did not preserve leather-resource provenance.");
            DisassemblyResult recovery = services.RecoverPrototypeItem(craftedSwordId);
            Require(recovery.Succeeded, $"Crafted sword disassembly failed: {recovery.Message}");
            Require(services.ItemIdentities.TryGetSnapshot(craftedSwordId, out ItemInstanceSnapshot recoveredSource)
                && recoveredSource.LifecycleState == ItemLifecycleState.Disassembled, "Disassembled sword did not enter its terminal lifecycle state.");

            Require(services.ItemIdentities.TryGetSnapshot(craftedBowId, out ItemInstanceSnapshot bowBeforeCapacityCheck), "Crafted bow identity was unavailable.");
            long multiOutputSequence = FindMultiOutputNatureSequence(services, registry, craftedBowId, bowBeforeCapacityCheck.OwnerPersonId);
            List<string> capacityFillers = FillInventoryWithStatefulItems(inventory, swordDefinition);
            Require(inventory.Slots.All(slot => slot != null && !slot.IsEmpty), "Could not fill the inventory for atomic recovery verification.");
            ForceFivePercentDecomposition(services, registry, craftedBowId, multiOutputSequence);
            Require(services.ItemIdentities.TryGetSnapshot(craftedBowId, out ItemInstanceSnapshot rolledBackBow)
                && rolledBackBow.LocationKind == ItemLocationKind.Inventory
                && rolledBackBow.LifecycleState == ItemLifecycleState.InInventory, "Full-inventory decomposition did not roll back its source item.");
            Require(services.ItemDurability.TryGetDurabilityForItem(craftedBowId, out ItemDurabilitySnapshot rolledBackDurability)
                && rolledBackDurability.PendingForcedDecomposition, "Full-inventory rollback lost the pending decomposition state.");
            foreach (string fillerId in capacityFillers)
            {
                int fillerSlot = FindInventorySlot(inventory, fillerId);
                Require(fillerSlot >= 0 && inventory.RemoveItemAt(fillerSlot, 1), "Could not clear a verification inventory filler.");
            }
            ItemDurabilityOperationResult retrigger = services.ItemDurability.SetDurabilityRecord(
                services.ItemIdentities, services.ItemCompositions, services.ItemQualityAffixes, registry, rolledBackDurability.Data);
            Require(retrigger.Succeeded, $"Could not retry inventory decomposition after making room: {retrigger.Message}");
            Require(services.ItemIdentities.TryGetSnapshot(craftedBowId, out ItemInstanceSnapshot inventorySource)
                && inventorySource.LifecycleState == ItemLifecycleState.Disassembled, "A player-inventory item did not decompose at the 5% cutoff.");
            DisassemblyOperationRecordData inventoryOperation = RecoveryOperation(services, craftedBowId);
            foreach (string outputId in inventoryOperation.outcomes.SelectMany(outcome => outcome.outputItemInstanceIds ?? Array.Empty<string>()))
            {
                Require(services.ItemIdentities.TryGetSnapshot(outputId, out ItemInstanceSnapshot output)
                    && output.LocationKind == ItemLocationKind.Inventory, "A player-inventory decomposition output was not retained in inventory.");
            }

            PlayerEquipment equipment = UnityEngine.Object.FindAnyObjectByType<PlayerEquipment>();
            Require(equipment != null, "Player equipment was not found.");
            int helmetSlot = FindInventorySlot(inventory, craftedHelmetId);
            Require(helmetSlot >= 0 && equipment.EquipFromInventorySlot(helmetSlot).Succeeded, "Could not equip the crafted helmet for decomposition verification.");
            Require(services.ItemIdentities.TryGetSnapshot(craftedHelmetId, out ItemInstanceSnapshot equippedIdentity)
                && equippedIdentity.LocationKind == ItemLocationKind.Equipped, "Equipped helmet identity was not projected to an equipped location.");
            ForceFivePercentDecomposition(services, registry, craftedHelmetId);
            Require(equipment.GetSlot(EquipmentSlotType.Head).IsEmpty, "Forced decomposition did not clear the equipped helmet slot.");
            bool equippedSourceFound = services.ItemIdentities.TryGetSnapshot(craftedHelmetId, out ItemInstanceSnapshot equippedSource);
            Require(equippedSourceFound && equippedSource.LifecycleState == ItemLifecycleState.Disassembled,
                $"An equipped item did not decompose at the 5% cutoff (found={equippedSourceFound}, state={equippedSource?.LifecycleState}, location={equippedSource?.LocationKind}).");

            int containerItemSlot = FindInventorySlot(inventory, containerItemId);
            Require(containerItemSlot >= 0, "The crafted container test item was not found in inventory.");
            bool extracted = inventory.TryExtractSlotIdentity(containerItemSlot, out _, out string extractedContainerItemId, out string extractFailure);
            Require(extracted, $"Could not remove the crafted item for container verification: {extractFailure}");
            Require(string.Equals(extractedContainerItemId, containerItemId, StringComparison.Ordinal), "Container verification extracted the wrong item identity.");
            const string containerId = "container.group6.verification";
            Require(services.ItemIdentities.SetContainerLocation(containerItemId, containerId).Succeeded, "Could not place the crafted item in the verification container.");
            ForceFivePercentDecomposition(services, registry, containerItemId);
            Require(services.ItemIdentities.TryGetSnapshot(containerItemId, out ItemInstanceSnapshot containerSource)
                && containerSource.LifecycleState == ItemLifecycleState.Disassembled, "A container item did not decompose at the 5% cutoff.");
            DisassemblyOperationRecordData containerOperation = RecoveryOperation(services, containerItemId);
            foreach (string outputId in containerOperation.outcomes.SelectMany(outcome => outcome.outputItemInstanceIds ?? Array.Empty<string>()))
            {
                Require(services.ItemIdentities.TryGetSnapshot(outputId, out ItemInstanceSnapshot output)
                    && output.LocationKind == ItemLocationKind.Container
                    && string.Equals(output.Data.location.containerId, containerId, StringComparison.Ordinal), "A container decomposition output left its source container.");
            }

            string craftedShieldId = results.SelectMany(result => result.Operation.outputs)
                .First(output => output.itemDefinitionId == "item.prototype-shield").itemInstanceId;
            PrototypeItemDropResult droppedShield = services.DropPrototypeItemToWorld(craftedShieldId, workstation.transform.position + Vector3.right * 2f, Quaternion.identity);
            Require(droppedShield.Succeeded, $"Could not drop natural-decomposition test item: {droppedShield.Message}");
            Require(services.ItemDurability.TryGetDurabilityForItem(craftedShieldId, out ItemDurabilitySnapshot shieldDurability), "Dropped shield durability was unavailable.");
            ItemDurabilityRecordData forcedDecomposition = shieldDurability.Data.Clone();
            forcedDecomposition.currentDurability = forcedDecomposition.maximumDurability * services.ItemDurability.ForcedDecompositionNormalized;
            forcedDecomposition.lastBreakCheckPercent = 5;
            forcedDecomposition.breakCheckSequence += 6L;
            forcedDecomposition.hasBroken = false;
            forcedDecomposition.pendingForcedDecomposition = true;
            ItemDurabilityOperationResult ruinedShield = services.ItemDurability.SetDurabilityRecord(
                services.ItemIdentities, services.ItemCompositions, services.ItemQualityAffixes, registry, forcedDecomposition);
            Require(ruinedShield.Succeeded && ruinedShield.Snapshot.PendingForcedDecomposition, "Could not place the natural-decomposition test item at its forced 5% cutoff.");
            droppedShield.Pickup.GetComponent<WorldItemDecomposition>()?.EvaluateNow();
            Require(services.ItemIdentities.TryGetSnapshot(craftedShieldId, out ItemInstanceSnapshot naturalSource)
                && naturalSource.LifecycleState == ItemLifecycleState.Disassembled, "An item at 5% condition did not immediately decompose.");
            NaturalDecompositionRecordData naturalSchedule = services.ItemRecovery.NaturalDecompositions.Single(record => record.itemInstanceId == craftedShieldId);
            Require(naturalSchedule.state == NaturalDecompositionState.Completed, "Natural-decomposition schedule was not completed.");
            Require(services.ItemRecovery.TryGetOperation(naturalSchedule.operationId, out DisassemblyOperationRecordData naturalOperation)
                && naturalOperation.operationKind == ItemRecoveryOperationKind.NaturalDecomposition
                && naturalOperation.actorPersonId == "actor.nature", "Natural decomposition did not record Nature as the actor.");
            if (naturalOperation.outcomes.Sum(outcome => outcome.returnedQuantity) > 0)
                Require(UnityEngine.Object.FindObjectsByType<WorldItemPickup>(FindObjectsInactive.Include).Any(candidate => candidate.IsSalvagePickup), "Natural decomposition returned components but did not create Salvager-eligible pickups.");

            SalvagePickupCalculation masterSalvage = SalvagePickupScalingCalculator.Calculate(4, 7, true, "group6.master-salvager");
            Require(Mathf.Approximately(masterSalvage.BonusChance, 0.5f), "AAA Salvaging does not use the configured 50% double-yield chance.");
            DefinitionRegistry professionRegistry = PrototypeProfessionDefinitionFactory.AddMissingPrototypeProfessionDefinitions(registry);
            Require(professionRegistry.TryGet(PrototypeProfessionDefinitionFactory.DisassemblerProfessionId, out ProfessionDefinition _), "Disassembler profession is missing.");
            Require(professionRegistry.TryGet(PrototypeProfessionDefinitionFactory.SalvagerProfessionId, out ProfessionDefinition _), "Salvager profession is missing.");
        }

        private static string CraftedItemId(PrototypeCraftingResult[] results, string definitionId)
        {
            return results.SelectMany(result => result.Operation.outputs)
                .First(output => string.Equals(output.itemDefinitionId, definitionId, StringComparison.Ordinal)).itemInstanceId;
        }

        private static int FindInventorySlot(PlayerInventory inventory, string itemInstanceId)
        {
            for (int index = 0; index < inventory.Slots.Count; index++)
            {
                if (string.Equals(inventory.Slots[index]?.ItemInstanceId, itemInstanceId, StringComparison.Ordinal)) return index;
            }
            return -1;
        }

        private static void ForceFivePercentDecomposition(PrototypePersistenceServiceBehaviour services, DefinitionRegistry registry, string itemInstanceId, long? breakCheckSequence = null)
        {
            Require(services.ItemDurability.TryGetDurabilityForItem(itemInstanceId, out ItemDurabilitySnapshot durability), $"Durability was unavailable for '{itemInstanceId}'.");
            ItemDurabilityRecordData record = durability.Data.Clone();
            record.currentDurability = record.maximumDurability * services.ItemDurability.ForcedDecompositionNormalized;
            record.lastBreakCheckPercent = 5;
            record.breakCheckSequence = breakCheckSequence ?? record.breakCheckSequence + 6L;
            record.hasBroken = false;
            record.pendingForcedDecomposition = true;
            ItemDurabilityOperationResult result = services.ItemDurability.SetDurabilityRecord(
                services.ItemIdentities, services.ItemCompositions, services.ItemQualityAffixes, registry, record);
            Require(result.Succeeded, $"Could not set '{itemInstanceId}' to its forced-decomposition cutoff: {result.Message}");
        }

        private static long FindMultiOutputNatureSequence(PrototypePersistenceServiceBehaviour services, DefinitionRegistry registry, string itemInstanceId, string ownerPersonId)
        {
            Require(services.ItemDurability.TryGetDurabilityForItem(itemInstanceId, out ItemDurabilitySnapshot current), "Capacity-check durability was unavailable.");
            ItemDurabilityRuntime previewDurability = new ItemDurabilityRuntime();
            Require(previewDurability.RestoreFromSaveData(services.ItemDurability.CreateSaveData(), registry, services.ItemIdentities, services.ItemCompositions).Succeeded,
                "Could not clone durability state for the capacity check.");
            ItemDurabilityRecordData fivePercent = current.Data.Clone();
            fivePercent.currentDurability = fivePercent.maximumDurability * previewDurability.ForcedDecompositionNormalized;
            fivePercent.lastBreakCheckPercent = 5;
            fivePercent.hasBroken = false;
            fivePercent.pendingForcedDecomposition = true;
            Require(previewDurability.SetDurabilityRecord(services.ItemIdentities, services.ItemCompositions, services.ItemQualityAffixes, registry, fivePercent).Succeeded,
                "Could not prepare 5% durability for the capacity check.");
            for (long sequence = 1L; sequence <= 10000L; sequence++)
            {
                string operationId = $"natural-forced.{itemInstanceId}.{sequence}";
                DisassemblyResult preview = services.ItemRecovery.Preview(new DisassemblyRequest
                {
                    operationId = operationId,
                    itemInstanceId = itemInstanceId,
                    actorPersonId = "actor.nature",
                    ownerPersonId = ownerPersonId,
                    deterministicSeed = operationId,
                    operationKind = ItemRecoveryOperationKind.NaturalDecomposition,
                    skillUsed = false,
                    materializeOutputIdentities = true
                }, registry, services.ItemIdentities, services.ItemCompositions, services.ItemQualityAffixes, previewDurability);
                if (preview.Succeeded && preview.Operation.outcomes.Count(outcome => outcome.returnedQuantity > 0) >= 2)
                {
                    return sequence;
                }
            }

            throw new InvalidOperationException("Could not find a deterministic multi-output decomposition result for full-inventory verification.");
        }

        private static List<string> FillInventoryWithStatefulItems(PlayerInventory inventory, ItemDefinition definition)
        {
            List<string> fillerIds = new List<string>();
            while (inventory.Slots.Any(slot => slot == null || slot.IsEmpty))
            {
                string fillerId = Guid.NewGuid().ToString("D");
                InventoryInstanceOperationResult added = inventory.AddExistingItemIdentity(definition, fillerId);
                Require(added.Succeeded, $"Could not fill inventory for capacity verification: {added.Message}");
                fillerIds.Add(fillerId);
            }
            return fillerIds;
        }

        private static DisassemblyOperationRecordData RecoveryOperation(PrototypePersistenceServiceBehaviour services, string itemInstanceId)
        {
            DisassemblyOperationRecordData operation = services.ItemRecovery.Operations
                .LastOrDefault(candidate => string.Equals(candidate.itemInstanceId, itemInstanceId, StringComparison.Ordinal)
                    && candidate.operationKind == ItemRecoveryOperationKind.NaturalDecomposition);
            Require(operation != null, $"No natural-decomposition operation was recorded for '{itemInstanceId}'.");
            return operation;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
