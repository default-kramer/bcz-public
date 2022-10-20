using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    public interface ITickCalculations
    {
        /// <summary>
        /// For the background shader.
        /// Bit N indicates whether vertical destruction occurred in column N.
        /// </summary>
        int ColumnDestructionBitmap { get; }

        /// <summary>
        /// For the background shader.
        /// Bit N indicates whether horiztonal destruction occurred in row N.
        /// </summary>
        int RowDestructionBitmap { get; }

        Occupant GetDestroyedOccupant(Loc loc, IReadOnlyGrid grid);
    }

    // TODO rename to "DestructionCalculations" or something like that...
    /// <summary>
    /// Used to capture information during a call to <see cref="State.Tick(TickCalculations)"/>.
    /// </summary>
    sealed class TickCalculations : ITickCalculations
    {
        /// <summary>
        /// For the background shader.
        /// Bit N indicates whether vertical destruction occurred in column N.
        /// </summary>
        public int ColumnDestructionBitmap;

        /// <summary>
        /// For the background shader.
        /// Bit N indicates whether horiztonal destruction occurred in row N.
        /// </summary>
        public int RowDestructionBitmap;

        private Occupant[] destroyedOccupants;

        public int NumVerticalGroups;
        public int NumHorizontalGroups;

        public TickCalculations(IReadOnlyGrid grid)
        {
            destroyedOccupants = new Occupant[grid.Width * grid.Height];
        }

        int ITickCalculations.ColumnDestructionBitmap => this.ColumnDestructionBitmap;
        int ITickCalculations.RowDestructionBitmap => this.RowDestructionBitmap;

        public void Reset()
        {
            ColumnDestructionBitmap = 0;
            RowDestructionBitmap = 0;
            destroyedOccupants.AsSpan().Fill(Occupant.None);
            NumVerticalGroups = 0;
            NumHorizontalGroups = 0;
        }

        public void AddColumnDestruction(int x)
        {
            NumVerticalGroups++;
            ColumnDestructionBitmap |= 1 << x;
        }

        public void AddRowDestruction(int y, IReadOnlyGrid grid)
        {
            NumHorizontalGroups++;
            y = grid.Height - 1 - y; // the shader uses Y=0 at the top
            RowDestructionBitmap |= 1 << y;
        }

        public void AddDestroyedOccupant(Loc loc, Occupant occupant, IReadOnlyGrid grid)
        {
            int size = grid.Width * grid.Height;
            if (destroyedOccupants.Length < size)
            {
                destroyedOccupants = new Occupant[size];
            }

            destroyedOccupants[loc.ToIndex(grid)] = occupant;
        }

        public Occupant GetDestroyedOccupant(Loc loc, IReadOnlyGrid grid)
        {
            return destroyedOccupants[loc.ToIndex(grid)];
        }
    }
}
