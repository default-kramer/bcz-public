using Godot;
using System;

public class BorderRect : ColorRect
{
    public override void _Draw()
    {
        DrawRect(new Rect2(0, 0, this.RectSize), Godot.Colors.White, filled: false, width: 2);
    }
}
