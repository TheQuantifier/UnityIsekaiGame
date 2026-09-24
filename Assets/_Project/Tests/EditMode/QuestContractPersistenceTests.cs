using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Persistence;

namespace UnityIsekaiGame.Tests
{
    public sealed class QuestContractPersistenceTests
    {
        private string testRoot;

        [SetUp]
        public void SetUp()
        {
            testRoot = Path.Combine(Path.GetTempPath(), "UnityIsekaiGameQuestContractPersistenceTests", Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrWhiteSpace(testRoot) && Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, true);
            }
        }

        [Test]
        public void ParticipantDeclaresPlayerScopeOwnerAndStableIdentity()
        {
            using RuntimeFixture fixture = RuntimeFixture.Create();
            object participant = CreateParticipant(fixture);

            Assert.That(Get<string>(participant, "ParticipantKey"), Is.EqualTo("player.quests-contracts"));
            Assert.That(Get<int>(participant, "ParticipantSchemaVersion"), Is.EqualTo(2));
            Assert.That(Get<bool>(participant, "IsRequired"), Is.True);
            Assert.That(Get<PersistenceScope>(participant, "Scope"), Is.EqualTo(PersistenceScope.Player));
            Assert.That(Get<string>(participant, "OwnerId"), Is.EqualTo(PersistenceService.LocalPlayerId));
            Assert.That(Get<PersistenceLoadPhase>(participant, "LoadPhase"), Is.EqualTo(PersistenceLoadPhase.QuestsAndContracts));
        }

        [Test]
        public void EmptyQuestContractStateRoundTrips()
        {
            using RuntimeFixture fixture = RuntimeFixture.Create();
            PersistenceService service = CreateService(fixture);

            Assert.That(service.Save("slot-0001").Succeeded, Is.True);
            PersistenceLoadResult load = service.Load("slot-0001");

            Assert.That(load.Succeeded, Is.True, load.Message);
            Assert.That(Count(Get<object>(fixture.QuestLog, "Quests")), Is.EqualTo(0));
            Assert.That(Count(Get<object>(fixture.ContractJournal, "Contracts")), Is.EqualTo(0));
        }

        [Test]
        public void ActiveContractObjectiveProgressRoundTripsWithoutCompleting()
        {
            using RuntimeFixture fixture = RuntimeFixture.Create();
            using TargetFixture targets = new TargetFixture();
            ScriptableObject objective = CreateDefeatObjective("defeat_goblin", "goblin", 3);
            ScriptableObject contract = CreateContract("contract.goblin", objective);
            fixture.SetRegistry(new IGameDefinition[] { (IGameDefinition)contract });
            Invoke(fixture.ContractJournal, "AcceptContract", contract);
            Invoke(fixture.ContractJournal, "RecordDefeat", targets.CreateTarget("goblin"));
            PersistenceService service = CreateService(fixture);
            Assert.That(service.Save("slot-0001").Succeeded, Is.True);

            Invoke(fixture.ContractJournal, "RecordDefeat", targets.CreateTarget("goblin"));
            Invoke(fixture.ContractJournal, "RecordDefeat", targets.CreateTarget("goblin"));
            Assert.That(Get<int>(FirstObjective(FirstContract(fixture.ContractJournal)), "CurrentProgress"), Is.EqualTo(3));

            PersistenceLoadResult load = service.Load("slot-0001");

            Assert.That(load.Succeeded, Is.True, load.Message);
            object restored = FirstContract(fixture.ContractJournal);
            object restoredObjective = FirstObjective(restored);
            Assert.That(Get<object>(restored, "State").ToString(), Is.EqualTo("Active"));
            Assert.That(Get<int>(restoredObjective, "CurrentProgress"), Is.EqualTo(1));
            Assert.That(Get<bool>(restoredObjective, "IsComplete"), Is.False);
        }

