using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    sealed class PenaltySchedule
    {
        private readonly Penalty[] penalties;
        private readonly int millisPerPenalty;

        private int nextPenaltyTime;
        private int index;

        private PenaltySchedule(Penalty[] penalties, int millisPerPenalty)
        {
            this.penalties = penalties;
            this.millisPerPenalty = millisPerPenalty;
            nextPenaltyTime = millisPerPenalty;
            index = 0;
        }

        /// <summary>
        /// If enough time has elapsed, return true and the Penalty to be applied.
        /// Otherwise return false.
        /// </summary>
        /// <param name="totalMillis">Total number of milliseconds that have elapsed this game</param>
        public bool TryAdvance(int totalMillis, out Penalty penalty)
        {
            if (totalMillis < nextPenaltyTime)
            {
                penalty = default(Penalty);
                return false;
            }

            nextPenaltyTime = nextPenaltyTime + millisPerPenalty;
            penalty = penalties[index];
            index = (index + 1) % penalties.Length;
            return true;
        }

        private static readonly Penalty[] todo = new Penalty[]
        {
            new Penalty(PenaltyKind.Levelled, 0),
            new Penalty(PenaltyKind.Levelled, 1),
            new Penalty(PenaltyKind.Levelled, 2),
            new Penalty(PenaltyKind.Levelled, 0),
            new Penalty(PenaltyKind.Levelled, 1),
            new Penalty(PenaltyKind.Levelled, 3),
        };

        public static PenaltySchedule BasicIndexedSchedule(int millisPerPenalty)
        {
            return new PenaltySchedule(todo, millisPerPenalty);
        }
    }
}
