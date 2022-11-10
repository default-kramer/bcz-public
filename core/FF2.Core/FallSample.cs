using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    // WHY IS THIS NECESSARY? Insulating the caller from animationProgress I suppose...
    public readonly struct FallSample
    {
        private readonly FallAnimationSampler sampler;
        private readonly float animationProgress;

        public FallSample(FallAnimationSampler sampler, float animationProgress)
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
