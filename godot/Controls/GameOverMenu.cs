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
//
// Within the Grid Container, we want small spacing between "Efficiency" and "Medal"
// and larger spacing between all the rest.
// We use RectMinSize on the Labels so that when they are not visible everything will collapse properly.

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

        members.EfficiencyValue.Text = efficiency.ToString();
        if (state.ClearedAllEnemies && gamePackage.Goals.Count > 0)
        {
            var medal = MostImpressiveGoal(gamePackage.Goals, efficiency);
            members.MedalValue.Text = medal.ToString();
            members.SetMedalVisibility(true);
        }
        else
        {
            members.SetMedalVisibility(false);
        }

        members.ScoreValue.Text = state.Score.ToString();
        members.BestComboValue.Text = "TODO"; // NOMERGE
        members.TimeValue.Text = "TODO"; // NOMERGE
    }

    private static GoalKind MostImpressiveGoal(IReadOnlyList<IGoal> goals, int playerValue)
    {
        GoalKind best = GoalKind.None;
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
