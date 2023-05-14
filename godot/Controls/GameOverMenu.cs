using BCZ.Core;
using Godot;
using System;
using System.Collections.Generic;

#nullable enable

// == Sizing Notes ==
// The VBox container is used to limit the main content to the top 2/3 of the screen.
// Its children set
// - Size Flags
//   - Vertical: Fill, Expand
//   - Stretch Ratio: 1/3 or 2/3

public class GameOverMenu : Control
{
    readonly struct Members
    {
        public readonly Label LabelMessage;
        public readonly Button ButtonNext;
        public readonly Button ButtonReplay;
        public readonly Button ButtonQuit;

        // Grid and its members
        public readonly GridContainer GridContainer;
        public readonly Label EfficiencyCaption;
        public readonly Label EfficiencyValue;
        public readonly Label MedalCaption;
        public readonly Label MedalValue;
        public readonly Label ScoreCaption;
        public readonly Label ScoreValue;
        public readonly Label EnemyScoreValue;
        public readonly Label ComboScoreValue;
        public readonly Label BestComboCaption;
        public readonly Label BestComboValue;
        public readonly Label TimeCaption;
        public readonly Label TimeValue;

        public Members(GameOverMenu parent)
        {
            parent.FindNode(out LabelMessage, nameof(LabelMessage));
            parent.FindNode(out ButtonNext, nameof(ButtonNext));
            parent.FindNode(out ButtonReplay, nameof(ButtonReplay));
            parent.FindNode(out ButtonQuit, nameof(ButtonQuit));

            parent.FindNode(out GridContainer, nameof(GridContainer));
            parent.FindNode(out EfficiencyCaption, nameof(EfficiencyCaption));
            parent.FindNode(out EfficiencyValue, nameof(EfficiencyValue));
            parent.FindNode(out MedalCaption, nameof(MedalCaption));
            parent.FindNode(out MedalValue, nameof(MedalValue));
            parent.FindNode(out ScoreCaption, nameof(ScoreCaption));
            parent.FindNode(out ScoreValue, nameof(ScoreValue));
            parent.FindNode(out EnemyScoreValue, nameof(EnemyScoreValue));
            parent.FindNode(out ComboScoreValue, nameof(ComboScoreValue));
            parent.FindNode(out BestComboCaption, nameof(BestComboCaption));
            parent.FindNode(out BestComboValue, nameof(BestComboValue));
            parent.FindNode(out TimeCaption, nameof(TimeCaption));
            parent.FindNode(out TimeValue, nameof(TimeValue));
        }

        public void SetMedalVisibility(bool visible)
        {
            MedalCaption.Visible = visible;
            MedalValue.Visible = visible;
        }
    }

    private Members members;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        members = new Members(this);
        members.ButtonNext.Connect("pressed", this, nameof(NextLevel));
        members.ButtonReplay.Connect("pressed", this, nameof(ReplayLevel));
        members.ButtonQuit.Connect("pressed", this, nameof(Quit));
    }

    public void OnGameOver(State state, GamePackage gamePackage)
    {
        this.Visible = true;
        var root = NewRoot.FindRoot(this);

        if (state.ClearedAllEnemies)
        {
            members.LabelMessage.Text = $"Stage Clear!";
        }
        else
        {
            members.LabelMessage.Text = "Game Over";
        }

        DisplayPostgameStats(state, gamePackage);

        if (state.ClearedAllEnemies && root.CanAdvanceToNextLevel())
        {
            members.ButtonNext.Visible = true;
            members.ButtonNext.GrabFocus();
        }
        else
        {

            members.ButtonNext.Visible = false;
            members.ButtonReplay.GrabFocus();
        }
    }

    private void DisplayPostgameStats(State state, GamePackage gamePackage)
    {
        var efficiency = state.EfficiencyInt();
        var medal = MostImpressiveGoal(gamePackage.Goals, efficiency);
        bool showMedal = state.ClearedAllEnemies;
        if (gamePackage.HideMedalProgress)
        {
            // Even if the user chose to hide the medal progress indicator, they might have
            // earned a medal anyway. If they did, show it.
            showMedal = showMedal && medal > MedalKind.None;
        }

        members.EfficiencyValue.Text = efficiency.ToString();
        if (showMedal)
        {
            members.MedalValue.Text = medal.ToString();
            members.SetMedalVisibility(true);
        }
        else
        {
            members.SetMedalVisibility(false);
        }

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

    private void NextLevel()
    {
        NewRoot.FindRoot(this).AdvanceToNextLevel();
    }

    private void ReplayLevel()
    {
        NewRoot.FindRoot(this).ReplayCurrentLevel();
    }

    private void Quit()
    {
        NewRoot.FindRoot(this).BackToMainMenu();
    }
}
