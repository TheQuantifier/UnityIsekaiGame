using System.Linq;
using NUnit.Framework;
using UnityIsekaiGame.Organizations;

namespace UnityIsekaiGame.Tests
{
    public sealed class InstitutionalRetentionPolicyTests
    {
        [Test]
        public void RetainNewestTransactions_BoundsLedgerAndKeepsTail()
        {
            int sourceCount = InstitutionalRetentionPolicy.MaximumPersistedTransactionsPerRuntime + 137;
            int[] retained = Enumerable.Range(0, sourceCount).RetainNewestTransactions().ToArray();

            Assert.That(retained.Length, Is.EqualTo(InstitutionalRetentionPolicy.MaximumPersistedTransactionsPerRuntime));
            Assert.That(retained[0], Is.EqualTo(137));
            Assert.That(retained[^1], Is.EqualTo(sourceCount - 1));
        }
    }
}
