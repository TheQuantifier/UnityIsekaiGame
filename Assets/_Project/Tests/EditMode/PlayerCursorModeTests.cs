using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Input;

namespace UnityIsekaiGame.Tests.EditMode
{
    public sealed class PlayerCursorModeTests
    {
        [Test]
        public void MenuOwnershipKeepsMouseLookDisabledUntilEveryMenuCloses()
        {
            GameObject firstMenu = new GameObject("First Menu");
            GameObject secondMenu = new GameObject("Second Menu");
            try
            {
                PlayerCursorMode.SetMouseLookEnabled(true);
                PlayerCursorMode.SetMenuOpen(firstMenu, true);
                PlayerCursorMode.SetMenuOpen(secondMenu, true);

                Assert.That(PlayerCursorMode.HasOpenMenu, Is.True);
                Assert.That(PlayerCursorMode.IsMouseLookActive, Is.False);

                PlayerCursorMode.SetMenuOpen(firstMenu, false);
                Assert.That(PlayerCursorMode.HasOpenMenu, Is.True);
                Assert.That(PlayerCursorMode.IsMouseLookActive, Is.False);

                PlayerCursorMode.SetMenuOpen(secondMenu, false);
                Assert.That(PlayerCursorMode.HasOpenMenu, Is.False);
                Assert.That(PlayerCursorMode.IsMouseLookActive, Is.True);
            }
            finally
            {
                PlayerCursorMode.SetMenuOpen(firstMenu, false);
                PlayerCursorMode.SetMenuOpen(secondMenu, false);
                Object.DestroyImmediate(firstMenu);
                Object.DestroyImmediate(secondMenu);
                PlayerCursorMode.SetMouseLookEnabled(true);
            }
        }

        [Test]
        public void UnlockAndToggleControlWhetherMouseCanDriveCameraLook()
        {
            PlayerCursorMode.SetMouseLookEnabled(true);
            Assert.That(PlayerCursorMode.IsMouseLookActive, Is.True);

            PlayerCursorMode.UnlockMouse();
            Assert.That(PlayerCursorMode.MouseLookEnabled, Is.False);
            Assert.That(PlayerCursorMode.IsMouseLookActive, Is.False);

            PlayerCursorMode.ToggleMouseLook();
            Assert.That(PlayerCursorMode.MouseLookEnabled, Is.True);
            Assert.That(PlayerCursorMode.IsMouseLookActive, Is.True);
        }

        [Test]
        public void TextChatOpenStateBlocksMouseLookUntilChatCloses()
        {
            GameObject host = new GameObject("Text Chat Host");
            PrototypeTextChatPanel chat = host.AddComponent<PrototypeTextChatPanel>();
            try
            {
                PlayerCursorMode.SetMouseLookEnabled(true);

                chat.SetOpen(true);
                Assert.That(PrototypeTextChatPanel.IsChatOpen, Is.True);
                Assert.That(PlayerCursorMode.HasOpenMenu, Is.True);
                Assert.That(PlayerCursorMode.IsMouseLookActive, Is.False);

                chat.SetOpen(false);
                Assert.That(PrototypeTextChatPanel.IsChatOpen, Is.False);
                Assert.That(PlayerCursorMode.HasOpenMenu, Is.False);
                Assert.That(PlayerCursorMode.IsMouseLookActive, Is.True);
            }
            finally
            {
                chat.SetOpen(false);
                Object.DestroyImmediate(host);
                PlayerCursorMode.SetMouseLookEnabled(true);
            }
        }
    }
}
