using BCZ.Core;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

public class SinglePlayerMenu : Control
{
    sealed class ChoiceItem
    {
        public readonly string DisplayValue;
        public readonly string HelpText;

        public ChoiceItem(string displayValue, string helpText)
        {
            this.DisplayValue = displayValue;
            this.HelpText = helpText;
        }

        public override string ToString() => DisplayValue;

        public static string GetHelpText(ChoiceItem item) => item.HelpText;

        public static readonly ChoiceItem ModeLevels = new("Levels", "Eliminate all targets to clear the level.\nAccuracy matters more than speed.\nPlay large combos to earn medals.");
        public static readonly ChoiceItem ModeScoreAttack = new("Score Attack", "Earn the highest score you can in a fixed amount of time.\nPlay quickly and combo aggressively!");
        public static readonly ChoiceItem ModePuzzles = new("Puzzles", "Take your time and find the largest combo.");
        public static readonly ChoiceItem ModePvPSim = new("PvP Simulator", "NO DESCRIPTION");
    }

    private readonly ChoiceModel<ChoiceItem> GameModeChoices = new ChoiceModel<ChoiceItem>()
        .AddChoices(ChoiceItem.ModeLevels, ChoiceItem.ModePuzzles, ChoiceItem.ModeScoreAttack);

    private static readonly BCZ.Core.ISettingsCollection LevelsModeSettings = BCZ.Core.SinglePlayerSettings.NormalSettings;
    private static readonly ISinglePlayerSettings ScoreAttackSettings = SinglePlayerSettings.TODO;

    private readonly ChoiceModel<int> LevelChoices = new ChoiceModel<int>()
        .AddChoices(Enumerable.Range(1, LevelsModeSettings.MaxLevel));

    private readonly ChoiceModel<string> ChoiceMedals = new ChoiceModel<string>()
        .AddChoices(MedalsHide, MedalsShow);
    const string MedalsHide = "Hide";
    const string MedalsShow = "Show";

    private readonly ChoiceModel<string> ChoiceScoreAttackGoal = new ChoiceModel<string>()
        .AddChoices(ScoreAttackGoal_PB, ScoreAttackGoal_Beginner, ScoreAttackGoal_Advanced, ScoreAttackGoal_Elite, ScoreAttackGoal_WR);
    const string ScoreAttackGoal_PB = "Personal Best";
    const string ScoreAttackGoal_Beginner = "Beginner";
    const string ScoreAttackGoal_Advanced = "Advanced";
    const string ScoreAttackGoal_Elite = "Elite";
    const string ScoreAttackGoal_WR = "World Record";

    readonly struct Members
    {
        public readonly MenuChoiceControl ChoiceGameMode;
        public readonly MenuChoiceControl ChoiceLevel;
        public readonly MenuChoiceControl ChoiceMedals;
        public readonly MenuChoiceControl ChoiceScoreAttackGoal;
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

            me.FindNode(out ChoiceScoreAttackGoal, nameof(ChoiceScoreAttackGoal));
            ChoiceScoreAttackGoal.Model = me.ChoiceScoreAttackGoal;

            me.FindNode(out ButtonStartGame, nameof(ButtonStartGame));
            me.FindNode(out ButtonBack, nameof(ButtonBack));
        }
    }

    private Members members;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        GameModeChoices.AddHelpText(ChoiceItem.GetHelpText);

        if (Util.IsSuperuser)
        {
            GameModeChoices.AddChoice(ChoiceItem.ModePvPSim);
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
        var item = GameModeChoices.SelectedItem;
        if (item == ChoiceItem.ModeLevels || item == ChoiceItem.ModePvPSim)
        {
            Show(members.NormalModeOptions);
        }
        else if (item == ChoiceItem.ModeScoreAttack)
        {
            Show(members.ScoreAttackOptions);
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
        if (mode == ChoiceItem.ModePuzzles)
        {
            NewRoot.FindRoot(this).SolvePuzzles();
        }
        else if (mode == ChoiceItem.ModePvPSim)
        {
            var collection = BCZ.Core.SinglePlayerSettings.PvPSimSettings;
            int level = LevelChoices.SelectedItem;
            var token = new LevelsModeToken(level, collection, HideMedals);
            NewRoot.FindRoot(this).StartGame(token);
        }
        else if (mode == ChoiceItem.ModeScoreAttack)
        {
            var token = new ScoreAttackLevelToken(ScoreAttackSettings, ChoiceScoreAttackGoal.SelectedItem);
            NewRoot.FindRoot(this).StartGame(token);
        }
        else
        {
            var collection = LevelsModeSettings;
            int level = LevelChoices.SelectedItem;
            var token = new LevelsModeToken(level, collection, HideMedals);
            NewRoot.FindRoot(this).StartGame(token);
        }
    }

    private void BackToMainMenu()
    {
        NewRoot.FindRoot(this).BackToMainMenu();
    }

    /// <summary>
    /// Handles responses to the "Game Over" menu, such as "retry" or "proceed to next level".
    /// </summary>
    public abstract class LevelToken
    {
        /// <summary>
        /// Create a new game at the current level.
        /// </summary>
        public abstract GamePackage CreateGamePackage();

        public abstract bool CanAdvance { get; }

        /// <summary>
        /// Advance to the next level. Should only be called when <see cref="CanAdvance"/>.
        /// </summary>
        public abstract bool NextLevel();
    }

    class LevelsModeToken : LevelToken
    {
        private int Level;
        private readonly ISettingsCollection Collection;
        private readonly bool HideMedals;

        public LevelsModeToken(int level, ISettingsCollection collection, bool hideMedals)
        {
            this.Level = level;
            this.Collection = collection;
            this.HideMedals = hideMedals;
        }

        public override bool CanAdvance { get { return Level < Collection.MaxLevel; } }

        public override bool NextLevel()
        {
            if (CanAdvance)
            {
                Level = Level + 1;
                return true;
            }
            return false;
        }

        public override GamePackage CreateGamePackage()
        {
            var settings = Collection.GetSettings(Level).AddRandomSeed();
            var goals = Collection.GetGoals(Level);
            var package = new LevelsModeGamePackage(settings, Level, goals);
            package.HideMedalProgress = HideMedals;
            return package;
        }
    }

    class ScoreAttackLevelToken : LevelToken
    {
        private readonly ISinglePlayerSettings settings;
        private readonly string goalType;

        public ScoreAttackLevelToken(ISinglePlayerSettings settings, string goalType)
        {
            this.settings = settings;
            this.goalType = goalType;
        }

        public override bool CanAdvance => false;

        public override bool NextLevel()
        {
            throw new NotImplementedException("This should never be called");
        }

        public override GamePackage CreateGamePackage()
        {
            var settings = this.settings.AddRandomSeed();
            var package = new ScoreAttackGamePackage(settings);
            package.ScoreAttackGoal = GetGoal();
            return package;
        }

        private int GetGoal()
        {
            switch (goalType)
            {
                case ScoreAttackGoal_PB:
                    return Math.Max(100, SaveData.ScoreAttackPB);
                case ScoreAttackGoal_Beginner:
                    return 10000;
                case ScoreAttackGoal_Advanced:
                    return 20000;
                case ScoreAttackGoal_Elite:
                    return 30000;
                case ScoreAttackGoal_WR:
                    return 33000;
            }
            throw new Exception($"Unexpected goal type: {goalType}");
        }
    }
}
