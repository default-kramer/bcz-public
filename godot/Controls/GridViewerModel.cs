using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FF2.Core;

namespace FF2.Godot.Controls
{
    public sealed class GridViewerModel
    {
        private readonly State state;
        private readonly Ticker ticker;
        private readonly ITickCalculations tickCalculations;

        public GridViewerModel(State state, Ticker timing)
        {
            this.state = state;
            this.ticker = timing;
            this.tickCalculations = state.TickCalculations;
        }

        public IReadOnlyGrid Grid { get { return state.Grid; } }

        public decimal CorruptionProgress { get { return state.CorruptionProgress; } }

        const int LastChanceMillis = 5000;

        public bool ShouldFlicker { get { return state.RemainingMillis < LastChanceMillis; } }

        public float LastChanceProgress { get { return Convert.ToSingle(LastChanceMillis - state.RemainingMillis) / LastChanceMillis; } }

        public Mover? PreviewPlummet() { return state.PreviewPlummet(); }

        public int ColumnDestructionBitmap => tickCalculations.ColumnDestructionBitmap;
        public int RowDestructionBitmap => tickCalculations.RowDestructionBitmap;

        public float BurstProgress() { return ticker.BurstProgress(); }

        public float DestructionIntensity()
        {
            return ticker.DestructionIntensity();
        }

        public FallSample? GetFallSample()
        {
            return ticker.GetFallSample();
        }

        public Occupant GetDestroyedOccupant(Loc loc)
        {
            return tickCalculations.GetDestroyedOccupant(loc, state.Grid);
        }

        public float DestructionProgress(Loc loc)
        {
            if (GetDestroyedOccupant(loc) != Occupant.None)
            {
                return ticker.DestructionProgress();
            }
            else
            {
                return 0;
            }
        }
    }
}
