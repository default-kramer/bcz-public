using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core.ReplayModel
{
    public sealed class ReplayWriter : IReplayCollector, IDisposable
    {
        private readonly TextWriter writer;

        private ReplayWriter(TextWriter writer)
        {
            this.writer = writer;
        }

        public static ReplayWriter Begin(TextWriter writer, SeededSettings ss)
        {
            var settings = ss.Settings;

            writer.WriteLine("version -1"); // use negative numbers until I am ready to promise forward compatibility
            writer.WriteLine("s seed {0}", ss.Seed.Serialize());
            writer.WriteLine("s enemyCount {0}", settings.EnemyCount);
            writer.WriteLine("s spawnBlanks {0}", settings.SpawnBlanks);
            writer.WriteLine("s gridWidth {0}", settings.GridWidth);
            writer.WriteLine("s gridHeight {0}", settings.GridHeight);
            writer.WriteLine("s enemiesPerStripe {0}", settings.EnemiesPerStripe);
            writer.WriteLine("s rowsPerStripe {0}", settings.RowsPerStripe);

            return new ReplayWriter(writer);
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

        public void Flush() { writer.Flush(); }
        public void Close() { writer.Close(); }
        public void Dispose() { writer.Dispose(); }
    }
}
