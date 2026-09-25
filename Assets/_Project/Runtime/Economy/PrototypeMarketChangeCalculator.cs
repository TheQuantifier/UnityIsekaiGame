using System;
using UnityIsekaiGame.Economy.Businesses;

namespace UnityIsekaiGame.Economy
{
    /// <summary>
    /// Produces a small, deterministic aggregate trade plan for each market interval.
    /// The result feels variable during play, while remaining repeatable across save/load
    /// and never requesting more stock than the regional pools can supply.
    /// </summary>
    public static class PrototypeMarketChangeCalculator
    {
        public const int IntervalSeconds = 60;
        public const long RawResourceTarget = 120L;
        public const long LeatherTarget = 40L;
        public const long ExportReserve = 4L;
        public const long ExportStockTarget = 12L;

        public static PrototypeMarketChangePlan Calculate(
            long boundary,
            long ironAvailable,
            long woodAvailable,
            long leatherAvailable,
            long swordsAvailable,
            long bowsAvailable)
        {
            return Calculate(boundary, ironAvailable, woodAvailable, leatherAvailable, swordsAvailable, bowsAvailable,
                new MerchantSimulationPolicyData());
        }

        public static PrototypeMarketChangePlan Calculate(
            long boundary,
            long ironAvailable,
            long woodAvailable,
            long leatherAvailable,
            long swordsAvailable,
            long bowsAvailable,
            MerchantSimulationPolicyData simulationPolicy)
        {
            MerchantSimulationPolicyData policy = (simulationPolicy ?? new MerchantSimulationPolicyData()).Clone();
            ironAvailable = Math.Max(0L, ironAvailable);
            woodAvailable = Math.Max(0L, woodAvailable);
            leatherAvailable = Math.Max(0L, leatherAvailable);
            swordsAvailable = Math.Max(0L, swordsAvailable);
            bowsAvailable = Math.Max(0L, bowsAvailable);

            uint state = unchecked((uint)boundary * 747796405u + 2891336453u);
            long ironImports = Replenishment(ironAvailable, policy.rawResourceTarget, NextInclusive(ref state, 2, 7));
            long woodImports = Replenishment(woodAvailable, policy.rawResourceTarget, NextInclusive(ref state, 1, 5));
            long leatherImports = Replenishment(leatherAvailable, policy.leatherTarget, NextInclusive(ref state, 1, 2));
            long swordExports = Math.Min(AvailableAboveReserve(swordsAvailable, policy.exportReserve), WeightedWeaponDemand(ref state, bows: false));
            long bowExports = Math.Min(AvailableAboveReserve(bowsAvailable, policy.exportReserve), WeightedWeaponDemand(ref state, bows: true));

            long ironDemand = Demand(30L, policy.rawResourceTarget - ironAvailable, ironImports, NextInclusive(ref state, -4, 5));
            long woodDemand = Demand(34L, policy.rawResourceTarget - woodAvailable, woodImports, NextInclusive(ref state, -4, 5));
            long leatherDemand = Demand(16L, policy.leatherTarget - leatherAvailable, leatherImports, NextInclusive(ref state, -2, 3));
            long swordDemand = Demand(25L, policy.exportStockTarget - swordsAvailable, swordExports * 3L, NextInclusive(ref state, -3, 6));
            long bowDemand = Demand(28L, policy.exportStockTarget - bowsAvailable, bowExports * 3L, NextInclusive(ref state, -3, 6));

            return new PrototypeMarketChangePlan(
                boundary,
                ironImports,
                woodImports,
                leatherImports,
                swordExports,
                bowExports,
                ironDemand,
                woodDemand,
                leatherDemand,
                swordDemand,
                bowDemand);
        }

        private static long Replenishment(long available, long target, int shipment)
        {
            long missing = Math.Max(0L, target - available);
            return Math.Min(missing, Math.Max(0, shipment));
        }

        private static long AvailableAboveReserve(long available, long reserve) => Math.Max(0L, available - Math.Max(0L, reserve));

        private static int WeightedWeaponDemand(ref uint state, bool bows)
        {
            int roll = NextInclusive(ref state, 1, 100);
            if (bows)
            {
                if (roll <= 12) return 0;
                if (roll <= 67) return 1;
                if (roll <= 94) return 2;
                return 3;
            }

            if (roll <= 15) return 0;
            if (roll <= 80) return 1;
            if (roll <= 98) return 2;
            return 3;
        }

        private static long Demand(long baseline, long shortage, long activeTrade, int variation)
        {
            long shortagePressure = Math.Clamp(shortage, -30L, 30L) / 3L;
            return Math.Clamp(baseline + shortagePressure + activeTrade + variation, 10L, 80L);
        }

        private static int NextInclusive(ref uint state, int minimum, int maximum)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            uint range = (uint)(maximum - minimum + 1);
            return minimum + (int)(state % range);
        }
    }

    public sealed class PrototypeMarketChangePlan
    {
        public PrototypeMarketChangePlan(
            long boundary,
            long ironImports,
            long woodImports,
            long leatherImports,
            long swordExports,
            long bowExports,
            long ironDemand,
            long woodDemand,
            long leatherDemand,
            long swordDemand,
            long bowDemand)
        {
            Boundary = boundary;
            IronImports = Math.Max(0L, ironImports);
            WoodImports = Math.Max(0L, woodImports);
            LeatherImports = Math.Max(0L, leatherImports);
            SwordExports = Math.Max(0L, swordExports);
            BowExports = Math.Max(0L, bowExports);
            IronDemand = Math.Max(0L, ironDemand);
            WoodDemand = Math.Max(0L, woodDemand);
            LeatherDemand = Math.Max(0L, leatherDemand);
            SwordDemand = Math.Max(0L, swordDemand);
            BowDemand = Math.Max(0L, bowDemand);
        }

        public long Boundary { get; }
        public long IronImports { get; }
        public long WoodImports { get; }
        public long LeatherImports { get; }
        public long SwordExports { get; }
        public long BowExports { get; }
        public long IronDemand { get; }
        public long WoodDemand { get; }
        public long LeatherDemand { get; }
        public long SwordDemand { get; }
        public long BowDemand { get; }
        public long TotalTradeUnits => IronImports + WoodImports + LeatherImports + SwordExports + BowExports;
    }
}
