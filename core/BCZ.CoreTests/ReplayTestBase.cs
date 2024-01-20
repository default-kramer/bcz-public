using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BCZ.Core;
using BCZ.Core.ReplayModel;

namespace BCZ.CoreTests
{
    public class ReplayTestBase
    {
        protected static readonly DirectoryInfo ReplayDirectory = FindReplayDirectory();

        protected static ReplayDriver ParseReplay(string path)
        {
            path = Path.Combine(ReplayDirectory.FullName, path);
            return ReplayReader.BuildReplayDriver(path);
        }

        private static DirectoryInfo FindReplayDirectory()
        {
            var startLoc = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var currentDir = new DirectoryInfo(Path.GetDirectoryName(startLoc)!);

            while (currentDir.Parent != null)
            {
                if (currentDir.Name == "BCZ.CoreTests")
                {
                    var replayDir = new DirectoryInfo(Path.Combine(currentDir.FullName, "Replays"));
                    if (!replayDir.Exists)
                    {
                        throw new Exception($"Replay directory does not exist: {replayDir.FullName}");
                    }
                    return replayDir;
                }
                currentDir = currentDir.Parent;
            }

            throw new Exception($"Could not find replay directory. Started from {startLoc}");
        }
    }
}
