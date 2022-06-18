using FF2.Core;
using FF2.Godot;
using FF2.Godot.Controls;
using Godot;
using System;

#nullable enable

public class GameViewerControl : Control
{
    private readonly TickCalculations tickCalculations = new TickCalculations();

    private DotnetTicker ticker = null!;
    private State? __state;
    private State State
    {
        get { return __state ?? throw new Exception("TODO missing state"); }
        set
        {
            __state?.Dispose();
            __state = value;
            ticker = new DotnetTicker(value, tickCalculations);
            gridViewer.Model = new GridViewerModel(value, ticker, tickCalculations);
            penaltyViewer.Model = value.MakePenaltyModel();
            queueViewer.Model = value.MakeQueueModel();
        }
    }

    private GridViewerControl gridViewer = null!;
    private PenaltyViewerControl penaltyViewer = null!;
    private QueueViewerControl queueViewer = null!;

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

        penaltyViewer = GetNode<PenaltyViewerControl>("PenaltyViewer");

        queueViewer = GetNode<QueueViewerControl>("QueueViewer");
        queueViewer.GridViewer = gridViewer;

        State = State.Create(PRNG.Create());

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

        var gvSize = gridViewer.DesiredSize(new Vector2(availWidth, RectSize.y));

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
        penaltyViewer.Update();
        queueViewer.Update();
    }
}
