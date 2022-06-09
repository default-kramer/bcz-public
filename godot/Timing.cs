using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FF2.Core;
using Godot;

namespace FF2.Godot
{
    public enum GameKeys
    {
        None = 0,
        Left = 1,
        Right = 2,
        RotateCW = 4,
        RotateCCW = 8,
        Drop = 16,
    }

    public sealed class Timing
    {
        private readonly State state;
        private readonly TickCalculations tickCalculations;
        private int totalMillis = 0;

        public Timing(State state, TickCalculations tickCalculations)
        {
            this.state = state;
            this.tickCalculations = tickCalculations;
        }

        DateTime startTime = default(DateTime);
        DateTime lastProcess = default(DateTime);

        // Initial value should not be near int.MaxValue to avoid rollover,
        // but it must be > DestructionEnd
        int timeSinceDestruction = (int)DestructionEnd + 1;

        // How long has the drop button been held down?
        // A negative value means it is not down.
        int dropDuration = -1;
        bool burstComplete = false;

        int frames = 0;

        // When does destruction intensity enter the max value?
        const float DestructionPeakStart = 100;
        // When does destruction intensity exit the max value?
        const float DestructionPeakEnd = 300;
        // When does destruction intensity finish completely?
        const float DestructionEnd = 550;

        public float DestructionIntensity()
        {
            float intensity = 0f;

            if (timeSinceDestruction < DestructionPeakStart)
            {
                intensity = timeSinceDestruction / DestructionPeakStart;
            }
            else if (timeSinceDestruction < DestructionPeakEnd)
            {
                intensity = 1.0f;
            }
            else if (timeSinceDestruction < DestructionEnd)
            {
                intensity = 1.0f - (timeSinceDestruction - DestructionPeakEnd) / (DestructionEnd - DestructionPeakEnd);
            }

            return intensity;
        }

        public float DestructionProgress()
        {
            return Math.Min(1f, timeSinceDestruction / DestructionEnd);
        }

        public void _Process(float delta, GameKeys input)
        {
            if (startTime == default(DateTime))
            {
                startTime = DateTime.UtcNow;
                lastProcess = startTime;
            }
            else
            {
                var now = DateTime.UtcNow;
                var diff = now - lastProcess;
                lastProcess = now;

                Elapse(diff.Milliseconds, input);
            }
        }

        private void HandleInput(GameKeys input, int elapsedMillis)
        {
            if (input.HasFlag(GameKeys.Left))
            {
                state.Move(Direction.Left);
            }
            if (input.HasFlag(GameKeys.Right))
            {
                state.Move(Direction.Right);
            }
            if (input.HasFlag(GameKeys.RotateCW))
            {
                state.Rotate(clockwise: true);
            }
            if (input.HasFlag(GameKeys.RotateCCW))
            {
                state.Rotate(clockwise: false);
            }
            if (input.HasFlag(GameKeys.Drop))
            {
                if (dropDuration < 0)
                {
                    state.Plummet();
                    dropDuration = 0;
                }
                else
                {
                    dropDuration += elapsedMillis;
                }
            }
            else
            {
                dropDuration = -1;
                burstComplete = false;
            }
        }

        public void Elapse(int milliseconds, GameKeys input)
        {
            HandleInput(input, milliseconds);

            state.Elapse(milliseconds);

            timeSinceDestruction += milliseconds;

            const int burstLimit = 500;
            if (dropDuration >= burstLimit && !burstComplete)
            {
                burstComplete = true;
                state.Burst();
            }
            else if (dropDuration > -1 && !burstComplete)
            {
                return;
            }

            if (timeSinceDestruction < DestructionEnd)
            {
                return;
            }

            tickCalculations.Reset();

            frames++;

            if (frames % 10 == 0)
            {
                state.Tick(tickCalculations);
            }

            if (tickCalculations.RowDestructionBitmap != 0 || tickCalculations.ColumnDestructionBitmap != 0)
            {
                timeSinceDestruction = 0;
            }
        }
    }
}
