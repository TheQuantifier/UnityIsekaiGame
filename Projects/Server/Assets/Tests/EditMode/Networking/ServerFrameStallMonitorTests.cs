using NUnit.Framework;
using UnityIsekaiGame.Networking;
using UnityIsekaiGame.Networking.Server;

namespace UnityIsekaiGame.ServerProject.Tests
{
    public sealed class ServerFrameStallMonitorTests
    {
        [Test]
        public void Stall_monitor_uses_a_thirty_millisecond_threshold()
        {
            Assert.That(ServerFrameStallMonitor.StallThresholdMilliseconds, Is.EqualTo(30d));
        }

        [Test]
        public void Receive_batch_reports_packet_count_reliable_count_and_sequence_span()
        {
            var batch = new ServerMovementReceiveBatch(5, 2, 100u, 112u);

            Assert.That(batch.Count, Is.EqualTo(5));
            Assert.That(batch.ReliableCount, Is.EqualTo(2));
            Assert.That(batch.SequenceSpan, Is.EqualTo(13u));
        }
    }
}
