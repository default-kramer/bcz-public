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

        private static PuzzleReplayDriver RunToCompletion(ComboDistillery.Puzzle puzzle)
        {
            var driver = PuzzleReplayDriver.BuildPuzzleReplay(puzzle, new TickCalculations());

            var now = new Moment(500);
            while (!driver.IsDone)
            {
                driver.Advance(now);
                now = now.AddMillis(500);
            }

            return driver;
        }

        [TestMethod]
        public void Replay001()
        {
            var result = ParseReplay("001.ffr");
            Assert.AreEqual(201, result.Commands.Count);
        }

        [TestMethod]
        public void Puzzle002()
        {
            var puzzles = GetPuzzles("002.ffr");
            Assert.AreEqual(14, puzzles.Count);

            var biggest = puzzles.OrderByDescending(p => p.Combo.AdjustedGroupCount).First();
            Assert.AreEqual(5, biggest.Combo.NumVerticalGroups);
            Assert.AreEqual(1, biggest.Combo.NumHorizontalGroups);
            Assert.AreEqual(59492762, biggest.InitialGrid.HashGrid());

            var result = RunToCompletion(biggest);
            Assert.AreEqual(-1847731085, result.Ticker.state.Grid.HashGrid());
            Assert.IsTrue(result.Ticker.state.Grid.CheckGridString(@"
   rr                   
   rr                   
   RR                   
                     BB 
   RR    YY       BB    
   YY                RR 
               RR       
BB    RR RR BB YY    YY 
RR RR BB       YY       
   BB BB    YY          
YY YY             BB    
   RR RR       YY    BB "));
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
