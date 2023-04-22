using BCZ.Core;
using Godot;
using System;
using System.Collections.Generic;

public class GoalViewerControl : Control
{
    private static Godot.Color b1 = Godot.Color.Color8(45, 55, 72);

    private IViewmodel viewmodel = NullViewmodel.Instance;

    public void Disable()
    {
        viewmodel = NullViewmodel.Instance;
    }

    public void TODO(State state, IReadOnlyList<IGoal> goals)
    {
        this.viewmodel = new Viewmodel(goals, state);
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(0, 0, RectSize), b1);
        QueueViewerControl.DrawBorder(this, new Rect2(0, 0, RectSize));

        var goals = viewmodel.Recalculate();
        int count = goals.Length;
        const float totalPadding = 0.4f;
        const float yPadding = 5f;
        float yStart = RectSize.y - yPadding;
        float maxHeight = yStart - yPadding;
        float barWidth = RectSize.x * (1 - totalPadding) / count;
        float padding = RectSize.x * totalPadding / (count + 1);

        for (int i = 0; i < count; i++)
        {
            var goal = goals[i];
            float xOffset = padding * (i + 1) + barWidth * i;
            float height = maxHeight * goal.Progress;
            DrawRect(new Rect2(xOffset, yStart - height, barWidth, height), goal.Color);
        }
    }

    interface IViewmodel
    {
        ReadOnlySpan<GoalModel> Recalculate();
    }

    class NullViewmodel : IViewmodel
    {
        public static readonly NullViewmodel Instance = new NullViewmodel();

        public ReadOnlySpan<GoalModel> Recalculate()
        {
            return ReadOnlySpan<GoalModel>.Empty;
        }
    }

    class Viewmodel : IViewmodel
    {
        private readonly IReadOnlyList<IGoal> goals;
        private readonly State state;
        private readonly int[] targets;
        private readonly GoalModel[] models;
        private int headroom = int.MinValue;

        public Viewmodel(IReadOnlyList<IGoal> goals, State state)
        {
            this.goals = goals;
            this.state = state;
            this.targets = new int[1 + goals.Count];
            this.models = new GoalModel[1 + goals.Count];
        }

        public ReadOnlySpan<GoalModel> Recalculate()
        {
            var goalArgs = state.MakeGoalArgs();

            int foo = 0;
            if (state.NumCombos > 0)
            {
                foo = state.Score / state.NumCombos;
            }
            targets[0] = foo;

            int maxTarget = targets[0];
            for (int i = 0; i < goals.Count; i++)
            {
                int target = goals[i].GetTargetScore(goalArgs);
                targets[i + 1] = target;
                if (target > maxTarget)
                {
                    maxTarget = target;
                }
            }

            if (maxTarget >= headroom)
            {
                headroom = maxTarget * 2;
            }

            for (int i = 0; i < targets.Length; i++)
            {
                // Scale progress to max out at 96% because I think it looks better
                float progress = targets[i] * 0.96f / headroom;
                var color = i == 0 ? GameColors.Green : GameColors.Bronze;
                models[i] = new GoalModel(progress, color);
            }

            return models;
        }
    }

    readonly struct GoalModel
    {
        public readonly float Progress;
        public readonly Godot.Color Color;

        public GoalModel(float progress, Godot.Color color)
        {
            this.Progress = progress;
            this.Color = color;
        }
    }
}
