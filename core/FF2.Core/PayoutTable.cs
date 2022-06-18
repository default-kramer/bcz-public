using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    readonly struct PayoutTable
    {
        private readonly int[] payouts;

        public PayoutTable(params int[] payouts)
        {
            this.payouts = payouts;
        }

        private PayoutTable(int baseValue, params decimal[] decimals)
        {
            payouts = new int[decimals.Length];
            for (int i = 0; i < payouts.Length; i++)
            {
                payouts[i] = Convert.ToInt32(decimals[i] * baseValue);
            }
        }

        public int GetPayout(int N)
        {
            if (N < payouts.Length)
            {
                return payouts[N];
            }
            else
            {
                int lastIndex = payouts.Length - 1;
                int lastValue = payouts[lastIndex];
                int step = lastValue - payouts[lastIndex - 1];
                return lastValue + step * (N - lastIndex);
            }
        }

        public static readonly PayoutTable DefaultCorruptionPayoutTable
            = new PayoutTable(2000 * 1000, 0m, 0m, 1m, 3m, 6m, 10m, 15m);

        public static readonly PayoutTable DefaultCorruptionAccelerationPayoutTable
            = new PayoutTable(CorruptionManager.CorruptionAccelerationDenominator, 1m, 1.2m, 1.4m, 1.7m, 2m, 2.5m);
    }
}
