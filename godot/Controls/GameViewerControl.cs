using FF2.Core;
using FF2.Godot;
using FF2.Godot.Controls;
using Godot;
using System;
using System.Collections.Generic;

#nullable enable

public class GameViewerControl : Control
{
    private readonly TickCalculations tickCalculations = new TickCalculations();

    // Temp code testing the replay functionality; should get pulled out eventually.
    class ReplayCollector : IReplayCollector
    {
        public readonly List<Stamped<Command>> Commands = new List<Stamped<Command>>();

        public void Collect(Stamped<Command> command)
        {
            Commands.Add(command);
        }
    }

    class NullCollector : IReplayCollector
    {
        public void Collect(Stamped<Command> command) { }

        public static readonly NullCollector Instance = new NullCollector();
    }

    private DotnetTicker ticker = null!;
    private ReplayDriver replayDriver = null!;
    private State? __state;
    private State State
    {
        get { return __state ?? throw new Exception("TODO missing state"); }
    }

    private void NewGame(SeededSettings ss)
    {
        __state?.Dispose();
        __state = State.Create(ss);
        var replayCollector = new ReplayCollector();
        ticker = new DotnetTicker(__state, tickCalculations, replayCollector);
        members.GridViewer.Model = new GridViewerModel(__state, ticker, tickCalculations);
        members.PenaltyViewer.Model = __state.MakePenaltyModel();
        members.QueueViewer.Model = __state.MakeQueueModel();
        members.GameOverMenu.Visible = false;

        (this.replayDriver, members.ReplayViewer.Model) = BuildReplay(ss, replayCollector.Commands);
    }

    private static (ReplayDriver, GridViewerModel) BuildReplay(SeededSettings ss, IReadOnlyList<Stamped<Command>> commands)
    {
        var state = State.Create(ss);
        var calc = new TickCalculations();
        var ticker = new Ticker(state, calc, NullCollector.Instance);
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

        // TODO needed to avoid null refs, should fix this so we can exist without a state
        StartGame(SinglePlayerSettings.Default.AddRandomSeed());

        GetTree().Root.Connect("size_changed", this, nameof(OnSizeChanged));
        OnSizeChanged();
    }

    public void OnSizeChanged()
    {
        const float pvWidth = 50;
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

    bool holdingDrop = false;

    public override void _Process(float delta)
    {
        ticker._Process(delta);

        var laggingNow = ticker.Now.AddMillis(-3000);
        if (laggingNow.Millis > 0)
        {
            replayDriver.Advance(laggingNow);
        }

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

        members.GridViewer.Update();
        members.ReplayViewer.Update();
        members.PenaltyViewer.Update();
        members.QueueViewer.Update();

        if (State.Kind == StateKind.GameOver && !members.GameOverMenu.Visible)
        {
            members.GameOverMenu.OnGameOver(State);
        }
    }

    public void StartGame(SeededSettings ss)
    {
        // TODO should we use SetProcess(false/true) when a game is or is not active?
        NewGame(ss);
        members.GameOverMenu.Visible = false;
    }
}
