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

        var puzzles = new List<Puzzle>();
        //var dir = new System.IO.DirectoryInfo(@"C:\fission-flare-recordings\raw");
        var dir = new System.IO.DirectoryInfo(@"C:\Users\kramer\Documents\code\ff2\core\FF2.CoreTests\Replays");
        foreach (var file in dir.EnumerateFiles("*.ffr"))
        {
            try
            {
                var temp = FF2.Core.ReplayModel.ReplayReader.GetRawPuzzles(file.FullName);
                puzzles.AddRange(temp.Select(TryDistill)
                    .Where(x => x != null).Cast<Puzzle>()
                    .Where(x => x.OriginalCombo.AdjustedGroupCount >= 3));

                //Console.WriteLine($"Processed {file.FullName}, count is now {puzzles.Count}");

                if (puzzles.Count > 3)
                {
                    //break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        SolvePuzzles(puzzles);
    }

    static Puzzle? TryDistill(UnsolvedPuzzle puzzle)
    {
        try
        {
            return puzzle.Distill();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
            return null;
        }
    }

    public void SolvePuzzles(IReadOnlyList<Puzzle> puzzles)
    {
        puzzleProvider = new PuzzleProvider(puzzles, 0);
        DoPuzzle(puzzleProvider.Value.Puzzle);
    }

    private void DoPuzzle(Puzzle puzzle)
    {
        var state = new State(Grid.Clone(puzzle.InitialGrid), puzzle.MakeDeck());
        var ticker = new DotnetTicker(state, NullReplayCollector.Instance);
        var logic = new SolvePuzzleLogic(ticker, puzzle, this);

        members.GameViewer1.SetLogic(logic);
        members.GameViewer1.Visible = true;
        members.GameViewer1.ShowPenalties = false;
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
            DoPuzzle(puzzleProvider.Value.Puzzle);
        }
    }

    void PuzzleMenu.ILogic.RestartPuzzle()
    {
        if (puzzleProvider.HasValue)
        {
            HideMenu();
            DoPuzzle(puzzleProvider.Value.Puzzle);
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
                var targetScore = puzzle.ExpectedScore;
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
        private readonly IReadOnlyList<Puzzle> Puzzles;
        private readonly int Index;

        public PuzzleProvider(IReadOnlyList<Puzzle> puzzles, int index)
        {
            this.Puzzles = puzzles;
            this.Index = index;
        }

        public PuzzleProvider Next()
        {
            return new PuzzleProvider(Puzzles, (Index + 1) % Puzzles.Count);
        }

        public Puzzle Puzzle => Puzzles[Index];
    }
}
