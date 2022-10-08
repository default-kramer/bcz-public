using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    public class PuzzleReplayDriver : IReplayDriver
    {
        private readonly Ticker ticker;
        private readonly IReadOnlyList<Move> moves;
        private int i = 0;

        const int normalDelay = 500; // millis
        const int burstDelay = 1000; // millis
        private Moment nextCommandTime = new Moment(normalDelay);

        public PuzzleReplayDriver(Ticker ticker, IReadOnlyList<Move> moves)
        {
            this.ticker = ticker;
            this.moves = moves;
        }

        public State State => ticker.state;

        public static PuzzleReplayDriver BuildPuzzleReplay(Puzzle puzzle)
        {
            var grid = Grid.Clone(puzzle.InitialGrid);
            var state = new State(grid, puzzle.MakeDeck());
            var ticker = new Ticker(state, NullReplayCollector.Instance);
            return new PuzzleReplayDriver(ticker, puzzle.Moves);
        }

        public Ticker Ticker => ticker;

        // State should go to GameOver once when it can no longer spawn
        public bool IsDone => i == moves.Count && ticker.state.Kind == StateKind.GameOver;

        public void Advance(Moment now)
        {
            // TODO should also wait until the state is accepting input...
            // But for now it won't crash because we are only replaying the moves
            // from a single combo so all the animations will be very short (like Spawning).
            while (i < moves.Count && nextCommandTime < now)
            {
                var current = moves[i];
                var command = ticker.state.Approach(current.Orientation) ?? Command.BurstBegin;

                if (command == Command.BurstBegin)
                {
                    Do(Command.BurstBegin, now);
                    if (!current.DidBurst)
                    {
                        Do(Command.BurstCancel, now);
                    }
                    else
                    {
                        nextCommandTime = now.AddMillis(burstDelay);
                    }
                    i++;
                }
                else
                {
                    Do(command, now);
                }
            }

            ticker.Advance(now);
        }

        private void Do(Command command, Moment now)
        {
            if (!ticker.HandleCommand(command, now))
            {
                throw new Exception($"TODO command failed: {command} at {now}");
            }
            nextCommandTime = now.AddMillis(normalDelay);
        }

        public void RunToCompletion()
        {
            var now = new Moment(500);
            while (!IsDone)
            {
                Advance(now);
                now = now.AddMillis(500);
            }
        }
    }

    public class ReplayDriver : IReplayDriver
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
        public readonly IReadOnlyList<Stamped<Command>> Commands;

        /// <summary>
        /// The index into <see cref="Commands"/>.
        /// </summary>
        private int i = 0;

        public ReplayDriver(Ticker ticker, IReadOnlyList<Stamped<Command>> commands)
        {
            this.ticker = ticker;
            this.Commands = commands;
        }

        public Ticker Ticker => ticker;

        public void Advance(Moment now)
        {
            while (i < Commands.Count)
            {
                var command = Commands[i];
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
