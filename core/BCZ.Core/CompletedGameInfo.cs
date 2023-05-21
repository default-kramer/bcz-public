using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BCZ.Core
{
    public readonly struct CompletedGameInfo
    {
        public readonly SeededSettings Settings;
        public readonly IStateData FinalState;

        public CompletedGameInfo(SeededSettings settings, IStateData finalState)
        {
            this.Settings = settings;
            this.FinalState = finalState;
        }
    }
}
