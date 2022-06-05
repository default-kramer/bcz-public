using Godot;
using System;

public class PenaltyViewerControl : Control
{
    public override void _Draw()
    {
        base._Draw();

        DrawRect(new Rect2(0, 0, RectSize), Colors.Yellow);
    }
}
