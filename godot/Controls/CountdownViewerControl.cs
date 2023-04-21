using BCZ.Core.Viewmodels;
using Godot;
using System;

#nullable enable

public class CountdownViewerControl : Control
{
    private ICountdownViewmodel vm = NullModel.Instance;
    private float position = 1f;
    private Members members;

    readonly struct Members
    {
        public Members(Control me)
        {
        }
    }

    public override void _Ready()
    {
        members = new Members(this);
    }

    public void SetModel(ICountdownViewmodel? viewmodel)
    {
        this.vm = viewmodel ?? NullModel.Instance;
        this.position = CalcPosition(vm);
    }

    private static float CalcPosition(ICountdownViewmodel vm)
    {
        float m = vm.CurrentMillis;
        return m / vm.MaxMillis;
    }

    private const float slowBlinkRate = 0.5f;
    private const float fastBlinkRate = 0.1f;
    float slowBlinker = 0;
    float fastBlinker = 0;
    bool SlowBlinkOn => slowBlinker < slowBlinkRate / 2;
    bool FastBlinkOn => fastBlinker < fastBlinkRate / 2;

    public override void _Process(float delta)
    {
        slowBlinker = (slowBlinker + delta) % slowBlinkRate;
        fastBlinker = (fastBlinker + delta) % fastBlinkRate;

        var desiredPosition = CalcPosition(vm);

        float maxJumpRestore = delta * 0.25f;
        if (desiredPosition - position > maxJumpRestore)
        {
            position += maxJumpRestore;
        }
        else
        {
            position = desiredPosition;
        }
    }

    static readonly Godot.Color darkGreen = Godot.Colors.Black;// green.Darkened(0.7f);
    static readonly Godot.Color orange = Godot.Colors.Orange;// Godot.Color.Color8(244, 185, 58);

    public override void _Draw()
    {
        DrawRect(new Rect2(0, 0, RectSize), Godot.Colors.Black);

        var ts = TimeSpan.FromMilliseconds(vm.CurrentMillis);
        bool draw = true;
        if (ts.TotalSeconds < 2)
        {
            draw = FastBlinkOn;
        }
        else if (ts.TotalSeconds < 5)
        {
            draw = SlowBlinkOn;
        }

        float padding = 2f;// RectSize.x * 0.2f;
        float left = padding;
        float top = padding + 1f;
        float bottom = RectSize.y - padding;
        var size = new Vector2(RectSize.x * 0.14f, bottom - top);
        var timerRect = new Rect2(left, top, size);
        if (draw)
        {
            DrawRect(timerRect, orange);
        }
        if (true)
        {
            float y = top + size.y * (1f - position);
            DrawRect(new Rect2(left, y, size * new Vector2(1, position)), GameColors.Green, filled: true);
            DrawRect(timerRect, darkGreen, filled: false, width: 2);
        }

        QueueViewerControl.DrawBorder(this, new Rect2(0, 0, RectSize));


        if (ts.TotalSeconds > 5)
        {
            //members.LabelTimer.Text = ts.ToString("mm\\:ss", System.Globalization.CultureInfo.InvariantCulture);
        }
        else
        {
            //members.LabelTimer.Text = ts.ToString("mm\\:ss\\.fff", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    class NullModel : ICountdownViewmodel
    {
        public int MaxMillis => 1;
        public int CurrentMillis => 1;

        private NullModel() { }

        public static readonly NullModel Instance = new NullModel();
    }
}
