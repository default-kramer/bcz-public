using Godot;
using System;

#nullable enable

public class PuzzleMenu : Control
{
    public ILogic Logic = null!; // parent should set this before it ever gets called

    readonly struct Members
    {
        public readonly Button ButtonNextPuzzle;
        public readonly Button ButtonRestartPuzzle;
        public readonly Button ButtonSkipPuzzle;
        public readonly MenuChoiceControl ChoiceHintsEnabled;
        public readonly Button ButtonQuitToTitle;

        public Members(Control me)
        {
            me.FindNode(out ButtonNextPuzzle, nameof(ButtonNextPuzzle));
            me.FindNode(out ButtonRestartPuzzle, nameof(ButtonRestartPuzzle));
            me.FindNode(out ButtonSkipPuzzle, nameof(ButtonSkipPuzzle));
            me.FindNode(out ChoiceHintsEnabled, nameof(ChoiceHintsEnabled));
            me.FindNode(out ButtonQuitToTitle, nameof(ButtonQuitToTitle));
        }
    }

    private Members members;

    const string HintsOn = "Hints: On";
    const string HintsOff = "Hints: Off";
    private readonly ChoiceModel<string> HintsEnabledChoices = new ChoiceModel<string>()
        .AddChoice(HintsOn)
        .AddChoice(HintsOff);

    public override void _Ready()
    {
        members = new Members(this);
        members.ChoiceHintsEnabled.Model = HintsEnabledChoices;
        members.ButtonNextPuzzle.Connect("pressed", this, nameof(NextPuzzle));
        members.ButtonRestartPuzzle.Connect("pressed", this, nameof(RestartPuzzle));
        members.ButtonSkipPuzzle.Connect("pressed", this, nameof(SkipPuzzle));
        members.ButtonQuitToTitle.Connect("pressed", this, nameof(BackToTitle));
    }

    public interface ILogic
    {
        void NextPuzzle();
        void RestartPuzzle();
        void SkipPuzzle();
        void BackToMainMenu();
    }

    public bool HintsEnabled => HintsEnabledChoices.SelectedItem == HintsOn;

    private void NextPuzzle()
    {
        Logic.NextPuzzle();
    }

    private void RestartPuzzle()
    {
        Logic.RestartPuzzle();
    }

    private void SkipPuzzle()
    {
        Logic.SkipPuzzle();
    }

    private void BackToTitle()
    {
        Logic.BackToMainMenu();
    }

    private void SetVisibility(bool success)
    {
        this.Visible = true;
        members.ButtonNextPuzzle.Visible = success;
        members.ButtonRestartPuzzle.Visible = true;
        members.ButtonSkipPuzzle.Visible = !success;
        members.ChoiceHintsEnabled.Visible = true;
        members.ButtonQuitToTitle.Visible = true;
    }

    public void OnSuccess()
    {
        SetVisibility(true);
        members.ButtonNextPuzzle.GrabFocus();
    }

    public void OnFailure()
    {
        SetVisibility(false);
        members.ButtonRestartPuzzle.GrabFocus();
    }
}