        [Test]
        public void ActiveQuestStageAndObjectiveProgressRoundTrip()
        {
            using RuntimeFixture fixture = RuntimeFixture.Create();
            using TargetFixture targets = new TargetFixture();
            ScriptableObject objective = CreateDefeatObjective("defeat_shade", "shade", 2);
            ScriptableObject quest = CreateQuest("quest.strange-disturbance", objective);
            fixture.SetRegistry(new IGameDefinition[] { (IGameDefinition)quest });
            Invoke(fixture.QuestLog, "StartQuest", quest);
            Invoke(fixture.QuestLog, "RecordDefeat", targets.CreateTarget("shade"));
            PersistenceService service = CreateService(fixture);
            Assert.That(service.Save("slot-0001").Succeeded, Is.True);

            Invoke(fixture.QuestLog, "RecordDefeat", targets.CreateTarget("shade"));
            Assert.That(Get<object>(FirstQuest(fixture.QuestLog), "State").ToString(), Is.EqualTo("Completed"));

            PersistenceLoadResult load = service.Load("slot-0001");

            Assert.That(load.Succeeded, Is.True, load.Message);
            object restored = FirstQuest(fixture.QuestLog);
            Assert.That(Get<int>(restored, "CurrentStageIndex"), Is.EqualTo(0));
            Assert.That(Get<object>(restored, "State").ToString(), Is.EqualTo("Active"));
            Assert.That(Get<int>(FirstObjective(restored), "CurrentProgress"), Is.EqualTo(1));
        }

        [Test]
        public void PrepareRejectsDuplicateRuntimeIdsWithoutMutation()
        {
            using RuntimeFixture fixture = RuntimeFixture.Create();
            ScriptableObject objective = CreateDefeatObjective("defeat_goblin", "goblin", 1);
            ScriptableObject contract = CreateContract("contract.goblin", objective);
            fixture.SetRegistry(new IGameDefinition[] { (IGameDefinition)contract });
            Invoke(fixture.ContractJournal, "AcceptContract", contract);
            object participant = CreateParticipant(fixture);
            object payload = CreatePayload();
            AddContractSave(payload, "contract.goblin", "11111111-1111-4111-8111-111111111111", "Active", objective, 0, false);
            AddContractSave(payload, "contract.goblin", "11111111-1111-4111-8111-111111111111", "Active", objective, 0, false);

            object result = Invoke(participant, "PreparePayload", JsonUtility.ToJson(payload), 2);

            Assert.That(Get<bool>(result, "Succeeded"), Is.False);
            Assert.That(Count(Get<object>(fixture.ContractJournal, "Contracts")), Is.EqualTo(1));
        }

        [Test]
        public void PrepareRejectsMissingQuestDefinition()
        {
            using RuntimeFixture fixture = RuntimeFixture.Create();
            object participant = CreateParticipant(fixture);
            object payload = CreatePayload();
            AddQuestSave(payload, "quest.missing", "quest.missing", "Active", 0, "stage.start");

            object result = Invoke(participant, "PreparePayload", JsonUtility.ToJson(payload), 2);

            Assert.That(Get<bool>(result, "Succeeded"), Is.False);
        }

        [Test]
        public void PrepareRejectsLegacySchemaOneIndexBasedQuestObjectiveSaves()
        {
            using RuntimeFixture fixture = RuntimeFixture.Create();
            object participant = CreateParticipant(fixture);
            object payload = CreatePayload();

            object result = Invoke(participant, "PreparePayload", JsonUtility.ToJson(payload), 1);

            Assert.That(Get<bool>(result, "Succeeded"), Is.False);
        }

