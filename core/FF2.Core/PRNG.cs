using System;

namespace FF2.Core
{
    /// <summary>
    /// A deterministic pseudorandom number generator.
    /// Starting from the same seed will always produce the same pseudorandom sequence.
    /// </summary>
    /// <remarks>
    /// Based on the Java and C code by Sebastiano Vigna: https://github.com/vigna/MRG32k3a,
    /// which is called "the reference implementation" in this file.
    /// 
    /// Original paper by Pierre L'Ecuyer: https://pubsonline.informs.org/doi/abs/10.1287/opre.47.1.159
    /// </remarks>
    public sealed class PRNG
    {
        const long m1 = 4294967087L;
        const long m2 = 4294944443L;
        const long a12 = 1403580L;
        const long a13 = 810728L;
        const long a21 = 527612L;
        const long a23 = 1370589L;
        const long corr1 = (m1 * a13);
        const long corr2 = (m2 * a23);
        const double norm = 2.328306549295727688e-10;

        private long s10, s11, s12, s20, s21, s22;

        public State GetState()
        {
            return new State(s10, s11, s12, s20, s21, s22);
        }

        public void SetState(State state)
        {
            if (state.s10 == 0 && state.s11 == 0 && state.s12 == 0)
            {
                throw new ArgumentException("s10, s11 and s12 cannot be all zero");
            }
            if (state.s20 == 0 && state.s21 == 0 && state.s22 == 0)
            {
                throw new ArgumentException("s20, s21 and s22 cannot be all zero");
            }
            EnsureNonnegative(state.s20, nameof(state.s20));
            EnsureNonnegative(state.s21, nameof(state.s21));
            EnsureNonnegative(state.s22, nameof(state.s22));
            EnsureLessThan(state.s10, m1, nameof(state.s10));
            EnsureLessThan(state.s11, m1, nameof(state.s11));
            EnsureLessThan(state.s12, m1, nameof(state.s12));
            EnsureLessThan(state.s20, m2, nameof(state.s20));
            EnsureLessThan(state.s21, m2, nameof(state.s21));
            EnsureLessThan(state.s22, m2, nameof(state.s22));

            s10 = state.s10;
            s11 = state.s11;
            s12 = state.s12;
            s20 = state.s20;
            s21 = state.s21;
            s22 = state.s22;

            // Throw away one value to align output with L'Ecuyer original version
            NextDouble();
        }

        private static void EnsureNonnegative(long n, string name)
        {
            if (n < 0)
            {
                throw new ArgumentException(string.Format("{0} must be >= 0 (given: {1})", name, n));
            }
        }

        private static void EnsureLessThan(long n, long limit, string name)
        {
            if (n >= limit)
            {
                throw new ArgumentException(string.Format(
                    "{0} must be smaller than {1} (given: {2})", name, limit, n));
            }
        }

        public PRNG(State state)
        {
            SetState(state);
        }

        private static readonly Random seeder = new Random();
        private static long GetSeed(long limit)
        {
            return (long)(seeder.NextDouble() * (limit - 1));
        }
        public static PRNG Create()
        {
            var state = new State(GetSeed(m1), GetSeed(m1), GetSeed(m1), GetSeed(m2), GetSeed(m2), GetSeed(m2));
            try
            {
                return new PRNG(state);
            }
            catch (ArgumentException)
            {
                // This exception should be ****Extremely**** unlikely (getting s10,s11,s12 all zero),
                // but it is still technically possible
                return Create();
            }
        }

        /// <returns>A pseudorandom double N such that 0 &lt; N &lt; 1</returns>
        public double NextDouble()
        {
            /* Combination */
            long r = s12 - s22;
            r -= m1 * ((r - 1) >> 63);

            /* Component 1 */
            long p = (a12 * s11 - a13 * s10 + corr1) % m1;
            s10 = s11;
            s11 = s12;
            s12 = p;

            /* Component 2 */
            p = (a21 * s22 - a23 * s20 + corr2) % m2;
            s20 = s21;
            s21 = s22;
            s22 = p;

            // Warning - I am not 100% sure that 0 < N < 1, but
            // 1. The reference implementation says "Returns the next pseudorandom double in (0..1)"
            //    and I am choosing to trust that this is correct.
            // 2. I haven't seen a counterexample yet!
            return r * norm;
        }

        /// <returns>A pseudorandom int N such that 0 &lt;= N &lt; <paramref name="maxValue"/></returns>
        public int NextInt32(int maxValue)
        {
            if (maxValue < 1)
            {
                throw new ArgumentException("maxValue must be greater than zero");
            }

            return (int)(NextDouble() * maxValue);
        }

        public struct State
        {
            public readonly long s10;
            public readonly long s11;
            public readonly long s12;
            public readonly long s20;
            public readonly long s21;
            public readonly long s22;

            public State(long s10, long s11, long s12, long s20, long s21, long s22)
            {
                this.s10 = s10;
                this.s11 = s11;
                this.s12 = s12;
                this.s20 = s20;
                this.s21 = s21;
                this.s22 = s22;
            }

            public string Serialize()
            {
                return string.Format("{0}-{1}-{2}-{3}-{4}-{5}", s10, s11, s12, s20, s21, s22);
            }

            public static State Deserialize(string item)
            {
                string[] parts = item.Split('-');
                if (parts.Length != 6)
                {
                    throw new ArgumentException("invalid State string: " + item);
                }
                long s10 = long.Parse(parts[0]);
                long s11 = long.Parse(parts[1]);
                long s12 = long.Parse(parts[2]);
                long s20 = long.Parse(parts[3]);
                long s21 = long.Parse(parts[4]);
                long s22 = long.Parse(parts[5]);
                return new State(s10, s11, s12, s20, s21, s22);
            }
        }
    }
}
