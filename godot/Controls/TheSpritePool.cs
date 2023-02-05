using FF2.Core;
using Godot;
using System;

/// <summary>
/// We ensure that the sprite pool is created before everything else by making it an autoload singleton.
/// It should come first in the Project Settings.
/// See https://docs.godotengine.org/en/stable/tutorials/scripting/singletons_autoload.html
/// </summary>
public class TheSpritePool : Control
{
    private static int instanceCount = 0;

    internal SpritePool Pool { get; private set; }

    public override void _Ready()
    {
        instanceCount++;
        if (instanceCount > 1)
        {
            throw new Exception("Should not instantiate a 2nd sprite pool!");
        }

        Pool = new SpritePool(this, SpriteKind.Single, SpriteKind.Joined,
                SpriteKind.Enemy, SpriteKind.BlankJoined, SpriteKind.BlankSingle, SpriteKind.Heart,
                SpriteKind.Heart0); //, SpriteKind.Heart25, SpriteKind.Heart50, SpriteKind.Heart75, SpriteKind.Heart100, SpriteKind.Heartbreaker);
    }
}
