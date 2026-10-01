using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityIsekaiGame.Input;
using UnityIsekaiGame.UI;

namespace UnityIsekaiGame.Tests
{
    public sealed class PauseMenuTests
    {
        [Test]
        public void PauseMenu_IsCompactCenteredAndContainsOnlyRequestedActions()
        {
            PauseMenuFixture fixture = PauseMenuFixture.Create();
            try
            {
                Assert.That(fixture.Menu.WindowRoot, Is.Not.Null);
                Assert.That(fixture.Menu.WindowRoot.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)));
                Assert.That(fixture.Menu.WindowRoot.anchorMax, Is.EqualTo(new Vector2(0.5f, 0.5f)));
                Assert.That(fixture.Menu.WindowRoot.sizeDelta, Is.EqualTo(PauseMenuController.PauseWindowSize));
                Assert.That(fixture.Menu.WindowRoot.sizeDelta.x, Is.LessThanOrEqualTo(480f));
                Assert.That(fixture.Menu.WindowRoot.sizeDelta.y, Is.LessThanOrEqualTo(300f));

                Text[] labels = fixture.Root.GetComponentsInChildren<Text>(true);
                Assert.That(Array.Exists(labels, value => value.text == "You are Paused"), Is.True);
                Assert.That(fixture.Menu.UnpauseButton.GetComponentInChildren<Text>(true).text, Is.EqualTo("Unpause"));
                Assert.That(fixture.Menu.QuitButton.GetComponentInChildren<Text>(true).text, Is.EqualTo("Quit Game"));
                Assert.That(fixture.Menu.UnpauseButton.GetComponent<RectTransform>().sizeDelta, Is.EqualTo(new Vector2(250f, 42f)));
                Assert.That(fixture.Menu.QuitButton.GetComponent<RectTransform>().sizeDelta, Is.EqualTo(new Vector2(250f, 42f)));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void PauseMenu_BlocksOnlyLocalInputAndNeverChangesWorldTimeScale()
        {
            PauseMenuFixture fixture = PauseMenuFixture.Create();
            float previousTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 0.73f;
                fixture.Menu.Open();

                Assert.That(fixture.Menu.IsOpen, Is.True);
                Assert.That(fixture.Input.MenuBlocked, Is.True);
                Assert.That(PlayerCursorMode.TopMenuOwner, Is.SameAs(fixture.Menu));
                Assert.That(Time.timeScale, Is.EqualTo(0.73f), "Opening a client menu must not pause authoritative simulation time.");

                fixture.Menu.Unpause();
                Assert.That(fixture.Menu.IsOpen, Is.False);
                Assert.That(fixture.Input.MenuBlocked, Is.False);
                Assert.That(PlayerCursorMode.HasOpenMenu, Is.False);
                Assert.That(Time.timeScale, Is.EqualTo(0.73f));
            }
            finally
            {
                Time.timeScale = previousTimeScale;
                fixture.Dispose();
            }
        }

        private sealed class PauseMenuFixture : IDisposable
        {
            private PauseMenuFixture(GameObject root, RecordingInputReader input, PauseMenuController menu, EventSystem createdEventSystem)
            {
                Root = root;
                Input = input;
                Menu = menu;
                CreatedEventSystem = createdEventSystem;
            }

            public GameObject Root { get; }
            public RecordingInputReader Input { get; }
            public PauseMenuController Menu { get; }
            private EventSystem CreatedEventSystem { get; }

            public static PauseMenuFixture Create()
            {
                PauseMenuController[] existingMenus = UnityEngine.Object.FindObjectsByType<PauseMenuController>(
                    FindObjectsInactive.Include);
                for (int i = 0; i < existingMenus.Length; i++)
                {
                    if (existingMenus[i] != null) UnityEngine.Object.DestroyImmediate(existingMenus[i].gameObject);
                }

                EventSystem eventSystemBefore = UnityEngine.Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include);
                GameObject inputObject = new GameObject("Pause Menu Test Input");
                RecordingInputReader input = inputObject.AddComponent<RecordingInputReader>();
                GameObject root = new GameObject(
                    "Pause Menu Test Canvas",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(GraphicRaycaster));
                PauseMenuController menu = root.AddComponent<PauseMenuController>();
                EventSystem eventSystemAfter = UnityEngine.Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include);
                return new PauseMenuFixture(root, input, menu, eventSystemBefore == null ? eventSystemAfter : null);
            }

            public void Dispose()
            {
                if (Root != null) UnityEngine.Object.DestroyImmediate(Root);
                if (Input != null) UnityEngine.Object.DestroyImmediate(Input.gameObject);
                if (CreatedEventSystem != null) UnityEngine.Object.DestroyImmediate(CreatedEventSystem.gameObject);
                PlayerCursorMode.SetMouseLookEnabled(true);
            }
        }

        private sealed class RecordingInputReader : PlayerInputReader
        {
            public bool MenuBlocked { get; private set; }

            public override void SetMenuInputBlocked(UnityEngine.Object owner, bool blocked, Action closeRequested = null)
            {
                MenuBlocked = blocked;
                PlayerCursorMode.SetMenuOpen(owner, blocked, closeRequested);
            }
        }
    }
}
