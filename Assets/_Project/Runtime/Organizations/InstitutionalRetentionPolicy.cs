using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityIsekaiGame.Organizations
{
    /// <summary>Keeps idempotency ledgers useful without allowing long-running worlds to create unbounded saves.</summary>
    public static class InstitutionalRetentionPolicy
    {
        public const int MaximumPersistedTransactionsPerRuntime = 8192;

        public static IEnumerable<T> RetainNewestTransactions<T>(this IEnumerable<T> orderedTransactions)
        {
            T[] records = (orderedTransactions ?? Array.Empty<T>()).ToArray();
            int first = Math.Max(0, records.Length - MaximumPersistedTransactionsPerRuntime);
            for (int index = first; index < records.Length; index++) yield return records[index];
        }
    }
}
