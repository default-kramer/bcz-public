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
        float timeSinceDestruction = DestructionEnd + 1f;

        int frames = 0;

        // When does destruction intensity enter the max value?
        const float DestructionPeakStart = 0.1f;
        // When does destruction intensity exit the max value?
        const float DestructionPeakEnd = 0.3f;
        // When does destruction intensity finish completely?
        const float DestructionEnd = 0.55f;

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

                state.Elapse(diff.Milliseconds);
            }

            timeSinceDestruction += delta;
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
                timeSinceDestruction = 0.0f;
            }
        }
    }
}
