using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BCZ.Core
{
    /// <summary>
    /// The ticker layers timing-related concerns on top of a <see cref="State"/>.
    /// </summary>
    public class Ticker
    {
        public readonly State state;
        private readonly IReplayCollector replayCollector;
        private bool isGameOver;
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

        public IFallAnimator GetFallAnimator()
        {
            return state.GetFallAnimator();
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
            CheckGameOver();
        }

        /// <summary>
        /// Return true if the game is over, false otherwise.
        /// </summary>
        protected bool CheckGameOver()
        {
            if (isGameOver)
            {
                return true;
            }

            if (state.IsGameOver)
            {
                bool doCleanup = false;

                // Only lock once per game. I'm not even sure if locking is necessary at all,
                // but I've already seen some double-Dispose() exceptions so let's be safe.
                lock (this)
                {
                    if (!isGameOver)
                    {
                        isGameOver = true;
                        doCleanup = true;
                    }
                }
                if (doCleanup)
                {
                    replayCollector.OnGameEnded();
                }
            }

            return isGameOver;
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
        // Godot's `Process(float delta)` timekeeping mechanism loses time against a wall clock
        // if the game starts dropping frames.
        // (Even if you never drop frames, I'm not sure that it would precisely match a wall clock.)
        // So we also use a Stopwatch and use whichever one runs faster.
        private readonly System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        private float accumulatedGodotSeconds = 0;
        private bool isPaused;
        private bool isStarted;
        private Moment now = Moment.Zero;

        public DotnetTicker(State state, IReplayCollector replayCollector)
            : base(state, replayCollector)
        {
        }

        public Moment Now { get { return now; } }

        float startDelay = 0;
        public void DelayStart(float seconds)
        {
            startDelay = seconds;
        }

        public void _Process(float delta)
        {
            if (startDelay > 0)
            {
                startDelay -= delta;
                return;
            }
            if (isPaused)
            {
                return;
            }

            if (!isStarted)
            {
                // Frame 0
                isStarted = true;
                stopwatch.Start();
            }
            else
            {
                // Frame N>0
                accumulatedGodotSeconds += delta;
            }

            if (CheckGameOver())
            {
                stopwatch.Stop();
                return;
            }

            int totalStopwatchMillis = Convert.ToInt32(stopwatch.ElapsedMilliseconds);
            int totalMillisGodot = Convert.ToInt32(accumulatedGodotSeconds * 1000);

            var totalMillis = Math.Max(totalStopwatchMillis, totalMillisGodot);
            if (totalMillis < now.Millis)
            {
                throw new Exception("Cannot reverse time!");
            }

            //Console.WriteLine($"Total Millis: Godot {totalMillisGodot} / dotnet {totalStopwatchMillis}");

            now = new Moment(totalMillis);
            Advance(now);
        }

        public void SetPaused(bool paused)
        {
            if (paused && !this.isPaused)
            {
                stopwatch.Stop();
                isPaused = true;
            }
            if (!paused && this.isPaused)
            {
                isPaused = false;
                stopwatch.Start();
            }
        }

        public bool HandleCommand(Command command)
        {
            if (startDelay > 0)
            {
                return false;
            }
            return HandleCommand(command, now);
        }
    }
}
