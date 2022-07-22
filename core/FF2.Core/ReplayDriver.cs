using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    public class ReplayDriver
    {
        private readonly Ticker ticker;

        /// <summary>
        /// It's fine if this list is appended to while the replay is in progress.
        /// (But things might misbehave if you <see cref="Advance(Moment)"/> too far and then
        ///  add a command that happened in the past.)
        /// </summary>
        /// <remarks>
        /// Perhaps a queue would be better?
        /// </remarks>
        private readonly IReadOnlyList<Stamped<Command>> commands;

        /// <summary>
        /// The index into <see cref="commands"/>.
        /// </summary>
        private int i = 0;

        public ReplayDriver(Ticker ticker, IReadOnlyList<Stamped<Command>> commands)
        {
            this.ticker = ticker;
            this.commands = commands;
        }

        public void Advance(Moment now)
        {
            while (i < commands.Count)
            {
                var command = commands[i];
                if (command.Moment <= now)
                {
                    i++;
                    if (!ticker.HandleCommand(command))
                    {
                        throw new Exception($"Replay failed: {command.Value} at {command.Moment}");
                    }
                }
                else
                {
                    break;
                }
            }

            ticker.Advance(now);
        }
    }
}
