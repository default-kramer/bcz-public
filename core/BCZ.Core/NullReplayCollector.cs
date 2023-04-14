using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    public sealed class NullReplayCollector : IReplayCollector
    {
        public static readonly NullReplayCollector Instance = new NullReplayCollector();

        public void Collect(Stamped<Command> command) { }

        public void AfterCommand(Moment moment, State state) { }
    }

    public sealed class ListReplayCollector : IReplayCollector
    {
        private readonly List<Stamped<Command>> commands = new List<Stamped<Command>>();

        public IReadOnlyList<Stamped<Command>> Commands => commands;

        public void Collect(Stamped<Command> command)
        {
            commands.Add(command);
        }

        public void AfterCommand(Moment moment, State state) { }
    }
}
