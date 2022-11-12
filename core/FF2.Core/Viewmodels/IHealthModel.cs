using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core.Viewmodels
{
    public interface IHealthModel
    {
        IReadOnlyGrid Grid { get; }

        float GetAdder(Loc loc); // formerly handled by FallSample, rethink this?

        /// <summary>
        /// Used to warn the player that they are nearing failure.
        /// Values may range from 0.0 (no cause for alarm) to 1.0 (failure).
        /// </summary>
        float LastGaspProgress();

        float DestructionProgress(Loc loc);
    }
}
