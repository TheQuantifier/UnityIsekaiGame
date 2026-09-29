using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Gameplay;

namespace UnityIsekaiGame.Tests
{
    public sealed class PlayerLocationPersistenceTests
    {
        private readonly ArrayList cleanup = new ArrayList();

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object obj in cleanup)
            {
                if (obj != null)
                {
                    UnityEngine.Object.DestroyImmediate(obj);
                }
            }

            cleanup.Clear();
        }

        [Test]
        public void ParticipantDeclaresPlayerScopeOwnerAndLoadPhase()
        {
            GameObject player = CreateGameObject("Player");
            object participant = CreateParticipant(player.transform);

            Assert.That(Get<string>(participant, "ParticipantKey"), Is.EqualTo("player.location"));
            Assert.That(Get<int>(participant, "ParticipantSchemaVersion"), Is.EqualTo(1));
            Assert.That(Get<object>(participant, "Scope").ToString(), Is.EqualTo("Player"));
            Assert.That(Get<string>(participant, "OwnerId"), Is.EqualTo("local-player"));
            Assert.That(Get<object>(participant, "LoadPhase").ToString(), Is.EqualTo("PositionAndPlace"));
        }

        [Test]
        public void SameScenePositionAndRotationRoundTripRestoresExactly()
        {
            CreateGround();
            GameObject player = CreateGameObject("Player");
            player.AddComponent<CharacterController>();
            player.transform.SetPositionAndRotation(new Vector3(2f, 1.1f, 3f), Quaternion.Euler(0f, 75f, 0f));
            object participant = CreateParticipant(player.transform);

            object save = Invoke(participant, "CapturePayload");
            Assert.That(Get<bool>(save, "Succeeded"), Is.True, Get<string>(save, "Message"));
            player.transform.SetPositionAndRotation(new Vector3(-4f, 1.1f, -6f), Quaternion.identity);

            object prepare = Invoke(participant, "PreparePayload", Get<string>(save, "PayloadJson"), Get<int>(participant, "ParticipantSchemaVersion"));
            Assert.That(Get<bool>(prepare, "Succeeded"), Is.True, Get<string>(prepare, "Message"));
            object commit = Invoke(participant, "CommitPreparedPayload", Get<object>(prepare, "PreparedPayload"));

            Assert.That(Get<bool>(commit, "Succeeded"), Is.True, Get<string>(commit, "Message"));
            Assert.That(player.transform.position.x, Is.EqualTo(2f).Within(0.001f));
            Assert.That(player.transform.position.y, Is.EqualTo(1.1f).Within(0.001f));
            Assert.That(player.transform.position.z, Is.EqualTo(3f).Within(0.001f));
            Assert.That(Quaternion.Angle(player.transform.rotation, Quaternion.Euler(0f, 75f, 0f)), Is.LessThan(0.01f));
        }

        [Test]
        public void LocationRestorePreservesOpenPlayerMenu()
        {
            CreateGround();
            GameObject player = CreateGameObject("Player");
            player.AddComponent<CharacterController>();
            FakePlayerMenuController menu = new FakePlayerMenuController { IsOpen = true };
            object participant = CreateParticipant(player.transform, menu);

            object data = ValidSaveData();
            SetField(data, "positionX", 1f);
            SetField(data, "positionY", 1.1f);
            SetField(data, "positionZ", 2f);
            object prepare = Invoke(participant, "PreparePayload", JsonUtility.ToJson(data), Get<int>(participant, "ParticipantSchemaVersion"));
            Assert.That(Get<bool>(prepare, "Succeeded"), Is.True, Get<string>(prepare, "Message"));

            object commit = Invoke(participant, "CommitPreparedPayload", Get<object>(prepare, "PreparedPayload"));

            Assert.That(Get<bool>(commit, "Succeeded"), Is.True, Get<string>(commit, "Message"));
            Assert.That(menu.CloseCount, Is.EqualTo(0));
            Assert.That(menu.BeginRestoreCount, Is.EqualTo(1));
            Assert.That(menu.CompleteRestoreCount, Is.EqualTo(1));
            Assert.That(menu.IsOpen, Is.True);
        }

        [Test]
        public void InvalidPositionAndFutureSchemaAreRejectedBeforeCommit()
        {
            GameObject player = CreateGameObject("Player");
            object participant = CreateParticipant(player.transform);

            object invalidPosition = ValidSaveData();
            SetField(invalidPosition, "positionX", float.NaN);
            object positionPrepare = Invoke(participant, "PreparePayload", JsonUtility.ToJson(invalidPosition), Get<int>(participant, "ParticipantSchemaVersion"));
            Assert.That(Get<bool>(positionPrepare, "Succeeded"), Is.False);

            object future = ValidSaveData();
            SetField(future, "schemaVersion", 2);
            object futurePrepare = Invoke(participant, "PreparePayload", JsonUtility.ToJson(future), Get<int>(participant, "ParticipantSchemaVersion"));
            Assert.That(Get<bool>(futurePrepare, "Succeeded"), Is.False);
        }

        [Test]
        public void UnsafePositionUsesStableSpawnFallback()
        {
            GameObject player = CreateGameObject("Player");
            GameObject spawn = CreateGameObject("Spawn");
            spawn.transform.SetPositionAndRotation(new Vector3(8f, 1.1f, 9f), Quaternion.Euler(0f, 180f, 0f));
            Component spawnPoint = spawn.AddComponent(RequiredType("UnityIsekaiGame.Persistence.PlayerSpawnPoint"));
            Invoke(spawnPoint, "DevelopmentConfigure", "spawn.prototype.default", null, 100);
            object participant = CreateParticipant(player.transform);

            object data = ValidSaveData();
            SetField(data, "positionY", 500f);
            object prepare = Invoke(participant, "PreparePayload", JsonUtility.ToJson(data), Get<int>(participant, "ParticipantSchemaVersion"));
            Assert.That(Get<bool>(prepare, "Succeeded"), Is.True, Get<string>(prepare, "Message"));

            object commit = Invoke(participant, "CommitPreparedPayload", Get<object>(prepare, "PreparedPayload"));
            Assert.That(Get<bool>(commit, "Succeeded"), Is.True, Get<string>(commit, "Message"));
            Assert.That(player.transform.position, Is.EqualTo(new Vector3(8f, 1.1f, 9f)));
        }

        [Test]
        public void SavedPositionBelowTerrainGroundsAtSameHorizontalLocation()
        {
            GameObject player = CreateGameObject("Player");
            CharacterController controller = player.AddComponent<CharacterController>();
            controller.center = Vector3.up;
            CreateGround("Raised Ground", new Vector3(2f, 3.9f, 3f), new Vector3(8f, 0.2f, 8f));
            object participant = CreateParticipant(player.transform);

            object data = ValidSaveData();
            SetField(data, "positionX", 2f);
            SetField(data, "positionY", 1.1f);
            SetField(data, "positionZ", 3f);
            object prepare = Invoke(participant, "PreparePayload", JsonUtility.ToJson(data), Get<int>(participant, "ParticipantSchemaVersion"));
            Assert.That(Get<bool>(prepare, "Succeeded"), Is.True, Get<string>(prepare, "Message"));

            object commit = Invoke(participant, "CommitPreparedPayload", Get<object>(prepare, "PreparedPayload"));

            Assert.That(Get<bool>(commit, "Succeeded"), Is.True, Get<string>(commit, "Message"));
            Assert.That(player.transform.position.x, Is.EqualTo(2f).Within(0.001f));
            Assert.That(player.transform.position.y, Is.EqualTo(4f + controller.skinWidth).Within(0.001f));
            Assert.That(player.transform.position.z, Is.EqualTo(3f).Within(0.001f));
        }

        [Test]
        public void FallbackSpawnPointBelowTerrainGroundsBeforeRestore()
        {
            GameObject player = CreateGameObject("Player");
            CharacterController controller = player.AddComponent<CharacterController>();
            controller.center = Vector3.up;
            CreateGround("Spawn Raised Ground", new Vector3(8f, 5.9f, 9f), new Vector3(8f, 0.2f, 8f));
            GameObject spawn = CreateGameObject("Spawn");
            spawn.transform.SetPositionAndRotation(new Vector3(8f, 1.1f, 9f), Quaternion.Euler(0f, 180f, 0f));
            Component spawnPoint = spawn.AddComponent(RequiredType("UnityIsekaiGame.Persistence.PlayerSpawnPoint"));
            Invoke(spawnPoint, "DevelopmentConfigure", "spawn.prototype.default", null, 100);
            object participant = CreateParticipant(player.transform);

            object data = ValidSaveData();
            SetField(data, "positionX", 100f);
            SetField(data, "positionY", 1.1f);
            SetField(data, "positionZ", 100f);
            object prepare = Invoke(participant, "PreparePayload", JsonUtility.ToJson(data), Get<int>(participant, "ParticipantSchemaVersion"));
            Assert.That(Get<bool>(prepare, "Succeeded"), Is.True, Get<string>(prepare, "Message"));

            object commit = Invoke(participant, "CommitPreparedPayload", Get<object>(prepare, "PreparedPayload"));

            Assert.That(Get<bool>(commit, "Succeeded"), Is.True, Get<string>(commit, "Message"));
            Assert.That(player.transform.position.x, Is.EqualTo(8f).Within(0.001f));
            Assert.That(player.transform.position.y, Is.EqualTo(6f + controller.skinWidth).Within(0.001f));
            Assert.That(player.transform.position.z, Is.EqualTo(9f).Within(0.001f));
        }

        [Test]
        public void PrototypeSceneEntryGroundsSpawnPointBelowTerrain()
        {
            GameObject player = CreateGameObject("Player");
            CharacterController controller = player.AddComponent<CharacterController>();
            controller.center = Vector3.up;
            CreateGround("Lower Safety Ground", new Vector3(-3f, -0.1f, 6f), new Vector3(8f, 0.2f, 8f));
            CreateGround("Scene Entry Raised Ground", new Vector3(-3f, 4.9f, 6f), new Vector3(8f, 0.2f, 8f));
            GameObject spawn = CreateGameObject("Spawn");
            spawn.transform.SetPositionAndRotation(new Vector3(-3f, 1.1f, 6f), Quaternion.Euler(0f, 45f, 0f));
            GameObject controllerObject = CreateGameObject("Prototype Test Controller");
            PrototypeTestController testController = controllerObject.AddComponent<PrototypeTestController>();
            SetField(testController, "player", player.transform);
            SetField(testController, "playerSpawnPoint", spawn.transform);

            Invoke(testController, "Awake");
            Invoke(testController, "Start");

            Assert.That(player.transform.position.x, Is.EqualTo(-3f).Within(0.001f));
            Assert.That(player.transform.position.y, Is.EqualTo(5f + controller.skinWidth).Within(0.001f));
            Assert.That(player.transform.position.z, Is.EqualTo(6f).Within(0.001f));
            Assert.That(Quaternion.Angle(player.transform.rotation, spawn.transform.rotation), Is.LessThan(0.01f));
        }

        [Test]
        public void PrototypeEnemyGroundsToRaisedTerrainAtSceneEntryAndReset()
        {
            CreateGround("Enemy Raised Ground", new Vector3(4f, 3.9f, -2f), new Vector3(8f, 0.2f, 8f));
            GameObject enemy = CreateGameObject("Enemy");
            enemy.transform.position = new Vector3(4f, 0f, -2f);
            Component controller = enemy.AddComponent(RequiredType("UnityIsekaiGame.Combat.PrototypeEnemyController"));
            Physics.SyncTransforms();

            Invoke(controller, "Awake");
            Invoke(controller, "Start");

            Assert.That(enemy.transform.position.x, Is.EqualTo(4f).Within(0.001f));
            Assert.That(enemy.transform.position.y, Is.EqualTo(4.05f).Within(0.001f));
            Assert.That(enemy.transform.position.z, Is.EqualTo(-2f).Within(0.001f));

            enemy.transform.position = new Vector3(4f, 0f, -2f);
            Physics.SyncTransforms();
            Invoke(controller, "ResetControllerState");

            Assert.That(enemy.transform.position.x, Is.EqualTo(4f).Within(0.001f));
            Assert.That(enemy.transform.position.y, Is.EqualTo(4.05f).Within(0.001f));
            Assert.That(enemy.transform.position.z, Is.EqualTo(-2f).Within(0.001f));
        }

        [Test]
        public void CurrentPlaceTrackerChoosesDeepestPlaceAndReturnsToParent()
        {
            ScriptableObject parent = CreatePlace("place.region.prototype", "Region", "scene.prototype");
            ScriptableObject child = CreatePlace("place.poi.disturbance-site", "Disturbance", "scene.prototype", parent);
            GameObject player = CreateGameObject("Player");
            Component tracker = player.AddComponent(RequiredType("UnityIsekaiGame.Places.CurrentPlaceTracker"));

            Invoke(tracker, "NotifyEntered", parent, false);
            Assert.That(Get<string>(tracker, "CurrentPlaceId"), Is.EqualTo(Get<string>(parent, "Id")));
            Invoke(tracker, "NotifyEntered", child, false);
            Invoke(tracker, "NotifyEntered", child, false);
            Assert.That(Get<string>(tracker, "CurrentPlaceId"), Is.EqualTo(Get<string>(child, "Id")));
            Invoke(tracker, "NotifyExited", child, false);
            Assert.That(Get<string>(tracker, "CurrentPlaceId"), Is.EqualTo(Get<string>(parent, "Id")));
        }

        [Test]
        public void ReachLocationReporterSuppressesObjectiveSignalDuringLocationRestore()
        {
            ScriptableObject place = CreatePlace("place.poi.disturbance-site", "Disturbance", "scene.prototype");
            GameObject trigger = CreateGameObject("Trigger");
            Component reporter = trigger.AddComponent(RequiredType("UnityIsekaiGame.Quests.QuestReachLocationReporter"));
            SetField(reporter, "targetPlace", place);

            GameObject player = CreateGameObject("Player");
            player.AddComponent(RequiredType("UnityIsekaiGame.Input.PlayerInputReader"));
            Collider collider = player.AddComponent<BoxCollider>();
            GameObject authority = CreateGameObject("Authority");
            authority.SetActive(false);
            Component persistence = authority.AddComponent(RequiredType("UnityIsekaiGame.Gameplay.PrototypePersistenceServiceBehaviour"));
            SetField(persistence, "playerRoot", player.transform);

            int reached = 0;
            Type bus = RequiredType("UnityIsekaiGame.Quests.QuestObjectiveSignalBus");
            Action<string> handler = id =>
            {
                if (id == Get<string>(place, "Id"))
                {
                    reached++;
                }
            };
            bus.GetEvent("ReachedLocation", BindingFlags.Static | BindingFlags.Public).AddEventHandler(null, handler);
            try
            {
                object guard = InvokeStatic(RequiredType("UnityIsekaiGame.Persistence.LocationRestoreGuard"), "Enter");
                try
                {
                    Invoke(reporter, "OnTriggerEnter", collider);
                }
                finally
                {
                    ((IDisposable)guard).Dispose();
                }

                Assert.That(reached, Is.EqualTo(0));
                Invoke(reporter, "OnTriggerEnter", collider);
                Assert.That(reached, Is.EqualTo(1));
            }
            finally
            {
                bus.GetEvent("ReachedLocation", BindingFlags.Static | BindingFlags.Public).RemoveEventHandler(null, handler);
            }
        }

        private object CreateParticipant(Transform player)
        {
            return CreateParticipant(player, null);
        }

        private object CreateParticipant(Transform player, IPlayerMenuController menuController)
        {
            Type participantType = RequiredType("UnityIsekaiGame.Persistence.PlayerLocationPersistenceParticipant");
            Func<DefinitionRegistry> registryProvider = () => new DefinitionRegistry(Array.Empty<IGameDefinition>());
            return Activator.CreateInstance(
                participantType,
                player,
                registryProvider,
                "local-player",
                "scene.prototype",
                "spawn.prototype.default",
                null,
                menuController,
                null);
        }

        private object ValidSaveData()
        {
            Type saveType = RequiredType("UnityIsekaiGame.Persistence.PlayerLocationSaveData");
            object data = Activator.CreateInstance(saveType);
            SetField(data, "schemaVersion", 1);
            SetField(data, "sceneKey", "scene.prototype");
            SetField(data, "placeId", string.Empty);
            SetField(data, "positionX", 0f);
            SetField(data, "positionY", 1.1f);
            SetField(data, "positionZ", 0f);
            SetField(data, "rotationW", 1f);
            SetField(data, "spawnPointId", "spawn.prototype.default");
            SetField(data, "locationMode", "same-scene-v1");
            return data;
        }

        private GameObject CreateGround()
        {
            return CreateGround("Ground", new Vector3(0f, -0.1f, 0f), new Vector3(40f, 0.2f, 40f));
        }

        private GameObject CreateGround(string name, Vector3 center, Vector3 size)
        {
            GameObject ground = CreateGameObject(name);
            BoxCollider collider = ground.AddComponent<BoxCollider>();
            collider.size = size;
            collider.center = center;
            return ground;
        }

        private ScriptableObject CreatePlace(string id, string displayName, string sceneKey, ScriptableObject parent = null)
        {
            ScriptableObject place = ScriptableObject.CreateInstance(RequiredType("UnityIsekaiGame.Places.PlaceDefinition"));
            cleanup.Add(place);
            SetField(place, "placeId", id);
            SetField(place, "displayName", displayName);
            SetField(place, "sceneKey", sceneKey);
            SetField(place, "parentPlace", parent);
            return place;
        }

        private GameObject CreateGameObject(string name)
        {
            GameObject obj = new GameObject(name);
            cleanup.Add(obj);
            return obj;
        }

        private static Type RequiredType(string typeName)
        {
            Type type = TestTypeResolver.RequiredType(typeName);
            Assert.That(type, Is.Not.Null, $"Required type not found: {typeName}");
            return type;
        }

        private static T Get<T>(object target, string memberName)
        {
            object value = GetMember(target, memberName);
            return value == null ? default : (T)value;
        }

        private static object GetMember(object target, string memberName)
        {
            Type type = target.GetType();
            PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
            {
                return property.GetValue(target);
            }

            FieldInfo field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{memberName} not found on {type.Name}");
            return field.GetValue(target);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{fieldName} not found on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        private static object Invoke(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{methodName} not found on {target.GetType().Name}");
            return method.Invoke(target, args);
        }

        private static object InvokeStatic(Type type, string methodName, params object[] args)
        {
            MethodInfo method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{methodName} not found on {type.Name}");
            return method.Invoke(null, args);
        }

        private sealed class FakePlayerMenuController : IPlayerMenuController
        {
            public bool IsOpen { get; set; }
            public int CloseCount { get; private set; }
            public int BeginRestoreCount { get; private set; }
            public int CompleteRestoreCount { get; private set; }

            public void CloseForPrototypeReset()
            {
                CloseCount++;
                IsOpen = false;
            }

            public void BeginPersistenceRestore()
            {
                BeginRestoreCount++;
            }

            public void CompletePersistenceRestore()
            {
                CompleteRestoreCount++;
            }
        }
    }
}
