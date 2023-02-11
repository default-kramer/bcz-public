using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    /// <summary>
    /// The ticker layers timing-related concerns on top of a <see cref="State"/>.
    /// </summary>
    public class Ticker
    {
        public readonly State state;
        private readonly IReplayCollector replayCollector;
        private Moment lastMoment;

        public Ticker(State state, IReplayCollector replayCollector)
        {
            this.state = state;
            this.replayCollector = replayCollector;
            lastMoment = Moment.Zero;
        }

        public bool Slowmo => state.Slowmo;// TODO || currentAnimation.GetValueOrDefault().Slowmo(lastMoment);

        // When does destruction intensity enter the max value?
        const float DestructionPeakStart = 100f / 550f;
        // When does destruction intensity exit the max value?
        const float DestructionPeakEnd = 300f / 550f;

        public FallSample? GetFallSample(Moment? now = null)
        {
            if (state.CurrentEvent.Kind == StateEventKind.Fell)
            {
                float progress = state.CurrentEvent.Completion.Progress();
                var sampler = state.CurrentEvent.FellPayload();
                return new FallSample(sampler, progress);
            }
            return null;
        }

        public float DestructionIntensity()
        {
            float intensity = 0f;

            float progress = DestructionProgress();

            if (progress < DestructionPeakStart)
            {
                intensity = progress / DestructionPeakStart;
            }
            else if (progress < DestructionPeakEnd)
            {
                intensity = 1.0f;
            }
            else if (progress < 1f)
            {
                intensity = 1.0f - (progress - DestructionPeakEnd) / (1f - DestructionPeakEnd);
            }

            return intensity;
        }

        public float DestructionProgress()
        {
            return state.CurrentEvent.ProgressOr(StateEventKind.Destroyed, 0);
        }

        public float BurstProgress(Moment? now = null)
        {
            return state.CurrentEvent.ProgressOr(StateEventKind.BurstBegan, 0);
        }

        private const int BurstSlowmoDebounce = 100;

        public bool HandleCommand(Stamped<Command> command)
        {
            return HandleCommand(command.Value, command.Moment);
        }

        public bool HandleCommand(Command command, Moment now)
        {
            if (DoHandleCommand(command, now))
            {
                replayCollector.Collect(new Stamped<Command>(now, command));
                replayCollector.AfterCommand(now, state);
                return true;
            }
            return false;
        }

        private bool DoHandleCommand(Command command, Moment now)
        {
            Advance(now);
            return state.HandleCommand(command, now);
        }

        /// <summary>
        /// TODO can we make this private?
        /// </summary>
        public void Advance(Moment target)
        {
            Advance(lastMoment, target);
            this.lastMoment = target;
        }

        private void Advance(Moment cursor, Moment target)
        {
            state.Elapse(target);
        }

        /// <summary>
        /// Just for testing
        /// </summary>
        public string AnimationString
        {
            get
            {
                return $"{state.CurrentEvent.Kind} progress: {state.CurrentEvent.Completion.Progress()}";
            }
        }
    }

    public sealed class DotnetTicker : Ticker
    {
        private DateTime startTime = default(DateTime);
        private Moment now;

        public DotnetTicker(State state, IReplayCollector replayCollector)
            : base(state, replayCollector)
        {
        }

        public Moment Now { get { return now; } }

        public void _Process(float delta)
        {
            if (startTime == default(DateTime))
            {
                startTime = DateTime.UtcNow;
            }

            var millis = (DateTime.UtcNow - startTime).TotalMilliseconds;
            now = new Moment(Convert.ToInt32(millis));
            Advance(now);
        }

        public bool HandleCommand(Command command)
        {
            return HandleCommand(command, now);
        }
    }
}
