using Godot;
using System;
using System.Linq;

#nullable enable

public class SinglePlayerMenu : Control
{
    private readonly ChoiceModel<string> GameModeChoices = new ChoiceModel<string>()
        .AddChoices(ModeNormal, ModeScoreAttack, ModeTraining);
    const string ModeNormal = "Normal";
    const string ModeScoreAttack = "Score Attack";
    const string ModeTraining = "Training";

    private readonly ChoiceModel<int> LevelChoices = new ChoiceModel<int>()
        .AddChoices(Enumerable.Range(1, 40));

    private readonly ChoiceModel<string> TutorialChoices = new ChoiceModel<string>()
        .AddChoices(TutorialsOn, TutorialsOff);
    const string TutorialsOn = "On";
    const string TutorialsOff = "Off";

    private readonly ChoiceModel<string> TimeLimitChoices = new ChoiceModel<string>()
        .AddChoices(TimeLimit2m, TimeLimit5m, TimeLimit10m);
    const string TimeLimit2m = "2 minutes";
    const string TimeLimit5m = "5 minutes";
    const string TimeLimit10m = "10 minutes";

    private readonly ChoiceModel<string> LayoutChoices = new ChoiceModel<string>()
        .AddChoices(LayoutStandard, LayoutWave);
    const string LayoutStandard = "Standard";
    const string LayoutWave = "Wave"; // TODO would "Wide" be better?

    private readonly ChoiceModel<int> EnemyCountChoices = new ChoiceModel<int>()
        .AddChoices(Enumerable.Range(1, 20).Select(i => i * 4));

    private readonly ChoiceModel<string> BlanksChoices = new ChoiceModel<string>()
        .AddChoices(BlanksOn, BlanksOff);
    const string BlanksOn = "On";
    const string BlanksOff = "Off";

    readonly struct Members
    {
        public readonly MenuChoiceControl ChoiceGameMode;
        public readonly MenuChoiceControl ChoiceLevel;
        public readonly MenuChoiceControl ChoiceTutorials;
        public readonly MenuChoiceControl ChoiceTimeLimit;
        public readonly MenuChoiceControl ChoiceLayout;
        public readonly MenuChoiceControl ChoiceEnemyCount;
        public readonly MenuChoiceControl ChoiceBlanks;
        public readonly Button ButtonStartGame;
        public readonly Button ButtonBack;

        public readonly Control NormalModeOptions;
        public readonly Control ScoreAttackOptions;

        public Members(SinglePlayerMenu me)
        {
            me.FindNode(out ChoiceGameMode, nameof(ChoiceGameMode));
            ChoiceGameMode.Model = me.GameModeChoices;

            me.FindNode(out NormalModeOptions, nameof(NormalModeOptions));
            me.FindNode(out ScoreAttackOptions, nameof(ScoreAttackOptions));

            me.FindNode(out ChoiceLevel, nameof(ChoiceLevel));
            ChoiceLevel.Model = me.LevelChoices;

            me.FindNode(out ChoiceTutorials, nameof(ChoiceTutorials));
            ChoiceTutorials.Model = me.TutorialChoices;

            me.FindNode(out ChoiceTimeLimit, nameof(ChoiceTimeLimit));
            ChoiceTimeLimit.Model = me.TimeLimitChoices;

            me.FindNode(out ChoiceLayout, nameof(ChoiceLayout));
            ChoiceLayout.Model = me.LayoutChoices;

            me.FindNode(out ChoiceEnemyCount, nameof(ChoiceEnemyCount));
            ChoiceEnemyCount.Model = me.EnemyCountChoices;

            me.FindNode(out ChoiceBlanks, nameof(ChoiceBlanks));
            ChoiceBlanks.Model = me.BlanksChoices;

            me.FindNode(out ButtonStartGame, nameof(ButtonStartGame));
            me.FindNode(out ButtonBack, nameof(ButtonBack));
        }
    }

    private Members members;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        this.members = new Members(this);

        members.ButtonStartGame.Connect("pressed", this, nameof(StartGame));
        members.ButtonBack.Connect("pressed", this, nameof(BackToMainMenu));

        GameModeChoices.OnChanged(GameModeChanged);
        LevelChoices.OnChanged(LevelChanged);

        members.ChoiceGameMode.GrabFocus();
        GameModeChanged();
    }

    private bool firstTime = true;
    public override void _Input(InputEvent @event)
    {
        if (firstTime)
        {
            members.ChoiceGameMode.GrabFocus();
            firstTime = false;
        }
    }

    private void Show(Control control)
    {
        members.NormalModeOptions.Visible = false;
        members.ScoreAttackOptions.Visible = false;
        control.Visible = true;
    }

    private void GameModeChanged()
    {
        switch (GameModeChoices.SelectedItem)
        {
            case ModeNormal:
                Show(members.NormalModeOptions);
                break;
            case ModeScoreAttack:
                Show(members.ScoreAttackOptions);
                break;
        }
    }

    private void LevelChanged()
    {
        var level = LevelChoices.SelectedItem;
        // TODO would be nice to show a preview grid so you know how big the level is...
        // For now just placeholder code
        Console.WriteLine("Chosen level: " + level);
    }

    private void StartGame()
    {
        NewRoot.FindRoot(this).StartGame();
    }

    private void BackToMainMenu()
    {
        NewRoot.FindRoot(this).BackToMainMenu();
    }
}
