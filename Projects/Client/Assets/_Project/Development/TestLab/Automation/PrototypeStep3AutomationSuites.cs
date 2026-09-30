#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.People;
using UnityIsekaiGame.Places;
using UnityIsekaiGame.Quests;
using UnityIsekaiGame.StatusEffects;

namespace UnityIsekaiGame.Development.Automation
{
    [PrototypeTestLabAutomationProvider(3, "Runtime Taxonomy", 300)]
    public static class PrototypeStep3AutomationSuites
    {
        public static void RegisterDefaults(TestLabAutomationRegistry registry)
        {
            if (registry == null)
            {
                return;
            }

            TryRegister(registry, BuildRuntimeTaxonomySuite());
        }

        private static ITestLabAutomationSuite BuildRuntimeTaxonomySuite()
        {
            return Suite("feature.3.runtime-taxonomy", "Step 3 Runtime Taxonomy", "3.x", 300,
                Required("PrototypeTestLabService", "Inventory", "Narrative Coordinator"),
                Scenario("item-instance-equipment-flow", "Item instances and equipment flow", 10,
                    Step("clear-inventory", "Clear inventory", context => Operation(context.Prototype().ClearInventory(true), context, "step3-clear-inventory")),
                    Step("grant-stack", "Grant stackable item", context => Operation(context.Prototype().GrantItem(FirstStackableItem(context), 2), context, "step3-grant-stack")),
                    Step("grant-instance", "Grant stateful item", context => Operation(context.Prototype().GrantStatefulItem(FirstStatefulItem(context)), context, "step3-grant-instance")),
                    Step("grant-equippable", "Grant equippable item", context => Operation(context.Prototype().GrantStatefulItem(FirstEquippableItem(context)), context, "step3-grant-equippable")),
                    Step("equip", "Equip compatible item", context => Operation(context.Prototype().EquipFirstCompatible(FirstEquippableItem(context)), context, "step3-equip")),
                    Step("unequip", "Unequip all", context => Operation(context.Prototype().UnequipAll(true), context, "step3-unequip"))),
                Scenario("status-damage-and-vitals-flow", "Status, damage type, and vitals flow", 20,
                    Step("restore", "Restore vitals", context => Operation(context.Prototype().RestoreVitals(), context, "step3-restore-vitals")),
                    Step("status", "Apply status", context => Operation(context.Prototype().ApplyStatus(First<StatusEffectDefinition>(context), toEnemy: false), context, "step3-apply-status")),
                    Step("damage", "Apply typed damage", context => Operation(context.Prototype().ApplyTypedDamage(First<DamageTypeDefinition>(context), 5f, targetEnemy: false, sourcePlayer: false), context, "step3-typed-damage")),
                    Step("remove-status", "Remove status", context => Operation(context.Prototype().RemoveStatus(First<StatusEffectDefinition>(context), fromEnemy: false), context, "step3-remove-status"))),
                Scenario("quest-objective-signals", "Quest objective signals", 30,
                    Step("start-quest", "Start quest", context => Operation(context.Prototype().StartQuest(First<QuestDefinition>(context)), context, "step3-start-quest")),
                    Step("report-talk", "Report talk", context => Operation(context.Prototype().ReportTalk(First<PersonDefinition>(context)), context, "step3-report-talk")),
                    Step("report-reach", "Report reach", context => Operation(context.Prototype().ReportReach(First<PlaceDefinition>(context)), context, "step3-report-reach")),
                    Step("report-defeat", "Report defeat", context => Operation(context.Prototype().ReportDefeat("prototype_enemy"), context, "step3-report-defeat"))),
                Scenario("ranged-weapon-ammo-flow", "Bow, arrow, and ranged weapon flow", 35,
                    Step("validate-ranged-definitions", "Validate ranged item definitions", ValidatePrototypeRangedDefinitions),
                    Step("grant-arrows", "Grant ammunition", context => Operation(context.Prototype().GrantItem(RequiredItem(context, "item.prototype-arrow"), 2), context, "step3-grant-arrows")),
                    Step("grant-bow", "Grant bow", context => Operation(context.Prototype().GrantStatefulItem(RequiredItem(context, "item.prototype-bow")), context, "step3-grant-bow")),
                    Step("equip-bow", "Equip bow", context => Operation(context.Prototype().EquipFirstCompatible(RequiredItem(context, "item.prototype-bow")), context, "step3-equip-bow"))),
                Scenario("location-and-world-entity-diagnostics", "Location and world entity diagnostics", 40,
                    Step("location", "Validate current location", context => Operation(context.Prototype().ValidateCurrentLocation(), context, "step3-location")),
                    Step("world-entities", "Refresh world entity diagnostics", context => Operation(context.Prototype().RefreshWorldEntityDiagnostics(), context, "step3-world-entities"))));
        }

        private static ITestLabAutomationSuite Suite(string suiteId, string displayName, string feature, int order, IReadOnlyList<string> required, params ITestLabAutomationScenario[] scenarios)
        {
            return new TestLabAutomationSuite(
                suiteId,
                displayName,
                feature,
                $"{displayName} runtime integration scenarios.",
                order,
                TestLabAutomationCategory.Standard,
                includeInRunAll: true,
                requiredServices: required,
                scenarios: scenarios);
        }

