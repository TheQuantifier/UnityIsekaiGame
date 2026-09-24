using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityIsekaiGame.Knowledge.History
{
    public sealed class MemoryMaintenanceService
    {
        private readonly PersonMemoryRuntime memory;
        private readonly KnowledgePolicyDefinition policy;
        private double lastMaintenanceWorldTime = -1d;

        public MemoryMaintenanceService(PersonMemoryRuntime memoryRuntime, KnowledgePolicyDefinition knowledgePolicy)
        {
            memory = memoryRuntime ?? throw new ArgumentNullException(nameof(memoryRuntime));
            policy = knowledgePolicy;
        }

        public double LastMaintenanceWorldTime => lastMaintenanceWorldTime;

        public IReadOnlyList<HistoryOperationResult> AdvanceTo(double worldTime, bool restoring = false)
        {
            double now = Math.Max(0d, worldTime);
            double interval = policy == null ? 60d : policy.MemoryMaintenanceIntervalSeconds;
            if (lastMaintenanceWorldTime >= 0d && now - lastMaintenanceWorldTime < interval)
            {
                return Array.Empty<HistoryOperationResult>();
            }

            lastMaintenanceWorldTime = now;
            List<HistoryOperationResult> results = new List<HistoryOperationResult>();
            foreach (HistoryMemoryRecord record in memory.CreateSnapshot().Memories.OrderBy(item => item.MemoryId, StringComparer.Ordinal))
            {
                foreach (MemorySuppressionData suppression in record.Suppressions
                    .Where(item => !item.removed && item.endedAtWorldTime >= 0d && item.endedAtWorldTime <= now)
                    .OrderBy(item => item.suppressionId, StringComparer.Ordinal))
                {
                    results.Add(memory.RemoveSuppression(
                        record.MemoryId,
                        suppression.suppressionId,
                        $"memory-maintenance.suppression.{StableTime(now)}.{record.MemoryId}.{suppression.suppressionId}",
                        now,
                        expireOnly: true,
                        restoring: restoring));
                }

                double from = Math.Max(record.FormedAtWorldTime, record.LastDegradationEvaluatedWorldTime);
                if (now <= from)
                {
                    continue;
                }

                results.Add(memory.ApplyDegradation(new MemoryDegradationRequest
                {
                    TransactionId = $"memory-maintenance.degradation.{StableTime(now)}.{record.MemoryId}",
                    OwnerPersonId = record.OwnerPersonId,
                    MemoryId = record.MemoryId,
                    FromWorldTime = from,
                    ToWorldTime = now,
                    ConfidenceLossPerDay = policy == null ? 1 : policy.MemoryConfidenceLossPerDay,
                    ClarityLossPerDay = policy == null ? 2 : policy.MemoryClarityLossPerDay,
                    SalienceLossPerDay = policy == null ? 0 : policy.MemorySalienceLossPerDay,
                    CreateRevision = true
                }, restoring: restoring));
            }

            return results;
        }

        private static string StableTime(double value)
        {
            return Math.Floor(Math.Max(0d, value)).ToString("0", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
