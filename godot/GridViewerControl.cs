using FF2.Core;
using FF2.Godot;
using Godot;
using System;
using System.Collections.Generic;
using Color = FF2.Core.Color;

public class GridViewerControl : Control
{
    public State State { get; set; }
    private IReadOnlyGrid grid { get { return State.Grid; } }
    private TrackedSprite[] activeSprites = new TrackedSprite[400]; // should be way more than we need

    private SpritePool spritePool;
    private TextureRect background;
    private Vector2 backgroundDefaultSize;

    public override void _Ready()
    {
        spritePool = new SpritePool(this, SpriteKind.Single, SpriteKind.JoinedUp, SpriteKind.JoinedDown, SpriteKind.JoinedLeft, SpriteKind.JoinedRight);
        background = GetNode<TextureRect>("Background");
        backgroundDefaultSize = background.RectSize;
    }

    public override void _Draw()
    {
        var fullSize = this.RectSize;

        // For debugging, show the excess width/height as brown:
        //DrawRect(new Rect2(0, 0, fullSize), Colors.Brown);

        float screenCellSize = Math.Min(fullSize.x / grid.Width, fullSize.y / grid.Height);
        float extraX = Math.Max(0, fullSize.x - screenCellSize * grid.Width);
        float extraY = Math.Max(0, fullSize.y - screenCellSize * grid.Height);

        // Occupant sprites are always 360x360 pixels
        float spriteScale = screenCellSize / 360.0f;
        var spriteScale2 = new Vector2(spriteScale, spriteScale);

        //DrawRect(new Rect2(extraX / 2, extraY / 2, fullSize.x - extraX, fullSize.y - extraY), Colors.Black);

        var bgScale = grid.Width * screenCellSize / backgroundDefaultSize.x;
        background.RectScale = new Vector2(bgScale, bgScale);
        background.RectPosition = new Vector2(extraX / 2.0f, 0); // center it ourselves, anchor isn't doing exactly what I expected

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

                SpriteKind kind = SpriteKind.None;
                if (occ.Kind == OccupantKind.Catalyst)
                {
                    kind = occ.Direction switch
                    {
                        Direction.None => SpriteKind.Single,
                        Direction.Up => SpriteKind.JoinedUp,
                        Direction.Down => SpriteKind.JoinedDown,
                        Direction.Left => SpriteKind.JoinedLeft,
                        Direction.Right => SpriteKind.JoinedRight,
                        _ => SpriteKind.None,
                    };
                }

                var index = grid.Index(loc);
                TrackedSprite previousSprite = activeSprites[index];
                TrackedSprite currentSprite = default(TrackedSprite);

                if (previousSprite.IsSomething)
                {
                    if (previousSprite.Kind == kind)
                    {
                        currentSprite = previousSprite;
                    }
                    else
                    {
                        spritePool.Return(previousSprite);
                        activeSprites[index] = default(TrackedSprite);
                    }
                }

                if (kind != SpriteKind.None && kind != currentSprite.Kind)
                {
                    currentSprite = spritePool.Rent(kind);
                }

                if (currentSprite.IsSomething)
                {
                    activeSprites[index] = currentSprite;

                    var sprite = currentSprite.Sprite;
                    sprite.Position = new Vector2(screenX + screenCellSize / 2, screenY + screenCellSize / 2);
                    sprite.Scale = spriteScale2;
                    var shader = (ShaderMaterial)sprite.Material;
                    shader.SetShaderParam("my_color", ToVector(occ.Color));
                    shader.SetShaderParam("my_alpha", previewOcc.HasValue ? 0.5f : 1.0f);
                }
                else
                {
                    switch (occ.Kind)
                    {
                        case OccupantKind.Enemy:
                            DrawEnemy(occ, screenX, screenY, screenCellSize);
                            break;
                    }
                }
            }
        }
    }

    private static readonly Godot.Color Red = Godot.Color.Color8(255, 0, 0);
    private static readonly Godot.Color Blue = Godot.Color.Color8(0, 148, 255);
    private static readonly Godot.Color Yellow = Godot.Color.Color8(255, 243, 0);
    private static readonly Vector3 RedV = ToVector(Red);
    private static readonly Vector3 BlueV = ToVector(Blue);
    private static readonly Vector3 YellowV = ToVector(Yellow);

    private static Vector3 ToVector(Godot.Color color)
    {
        return new Vector3(color.r, color.g, color.b);
    }

    private static Godot.Color ToColor(Color color)
    {
        return color switch
        {
            Color.Red => Red,
            Color.Blue => Blue,
            Color.Yellow => Yellow,
            _ => throw new Exception("unexpected color: " + color),
        };
    }

    private static Vector3 ToVector(Color color)
    {
        return color switch
        {
            Color.Red => RedV,
            Color.Blue => BlueV,
            Color.Yellow => YellowV,
            _ => new Vector3(0.5f, 1.0f, 0.8f) // maybe this will jump out at me
        };
    }

    private readonly Vector2[] enemyPointBuffer = new Vector2[4];

    private void DrawEnemy(Occupant occ, float screenX, float screenY, float screenCellSize)
    {
        var gColor = ToColor(occ.Color);
        var half = screenCellSize / 2;
        enemyPointBuffer[0] = new Vector2(screenX + half, screenY);
        enemyPointBuffer[1] = new Vector2(screenX + screenCellSize, screenY + half);
        enemyPointBuffer[2] = new Vector2(screenX + half, screenY + screenCellSize);
        enemyPointBuffer[3] = new Vector2(screenX, screenY + half);
        DrawColoredPolygon(enemyPointBuffer, gColor);
    }

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
                //this.Update();
            }
        }

        this.Update();
    }
}
