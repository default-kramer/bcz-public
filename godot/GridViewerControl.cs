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
    private ShaderMaterial bgShader;

    public override void _Ready()
    {
        spritePool = new SpritePool(this, SpriteKind.Single, SpriteKind.JoinedUp, SpriteKind.JoinedDown,
            SpriteKind.JoinedLeft, SpriteKind.JoinedRight, SpriteKind.Enemy);

        background = GetNode<TextureRect>("Background");
        backgroundDefaultSize = background.RectSize;
        bgShader = (ShaderMaterial)background.Material;
    }

    private void AdjustBackground(float screenCellSize, float extraX)
    {
        var bgScale = grid.Width * screenCellSize / backgroundDefaultSize.x;
        background.RectScale = new Vector2(bgScale, bgScale);
        background.RectPosition = new Vector2(extraX / 2.0f, 0); // center it ourselves, anchor isn't doing exactly what I expected

        float usedBgHeight = backgroundDefaultSize.x / grid.Width * grid.Height;
        bgShader.SetShaderParam("maxY", usedBgHeight / backgroundDefaultSize.y);

        float corruptionProgress = Convert.ToSingle(State.CorruptionProgress);
        bgShader.SetShaderParam("corruptionProgress", corruptionProgress);
    }

    // When does destruction intensity enter the max value?
    const float DestructionPeakStart = 0.1f;
    // When does destruction intensity exit the max value?
    const float DestructionPeakEnd = 0.3f;
    // When does destruction intensity finish completely?
    const float DestructionEnd = 0.55f;

    private void SendBackgroundDestructionInfo()
    {
        bgShader.SetShaderParam("numColumns", grid.Width);
        bgShader.SetShaderParam("numRows", grid.Height);
        bgShader.SetShaderParam("columnActivation", columnDestructionBitmap);
        bgShader.SetShaderParam("rowActivation", rowDestructionBitmap);

        float intensity = 0f;

        if (timeSinceDestruction < DestructionPeakStart)
        {
            intensity = timeSinceDestruction / DestructionPeakStart;
        }
        else if (timeSinceDestruction < DestructionPeakEnd)
        {
            intensity = 1.0f;
        }
        else if (timeSinceDestruction < DestructionEnd)
        {
            intensity = 1.0f - (timeSinceDestruction - DestructionPeakEnd) / (DestructionEnd - DestructionPeakEnd);
        }
        bgShader.SetShaderParam("destructionIntensity", intensity);
    }

    private float GetCellSize(Vector2 maxSize)
    {
        return Math.Min(maxSize.x / grid.Width, maxSize.y / grid.Height);
    }

    public Vector2 DesiredSize(Vector2 maxSize)
    {
        float cellSize = GetCellSize(maxSize);
        return new Vector2(cellSize * grid.Width, cellSize * grid.Height);
    }

    public override void _Draw()
    {
        var fullSize = this.RectSize;

        // For debugging, show the excess width/height as brown:
        //DrawRect(new Rect2(0, 0, fullSize), Colors.Brown);
        //DrawRect(new Rect2(extraX / 2, extraY / 2, fullSize.x - extraX, fullSize.y - extraY), Colors.Black);

        float screenCellSize = GetCellSize(fullSize);
        float extraX = Math.Max(0, fullSize.x - screenCellSize * grid.Width);
        float extraY = Math.Max(0, fullSize.y - screenCellSize * grid.Height);

        // Occupant sprites are always 360x360 pixels
        float spriteScale = screenCellSize / 360.0f;
        var spriteScale2 = new Vector2(spriteScale, spriteScale);

        AdjustBackground(screenCellSize, extraX);
        SendBackgroundDestructionInfo();

        var temp = State.PreviewPlummet();

        for (int x = 0; x < grid.Width; x++)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                var loc = new Loc(x, y);
                var previewOcc = temp?.GetOcc(loc);
                var occ = previewOcc ?? grid.Get(loc);

                var destroyedOcc = tickCalculations.GetDestroyedOccupant(loc, grid);
                if (destroyedOcc != Occupant.None)
                {
                    occ = destroyedOcc;
                }

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
                else if (occ.Kind == OccupantKind.Enemy)
                {
                    kind = SpriteKind.Enemy;
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

                    float destructionProgress = 0f;
                    if (destroyedOcc == occ)
                    {
                        destructionProgress = Math.Min(1f, timeSinceDestruction / DestructionEnd);
                    }
                    shader.SetShaderParam("destructionProgress", destructionProgress);

                    if (currentSprite.Kind == SpriteKind.Enemy)
                    {
                        shader.SetShaderParam("is_corrupt", y < 7 ? 1.0f : 0.0f);
                    }
                }
            }
        }
    }

    private static readonly Godot.Color Red = Godot.Color.Color8(255, 56, 120);
    private static readonly Godot.Color Blue = Godot.Color.Color8(0, 148, 255);
    private static readonly Godot.Color Yellow = Godot.Color.Color8(255, 255, 107);
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

    private readonly TickCalculations tickCalculations = new TickCalculations();
    int frames = 0;

    int rowDestructionBitmap = 0;
    int columnDestructionBitmap = 0;
    float timeSinceDestruction = DestructionEnd + 1f;
    DateTime startTime = default(DateTime);
    DateTime lastProcess = default(DateTime);

    public override void _Process(float delta)
    {
        if (startTime == default(DateTime))
        {
            startTime = DateTime.UtcNow;
            lastProcess = startTime;
        }
        else
        {
            var now = DateTime.UtcNow;
            var diff = now - lastProcess;
            lastProcess = now;

            State.Elapse(diff.Milliseconds);
        }

        timeSinceDestruction += delta;
        if (timeSinceDestruction < DestructionEnd)
        {
            this.Update();
            return;
        }

        tickCalculations.Reset();

        frames++;

        if (frames % 10 == 0)
        {
            State.Tick(tickCalculations);
        }

        if (tickCalculations.RowDestructionBitmap != 0 || tickCalculations.ColumnDestructionBitmap != 0)
        {
            rowDestructionBitmap = tickCalculations.RowDestructionBitmap;
            columnDestructionBitmap = tickCalculations.ColumnDestructionBitmap;
            timeSinceDestruction = 0.0f;
        }

        this.Update();
    }
}
