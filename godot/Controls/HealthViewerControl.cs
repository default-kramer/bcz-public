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

    private const float slowBlinkRate = 0.5f;
    private const float fastBlinkRate = 0.2f;
    float slowBlinker = 0;
    float fastBlinker = 0;
    bool SlowBlinkOn => slowBlinker < slowBlinkRate / 2;
    bool FastBlinkOn => fastBlinker < fastBlinkRate / 2;

    public override void _Process(float delta)
    {
        base._Process(delta);

        slowBlinker = (slowBlinker + delta) % slowBlinkRate;
        fastBlinker = (fastBlinker + delta) % fastBlinkRate;
    }

    public override void _Draw()
    {
        float boxHeight = RectSize.y / vm.NumSlots;
        float boxWidth = RectSize.x;

        for (int i = 0; i < vm.NumSlots; i++)
        {
            var penalty = vm.GetPenalty(i);
            var rect = new Rect2(0, i * boxHeight, boxWidth, boxHeight);
            bool draw = penalty.Size > 0;
            if (draw)
            {
                if (i == 0)
                {
                    draw = FastBlinkOn;
                }
                else if (i == 1)
                {
                    draw = SlowBlinkOn;
                }
            }

            if (draw)
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
