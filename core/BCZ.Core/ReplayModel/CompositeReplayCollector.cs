using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BCZ.Core.ReplayModel
{
    sealed class CompositeReplayCollector : IReplayCollector
    {
        private readonly IReplayCollector first;
        private readonly IReplayCollector second;

        public CompositeReplayCollector(IReplayCollector first, IReplayCollector second)
        {
            this.first = first;
            this.second = second;
        }

        public void AfterCommand(Moment moment, State state)
        {
            first.AfterCommand(moment, state);
            second.AfterCommand(moment, state);
        }

        public void Collect(Stamped<Command> command)
        {
            first.Collect(command);
            second.Collect(command);
        }

        public void OnGameEnded()
        {
            first.OnGameEnded();
            second.OnGameEnded();
        }
    }
}
