using NUnit.Framework;
using UnityIsekaiGame.Networking.Server;

namespace UnityIsekaiGame.ServerProject.Tests
{
    public sealed class ServerIdleShutdownPolicyTests
    {
        [Test]
        public void Server_stops_after_two_minutes_without_a_remote_client()
        {
            var policy = new ServerIdleShutdownPolicy();

            Assert.That(policy.Observe(10d, false, ServerIdleShutdownPolicy.DefaultTimeoutSeconds), Is.False);
            Assert.That(policy.Observe(129.999d, false, ServerIdleShutdownPolicy.DefaultTimeoutSeconds), Is.False);
            Assert.That(policy.Observe(130d, false, ServerIdleShutdownPolicy.DefaultTimeoutSeconds), Is.True);
        }

        [Test]
        public void Any_remote_client_cancels_and_later_restarts_the_countdown()
        {
            var policy = new ServerIdleShutdownPolicy();

            Assert.That(policy.Observe(0d, false, 120f), Is.False);
            Assert.That(policy.Observe(119d, true, 120f), Is.False);
            Assert.That(policy.IsCountingDown, Is.False);
            Assert.That(policy.Observe(200d, false, 120f), Is.False);
            Assert.That(policy.Observe(319.999d, false, 120f), Is.False);
            Assert.That(policy.Observe(320d, false, 120f), Is.True);
        }

        [Test]
        public void Zero_timeout_disables_idle_shutdown()
        {
            var policy = new ServerIdleShutdownPolicy();

            Assert.That(policy.Observe(0d, false, 0f), Is.False);
            Assert.That(policy.Observe(1000d, false, 0f), Is.False);
            Assert.That(policy.IsCountingDown, Is.False);
        }
    }
}