        [Test]
        public void QuestStageReorderRestoresByStableStageId()
        {
            using RuntimeFixture fixture = RuntimeFixture.Create();
            using TargetFixture targets = new TargetFixture();
            ScriptableObject savedObjective = CreateDefeatObjective("defeat_saved", "saved", 2);
            ScriptableObject otherObjective = CreateDefeatObjective("defeat_other", "other", 2);
            ScriptableObject originalQuest = CreateQuest("quest.reorder-stage", new[] { "stage.saved", "stage.other" }, new[] { new[] { savedObjective }, new[] { otherObjective } });
            fixture.SetRegistry(new IGameDefinition[] { (IGameDefinition)originalQuest });
            Invoke(fixture.QuestLog, "StartQuest", originalQuest);
            Invoke(fixture.QuestLog, "RecordDefeat", targets.CreateTarget("saved"));
            PersistenceService service = CreateService(fixture);
            Assert.That(service.Save("slot-0001").Succeeded, Is.True);

            ScriptableObject reorderedQuest = CreateQuest("quest.reorder-stage", new[] { "stage.other", "stage.saved" }, new[] { new[] { otherObjective }, new[] { savedObjective } });
            fixture.SetRegistry(new IGameDefinition[] { (IGameDefinition)reorderedQuest });
            PersistenceLoadResult load = service.Load("slot-0001");

            Assert.That(load.Succeeded, Is.True, load.Message);
            object restored = FirstQuest(fixture.QuestLog);
            Assert.That(Get<int>(restored, "CurrentStageIndex"), Is.EqualTo(1));
            Assert.That(Get<int>(FindObjectiveById(restored, "defeat_saved"), "CurrentProgress"), Is.EqualTo(1));
        }

        [Test]
        public void QuestObjectiveReorderRestoresByStableObjectiveId()
        {
            using RuntimeFixture fixture = RuntimeFixture.Create();
            using TargetFixture targets = new TargetFixture();
            ScriptableObject first = CreateDefeatObjective("defeat_first", "first", 2);
            ScriptableObject second = CreateDefeatObjective("defeat_second", "second", 2);
            ScriptableObject originalQuest = CreateQuest("quest.reorder-objective", new[] { "stage.start" }, new[] { new[] { first, second } });
            fixture.SetRegistry(new IGameDefinition[] { (IGameDefinition)originalQuest });
            Invoke(fixture.QuestLog, "StartQuest", originalQuest);
            Invoke(fixture.QuestLog, "RecordDefeat", targets.CreateTarget("second"));
            PersistenceService service = CreateService(fixture);
            Assert.That(service.Save("slot-0001").Succeeded, Is.True);

            ScriptableObject reorderedQuest = CreateQuest("quest.reorder-objective", new[] { "stage.start" }, new[] { new[] { second, first } });
            fixture.SetRegistry(new IGameDefinition[] { (IGameDefinition)reorderedQuest });
            PersistenceLoadResult load = service.Load("slot-0001");

            Assert.That(load.Succeeded, Is.True, load.Message);
            Assert.That(Get<int>(FindObjectiveById(FirstQuest(fixture.QuestLog), "defeat_second"), "CurrentProgress"), Is.EqualTo(1));
            Assert.That(Get<int>(FindObjectiveById(FirstQuest(fixture.QuestLog), "defeat_first"), "CurrentProgress"), Is.EqualTo(0));
        }

        [Test]
        public void ContractObjectiveReorderRestoresByStableObjectiveId()
        {
            using RuntimeFixture fixture = RuntimeFixture.Create();
            using TargetFixture targets = new TargetFixture();
            ScriptableObject first = CreateDefeatObjective("defeat_first", "first", 2);
            ScriptableObject second = CreateDefeatObjective("defeat_second", "second", 2);
            ScriptableObject originalContract = CreateContract("contract.reorder-objective", first, second);
            fixture.SetRegistry(new IGameDefinition[] { (IGameDefinition)originalContract });
            Invoke(fixture.ContractJournal, "AcceptContract", originalContract);
            Invoke(fixture.ContractJournal, "RecordDefeat", targets.CreateTarget("second"));
            PersistenceService service = CreateService(fixture);
            Assert.That(service.Save("slot-0001").Succeeded, Is.True);

            ScriptableObject reorderedContract = CreateContract("contract.reorder-objective", second, first);
            fixture.SetRegistry(new IGameDefinition[] { (IGameDefinition)reorderedContract });
            PersistenceLoadResult load = service.Load("slot-0001");

            Assert.That(load.Succeeded, Is.True, load.Message);
            Assert.That(Get<int>(FindObjectiveById(FirstContract(fixture.ContractJournal), "defeat_second"), "CurrentProgress"), Is.EqualTo(1));
            Assert.That(Get<int>(FindObjectiveById(FirstContract(fixture.ContractJournal), "defeat_first"), "CurrentProgress"), Is.EqualTo(0));
        }

