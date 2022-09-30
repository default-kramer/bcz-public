using FF2.Core;
using FF2.Core.ReplayModel;
using FF2.Godot;
using FF2.Godot.Controls;
using Godot;
using System;
using System.Collections.Generic;

#nullable enable

public class GameViewerControl : Control
{
    private readonly TickCalculations tickCalculations = new TickCalculations();
    private ILogic logic = NullLogic.Instance;

    public void WatchReplay(string filepath)
    {
        logic.Cleanup();
        //var driver = ReplayReader.BuildReplayDriver(filepath, tickCalculations);
        var driver = ReplayReader.FindBestCombo(filepath, tickCalculations);
        SetupChildren(driver.Ticker.state, driver.Ticker);
        logic = new WatchReplayLogic(driver);
    }

    private void NewGame(SeededSettings ss)
    {
        logic.Cleanup();

        var state = State.Create(ss);
        ReplayWriter? replayWriter = null;
        var listReplayCollector = new ListReplayCollector();
        IReplayCollector replayCollector = listReplayCollector;

        string replayDir = System.Environment.GetEnvironmentVariable("ffreplaydir");
        if (replayDir != null)
        {
            replayDir = System.IO.Path.Combine(replayDir, "raw");
            var di = new System.IO.DirectoryInfo(replayDir);
            if (!di.Exists)
            {
                di.Create();
            }
            var filename = System.IO.Path.Combine(replayDir, $"{DateTime.Now.ToString("yyyyMMdd_HHmmss")}_{ss.Seed.Serialize()}.ffr");
            var writer = new System.IO.StreamWriter(filename);
            replayWriter = ReplayWriter.Begin(writer, ss);
            replayCollector = replayCollector.Combine(replayWriter);
        }

        var ticker = new DotnetTicker(state, tickCalculations, replayCollector);
        SetupChildren(state, ticker);

        var replayItems = BuildReplay(ss, listReplayCollector.Commands);
        members.ReplayViewer.SetModel(replayItems.Item2);
        members.ReplayViewer.Visible = true;

        this.logic = new LiveGameLogic(replayWriter, ticker, replayItems.Item1, state);
    }

    private void SetupChildren(State state, Ticker ticker)
    {
        members.GridViewer.SetModel(new GridViewerModel(state, ticker, tickCalculations));
        members.PenaltyViewer.Model = state.MakePenaltyModel(ticker);
        members.QueueViewer.Model = state.MakeQueueModel();
        members.GameOverMenu.Visible = false;
    }

    private static (ReplayDriver, GridViewerModel) BuildReplay(SeededSettings ss, IReadOnlyList<Stamped<Command>> commands)
    {
        var state = State.Create(ss);
        var calc = new TickCalculations();
        var ticker = new Ticker(state, calc, NullReplayCollector.Instance);
        var replay = new ReplayDriver(ticker, commands);
        var model = new GridViewerModel(state, ticker, calc);
        return (replay, model);
    }

    readonly struct Members
    {
        public readonly PenaltyViewerControl PenaltyViewer;
        public readonly GridViewerControl GridViewer;
        public readonly QueueViewerControl QueueViewer;
        public readonly GridViewerControl ReplayViewer;
        public readonly GameOverMenu GameOverMenu;

        public Members(Control me)
        {
            me.FindNode(out PenaltyViewer, nameof(PenaltyViewer));
            me.FindNode(out GridViewer, nameof(GridViewer));
            me.FindNode(out QueueViewer, nameof(QueueViewer));
            me.FindNode(out ReplayViewer, nameof(ReplayViewer));
            me.FindNode(out GameOverMenu, nameof(GameOverMenu));

            QueueViewer.GridViewer = GridViewer;
        }
    }

    private Members members;

    public bool ShowPenalties
    {
        get { return members.PenaltyViewer.Visible; }
        set { members.PenaltyViewer.Visible = value; }
    }

    public bool ShowQueue
    {
        get { return members.QueueViewer.Visible; }
        set { members.QueueViewer.Visible = value; }
    }

    public override void _Ready()
    {
        this.members = new Members(this);
        GetTree().Root.Connect("size_changed", this, nameof(OnSizeChanged));
        OnSizeChanged();
    }

