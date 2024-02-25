using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BCZ.Core;
using Godot;

/// <summary>
/// This interface is designed to be the sole argument to <see cref="GamePackage.PrepareGame"/>.
/// Allows the game package to do custom "prepare the game" logic (eg getting a seed from the server)
/// and call back when the game is ready.
/// </summary>
interface IGameStarter
{
    IServerConnection GetServerConnection();

    void StartGame(GamePackage package, SeededSettings settings);
}

/// <summary>
/// Contains everything the UI needs to know to start a game.
/// </summary>
public abstract class GamePackage
{
    /// <summary>
    /// Used to set the size of the grid to be shown while waiting for the game to be ready.
    /// </summary>
    public abstract GridSize GridSize { get; }

    internal abstract void PrepareGame(IGameStarter starter);

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
    private readonly SeededSettings settings;
    public readonly IReadOnlyList<IGoal> Goals;
    private readonly int Level;

    public LevelsModeGamePackage(SeededSettings settings, int level, IReadOnlyList<IGoal> goals)
    {
        this.settings = settings;
        this.Goals = goals;
        this.Level = level;
    }

    public override GridSize GridSize => new GridSize(settings.Settings.GridWidth, settings.Settings.GridHeight);

    internal override void PrepareGame(IGameStarter starter)
    {
        starter.StartGame(this, settings);
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
            members.ShowGreatNews($"{medal} Medal Earned!", medal);
        }
        else if (state.ClearedAllEnemies)
        {
            SaveData.RecordLevelComplete(Level);
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
    private readonly ISinglePlayerSettings settings;
    private readonly SinglePlayerMenu.ScoreAttackGoal goal;
    private readonly Layout layout;

    public ScoreAttackGamePackage(ISinglePlayerSettings settings, SinglePlayerMenu.ScoreAttackGoal goal, Layout layout)
    {
        this.settings = settings;
        this.goal = goal;
        this.layout = layout;
    }

    public override GridSize GridSize => new GridSize(settings.GridWidth, settings.GridHeight);

    private void StartGameWithLocalSeed(IGameStarter starter)
    {
        var settings = this.settings.AddRandomSeed(Util.seeder);
        starter.StartGame(this, settings);
    }

    internal override void PrepareGame(IGameStarter starter)
    {
        var server = starter.GetServerConnection();
        if (server.IsOnline)
        {
            server.Execute(new GetRandomSeedRequest(this, starter));
        }
        else
        {
            StartGameWithLocalSeed(starter);
        }
    }

    internal override void Initialize(GameViewerControl.Members members, Ticker ticker)
    {
        var targetScore = goal.GetTargetScore(layout);
        if (targetScore > 0)
        {
            Console.WriteLine($"Goal is {targetScore}");
            var state = ticker.state;
            var countdownVM = state.CountdownViewmodel ?? throw new Exception("Expected Countdown VM to exist");
            members.GoalViewerControl.SetLogicForScoreAttack(state.Data, countdownVM, targetScore);
            members.GoalViewerControl.Visible = true;
        }
    }

    internal override void OnGameOver(GameOverMenu.Members members, State state)
    {
        base.OnGameOver(members, state);

        var data = state.Data;
        var totalScore = data.Score.TotalScore;
        if (SaveData.UpdatePersonalBest(layout, totalScore))
        {
            members.ShowGreatNews("!!! New Personal Best !!!", MedalKind.None);
        }
    }

    class GetRandomSeedRequest : Request
    {
        private readonly ScoreAttackGamePackage package;
        private readonly IGameStarter starter;

        public GetRandomSeedRequest(ScoreAttackGamePackage package, IGameStarter starter)
        {
            this.package = package;
            this.starter = starter;
        }

        public override string Path => "/api/create-seed/v1";
        public override double TimeoutSeconds => 5;
        public override HTTPClient.Method Method => HTTPClient.Method.Post;

        public override void OnError(Error error)
        {
            package.StartGameWithLocalSeed(starter);
        }

        public override void OnRequestCompleted(long result, long responseCode, string[] headers, byte[] body)
        {
            SeededSettings ss;
            if (responseCode == 200)
            {
                ss = HandleResponse(body);
            }
            else
            {
                GD.PushError($"Failed to get seed from server, result={result}, responseCode={responseCode}");
                ss = package.settings.AddRandomSeed(Util.seeder);
            }
            starter.StartGame(package, ss);
        }

        private SeededSettings HandleResponse(byte[] body)
        {
            Newtonsoft.Json.Linq.JObject? response = null;
            try
            {
                // Just assume that charset=utf-8
                string content = Encoding.UTF8.GetString(body);
                response = Newtonsoft.Json.Linq.JObject.Parse(content);
            }
            catch (Exception ex)
            {
                GD.PushError(ex.ToString());
            }

            if (response != null)
            {
                return HandleResponse(response);
            }
            return package.settings.AddRandomSeed(Util.seeder);
        }

        private SeededSettings HandleResponse(Newtonsoft.Json.Linq.JObject response)
        {
            var seedId = response.Value<long>("seed_id");
            var seed = response.Value<string>("seed");
            var state = PRNG.State.Deserialize(seed ?? "assert-fail!");
            return package.settings.AddSeed(state, seedId);
        }
    }
}
