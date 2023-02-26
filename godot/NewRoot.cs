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
        public readonly TutorialControl TutorialControl;

        public Members(Control me)
        {
            me.FindNode(out GameViewer, nameof(GameViewer));
            me.FindNode(out PuzzleControl, nameof(PuzzleControl));
            me.FindNode(out MainMenu, nameof(MainMenu));
            me.FindNode(out ControllerSetupControl, nameof(ControllerSetupControl));
            me.FindNode(out TutorialControl, nameof(TutorialControl));
        }
    }

    private Members members;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        var userAgent = JavaScript.Eval("navigator.userAgent") as string;
        if (userAgent != null)
        {
            Console.WriteLine("Got user agent: " + userAgent);
        }

        this.members = new Members(this);
        BackToMainMenu();
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

    private static void Remove(Control c)
    {
        c.GetParent()?.RemoveChild(c);
    }

    // It's difficult to remove children without leaking them at shutdown.
    // This seems to work just as well:
    private static void SetEnabled(Control c, bool enabled)
    {
        c.Visible = enabled;
        c.SetProcess(enabled);
        c.SetProcessInput(enabled);
    }

    private void SwitchTo(Control control)
    {
        SetEnabled(members.GameViewer, false);
        SetEnabled(members.PuzzleControl, false);
        SetEnabled(members.MainMenu, false);
        SetEnabled(members.ControllerSetupControl, false);
        SetEnabled(members.TutorialControl, false);

        SetEnabled(control, true);
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

    public void StartTutorial()
    {
        SwitchTo(members.TutorialControl);
        members.TutorialControl.Reset();
    }
}
