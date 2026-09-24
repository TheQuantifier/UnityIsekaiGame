using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Input;
using UnityIsekaiGame.Interaction;
using UnityIsekaiGame.Inventory.Crafting;
using UnityIsekaiGame.Inventory.Disassembly;
using UnityIsekaiGame.Inventory.Recipes;
using UnityIsekaiGame.WorldLocations;
using UnityIsekaiGame.WorldLocations.SceneBinding;

namespace UnityIsekaiGame.Gameplay
{
    public sealed class PrototypeCraftingWorkstation : MonoBehaviour, IInteractionPointDestinationHandler
    {
        [SerializeField] private PrototypePersistenceServiceBehaviour services;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private string title = "Prototype Workstation";
        [SerializeField] private Vector2 windowSize = new Vector2(720f, 680f);

        private bool open;
        private Vector2 scroll;
        private string status = string.Empty;
        private CursorLockMode priorLockMode;
        private bool priorCursorVisible;
        private PrototypePersistenceServiceBehaviour subscribedServices;
        private string selectedRecipeId = string.Empty;
        private SlotCraftingRequest configuredRequest;
        private readonly List<PrototypeCraftingCatalystChoice> catalystChoices = new List<PrototypeCraftingCatalystChoice>();
        private string selectedCatalystInstanceId = string.Empty;
        private int catalystQuantity = 1;
        private bool showItemRecovery;

        public string InteractionPrompt => open ? "Use Workstation (already open)" : "Use Workstation";

        public bool CanHandleInteraction(in InteractionContext context, InteractionPointSnapshot point)
        {
            ResolveServices();
            return services != null;
        }

