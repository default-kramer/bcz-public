using BCZ.Core;
using BCZ.Core.Viewmodels;
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

    public void SetLogic(State state, IReadOnlyList<IGoal> goals)
    {
        this.viewmodel = new Viewmodel(goals, state.Data);
    }

    public void SetLogicForScoreAttack(IStateData state, ICountdownViewmodel countdown, int targetScore)
    {
        this.viewmodel = new ScoreAttackViewmodel(state, countdown, targetScore);
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
            float height;
            if (goal.Projection != null)
            {
                height = maxHeight * goal.Projection.Value;
                DrawRect(new Rect2(xOffset, yStart - height, barWidth, height), goal.Color, filled: false);
            }
            height = maxHeight * goal.Progress;
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
        private readonly IStateData state;
        private readonly int[] bars;
        private readonly GoalModel[] models;
        private int headroom = int.MinValue;

        // Player's value goes into bars[0] and does not affect headroom.
        // (That is, the player can go past 100% and we will just draw it as 100%.)
        // The N goal values go into bars[1]...bars[N], inclusive.
        const int playerIndex = 0;
        const int goalsOffset = 1;

        public Viewmodel(IReadOnlyList<IGoal> goals, IStateData state)
        {
            this.goals = goals;
            this.state = state;
            this.bars = new int[1 + goals.Count];
            this.models = new GoalModel[1 + goals.Count];
        }

        public ReadOnlySpan<GoalModel> Recalculate()
        {
            int playerValue = state.EfficiencyInt();
            bars[playerIndex] = playerValue;

            int maxTarget = 0;

            for (int i = 0; i < goals.Count; i++)
            {
                int target = goals[i].Target;
                bars[i + goalsOffset] = target;
                if (target > maxTarget)
                {
                    maxTarget = target;
                }
            }

            if (maxTarget >= headroom)
            {
                headroom = Convert.ToInt32(maxTarget * 1.2);
            }

            for (int i = 0; i < bars.Length; i++)
            {
                int clampedValue = Math.Min(bars[i], headroom);
                float progress = clampedValue * 1f / headroom;
                var color = GameColors.Green;
                if (i != playerIndex)
                {
                    var goal = goals[i - goalsOffset];
                    switch (goal.Kind)
                    {
                        case MedalKind.Bronze:
                            color = GameColors.Bronze;
                            break;
                        case MedalKind.Silver:
                            color = GameColors.Silver;
                            break;
                        case MedalKind.Gold:
                            color = GameColors.Gold;
                            break;
                    }
                }
                models[i] = new GoalModel(progress, color);
            }

            return models;
        }
    }

    class ScoreAttackViewmodel : IViewmodel
    {
        private readonly IStateData state;
        private readonly ICountdownViewmodel countdown;
        private readonly int targetScore;
        private readonly float headroom;
        private readonly GoalModel[] models = new GoalModel[2];
        private int lastPlayerScore = 0;

        public ScoreAttackViewmodel(IStateData state, ICountdownViewmodel countdown, int targetScore)
        {
            this.state = state;
            this.countdown = countdown;
            this.targetScore = targetScore;
            this.headroom = targetScore * 1.2f;
            models[1] = new GoalModel(targetScore / headroom, GameColors.Silver);
            models[0] = new GoalModel(lastPlayerScore, GameColors.Green, 0f);
        }

        public ReadOnlySpan<GoalModel> Recalculate()
        {
            var playerScore = state.Score.TotalScore;
            if (playerScore != lastPlayerScore)
            {
                lastPlayerScore = playerScore;
                float scorePerMilli = playerScore * 1f / state.LastComboMoment.Millis;
                float projection = playerScore + scorePerMilli * countdown.RemainingMillis;
                float bar1 = Math.Min(1f, playerScore / headroom);
                float bar2 = Math.Min(1f, projection / headroom);
                models[0] = new GoalModel(bar1, GameColors.Green, bar2);
                Console.WriteLine($"New Projection: {projection} / {scorePerMilli} / {countdown.RemainingMillis}");
            }
            return models;
        }
    }

    readonly struct GoalModel
    {
        public readonly float Progress;
        public readonly Godot.Color Color;
        public readonly float? Projection;

        public GoalModel(float progress, Godot.Color color) : this(progress, color, null) { }

        public GoalModel(float progress, Godot.Color color, float? projection)
        {
            this.Progress = progress;
            this.Color = color;
            this.Projection = projection;
        }
    }
}
