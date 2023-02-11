using FF2.Core;
using Godot;
using System;

#nullable enable

public class NewRoot : Control
{
    readonly struct Members
    {
        public readonly GameViewerControl GameViewer;
        public readonly PuzzleControl PuzzleControl;
        public readonly MainMenu MainMenu;
        public readonly ControllerSetupControl ControllerSetupControl;

        public Members(Control me)
        {
            me.FindNode(out GameViewer, nameof(GameViewer));
            me.FindNode(out PuzzleControl, nameof(PuzzleControl));
            me.FindNode(out MainMenu, nameof(MainMenu));
            me.FindNode(out ControllerSetupControl, nameof(ControllerSetupControl));
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
        var obj = child.GetNode("/root/TheSpritePool");
        var sp = (TheSpritePool)obj;
        return sp.Pool;
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

    private void SwitchTo(Control control)
    {
        // Removing children is better than just hiding them, probably for many reasons.
        // One reason is so that the input events aren't constantly routing to the Controller Setup code.

        var me = this;
        while (me.GetChildCount() > 0)
        {
            me.RemoveChild(me.GetChild(0));
        }
        control.Visible = true;
        me.AddChild(control);
    }

    private void StartGame(SeededSettings ss)
    {
        SwitchTo(members.GameViewer);
        members.GameViewer.StartGame(ss);
    }

    public void BackToMainMenu()
    {
        SwitchTo(members.MainMenu);
        members.MainMenu.ShowMainMenu();
    }

    public void WatchReplay(string replayFile)
    {
        SwitchTo(members.GameViewer);
        members.GameViewer.WatchReplay(replayFile);
    }

    public void SolvePuzzles()
    {
        SwitchTo(members.PuzzleControl);
        members.PuzzleControl.TEST_SolvePuzzle();
    }

    public void ControllerSetup()
    {
        SwitchTo(members.ControllerSetupControl);
        members.ControllerSetupControl.Reset();
    }
}