        public void HandleInteraction(in InteractionContext context, InteractionPointSnapshot point)
        {
            ResolveServices();
            if (services == null)
            {
                PrototypeHudMessageBus.Show("Crafting services are unavailable.");
                return;
            }

            if (!open)
            {
                priorLockMode = Cursor.lockState;
                priorCursorVisible = Cursor.visible;
            }

            open = true;
            input?.SetGameplayInputBlocked(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            status = "Choose an item type, then place resources into its component slots.";
            showItemRecovery = false;
            selectedRecipeId = string.Empty;
            configuredRequest = null;
            RefreshCatalystChoices();
        }

        private void OnDisable()
        {
            SubscribeToServices(null);
            Close();
        }

        private void OnGUI()
        {
            if (!open)
            {
                return;
            }

            float width = Mathf.Min(windowSize.x, Screen.width - 30f);
            float height = Mathf.Min(windowSize.y, Screen.height - 30f);
            Rect window = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUI.Box(window, GUIContent.none);
            GUILayout.BeginArea(new Rect(window.x + 16f, window.y + 14f, window.width - 32f, window.height - 28f));
            GUILayout.BeginHorizontal();
            GUILayout.Label(title, new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold });
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", GUILayout.Width(90f), GUILayout.Height(30f)))
            {
                Close();
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
                return;
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Craft", GUILayout.Height(30f))) { showItemRecovery = false; status = "Choose an item type, then place resources into its component slots."; }
            if (GUILayout.Button("Disassemble", GUILayout.Height(30f))) { showItemRecovery = true; selectedRecipeId = string.Empty; configuredRequest = null; status = "Choose a crafted item to recover its recorded component materials."; }
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
            GUILayout.Label(status, new GUIStyle(GUI.skin.label) { wordWrap = true });
            GUILayout.Space(8f);

            PrototypeActiveCraftSnapshot activeCraft = services.ActivePrototypeCraft;
            if (activeCraft != null)
            {
                DrawActiveCraft(activeCraft);
            }

            scroll = GUILayout.BeginScrollView(scroll);
            if (showItemRecovery)
            {
                DrawItemRecovery();
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }
            RecipeDefinition[] recipes = services.AvailableCraftingRecipes.ToArray();
            if (recipes.Length == 0)
            {
                GUILayout.Label("No authored crafting recipes are available.");
            }
            else
            {
                RecipeDefinition selectedRecipe = recipes.FirstOrDefault(recipe => string.Equals(recipe.Id, selectedRecipeId, StringComparison.Ordinal));
                if (selectedRecipe == null)
                {
                    DrawCraftableSelection(recipes, activeCraft != null);
                }
                else
                {
                    DrawConfiguredCraft(selectedRecipe, activeCraft != null);
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawItemRecovery()
        {
            GUILayout.Label("Disassembly", new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold });
            GUILayout.Label("Break crafted inventory items into their recorded components. Damage limits what physically remains; Salvaging is a separate field role used when gathering fallen resource pickups.", new GUIStyle(GUI.skin.label) { wordWrap = true });
            PrototypeItemRecoveryChoice[] choices = services.GetPrototypeItemRecoveryChoices().ToArray();
            if (choices.Length == 0) { GUILayout.Label("No crafted inventory items have recoverable composition records."); return; }
            foreach (PrototypeItemRecoveryChoice choice in choices)
            {
                DisassemblyOperationRecordData preview = choice.Preview;
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(choice.DisplayName, new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold });
                GUILayout.Label($"{choice.OperationLabel} | Item level {preview.itemLevel} | Skill: {(preview.skillUsed ? $"{preview.skillId} {(UnityIsekaiGame.Skills.SkillGrade)preview.skillGrade}" : "Unskilled")}");
                GUILayout.Label($"Expected efficiency: {preview.expectedEfficiency:P0} | Rolled tier: {preview.efficiencyTier}");
                foreach (DisassemblyComponentOutcomeData outcome in preview.outcomes)
                {
                    string component = string.IsNullOrWhiteSpace(outcome.componentEntryId) ? "Component" : ComponentLabel(new RecipeInputSpecificationData { componentRoleId = outcome.componentEntryId });
                    GUILayout.Label($"{component}: {outcome.returnedQuantity}/{outcome.sourceQuantity:0.##} returned | {outcome.recoveryChance:P0} chance | {outcome.componentCondition:P0} condition | component rarity {outcome.componentRarityRank}");
                }
                if (GUILayout.Button($"{choice.OperationLabel} {choice.DisplayName}", GUILayout.Height(30f)))
                {
                    DisassemblyResult result = services.RecoverPrototypeItem(choice.ItemInstanceId);
                    status = result.Message;
                    PrototypeHudMessageBus.Show(result.Message);
                }
                GUILayout.EndVertical();
                GUILayout.Space(6f);
            }
        }

        private void DrawActiveCraft(PrototypeActiveCraftSnapshot activeCraft)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"Crafting: {activeCraft.RecipeDisplayName}", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
            Rect progressRect = GUILayoutUtility.GetRect(1f, 22f, GUILayout.ExpandWidth(true));
            GUI.Box(progressRect, GUIContent.none);
            Rect fill = progressRect;
            fill.width *= activeCraft.ProgressNormalized;
            GUI.Box(fill, GUIContent.none);
            GUI.Label(progressRect, $"{activeCraft.ProgressNormalized:P0} - {activeCraft.RemainingSeconds:0.0}s remaining", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
            GUILayout.Label($"Skill: {activeCraft.Scaling.SkillLabel} | Expected quality: {activeCraft.Scaling.ExpectedQuality:P0} | Expected stat scale: {activeCraft.Scaling.ExpectedEquipmentStatMultiplier:P0}");
            CraftingSlotEntryData activeCatalyst = activeCraft.SlotRequest?.slots?.FirstOrDefault(slot => slot.kind == CraftingSlotKind.OptionalCatalyst);
            if (activeCatalyst != null)
            {
                GUILayout.Label($"Catalyst: {activeCatalyst.quantity} x {DisplayItemName(activeCatalyst.itemDefinitionId)}");
            }

            if (GUILayout.Button("Cancel Craft", GUILayout.Height(28f)))
            {
                services.CancelActivePrototypeCraft();
                status = "Craft cancelled. No materials were consumed.";
            }

            GUILayout.EndVertical();
            GUILayout.Space(8f);
        }

        private void DrawCraftableSelection(IEnumerable<RecipeDefinition> recipes, bool craftActive)
        {
            GUILayout.Space(6f);
            GUILayout.Label("Choose What To Craft", new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold });
            GUILayout.Label("Each choice is an authored craftable tag. Selecting one opens its component layout.", new GUIStyle(GUI.skin.label) { wordWrap = true });
            GUILayout.Space(8f);
            foreach (RecipeDefinition recipe in recipes.OrderBy(recipe => CraftableLabel(recipe), StringComparer.Ordinal))
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(CraftableLabel(recipe), new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold });
                GUILayout.Label(recipe.DisplayName);
                GUILayout.Label($"Requires: {services.DescribeRecipeRequirements(recipe)}");
                CraftingScalingCalculation scaling = services.GetCraftingScaling(recipe.Id);
                if (scaling != null) GUILayout.Label($"Time: {scaling.FinalDurationSeconds:0.0}s | {scaling.SkillLabel} | Quality: {scaling.ExpectedQuality:P0} | Stats: {scaling.ExpectedEquipmentStatMultiplier:P0}");
                GUI.enabled = !craftActive;
                if (GUILayout.Button($"Craft {CraftableLabel(recipe)}", GUILayout.Height(30f))) SelectRecipe(recipe.Id);
                GUI.enabled = true;
                GUILayout.EndVertical();
                GUILayout.Space(6f);
            }
        }

