using FF2.Core;
using FF2.Godot;
using Godot;
using System;

public class NewRoot : Control
{
    private SpritePool spritePool = null!;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        this.spritePool = new SpritePool(this, SpriteKind.Single, SpriteKind.Joined,
            SpriteKind.Enemy, SpriteKind.BlankJoined, SpriteKind.BlankSingle);
    }

    internal static SpritePool GetSpritePool(Node child)
    {
        if (child == null)
        {
            throw new Exception("Failed to find root node");
        }
        if (child is NewRoot me)
        {
            return me.spritePool;
        }
        return GetSpritePool(child.GetParent());
    }
}
