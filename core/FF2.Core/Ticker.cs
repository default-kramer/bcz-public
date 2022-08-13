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
        private readonly State state;
        private readonly IReplayCollector replayCollector;
        private readonly TickCalculations tickCalculations;
        private Moment lastMoment = new Moment(0);

        // When `state.Tick(...)` returns true, we store the Kind from *before* the tick,
        // because this is the Kind of thing that succeeded.
        private (StateKind, Moment)? currentAnimation; // Item2 is the start time of the animation

        private int timeSinceDestruction(Moment now)
        {
            if (currentAnimation.HasValue && currentAnimation.Value.Item1 == StateKind.Destroying)
            {
                return now.Millis - currentAnimation.Value.Item2.Millis;
            }
            return DestructionEndInt + 1;
        }

        public Ticker(State state, TickCalculations tickCalculations, IReplayCollector replayCollector)
        {
            this.state = state;
            this.replayCollector = replayCollector;
            this.tickCalculations = tickCalculations;
        }

        // When does destruction intensity enter the max value?
        const float DestructionPeakStart = 100;
        // When does destruction intensity exit the max value?
        const float DestructionPeakEnd = 300;
        // When does destruction intensity finish completely?
        const float DestructionEnd = DestructionEndInt;
        const int DestructionEndInt = 550;

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
            if (currentAnimation.HasValue && currentAnimation.Value.Item1 == StateKind.Bursting)
            {
                const int delay = 70;
                const float duration2 = BurstDuration - delay;

                now = now ?? lastMoment;
                int completed = now.Value.Millis - currentAnimation.Value.Item2.Millis - delay;
                return Math.Max(0f, Math.Min(1f, completed / duration2));
            }

            return 0;
        }

        private const int BurstDuration = 500;

        public bool HandleCommand(Stamped<Command> command)
        {
            return HandleCommand(command.Value, command.Moment);
        }

        public bool HandleCommand(Command command, Moment now)
        {
            if (DoHandleCommand(command, now))
            {
                replayCollector.Collect(new Stamped<Command>(now, command));
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
                    currentAnimation = (StateKind.Bursting, now);
                    return true;
                }
                return false;
            }
            else if (command == Command.BurstCancel)
            {
                if (currentAnimation.HasValue && currentAnimation.Value.Item1 == StateKind.Bursting)
                {
                    currentAnimation = null;
                    return true;
                }
                return false;
            }
            else
            {
                return state.HandleCommand(command, now);
            }
        }

        /// <summary>
        /// TODO can we make this private?
        /// </summary>
        internal void Advance(Moment target)
        {
            Advance(lastMoment, target);
            this.lastMoment = target;
        }

        private (bool, StateKind) Tick(Moment cursor)
        {
            var attempt = state.Kind;

            tickCalculations.Reset();
            if (state.Tick(cursor, tickCalculations))
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
            var result = Tick(cursor);
            if (result.Item1)
            {
                currentAnimation = (result.Item2, cursor);
                return true;
            }
            return false;
        }

        private void Advance(Moment cursor, Moment target)
        {
            while (cursor < target)
            {
                state.Elapse(cursor);

                if (currentAnimation.HasValue)
                {
                    (StateKind kind, Moment startTime) = currentAnimation.Value;
                    int duration = kind switch
                    {
                        StateKind.Falling => 250,
                        StateKind.Spawning => 100,
                        StateKind.Destroying => DestructionEndInt,
                        StateKind.Bursting => BurstDuration,
                        _ => throw new Exception($"TODO: {kind}"),
                    };
                    var endTime = startTime.AddMillis(duration);

                    if (endTime <= target)
                    {
                        //Console.WriteLine($"Completed {kind} after {duration} ms");
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
                else if (state.Kind != StateKind.Waiting)
                {
                    // TODO figure out why this is necessary beyond the first tick...
                    //Console.WriteLine($"BOOTSTRAP TICK: {state.Kind}");
                    DoTick(cursor);
                    cursor = target;
                }
                else
                {
                    cursor = target;
                }
            }

            state.Elapse(cursor);
        }
    }

    public sealed class DotnetTicker : Ticker
    {
        private DateTime startTime = default(DateTime);
        private Moment now;

        public DotnetTicker(State state, TickCalculations tickCalculations, IReplayCollector replayCollector)
            : base(state, tickCalculations, replayCollector)
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
