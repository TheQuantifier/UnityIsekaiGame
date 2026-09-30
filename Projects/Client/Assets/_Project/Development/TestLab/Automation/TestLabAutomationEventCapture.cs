#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityIsekaiGame.Development.Automation
{
    public sealed class TestLabAutomationEventRecord
    {
        public TestLabAutomationEventRecord(int order, string eventType, float simulationTime, string transactionId, string sourceActorId, string targetActorId, string resultCode)
        {
            Order = order;
            EventType = eventType ?? string.Empty;
            SimulationTime = simulationTime;
            TransactionId = transactionId ?? string.Empty;
            SourceActorId = sourceActorId ?? string.Empty;
            TargetActorId = targetActorId ?? string.Empty;
            ResultCode = resultCode ?? string.Empty;
        }

        public int Order { get; }
        public string EventType { get; }
        public float SimulationTime { get; }
        public string TransactionId { get; }
        public string SourceActorId { get; }
        public string TargetActorId { get; }
        public string ResultCode { get; }
    }

    public sealed class TestLabAutomationEventCapture : IDisposable
    {
        private readonly List<TestLabAutomationEventRecord> records = new List<TestLabAutomationEventRecord>();
        private readonly List<Action> unsubscribers = new List<Action>();
        private int nextOrder;
        private bool disposed;

        public IReadOnlyList<TestLabAutomationEventRecord> Records => records.ToArray();

        public void Record(string eventType, float simulationTime = 0f, string transactionId = "", string sourceActorId = "", string targetActorId = "", string resultCode = "")
        {
            if (disposed)
            {
                return;
            }

            records.Add(new TestLabAutomationEventRecord(++nextOrder, eventType, simulationTime, transactionId, sourceActorId, targetActorId, resultCode));
        }

        public void AddSubscription(Action unsubscribe)
        {
            if (unsubscribe != null && !unsubscribers.Contains(unsubscribe))
            {
                unsubscribers.Add(unsubscribe);
            }
        }

        public void Clear()
        {
            records.Clear();
            nextOrder = 0;
        }

        public bool HasEvent(string eventType)
        {
            return records.Any(record => string.Equals(record.EventType, eventType, StringComparison.Ordinal));
        }

        public bool HasNoEvent(string eventType)
        {
            return !HasEvent(eventType);
        }

        public bool OccurredBefore(string firstEventType, string secondEventType)
        {
            TestLabAutomationEventRecord first = records.FirstOrDefault(record => string.Equals(record.EventType, firstEventType, StringComparison.Ordinal));
            TestLabAutomationEventRecord second = records.FirstOrDefault(record => string.Equals(record.EventType, secondEventType, StringComparison.Ordinal));
            return first != null && second != null && first.Order < second.Order;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            foreach (Action unsubscribe in unsubscribers.ToArray())
            {
                unsubscribe?.Invoke();
            }

            unsubscribers.Clear();
            records.Clear();
        }
    }
}
#endif
