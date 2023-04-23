using BCZ.Core;
using Godot;
using System;

#nullable enable

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
            var node = parent.FindNode("VBoxContainer");
            this.LabelMessage = node.GetNode<Label>("LabelMessage");
            this.ButtonNext = node.GetNode<Button>("ButtonNext");
            this.ButtonReplay = node.GetNode<Button>("ButtonReplay");
            this.ButtonQuit = node.GetNode<Button>("ButtonQuit");
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
