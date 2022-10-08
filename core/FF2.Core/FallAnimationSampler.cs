using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    sealed class FallTracker : FallAnimationSampler
    {
        private readonly int[] swapBuffer;

        public FallTracker(IReadOnlyGrid grid) : base(grid)
        {
            this.swapBuffer = new int[fallCountBuffer.Length];
        }

        public void Combine(FallAnimationSampler sample2)
        {
            var temp = swapBuffer.AsSpan();
            temp.Fill(0);
            Combine(this, sample2, temp);
            temp.CopyTo(fallCountBuffer);
        }
    }

    public class FallAnimationSampler
    {
        private readonly IReadOnlyGrid grid;
        protected readonly int[] fallCountBuffer;
        private int? maxFall;

        public FallAnimationSampler(IReadOnlyGrid grid)
        {
            this.grid = grid;
            this.fallCountBuffer = new int[grid.Width * grid.Height];
            ResetFallCountBuffer();
        }

        protected static void Combine(FallAnimationSampler sample1, FallAnimationSampler sample2, Span<int> newBuffer)
        {
            var grid = sample1.grid;

            for (int i = 0; i < sample1.fallCountBuffer.Length; i++)
            {
                var loc = Loc.FromIndex(i, grid);
                var drop2 = sample2.GetFall(loc);
                var loc1 = new Loc(loc.X, loc.Y + drop2);
                var drop1 = sample1.GetFall(loc1);
                newBuffer[i] = drop1 + drop2;
            }
        }

        internal Span<int> ResetFallCountBuffer()
        {
            maxFall = null;
            var span = fallCountBuffer.AsSpan();
            span.Fill(0);
            return span;
        }

        public int MaxFall()
        {
            maxFall = maxFall ?? fallCountBuffer.Max();
            return maxFall.Value;
        }

        private int GetFall(Loc loc)
        {
            var index = loc.ToIndex(grid);
            return fallCountBuffer[index];
        }

        public Loc GetOriginalLoc(Loc loc)
        {
            int drop = GetFall(loc);
            return new Loc(loc.X, loc.Y + drop);
        }

        /// <summary>
        /// If the occupant at (2,4) fell from (2,10) then the adder returned by this method for
        /// the location (2,4) will decrease from 6.0 to 0.0 as the given <paramref name="animationProgress"/>
        /// increases from 0.0 to 1.0 (aka from 0% to 100% complete).
        /// All occupants will fall at the same speed, so if 6 is the max fall but another occupant falls
        /// by only 4, that occupant will be done falling when the animation is 67% complete.
        /// </summary>
        public float GetAdder(Loc loc, float animationProgress)
        {
            var maxFall = MaxFall();
            if (maxFall < 1)
            {
                return 0;
            }
            float completeTime = GetFall(loc) * 1f / maxFall;
            if (animationProgress >= completeTime)
            {
                return 0;
            }
            int fall = GetFall(loc);
            return fall - animationProgress / completeTime * fall;
        }
    }
}
