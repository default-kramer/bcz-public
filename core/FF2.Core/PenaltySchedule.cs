using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    readonly struct PenaltySchedule
    {
        /// <summary>
        /// The penalty to be applied when <see cref="TryAdvance(int, out PenaltySchedule)"/> returns true.
        /// </summary>
        public readonly Penalty Penalty;

        /// <summary>
        /// The time at which this penalty should be applied.
        /// </summary>
        private readonly int neededMillis;

        private readonly Func<PenaltySchedule, PenaltySchedule> nextFunc;

        /// <summary>
        /// If enough time has elapsed, return true and the next item.
        /// Otherwise return false.
        /// </summary>
        /// <param name="millis">Total number of milliseconds that have elapsed this game</param>
        public bool TryAdvance(int millis, out PenaltySchedule next)
        {
            if (millis >= neededMillis)
            {
                next = nextFunc(this);
                return true;
            }
            else
            {
                next = this;
                return false;
            }
        }

        private PenaltySchedule(Penalty penalty, int neededMillis, Func<PenaltySchedule, PenaltySchedule> nextFunc)
        {
            this.Penalty = penalty;
            this.neededMillis = neededMillis;
            this.nextFunc = nextFunc;
        }

        public static PenaltySchedule BasicSchedule(int millis)
        {
            Func<PenaltySchedule, PenaltySchedule> next = ps =>
            {
                int level = Math.Min(9, ps.Penalty.Level + 1);
                var penalty = new Penalty(PenaltyKind.Levelled, level);
                return new PenaltySchedule(penalty, ps.neededMillis + millis, ps.nextFunc);
            };

            return new PenaltySchedule(new Penalty(PenaltyKind.Levelled, 2), millis, next);
        }

        private static readonly int[] indexSchedule = new int[] { 0, 0, 1, 0, 1, 2, 0, 1, 2, 3 };

        public static PenaltySchedule BasicIndexedSchedule(int millis)
        {
            int counter = 0;

            Func<PenaltySchedule, PenaltySchedule> next = ps =>
            {
                counter = (counter + 1) % indexSchedule.Length;

                var p = new Penalty(PenaltyKind.Levelled, indexSchedule[counter]);
                // TODO the next line "ps.neededMillis + millis" is tricky. Can this be cleaner?
                // Why am I using such a functional approach anyway?
                var nextPs = new PenaltySchedule(p, ps.neededMillis + millis, ps.nextFunc);
                return nextPs;
            };

            return new PenaltySchedule(new Penalty(PenaltyKind.Levelled, indexSchedule[0]), millis, next);
        }
    }
}
