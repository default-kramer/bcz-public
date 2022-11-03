using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    public sealed class FallAnimationSampler
    {
        private readonly IReadOnlyGrid grid;
        private readonly int[] fallCountBuffer;
        private int? maxFall;

        public FallAnimationSampler(IReadOnlyGrid grid)
        {
            this.grid = grid;
            this.fallCountBuffer = new int[grid.Width * grid.Height];
            ResetFallCountBuffer();
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
