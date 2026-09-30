#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Places;
using UnityIsekaiGame.Quests;
using UnityIsekaiGame.StatusEffects;

namespace UnityIsekaiGame.Development.Automation
{
    [PrototypeTestLabAutomationProvider(4, "World Data", 400)]
    public static class PrototypeStep4AutomationSuites
    {
        public static void RegisterDefaults(TestLabAutomationRegistry registry)
        {
            if (registry == null)
            {
                return;
            }

            TryRegister(registry, BuildSaveFileFoundationSuite());
            TryRegister(registry, BuildInventoryEquipmentPersistenceSuite());
            TryRegister(registry, BuildVitalsStatusPersistenceSuite());
            TryRegister(registry, BuildQuestPersistenceSuite());
            TryRegister(registry, BuildLocationPersistenceSuite());
            TryRegister(registry, BuildWorldEntityIdentitySuite());
            TryRegister(registry, BuildSaveSlotsRecoverySuite());
            TryRegister(registry, BuildFailureHardeningSuite());
        }

        private static ITestLabAutomationSuite BuildSaveFileFoundationSuite()
        {
            return Suite("feature.4.1.save-file-foundation", "Feature 4.1 Save File Foundation", "4.1", 410,
                Required("PrototypePersistenceServiceBehaviour", "PersistenceService"),
                Scenario("base-save-load-validate", "Base save, validate, load, and fingerprint", 10,
                    Step("safe-location", "Move to known test point", context => Operation(context.Prototype().Teleport(FirstTestPoint(context)), context, "step4-safe-location")),
                    Step("save", "Save prototype slot", context => Operation(context.Prototype().Save(), context, "step4-save")),
                    Step("validate", "Validate prototype slot", context => Operation(context.Prototype().ValidateSave(), context, "step4-validate")),
                    Step("load", "Load prototype slot", context => Operation(context.Prototype().Load(), context, "step4-load")),
                    Step("fingerprint", "Record runtime fingerprint", context => Operation(context.Prototype().RecordFingerprint(), context, "step4-fingerprint"))));
        }

        private static ITestLabAutomationSuite BuildInventoryEquipmentPersistenceSuite()
        {
            return Suite("feature.4.2.inventory-equipment-persistence", "Feature 4.2 Inventory and Equipment Persistence", "4.2", 420,
                Required("Inventory", "Equipment", "PersistenceService"),
                Scenario("inventory-equipment-roundtrip", "Inventory and equipment save/load round trip", 10,
                    Step("clear", "Clear inventory", context => Operation(context.Prototype().ClearInventory(true), context, "step4-inventory-clear")),
                    Step("unequip", "Unequip all", context => Operation(context.Prototype().UnequipAll(true), context, "step4-unequip")),
                    Step("grant", "Grant equippable item", context => Operation(context.Prototype().GrantStatefulItem(FirstEquippableItem(context)), context, "step4-grant-equippable")),
                    Step("equip", "Equip item", context => Operation(context.Prototype().EquipFirstCompatible(FirstEquippableItem(context)), context, "step4-equip")),
                    Step("save", "Save equipped state", context => Operation(context.Prototype().Save(), context, "step4-save-equipped")),
                    Step("load", "Load equipped state", context => Operation(context.Prototype().Load(), context, "step4-load-equipped"))));
        }

        private static ITestLabAutomationSuite BuildVitalsStatusPersistenceSuite()
        {
            return Suite("feature.4.3.vitals-status-persistence", "Feature 4.3 Vitals and Status Persistence", "4.3", 430,
                Required("Current Resources", "StatusEffectController", "PersistenceService"),
                Scenario("vitals-status-resources-roundtrip", "Vitals, resources, and statuses save/load round trip", 10,
                    Step("restore", "Restore vitals", context => Operation(context.Prototype().RestoreVitals(), context, "step4-restore-vitals")),
                    Step("damage", "Damage player", context => Operation(context.Prototype().DamagePlayer(10), context, "step4-damage-player")),
                    Step("mana", "Drain mana", context => Operation(context.Prototype().DrainMana(5f), context, "step4-drain-mana")),
                    Step("stamina", "Drain stamina", context => Operation(context.Prototype().DrainStamina(5f), context, "step4-drain-stamina")),
                    Step("status", "Apply status", context => Operation(context.Prototype().ApplyStatus(First<StatusEffectDefinition>(context), toEnemy: false), context, "step4-apply-status")),
                    Step("snapshot", "Snapshot resources", context => Operation(context.Prototype().SnapshotResourcesForPersistence(), context, "step4-resource-snapshot")),
                    Step("save", "Save vitals/resources/statuses", context => Operation(context.Prototype().Save(), context, "step4-save-vitals")),
                    Step("load", "Load vitals/resources/statuses", context => Operation(context.Prototype().Load(), context, "step4-load-vitals"))));
        }

        private static ITestLabAutomationSuite BuildQuestPersistenceSuite()
        {
            return Suite("feature.4.4.quest-persistence", "Feature 4.4 Quest Persistence", "4.4", 440,
                Required("Narrative Coordinator", "PersistenceService"),
                Scenario("quest-roundtrip", "Quest save/load round trip", 10,
                    Step("start-quest", "Start quest", context => Operation(context.Prototype().StartQuest(First<QuestDefinition>(context)), context, "step4-start-quest")),
                    Step("progress", "Report defeat progress", context => Operation(context.Prototype().ReportDefeat("prototype_enemy"), context, "step4-report-defeat")),
                    Step("save", "Save quest state", context => Operation(context.Prototype().Save(), context, "step4-save-quests")),
                    Step("load", "Load quest state", context => Operation(context.Prototype().Load(), context, "step4-load-quests"))));
        }

