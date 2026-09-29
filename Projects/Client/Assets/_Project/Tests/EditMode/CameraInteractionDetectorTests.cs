using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.Interaction;

namespace UnityIsekaiGame.Tests
{
    public sealed class CameraInteractionDetectorTests
    {
        private GameObject root;
        private GameObject player;
        private CameraInteractionDetector detector;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Interaction Detector Test Root");
            player = new GameObject("Player");
            player.transform.SetParent(root.transform, false);
            GameObject origin = new GameObject("Ray Origin");
            origin.transform.SetParent(player.transform, false);
            detector = origin.AddComponent<CameraInteractionDetector>();

            SerializedObject serialized = new SerializedObject(detector);
            serialized.FindProperty("interactor").objectReferenceValue = player;
            serialized.FindProperty("rayOrigin").objectReferenceValue = origin.transform;
            serialized.FindProperty("maxDistance").floatValue = 4f;
            serialized.FindProperty("interactionMask").intValue = ~0;
            serialized.FindProperty("triggerInteraction").enumValueIndex = (int)QueryTriggerInteraction.Collide;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        [Test]
        public void RefreshTarget_SkipsSelfAndUnrelatedTriggers_ThenSelectsInteractable()
        {
            CreateCollider("Player Body", new Vector3(0f, 0f, 0.5f), false, player.transform);
            CreateCollider("Unrelated Volume", new Vector3(0f, 0f, 1f), true);
            TestInteractable expected = CreateInteractable("Target", new Vector3(0f, 0f, 2f));

            Physics.SyncTransforms();
            detector.RefreshTarget();

            Assert.That(detector.CurrentInteractable, Is.SameAs(expected));
        }

        [Test]
        public void RefreshTarget_SolidGeometryBlocksInteractionThroughWalls()
        {
            CreateCollider("Wall", new Vector3(0f, 0f, 1.5f), false);
            CreateInteractable("Target Behind Wall", new Vector3(0f, 0f, 2.5f));

            Physics.SyncTransforms();
            detector.RefreshTarget();

            Assert.That(detector.HasTarget, Is.False);
        }

        [Test]
        public void RefreshTarget_SelectsNearestOverlap_AndSwitchesWhenItLeavesRange()
        {
            TestInteractable near = CreateInteractable("Near Target", new Vector3(0f, 0f, 1.5f));
            TestInteractable far = CreateInteractable("Far Target", new Vector3(0f, 0f, 2.5f));

            Physics.SyncTransforms();
            detector.RefreshTarget();
            Assert.That(detector.CurrentInteractable, Is.SameAs(near));

            near.gameObject.SetActive(false);
            Physics.SyncTransforms();
            detector.RefreshTarget();
            Assert.That(detector.CurrentInteractable, Is.SameAs(far));
        }

        [Test]
        public void RefreshTarget_RejectsTargetsBeyondConfiguredRange()
        {
            CreateInteractable("Out Of Range", new Vector3(0f, 0f, 5f));

            Physics.SyncTransforms();
            detector.RefreshTarget();

            Assert.That(detector.HasTarget, Is.False);
        }

        private TestInteractable CreateInteractable(string name, Vector3 position)
        {
            GameObject obj = CreateCollider(name, position, true);
            return obj.AddComponent<TestInteractable>();
        }

        private GameObject CreateCollider(string name, Vector3 position, bool trigger, Transform parent = null)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent != null ? parent : root.transform, true);
            obj.transform.position = position;
            BoxCollider collider = obj.AddComponent<BoxCollider>();
            collider.size = Vector3.one * 0.5f;
            collider.isTrigger = trigger;
            return obj;
        }

        private sealed class TestInteractable : MonoBehaviour, IInteractable
        {
            public string InteractionPrompt => "Test";

            public bool CanInteract(in InteractionContext context) => true;

            public void Interact(in InteractionContext context)
            {
            }
        }
    }
}
