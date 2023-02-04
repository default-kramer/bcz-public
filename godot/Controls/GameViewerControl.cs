using FF2.Core;
using FF2.Core.ReplayModel;
using FF2.Godot;
using Godot;
using System;
using System.Collections.Generic;

#nullable enable

public class GameViewerControl : Control
{
    private ILogic logic = NullLogic.Instance;

    internal void SetLogic(ILogic newLogic)
    {
        logic.Cleanup();
        logic = newLogic;
        logic.Initialize(members);
        members.GameOverMenu.Visible = false;

        OnSizeChanged(); // In case the grid size changed
    }

    public void WatchReplay(string filepath)
    {
        logic.Cleanup();
        var driver = ReplayReader.BuildReplayDriver(filepath);
        var newLogic = new WatchReplayLogic(driver);
        SetLogic(newLogic);
    }

    private void NewGame(SeededSettings ss)
    {
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

        var ticker = new DotnetTicker(state, replayCollector);
        var newLogic = new LiveGameLogic(replayWriter, ticker, members);
        SetLogic(newLogic);
    }

    internal readonly struct Members
    {
        public readonly GridViewerControl GridViewer;
        public readonly QueueViewerControl QueueViewer;
        public readonly CountdownViewerControl CountdownViewer;
        public readonly HealthViewerControl HealthViewer;
        public readonly GridViewerControl MoverGridViewer;
        public readonly GameOverMenu GameOverMenu;

        public Members(Control me)
        {
            me.FindNode(out GridViewer, nameof(GridViewer));
            me.FindNode(out QueueViewer, nameof(QueueViewer));
            me.FindNode(out CountdownViewer, nameof(CountdownViewer));
            me.FindNode(out HealthViewer, nameof(HealthViewer));
            me.FindNode(out MoverGridViewer, nameof(MoverGridViewer));
            me.FindNode(out GameOverMenu, nameof(GameOverMenu));

            QueueViewer.GridViewer = GridViewer;
        }
    }

    private Members members;

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
        float ladderWidth = RectSize.y * 0.16f;
        const float ladderPadding = 10;
        float queueWidth = ladderWidth;
        const float queuePadding = ladderPadding;

        const bool ShowLadder = true;

        float availWidth = RectSize.x;

        float nonGridWidth = 0;
        if (ShowLadder)
        {
            nonGridWidth += (ladderWidth + ladderPadding);
        }
        if (ShowQueue)
        {
            nonGridWidth += (queueWidth + queuePadding);
        }

        availWidth -= nonGridWidth;

        // Divide by 2 for both gridviewers
        var (gvSize, moverSize) = members.GridViewer.DesiredSize(new Vector2(availWidth / 2, RectSize.y));

        float neededWidth = gvSize.x + nonGridWidth;

        float meCenter = RectSize.x / 2f;
        float left = meCenter - neededWidth / 2f;


        if (ShowLadder)
        {
            members.HealthViewer.RectSize = new Vector2(ladderWidth, RectSize.y);
            members.HealthViewer.RectPosition = new Vector2(left, 0);
            left += ladderWidth;
            left += ladderPadding;
        }

        // show main Grid
        members.MoverGridViewer.RectSize = moverSize;
        members.MoverGridViewer.RectPosition = new Vector2(left, 0);

        members.GridViewer.RectSize = gvSize;
        members.GridViewer.RectPosition = new Vector2(left, moverSize.y);

        left += gvSize.x;

        if (ShowQueue)
        {
            left += queuePadding;

            var queueBottom = RectSize.y / 2f;// * 2f;
            members.QueueViewer.RectSize = new Vector2(queueWidth, queueBottom);
            members.QueueViewer.RectPosition = new Vector2(left, 0);

            members.CountdownViewer.RectSize = new Vector2(queueWidth, RectSize.y - queueBottom);
            members.CountdownViewer.RectPosition = new Vector2(left, queueBottom);
            members.CountdownViewer.Visible = true;

            left += queueWidth;
        }
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
        members.QueueViewer.Update();
        members.CountdownViewer.Update();
        members.HealthViewer.Update();
        members.MoverGridViewer.Update();

