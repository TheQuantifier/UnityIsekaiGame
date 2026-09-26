using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityIsekaiGame.Interaction;
using UnityIsekaiGame.WorldLocations.SceneBinding;

namespace UnityIsekaiGame.Tests
{
    public sealed class PrototypeInteractionPlayModeTests
    {
        private const string PrototypeScenePath = "Assets/_Project/Scenes/Prototype/PrototypeScene.unity";
        private readonly List<string> runtimeErrors = new List<string>();
        private static readonly Vector3[] ApproachDirections =
        {
            Vector3.forward,
            Vector3.back,
            Vector3.left,
            Vector3.right
        };

        [SetUp]
        public void SetUp()
        {
            runtimeErrors.Clear();
            LogAssert.ignoreFailingMessages = true;
            Application.logMessageReceived += OnLogMessage;
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= OnLogMessage;
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator PrototypeScene_AllInteractablesHaveReachableFocusAndCorrectRange()
        {
            yield return SceneManager.LoadSceneAsync(PrototypeScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;

            CameraInteractionDetector detector = Object.FindAnyObjectByType<CameraInteractionDetector>();
            Assert.That(detector, Is.Not.Null);
            Assert.That(detector.TriggerInteraction, Is.EqualTo(QueryTriggerInteraction.Collide));
            CaptureCamera(detector.GetComponent<Camera>(), "PrototypeInteractionPlayMode-Spawn.png");

            MonoBehaviour[] interactables = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude)
                .Where(item => item is IInteractable)
                .OrderBy(item => HierarchyPath(item.transform))
                .ToArray();
            Assert.That(interactables.Length, Is.GreaterThanOrEqualTo(28), "The Prototype lost one or more of its established interaction surfaces.");

            RaycastHit counterCaptureHit = default;
            Vector3 counterCaptureDirection = Vector3.zero;
            foreach (MonoBehaviour component in interactables)
            {
                IInteractable interactable = (IInteractable)component;
                Collider[] colliders = FindOwnedColliders(component);
                Assert.That(colliders, Is.Not.Empty, HierarchyPath(component.transform));
                Assert.That(interactable.InteractionPrompt, Is.Not.Empty, HierarchyPath(component.transform));

                int directApproaches = 0;
                bool detectorFocusedTarget = false;
                RaycastHit usableHit = default;
                Vector3 usableDirection = Vector3.zero;
                foreach (Vector3 direction in ApproachDirections)
                {
                    if (!TryRaycastOwnedSurface(colliders, direction, out RaycastHit hit))
                    {
                        continue;
                    }

                    directApproaches++;
                    detector.RayOrigin.position = hit.point + direction * 2.5f;
                    detector.RayOrigin.rotation = Quaternion.LookRotation(-direction, Vector3.up);
                    Physics.SyncTransforms();
                    detector.RefreshTarget();
                    if (ReferenceEquals(detector.CurrentInteractable, interactable))
                    {
                        detectorFocusedTarget = true;
                        usableHit = hit;
                        usableDirection = direction;
                        if (component.name == "AdventurerGuildCounter")
                        {
                            counterCaptureHit = hit;
                            counterCaptureDirection = direction;
                        }
                    }
                }

                Assert.That(directApproaches, Is.GreaterThanOrEqualTo(2), $"{HierarchyPath(component.transform)} is not approachable from multiple directions.");
                Assert.That(detectorFocusedTarget, Is.True, $"{HierarchyPath(component.transform)} cannot receive focus from any clear approach.");

                if (component is InteractionPointSceneBinding point)
                {
                    AssertRangeBoundary(point, interactable, detector, usableHit, usableDirection, point.InteractionRange);
                }
                else if (component is ConnectionSceneBinding connection)
                {
                    AssertRangeBoundary(connection, interactable, detector, usableHit, usableDirection, connection.InteractionRange);
                }

                detector.RayOrigin.position = usableHit.point + usableDirection * 2.5f;
                InteractionContext actionContext = new InteractionContext(detector.Interactor, detector.RayOrigin, usableHit);
                Assert.That(interactable.CanInteract(actionContext), Is.True, $"{HierarchyPath(component.transform)} became unavailable before action testing.");
                interactable.Interact(actionContext);
                if (component is InteractionPointSceneBinding invokedPoint)
                {
                    Assert.That(invokedPoint.LastPoint, Is.Not.Null, $"{HierarchyPath(component.transform)} did not route its interaction action.");
                }
                else if (component is ConnectionSceneBinding invokedConnection)
                {
                    Assert.That(invokedConnection.LastInteractionResult, Is.Not.Null, $"{HierarchyPath(component.transform)} did not issue a connection action.");
                    interactable.Interact(actionContext);
                    Assert.That(invokedConnection.LastInteractionResult.Duplicate, Is.False, $"{HierarchyPath(component.transform)} reused a rapid-input transaction ID.");
                }
            }

            Assert.That(counterCaptureDirection, Is.Not.EqualTo(Vector3.zero));
            detector.RayOrigin.position = counterCaptureHit.point + counterCaptureDirection * 2.5f;
            detector.RayOrigin.rotation = Quaternion.LookRotation(-counterCaptureDirection, Vector3.up);
            Physics.SyncTransforms();
            detector.RefreshTarget();
            yield return null;
            InteractionPromptView promptView = Object.FindAnyObjectByType<InteractionPromptView>(FindObjectsInactive.Include);
            Assert.That(promptView, Is.Not.Null);
            Assert.That(promptView.IsVisible, Is.True);
            IInteractable guildCounter = (IInteractable)interactables.Single(component => component.name == "AdventurerGuildCounter");
            Assert.That(promptView.DisplayedPrompt, Is.EqualTo(guildCounter.InteractionPrompt));
            CaptureCamera(detector.GetComponent<Camera>(), "PrototypeInteractionPlayMode-GuildCounter.png");
            Assert.That(runtimeErrors, Is.Empty, string.Join("\n", runtimeErrors));
        }

        private static void AssertRangeBoundary(
            MonoBehaviour component,
            IInteractable interactable,
            CameraInteractionDetector detector,
            RaycastHit hit,
            Vector3 direction,
            float range)
        {
            Transform origin = detector.RayOrigin;
            origin.position = hit.point + direction * (range - 0.01f);
            InteractionContext inside = new InteractionContext(detector.Interactor, origin, hit);
            Assert.That(interactable.CanInteract(inside), Is.True, $"{HierarchyPath(component.transform)} rejects the inside edge of its range.");

            origin.position = hit.point + direction * (range + 0.01f);
            InteractionContext outside = new InteractionContext(detector.Interactor, origin, hit);
            Assert.That(interactable.CanInteract(outside), Is.False, $"{HierarchyPath(component.transform)} accepts interaction beyond its range.");
        }

        private static bool TryRaycastOwnedSurface(Collider[] colliders, Vector3 direction, out RaycastHit closest)
        {
            closest = default;
            Bounds bounds = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++)
            {
                bounds.Encapsulate(colliders[i].bounds);
            }

            Ray ray = new Ray(bounds.center + direction * (bounds.extents.magnitude + 2f), -direction);
            bool found = false;
            float distance = float.PositiveInfinity;
            foreach (Collider collider in colliders)
            {
                if (collider.enabled && collider.Raycast(ray, out RaycastHit hit, bounds.extents.magnitude + 4f) && hit.distance < distance)
                {
                    closest = hit;
                    distance = hit.distance;
                    found = true;
                }
            }

            return found;
        }

