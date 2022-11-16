using FF2.Core;
using FF2.Godot;
using Godot;
using System;

#nullable enable

public class NewRoot : Control
{
    readonly struct Members
    {
        public readonly SpritePool SpritePool;
        public readonly GameViewerControl GameViewer;
        public readonly PuzzleControl PuzzleControl;
        public readonly MainMenu MainMenu;

        public Members(Control me)
        {
            SpritePool = new SpritePool(me, SpriteKind.Single, SpriteKind.Joined,
                SpriteKind.Enemy, SpriteKind.BlankJoined, SpriteKind.BlankSingle, SpriteKind.Heart,
                SpriteKind.Heart0, SpriteKind.Heart25, SpriteKind.Heart50, SpriteKind.Heart75, SpriteKind.Heart100, SpriteKind.Heartbreaker);

            me.FindNode(out GameViewer, nameof(GameViewer));
            me.FindNode(out PuzzleControl, nameof(PuzzleControl));
            me.FindNode(out MainMenu, nameof(MainMenu));
        }

        public void HideAll()
        {
            GameViewer.Visible = false;
            PuzzleControl.Visible = false;
            MainMenu.Visible = false;
        }
    }

    private Members members;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        this.members = new Members(this);
        BackToMainMenu();
    }

    internal static SpritePool GetSpritePool(Node child)
    {
        return FindRoot(child).members.SpritePool;
    }

    internal static NewRoot FindRoot(Node child)
    {
        if (child == null)
        {
            throw new Exception("Failed to find root node");
        }
        if (child is NewRoot me)
        {
            return me;
        }
        return FindRoot(child.GetParent());
    }

    private SinglePlayerMenu.LevelToken? levelToken;

    public void StartGame(SinglePlayerMenu.LevelToken levelToken)
    {
        this.levelToken = levelToken;
        StartGame(levelToken.CreateSeededSettings());
    }

    public bool CanAdvanceToNextLevel()
    {
        return levelToken.HasValue && levelToken.Value.CanAdvance;
    }

    public void AdvanceToNextLevel()
    {
        if (!CanAdvanceToNextLevel() || levelToken == null)
        {
            throw new InvalidOperationException("cannot advance");
        }

        if (levelToken.Value.NextLevel(out var nextToken))
        {
            StartGame(nextToken);
        }
        else
        {
            throw new Exception("TODO");
        }
    }

    public void ReplayCurrentLevel()
    {
        if (levelToken == null)
        {
            throw new InvalidOperationException("cannot replay");
        }
        StartGame(levelToken.Value);
    }

    private void StartGame(SeededSettings ss)
    {
        members.HideAll();
        members.GameViewer.Visible = true;
        members.GameViewer.StartGame(ss);
    }

    public void BackToMainMenu()
    {
        members.HideAll();
        members.MainMenu.Visible = true;
        members.MainMenu.ShowMainMenu();
    }

    public void WatchReplay(string replayFile)
    {
        members.HideAll();
        members.GameViewer.Visible = true;
        members.GameViewer.WatchReplay(replayFile);
    }

    public void SolvePuzzles()
    {
        members.HideAll();
        members.PuzzleControl.Visible = true;
        members.PuzzleControl.TEST_SolvePuzzle();
    }
}
