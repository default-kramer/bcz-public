using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BCZ.Core.ReplayModel
{
    public sealed class ReplayWriter : IReplayCollector
    {
        private readonly TextWriter writer;
        private readonly bool shouldDispose;

        private ReplayWriter(TextWriter writer, bool shouldDispose)
        {
            this.writer = writer;
            this.shouldDispose = shouldDispose;
        }

        public static ReplayWriter Begin(TextWriter writer, SeededSettings ss, bool shouldDispose)
        {
            var settings = ss.Settings;

            writer.WriteLine("version -1"); // use negative numbers until I am ready to promise forward compatibility
            writer.WriteLine("s seed {0}", ss.Seed.Serialize());
            if (settings.OfficialSettingsId.HasValue)
            {
                writer.WriteLine("s officialSettingsId {0}", settings.OfficialSettingsId.Value.Id);
            }
            else
            {
                writer.WriteLine("s mode {0}", settings.GameMode);
                writer.WriteLine("s enemyCount {0}", settings.EnemyCount);
                writer.WriteLine("s spawnBlanks {0}", settings.SpawnBlanks);
                writer.WriteLine("s gridWidth {0}", settings.GridWidth);
                writer.WriteLine("s gridHeight {0}", settings.GridHeight);
                writer.WriteLine("s enemiesPerStripe {0}", settings.EnemiesPerStripe);
                writer.WriteLine("s rowsPerStripe {0}", settings.RowsPerStripe);
                writer.WriteLine("s scorePerEnemy {0}", settings.ScorePerEnemy);
            }

            return new ReplayWriter(writer, shouldDispose);
        }

        public void Collect(Stamped<Command> command)
        {
            int commandCode = (int)command.Value;
            writer.WriteLine("c {0} {1}", commandCode, command.Moment.Millis);
        }

        public void AfterCommand(Moment moment, State state)
        {
            writer.WriteLine("h {0}", state.HashGrid());
        }

        public void OnGameEnded()
        {
            if (shouldDispose)
            {
                writer.Flush();
                writer.Close();
                writer.Dispose();
            }
        }
    }
}
