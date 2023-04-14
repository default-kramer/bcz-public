using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BCZ.Core
{
    /// <summary>
    /// A command from the player to the game.
    /// If we have the initial state and a list of timestamped commands, that should
    /// be enough to perfectly replay a single-player game.
    /// 
    /// I'm also thinking that: If we add a list of timestamped homework, that should
    /// be enough to perfectly replay (one player's view of) a multiplayer game.
    /// </summary>
    public enum Command
    {
        None = 0,
        Left = 1,
        Right = 2,
        RotateCW = 3,
        RotateCCW = 4,
        // Plummet is equivalent to BurstBegin followed immediately by BurstCancel.
        // Human players currently have no way to input a Plummet command.
        Plummet = 5,
        BurstBegin = 6,
        BurstCancel = 7,
    }
}
