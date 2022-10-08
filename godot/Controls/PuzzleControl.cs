using FF2.Core;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public class PuzzleControl : Control
{
    readonly struct Members
    {
        public readonly GameViewerControl GameViewer1;
        public readonly GameViewerControl GameViewer2;

        public Members(Control me)
        {
            me.FindNode(out GameViewer1, nameof(GameViewer1));
            me.FindNode(out GameViewer2, nameof(GameViewer2));
        }
    }

    private Members members;

    public override void _Ready()
    {
        members = new Members(this);
    }

    public void TEST_SolvePuzzle()
    {
        //string path = @"C:\fission-flare-recordings\curated-puzzles\20220930_122640_2130838410-1956952547-1089322391-2206903227-2910161783-3276923346.ffr";
        //var puzzles = FF2.Core.ReplayModel.ReplayReader.GetPuzzles(path);
        //puzzles = puzzles.Select(x => x.Distill().Value).ToList();
        //SolvePuzzles(puzzles);

        var puzzles = new List<Puzzle>();
        var dir = new System.IO.DirectoryInfo(@"C:\fission-flare-recordings\raw");
        foreach (var file in dir.EnumerateFiles("*.ffr"))
        {
            try
            {
                var temp = FF2.Core.ReplayModel.ReplayReader.GetPuzzles(file.FullName);
                puzzles.AddRange(temp.Select(x => x.Distill())
                    .Where(x => x.HasValue)
                    .Select(x => x.Value)
                    .Where(x => x.Combo.AdjustedGroupCount >= 3));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        SolvePuzzles(puzzles);
    }

    public void SolvePuzzles(IReadOnlyList<Puzzle> puzzles)
    {
        members.GameViewer1.SolvePuzzles(puzzles);
        members.GameViewer1.Visible = true;
        members.GameViewer1.ShowPenalties = false;
        members.GameViewer1.ShowQueue = true;

        // TODO show hints on 2nd game viewer
        members.GameViewer2.Visible = false;
    }
}