        private static Collider[] FindOwnedColliders(MonoBehaviour interactable)
        {
            return interactable.GetComponentsInChildren<Collider>(true)
                .Where(collider => ReferenceEquals(collider.GetComponentInParent<IInteractable>(), interactable))
                .Where(collider => collider.enabled && collider.gameObject.activeInHierarchy)
                .ToArray();
        }

        private static void CaptureCamera(Camera camera, string fileName)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                return;
            }

            Assert.That(camera, Is.Not.Null);
            const int width = 1280;
            const int height = 720;
            RenderTexture target = new RenderTexture(width, height, 24);
            Texture2D image = new Texture2D(width, height, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = target;
                RenderTexture.active = target;
                camera.Render();
                image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                image.Apply();
                string path = Path.Combine(Directory.GetCurrentDirectory(), "Logs", fileName);
                File.WriteAllBytes(path, image.EncodeToPNG());
                Assert.That(File.Exists(path), Is.True, $"Play Mode capture '{fileName}' was not written.");
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previous;
                Object.Destroy(target);
                Object.Destroy(image);
            }
        }

        private static string HierarchyPath(Transform transform)
        {
            Stack<string> names = new Stack<string>();
            while (transform != null)
            {
                names.Push(transform.name);
                transform = transform.parent;
            }

            return string.Join("/", names);
        }

        private void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                runtimeErrors.Add($"{type}: {condition}\n{stackTrace}");
            }
        }
    }
}
