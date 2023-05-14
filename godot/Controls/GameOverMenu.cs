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
    internal readonly struct Members
    {
        public readonly Label LabelMessage;
        private readonly Label LabelGreatNews;
        public readonly Button ButtonNext;
        public readonly Button ButtonReplay;
        public readonly Button ButtonQuit;

        // Grid and its members
        public readonly GridContainer GridContainer;
        private readonly Label EfficiencyCaption;
        private readonly Label EfficiencyValue;
        public readonly Label MedalCaption;
        public readonly Label MedalValue; // Should we move the medal message to "Great News" ?
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
            parent.FindNode(out LabelGreatNews, nameof(LabelGreatNews));
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

        public void ShowEfficiency(int efficiency)
        {
            EfficiencyValue.Text = efficiency.ToString();
            EfficiencyCaption.Visible = true;
            EfficiencyValue.Visible = true;
        }

        public void HideEfficiency()
        {
            EfficiencyCaption.Visible = false;
            EfficiencyValue.Visible = false;
        }

        public void ShowGreatNews(string message)
        {
            LabelGreatNews.Text = message;
            LabelGreatNews.Visible = true;
        }

        public void HideGreatNews()
        {
            LabelGreatNews.Visible = false;
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

        gamePackage.OnGameOver(members, state);

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
