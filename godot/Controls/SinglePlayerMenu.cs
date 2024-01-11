using BCZ.Core;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

public class SinglePlayerMenu : Control
{
    sealed class ChoiceItem : IHaveHelpText
    {
        public readonly string DisplayValue;
        public readonly string HelpText;

        public ChoiceItem(string displayValue, string helpText)
        {
            this.DisplayValue = displayValue;
            this.HelpText = helpText;
        }

        public override string ToString() => DisplayValue;

        string? IHaveHelpText.GetHelpText() => HelpText;

        // Game Mode choices
        public static readonly ChoiceItem ModeLevels = new("Levels", "Eliminate all targets to clear the level.\nAccuracy matters more than speed.\nPlay large combos to earn medals.");
        public static readonly ChoiceItem ModeScoreAttack = new("Score Attack", "Earn the highest score you can in a fixed amount of time.\nPlay quickly and combo aggressively!");
        public static readonly ChoiceItem ModePuzzles = new("Puzzles", "Take your time and find the largest combo.");
        public static readonly ChoiceItem ModePvPSim = new("PvP Simulator", "NO DESCRIPTION");

        // Show/Hide Medals
        public static readonly ChoiceItem MedalsHide = new("Hide", "Do not display your progress towards each medal.\nYou can still earn medals anyway.");
        public static readonly ChoiceItem MedalsShow = new("Show", "Display your progress towards each medal.\nLarge combos are needed to win the gold medal.");

        // Layout
        public static readonly ChoiceItem LayoutTall = new ChoiceItem("Tall", "Play on the normal grid.");
        public static readonly ChoiceItem LayoutWide = new ChoiceItem("Wide", "Play on a wide grid.");

        // Enable/Disable blanks
        public static readonly ChoiceItem BlanksOn = new("On", "Play with blank pieces.");
        public static readonly ChoiceItem BlanksOff = new("Off", "Playing without blanks might help beginners learn.\n(But earning medals will be practically impossible.)");
    }

    private readonly ChoiceModel<ChoiceItem> GameModeChoices = new ChoiceModel<ChoiceItem>()
        .AddChoices(ChoiceItem.ModeLevels, ChoiceItem.ModeScoreAttack);

    private readonly ChoiceModel<int> LevelChoices = new ChoiceModel<int>()
        .AddChoices(Enumerable.Range(1, SinglePlayerSettings.MaxLevel));

    private readonly ChoiceModel<ChoiceItem> ChoiceMedals = new ChoiceModel<ChoiceItem>()
        //.AddChoices(ChoiceItem.MedalsHide, ChoiceItem.MedalsShow);
        // Decided to always show medals:
        .AddChoices(ChoiceItem.MedalsShow);

    private readonly ChoiceModel<ChoiceItem> ChoiceBlanks = new ChoiceModel<ChoiceItem>()
        .AddChoices(ChoiceItem.BlanksOn, ChoiceItem.BlanksOff);

    private readonly ChoiceModel<ChoiceItem> LayoutChoices = new ChoiceModel<ChoiceItem>()
        .AddChoices(ChoiceItem.LayoutTall, ChoiceItem.LayoutWide);

    private readonly ChoiceModel<ScoreAttackGoal> ChoiceScoreAttackGoal = new ChoiceModel<ScoreAttackGoal>().AddChoices(
        ScoreAttackGoal.PersonalBest,
        ScoreAttackGoal.Beginner,
        ScoreAttackGoal.Advanced,
        ScoreAttackGoal.Elite,
        ScoreAttackGoal.WorldRecord);

