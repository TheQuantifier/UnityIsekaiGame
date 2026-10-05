using NUnit.Framework;
using UnityIsekaiGame.Networking.Server;

namespace UnityIsekaiGame.ServerProject.Tests
{
    public sealed class AccountAuthenticationTimeoutPolicyTests
    {
        [Test]
        public void Login_screen_connection_remains_available_for_ten_minutes()
        {
            Assert.That(AccountAuthenticationTimeoutPolicy.Evaluate(100d, 0d, false, 699.999d),
                Is.EqualTo(AccountAuthenticationExpiration.None));
            Assert.That(AccountAuthenticationTimeoutPolicy.Evaluate(100d, 0d, false, 700d),
                Is.EqualTo(AccountAuthenticationExpiration.LoginConnectionIdle));
        }

        [Test]
        public void Active_authentication_uses_its_own_short_timeout()
        {
            Assert.That(AccountAuthenticationTimeoutPolicy.Evaluate(0d, 500d, true, 514.999d),
                Is.EqualTo(AccountAuthenticationExpiration.None));
            Assert.That(AccountAuthenticationTimeoutPolicy.Evaluate(0d, 500d, true, 515d),
                Is.EqualTo(AccountAuthenticationExpiration.AuthenticationRequest));
        }
    }
}
