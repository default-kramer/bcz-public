using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FF2.Core;

namespace FF2.Godot
{
    public sealed class Timing
    {
        private readonly State state;
        private readonly TickCalculations tickCalculations;

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

        public void _Process(float delta)
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

                Elapse(diff.Milliseconds);
            }
        }

        public void Elapse(int milliseconds)
        {
            state.Elapse(milliseconds);

            timeSinceDestruction += milliseconds;
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
