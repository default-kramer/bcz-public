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

        private static (UnsolvedPuzzle, Puzzle) Get(string path, int index)
        {
            var raw = GetRawPuzzles(path)[index];
            var distilled = raw.Distill();
            if (distilled == null)
            {
                throw new Exception("Distill() returned null");
            }
            return (raw, distilled);
        }

        /// <summary>
        /// Returns one raw puzzle per combo in the original game.
        /// This makes it safe to grab the Nth puzzle and know it will always be the same.
        /// </summary>
        private static IReadOnlyList<UnsolvedPuzzle> GetRawPuzzles(string path)
        {
            path = Path.Combine(ReplayDirectory.FullName, path);
            return ReplayReader.GetRawPuzzles(path);
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
            var rawPuzzles = GetRawPuzzles("002.ffr");
            Assert.AreEqual(14, rawPuzzles.Count);
            foreach (var raw in rawPuzzles)
            {
                // Wait, does Distill() ever return null anymore??
                var distilled = raw.Distill()!;
                // TODO should filter out puzzles with zero enemies?
                // Or always change at least 1 catalyst to an enemy?
                //Assert.IsTrue(distilled.InitialGrid.CountEnemies() > 0);

                var resultGrid = distilled.ResultGrid;
                Assert.AreEqual(0, resultGrid.CountEnemies());
                if (resultGrid.Count(OccupantKind.Enemy) > 10)
                {
                    var asdf = 99;
                }
            }
        }

        [TestMethod]
        public void Puzzle002_10()
        {
            var raw = GetRawPuzzles("002.ffr")[10];
            Assert.AreEqual(5, raw.OriginalCombo.PermissiveCombo.NumVerticalGroups);
            Assert.AreEqual(1, raw.OriginalCombo.PermissiveCombo.NumHorizontalGroups);
            Assert.AreEqual(59492762, raw.InitialGrid.HashGrid());

            Assert.AreEqual("ok", raw.InitialGrid.DiffGridString(@"
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

            var distilled = raw.Distill()!;

            Assert.IsTrue(distilled.CheckString(@"
                  <o y> 
                  rr    
                  rr    
            <y b>       
      bb                
      bb                
      oo                
      bb                
               <b r>    
               oo       
               bb       
      <y y>             
==|==|==|==|==|==|==|==|
                  oo oo 
                  RR yy 
                     YY 
                     YY 
            <y b>       
   YY          []       
      BB    yy          
            YY          "));
        }

        [TestMethod]
        public void Puzzle003()
        {
            // A simple regression test to make sure we're updating paired occupants correctly
            // when one half of the pair is removed/replaced.
            var puzzle = GetRawPuzzles("003.ffr")[1];
            Assert.AreEqual(5, puzzle.OriginalCombo.PermissiveCombo.NumVerticalGroups);
            Assert.AreEqual(0, puzzle.OriginalCombo.PermissiveCombo.NumHorizontalGroups);
            Assert.AreEqual("ok", puzzle.InitialGrid.DiffGridString(@"
            yy          
<b y>       yy          
<b y> yy bb YY          
   YY yy BB             
      YY BB       BB    
            RR    YY    
            RR BB YY    
            BB RR       "));

            var distilled = puzzle.Distill()!;
            Assert.AreEqual("ok", distilled.InitialGrid.DiffGridString(@"
            yy          
   yy       yy          
   yy yy bb YY          
   YY YY BB             
                        
            RR          "));
        }

        [TestMethod]
        public void Puzzle004_4()
        {
            // A simple regression where useless blanks were being left on the grid.
            var raw = GetRawPuzzles("004.ffr")[4];
            var distilled = raw.Distill()!;
            Assert.AreEqual("ok", distilled.InitialGrid.DiffGridString(@"
                     bb 
                     bb 
                     BB 
            yy          
            yy          
            YY RR RR RR "));
        }

        [TestMethod]
        public void Puzzle005_8()
        {
            // A simple regression where useless blanks were being left on the grid.
            var raw = GetRawPuzzles("005.ffr")[8];
            var distilled = raw.Distill()!;
            Assert.AreEqual("ok", distilled.InitialGrid.DiffGridString(@"
         rr             
         oo             
         rr             
         <r y>          
         RR          rr 
                     rr 
                  YY RR "));
        }

        [TestMethod]
        public void Puzzle006_8()
        {
            var raw = GetRawPuzzles("006.ffr")[8];
            var distilled = raw.Distill()!;
            // We convert bb->BB
            Assert.AreEqual("ok", distilled.InitialGrid.DiffGridString(@"
   yy RR                
   yy                   
bb YY                   
BB                      "));
        }

        [TestMethod]
        public void Puzzle006_11()
        {
            var raw = GetRawPuzzles("006.ffr")[11];
            var distilled = raw.Distill()!;
            // We convert rr->RR and yy->YY
            Assert.AreEqual("ok", distilled.InitialGrid.DiffGridString(@"
            rr          
            RR          
            yy          
            YY          
         BB BB          "));
        }

        [TestMethod]
        public void Puzzle007_1()
        {
            // This is a good example of a puzzle having all-catalyst groups.
            // The distillery provides this information so we can filter these puzzles out if we want.
            var (raw, distilled) = Get("007.ffr", 1);
            var rc = raw.OriginalCombo;
            var dc = distilled.LastCombo;

            // Note that both of them have at least 1 all-catalyst group
            Assert.AreEqual(2, dc.AllCatalystGroupCount);
            Assert.AreEqual(1, rc.AllCatalystGroupCount);
            // Permissive is always 4v0h
            Assert.AreEqual(4, dc.PermissiveCombo.NumVerticalGroups);
            Assert.AreEqual(0, dc.PermissiveCombo.NumHorizontalGroups);
            Assert.AreEqual(4, rc.PermissiveCombo.NumVerticalGroups);
            Assert.AreEqual(0, rc.PermissiveCombo.NumHorizontalGroups);
            // Strict is 3v0h raw but 2v0h distilled
            Assert.AreEqual(2, dc.StrictCombo.NumVerticalGroups);
            Assert.AreEqual(0, dc.StrictCombo.NumHorizontalGroups);
            Assert.AreEqual(3, rc.StrictCombo.NumVerticalGroups);
            Assert.AreEqual(0, rc.StrictCombo.NumHorizontalGroups);


            // Just making sure I have the correct puzzle:
            Assert.AreEqual("ok", distilled.InitialGrid.DiffGridString(@"
            BB          
                        
                        
         BB             "));
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
