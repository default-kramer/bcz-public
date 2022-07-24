using Godot;
using System;
using System.Collections.Generic;

#nullable enable

public class MainMenu : Control
{
    private Control MainContainer = null!;
    private Control MenuSinglePlayer = null!;

    private Button ButtonSinglePlayer = null!;
    private Button ButtonMultiplayer = null!;
    private Button ButtonControllerSetup = null!;

    private void SwitchTo(Control control)
    {
        MainContainer.Visible = false;
        MenuSinglePlayer.Visible = false;
        control.Visible = true;

        var c = control.FindNextValidFocus();
        c.GrabFocus();
    }

    public void ShowMainMenu()
    {
        SwitchTo(MainContainer);
    }

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        MainContainer = GetNode<Control>("MainContainer");
        MenuSinglePlayer = GetNode<Control>("MenuSinglePlayer");

        var node = FindNode("VBoxContainer");
        ButtonSinglePlayer = node.GetNode<Button>("ButtonSinglePlayer");
        ButtonMultiplayer = node.GetNode<Button>("ButtonMultiplayer");
        ButtonControllerSetup = node.GetNode<Button>("ButtonControllerSetup");

        ButtonSinglePlayer.Connect("pressed", this, nameof(PressedSinglePlayer));
        ButtonMultiplayer.Connect("pressed", this, nameof(PressedMultiplayer));

        SwitchTo(MainContainer);
        ButtonSinglePlayer.GrabFocus();
    }

    private void PressedSinglePlayer()
    {
        SwitchTo(MenuSinglePlayer);
    }

    private void PressedMultiplayer()
    {
        Console.WriteLine("TODO show multiplayer menu");
    }
}
