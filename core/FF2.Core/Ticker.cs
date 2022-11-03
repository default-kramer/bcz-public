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

        /// <summary>
        /// When `state.Tick(...)` returns true, we store the Kind from *before* the tick,
        /// because this is the Kind of thing that succeeded.
        /// </summary>
        readonly struct Animation
        {
            public readonly StateKind Kind;
            public readonly Moment BeginTime;

            public Animation(StateKind kind, Moment beginTime)
            {
                this.Kind = kind;
                this.BeginTime = beginTime;
            }

            /// <summary>
            /// Returns true if the user has been holding the burst action long enough
            /// to enable the slowmo effect.
            /// </summary>
            public bool Slowmo(Moment now)
            {
                return Kind == StateKind.Bursting && now >= BeginTime.AddMillis(BurstSlowmoDebounce);
            }
        }

        private Animation? currentAnimation;

        private int timeSinceDestruction(Moment now)
        {
            if (currentAnimation.HasValue && currentAnimation.Value.Kind == StateKind.Destroying)
            {
                return now.Millis - currentAnimation.Value.BeginTime.Millis;
            }
            return DestructionEndInt + 1;
        }

        public Ticker(State state, IReplayCollector replayCollector)
        {
            this.state = state;
            this.replayCollector = replayCollector;

            // TODO it's unclear whose responsibility this should be:
            lastMoment = new Moment(0);
            state.Tick(lastMoment);
            if (state.Kind != StateKind.Waiting)
            {
                throw new Exception("WTF?");
            }
            currentAnimation = new Animation(StateKind.Spawning, lastMoment);
        }

        public bool Slowmo => state.Slowmo || currentAnimation.GetValueOrDefault().Slowmo(lastMoment);

        // When does destruction intensity enter the max value?
        const float DestructionPeakStart = 100;
        // When does destruction intensity exit the max value?
        const float DestructionPeakEnd = 300;
        // When does destruction intensity finish completely?
        const float DestructionEnd = DestructionEndInt;
        const int DestructionEndInt = 550;

        private float AnimationProgress(Moment now, Animation animation)
        {
            if (currentAnimation.HasValue)
            {
                var elapsed = now.Millis - animation.BeginTime.Millis;
                return elapsed * 1f / GetDuration(animation.Kind, state);
            }
            return 0;
        }

        public float AnimationProgress(Moment? now = null)
        {
            if (currentAnimation.HasValue)
            {
                return AnimationProgress(now ?? lastMoment, currentAnimation.Value);
            }
            return 0;
        }

        public FallSample? GetFallSample(Moment? now = null)
        {
            if (currentAnimation == null) { return null; }

            var kind = currentAnimation.Value.Kind;
            if (kind == StateKind.Falling)
            {
                float progress = AnimationProgress(now ?? lastMoment, currentAnimation.Value);
                return new FallSample(state.FallSampler, progress);
            }

            return null;
        }

        public float DestructionIntensity(Moment? now = null)
        {
            float intensity = 0f;

            int time = timeSinceDestruction(now ?? lastMoment);

            if (time < DestructionPeakStart)
            {
                intensity = time / DestructionPeakStart;
            }
            else if (time < DestructionPeakEnd)
            {
                intensity = 1.0f;
            }
            else if (time < DestructionEnd)
            {
                intensity = 1.0f - (time - DestructionPeakEnd) / (DestructionEnd - DestructionPeakEnd);
            }

            return intensity;
        }

        public float DestructionProgress(Moment? now = null)
        {
            return Math.Min(1f, timeSinceDestruction(now ?? lastMoment) / DestructionEnd);
        }

        public float BurstProgress(Moment? now = null)
        {
            if (currentAnimation.HasValue && currentAnimation.Value.Kind == StateKind.Bursting)
            {
                const int delay = 70;
                const float duration2 = BurstDuration - delay;

                now = now ?? lastMoment;
                int completed = now.Value.Millis - currentAnimation.Value.BeginTime.Millis - delay;
                return Math.Max(0f, Math.Min(1f, completed / duration2));
            }

            return 0;
        }

        private const int BurstDuration = 500;
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

            if (command == Command.BurstBegin)
            {
                if (state.HandleCommand(Command.Plummet, now))
                {
                    currentAnimation = new Animation(StateKind.Bursting, now);
                    return true;
                }
                return false;
            }
            else if (command == Command.BurstCancel)
            {
                if (currentAnimation.HasValue && currentAnimation.Value.Kind == StateKind.Bursting)
                {
                    currentAnimation = null;
                    return true;
                }
                return false;
            }
            else if (currentAnimation.HasValue)
            {
                return false; // need to wait for animation to complete
            }
            else
            {
                return state.HandleCommand(command, now);
            }
        }

        /// <summary>
        /// TODO can we make this private?
        /// </summary>
        public void Advance(Moment target)
        {
            Advance(lastMoment, target);
            this.lastMoment = target;
        }

        private (bool, StateKind) Tick(Moment cursor)
        {
            var attempt = state.Kind;

            if (state.Tick(cursor))
            {
                return (true, attempt);
            }
            else if (state.Kind != attempt)
            {
                return Tick(cursor);
            }
            else
            {
                return (false, StateKind.Empty);
            }
        }

        private bool DoTick(Moment cursor)
        {
            if (currentAnimation.HasValue)
            {
                throw new Exception($"Assert fail. Previous animation incomplete: {currentAnimation.Value.Kind}");
            }

            var result = Tick(cursor);
            if (result.Item1)
            {
                currentAnimation = new Animation(result.Item2, cursor);
                //Console.WriteLine($"Beginning: {currentAnimation.Value.Kind} / {currentAnimation.Value.BeginTime}");
                return true;
            }
            return false;
        }

        /// <summary>
        /// Returns how many milliseconds the animation should last
        /// </summary>
        private static int GetDuration(StateKind kind, State state)
        {
            const int fallSpeed = 150; // TODO This should be configurable

            return kind switch
            {
                StateKind.Falling => state.FallSampler.MaxFall() * fallSpeed,
                StateKind.Spawning => 100,
                StateKind.Destroying => DestructionEndInt,
                StateKind.Bursting => BurstDuration,
                _ => throw new Exception($"TODO: {kind}"),
            };
        }

        private void Advance(Moment cursor, Moment target)
        {
            while (cursor < target)
            {
                state.Elapse(cursor);

                if (currentAnimation.HasValue)
                {
                    var kind = currentAnimation.Value.Kind;
                    var startTime = currentAnimation.Value.BeginTime;
                    int duration = GetDuration(kind, state);
                    var endTime = startTime.AddMillis(duration);

                    if (endTime <= target)
                    {
                        //Console.WriteLine($"Completed {kind} after {duration} ms ({target} >= {endTime})");
                        currentAnimation = null;
                        cursor = endTime;

                        if (kind == StateKind.Bursting)
                        {
                            state.Burst(endTime);
                        }

                        DoTick(cursor);
                    }
                    else
                    {
                        // continue current animation
                        cursor = target;
                    }
                }
                else if (state.Kind == StateKind.GameOver)
                {
                    cursor = target;
                }
                else if (state.Kind != StateKind.Waiting)
                {
                    // TODO figure out why this is necessary beyond the first tick...
                    //Console.WriteLine($"BOOTSTRAP TICK: {state.Kind}");
                    DoTick(cursor);
                }
                else
                {
                    cursor = target;
                }
            }

            state.Elapse(cursor);
        }

        /// <summary>
        /// Just for testing
        /// </summary>
        public string AnimationString
        {
            get
            {
                if (currentAnimation.HasValue)
                {
                    var kind = currentAnimation.Value.Kind;
                    var start = currentAnimation.Value.BeginTime;
                    int duration = GetDuration(kind, state);
                    var end = start.AddMillis(duration);
                    var remainingMillis = end.Millis - lastMoment.Millis;
                    return $"{kind} {remainingMillis}";
                }
                else
                {
                    return $"Pre-{state.Kind}";
                }
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
