using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BCZ.Core;

/// <summary>
/// Contains everything the UI needs to know to start a game.
/// </summary>
public abstract class GamePackage
{
    public readonly SeededSettings Settings;

    public GamePackage(SeededSettings settings)
    {
        Settings = settings;
    }

    internal abstract void Initialize(GameViewerControl.Members members, Ticker ticker);

    internal virtual void OnGameOver(GameOverMenu.Members members, State state)
    {
        members.HideGreatNews();
        members.HideEfficiency();

        var score = state.Score;
        members.ScoreValue.Text = score.TotalScore.ToString();
        members.EnemyScoreValue.Text = score.EnemyScore.ToString();
        members.ComboScoreValue.Text = score.ComboScore.ToString();

        members.BestComboValue.Text = state.BestCombo.ComboToReward.Describe("none");

        var time = state.FinishTime;
        if (time.HasValue)
        {
            var ts = time.Value.ToTimeSpan();
            members.TimeValue.Text = ts.ToString("m\\:ss\\.fff");
        }
        else
        {
            members.TimeValue.Text = "?bug?";
        }
    }
}

class LevelsModeGamePackage : GamePackage
{
    public readonly IReadOnlyList<IGoal> Goals;
    private readonly int Level;

    public LevelsModeGamePackage(SeededSettings settings, int level, IReadOnlyList<IGoal> goals) : base(settings)
    {
        this.Goals = goals;
        this.Level = level;
    }

    /// <summary>
    /// Did the user choose to hide the medal progress indicator?
    /// </summary>
    public bool HideMedalProgress { get; set; }

    internal override void Initialize(GameViewerControl.Members members, Ticker ticker)
    {
        if (!HideMedalProgress && Goals.Count > 0)
        {
            members.GoalViewerControl.SetLogic(ticker.state, Goals);
            members.GoalViewerControl.Visible = true;
        }
        else
        {
            members.GoalViewerControl.Disable();
            members.GoalViewerControl.Visible = false;
        }
    }

    internal override void OnGameOver(GameOverMenu.Members members, State state)
    {
        base.OnGameOver(members, state);

        var efficiency = state.Data.EfficiencyInt();
        members.ShowEfficiency(efficiency);

        var medal = MedalKind.None;
        if (state.ClearedAllEnemies)
        {
            medal = MostImpressiveGoal(Goals, efficiency);
        }
        if (medal != MedalKind.None)
        {
            SaveData.RecordMedal(Level, medal);
            members.ShowGreatNews($"{medal} Medal Earned!");
        }
    }

    private static MedalKind MostImpressiveGoal(IReadOnlyList<IGoal> goals, int playerValue)
    {
        MedalKind best = MedalKind.None;
        for (int i = 0; i < goals.Count; i++)
        {
            var goal = goals[i];
            if (playerValue >= goal.Target && goal.Kind > best)
            {
                best = goal.Kind;
            }
        }

        return best;
    }
}

class ScoreAttackGamePackage : GamePackage
{
    public ScoreAttackGamePackage(SeededSettings settings) : base(settings) { }

    /// <summary>
    /// Did the user choose a non-zero goal for score attack mode?
    /// </summary>
    public int ScoreAttackGoal { get; set; }

    internal override void Initialize(GameViewerControl.Members members, Ticker ticker)
    {
        if (ScoreAttackGoal > 0)
        {
            var state = ticker.state;
            var countdownVM = state.CountdownViewmodel ?? throw new Exception("Expected Countdown VM to exist");
            members.GoalViewerControl.SetLogicForScoreAttack(state.Data, countdownVM, ScoreAttackGoal);
            members.GoalViewerControl.Visible = true;
        }
    }

    internal override void OnGameOver(GameOverMenu.Members members, State state)
    {
        base.OnGameOver(members, state);

        var data = state.Data;
        var totalScore = data.Score.TotalScore;
        if (totalScore > SaveData.ScoreAttackPB)
        {
            SaveData.ScoreAttackPB = totalScore;
            members.ShowGreatNews("!!! New Personal Best !!!");
        }
    }
}
