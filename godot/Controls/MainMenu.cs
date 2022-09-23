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
        public readonly Button ButtonMultiplayer;
        public readonly Button ButtonControllerSetup;
        public readonly Button ButtonWatchReplay;
        public readonly Control MainContainer;
        public readonly Control MenuSinglePlayer;
        public readonly Control FileDialog;

        public Members(Control me)
        {
            me.FindNode(out ButtonSinglePlayer, nameof(ButtonSinglePlayer));
            me.FindNode(out ButtonMultiplayer, nameof(ButtonMultiplayer));
            me.FindNode(out ButtonControllerSetup, nameof(ButtonControllerSetup));
            me.FindNode(out ButtonWatchReplay, nameof(ButtonWatchReplay));
            me.FindNode(out MainContainer, nameof(MainContainer));
            me.FindNode(out MenuSinglePlayer, nameof(MenuSinglePlayer));
            me.FindNode(out FileDialog, nameof(FileDialog));
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

        members.ButtonSinglePlayer.Connect("pressed", this, nameof(PressedSinglePlayer));
        members.ButtonMultiplayer.Connect("pressed", this, nameof(PressedMultiplayer));
        members.ButtonWatchReplay.Connect("pressed", this, nameof(PressedWatchReplay));
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

    private void PressedMultiplayer()
    {
        Console.WriteLine("TODO show multiplayer menu");
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
}
