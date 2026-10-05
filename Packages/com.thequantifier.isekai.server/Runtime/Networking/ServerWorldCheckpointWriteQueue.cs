using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using UnityIsekaiGame.GameData.Persistence;

namespace UnityIsekaiGame.Networking.Server
{
    public readonly struct ServerWorldCheckpointWriteResult
    {
        public ServerWorldCheckpointWriteResult(PreparedPersistenceSave prepared, PersistenceSaveResult result)
        {
            Prepared = prepared;
            Result = result;
        }

        public PreparedPersistenceSave Prepared { get; }
        public PersistenceSaveResult Result { get; }
    }

    /// <summary>
    /// Writes immutable world snapshots on a single background thread. Runtime participant capture
    /// remains on Unity's simulation thread; checksum construction, envelope serialization, temporary
    /// verification, backup preservation, and atomic promotion never block the authoritative tick.
    /// </summary>
    public sealed class ServerWorldCheckpointWriteQueue : IDisposable
    {
        private readonly PersistenceService service;
        private readonly object gate = new object();
        private readonly ConcurrentQueue<ServerWorldCheckpointWriteResult> results =
            new ConcurrentQueue<ServerWorldCheckpointWriteResult>();
        private readonly AutoResetEvent signal = new AutoResetEvent(false);
        private readonly Thread worker;
        private PreparedPersistenceSave pending;
        private bool stopping;
        private int activeWrites;

        public ServerWorldCheckpointWriteQueue(PersistenceService service)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            worker = new Thread(WorkLoop)
            {
                IsBackground = true,
                Name = "Isekai World Checkpoint Writer"
            };
            worker.Start();
        }

        public int PendingCount
        {
            get
            {
                lock (gate) return (pending == null ? 0 : 1) + activeWrites;
            }
        }

        public bool TryEnqueue(PreparedPersistenceSave prepared, out string message)
        {
            if (prepared == null)
            {
                message = "A prepared world checkpoint is required.";
                return false;
            }

            lock (gate)
            {
                if (stopping)
                {
                    message = "The world checkpoint writer is shutting down.";
                    return false;
                }

                if (pending != null || activeWrites > 0)
                {
                    message = "A world checkpoint write is already pending.";
                    return false;
                }

                pending = prepared;
            }

            signal.Set();
            message = $"Queued world checkpoint transaction {prepared.TransactionId}.";
            return true;
        }

        public bool TryDequeueResult(out ServerWorldCheckpointWriteResult result) => results.TryDequeue(out result);

        public bool Flush(TimeSpan timeout)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            signal.Set();
            while (stopwatch.Elapsed < timeout)
            {
                if (PendingCount == 0) return true;
                Thread.Sleep(5);
            }

            return PendingCount == 0;
        }

        public void Dispose()
        {
            Flush(TimeSpan.FromSeconds(30));
            lock (gate) stopping = true;
            signal.Set();
            worker.Join(TimeSpan.FromSeconds(2));
            signal.Dispose();
        }

        private void WorkLoop()
        {
            while (true)
            {
                PreparedPersistenceSave request = null;
                lock (gate)
                {
                    if (pending != null)
                    {
                        request = pending;
                        pending = null;
                        activeWrites++;
                    }
                    else if (stopping)
                    {
                        return;
                    }
                }

                if (request == null)
                {
                    signal.WaitOne();
                    continue;
                }

                PersistenceSaveResult result = service.WritePreparedSave(request);
                results.Enqueue(new ServerWorldCheckpointWriteResult(request, result));
                lock (gate) activeWrites--;
            }
        }
    }
}
