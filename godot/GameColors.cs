using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Godot;
using Color = BCZ.Core.Color;

static class GameColors
{
    public static readonly Godot.Color Red = Godot.Color.Color8(255, 56, 120);
    public static readonly Godot.Color Blue = Godot.Color.Color8(0, 148, 255);
    public static readonly Godot.Color Yellow = Godot.Color.Color8(255, 255, 107);
    public static readonly Godot.Color Corrupt = Godot.Color.Color8(86, 41, 25);
    public static readonly Godot.Color Green = Godot.Color.Color8(60, 175, 36);
    public static readonly Godot.Color Bronze = Godot.Color.Color8(205, 127, 50);
    public static readonly Godot.Color Silver = Godot.Color.Color8(192, 192, 192);
    public static readonly Godot.Color Gold = Godot.Color.Color8(255, 215, 0);
    public static readonly Godot.Color Shroud = new Godot.Color(.21f, .36f, .34f, .7f);
    public static readonly Godot.Color OccupantDuringPause = Godot.Colors.DarkGray;

    public static readonly Vector3 RedV = ToVector(Red);
    public static readonly Vector3 BlueV = ToVector(Blue);
    public static readonly Vector3 YellowV = ToVector(Yellow);
    public static readonly Vector3 WhiteV = new Vector3(1, 1, 1);

    private static Vector3 ToVector(Godot.Color color)
    {
        return new Vector3(color.r, color.g, color.b);
    }

    public static Vector3 ToVector(Color color)
    {
        return color switch
        {
            Color.Red => RedV,
            Color.Blue => BlueV,
            Color.Yellow => YellowV,
            Color.Blank => WhiteV,
            _ => new Vector3(0.5f, 1.0f, 0.8f) // maybe this will jump out at me
        };
    }
}
