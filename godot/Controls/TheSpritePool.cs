using FF2.Core;
using FF2.Godot;
using Godot;
using System;

public class TheSpritePool : Control
{
    public static TheSpritePool Instance;
    internal SpritePool Pool { get; private set; }

    public override void _Ready()
    {
        if (Instance != null)
        {
            throw new Exception("Should not instantiate a 2nd sprite pool!");
        }

        Instance = this;

        Pool = new SpritePool(this, SpriteKind.Single, SpriteKind.Joined,
                SpriteKind.Enemy, SpriteKind.BlankJoined, SpriteKind.BlankSingle, SpriteKind.Heart,
                SpriteKind.Heart0); //, SpriteKind.Heart25, SpriteKind.Heart50, SpriteKind.Heart75, SpriteKind.Heart100, SpriteKind.Heartbreaker);
    }

    protected override void Dispose(bool disposing)
    {
        Instance = null;
        base.Dispose(disposing);
    }
}
