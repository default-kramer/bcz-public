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
        private readonly Ticker timing;
        private readonly TickCalculations tickCalculations;

        public GridViewerModel(State state, Ticker timing, TickCalculations tickCalculations)
        {
            this.state = state;
            this.timing = timing;
            this.tickCalculations = tickCalculations;
        }

        public IReadOnlyGrid Grid { get { return state.Grid; } }

        public decimal CorruptionProgress { get { return state.CorruptionProgress; } }

        public Mover? PreviewPlummet() { return state.PreviewPlummet(); }

        public int ColumnDestructionBitmap { get { return tickCalculations.ColumnDestructionBitmap; } }
        public int RowDestructionBitmap { get { return tickCalculations.RowDestructionBitmap; } }

        public float BurstProgress() { return timing.BurstProgress(); }

        public float DestructionIntensity()
        {
            return timing.DestructionIntensity();
        }

        public Occupant GetDestroyedOccupant(Loc loc)
        {
            return tickCalculations.GetDestroyedOccupant(loc, state.Grid);
        }

        public float DestructionProgress(Loc loc)
        {
            if (GetDestroyedOccupant(loc) != Occupant.None)
            {
                return timing.DestructionProgress();
            }
            else
            {
                return 0;
            }
        }
    }
}
