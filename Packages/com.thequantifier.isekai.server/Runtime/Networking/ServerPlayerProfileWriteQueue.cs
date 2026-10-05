using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace UnityIsekaiGame.Networking.Server
{
    public readonly struct ServerPlayerProfileWriteResult
    {
        public ServerPlayerProfileWriteResult(bool succeeded, string message)
        {
            Succeeded = succeeded;
            Message = message ?? string.Empty;
        }

        public bool Succeeded { get; }
        public string Message { get; }
    }

    /// <summary>
    /// Copies profile DTOs on the Unity thread, then coalesces, serializes, and writes those immutable
    /// snapshots on one background worker. This keeps serialization and disk latency out of authoritative
    /// commands while preserving ordered, atomic writes per player.
    /// </summary>
    public sealed class ServerPlayerProfileWriteQueue : IDisposable
    {
        private readonly ServerPlayerProfileStore store;
        private readonly object gate = new object();
        private readonly Dictionary<string, ServerPlayerProfileData> pending =
            new Dictionary<string, ServerPlayerProfileData>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentQueue<ServerPlayerProfileWriteResult> results =
            new ConcurrentQueue<ServerPlayerProfileWriteResult>();
        private readonly AutoResetEvent signal = new AutoResetEvent(false);
        private readonly Thread worker;
        private bool stopping;
        private int activeWrites;

        public ServerPlayerProfileWriteQueue(ServerPlayerProfileStore store)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            worker = new Thread(WorkLoop)
            {
                IsBackground = true,
                Name = "Isekai Server Profile Writer"
            };
            worker.Start();
        }

        public int PendingCount
        {
            get
            {
                lock (gate) return pending.Count + activeWrites;
            }
        }

        public bool TryEnqueue(ServerPlayerProfileData profile, out string message)
        {
            if (profile == null || string.IsNullOrWhiteSpace(profile.playerId))
            {
                message = "A valid server profile is required.";
                return false;
            }

            ServerPlayerProfileData snapshot = profile.Clone();

            lock (gate)
            {
                if (stopping)
                {
                    message = "The server profile writer is shutting down.";
                    return false;
                }

                if (!pending.TryGetValue(snapshot.playerId, out ServerPlayerProfileData existing)
                    || snapshot.revision >= existing.revision)
                {
                    pending[snapshot.playerId] = snapshot;
                }
            }

            signal.Set();
            message = $"Queued server profile revision {snapshot.revision} for '{snapshot.playerId}'.";
            return true;
        }

        public bool TryDequeueResult(out ServerPlayerProfileWriteResult result) => results.TryDequeue(out result);

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
            Flush(TimeSpan.FromSeconds(5));
            lock (gate) stopping = true;
            signal.Set();
            worker.Join(TimeSpan.FromSeconds(2));
            signal.Dispose();
        }

        private void WorkLoop()
        {
            while (true)
            {
                ServerPlayerProfileData profile = null;
                lock (gate)
                {
                    if (pending.Count > 0)
                    {
                        profile = pending.Values.OrderBy(value => value.revision).First();
                        pending.Remove(profile.playerId);
                        activeWrites++;
                    }
                    else if (stopping)
                    {
                        return;
                    }
                }

                if (profile == null)
                {
                    signal.WaitOne();
                    continue;
                }

                bool succeeded = store.TryPrepareWrite(profile, out ServerPlayerProfileWriteRequest request, out string message)
                    && store.TryWrite(request, out message);
                results.Enqueue(new ServerPlayerProfileWriteResult(succeeded, message));
                lock (gate) activeWrites--;
            }
        }
    }
}
