using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BCZ.Core.ReplayModel
{
    public interface IReplayParser
    {
        void Parse(VersionElement version);
        void Parse(CommandElement command);
        void Parse(GridHashElement gridHash);
        void Parse(SettingsElement setting);
    }

    sealed class ReplayParser : IReplayParser
    {
        readonly struct GameInfo
        {
            public readonly SeededSettings Settings;

            /// <summary>
            /// This ticker is used internally to validate that the replay file
            /// does not contain any illegal/impossible commands.
            /// </summary>
            public readonly Ticker Ticker;

            /// <summary>
            /// This is used internally to capture the commands of the replay.
            /// </summary>
            public readonly ListReplayCollector ReplayCollector;

            public GameInfo(SeededSettings settings, Ticker ticker, ListReplayCollector replayCollector)
            {
                this.Settings = settings;
                this.Ticker = ticker;
                this.ReplayCollector = replayCollector;
            }

            public State CreateInitialState() => State.Create(Settings);
        }

        private GameInfo __gameInfo;
        private GameInfo Game
        {
            get
            {
                if (__gameInfo.Ticker == null)
                {
                    var settings = BuildSettings();
                    var state = State.Create(settings);
                    var collector = new ListReplayCollector();
                    var ticker = new Ticker(state, collector);
                    __gameInfo = new GameInfo(settings, ticker, collector);
                }
                return __gameInfo;
            }
        }

        int? version = null;
        private PRNG.State? seed = null;
        private OfficialSettingsId? officialSettingsId = null;
        private GameMode? mode;
        private int? enemyCount;
        private bool? spawnBlanks;
        private int? gridWidth;
        private int? gridHeight;
        private int? enemiesPerStripe;
        private int? rowsPerStripe;
        private int? scorePerEnemy;

        private SeededSettings BuildSettings()
        {
            if (seed == null)
            {
                throw new InvalidReplayException("Missing random seed");
            }

            if (officialSettingsId.HasValue)
            {
                if (officialSettingsId.Value.Validate(out var officialSettings))
                {
                    return new SeededSettings(seed.Value, officialSettings);
                }
                else
                {
                    throw new InvalidReplayException($"Unrecognized official settings: {officialSettingsId}");
                }
            }

            var settings = new SinglePlayerSettings();
            settings.EnemyCount = this.enemyCount ?? settings.EnemyCount;
            settings.SpawnBlanks = this.spawnBlanks ?? settings.SpawnBlanks;
            settings.GridWidth = this.gridWidth ?? settings.GridWidth;
            settings.GridHeight = this.gridHeight ?? settings.GridHeight;
            settings.EnemiesPerStripe = this.enemiesPerStripe ?? settings.EnemiesPerStripe;
            settings.RowsPerStripe = this.rowsPerStripe ?? settings.RowsPerStripe;
            settings.ScorePerEnemy = this.scorePerEnemy ?? 100;
            settings.GameMode = this.mode ?? GameMode.Levels;
            return new SeededSettings(seed.Value, settings);
        }

        public void Parse(VersionElement v)
        {
            if (version != null)
            {
                throw new InvalidReplayException("Unexpected 2nd version element");
            }
            version = v.VersionNumber;
            if (version != -1)
            {
                throw new InvalidReplayException($"Unsupported version: {version}");
            }
        }

        public void Parse(SettingsElement setting)
        {
            if (setting.Name == "seed")
            {
                seed = PRNG.State.Deserialize(setting.Value);
            }
            if (setting.Name == "officialSettingsId")
            {
                officialSettingsId = new OfficialSettingsId(setting.Value);
            }
            if (setting.Name == "mode")
            {
                mode = setting.Value switch
                {
                    nameof(GameMode.Levels) => GameMode.Levels,
                    nameof(GameMode.ScoreAttack) => GameMode.ScoreAttack,
                    nameof(GameMode.PvPSim) => GameMode.PvPSim,
                    _ => throw new InvalidReplayException($"Unsupported mode: {setting.Value}"),
                };
            }
            if (setting.Name == "enemyCount")
            {
                enemyCount = int.Parse(setting.Value);
            }
            if (setting.Name == "spawnBlanks")
            {
                spawnBlanks = bool.Parse(setting.Value);
            }
            if (setting.Name == "gridWidth")
            {
                gridWidth = int.Parse(setting.Value);
            }
            if (setting.Name == "gridHeight")
            {
                gridHeight = int.Parse(setting.Value);
            }
            if (setting.Name == "enemiesPerStripe")
            {
                enemiesPerStripe = int.Parse(setting.Value);
            }
            if (setting.Name == "rowsPerStripe")
            {
                rowsPerStripe = int.Parse(setting.Value);
            }
            if (setting.Name == "scorePerEnemy")
            {
                scorePerEnemy = int.Parse(setting.Value);
            }
        }

        public void Parse(CommandElement command)
        {
            if (!Game.Ticker.HandleCommand(command.Command, command.Moment))
            {
                throw new InvalidReplayException($"HandleCommand failed: {command.Command} at {command.Moment}");
            }
        }

        public void Parse(GridHashElement gridHash)
        {
            var actual = Game.Ticker.state.HashGrid();
            if (actual != gridHash.HashValue)
            {
                throw new InvalidReplayException($"Grid hash check failed. Expected {gridHash.HashValue} but got {actual}");
            }
        }

        public ReplayDriver BuildReplayDriver()
        {
            var ticker = new Ticker(Game.CreateInitialState(), NullReplayCollector.Instance);
            return new ReplayDriver(ticker, Game.ReplayCollector.Commands);
        }

        public IReadOnlyList<Puzzle> GetRawPuzzles()
        {
            var ticker = new Ticker(Game.CreateInitialState(), NullReplayCollector.Instance);
            return Puzzle.FindRawPuzzles(ticker, Game.ReplayCollector.Commands);
        }

        public (SeededSettings settings, int finalScore) TODO()
        {
            return (Game.Settings, 0);
        }
    }
}
