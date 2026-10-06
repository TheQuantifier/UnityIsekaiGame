using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace UnityIsekaiGame.Networking
{
    public static class NetworkMovementTrace
    {
        public const string CommandLineFlag = "--movement-trace";
        public const string ControlFileFlag = "--movement-trace-control";
        public const double SampleIntervalSeconds = 0.2d;
        public const uint DeliveryTimingSampleStride = 6u;

        private const double ControlFilePollSeconds = 0.1d;
        private static readonly string[] ProcessArguments = Environment.GetCommandLineArgs();
        private static readonly bool CommandLineRequested = ContainsFlag(ProcessArguments, CommandLineFlag);
        private static readonly string ControlFilePath = FindOptionValue(ProcessArguments, ControlFileFlag);
        private static long nextControlFileCheck;
        private static bool controlFileRequested;

        private static int receiveBatchFrame = -1;
        private static int receiveBatchCount;
        private static int receiveBatchReliableCount;
        private static uint receiveBatchFirstSequence;
        private static uint receiveBatchLastSequence;
        private static int phaseFrame = -1;
        private static string activeServerPhase = "none";
        private static string lastCompletedServerPhase = "none";
        private static string longestServerPhase = "none";
        private static double longestServerPhaseMilliseconds;

        public static bool ServerFrameTimingEnabled { get; set; }

        public static bool IsRequested(string[] arguments = null)
        {
            if (arguments != null)
            {
                if (ContainsFlag(arguments, CommandLineFlag)) return true;
                string suppliedControlFile = FindOptionValue(arguments, ControlFileFlag);
                return !string.IsNullOrWhiteSpace(suppliedControlFile) && File.Exists(suppliedControlFile);
            }

            if (CommandLineRequested) return true;
            if (string.IsNullOrWhiteSpace(ControlFilePath)) return false;

            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            if (now < nextControlFileCheck) return controlFileRequested;
            nextControlFileCheck = now + (long)(System.Diagnostics.Stopwatch.Frequency * ControlFilePollSeconds);
            controlFileRequested = File.Exists(ControlFilePath);
            return controlFileRequested;
        }

        private static bool ContainsFlag(string[] arguments, string flag) =>
            Array.Exists(arguments, value => string.Equals(value, flag, StringComparison.OrdinalIgnoreCase));

        private static string FindOptionValue(string[] arguments, string option)
        {
            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], option, StringComparison.OrdinalIgnoreCase))
                {
                    return arguments[index + 1];
                }
            }

            return null;
        }

        public static string TimestampUtc() => DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

        public static bool ShouldSampleDeliveryTiming(uint sequence) =>
            sequence != 0u && sequence % DeliveryTimingSampleStride == 0u;

        public static void RecordServerInputReceived(uint sequence, bool reliable)
        {
            if (!ServerFrameTimingEnabled) return;
            int frame = Time.frameCount;
            if (receiveBatchFrame != frame)
            {
                receiveBatchFrame = frame;
                receiveBatchCount = 0;
                receiveBatchReliableCount = 0;
                receiveBatchFirstSequence = sequence;
            }

            receiveBatchCount++;
            if (reliable) receiveBatchReliableCount++;
            receiveBatchLastSequence = sequence;
        }

        public static ServerMovementReceiveBatch GetServerReceiveBatch(int frame)
        {
            return receiveBatchFrame == frame
                ? new ServerMovementReceiveBatch(
                    receiveBatchCount,
                    receiveBatchReliableCount,
                    receiveBatchFirstSequence,
                    receiveBatchLastSequence)
                : default;
        }

        public static uint CalculateSequenceSpan(uint firstSequence, uint lastSequence) =>
            firstSequence == 0u || lastSequence == 0u
                ? 0u
                : unchecked(lastSequence - firstSequence) + 1u;

        public static ServerPhaseScope MeasureServerPhase(string phase)
        {
            if (!ServerFrameTimingEnabled) return default;
            int frame = Time.frameCount;
            PreparePhaseFrame(frame);
            activeServerPhase = phase ?? "unknown";
            return new ServerPhaseScope(activeServerPhase, frame, System.Diagnostics.Stopwatch.GetTimestamp(), true);
        }

        public static ServerPhaseSnapshot GetServerPhaseSnapshot(int frame)
        {
            PreparePhaseFrame(frame);
            return new ServerPhaseSnapshot(
                activeServerPhase,
                lastCompletedServerPhase,
                longestServerPhase,
                longestServerPhaseMilliseconds);
        }

        private static void PreparePhaseFrame(int frame)
        {
            if (phaseFrame == frame) return;
            phaseFrame = frame;
            activeServerPhase = "none";
            lastCompletedServerPhase = "none";
            longestServerPhase = "none";
            longestServerPhaseMilliseconds = 0d;
        }

        private static void CompleteServerPhase(string phase, int frame, long startedAt)
        {
            if (!ServerFrameTimingEnabled || frame != Time.frameCount) return;
            double milliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - startedAt)
                * 1000d
                / System.Diagnostics.Stopwatch.Frequency;
            PreparePhaseFrame(frame);
            lastCompletedServerPhase = phase;
            activeServerPhase = "none";
            if (milliseconds <= longestServerPhaseMilliseconds) return;
            longestServerPhaseMilliseconds = milliseconds;
            longestServerPhase = phase;
        }

        public static string Format(Vector2 value) => string.Format(
            CultureInfo.InvariantCulture,
            "({0:F3},{1:F3})",
            value.x,
            value.y);

        public static string Format(Vector3 value) => string.Format(
            CultureInfo.InvariantCulture,
            "({0:F3},{1:F3},{2:F3})",
            value.x,
            value.y,
            value.z);

        public readonly struct ServerPhaseScope : IDisposable
        {
            private readonly string phase;
            private readonly int frame;
            private readonly long startedAt;
            private readonly bool active;

            internal ServerPhaseScope(string phase, int frame, long startedAt, bool active)
            {
                this.phase = phase;
                this.frame = frame;
                this.startedAt = startedAt;
                this.active = active;
            }

            public void Dispose()
            {
                if (active) CompleteServerPhase(phase, frame, startedAt);
            }
        }
    }

    public readonly struct ServerMovementReceiveBatch
    {
        public ServerMovementReceiveBatch(int count, int reliableCount, uint firstSequence, uint lastSequence)
        {
            Count = count;
            ReliableCount = reliableCount;
            FirstSequence = firstSequence;
            LastSequence = lastSequence;
        }

        public int Count { get; }
        public int ReliableCount { get; }
        public uint FirstSequence { get; }
        public uint LastSequence { get; }
        public uint SequenceSpan => NetworkMovementTrace.CalculateSequenceSpan(FirstSequence, LastSequence);
    }

    public readonly struct ServerPhaseSnapshot
    {
        public ServerPhaseSnapshot(
            string activePhase,
            string lastCompletedPhase,
            string longestPhase,
            double longestPhaseMilliseconds)
        {
            ActivePhase = activePhase;
            LastCompletedPhase = lastCompletedPhase;
            LongestPhase = longestPhase;
            LongestPhaseMilliseconds = longestPhaseMilliseconds;
        }

        public string ActivePhase { get; }
        public string LastCompletedPhase { get; }
        public string LongestPhase { get; }
        public double LongestPhaseMilliseconds { get; }
    }

    public enum NetworkActionTraceCategory : byte
    {
        Movement = 1,
        Interaction = 2,
        UI = 3,
        Inventory = 4,
        Combat = 5,
        Authentication = 6,
        System = 7
    }

    public static class NetworkActionTrace
    {
        public static bool IsEnabled => NetworkMovementTrace.IsRequested();

        public static string Correlation(string actorId, uint sequence)
            => $"{Token(actorId)}:{sequence.ToString(CultureInfo.InvariantCulture)}";

        public static void ClientSend(
            NetworkActionTraceCategory category,
            string action,
            string correlation,
            string delivery = "game-reliable",
            UnityEngine.Object context = null)
        {
            if (!IsEnabled) return;
            Debug.Log(
                $"[Action Timing][ClientSend] utc={NetworkMovementTrace.TimestampUtc()} " +
                $"category={category} action={Token(action)} correlation={Token(correlation)} " +
                $"client=local delivery={Token(delivery)}",
                context);
        }

        public static void ServerReceive(
            NetworkActionTraceCategory category,
            string action,
            string correlation,
            ulong clientId,
            string delivery = "game-reliable",
            UnityEngine.Object context = null)
        {
            if (!IsEnabled) return;
            Debug.Log(
                $"[Action Timing][ServerReceive] utc={NetworkMovementTrace.TimestampUtc()} " +
                $"category={category} action={Token(action)} correlation={Token(correlation)} " +
                $"client={clientId.ToString(CultureInfo.InvariantCulture)} delivery={Token(delivery)}",
                context);
        }

        private static string Token(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "unknown";
            string trimmed = value.Trim();
            var builder = new StringBuilder(Math.Min(trimmed.Length, 96));
            for (int index = 0; index < trimmed.Length && builder.Length < 96; index++)
            {
                char character = trimmed[index];
                builder.Append(char.IsWhiteSpace(character) || character == '=' ? '_' : character);
            }
            return builder.ToString();
        }
    }
}
