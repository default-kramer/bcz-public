using Godot;
using System;

public class ControllerSetupControl : Control
{
    public override void _Draw()
    {
        DrawRect(new Rect2(RectPosition, RectSize), Godot.Colors.Green, filled: false, width: 5);
    }

    public override void _Input(InputEvent e)
    {
        if (e is InputEventJoypadButton button)
        {
            Console.WriteLine($"Button: {button.Device}/{button.ButtonIndex}, Pressed: {button.Pressed}, Pressure: {button.Pressure}");
        }
    }
}