        [Test]
        public void InsertedQuestObjectiveIsRejectedInsteadOfSilentlyDefaulted()
        {
            using RuntimeFixture fixture = RuntimeFixture.Create();
            ScriptableObject original = CreateDefeatObjective("defeat_original", "original", 2);
            ScriptableObject inserted = CreateDefeatObjective("defeat_inserted", "inserted", 2);
            ScriptableObject originalQuest = CreateQuest("quest.inserted-objective", new[] { "stage.start" }, new[] { new[] { original } });
            fixture.SetRegistry(new IGameDefinition[] { (IGameDefinition)originalQuest });
            Invoke(fixture.QuestLog, "StartQuest", originalQuest);
            PersistenceService service = CreateService(fixture);
            Assert.That(service.Save("slot-0001").Succeeded, Is.True);

            ScriptableObject changedQuest = CreateQuest("quest.inserted-objective", new[] { "stage.start" }, new[] { new[] { original, inserted } });
            fixture.SetRegistry(new IGameDefinition[] { (IGameDefinition)changedQuest });
            PersistenceLoadResult load = service.Load("slot-0001");

            Assert.That(load.Succeeded, Is.False);
        }

        [Test]
        public void RemovedContractObjectiveIsRejectedInsteadOfSilentlyDroppingProgress()
        {
            using RuntimeFixture fixture = RuntimeFixture.Create();
            ScriptableObject kept = CreateDefeatObjective("defeat_kept", "kept", 2);
            ScriptableObject removed = CreateDefeatObjective("defeat_removed", "removed", 2);
            ScriptableObject originalContract = CreateContract("contract.removed-objective", kept, removed);
            fixture.SetRegistry(new IGameDefinition[] { (IGameDefinition)originalContract });
            Invoke(fixture.ContractJournal, "AcceptContract", originalContract);
            PersistenceService service = CreateService(fixture);
            Assert.That(service.Save("slot-0001").Succeeded, Is.True);

            ScriptableObject changedContract = CreateContract("contract.removed-objective", kept);
            fixture.SetRegistry(new IGameDefinition[] { (IGameDefinition)changedContract });
            PersistenceLoadResult load = service.Load("slot-0001");

            Assert.That(load.Succeeded, Is.False);
        }

        [Test]
        public void InsertedContractObjectiveIsRejectedInsteadOfSilentlyDefaulted()
        {
            using RuntimeFixture fixture = RuntimeFixture.Create();
            ScriptableObject original = CreateDefeatObjective("defeat_original", "original", 2);
            ScriptableObject inserted = CreateDefeatObjective("defeat_inserted", "inserted", 2);
            ScriptableObject originalContract = CreateContract("contract.inserted-objective", original);
            fixture.SetRegistry(new IGameDefinition[] { (IGameDefinition)originalContract });
            Invoke(fixture.ContractJournal, "AcceptContract", originalContract);
            PersistenceService service = CreateService(fixture);
            Assert.That(service.Save("slot-0001").Succeeded, Is.True);

            ScriptableObject changedContract = CreateContract("contract.inserted-objective", original, inserted);
            fixture.SetRegistry(new IGameDefinition[] { (IGameDefinition)changedContract });
            PersistenceLoadResult load = service.Load("slot-0001");

            Assert.That(load.Succeeded, Is.False);
        }

        [Test]
        public void RemovedQuestObjectiveIsRejectedInsteadOfSilentlyDroppingProgress()
        {
            using RuntimeFixture fixture = RuntimeFixture.Create();
            ScriptableObject kept = CreateDefeatObjective("defeat_kept", "kept", 2);
            ScriptableObject removed = CreateDefeatObjective("defeat_removed", "removed", 2);
            ScriptableObject originalQuest = CreateQuest("quest.removed-objective", new[] { "stage.start" }, new[] { new[] { kept, removed } });
            fixture.SetRegistry(new IGameDefinition[] { (IGameDefinition)originalQuest });
            Invoke(fixture.QuestLog, "StartQuest", originalQuest);
            PersistenceService service = CreateService(fixture);
            Assert.That(service.Save("slot-0001").Succeeded, Is.True);

            ScriptableObject changedQuest = CreateQuest("quest.removed-objective", new[] { "stage.start" }, new[] { new[] { kept } });
            fixture.SetRegistry(new IGameDefinition[] { (IGameDefinition)changedQuest });
            PersistenceLoadResult load = service.Load("slot-0001");

            Assert.That(load.Succeeded, Is.False);
        }