        private static ITestLabAutomationSuite BuildLocationPersistenceSuite()
        {
            return Suite("feature.4.5.location-persistence", "Feature 4.5 Location Persistence", "4.5", 450,
                Required("PlayerLocationPersistenceParticipant", "CurrentPlaceTracker", "PersistenceService"),
                Scenario("position-place-roundtrip", "Position and place save/load round trip", 10,
                    Step("teleport", "Teleport to test point", context => Operation(context.Prototype().Teleport(FirstTestPoint(context)), context, "step4-teleport")),
                    Step("reach", "Report reach location", context => Operation(context.Prototype().ReportReach(First<PlaceDefinition>(context)), context, "step4-report-reach")),
                    Step("validate-location", "Validate location", context => Operation(context.Prototype().ValidateCurrentLocation(), context, "step4-location")),
                    Step("save", "Save location", context => Operation(context.Prototype().Save(), context, "step4-save-location")),
                    Step("load", "Load location", context => Operation(context.Prototype().Load(), context, "step4-load-location"))));
        }

        private static ITestLabAutomationSuite BuildWorldEntityIdentitySuite()
        {
            return Suite("feature.4.6.world-entity-identity", "Feature 4.6 World Entity Identity", "4.6", 460,
                Required("WorldEntityRegistry", "WorldEntityIdentity"),
                Scenario("world-entity-identity-flow", "Persistent world entity identity flow", 10,
                    Step("spawn", "Spawn persistent loot", context => Operation(context.Prototype().SpawnPersistentWorldLoot(First<ItemDefinition>(context)), context, "step4-spawn-world-loot")),
                    Step("duplicate", "Reject duplicate entity", context => Operation(context.Prototype().AttemptDuplicateWorldEntityRegistration(), context, "step4-duplicate-world-entity")),
                    Step("destroy", "Destroy spawned loot", context => Operation(context.Prototype().DestroyLastSpawnedWorldLoot(), context, "step4-destroy-world-loot")),
                    Step("recreate", "Recreate destroyed loot", context => Operation(context.Prototype().RecreateDestroyedWorldLoot(), context, "step4-recreate-world-loot")),
                    Step("diagnostics", "Refresh diagnostics", context => Operation(context.Prototype().RefreshWorldEntityDiagnostics(), context, "step4-world-diagnostics"))));
        }

        private static ITestLabAutomationSuite BuildSaveSlotsRecoverySuite()
        {
            return Suite("feature.4.7.save-slots-autosave-load-ui", "Feature 4.7 Save Slots and Autosave", "4.7", 470,
                Required("PrototypeSaveSlotCatalog", "PersistenceService"),
                Scenario("save-slots-autosave-and-recovery", "Save slots, autosave, and recovery scan", 10,
                    Step("manual-one", "Save manual slot one", context => Operation(context.Prototype().SaveManualSlotOne(), context, "step4-manual-one")),
                    Step("manual-two", "Save manual slot one again", context => Operation(context.Prototype().SaveManualSlotOne(), context, "step4-manual-two")),
                    Step("validate-backup", "Validate manual backup", context => Operation(context.Prototype().ValidateManualSlotOneBackup(), context, "step4-validate-backup")),
                    Step("dirty", "Mark save dirty", context => Operation(context.Prototype().MarkSaveDirty(), context, "step4-dirty")),
                    Step("autosave", "Force autosave", context => Operation(context.Prototype().ForceAutosave(), context, "step4-autosave")),
                    Step("scan", "Run recovery scan", context => Operation(context.Prototype().RunRecoveryScan(), context, "step4-recovery-scan")),
                    Step("cleanup", "Cleanup temporary saves", context => Operation(context.Prototype().CleanupTemporarySaves(true), context, "step4-cleanup-temporary"))));
        }

        private static ITestLabAutomationSuite BuildFailureHardeningSuite()
        {
            return Suite("feature.4.8.persistence-recovery-hardening", "Feature 4.8 Persistence Recovery Hardening", "4.8", 480,
                Required("PersistenceService", "PersistenceRecovery"),
                Scenario("prepare-failure-recovers-cleanly", "Prepare failure is rejected without corrupting current save", 10,
                    Step("save", "Save baseline", context => Operation(context.Prototype().Save(), context, "step4-failure-baseline")),
                    Step("inject", "Inject prepare failure", context => Operation(context.Prototype().InjectPrepareFailure(), context, "step4-inject-prepare")),
                    Step("load-fails", "Load rejects injected prepare failure", context => Operation(context.Prototype().RunExpectedAutomationFailure(() => context.Prototype().Load()), context, "step4-expected-load-failure", acceptFailure: true)),
                    Step("validate", "Validate save after rejected load", context => Operation(context.Prototype().ValidateSave(), context, "step4-validate-after-failure"))));
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
                requiredRuntimeAreas: TestLabRuntimeArea.Character | TestLabRuntimeArea.Persistence,
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

        private static ItemDefinition FirstEquippableItem(TestLabAutomationContext context)
        {
            return context.Prototype().GetDefinitions<ItemDefinition>().FirstOrDefault(item => item != null && item.IsEquippable)
                ?? context.Prototype().GetDefinitions<ItemDefinition>().FirstOrDefault();
        }

        private static PrototypeTestPoint FirstTestPoint(TestLabAutomationContext context)
        {
            return context.Prototype().GetTestPoints().FirstOrDefault();
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