    readonly struct Members
    {
        public readonly MenuChoiceControl ChoiceGameMode;
        public readonly MenuChoiceControl ChoiceLevel;
        public readonly MenuChoiceControl ChoiceMedals;
        public readonly MenuChoiceControl ChoiceBlanks;
        public readonly MenuChoiceControl ChoiceScoreAttackLayout;
        public readonly MenuChoiceControl ChoiceScoreAttackGoal;

        public readonly Button ButtonStartGame;
        public readonly Button ButtonBack;

        public readonly Control NormalModeOptions;
        public readonly Control ScoreAttackOptions;

        public readonly TextureRect IconBronze;
        public readonly TextureRect IconSilver;
        public readonly TextureRect IconGold;
        public readonly TextureRect IconCheckmark;

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

            me.FindNode(out ChoiceBlanks, nameof(ChoiceBlanks));
            ChoiceBlanks.Model = me.ChoiceBlanks;

            me.FindNode(out ChoiceScoreAttackLayout, nameof(ChoiceScoreAttackLayout));
            ChoiceScoreAttackLayout.Model = me.LayoutChoices;

            me.FindNode(out ChoiceScoreAttackGoal, nameof(ChoiceScoreAttackGoal));
            ChoiceScoreAttackGoal.Model = me.ChoiceScoreAttackGoal;

            me.FindNode(out ButtonStartGame, nameof(ButtonStartGame));
            me.FindNode(out ButtonBack, nameof(ButtonBack));

            me.FindNode(out IconBronze, nameof(IconBronze));
            me.FindNode(out IconSilver, nameof(IconSilver));
            me.FindNode(out IconGold, nameof(IconGold));
            me.FindNode(out IconCheckmark, nameof(IconCheckmark));
        }
    }

    private Members members;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        LevelChoices.AddHelpText(GetLevelsModeHelpText);
        ChoiceScoreAttackGoal.AddHelpText(GetScoreAttackGoalHelpText);

        if (Util.IsSuperuser)
        {
            GameModeChoices.AddChoice(ChoiceItem.ModePuzzles);
            GameModeChoices.AddChoice(ChoiceItem.ModePvPSim);
        }

        this.members = new Members(this);

        members.ButtonStartGame.Connect("pressed", this, nameof(StartGame));
        members.ButtonBack.Connect("pressed", this, nameof(BackToMainMenu));

        GameModeChoices.OnChanged(GameModeChanged);
        LevelChoices.OnChanged(LevelChanged);

        GameModeChanged();
    }

    public override void _Notification(int what)
    {
        base._Notification(what);
        if (what == NotificationVisibilityChanged)
        {
            // If the user improved the Completion of level N, and they exit to the Title Screen,
            // and they come back into this menu, we would still be showing level N with the old Completion.
            // So do this to refresh it:
            LevelChanged();
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
        var completion = SaveData.GetCompletion(level);
        members.IconCheckmark.Visible = completion >= SaveData.LevelCompletion.Complete;
        members.IconBronze.Visible = completion >= SaveData.LevelCompletion.Bronze;
        members.IconSilver.Visible = completion >= SaveData.LevelCompletion.Silver;
        members.IconGold.Visible = completion >= SaveData.LevelCompletion.Gold;
    }

    private bool HideMedals => ChoiceMedals.SelectedItem == ChoiceItem.MedalsHide;

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
            var layout = LayoutChoices.SelectedItem;
            ISinglePlayerSettings settings;
            if (layout == ChoiceItem.LayoutTall)
            {
                settings = OfficialSettings.ScoreAttackV0;
            }
            else if (layout == ChoiceItem.LayoutWide)
            {
                settings = OfficialSettings.ScoreAttackWide5;
            }
            else
            {
                throw new Exception("Unexpected layout choice");
            }
            var token = new ScoreAttackLevelToken(settings, ChoiceScoreAttackGoal.SelectedItem);
            NewRoot.FindRoot(this).StartGame(token);
        }
        else
        {
            bool blanksOn = ChoiceBlanks.SelectedItem == ChoiceItem.BlanksOn;
            var collection = blanksOn ? SinglePlayerSettings.LevelsModeWithBlanks : SinglePlayerSettings.LevelsModeWithoutBlanks;
            int level = LevelChoices.SelectedItem;
            var token = new LevelsModeToken(level, collection, HideMedals);
            NewRoot.FindRoot(this).StartGame(token);
        }
    }

    private void BackToMainMenu()
    {
        NewRoot.FindRoot(this).BackToMainMenu();
    }

    private string?[] LevelsModeDescriptions = new string?[SinglePlayerSettings.MaxLevel];
    private string GetLevelsModeHelpText(int level)
    {
        int index = level - 1;
        var item = LevelsModeDescriptions[index];
        if (item != null) { return item; }

        // Blanks On / Blanks Off should be identical in all other respects including enemy count.
        // So we can choose either collection:
        var collection = SinglePlayerSettings.LevelsModeWithBlanks;

        var count = collection.GetSettings(level).EnemyCount;
        var description = $"Eliminate {count} targets to win.";
        LevelsModeDescriptions[index] = description;
        return description;
    }

    private string? GetScoreAttackGoalHelpText(ScoreAttackGoal goal)
    {
        if (goal.FixedValue != null)
        {
            return goal.FixedValue.Value.helpText;
        }
        else if (goal == ScoreAttackGoal.PersonalBest)
        {
            var score = goal.GetTargetScore();
            return $"Your current Personal Best is {score.ToString("N0")}.";
        }
        else if (goal == ScoreAttackGoal.WorldRecord)
        {
            var score = goal.GetTargetScore();
            return $"Leaderboards/replays are coming soon...\nFor now, you can try to beat my PB: {score.ToString("N0")}";
        }
        return null;
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
        private readonly ScoreAttackGoal goalType;

        public ScoreAttackLevelToken(ISinglePlayerSettings settings, ScoreAttackGoal goalType)
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

        private int GetGoal() => goalType.GetTargetScore();
    }

    sealed class ScoreAttackGoal
    {
        public readonly string DisplayValue;
        public readonly (int score, string helpText)? FixedValue;

        private ScoreAttackGoal(string displayValue, (int score, string helpText)? fixedValue)
        {
            this.DisplayValue = displayValue;
            this.FixedValue = fixedValue;
        }

        public override string ToString() => DisplayValue;

        public static readonly ScoreAttackGoal Beginner = new ScoreAttackGoal("Beginner", (10000, $"Try for {10000.ToString("N0")} points."));
        public static readonly ScoreAttackGoal Advanced = new ScoreAttackGoal("Advanced", (20000, $"Try for {20000.ToString("N0")} points."));
        public static readonly ScoreAttackGoal Elite = new ScoreAttackGoal("Elite", (30000, $"Try for {30000.ToString("N0")} points."));
        public static readonly ScoreAttackGoal PersonalBest = new ScoreAttackGoal("Personal Best", null);
        public static readonly ScoreAttackGoal WorldRecord = new ScoreAttackGoal("World Record", null);

        public const int MinPersonalBest = 100; // To be used if the player doesn't have a PB yet

        public int GetTargetScore()
        {
            if (FixedValue.HasValue)
            {
                return FixedValue.Value.score;
            }
            else if (this == PersonalBest)
            {
                return Math.Max(MinPersonalBest, SaveData.ScoreAttackPB);
            }
            else if (this == WorldRecord)
            {
                return 32350;
            }
            throw new Exception("Assert fail");
        }
    }
}
