using FF2.Core.Viewmodels;
using Godot;
using System;

public class HealthViewerControl : Control
{
    private ISlidingPenaltyViewmodel vm = NullViewmodel.Instance;
    private Font font;
    private static readonly Color BoxColor = Godot.Colors.Orange;
    private static readonly Color TextColor = Godot.Colors.Black;

    public void SetModel(ISlidingPenaltyViewmodel viewmodel)
    {
        this.vm = viewmodel;
    }

    public override void _Ready()
    {
        font = this.GetFont("");
    }

    public override void _Draw()
    {
        float boxHeight = RectSize.y / vm.NumSlots;
        float boxWidth = RectSize.x;

        for (int i = 0; i < vm.NumSlots; i++)
        {
            var penalty = vm.GetPenalty(i);
            var rect = new Rect2(0, i * boxHeight, boxWidth, boxHeight);
            if (penalty.Size > 0)
            {
                DrawRect(rect, BoxColor, filled: true);
                DrawString(font, rect.Position + new Vector2(0, 15), levels[penalty.Size], TextColor);
            }
            else
            {
                DrawRect(rect, BoxColor, filled: false, width: 2);
            }
        }
    }

    private static readonly string[] levels =
    {
        "",
        "o",
        "oo",
        "ooo",
        "oooo",
        "W",
        "W o",
        "W oo",
        "W ooo",
        "W oooo",
        "WW",
        "WW o",
        "WW oo",
        "WW ooo",
        "WW oooo",
    };

    class NullViewmodel : ISlidingPenaltyViewmodel
    {
        private NullViewmodel() { }
        public static readonly NullViewmodel Instance = new NullViewmodel();

        public int NumSlots => 20;

        public PenaltyItem GetPenalty(int index)
        {
            return PenaltyItem.None;
        }
    }
}
