using FF2.Core;
using Godot;
using System;

using Color = FF2.Core.Color;

public class GridViewerControl : Control
{
    public State State { get; set; }
    private IReadOnlyGrid grid { get { return State.Grid; } }

    public override void _Ready()
    {
    }

    public override void _Draw()
    {
        var fullSize = this.RectSize;

        // For debugging, show the excess width/height as brown:
        DrawRect(new Rect2(0, 0, fullSize), Colors.Brown);

        float screenCellSize = Math.Min(fullSize.x / grid.Width, fullSize.y / grid.Height);
        float extraX = Math.Max(0, fullSize.x - screenCellSize * grid.Width);
        float extraY = Math.Max(0, fullSize.y - screenCellSize * grid.Height);

        DrawRect(new Rect2(extraX / 2, extraY / 2, fullSize.x - extraX, fullSize.y - extraY), Colors.Black);

        var temp = State.PreviewPlummet();

        for (int x = 0; x < grid.Width; x++)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                var loc = new Loc(x, y);
                var previewOcc = temp?.GetOcc(loc);
                var occ = previewOcc ?? grid.Get(loc);

                var canvasY = grid.Height - (y + 1);
                var screenY = canvasY * screenCellSize + extraY / 2;
                var screenX = x * screenCellSize + extraX / 2;

                switch (occ.Kind)
                {
                    case OccupantKind.Catalyst:
                        DrawCatalyst(occ, screenX, screenY, screenCellSize, previewOcc.HasValue);
                        break;
                    case OccupantKind.Enemy:
                        DrawEnemy(occ, screenX, screenY, screenCellSize);
                        break;
                }
            }
        }
    }

    private static Godot.Color ConvertColor(Color color)
    {
        return color switch
        {
            Color.Red => Red,
            Color.Blue => Blue,
            Color.Yellow => Yellow,
            _ => throw new Exception("unexpected color: " + color),
        };
    }

    private readonly Vector2[] enemyPointBuffer = new Vector2[4];

    private void DrawEnemy(Occupant occ, float screenX, float screenY, float screenCellSize)
    {
        var gColor = ConvertColor(occ.Color);
        var half = screenCellSize / 2;
        enemyPointBuffer[0] = new Vector2(screenX + half, screenY);
        enemyPointBuffer[1] = new Vector2(screenX + screenCellSize, screenY + half);
        enemyPointBuffer[2] = new Vector2(screenX + half, screenY + screenCellSize);
        enemyPointBuffer[3] = new Vector2(screenX, screenY + half);
        DrawColoredPolygon(enemyPointBuffer, gColor);
    }

    private void DrawCatalyst(Occupant occ, float screenX, float screenY, float screenCellSize, bool lighten)
    {
        var gColor = ConvertColor(occ.Color);
        if (lighten)
        {
            gColor = new Godot.Color(gColor, 0.5f);
        }

        // always draw the circle
        var radius = screenCellSize / 2;
        var centerX = screenX + radius;
        var centerY = screenY + radius;
        DrawCircle(new Vector2(centerX, centerY), radius, gColor);

        // if we have a partner, draw the half-rectangle
        var half = screenCellSize / 2;
        Rect2 rect;

        switch (occ.Direction)
        {
            case Direction.Left:
                rect = new Rect2(screenX, screenY, half, screenCellSize);
                DrawRect(rect, gColor);
                break;
            case Direction.Right:
                rect = new Rect2(screenX + half, screenY, half, screenCellSize);
                DrawRect(rect, gColor);
                break;
            case Direction.Up:
                rect = new Rect2(screenX, screenY, screenCellSize, half);
                DrawRect(rect, gColor);
                break;
            case Direction.Down:
                rect = new Rect2(screenX, screenY + half, screenCellSize, half);
                DrawRect(rect, gColor);
                break;
        }
    }

    private static readonly Godot.Color Yellow = Colors.Yellow;
    private static readonly Godot.Color Red = Colors.Red;
    private static readonly Godot.Color Blue = Colors.Blue;

    int frames = 0;
    public override void _Process(float delta)
    {
        frames++;

        if (frames % 10 == 0)
        {
            // TODO obviously we should be doing something else here
            var ugly = State.Tick() || State.Tick() || State.Tick();
            if (ugly)
            {
                this.Update();
            }
        }
    }
}
