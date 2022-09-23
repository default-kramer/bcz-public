using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    public interface IReplayCollector
    {
        void Collect(Stamped<Command> command);

        void AfterCommand(Moment moment, State state);
    }

    public interface ISettingsCollection
    {
        int MaxLevel { get; }

        ISinglePlayerSettings GetSettings(int level);
    }
}