        [Test]
        public void DefinitionValidationRejectsDuplicateAndMissingQuestStageObjectiveIds()
        {
            ScriptableObject duplicateStageQuest = CreateQuest("quest.duplicate-stage", new[] { "stage.same", "stage.same" }, new[] { new[] { CreateDefeatObjective("defeat_one", "one", 1) }, new[] { CreateDefeatObjective("defeat_two", "two", 1) } });
            ScriptableObject missingStageQuest = CreateQuest("quest.missing-stage", new[] { string.Empty }, new[] { new[] { CreateDefeatObjective("defeat_one", "one", 1) } });
            ScriptableObject duplicateObjectiveQuest = CreateQuest("quest.duplicate-objective", new[] { "stage.start" }, new[] { new[] { CreateDefeatObjective("defeat_same", "one", 1), CreateDefeatObjective("defeat_same", "two", 1) } });
            ScriptableObject missingObjectiveQuest = CreateQuest("quest.missing-objective", new[] { "stage.start" }, new[] { new[] { CreateDefeatObjective(string.Empty, "one", 1) } });
            DefinitionValidationReport report = new DefinitionValidationReport();

            Invoke(duplicateStageQuest, "ValidateCatalogDefinition", new Dictionary<string, IGameDefinition>(), report);
            Invoke(missingStageQuest, "ValidateCatalogDefinition", new Dictionary<string, IGameDefinition>(), report);
            Invoke(duplicateObjectiveQuest, "ValidateCatalogDefinition", new Dictionary<string, IGameDefinition>(), report);
            Invoke(missingObjectiveQuest, "ValidateCatalogDefinition", new Dictionary<string, IGameDefinition>(), report);

            Assert.That(report.ErrorCount, Is.EqualTo(4));
        }

        [Test]
        public void DefinitionValidationRejectsDuplicateAndMissingContractObjectiveIds()
        {
            ScriptableObject duplicateContract = CreateContract("contract.duplicate-objective", CreateDefeatObjective("defeat_same", "one", 1), CreateDefeatObjective("defeat_same", "two", 1));
            ScriptableObject missingContract = CreateContract("contract.missing-objective", CreateDefeatObjective(string.Empty, "one", 1));
            DefinitionValidationReport report = new DefinitionValidationReport();

            Invoke(duplicateContract, "ValidateCatalogDefinition", new Dictionary<string, IGameDefinition>(), report);
            Invoke(missingContract, "ValidateCatalogDefinition", new Dictionary<string, IGameDefinition>(), report);

            Assert.That(report.ErrorCount, Is.EqualTo(2));
        }

        [Test]
        public void RestoreRejectsObjectiveIdResolvedToDifferentObjectiveType()
        {
            using RuntimeFixture fixture = RuntimeFixture.Create();
            using TargetFixture targets = new TargetFixture();
            ScriptableObject defeat = CreateDefeatObjective("objective_same", "saved", 2);
            ScriptableObject originalQuest = CreateQuest("quest.type-mismatch", new[] { "stage.start" }, new[] { new[] { defeat } });
            fixture.SetRegistry(new IGameDefinition[] { (IGameDefinition)originalQuest });
            Invoke(fixture.QuestLog, "StartQuest", originalQuest);
            Invoke(fixture.QuestLog, "RecordDefeat", targets.CreateTarget("saved"));
            PersistenceService service = CreateService(fixture);
            Assert.That(service.Save("slot-0001").Succeeded, Is.True);

            ScriptableObject talk = CreateTalkObjective("objective_same");
            ScriptableObject changedQuest = CreateQuest("quest.type-mismatch", new[] { "stage.start" }, new[] { new[] { talk } });
            fixture.SetRegistry(new IGameDefinition[] { (IGameDefinition)changedQuest });
            PersistenceLoadResult load = service.Load("slot-0001");

            Assert.That(load.Succeeded, Is.False);
        }

