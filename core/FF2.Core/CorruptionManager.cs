using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    readonly struct CorruptionManager
    {
        private readonly int corruption;
        private readonly int corruptionRate; // how much should corruption increase per millisecond
        private const int maxCorruption = 100 * 1000 * 1000;
        private readonly PayoutTable payoutTable;

        public CorruptionManager()
        {
            corruption = 0;
            corruptionRate = 1000;
            this.payoutTable = PayoutTable.DefaultCorruptionPayoutTable;
        }

        private CorruptionManager(CorruptionManager current, int newCorruption)
        {
            this.corruption = newCorruption;
            this.corruptionRate = current.corruptionRate;
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
    }
}
