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

        const int factor = 2000 * 1000;

        public static readonly PayoutTable DefaultCorruptionPayoutTable
            = new PayoutTable(0, 0, 1 * factor, 3 * factor, 6 * factor, 10 * factor, 15 * factor);
    }
}
