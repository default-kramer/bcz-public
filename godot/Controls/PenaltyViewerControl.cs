using FF2.Core;
using Godot;
using System;

public class PenaltyViewerControl : Control
{
    public PenaltyManager Model { get; set; }

    public override void _Draw()
    {
        base._Draw();

        DrawRect(new Rect2(0, 0, RectSize), Colors.DarkGray);

        float penaltyMargin = this.RectSize.x * 0.1f;
        float penaltyW = this.RectSize.x - penaltyMargin - penaltyMargin;
        float penaltyH = penaltyW;
        float x = penaltyMargin;
        var penaltySize = new Vector2(penaltyW, penaltyH);
        float yBase = this.RectSize.y - penaltyH;

        for (int i = 0; i < Model.Count; i++)
        {
            float y = penaltyMargin * (i + 1) + penaltyH * i;
            y = yBase - y;
            DrawRect(new Rect2(x, y, penaltySize), GameColors.Corrupt);
        }
    }
}
