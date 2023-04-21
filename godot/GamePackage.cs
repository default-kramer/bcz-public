using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BCZ.Core;

/// <summary>
/// Contains everything the UI needs to know to start a game.
/// </summary>
public sealed class GamePackage
{
    public readonly SeededSettings Settings;
    public readonly IReadOnlyList<IGoal> Goals;

    public GamePackage(SeededSettings settings, IReadOnlyList<IGoal> goals)
    {
        Settings = settings;
        Goals = goals;
    }
}
