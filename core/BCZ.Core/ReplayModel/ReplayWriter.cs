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
            writer.WriteLine($"s {SettingName.seed} {ss.Seed.Serialize()}");
            if (ss.SeedId.HasValue)
            {
                writer.WriteLine($"s {SettingName.seedId} {ss.SeedId.Value}");
            }
            if (settings.OfficialSettingsId.HasValue)
            {
                writer.WriteLine($"s {SettingName.officialSettingsId} {settings.OfficialSettingsId.Value.Id}");
            }
            else
            {
                writer.WriteLine($"s {SettingName.mode} {settings.GameMode}");
                writer.WriteLine($"s {SettingName.enemyCount} {settings.EnemyCount}");
                writer.WriteLine($"s {SettingName.spawnBlanks} {settings.SpawnBlanks}");
                writer.WriteLine($"s {SettingName.gridWidth} {settings.GridWidth}");
                writer.WriteLine($"s {SettingName.gridHeight} {settings.GridHeight}");
                writer.WriteLine($"s {SettingName.enemiesPerStripe} {settings.EnemiesPerStripe}");
                writer.WriteLine($"s {SettingName.rowsPerStripe} {settings.RowsPerStripe}");
                writer.WriteLine($"s {SettingName.scorePerEnemy} {settings.ScorePerEnemy}");
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
