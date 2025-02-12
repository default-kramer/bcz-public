using BCZ.Core.ReplayModel;
using Godot;
using System;
using System.Collections.Generic;

#nullable enable

public class MainMenu : Control, IHelpText
{
    private Members members;

    readonly struct Members
    {
        public readonly Button ButtonSinglePlayer;
        public readonly Button ButtonTutorial;
        public readonly Button ButtonControllerSetup;
        public readonly Button ButtonWatchReplay;
        public readonly Button ButtonCredits;
        public readonly Control MainContainer;
        public readonly Control MenuSinglePlayer;
        public readonly FileDialog FileDialog;
        public readonly GameViewerControl GameViewerControl;
        private readonly Control CopyrightNotice;
        private readonly Label ExplanationLabel;

        public Members(Control me)
        {
            me.FindNode(out ButtonSinglePlayer, nameof(ButtonSinglePlayer));
            me.FindNode(out ButtonTutorial, nameof(ButtonTutorial));
            me.FindNode(out ButtonControllerSetup, nameof(ButtonControllerSetup));
            me.FindNode(out ButtonWatchReplay, nameof(ButtonWatchReplay));
            me.FindNode(out ButtonCredits, nameof(ButtonCredits));
            me.FindNode(out MainContainer, nameof(MainContainer));
            me.FindNode(out MenuSinglePlayer, nameof(MenuSinglePlayer));
            me.FindNode(out FileDialog, nameof(FileDialog));
            me.FindNode(out GameViewerControl, nameof(GameViewerControl));
            me.FindNode(out CopyrightNotice, nameof(CopyrightNotice));
            me.FindNode(out ExplanationLabel, nameof(ExplanationLabel));
        }

        public void SetHelpText(string text)
        {
            CopyrightNotice.Visible = false;
            ExplanationLabel.Visible = true;
            ExplanationLabel.Text = text;
        }

        public void ShowCopyrightNotice()
        {
            ExplanationLabel.Visible = false;
            CopyrightNotice.Visible = true;
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
        members.ShowCopyrightNotice();
        SwitchTo(members.MainContainer);
    }

    /// <summary>
    /// Usually <see cref="ShowMainMenu"/> is enough, but apparently there is some weird quirk at startup.
    /// </summary>
    public void StartupSetFocus()
    {
        CallDeferred(nameof(SetFocusOnStartup));
    }

    private void SetFocusOnStartup()
    {
        members.ButtonSinglePlayer.GrabFocus();
    }

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        members = new Members(this);

        members.ButtonWatchReplay.Visible = OS.GetName() != "HTML5";

        // TODO - this replay continues to run in the background, that should be fixed
        // Probably want to SetProcess(false) on inactive components?
        members.GameViewerControl.WatchDemo();

        members.ButtonSinglePlayer.Connect("pressed", this, nameof(PressedSinglePlayer));
        members.ButtonTutorial.Connect("pressed", this, nameof(PressedTutorial));
        members.ButtonControllerSetup.Connect("pressed", this, nameof(PressedControllerSetup));
        members.ButtonWatchReplay.Connect("pressed", this, nameof(PressedWatchReplay));
        members.ButtonCredits.Connect("pressed", this, nameof(PressedCredits));
        members.FileDialog.Connect("file_selected", this, nameof(OnFileSelected));

        SwitchTo(members.MainContainer);
        members.ButtonSinglePlayer.GrabFocus();
    }

    private void OnFileSelected(string path)
    {
        var root = NewRoot.FindRoot(this);
        try
        {
            root.WatchReplay(path);
            members.FileDialog.Hide();
        }
        catch (InvalidReplayException ex)
        {
            root.BackToMainMenu();
            GD.PushWarning(ex.ToString());
            OS.Alert("Selected file is not valid.", "Invalid Replay");
        }
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
        fd.RectMinSize = new Vector2(600, 500);
        fd.Access = FileDialog.AccessEnum.Filesystem;
        fd.Mode = FileDialog.ModeEnum.OpenFile;
        string replayDir = System.Environment.GetEnvironmentVariable("ffreplaydir");
        if (replayDir != null)
        {
            fd.CurrentDir = replayDir;
        }
        fd.PopupCentered();
    }

    private void PressedCredits()
    {
        NewRoot.FindRoot(this).ShowCredits();
    }

    void IHelpText.SetText(string? text)
    {
        members.SetHelpText(text ?? "");
    }
}
