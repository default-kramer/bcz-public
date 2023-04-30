using BCZ.Core;
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
    const string ModePvPSim = "PvP Simulator";

    private static readonly BCZ.Core.ISettingsCollection LevelsModeSettings = BCZ.Core.SinglePlayerSettings.NormalSettings;

    private readonly ChoiceModel<int> LevelChoices = new ChoiceModel<int>()
        .AddChoices(Enumerable.Range(1, LevelsModeSettings.MaxLevel));

    private readonly ChoiceModel<string> ChoiceMedals = new ChoiceModel<string>()
        .AddChoices(MedalsHide, MedalsShow);
    const string MedalsHide = "Hide";
    const string MedalsShow = "Show";

    private readonly ChoiceModel<string> TimeLimitChoices = new ChoiceModel<string>()
        .AddChoices(TimeLimit2m, TimeLimit5m, TimeLimit10m);
    const string TimeLimit2m = "2 minutes";
    const string TimeLimit5m = "5 minutes";
    const string TimeLimit10m = "10 minutes";

    private readonly ChoiceModel<string> LayoutChoices = new ChoiceModel<string>()
        .AddChoices(LayoutTall, LayoutWide);
    const string LayoutTall = "Tall";
    const string LayoutWide = "Wide";

    private readonly ChoiceModel<int> EnemyCountChoices = new ChoiceModel<int>()
        .AddChoices(Enumerable.Range(1, 20).Select(i => i * 4));

    readonly struct Members
    {
        public readonly MenuChoiceControl ChoiceGameMode;
        public readonly MenuChoiceControl ChoiceLevel;
        public readonly MenuChoiceControl ChoiceMedals;
        public readonly MenuChoiceControl ChoiceTimeLimit;
        public readonly MenuChoiceControl ChoiceLayout;
        public readonly MenuChoiceControl ChoiceEnemyCount;
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

            me.FindNode(out ChoiceMedals, nameof(ChoiceMedals));
            ChoiceMedals.Model = me.ChoiceMedals;

            me.FindNode(out ChoiceTimeLimit, nameof(ChoiceTimeLimit));
            ChoiceTimeLimit.Model = me.TimeLimitChoices;

            me.FindNode(out ChoiceLayout, nameof(ChoiceLayout));
            ChoiceLayout.Model = me.LayoutChoices;

            me.FindNode(out ChoiceEnemyCount, nameof(ChoiceEnemyCount));
            ChoiceEnemyCount.Model = me.EnemyCountChoices;

            me.FindNode(out ButtonStartGame, nameof(ButtonStartGame));
            me.FindNode(out ButtonBack, nameof(ButtonBack));
        }
    }

    private Members members;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        if (Util.IsSuperuser)
        {
            GameModeChoices.AddChoice(ModePvPSim);
        }

        this.members = new Members(this);

        members.ButtonStartGame.Connect("pressed", this, nameof(StartGame));
        members.ButtonBack.Connect("pressed", this, nameof(BackToMainMenu));

        GameModeChoices.OnChanged(GameModeChanged);
        LevelChoices.OnChanged(LevelChanged);

        GameModeChanged();
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
            case ModePvPSim:
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
    }

    private bool HideMedals => ChoiceMedals.SelectedItem == MedalsHide;

    private void StartGame()
    {
        var mode = GameModeChoices.SelectedItem;
        if (mode == ModePvPSim)
        {
            var collection = BCZ.Core.SinglePlayerSettings.PvPSimSettings;
            int level = LevelChoices.SelectedItem;
            var token = new LevelToken(level, collection, HideMedals);
            NewRoot.FindRoot(this).StartGame(token);
        }
        else
        {
            var collection = LevelsModeSettings;
            int level = LevelChoices.SelectedItem;
            var token = new LevelToken(level, collection, HideMedals);
            NewRoot.FindRoot(this).StartGame(token);
        }
    }

    private void BackToMainMenu()
    {
        NewRoot.FindRoot(this).BackToMainMenu();
    }

    public readonly struct LevelToken
    {
        private readonly int Level;
        private readonly ISettingsCollection Collection;
        private readonly bool HideMedals;

        public LevelToken(int level, ISettingsCollection collection, bool hideMedals)
        {
            this.Level = level;
            this.Collection = collection;
            this.HideMedals = hideMedals;
        }

        public bool CanAdvance { get { return Level < Collection.MaxLevel; } }

        public bool NextLevel(out LevelToken result)
        {
            if (CanAdvance)
            {
                result = new LevelToken(Level + 1, Collection, HideMedals);
                return true;
            }
            result = this;
            return false;
        }

        public GamePackage CreateGamePackage()
        {
            var settings = Collection.GetSettings(Level).AddRandomSeed();
            var goals = Collection.GetGoals(Level);
            var package = new GamePackage(settings, goals);
            package.HideMedalProgress = HideMedals;
            return package;
        }
    }
}
