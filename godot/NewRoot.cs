using FF2.Core;
using Godot;
using System;

public class NewRoot : Control
{
    private State __state;
    private State State
    {
        get { return __state; }
        set
        {
            __state?.Dispose();
            __state = value;
            gridViewer.State = value;
        }
    }

    private GridViewerControl gridViewer;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        gridViewer = GetNode<GridViewerControl>("GridViewerControl");
        State = State.Create(PRNG.Create());
    }

    public override void _UnhandledKeyInput(InputEventKey e)
    {
        if (e.Pressed && HandleInput((KeyList)e.Scancode))
        {
            this.Update();
            gridViewer.Update(); // TODO seems strange to me that this is necessary...
        }
    }

    private bool HandleInput(KeyList scancode)
    {
        switch (scancode)
        {
            case KeyList.A:
                return State.Move(Direction.Left);
            case KeyList.D:
                return State.Move(Direction.Right);
            case KeyList.J:
                return State.Rotate(clockwise: true);
            case KeyList.K:
                return State.Rotate(clockwise: false);
            case KeyList.H:
                return State.Plummet();
        }

        return false;
    }
}
