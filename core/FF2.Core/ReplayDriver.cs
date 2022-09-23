using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    public class ReplayDriver
    {
        public readonly Ticker Ticker;

        /// <summary>
        /// It's fine if this list is appended to while the replay is in progress.
        /// (But things might misbehave if you <see cref="Advance(Moment)"/> too far and then
        ///  add a command that happened in the past.)
        /// </summary>
        /// <remarks>
        /// Perhaps a queue would be better?
        /// </remarks>
        public readonly IReadOnlyList<Stamped<Command>> Commands;

        /// <summary>
        /// The index into <see cref="Commands"/>.
        /// </summary>
        private int i = 0;

        public ReplayDriver(Ticker ticker, IReadOnlyList<Stamped<Command>> commands)
        {
            this.Ticker = ticker;
            this.Commands = commands;
        }

        public void Advance(Moment now)
        {
            while (i < Commands.Count)
            {
                var command = Commands[i];
                if (command.Moment <= now)
                {
                    i++;
                    if (!Ticker.HandleCommand(command))
                    {
                        throw new Exception($"Replay failed: {command.Value} at {command.Moment}");
                    }
                }
                else
                {
                    break;
                }
            }

            Ticker.Advance(now);
        }
    }
}
