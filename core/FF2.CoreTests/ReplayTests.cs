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
            return ReplayReader.BuildReplayDriver(path);
        }

        private static IReadOnlyList<Puzzle> GetPuzzlesRaw(string path)
        {
            path = Path.Combine(ReplayDirectory.FullName, path);
            return ReplayReader.GetPuzzles(path);
        }

        private static Puzzle GetBiggestPuzzleRaw(string path)
        {
            return GetPuzzlesRaw(path).OrderByDescending(x => x.Combo.AdjustedGroupCount).First();
        }

        private static State RunToCompletion(Puzzle puzzle)
        {
            var driver = PuzzleReplayDriver.BuildPuzzleReplay(puzzle);
            driver.RunToCompletion();
            return driver.State;
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
            var rawPuzzles = GetPuzzlesRaw("002.ffr");
            Assert.AreEqual(14, rawPuzzles.Count);
            foreach (var raw in rawPuzzles)
            {
                var distilled = raw.Distill()!.Value;
                // TODO should filter out puzzles with zero enemies?
                // Or always change at least 1 catalyst to an enemy?
                //Assert.IsTrue(distilled.InitialGrid.CountEnemies() > 0);
                var result = RunToCompletion(distilled);
                Assert.AreEqual(0, result.Grid.CountEnemies());
                if (result.Grid.Count(OccupantKind.Enemy) > 10)
                {
                    var asdf = 99;
                }
            }
        }

        [TestMethod]
        public void Puzzle002_Distill()
        {
            var raw = GetBiggestPuzzleRaw("002.ffr");
            Assert.AreEqual(5, raw.Combo.NumVerticalGroups);
            Assert.AreEqual(1, raw.Combo.NumHorizontalGroups);
            Assert.AreEqual(59492762, raw.InitialGrid.HashGrid());

            Assert.IsTrue(raw.InitialGrid.CheckGridString(@"
                  oo oo 
                  rr yy 
   rr             RR YY 
   rr                YY 
   RR       <y b>       
   YY          BB    BB 
   RR BB YY yy    BB    
   YY       YY       RR 
               RR       
BB    RR RR BB YY    YY 
RR RR BB       YY       
   BB BB    YY          
YY YY             BB    
   RR RR       YY    BB "));

            var distilled = raw.Distill()!.Value;
            var asdf = RunToCompletion(distilled);

            Assert.IsTrue(distilled.InitialGrid.CheckGridString(@"
                  oo oo 
                  rr yy 
                  RR YY 
                     YY 
            <y b>       
   YY          BB       
      BB    yy          
            YY          "));
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
