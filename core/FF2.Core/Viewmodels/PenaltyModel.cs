using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core.Viewmodels
{
    public sealed class PenaltyModel
    {
        private readonly PenaltyManager penalties;
        private readonly State state;
        private readonly Ticker ticker;

        internal PenaltyModel(PenaltyManager penalties, State state, Ticker ticker)
        {
            this.penalties = penalties;
            this.state = state;
            this.ticker = ticker;
        }

        public bool Slowmo => ticker.Slowmo;

        public int Count { get { return penalties.Count; } }

        public Penalty this[int index] { get { return penalties[index]; } }

        public CorruptionSample SampleCorruption(CorruptionSample? previous)
        {
            const float maxChangePerSecond = 0.25f; // max refill rate is 25% / second

            var progress = 0f; // TODO Convert.ToSingle(state.CorruptionProgress);
            var now = state.Moment;
            if (previous.HasValue)
            {
                var elapsedMillis = now.Millis - previous.Value.Moment.Millis;
                float maxChange = elapsedMillis * maxChangePerSecond / 1000;
                float change = progress - previous.Value.CorruptionProgress;
                change = Math.Max(-maxChange, Math.Min(change, maxChange));
                return new CorruptionSample(now, previous.Value.CorruptionProgress + change);
            }
            else
            {
                return new CorruptionSample(now, progress);
            }
        }

        public readonly struct CorruptionSample
        {
            public readonly Moment Moment;
            public readonly float CorruptionProgress;

            public CorruptionSample(Moment moment, float corruptionProgress)
            {
                this.Moment = moment;
                this.CorruptionProgress = corruptionProgress;
            }
        }
    }
}
