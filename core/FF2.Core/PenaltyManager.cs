using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    public sealed class PenaltyManager
    {
        private readonly Penalty[] penalties;
        private int index;
        internal readonly PayoutTable CorruptionAccelerationPayoutTable;

        public PenaltyManager(int capacity = 10)
        {
            penalties = new Penalty[capacity];
            index = 0;
            CorruptionAccelerationPayoutTable = PayoutTable.DefaultCorruptionAccelerationPayoutTable;
        }

        public int Count { get { return index; } }
        public int Capacity { get { return penalties.Length; } }

        public Penalty this[int i]
        {
            get
            {
                if (i >= index)
                {
                    throw new IndexOutOfRangeException($"{i} exceeds {index}");
                }
                return penalties[i];
            }
        }

        public void Add(Penalty penalty)
        {
            if (index < penalties.Length)
            {
                penalties[index] = penalty;
                index++;
                //Console.WriteLine($"Added penalty: {penalty}");
            }
        }
    }
}
