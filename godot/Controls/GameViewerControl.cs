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
            penaltyViewer.Model = value.penalties;
        }
    }

    private GridViewerControl gridViewer = null!;
    private PenaltyViewerControl penaltyViewer = null!;

    public bool ShowPenalties
    {
        get { return penaltyViewer.Visible; }
        set { penaltyViewer.Visible = value; }
    }

    public override void _Ready()
    {
        gridViewer = GetNode<GridViewerControl>("GridViewer");
        penaltyViewer = GetNode<PenaltyViewerControl>("PenaltyViewer");

        State = State.Create(PRNG.Create());

        GetTree().Root.Connect("size_changed", this, nameof(OnSizeChanged));
        OnSizeChanged();
    }

    public void OnSizeChanged()
    {
        const float pvWidth = 50;

        float availWidth = RectSize.x;
        if (ShowPenalties)
        {
            availWidth -= pvWidth;
        }

        var gvSize = gridViewer.DesiredSize(new Vector2(availWidth, RectSize.y));

        float totalWidth = gvSize.x;
        if (ShowPenalties)
        {
            totalWidth += pvWidth;
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
        //left += gvWidth;
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

    bool bursting = false;

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
            bursting |= ticker.HandleCommand(Command.BurstBegin);
        }
        else
        {
            if (bursting)
            {
                ticker.HandleCommand(Command.BurstCancel);
            }
            bursting = false;
        }

        gridViewer.Update();
        penaltyViewer.Update();
    }
}
