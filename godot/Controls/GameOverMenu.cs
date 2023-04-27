using BCZ.Core;
using Godot;
using System;

#nullable enable

/*
 * Sizing Notes: The VBox container is used to limit the main content to the top 2/3 of the screen.
 * Its children set
 * - Size Flags
 *   - Vertical: Fill, Expand
 *   - Stretch Ratio: 1/3 or 2/3
 */

public class GameOverMenu : Control
{
    readonly struct Members
    {
        public readonly Label LabelMessage;
        public readonly Button ButtonNext;
        public readonly Button ButtonReplay;
        public readonly Button ButtonQuit;

        public Members(GameOverMenu parent)
        {
            parent.FindNode(out LabelMessage, nameof(LabelMessage));
            parent.FindNode(out ButtonNext, nameof(ButtonNext));
            parent.FindNode(out ButtonReplay, nameof(ButtonReplay));
            parent.FindNode(out ButtonQuit, nameof(ButtonQuit));
        }
    }

    private Members members;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        members = new Members(this);
        members.ButtonNext.Connect("pressed", this, nameof(NextLevel));
        members.ButtonReplay.Connect("pressed", this, nameof(ReplayLevel));
        members.ButtonQuit.Connect("pressed", this, nameof(Quit));
    }

    public void OnGameOver(State state, GamePackage gamePackage)
    {
        this.Visible = true;
        var root = NewRoot.FindRoot(this);

        if (state.ClearedAllEnemies)
        {
            members.LabelMessage.Text = $"You Win!";
        }
        else
        {
            members.LabelMessage.Text = "Game Over";
        }

        if (state.ClearedAllEnemies && root.CanAdvanceToNextLevel())
        {
            members.ButtonNext.Visible = true;
            members.ButtonNext.GrabFocus();
        }
        else
        {

            members.ButtonNext.Visible = false;
            members.ButtonReplay.GrabFocus();
        }
    }

    private void NextLevel()
    {
        NewRoot.FindRoot(this).AdvanceToNextLevel();
    }

    private void ReplayLevel()
    {
        NewRoot.FindRoot(this).ReplayCurrentLevel();
    }

    private void Quit()
    {
        NewRoot.FindRoot(this).BackToMainMenu();
    }
}
