using FF2.Core.Viewmodels;
using Godot;
using System;

public class HealthViewerControl : Control
{
    private ISlidingPenaltyViewmodel vm = NullViewmodel.Instance;

    public void SetModel(ISlidingPenaltyViewmodel viewmodel)
    {
        this.vm = viewmodel;
    }

    public override void _Draw()
    {
        float boxHeight = RectSize.y / vm.NumSlots;
        float boxWidth = RectSize.x;

        for (int i = 0; i < vm.NumSlots; i++)
        {
            var penalty = vm.GetPenalty(i);
            var rect = new Rect2(0, i * boxHeight, boxWidth, boxHeight);
            DrawRect(rect, Godot.Colors.Bisque, filled: penalty.Size > 0, width: 2);
        }
    }

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
