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
    public override void _Ready()
    {
        // TODO delete this file and scene, and auto-startup idea
    }
}
