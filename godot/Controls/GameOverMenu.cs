using Godot;
using System;

#nullable enable

public class GameOverMenu : Control
{
    private Button ButtonNext = null!;
    private Button ButtonReplay = null!;
    private Button ButtonQuit = null!;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        var node = FindNode("VBoxContainer");
        this.ButtonNext = node.GetNode<Button>("ButtonNext");
        this.ButtonReplay = node.GetNode<Button>("ButtonReplay");
        this.ButtonQuit = node.GetNode<Button>("ButtonQuit");

        ButtonReplay.GrabFocus();
    }
}
