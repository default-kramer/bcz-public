using FF2.Core.Viewmodels;
using Godot;
using System;

public class CountdownViewerControl : Control
{
    private ICountdownViewmodel vm = NullModel.Instance;
    private float position = 1f;

    public void SetModel(ICountdownViewmodel viewmodel)
    {
        this.vm = viewmodel ?? NullModel.Instance;
        this.position = CalcPosition(vm);
    }

    private static float CalcPosition(ICountdownViewmodel vm)
    {
        float m = vm.CurrentMillis;
        return m / vm.MaxMillis;
    }

    public override void _Process(float delta)
    {
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

    public override void _Draw()
    {
        DrawRect(new Rect2(0, 0, RectSize), Godot.Colors.Black);

        const int width = 20;
        float left = RectSize.x / 2 - width / 2;

        const int vPadding = 20;
        float availH = RectSize.y - (vPadding * 2);

        var color = Godot.Colors.Aquamarine;

        DrawRect(new Rect2(left, vPadding, width, availH), color, filled: false, width: 2);

        float y = vPadding + availH * (1f - position);
        DrawRect(new Rect2(left, y, width, RectSize.y - (vPadding + y)), color, filled: true);
    }

    class NullModel : ICountdownViewmodel
    {
        public int MaxMillis => 1;
        public int CurrentMillis => 1;

        private NullModel() { }

        public static readonly NullModel Instance = new NullModel();
    }
}
