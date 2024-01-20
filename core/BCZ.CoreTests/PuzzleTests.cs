using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BCZ.Core;
using BCZ.Core.ReplayModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BCZ.CoreTests
{
    [TestClass]
    public class PuzzleTests : ReplayTestBase
    {
        private static (Puzzle, Puzzle) Get(string path, int index)
        {
            var raw = GetRawPuzzles(path)[index];
            var distilled = raw.Distill();
            return (raw, distilled);
        }

        /// <summary>
        /// Returns one raw puzzle per combo in the original game.
        /// This makes it safe to grab the Nth puzzle and know it will always be the same.
        /// </summary>
        private static IReadOnlyList<Puzzle> GetRawPuzzles(string path)
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
        public void Puzzle002_all_enemies_cleared()
        {
            // Make sure that every puzzle has more than zero enemies, and that the solution clears them all.
            var rawPuzzles = GetRawPuzzles("002.ffr");
            Assert.AreEqual(14, rawPuzzles.Count);
            foreach (var raw in rawPuzzles)
            {
                var distilled = raw.Distill();
                Assert.IsTrue(distilled.InitialGrid.CountEnemies() > 0);

                var resultGrid = distilled.TEMP_ResultGrid();
                Assert.AreEqual(0, resultGrid.CountEnemies());
            }
        }

        [TestMethod]
        public void Puzzle002_10()
        {
            var raw = GetRawPuzzles("002.ffr")[10];
            Assert.AreEqual(5, raw.LastCombo.PermissiveCombo.NumVerticalGroups);
            Assert.AreEqual(1, raw.LastCombo.PermissiveCombo.NumHorizontalGroups);
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

            // Better code means we were able to remove a useless move
            Assert.AreEqual(8, raw.Moves.Count);
            Assert.AreEqual(7, distilled.Moves.Count);

            Assert.AreEqual("ok", distilled.DiffMoves(@"
                  <o y> 
                  rr    
                  rr    
            <y b>       
      bb                
      bb                
      oo                
      bb                
               <b r>    
      <y y>             "));

            Assert.AreEqual("ok", distilled.DiffGrid(@"
                  oo yy 
                  RR YY 
                     YY 
            <y b>       
   YY          BB       
      BB    yy          
            YY          "));
        }

        [TestMethod]
        public void Puzzle003()
        {
            // A simple regression test to make sure we're updating paired occupants correctly
            // when one half of the pair is removed/replaced.
            var puzzle = GetRawPuzzles("003.ffr")[1];
            Assert.AreEqual(5, puzzle.LastCombo.PermissiveCombo.NumVerticalGroups);
            Assert.AreEqual(0, puzzle.LastCombo.PermissiveCombo.NumHorizontalGroups);
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
            Assert.AreEqual("ok", distilled.DiffMoves(@"
   <y y>                
      <y b>             
            rr          
            rr          
         <b r>          
            yy          
            oo          "));
            Assert.AreEqual("ok", distilled.DiffGrid(@"
            yy          
   yy       yy          
   yy    bb YY          
   YY yy BB             
      YY                
            RR          "));
        }

        [TestMethod]
        public void Puzzle004_4()
        {
            // A simple regression where useless blanks were being left on the grid.
            var raw = GetRawPuzzles("004.ffr")[4];
            var distilled = raw.Distill()!;
            Assert.AreEqual("ok", distilled.DiffGrid(@"
                     bb 
                     bb 
                     BB 
            yy          
            yy          
            YY rr RR RR "));
        }

        [TestMethod]
        public void Puzzle005_8()
        {
            // A simple regression where useless blanks were being left on the grid.
            var raw = GetRawPuzzles("005.ffr")[8];
            var distilled = raw.Distill()!;
            Assert.AreEqual("ok", distilled.DiffGrid(@"
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
            Assert.AreEqual("ok", distilled.DiffGrid(@"
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
            Assert.AreEqual("ok", distilled.DiffGrid(@"
            rr          
            RR          
            yy          
            YY          
         bb BB          "));
        }

        [TestMethod]
        public void Puzzle007_1()
        {
            // This is a good example of a puzzle having all-catalyst groups.
            // Well, it *was* until I improved the distillery. Not so much anymore.
            // Now it is a nice regression that when the last move of the original combo is deemed useless
            // and removed, we still usually want the last move of the distilled combo to be a burst.
            var (raw, distilled) = Get("007.ffr", 1);

            // First make sure the grid looks as expected, otherwise the other assertions will most likely fail.
            Assert.AreEqual("ok", distilled.DiffMoves(@"
      <o b>             
      <y b>             
         <b b>          
            <o y>       
            <b y>       
            <b y>       "));
            Assert.AreEqual("ok", distilled.DiffGrid(@"
            BB          
                        
               YY       
         BB             "));

            Assert.AreEqual(12, raw.Moves.Count);
            Assert.AreEqual(6, distilled.Moves.Count);
            Assert.AreNotEqual(distilled.Moves.Last(), raw.Moves.Last());
            Assert.IsTrue(distilled.Moves.Last().DidBurst);
            Assert.IsTrue(raw.Moves.Last().DidBurst);
            Assert.AreEqual(1, raw.Moves.Where(m => m.DidBurst).Count());
            Assert.AreEqual(1, distilled.Moves.Where(m => m.DidBurst).Count());
        }

        [TestMethod]
        public void Puzzle008_7()
        {
            // Regression - we were failing to do the rr->RR conversion on the bottom row
            var (raw, distilled) = Get("008.ffr", 7);

            Assert.AreEqual("ok", distilled.DiffMoves(@"
yy                      
yy                      
   rr                   
   rr                   
oo                      
yy                      
      <b y>             
      oo                
      bb                
   <r b>                "));

            Assert.AreEqual("ok", distilled.DiffGrid(@"
YY                      
   oo                   
   RR                   
      BB                "));
        }

        [TestMethod]
        public void Puzzle009_1()
        {
            // On the left side, we change an <r y> into RR yy to avoid an all-catalyst red group.
            var (raw, distilled) = Get("009.ffr", 1);

            Assert.AreEqual("ok", distilled.DiffMoves(@"
<b r>                   
               yy       
               oo       
      yy                
      yy                
<b r>                   
                     bb 
                     oo 
   <r o>                
               <y b>    "));
            Assert.AreEqual("ok", distilled.DiffGrid(@"
                     bb 
               yy    bb 
               YY    BB 
   RR yy                
bb    YY                
BB                      "));
        }

        [TestMethod]
        public void ensure_no_duplicate_replay_files()
        {
            // Make sure I don't put the same replay file in here multiple times

            var dict = new Dictionary<long, List<FileInfo>>(); // key is file length
            foreach (var file in ReplayDirectory.EnumerateFiles("*.ffr"))
            {
                var key = file.Length;
                if (!dict.ContainsKey(key))
                {
                    dict[key] = new List<FileInfo>();
                }
                dict[key].Add(file);
            }
            Assert.IsTrue(dict.Count > 5);

            foreach (var key in dict.Keys)
            {
                var files = dict[key];
                for (int i = 0; i < files.Count; i++)
                {
                    for (int j = i + 1; j < files.Count; j++)
                    {
                        var iName = files[i].FullName;
                        var jName = files[j].FullName;
                        var iText = File.ReadAllText(iName);
                        var jText = File.ReadAllText(jName);
                        if (iText == jText)
                        {
                            Assert.Fail($"Duplicate file: {iName} and {jName}");
                        }
                    }
                }
            }
        }
    }
}
