using System;
using UnityEngine;
using UnityEngine.Profiling;

namespace UnityIsekaiGame.Networking.Server
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(32000)]
    public sealed class ServerFrameStallMonitor : MonoBehaviour
    {
        public const double StallThresholdMilliseconds = 30d;

        private double previousLateUpdateAt;
        private int previousGenerationZeroCollections;
        private int previousGenerationOneCollections;
        private int previousGenerationTwoCollections;
        private long previousMonoUsedBytes;
        private long peakFrameHeapGrowthBytes;
        private long intervalHeapGrowthBytes;
        private int intervalFrameCount;
        private bool traceEnabled;

        private void OnEnable()
        {
            SetTraceEnabled(NetworkMovementTrace.IsRequested());
        }

        private void SetTraceEnabled(bool enabled)
        {
            traceEnabled = enabled;
            NetworkMovementTrace.ServerFrameTimingEnabled = enabled;
            // The component is enabled before the dedicated server finishes loading its
            // authoritative scene. Establish the baseline on the first LateUpdate so startup
            // loading time is not presented as a gameplay frame stall.
            previousLateUpdateAt = 0d;
            previousGenerationZeroCollections = GC.CollectionCount(0);
            previousGenerationOneCollections = GC.CollectionCount(1);
            previousGenerationTwoCollections = GC.CollectionCount(2);
            previousMonoUsedBytes = Profiler.GetMonoUsedSizeLong();
            peakFrameHeapGrowthBytes = 0L;
            intervalHeapGrowthBytes = 0L;
            intervalFrameCount = 0;
        }

        private void OnDisable()
        {
            if (traceEnabled) NetworkMovementTrace.ServerFrameTimingEnabled = false;
            traceEnabled = false;
        }

        private void LateUpdate()
        {
            bool requested = NetworkMovementTrace.IsRequested();
            if (requested != traceEnabled) SetTraceEnabled(requested);
            if (!traceEnabled) return;

            double now = Time.realtimeSinceStartupAsDouble;
            if (previousLateUpdateAt <= 0d)
            {
                previousLateUpdateAt = now;
                previousGenerationZeroCollections = GC.CollectionCount(0);
                previousGenerationOneCollections = GC.CollectionCount(1);
                previousGenerationTwoCollections = GC.CollectionCount(2);
                return;
            }
            double frameGapMilliseconds = (now - previousLateUpdateAt) * 1000d;
            previousLateUpdateAt = now;

            long currentMonoUsedBytes = Profiler.GetMonoUsedSizeLong();
            long frameHeapGrowthBytes = Math.Max(0L, currentMonoUsedBytes - previousMonoUsedBytes);
            previousMonoUsedBytes = currentMonoUsedBytes;
            intervalHeapGrowthBytes += frameHeapGrowthBytes;
            intervalFrameCount++;
            if (frameHeapGrowthBytes > peakFrameHeapGrowthBytes) peakFrameHeapGrowthBytes = frameHeapGrowthBytes;

            int generationZeroCollections = GC.CollectionCount(0);
            int generationOneCollections = GC.CollectionCount(1);
            int generationTwoCollections = GC.CollectionCount(2);
            int generationZeroDelta = generationZeroCollections - previousGenerationZeroCollections;
            int generationOneDelta = generationOneCollections - previousGenerationOneCollections;
            int generationTwoDelta = generationTwoCollections - previousGenerationTwoCollections;
            previousGenerationZeroCollections = generationZeroCollections;
            previousGenerationOneCollections = generationOneCollections;
            previousGenerationTwoCollections = generationTwoCollections;

            if (frameGapMilliseconds < StallThresholdMilliseconds) return;

            long averageFrameHeapGrowthBytes = intervalFrameCount > 0
                ? intervalHeapGrowthBytes / intervalFrameCount
                : 0L;

            int frame = Time.frameCount;
            ServerPhaseSnapshot phases = NetworkMovementTrace.GetServerPhaseSnapshot(frame);
            ServerMovementReceiveBatch inputs = NetworkMovementTrace.GetServerReceiveBatch(frame);
            string source = phases.LongestPhaseMilliseconds >= StallThresholdMilliseconds * 0.5d
                ? phases.LongestPhase
                : "UnityPlayerLoopOrOS";
            long monoUsedBytes = Profiler.GetMonoUsedSizeLong();
            long managedBytes = GC.GetTotalMemory(false);

            Debug.LogWarning(
                $"[Server Frame Stall] utc={NetworkMovementTrace.TimestampUtc()} frame={frame} " +
                $"frameGapMs={frameGapMilliseconds:F3} unityDeltaMs={Time.unscaledDeltaTime * 1000f:F3} " +
                $"source={source} activePhase={phases.ActivePhase} lastPhase={phases.LastCompletedPhase} " +
                $"longestPhase={phases.LongestPhase} phaseMs={phases.LongestPhaseMilliseconds:F3} " +
                $"gc0={generationZeroDelta} gc1={generationOneDelta} gc2={generationTwoDelta} " +
                $"monoUsedBytes={monoUsedBytes} managedBytes={managedBytes} " +
                $"frameHeapGrowthBytes={frameHeapGrowthBytes} averageFrameHeapGrowthBytes={averageFrameHeapGrowthBytes} " +
                $"peakFrameHeapGrowthBytes={peakFrameHeapGrowthBytes} " +
                $"networkInputs={inputs.Count} reliableInputs={inputs.ReliableCount} " +
                $"firstSeq={inputs.FirstSequence} lastSeq={inputs.LastSequence} sequenceSpan={inputs.SequenceSpan}",
                this);

            peakFrameHeapGrowthBytes = 0L;
            intervalHeapGrowthBytes = 0L;
            intervalFrameCount = 0;
        }
    }
}
