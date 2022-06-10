using FF2.Core;
using FF2.Godot;
using FF2.Godot.Controls;
using Godot;
using System;

#nullable enable

public class GameViewerControl : Control
{
    private readonly TickCalculations tickCalculations = new TickCalculations();

    private DotnetTicker timing = null!;
    private State? __state;
    private State State
    {
        get { return __state ?? throw new Exception("TODO missing state"); }
        set
        {
            __state?.Dispose();
            __state = value;
            timing = new DotnetTicker(value, tickCalculations);
            gridViewer.Model = new GridViewerModel(value, timing, tickCalculations);
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
        const float pvWidth = 35;

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

    public override void _Process(float delta)
    {
        var input = GameKeys.None;
        if (Input.IsActionJustPressed("game_left"))
        {
            input |= GameKeys.Left;
        }
        if (Input.IsActionJustPressed("game_right"))
        {
            input |= GameKeys.Right;
        }
        if (Input.IsActionJustPressed("game_rotate_cw"))
        {
            input |= GameKeys.RotateCW;
        }
        if (Input.IsActionJustPressed("game_rotate_ccw"))
        {
            input |= GameKeys.RotateCCW;
        }
        if (Input.IsActionPressed("game_drop"))
        {
            input |= GameKeys.Drop;
        }

        timing._Process(delta, input);
        gridViewer.Update();
    }
}
