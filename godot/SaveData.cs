using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BCZ.Core;

/// <summary>
/// Just using an in-memory singleton for now. Will refactor when needed.
/// </summary>
static class SaveData
{
    public static int ScoreAttackPB { get; set; } = 0;

    public static void RecordMedal(int level, MedalKind medal)
    {
        // nothing for now...
    }
}
