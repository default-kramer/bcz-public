using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    sealed class PenaltyManager
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

        public void OnComboCompleted(Combo combo)
        {
            int payout = combo.AdjustedGroupCount;
            if (payout < 2)
            {
                return;
            }

            int removeIndex = -1;
            int removeLevel = -1;

            for (int i = 0; i < index; i++)
            {
                var penalty = penalties[i];
                if (penalty.Kind == PenaltyKind.Levelled && penalty.Level <= payout && penalty.Level > removeLevel)
                {
                    removeIndex = i;
                    removeLevel = penalty.Level;
                }
            }

            if (removeIndex >= 0)
            {
                Remove(removeIndex);
            }
        }

        private void Remove(int target)
        {
            for (int i = target; i < index - 1; i++)
            {
                penalties[i] = penalties[i + 1];
            }
            index--;
        }
    }
}
