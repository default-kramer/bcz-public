using FF2.Core;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

public class PuzzleControl : Control, PuzzleMenu.ILogic
{
    readonly struct Members
    {
        public readonly GameViewerControl GameViewer1;
        public readonly GameViewerControl GameViewer2;
        public readonly PuzzleMenu PuzzleMenu;

        public Members(Control me)
        {
            me.FindNode(out GameViewer1, nameof(GameViewer1));
            me.FindNode(out GameViewer2, nameof(GameViewer2));
            me.FindNode(out PuzzleMenu, nameof(PuzzleMenu));
        }
    }

    private Members members;
    private PuzzleProvider? puzzleProvider = null;

    public override void _Ready()
    {
        members = new Members(this);
        members.PuzzleMenu.Logic = this;
        members.PuzzleMenu.Visible = false;
    }

    private void OnSuccess()
    {
        members.PuzzleMenu.OnSuccess();
    }

    private void OnFailure()
    {
        members.PuzzleMenu.OnFailure();
    }

    public void TEST_SolvePuzzle()
    {
        //string path = @"C:\fission-flare-recordings\curated-puzzles\20220930_122640_2130838410-1956952547-1089322391-2206903227-2910161783-3276923346.ffr";
        //var puzzles = FF2.Core.ReplayModel.ReplayReader.GetPuzzles(path);
        //puzzles = puzzles.Select(x => x.Distill().Value).ToList();
        //SolvePuzzles(puzzles);

        //var dir = new System.IO.DirectoryInfo(@"C:\fission-flare-recordings\raw");
        var dir = new System.IO.DirectoryInfo(@"C:\Users\kramer\Documents\code\ff2\core\FF2.CoreTests\Replays");
        var puzzles = CollectPuzzles(dir);
        SolvePuzzles(puzzles);
    }

    private IReadOnlyList<PuzzleInfo> CollectPuzzles(System.IO.DirectoryInfo searchDir)
    {
        var collector = new List<PuzzleInfo>();
        var iter = searchDir.EnumerateFiles("*.ffr").GetEnumerator();
        // Grab 3 puzzles
        CollectPuzzles(iter, collector, 3);
        // Now continue collection of any more puzzles on a background thread
        var th = new System.Threading.Thread(() => CollectPuzzles(iter, collector, int.MaxValue));
        th.Start();
        return collector;
    }

    private void CollectPuzzles(IEnumerator<System.IO.FileInfo> replayFiles, List<PuzzleInfo> collector, int limit)
    {
        while (collector.Count < limit && replayFiles.MoveNext())
        {
            try
            {
                CollectPuzzles(replayFiles.Current, collector);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in {replayFiles.Current.FullName} - {ex.ToString()}");
            }
        }
    }

    private void CollectPuzzles(System.IO.FileInfo replayFile, List<PuzzleInfo> collector)
    {
        var temp = FF2.Core.ReplayModel.ReplayReader.GetRawPuzzles(replayFile.FullName);
        for (int i = 0; i < temp.Count; i++)
        {
            var item = temp[i];
            var d = item.Distill();
            if (d == null || d.LastCombo.PermissiveCombo.AdjustedGroupCount < 3)
            {
                continue;
            }

            // TEMP TESTING - see if there are any good puzzles with catalyst-only groups
            if (d.LastCombo.AllCatalystGroupCount == 0)
            {
                //continue;
            }

            var pi = new PuzzleInfo(d, $"{replayFile.FullName} :: {i}");
            collector.Add(pi);
        }
    }

    public void SolvePuzzles(IReadOnlyList<PuzzleInfo> puzzles)
    {
        puzzleProvider = new PuzzleProvider(puzzles, 0);
        DoPuzzle(puzzleProvider.Value.Puzzle, true);
    }

    private void DoPuzzle(PuzzleInfo puzzleInfo, bool firstTime)
    {
        if (firstTime)
        {
            puzzleInfo.BeforeShow();
        }

        var puzzle = puzzleInfo.Puzzle;
        var state = new State(Grid.Clone(puzzle.InitialGrid), puzzle.MakeDeck());
        var ticker = new DotnetTicker(state, NullReplayCollector.Instance);
        var logic = new SolvePuzzleLogic(ticker, puzzle, this);

        members.GameViewer1.SetLogic(logic);
        members.GameViewer1.Visible = true;
        // I had "ShowPenalties=false" which should probably become "ShowHealthGrid=false" or something
        members.GameViewer1.ShowQueue = true;

        // TODO show hints on 2nd game viewer
        members.GameViewer2.Visible = false;
    }

    private void HideMenu()
    {
        members.PuzzleMenu.Visible = false;
    }

    void PuzzleMenu.ILogic.NextPuzzle()
    {
        if (puzzleProvider.HasValue)
        {
            HideMenu();
            puzzleProvider = puzzleProvider.Value.Next();
            DoPuzzle(puzzleProvider.Value.Puzzle, true);
        }
    }

    void PuzzleMenu.ILogic.RestartPuzzle()
    {
        if (puzzleProvider.HasValue)
        {
            HideMenu();
            DoPuzzle(puzzleProvider.Value.Puzzle, false);
        }
    }

    void PuzzleMenu.ILogic.SkipPuzzle()
    {
        PuzzleMenu.ILogic logic = this;
        logic.NextPuzzle();
    }

    void PuzzleMenu.ILogic.BackToMainMenu()
    {
        HideMenu();
        NewRoot.FindRoot(this).BackToMainMenu();
    }

    class SolvePuzzleLogic : GameViewerControl.LogicBase
    {
        private readonly Puzzle puzzle;
        private readonly PuzzleControl parent;
        private bool gameOver = false;

        public SolvePuzzleLogic(DotnetTicker ticker, Puzzle puzzle, PuzzleControl parent)
            : base(ticker)
        {
            this.puzzle = puzzle;
            this.parent = parent;
        }

        public override void CheckGameOver()
        {
            var state = ticker.state;
            if (!gameOver && state.Kind == StateKind.GameOver)
            {
                gameOver = true;
                var targetScore = state.GetHypotheticalScore(puzzle.LastCombo);
                var userScore = state.Score;
                if (userScore >= targetScore)
                {
                    parent.OnSuccess();
                }
                else
                {
                    parent.OnFailure();
                }
            }
        }
    }

    readonly struct PuzzleProvider
    {
        private readonly IReadOnlyList<PuzzleInfo> Puzzles;
        private readonly int Index;

        public PuzzleProvider(IReadOnlyList<PuzzleInfo> puzzles, int index)
        {
            this.Puzzles = puzzles;
            this.Index = index;
        }

        public PuzzleProvider Next()
        {
            return new PuzzleProvider(Puzzles, (Index + 1) % Puzzles.Count);
        }

        public PuzzleInfo Puzzle => Puzzles[Index];
    }

    public sealed class PuzzleInfo
    {
        private readonly Puzzle puzzle;
        private readonly string info;

        public PuzzleInfo(Puzzle puzzle, string info)
        {
            this.puzzle = puzzle;
            this.info = info;
        }

        public Puzzle Puzzle => puzzle;

        public void BeforeShow()
        {
            Console.WriteLine("Starting puzzle: " + info);
        }
    }
}
