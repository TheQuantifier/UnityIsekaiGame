using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace UnityIsekaiGame.Networking.Server
{
    public readonly struct ServerPersistenceCaptureSample
    {
        public ServerPersistenceCaptureSample(int frame, string participantKey, double elapsedMilliseconds, long allocatedBytes = 0L)
        {
            Frame = Math.Max(1, frame);
            ParticipantKey = participantKey ?? string.Empty;
            ElapsedMilliseconds = Math.Max(0d, elapsedMilliseconds);
            AllocatedBytes = Math.Max(0L, allocatedBytes);
        }

        public int Frame { get; }
        public string ParticipantKey { get; }
        public double ElapsedMilliseconds { get; }
        public long AllocatedBytes { get; }
    }

    public readonly struct ServerPersistenceCaptureSummary
    {
        public ServerPersistenceCaptureSummary(
            int count,
            double totalMilliseconds,
            double meanMilliseconds,
            double medianMilliseconds,
            double modeBucketMilliseconds,
            double percentile95Milliseconds,
            double percentile99Milliseconds,
            double maximumMilliseconds,
            string maximumParticipantKey,
            long totalAllocatedBytes,
            long maximumAllocatedBytes,
            string maximumAllocationParticipantKey)
        {
            Count = count;
            TotalMilliseconds = totalMilliseconds;
            MeanMilliseconds = meanMilliseconds;
            MedianMilliseconds = medianMilliseconds;
            ModeBucketMilliseconds = modeBucketMilliseconds;
            Percentile95Milliseconds = percentile95Milliseconds;
            Percentile99Milliseconds = percentile99Milliseconds;
            MaximumMilliseconds = maximumMilliseconds;
            MaximumParticipantKey = maximumParticipantKey ?? string.Empty;
            TotalAllocatedBytes = Math.Max(0L, totalAllocatedBytes);
            MaximumAllocatedBytes = Math.Max(0L, maximumAllocatedBytes);
            MaximumAllocationParticipantKey = maximumAllocationParticipantKey ?? string.Empty;
        }

        public int Count { get; }
        public double TotalMilliseconds { get; }
        public double MeanMilliseconds { get; }
        public double MedianMilliseconds { get; }
        public double ModeBucketMilliseconds { get; }
        public double Percentile95Milliseconds { get; }
        public double Percentile99Milliseconds { get; }
        public double MaximumMilliseconds { get; }
        public string MaximumParticipantKey { get; }
        public long TotalAllocatedBytes { get; }
        public long MaximumAllocatedBytes { get; }
        public string MaximumAllocationParticipantKey { get; }
    }

    public static class ServerPersistenceCaptureMetrics
    {
        private const double ModeBucketWidthMilliseconds = 0.1d;

        public static ServerPersistenceCaptureSummary Summarize(
            IReadOnlyList<ServerPersistenceCaptureSample> samples)
        {
            if (samples == null || samples.Count == 0)
            {
                return new ServerPersistenceCaptureSummary(0, 0d, 0d, 0d, 0d, 0d, 0d, 0d, string.Empty, 0L, 0L, string.Empty);
            }

            double[] ordered = samples
                .Select(sample => sample.ElapsedMilliseconds)
                .OrderBy(value => value)
                .ToArray();
            double total = ordered.Sum();
            double median = ordered.Length % 2 == 0
                ? (ordered[ordered.Length / 2 - 1] + ordered[ordered.Length / 2]) * 0.5d
                : ordered[ordered.Length / 2];
            double maximum = ordered[ordered.Length - 1];
            ServerPersistenceCaptureSample maximumSample = samples
                .OrderByDescending(sample => sample.ElapsedMilliseconds)
                .First();
            ServerPersistenceCaptureSample maximumAllocationSample = samples
                .OrderByDescending(sample => sample.AllocatedBytes)
                .First();
            double mode = ordered
                .Select(ToModeBucket)
                .GroupBy(value => value)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key)
                .First()
                .Key;

            return new ServerPersistenceCaptureSummary(
                ordered.Length,
                total,
                total / ordered.Length,
                median,
                mode,
                Percentile(ordered, 0.95d),
                Percentile(ordered, 0.99d),
                maximum,
                maximumSample.ParticipantKey,
                samples.Sum(sample => sample.AllocatedBytes),
                maximumAllocationSample.AllocatedBytes,
                maximumAllocationSample.ParticipantKey);
        }

        public static string FormatSamples(IReadOnlyList<ServerPersistenceCaptureSample> samples)
        {
            if (samples == null || samples.Count == 0) return string.Empty;
            StringBuilder builder = new StringBuilder(samples.Count * 32);
            for (int i = 0; i < samples.Count; i++)
            {
                if (i > 0) builder.Append(';');
                ServerPersistenceCaptureSample sample = samples[i];
                builder
                    .Append(sample.Frame.ToString(CultureInfo.InvariantCulture))
                    .Append('|')
                    .Append(sample.ParticipantKey.Replace("|", "_").Replace(";", "_"))
                    .Append('|')
                    .Append(sample.ElapsedMilliseconds.ToString("F3", CultureInfo.InvariantCulture))
                    .Append('|')
                    .Append(sample.AllocatedBytes.ToString(CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static double Percentile(IReadOnlyList<double> ordered, double percentile)
        {
            if (ordered == null || ordered.Count == 0) return 0d;
            double index = Math.Clamp(percentile, 0d, 1d) * (ordered.Count - 1);
            int lower = (int)Math.Floor(index);
            int upper = (int)Math.Ceiling(index);
            if (lower == upper) return ordered[lower];
            double fraction = index - lower;
            return ordered[lower] + (ordered[upper] - ordered[lower]) * fraction;
        }

        private static double ToModeBucket(double milliseconds)
        {
            return Math.Round(
                Math.Max(0d, milliseconds) / ModeBucketWidthMilliseconds,
                MidpointRounding.AwayFromZero) * ModeBucketWidthMilliseconds;
        }
    }
}