        private PersistenceService CreateService(RuntimeFixture fixture)
        {
            PersistenceService service = new PersistenceService(new PersistencePathProvider(testRoot));
            service.RegisterParticipant(new DependencyParticipant(PlayerInventoryEquipmentPersistenceParticipant.Key), out _);
            service.RegisterParticipant(new DependencyParticipant(PlayerAttributesPersistenceParticipant.Key), out _);
            service.RegisterParticipant(new DependencyParticipant(PlayerStatusEffectsPersistenceParticipant.Key), out _);
            service.RegisterParticipant(new DependencyParticipant(PlayerResourcesPersistenceParticipant.Key), out _);
            Assert.That(service.RegisterParticipant((IPersistenceParticipant)CreateParticipant(fixture), out string failureReason), Is.True, failureReason);
            return service;
        }

        private sealed class DependencyParticipant : IPersistenceParticipant
        {
            public DependencyParticipant(string key) => ParticipantKey = key;
            public string ParticipantKey { get; }
            public int ParticipantSchemaVersion => 1;
            public bool IsRequired => true;
            public PersistenceScope Scope => PersistenceScope.Player;
            public string OwnerId => PersistenceService.LocalPlayerId;
            public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.Bootstrap;
            public int LoadPriority => -100;
            public PersistenceParticipantSaveResult CapturePayload() => PersistenceParticipantSaveResult.Success("{}");
            public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion) => PersistenceParticipantPrepareResult.Success(new object());
            public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload) => PersistenceParticipantCommitResult.Success();
            public void DiscardPreparedPayload(object preparedPayload) { }
        }

        private static object CreateParticipant(RuntimeFixture fixture)
        {
            return Activator.CreateInstance(
                RequiredType("UnityIsekaiGame.Persistence.PlayerQuestContractPersistenceParticipant"),
                fixture.QuestLog,
                fixture.ContractJournal,
                fixture.Inventory,
                (Func<DefinitionRegistry>)(() => fixture.Registry),
                PersistenceService.LocalPlayerId);
        }

        private static object CreatePayload()
        {
            return Activator.CreateInstance(RequiredType("UnityIsekaiGame.Persistence.PlayerQuestContractSaveData"));
        }

        private static void AddContractSave(object payload, string definitionId, string runtimeId, string state, ScriptableObject objectiveDefinition, int progress, bool complete)
        {
            IList contracts = (IList)payload.GetType().GetField("contracts").GetValue(payload);
            object entry = Activator.CreateInstance(RequiredType("UnityIsekaiGame.Persistence.ContractInstanceSaveData"));
            SetField(entry, "contractDefinitionId", definitionId);
            SetField(entry, "runtimeInstanceId", runtimeId);
            SetField(entry, "state", Enum.Parse(RequiredType("UnityIsekaiGame.Contracts.ContractState"), state));
            IList objectives = (IList)entry.GetType().GetField("objectives").GetValue(entry);
            string objectiveId = Get<string>(objectiveDefinition, "ObjectiveId");
            objectives.Add(CreateObjectiveSave(objectiveId, objectiveId, 0, objectiveDefinition.GetType().Name, progress, 1, complete));
            contracts.Add(entry);
        }

        private static void AddQuestSave(object payload, string definitionId, string runtimeId, string state, int stageIndex, string stageId)
        {
            IList quests = (IList)payload.GetType().GetField("quests").GetValue(payload);
            object entry = Activator.CreateInstance(RequiredType("UnityIsekaiGame.Persistence.QuestInstanceSaveData"));
            SetField(entry, "questDefinitionId", definitionId);
            SetField(entry, "runtimeInstanceId", runtimeId);
            SetField(entry, "state", Enum.Parse(RequiredType("UnityIsekaiGame.Quests.QuestState"), state));
            SetField(entry, "currentStageIndex", stageIndex);
            SetField(entry, "currentStageId", stageId);
            quests.Add(entry);
        }

        private static object CreateObjectiveSave(string key, string objectiveId, int index, string type, int progress, int required, bool complete)
        {
            object save = Activator.CreateInstance(RequiredType("UnityIsekaiGame.Persistence.ObjectiveProgressSaveData"));
            SetField(save, "objectiveKey", key);
            SetField(save, "objectiveId", objectiveId);
            SetField(save, "objectiveIndex", index);
            SetField(save, "objectiveType", type);
            SetField(save, "currentProgress", progress);
            SetField(save, "requiredProgress", required);
            SetField(save, "completed", complete);
            return save;
        }

        private static ScriptableObject CreateDefeatObjective(string objectiveId, string targetCategory, int requiredDefeats)
        {
            ScriptableObject objective = ScriptableObject.CreateInstance(RequiredType("UnityIsekaiGame.Contracts.DefeatObjectiveDefinition"));
            SerializedObject serialized = new SerializedObject(objective);
            serialized.FindProperty("objectiveId").stringValue = objectiveId;
            serialized.FindProperty("targetCategory").stringValue = targetCategory;
            serialized.FindProperty("requiredDefeats").intValue = requiredDefeats;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return objective;
        }

        private static ScriptableObject CreateContract(string id, params ScriptableObject[] objectiveDefinitions)
        {
            ScriptableObject contract = ScriptableObject.CreateInstance(RequiredType("UnityIsekaiGame.Contracts.ContractDefinition"));
            SerializedObject serialized = new SerializedObject(contract);
            serialized.FindProperty("contractId").stringValue = id;
            serialized.FindProperty("displayTitle").stringValue = id;
            SerializedProperty objectives = serialized.FindProperty("objectives");
            objectives.arraySize = objectiveDefinitions == null ? 0 : objectiveDefinitions.Length;
            for (int i = 0; i < objectives.arraySize; i++)
            {
                objectives.GetArrayElementAtIndex(i).objectReferenceValue = objectiveDefinitions[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return contract;
        }

        private static ScriptableObject CreateQuest(string id, ScriptableObject objective)
        {
            return CreateQuest(id, new[] { "stage.start" }, new[] { new[] { objective } });
        }

        private static ScriptableObject CreateQuest(string id, string[] stageIds, ScriptableObject[][] stageObjectives)
        {
            ScriptableObject quest = ScriptableObject.CreateInstance(RequiredType("UnityIsekaiGame.Quests.QuestDefinition"));
            SerializedObject serialized = new SerializedObject(quest);
            serialized.FindProperty("questId").stringValue = id;
            serialized.FindProperty("title").stringValue = id;
            SerializedProperty stages = serialized.FindProperty("stages");
            stages.arraySize = stageIds == null ? 0 : stageIds.Length;
            for (int stageIndex = 0; stageIndex < stages.arraySize; stageIndex++)
            {
                SerializedProperty stage = stages.GetArrayElementAtIndex(stageIndex);
                stage.FindPropertyRelative("stageId").stringValue = stageIds[stageIndex];
                stage.FindPropertyRelative("nextStageIndex").intValue = -1;
                SerializedProperty objectives = stage.FindPropertyRelative("objectives");
                ScriptableObject[] objectiveDefinitions = stageObjectives[stageIndex];
                objectives.arraySize = objectiveDefinitions == null ? 0 : objectiveDefinitions.Length;
                for (int objectiveIndex = 0; objectiveIndex < objectives.arraySize; objectiveIndex++)
                {
                    objectives.GetArrayElementAtIndex(objectiveIndex).objectReferenceValue = objectiveDefinitions[objectiveIndex];
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return quest;
        }

        private static ScriptableObject CreateTalkObjective(string objectiveId)
        {
            ScriptableObject objective = ScriptableObject.CreateInstance(RequiredType("UnityIsekaiGame.Quests.TalkObjectiveDefinition"));
            SerializedObject serialized = new SerializedObject(objective);
            serialized.FindProperty("objectiveId").stringValue = objectiveId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return objective;
        }

        private static object FirstContract(Component journal)
        {
            foreach (object contract in (IEnumerable)Get<object>(journal, "Contracts"))
            {
                return contract;
            }

            return null;
        }

        private static object FirstQuest(Component log)
        {
            foreach (object quest in (IEnumerable)Get<object>(log, "Quests"))
            {
                return quest;
            }

            return null;
        }

        private static object FirstObjective(object instance)
        {
            object collection = Get<object>(instance, instance.GetType().Name == "QuestInstance" ? "CurrentObjectives" : "Objectives");
            foreach (object objective in (IEnumerable)collection)
            {
                return objective;
            }

            return null;
        }

        private static object FindObjectiveById(object instance, string objectiveId)
        {
            object collection = Get<object>(instance, instance.GetType().Name == "QuestInstance" ? "CurrentObjectives" : "Objectives");
            foreach (object objective in (IEnumerable)collection)
            {
                object definition = Get<object>(objective, "Definition");
                if (definition != null && Get<string>(definition, "ObjectiveId") == objectiveId)
                {
                    return objective;
                }
            }

            return null;
        }

        private static int Count(object collection)
        {
            int count = 0;
            foreach (object _ in (IEnumerable)collection)
            {
                count++;
            }

            return count;
        }

        private static Type RequiredType(string fullName)
        {
            Type type = TestTypeResolver.RequiredType(fullName);
            Assert.That(type, Is.Not.Null, $"Expected runtime type {fullName} to exist in loaded project assemblies.");
            return type;
        }

        private static object Invoke(object target, string methodName, params object[] args)
        {
            return target.GetType().GetMethod(methodName).Invoke(target, args);
        }

        private static void InvokeNonPublic(object target, string methodName)
        {
            target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, Array.Empty<object>());
        }

        private static T Get<T>(object target, string propertyName)
        {
            return (T)target.GetType().GetProperty(propertyName).GetValue(target);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType().GetField(fieldName).SetValue(target, value);
        }

        private sealed class RuntimeFixture : IDisposable
        {
            public GameObject Root { get; private set; }
            public Component Inventory { get; private set; }
            public Component QuestLog { get; private set; }
            public Component ContractJournal { get; private set; }
            public DefinitionRegistry Registry { get; private set; } = new DefinitionRegistry(Array.Empty<IGameDefinition>());

            public static RuntimeFixture Create()
            {
                RuntimeFixture fixture = new RuntimeFixture();
                fixture.Root = new GameObject("Quest Contract Persistence Test");
                fixture.Inventory = fixture.Root.AddComponent(RequiredType("UnityIsekaiGame.Inventory.PlayerInventory"));
                fixture.QuestLog = fixture.Root.AddComponent(RequiredType("UnityIsekaiGame.Quests.PlayerQuestLog"));
                fixture.ContractJournal = fixture.Root.AddComponent(RequiredType("UnityIsekaiGame.Contracts.PlayerContractJournal"));
                InvokeNonPublic(fixture.Inventory, "Awake");
                InvokeNonPublic(fixture.QuestLog, "Awake");
                InvokeNonPublic(fixture.ContractJournal, "Awake");
                return fixture;
            }

            public void SetRegistry(IEnumerable<IGameDefinition> definitions)
            {
                Registry = new DefinitionRegistry(definitions);
            }

            public void Dispose()
            {
                if (Root != null)
                {
                    UnityEngine.Object.DestroyImmediate(Root);
                }
            }
        }

        private sealed class TargetFixture : IDisposable
        {
            private readonly List<GameObject> targets = new List<GameObject>();

            public object CreateTarget(string category)
            {
                GameObject gameObject = new GameObject("Contract Target");
                targets.Add(gameObject);
                object target = gameObject.AddComponent(RequiredType("UnityIsekaiGame.Contracts.ContractObjectiveTarget"));
                SerializedObject serialized = new SerializedObject((UnityEngine.Object)target);
                serialized.FindProperty("targetCategory").stringValue = category;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                return target;
            }

            public void Dispose()
            {
                foreach (GameObject target in targets)
                {
                    if (target != null)
                    {
                        UnityEngine.Object.DestroyImmediate(target);
                    }
                }
            }
        }
    }
}
