using System;
using System.IO;
using NUnit.Framework;
using UnityIsekaiGame.Networking;
using UnityIsekaiGame.Networking.Server;

namespace UnityIsekaiGame.ServerProject.Tests
{
    public sealed class ServerAccountStoreTests
    {
        private string temporaryRoot;

        [SetUp]
        public void SetUp()
        {
            temporaryRoot = Path.Combine(Path.GetTempPath(), "UnityIsekaiGame-AccountTests-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrWhiteSpace(temporaryRoot) && Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, true);
            }
        }

        [Test]
        public void Create_then_login_uses_the_same_canonical_player_identity()
        {
            var store = new ServerAccountStore(temporaryRoot);

            ServerAccountAuthenticationResult created = store.Authenticate(Request("Test.Player", "Correct-horse-42", AccountAuthenticationMode.CreateAccount));
            ServerAccountAuthenticationResult authenticated = store.Authenticate(Request("test.player", "Correct-horse-42", AccountAuthenticationMode.Login));

            Assert.That(created.Status, Is.EqualTo(ServerAccountAuthenticationStatus.Created), created.Message);
            Assert.That(authenticated.Status, Is.EqualTo(ServerAccountAuthenticationStatus.Authenticated), authenticated.Message);
            Assert.That(created.Username, Is.EqualTo("test.player"));
            Assert.That(created.UserId, Has.Length.EqualTo(AccountAuthenticationProtocol.SecureUserIdLength));
            Assert.That(created.UserId, Is.Not.EqualTo(created.Username));
            Assert.That(authenticated.UserId, Is.EqualTo(created.UserId));

            ServerAccountAuthenticationResult byId = store.Authenticate(Request(created.UserId, "Correct-horse-42", AccountAuthenticationMode.Login));
            Assert.That(byId.Succeeded, Is.True, byId.Message);
            Assert.That(byId.Username, Is.EqualTo("test.player"));
        }

        [Test]
        public void Duplicate_creation_and_wrong_password_are_rejected()
        {
            var store = new ServerAccountStore(temporaryRoot);
            Assert.That(store.Authenticate(Request("player.one", "Correct-horse-42", AccountAuthenticationMode.CreateAccount)).Succeeded, Is.True);

            ServerAccountAuthenticationResult duplicate = store.Authenticate(Request("PLAYER.ONE", "Different-pass-77", AccountAuthenticationMode.CreateAccount));
            ServerAccountAuthenticationResult wrongPassword = store.Authenticate(Request("player.one", "Incorrect-pass-99", AccountAuthenticationMode.Login));

            Assert.That(duplicate.Succeeded, Is.False);
            Assert.That(duplicate.Message, Does.Contain("already registered"));
            Assert.That(wrongPassword.Succeeded, Is.False);
            Assert.That(wrongPassword.Message, Does.Contain("incorrect"));
        }

        [Test]
        public void Stored_account_never_contains_the_plaintext_password()
        {
            const string password = "Never-store-this-42";
            var store = new ServerAccountStore(temporaryRoot);
            Assert.That(store.Authenticate(Request("private.player", password, AccountAuthenticationMode.CreateAccount)).Succeeded, Is.True);

            string[] accountFiles = Directory.GetFiles(store.AccountDirectory, "*.json", SearchOption.TopDirectoryOnly);
            Assert.That(accountFiles, Has.Length.EqualTo(1));
            string contents = File.ReadAllText(accountFiles[0]);
            Assert.That(contents, Does.Not.Contain(password));
            Assert.That(contents, Does.Contain("saltBase64"));
            Assert.That(contents, Does.Contain("passwordHashBase64"));
        }

        [Test]
        public void Distinct_accounts_receive_distinct_opaque_user_ids()
        {
            var store = new ServerAccountStore(temporaryRoot);
            ServerAccountAuthenticationResult first = store.Authenticate(Request("first.player", "Correct-horse-42", AccountAuthenticationMode.CreateAccount));
            ServerAccountAuthenticationResult second = store.Authenticate(Request("second.player", "Correct-horse-43", AccountAuthenticationMode.CreateAccount));

            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(second.Succeeded, Is.True, second.Message);
            Assert.That(AccountAuthenticationProtocol.IsSecureUserId(first.UserId), Is.True);
            Assert.That(AccountAuthenticationProtocol.IsSecureUserId(second.UserId), Is.True);
            Assert.That(second.UserId, Is.Not.EqualTo(first.UserId));
        }

        private static AccountAuthenticationRequest Request(
            string playerId,
            string password,
            AccountAuthenticationMode mode) =>
            new AccountAuthenticationRequest(playerId, password, mode);
    }
}
