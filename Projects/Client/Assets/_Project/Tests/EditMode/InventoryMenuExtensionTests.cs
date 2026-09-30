using NUnit.Framework;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityIsekaiGame.UI.Inventory;

namespace UnityIsekaiGame.Tests
{
    public sealed class InventoryMenuExtensionTests
    {
        [Test]
        public void MenuInitializesWithoutRegisteredExtensions()
        {
            GameObject owner = new GameObject("Inventory Menu Extension Empty Test");
            try
            {
                InventoryScreenView view = owner.AddComponent<InventoryScreenView>();

                Assert.DoesNotThrow(() => view.Initialize(null, null));
                Assert.DoesNotThrow(() => view.RefreshMenuExtensions());
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void DuplicateExtensionRegistrationIsIgnored()
        {
            GameObject owner = new GameObject("Inventory Menu Extension Duplicate Test");
            try
            {
                InventoryScreenView view = owner.AddComponent<InventoryScreenView>();
                FakeExtension extension = new FakeExtension("test.extension");

                Assert.That(view.RegisterMenuExtension(extension), Is.True);
                Assert.That(view.RegisterMenuExtension(extension), Is.False);
                Assert.That(extension.InitializeCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void ExtensionRefreshAndUnregisterAreStable()
        {
            GameObject owner = new GameObject("Inventory Menu Extension Refresh Test");
            try
            {
                InventoryScreenView view = owner.AddComponent<InventoryScreenView>();
                FakeExtension extension = new FakeExtension("test.extension");

                Assert.That(view.RegisterMenuExtension(extension), Is.True);
                view.RefreshMenuExtensions();

                Assert.That(extension.RefreshCount, Is.EqualTo(1));
                Assert.That(view.UnregisterMenuExtension(extension), Is.True);
                Assert.That(extension.DisposeCount, Is.EqualTo(1));
                Assert.That(view.UnregisterMenuExtension(extension), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void SelectingExtensionRefreshesOnceAndRepeatedSelectionDoesNoWork()
        {
            GameObject owner = new GameObject("Inventory Menu Extension Selection Test");
            try
            {
                InventoryScreenView view = owner.AddComponent<InventoryScreenView>();
                FakeExtension extension = new FakeExtension("test.extension");
                Assert.That(view.RegisterMenuExtension(extension), Is.True);

                Button extensionButton = owner.GetComponentsInChildren<Button>(true)
                    .Single(button => button.name == "Test Menu Button");
                extensionButton.onClick.Invoke();

                Assert.That(extension.ShowCount, Is.EqualTo(1));
                Assert.That(extension.RefreshCount, Is.EqualTo(1));

                extensionButton.onClick.Invoke();

                Assert.That(extension.ShowCount, Is.EqualTo(1));
                Assert.That(extension.RefreshCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void ActiveExtensionRefreshDoesNotRefreshHiddenExtensions()
        {
            GameObject owner = new GameObject("Inventory Menu Active Extension Refresh Test");
            try
            {
                InventoryScreenView view = owner.AddComponent<InventoryScreenView>();
                FakeExtension active = new FakeExtension("test.active", "Active");
                FakeExtension hidden = new FakeExtension("test.hidden", "Hidden");
                Assert.That(view.RegisterMenuExtension(active), Is.True);
                Assert.That(view.RegisterMenuExtension(hidden), Is.True);

                Button activeButton = owner.GetComponentsInChildren<Button>(true)
                    .Single(button => button.name == "Active Menu Button");
                activeButton.onClick.Invoke();
                int activeRefreshes = active.RefreshCount;

                view.RefreshActiveMenuExtension();

                Assert.That(active.RefreshCount, Is.EqualTo(activeRefreshes + 1));
                Assert.That(hidden.RefreshCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        private sealed class FakeExtension : IInventoryMenuExtension
        {
            public FakeExtension(string extensionId, string displayName = "Test")
            {
                ExtensionId = extensionId;
                DisplayName = displayName;
            }

            public string ExtensionId { get; }
            public string DisplayName { get; }
            public int Order => 10;
            public bool IsAvailable => true;
            public bool SuppressFeedbackText => true;
            public int InitializeCount { get; private set; }
            public int RefreshCount { get; private set; }
            public int DisposeCount { get; private set; }
            public int ShowCount { get; private set; }

            public void Initialize(InventoryMenuExtensionContext context)
            {
                InitializeCount++;
                Assert.That(context, Is.Not.Null);
                Assert.That(context.ContentRoot, Is.Not.Null);
            }

            public void Refresh()
            {
                RefreshCount++;
            }

            public void Show()
            {
                ShowCount++;
            }

            public void Hide()
            {
            }

            public void Dispose()
            {
                DisposeCount++;
            }
        }
    }
}
