using Godot;
using System;
using System.Collections.Generic;

#nullable enable

public class MainMenu : Control
{
    private Members members;

    readonly struct Members
    {
        public readonly Button ButtonSinglePlayer;
        public readonly Button ButtonTutorial;
        public readonly Button ButtonControllerSetup;
        public readonly Button ButtonWatchReplay;
        public readonly Button ButtonSolvePuzzles;
        public readonly Control MainContainer;
        public readonly Control MenuSinglePlayer;
        public readonly Control FileDialog;
        public readonly GameViewerControl GameViewerControl;

        public Members(Control me)
        {
            me.FindNode(out ButtonSinglePlayer, nameof(ButtonSinglePlayer));
            me.FindNode(out ButtonTutorial, nameof(ButtonTutorial));
            me.FindNode(out ButtonControllerSetup, nameof(ButtonControllerSetup));
            me.FindNode(out ButtonWatchReplay, nameof(ButtonWatchReplay));
            me.FindNode(out ButtonSolvePuzzles, nameof(ButtonSolvePuzzles));
            me.FindNode(out MainContainer, nameof(MainContainer));
            me.FindNode(out MenuSinglePlayer, nameof(MenuSinglePlayer));
            me.FindNode(out FileDialog, nameof(FileDialog));
            me.FindNode(out GameViewerControl, nameof(GameViewerControl));
        }
    }

    private void SwitchTo(Control control)
    {
        members.MainContainer.Visible = false;
        members.MenuSinglePlayer.Visible = false;
        control.Visible = true;

        var c = control.FindNextValidFocus();
        c.GrabFocus();
    }

    public void ShowMainMenu()
    {
        SwitchTo(members.MainContainer);
    }

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        members = new Members(this);
        members.ButtonWatchReplay.Visible = Util.IsSuperuser;

        try
        {
            // TODO - this replay continues to run in the background, that should be fixed
            // Probably want to SetProcess(false) on inactive components
            members.GameViewerControl.WatchReplay(@"C:\fission-flare-recordings\raw\20230212_161427_1990999595-197713288-3821534300-4226900136-1326336242-3242883821.ffr");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }

        members.ButtonSinglePlayer.Connect("pressed", this, nameof(PressedSinglePlayer));
        members.ButtonTutorial.Connect("pressed", this, nameof(PressedTutorial));
        members.ButtonControllerSetup.Connect("pressed", this, nameof(PressedControllerSetup));
        members.ButtonWatchReplay.Connect("pressed", this, nameof(PressedWatchReplay));
        members.ButtonSolvePuzzles.Connect("pressed", this, nameof(PressedSolvePuzzles));
        members.FileDialog.Connect("file_selected", this, nameof(OnFileSelected));

        SwitchTo(members.MainContainer);
        members.ButtonSinglePlayer.GrabFocus();
    }

    private void OnFileSelected(string path)
    {
        members.FileDialog.Hide();
        NewRoot.FindRoot(this).WatchReplay(path);
    }

    private void PressedSinglePlayer()
    {
        SwitchTo(members.MenuSinglePlayer);
    }

    private void PressedTutorial()
    {
        NewRoot.FindRoot(this).StartTutorial();
    }

    public void PressedControllerSetup()
    {
        NewRoot.FindRoot(this).ControllerSetup();
    }

    private void PressedWatchReplay()
    {
        var fd = members.FileDialog;
        fd.RectSize = this.RectSize;
        fd.Set("access", 2); // access the whole filesystem
        fd.Set("mode", 0); // select one and only one file
        string replayDir = System.Environment.GetEnvironmentVariable("ffreplaydir");
        if (replayDir != null)
        {
            fd.Set("current_dir", replayDir);
        }
        fd.Call("popup_centered");
    }

    private void PressedSolvePuzzles()
    {
        NewRoot.FindRoot(this).SolvePuzzles();
    }
}
