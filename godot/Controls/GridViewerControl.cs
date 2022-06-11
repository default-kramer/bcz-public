using FF2.Core;
using FF2.Godot;
using FF2.Godot.Controls;
using Godot;
using System;
using System.Collections.Generic;
using Color = FF2.Core.Color;


public class GridViewerControl : Control
{
    public GridViewerModel Model { get; set; }
    private IReadOnlyGrid grid { get { return Model.Grid; } }
    private TrackedSprite[] activeSprites = new TrackedSprite[400]; // should be way more than we need

    private SpritePool spritePool;
    private TextureRect background;
    private Vector2 backgroundDefaultSize;
    private ShaderMaterial bgShader;

    public override void _Ready()
    {
        spritePool = new SpritePool(this, SpriteKind.Single, SpriteKind.Joined,
            SpriteKind.Enemy, SpriteKind.BlankJoined, SpriteKind.BlankSingle);

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

        float corruptionProgress = Convert.ToSingle(Model.CorruptionProgress);
        bgShader.SetShaderParam("corruptionProgress", corruptionProgress);
    }

    private void SendBackgroundDestructionInfo()
    {
        bgShader.SetShaderParam("numColumns", grid.Width);
        bgShader.SetShaderParam("numRows", grid.Height);
        bgShader.SetShaderParam("columnActivation", Model.ColumnDestructionBitmap);
        bgShader.SetShaderParam("rowActivation", Model.RowDestructionBitmap);
        bgShader.SetShaderParam("destructionIntensity", Model.DestructionIntensity());
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

        var temp = Model.PreviewPlummet();

        float burstProgress = Model.BurstProgress();

        for (int x = 0; x < grid.Width; x++)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                var loc = new Loc(x, y);
                var previewOcc = temp?.GetOcc(loc);
                var occ = previewOcc ?? grid.Get(loc);

                var destroyedOcc = Model.GetDestroyedOccupant(loc);
                if (destroyedOcc != Occupant.None)
                {
                    occ = destroyedOcc;
                }

                var canvasY = grid.Height - (y + 1);
                var screenY = canvasY * screenCellSize + extraY / 2;
                var screenX = x * screenCellSize + extraX / 2;

                SpriteKind kind = GetSpriteKind(occ);

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

                    if (kind == SpriteKind.Joined || kind == SpriteKind.BlankJoined)
                    {
                        sprite.RotationDegrees = occ.Direction switch
                        {
                            Direction.Down => 0,
                            Direction.Left => 90,
                            Direction.Up => 180,
                            Direction.Right => 270,
                            _ => throw new Exception($"assert failed: {occ.Direction}"),
                        };
                    }

                    var shader = (ShaderMaterial)sprite.Material;
                    shader.SetShaderParam("my_color", ToVector(occ.Color));
                    shader.SetShaderParam("my_alpha", previewOcc.HasValue ? 0.5f : 1.0f);
                    shader.SetShaderParam("destructionProgress", Model.DestructionProgress(loc));

                    if (currentSprite.Kind == SpriteKind.Enemy)
                    {
                        shader.SetShaderParam("is_corrupt", y < 7 ? 1.0f : 0.0f);
                    }

                    if (currentSprite.Kind == SpriteKind.BlankSingle || currentSprite.Kind == SpriteKind.BlankJoined)
                    {
                        shader.SetShaderParam("destructionProgress", burstProgress);
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
    private static readonly Vector3 WhiteV = new Vector3(1, 1, 1);

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
            Color.Blank => WhiteV,
            _ => new Vector3(0.5f, 1.0f, 0.8f) // maybe this will jump out at me
        };
    }

    private static SpriteKind GetSpriteKind(Occupant occ)
    {
        if (occ.Kind == OccupantKind.Catalyst)
        {
            if (occ.Direction == Direction.None)
            {
                if (occ.Color == Color.Blank)
                {
                    return SpriteKind.BlankSingle;
                }
                else
                {
                    return SpriteKind.Single;
                }
            }
            else
            {
                if (occ.Color == Color.Blank)
                {
                    return SpriteKind.BlankJoined;
                }
                else
                {
                    return SpriteKind.Joined;
                }
            }
        }
        if (occ.Kind == OccupantKind.Enemy)
        {
            return SpriteKind.Enemy;
        }

        return SpriteKind.None;
    }
}