        this.logic.CheckGameOver();
    }

    public void StartGame(SeededSettings ss)
    {
        NewGame(ss);
        members.GameOverMenu.Visible = false;
    }

    internal interface ILogic
    {
        void Cleanup();
        void Process(float delta);
        void HandleInput();
        void CheckGameOver();
        void Initialize(Members members);
    }

    sealed class NullLogic : ILogic
    {
        private NullLogic() { }

        public static readonly NullLogic Instance = new NullLogic();

        public void Cleanup() { }
        public void Process(float delta) { }
        public void HandleInput() { }
        public void CheckGameOver() { }

        public void Initialize(Members members)
        {
            members.GridViewer.SetLogic(GridViewerControl.NullLogic.Instance);
            members.GridViewer.Visible = true;
            // TODO should set a null model here... but we'll just make them invisible for now
            members.QueueViewer.Visible = false;
        }
    }

    private static void StandardInitialize(Members members, Ticker ticker)
    {
        members.GridViewer.SetLogic(ticker);
        var state = ticker.state;
        members.QueueViewer.Model = state.MakeQueueModel();

        members.GridViewer.Visible = true;
        members.QueueViewer.Visible = true;

        members.MoverGridViewer.SetLogicForMover(ticker);

        members.CountdownViewer.SetModel(state.CountdownViewmodel);

        if (state.PenaltyViewmodel != null)
        {
            members.HealthViewer.SetModel(state.PenaltyViewmodel);
            members.HealthViewer.Visible = true;
        }
        else
        {
            members.HealthViewer.SetNullModel();
            members.HealthViewer.Visible = false;
        }
    }

    internal abstract class LogicBase : ILogic
    {
        protected readonly DotnetTicker ticker;
        private bool holdingDrop = false;

        protected LogicBase(DotnetTicker ticker)
        {
            this.ticker = ticker;
        }

        private bool HandleCommand(Command command)
        {
            return ticker.HandleCommand(command);
        }

        public virtual void HandleInput()
        {
            if (Input.IsActionJustPressed("game_left"))
            {
                HandleCommand(Command.Left);
            }
            if (Input.IsActionJustPressed("game_right"))
            {
                HandleCommand(Command.Right);
            }
            if (Input.IsActionJustPressed("game_rotate_cw"))
            {
                HandleCommand(Command.RotateCW);
            }
            if (Input.IsActionJustPressed("game_rotate_ccw"))
            {
                HandleCommand(Command.RotateCCW);
            }

            if (Input.IsActionPressed("game_drop"))
            {
                if (!holdingDrop)
                {
                    HandleCommand(Command.BurstBegin);
                    holdingDrop = true;
                }
            }
            else
            {
                if (holdingDrop)
                {
                    HandleCommand(Command.BurstCancel);
                    holdingDrop = false;
                }
            }
        }

        public virtual void Process(float delta)
        {
            ticker._Process(delta);
        }

        public virtual void Cleanup() { }

        public virtual void CheckGameOver() { }

        public void Initialize(Members members)
        {
            StandardInitialize(members, ticker);
        }
    }

    sealed class LiveGameLogic : LogicBase
    {
        private readonly ReplayWriter? replayWriter;
        private readonly Members members;

        public LiveGameLogic(ReplayWriter? replayWriter, DotnetTicker ticker, Members members)
            : base(ticker)
        {
            this.replayWriter = replayWriter;
            this.members = members;
        }

        public override void Cleanup()
        {
            if (replayWriter != null)
            {
                replayWriter.Flush();
                replayWriter.Close();
                replayWriter.Dispose();
            }
        }

        public override void CheckGameOver()
        {
            var state = ticker.state;
            if (state.IsGameOver && !members.GameOverMenu.Visible)
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

        public void CheckGameOver() { }

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

        public void Initialize(Members members)
        {
            StandardInitialize(members, replayDriver.Ticker);
        }
    }
}
