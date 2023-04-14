using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BCZ.Core
{
    readonly struct CorruptionManager
    {
        private readonly int corruption;
        private readonly int baseCorruptionRate; // how much should corruption increase per millisecond
        private readonly int corruptionRate;
        private const int maxCorruption = 100 * 1000 * 1000;
        private readonly PayoutTable payoutTable;

        public CorruptionManager()
        {
            corruption = 0;
            baseCorruptionRate = 1000;
            corruptionRate = baseCorruptionRate;
            this.payoutTable = PayoutTable.DefaultCorruptionPayoutTable;
        }

        private CorruptionManager(CorruptionManager current, int newCorruption)
        {
            this.corruption = newCorruption;
            this.baseCorruptionRate = current.baseCorruptionRate;
            this.corruptionRate = current.corruptionRate;
            this.payoutTable = current.payoutTable;
        }

        private CorruptionManager(CorruptionManager current, int newCorruption, int newCorruptionRate)
        {
            this.corruption = newCorruption;
            this.baseCorruptionRate = current.baseCorruptionRate;
            this.corruptionRate = newCorruptionRate;
            this.payoutTable = current.payoutTable;
        }

        public CorruptionManager OnComboCompleted(Combo combo)
        {
            int payout = payoutTable.GetPayout(combo.AdjustedGroupCount);
            return new CorruptionManager(this, Math.Max(0, corruption - payout));
        }

        public CorruptionManager Elapse(int millis)
        {
            return new CorruptionManager(this, corruption + corruptionRate * millis);
        }

        public decimal Progress { get { return Convert.ToDecimal(corruption) / maxCorruption; } }

        /// <summary>
        /// Returns milliseconds until corruption reaches max (assuming penalties don't change).
        /// </summary>
        public int RemainingMillis { get { return (maxCorruption - corruption) / corruptionRate; } }

        public CorruptionManager OnPenaltiesChanged(PenaltyManager penalties)
        {
            int numerator = penalties.CorruptionAccelerationPayoutTable.GetPayout(penalties.Count);
            int newRate = baseCorruptionRate * numerator / CorruptionAccelerationDenominator;
            //Console.WriteLine($"New Corruption Rate: {newRate} ({corruptionRate} : {numerator} : {penalties.Count})");
            return new CorruptionManager(this, this.corruption, newRate);
        }

        public static readonly int CorruptionAccelerationDenominator = 100;
    }
}
