using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FF2.Core;
using FF2.Core.ReplayModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FF2.CoreTests
{
    [TestClass]
    public class ReplayTests
    {
        private static ReplayDriver ParseReplay(string path)
        {
            path = Path.Combine(ReplayDirectory.FullName, path);
            return ReplayReader.BuildReplayDriver(path, new TickCalculations());
        }

        private static IReadOnlyList<ComboDistillery.Puzzle> GetPuzzles(string path)
        {
            path = Path.Combine(ReplayDirectory.FullName, path);
            return ReplayReader.GetPuzzles(path);
        }

        [TestMethod]
        public void Replay001()
        {
            var result = ParseReplay("001.ffr");
            Assert.AreEqual(201, result.Commands.Count);
        }

        [TestMethod]
        public void TODO()
        {
            var puzzles = GetPuzzles("001.ffr");
            Assert.AreEqual(10, puzzles.Count);
            Assert.Fail("TODO: need more meaningful assertions here");
        }

        private static readonly DirectoryInfo ReplayDirectory = FindReplayDirectory();

        private static DirectoryInfo FindReplayDirectory()
        {
            var startLoc = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var currentDir = new DirectoryInfo(Path.GetDirectoryName(startLoc)!);

            while (currentDir.Parent != null)
            {
                if (currentDir.Name == "FF2.CoreTests")
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
