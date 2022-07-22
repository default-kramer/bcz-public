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

    private void NewGame(PRNG prng)
    {
        // Must clone PRNG *before* creating state
        var prng2 = prng.Clone();

        __state?.Dispose();
        __state = State.Create(prng);
        var replayCollector = new ReplayCollector();
        ticker = new DotnetTicker(__state, tickCalculations, replayCollector);
        gridViewer.Model = new GridViewerModel(__state, ticker, tickCalculations);
        penaltyViewer.Model = __state.MakePenaltyModel();
        queueViewer.Model = __state.MakeQueueModel();

        (this.replayDriver, replayViewer.Model) = BuildReplay(prng2, replayCollector.Commands);
    }

    private static (ReplayDriver, GridViewerModel) BuildReplay(PRNG prng, IReadOnlyList<Stamped<Command>> commands)
    {
        var state = State.Create(prng);
        var calc = new TickCalculations();
        var ticker = new Ticker(state, calc, NullCollector.Instance);
        var replay = new ReplayDriver(ticker, commands);
        var model = new GridViewerModel(state, ticker, calc);
        return (replay, model);
    }

    private GridViewerControl gridViewer = null!;
    private PenaltyViewerControl penaltyViewer = null!;
    private QueueViewerControl queueViewer = null!;
    private GridViewerControl replayViewer = null!;

    public bool ShowPenalties
    {
        get { return penaltyViewer.Visible; }
        set { penaltyViewer.Visible = value; }
    }

    public bool ShowQueue
    {
        get { return queueViewer.Visible; }
        set { queueViewer.Visible = value; }
    }

    public override void _Ready()
    {
        gridViewer = GetNode<GridViewerControl>("GridViewer");
        replayViewer = GetNode<GridViewerControl>("ReplayViewer");

        penaltyViewer = GetNode<PenaltyViewerControl>("PenaltyViewer");

        queueViewer = GetNode<QueueViewerControl>("QueueViewer");
        queueViewer.GridViewer = gridViewer;

        NewGame(PRNG.Create());

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
        var gvSize = gridViewer.DesiredSize(new Vector2(availWidth / 2, RectSize.y));

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
            penaltyViewer.RectSize = new Vector2(pvWidth, RectSize.y);
            penaltyViewer.RectPosition = new Vector2(left, 0);
            left += pvWidth;
        }

        gridViewer.RectSize = gvSize;
        gridViewer.RectPosition = new Vector2(left, 0);
        left += gvSize.x;

        if (ShowQueue)
        {
            queueViewer.RectSize = new Vector2(queueWidth, RectSize.y);
            queueViewer.RectPosition = new Vector2(left, 0);
            left += queueWidth;
        }

        replayViewer.RectSize = gvSize;
        replayViewer.RectPosition = new Vector2(left, 0);
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

        gridViewer.Update();
        replayViewer.Update();
        penaltyViewer.Update();
        queueViewer.Update();
    }
}
