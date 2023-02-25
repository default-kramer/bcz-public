using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    public interface IFallAnimator
    {
        /// <summary>
        /// Returns the amount of y to add for the occupant at the given location.
        /// For example, a value of 2.3 means "draw that occupant 2.3 cells higher
        /// than you normally would."
        /// </summary>
        float GetAdder(Loc loc);
    }

    public sealed class NullFallAnimator : IFallAnimator
    {
        private NullFallAnimator() { }
        public static readonly NullFallAnimator Instance = new NullFallAnimator();

        public float GetAdder(Loc loc)
        {
            return 0;
        }
    }

    sealed class FallAnimator : IFallAnimator
    {
        private FallAnimationSampler sampler;
        private float animationProgress;

        public FallAnimator(FallAnimationSampler sampler, float animationProgress)
        {
            this.sampler = sampler;
            this.animationProgress = animationProgress;
        }

        public void Resample(FallAnimationSampler sampler, float animationProgress)
        {
            this.sampler = sampler;
            this.animationProgress = animationProgress;
        }

        public float GetAdder(Loc loc)
        {
            return sampler.GetAdder(loc, animationProgress);
        }
    }
}