        private static ITestLabAutomationScenario Scenario(string scenarioId, string displayName, int order, params ITestLabScenarioStep[] steps)
        {
            return new TestLabAutomationScenario(
                scenarioId,
                displayName,
                displayName,
                order,
                order <= 30 ? TestLabAutomationCategory.Quick : TestLabAutomationCategory.Standard,
                includeInQuickRun: order <= 30,
                steps: steps,
                isolationMode: TestLabScenarioIsolationMode.SharedRuntime,
                requiredRuntimeAreas: TestLabRuntimeArea.Character | TestLabRuntimeArea.Combat | TestLabRuntimeArea.Persistence,
                requiredHostId: PrototypeTestLabAutomationHost.DefaultHostId,
                requiredHostFeatures: TestLabHostFeature.SharedRuntime | TestLabHostFeature.SceneReset | TestLabHostFeature.FixtureFingerprinting | TestLabHostFeature.Persistence | TestLabHostFeature.AutomatedExecution);
        }

        private static ITestLabScenarioStep Step(string stepId, string displayName, Func<TestLabAutomationContext, TestLabAutomationStepResult> action)
        {
            return new TestLabScenarioStep(stepId, displayName, action);
        }

        private static IReadOnlyList<string> Required(params string[] services)
        {
            return services.ToArray();
        }

        private static T First<T>(TestLabAutomationContext context)
            where T : class, IGameDefinition
        {
            return context.Prototype().GetDefinitions<T>().FirstOrDefault();
        }

        private static ItemDefinition FirstStackableItem(TestLabAutomationContext context)
        {
            return context.Prototype().GetDefinitions<ItemDefinition>().FirstOrDefault(item => item != null && item.Stackable)
                ?? context.Prototype().GetDefinitions<ItemDefinition>().FirstOrDefault();
        }

        private static ItemDefinition FirstStatefulItem(TestLabAutomationContext context)
        {
            return context.Prototype().GetDefinitions<ItemDefinition>().FirstOrDefault(item => item != null && item.InstanceMode != ItemInstanceMode.DefinitionOnly)
                ?? context.Prototype().GetDefinitions<ItemDefinition>().FirstOrDefault();
        }

        private static ItemDefinition FirstEquippableItem(TestLabAutomationContext context)
        {
            return context.Prototype().GetDefinitions<ItemDefinition>().FirstOrDefault(item => item != null && item.IsEquippable)
                ?? context.Prototype().GetDefinitions<ItemDefinition>().FirstOrDefault();
        }

        private static ItemDefinition RequiredItem(TestLabAutomationContext context, string id)
        {
            return context.Prototype().GetDefinitions<ItemDefinition>().FirstOrDefault(item => item != null && string.Equals(item.Id, id, StringComparison.Ordinal));
        }

        private static TestLabAutomationStepResult ValidatePrototypeRangedDefinitions(TestLabAutomationContext context)
        {
            ItemDefinition bow = RequiredItem(context, "item.prototype-bow");
            ItemDefinition arrow = RequiredItem(context, "item.prototype-arrow");
            bool valid = bow != null
                && arrow != null
                && bow.IsEquippable
                && bow.Equipment?.RangedWeapon != null
                && bow.Equipment.RangedWeapon.AmmoItem == arrow
                && bow.Equipment.RangedWeapon.DamageType != null
                && !arrow.IsEquippable;
            string diagnostics = $"Bow={(bow == null ? "missing" : bow.Id)} Equippable={bow?.IsEquippable.ToString() ?? "False"} Ranged={bow?.Equipment?.RangedWeapon != null} Ammo={bow?.Equipment?.RangedWeapon?.AmmoItem?.Id ?? "missing"} Damage={bow?.Equipment?.RangedWeapon?.DamageType?.Id ?? "missing"} ArrowEquippable={arrow?.IsEquippable.ToString() ?? "False"}.";
            return TestLabAssertions.True("step3-ranged-definitions", "Validate ranged item definitions", valid, diagnostics);
        }

        private static TestLabAutomationStepResult Operation(PrototypeTestLabOperation operation, TestLabAutomationContext context, string operationId, bool acceptFailure = false)
        {
            string transactionId = context.TransactionIds.Create(context.CurrentSuiteId, context.CurrentScenarioId, context.RunId, context.CurrentStepIndex, operationId);
            if (acceptFailure)
            {
                return operation.Succeeded
                    ? TestLabAssertions.Fail(operationId, operation.OperationName, "OperationFailed", "Failure", operation.Code, operation.Message, string.Empty, transactionId)
                    : TestLabAssertions.Pass(operationId, operation.OperationName, $"Expected rejection observed: {operation.Code} {operation.Message}");
            }

            return operation.Succeeded
                ? new TestLabAutomationStepResult(operationId, operation.OperationName, TestLabAutomationStatus.Passed, "OperationSucceeded", "Succeeded", operation.Code, string.Empty, transactionId, operation.Message)
                : new TestLabAutomationStepResult(operationId, operation.OperationName, TestLabAutomationStatus.Failed, "OperationSucceeded", "Succeeded", operation.Code, string.Empty, transactionId, operation.Message);
        }

        private static void TryRegister(TestLabAutomationRegistry registry, ITestLabAutomationSuite suite)
        {
            registry.TryRegister(suite, out _);
        }
    }
}
#endif