        private void DrawConfiguredCraft(RecipeDefinition recipe, bool craftActive)
        {
            GUI.enabled = !craftActive;
            if (GUILayout.Button("← Back to Item Types", GUILayout.Width(170f), GUILayout.Height(28f)))
            {
                selectedRecipeId = string.Empty;
                configuredRequest = null;
                status = "Choose an item type to craft.";
                GUI.enabled = true;
                return;
            }
            GUI.enabled = true;

            GUILayout.Space(8f);
            GUILayout.Label(CraftableLabel(recipe), new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter });
            GUILayout.Label("Place one compatible resource type into each item-part slot.", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
            GUILayout.Space(8f);

            RecipeInputSpecificationData[] componentInputs = recipe.Inputs
                .Where(entry => entry.requirementState == RecipeRequirementState.Required)
                .OrderBy(entry => entry.inputId, StringComparer.Ordinal)
                .ToArray();
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(225f));
            for (int index = 0; index < componentInputs.Length; index += 2) DrawComponentSlot(recipe, componentInputs[index], craftActive);
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(190f), GUILayout.MinHeight(230f));
            GUILayout.FlexibleSpace();
            GUILayout.Label($"[ {CraftableLabel(recipe).ToUpperInvariant()} ]", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18, fontStyle = FontStyle.Bold }, GUILayout.Height(55f));
            GUILayout.Label("Item Outline", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
            GUILayout.Label($"{componentInputs.Length} components", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUILayout.Width(225f));
            for (int index = 1; index < componentInputs.Length; index += 2) DrawComponentSlot(recipe, componentInputs[index], craftActive);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUI.enabled = !craftActive;
            if (GUILayout.Button("Auto-fill All Component Slots", GUILayout.Height(28f)))
            {
                configuredRequest = services.BuildAutoFilledSlotCraftingRequest(recipe.Id);
                status = "Component slots auto-filled from compatible resources in inventory.";
                RefreshCatalystChoices();
            }
            GUI.enabled = true;

            GUILayout.Space(8f);
            GUILayout.Label("Optional Infusion / Catalyst Slot", new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold });
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("The catalyst is consumed even when its effect roll fails. More copies increase both chance and potential affix tier.", new GUIStyle(GUI.skin.label) { wordWrap = true });
            if (GUILayout.Button("No Catalyst", GUILayout.Height(24f)))
            {
                selectedCatalystInstanceId = string.Empty;
                catalystQuantity = 1;
            }

            foreach (PrototypeCraftingCatalystChoice choice in catalystChoices)
            {
                int remaining = RemainingCatalystQuantity(choice);
                if (remaining <= 0) continue;
                bool selected = string.Equals(selectedCatalystInstanceId, choice.ItemInstanceId, StringComparison.Ordinal);
                Color priorBackground = GUI.backgroundColor;
                if (selected) GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
                if (GUILayout.Button($"{choice.DisplayName} [{choice.RarityName}] x{remaining}{(selected ? " (Selected)" : string.Empty)}", GUILayout.Height(25f)))
                {
                    selectedCatalystInstanceId = choice.ItemInstanceId;
                    catalystQuantity = Mathf.Clamp(catalystQuantity, 1, remaining);
                }
                GUI.backgroundColor = priorBackground;
            }

            PrototypeCraftingCatalystChoice selectedCatalyst = catalystChoices.FirstOrDefault(choice => string.Equals(choice.ItemInstanceId, selectedCatalystInstanceId, StringComparison.Ordinal));
            if (selectedCatalyst != null)
            {
                int maximum = Math.Max(1, RemainingCatalystQuantity(selectedCatalyst));
                catalystQuantity = Mathf.Clamp(catalystQuantity, 1, maximum);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Quantity", GUILayout.Width(70f));
                if (GUILayout.Button("-", GUILayout.Width(32f))) catalystQuantity = Math.Max(1, catalystQuantity - 1);
                GUILayout.Label(catalystQuantity.ToString(), new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter }, GUILayout.Width(45f));
                if (GUILayout.Button("+", GUILayout.Width(32f))) catalystQuantity = Math.Min(maximum, catalystQuantity + 1);
                GUILayout.Label($"Available for catalyst: {maximum}");
                GUILayout.EndHorizontal();
                GUILayout.Label(services.DescribePrototypeCraftingCatalyst(recipe.Id, selectedCatalyst.ItemDefinitionId, catalystQuantity), new GUIStyle(GUI.skin.label) { wordWrap = true });
            }
            GUILayout.EndVertical();

            GUILayout.Space(8f);
            GUI.enabled = !craftActive;
            if (GUILayout.Button($"Start {recipe.DisplayName}", GUILayout.Height(36f)))
            {
                SlotCraftingRequest request = configuredRequest?.Clone() ?? services.BuildAutoFilledSlotCraftingRequest(recipe.Id);
                if (request != null && selectedCatalyst != null)
                {
                    request.slots.Add(new CraftingSlotEntryData
                    {
                        slotId = "catalyst.optional.1",
                        kind = CraftingSlotKind.OptionalCatalyst,
                        itemDefinitionId = selectedCatalyst.ItemDefinitionId,
                        itemInstanceId = selectedCatalyst.ItemInstanceId,
                        quantity = catalystQuantity
                    });
                }

                PrototypeCraftingStartResult result = services.BeginSlotCraftAtPrototypeWorkstation(request);
                status = result.Message;
                PrototypeHudMessageBus.Show(result.Message);
            }
            GUI.enabled = true;
        }

        private void SelectRecipe(string recipeId)
        {
            selectedRecipeId = recipeId ?? string.Empty;
            configuredRequest = new SlotCraftingRequest { recipeId = selectedRecipeId };
            selectedCatalystInstanceId = string.Empty;
            catalystQuantity = 1;
            RefreshCatalystChoices();
            status = "Item selected. Fill each component slot or use auto-fill.";
        }

        private void DrawComponentSlot(RecipeDefinition recipe, RecipeInputSpecificationData inputSpecification, bool craftActive)
        {
            int assigned = configuredRequest?.slots?.Where(slot => slot.kind == CraftingSlotKind.RequiredInput && string.Equals(slot.recipeInputId, inputSpecification.inputId, StringComparison.Ordinal)).Sum(slot => slot.quantity) ?? 0;
            int required = Mathf.RoundToInt(inputSpecification.quantity);
            CraftingSlotEntryData current = configuredRequest?.slots?.FirstOrDefault(slot => slot.kind == CraftingSlotKind.RequiredInput && string.Equals(slot.recipeInputId, inputSpecification.inputId, StringComparison.Ordinal));
            string componentName = ComponentLabel(inputSpecification);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(componentName, new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
            GUILayout.Label($"{services.DescribeRecipeInput(inputSpecification)} × {required}");
            GUILayout.Label(assigned == required
                ? $"Placed: {DisplayItemName(current?.itemDefinitionId)}"
                : $"Empty / missing {Math.Max(0, required - assigned)}");
            foreach (PrototypeCraftingMaterialChoice choice in services.GetPrototypeCraftingMaterialChoices(recipe.Id, inputSpecification.inputId))
            {
                bool selected = string.Equals(current?.itemDefinitionId, choice.ItemDefinitionId, StringComparison.Ordinal) && assigned == required;
                Color prior = GUI.backgroundColor;
                if (selected) GUI.backgroundColor = new Color(0.62f, 0.9f, 0.68f);
                GUI.enabled = !craftActive && choice.AvailableQuantity >= required;
                if (GUILayout.Button($"{choice.DisplayName} ({choice.AvailableQuantity}){(selected ? " ✓" : string.Empty)}", GUILayout.Height(24f)))
                {
                    configuredRequest = services.AssignPrototypeCraftingMaterial(configuredRequest, inputSpecification.inputId, choice.ItemDefinitionId);
                    status = $"Placed {choice.DisplayName} into {componentName}.";
                    RefreshCatalystChoices();
                }
                GUI.enabled = true;
                GUI.backgroundColor = prior;
            }
            GUILayout.EndVertical();
        }

        private static string ComponentLabel(RecipeInputSpecificationData input)
        {
            string id = string.IsNullOrWhiteSpace(input?.componentRoleId) ? input?.inputId : input.componentRoleId;
            string suffix = (id ?? "component").Split('.').LastOrDefault() ?? "component";
            return string.Join(" ", suffix.Split('-', '_').Where(part => part.Length > 0).Select(part => char.ToUpperInvariant(part[0]) + part.Substring(1)));
        }

        private static string CraftableLabel(RecipeDefinition recipe)
        {
            string tag = recipe?.Tags.FirstOrDefault(value => value.StartsWith("craftable.", StringComparison.Ordinal));
            string suffix = string.IsNullOrWhiteSpace(tag) ? recipe?.DisplayName ?? "Item" : tag.Substring("craftable.".Length);
            return string.Join(" ", suffix.Split('-', '_').Where(part => part.Length > 0).Select(part => char.ToUpperInvariant(part[0]) + part.Substring(1)));
        }

        private void RefreshCatalystChoices()
        {
            catalystChoices.Clear();
            if (services != null) catalystChoices.AddRange(services.GetPrototypeCraftingCatalystChoices());
            if (!catalystChoices.Any(choice => string.Equals(choice.ItemInstanceId, selectedCatalystInstanceId, StringComparison.Ordinal)))
            {
                selectedCatalystInstanceId = string.Empty;
                catalystQuantity = 1;
            }
        }

        private int RemainingCatalystQuantity(PrototypeCraftingCatalystChoice choice)
        {
            int usedByRequiredSlots = configuredRequest?.slots?.Where(slot => slot.kind == CraftingSlotKind.RequiredInput && string.Equals(slot.itemInstanceId, choice.ItemInstanceId, StringComparison.Ordinal)).Sum(slot => slot.quantity) ?? 0;
            return Math.Max(0, choice.AvailableQuantity - usedByRequiredSlots);
        }

        private string DisplayItemName(string definitionId)
        {
            return catalystChoices.FirstOrDefault(choice => string.Equals(choice.ItemDefinitionId, definitionId, StringComparison.Ordinal))?.DisplayName ?? definitionId;
        }

        private static string ShortId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "unassigned" : value.Substring(0, Math.Min(8, value.Length));
        }

        private void ResolveServices()
        {
            if (services == null) services = FindAnyObjectByType<PrototypePersistenceServiceBehaviour>(FindObjectsInactive.Include);
            SubscribeToServices(services);
            if (input == null) input = FindAnyObjectByType<PlayerInputReader>(FindObjectsInactive.Include);
        }

        private void SubscribeToServices(PrototypePersistenceServiceBehaviour target)
        {
            if (ReferenceEquals(subscribedServices, target)) return;
            if (subscribedServices != null) subscribedServices.PrototypeCraftCompleted -= OnCraftCompleted;
            subscribedServices = target;
            if (subscribedServices != null) subscribedServices.PrototypeCraftCompleted += OnCraftCompleted;
        }

        private void OnCraftCompleted(PrototypeCraftingResult result)
        {
            status = result?.Message ?? "Crafting finished without a result.";
            PrototypeHudMessageBus.Show(status);
            if (!string.IsNullOrWhiteSpace(selectedRecipeId)) configuredRequest = services.BuildAutoFilledSlotCraftingRequest(selectedRecipeId);
            RefreshCatalystChoices();
        }

        private void Close()
        {
            if (!open) return;
            open = false;
            input?.SetGameplayInputBlocked(false);
            Cursor.lockState = priorLockMode;
            Cursor.visible = priorCursorVisible;
        }
    }
}