    public void OnSizeChanged()
    {
        const float pvWidth = 100;
        const float queueWidth = 140;

        float availWidth = RectSize.x;
        if (ShowPenalties)
        {
            availWidth -= pvWidth;
        }
        if (ShowQueue)
        {
            availWidth -= queueWidth;
        }

        // Divide by 2 for both gridviewers
        var gvSize = members.GridViewer.DesiredSize(new Vector2(availWidth / 2, RectSize.y));

        float totalWidth = gvSize.x;
        if (ShowPenalties)
        {
            totalWidth += pvWidth;
        }
        if (ShowQueue)
        {
            totalWidth += queueWidth;
        }

        float meCenter = RectSize.x / 2f;
        float left = meCenter - totalWidth / 2f;

        if (ShowPenalties)
        {
            members.PenaltyViewer.RectSize = new Vector2(pvWidth, RectSize.y);
            members.PenaltyViewer.RectPosition = new Vector2(left, 0);
            left += pvWidth;
        }

        members.GridViewer.RectSize = gvSize;
        members.GridViewer.RectPosition = new Vector2(left, 0);
        left += gvSize.x;

        if (ShowQueue)
        {
            members.QueueViewer.RectSize = new Vector2(queueWidth, RectSize.y);
            members.QueueViewer.RectPosition = new Vector2(left, 0);
            left += queueWidth;
        }

        members.ReplayViewer.RectSize = gvSize;
        members.ReplayViewer.RectPosition = new Vector2(left, 0);
        left += gvSize.x;
    }

    private bool _firstDraw = true;
    public override void _Draw()
    {
        if (_firstDraw)
        {
            OnSizeChanged();
            Update();
            _firstDraw = false;
        }

        base._Draw();
    }

    public override void _Process(float delta)
    {
        this.logic.Process(delta);
        this.logic.HandleInput();

        members.GridViewer.Update();
        members.ReplayViewer.Update();
        members.PenaltyViewer.Update();
        members.QueueViewer.Update();

        this.logic.CheckGameOver(members);
    }

    public void StartGame(SeededSettings ss)
    {
        NewGame(ss);
        members.GameOverMenu.Visible = false;
    }

    interface ILogic
    {
        void Cleanup();
        void Process(float delta);
        void HandleInput();
        void CheckGameOver(Members members);
    }

    sealed class NullLogic : ILogic
    {
        public void Cleanup() { }
        public void Process(float delta) { }
        public void HandleInput() { }
        public void CheckGameOver(Members members) { }

        private NullLogic() { }

        public static readonly NullLogic Instance = new NullLogic();
    }

    sealed class LiveGameLogic : ILogic
    {
        private readonly ReplayWriter? replayWriter;
        private readonly DotnetTicker ticker;
        private readonly ReplayDriver replayDriver;
        private readonly State state;
        private bool holdingDrop = false;

        public LiveGameLogic(ReplayWriter? replayWriter, DotnetTicker ticker, ReplayDriver replayDriver, State state)
        {
            this.replayWriter = replayWriter;
            this.ticker = ticker;
            this.replayDriver = replayDriver;
            this.state = state;
        }

        public void Cleanup()
        {
            if (replayWriter != null)
            {
                replayWriter.Flush();
                replayWriter.Close();
                replayWriter.Dispose();
            }
        }

        public void Process(float delta)
        {
            ticker._Process(delta);

            var laggingNow = ticker.Now.AddMillis(-3000);
            if (laggingNow.Millis > 0)
            {
                replayDriver.Advance(laggingNow);
            }
        }

        public void HandleInput()
        {
            if (Input.IsActionJustPressed("game_left"))
            {
                ticker.HandleCommand(Command.Left);
            }
            if (Input.IsActionJustPressed("game_right"))
            {
                ticker.HandleCommand(Command.Right);
            }
            if (Input.IsActionJustPressed("game_rotate_cw"))
            {
                ticker.HandleCommand(Command.RotateCW);
            }
            if (Input.IsActionJustPressed("game_rotate_ccw"))
            {
                ticker.HandleCommand(Command.RotateCCW);
            }

            if (Input.IsActionPressed("game_drop"))
            {
                if (!holdingDrop)
                {
                    ticker.HandleCommand(Command.BurstBegin);
                    holdingDrop = true;
                }
            }
            else
            {
                if (holdingDrop)
                {
                    ticker.HandleCommand(Command.BurstCancel);
                    holdingDrop = false;
                }
            }
        }

        public void CheckGameOver(Members members)
        {
            if (state.Kind == StateKind.GameOver && !members.GameOverMenu.Visible)
            {
                members.GameOverMenu.OnGameOver(state);
            }
        }
    }

    class WatchReplayLogic : ILogic
    {
        private readonly IReplayDriver replayDriver;

        public WatchReplayLogic(IReplayDriver replayDriver)
        {
            this.replayDriver = replayDriver;
        }

        public void CheckGameOver(Members members) { }

        public void Cleanup() { }

        public void HandleInput() { }

        private Moment now = new Moment(-1);
        public void Process(float delta)
        {
            if (now.Millis < 0)
            {
                now = new Moment(0);
            }
            else
            {
                int millis = Convert.ToInt32(delta * 1000);
                now = now.AddMillis(millis);
            }
            replayDriver.Advance(now);
        }
    }
}
