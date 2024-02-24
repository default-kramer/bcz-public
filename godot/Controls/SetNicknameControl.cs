using Godot;
using System;

public class SetNicknameControl : CenterContainer
{
    public override void _Ready()
    {
        this.FindNode(out Button btn, "Button");
        btn.Connect("pressed", this, nameof(ButtonPressed));
    }

    private void ButtonPressed()
    {
        var root = (NewRoot)NewRoot.FindRoot(this);
        root.ConfirmNickname("desktop-test", playAnon: false, playOffline: false);
    }
}
