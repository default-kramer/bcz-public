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

    private SpritePool spritePool = null!;

    public override void _Ready()
    {
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

    internal Vector2 CurrentSpriteScale { get; set; }
    internal float CurrentCellSize { get; set; }

    private static readonly Godot.Color borderLight = Godot.Color.Color8(24, 130, 110);
    private static readonly Godot.Color borderDark = borderLight.Darkened(0.3f);
    private static readonly Godot.Color bgColor = Godot.Color.Color8(0, 0, 0);

    public override void _Draw()
    {
        spritePool = spritePool ?? NewRoot.GetSpritePool(this);

        const int padding = 2;

        DrawRect(new Rect2(default(Vector2), this.RectSize), bgColor);
        var fullSize = this.RectSize - new Vector2(padding, padding);

        float screenCellSize = GetCellSize(fullSize);
        float extraX = Math.Max(0, fullSize.x - screenCellSize * grid.Width);
        float extraY = Math.Max(0, fullSize.y - screenCellSize * grid.Height);

        // For debugging, show the excess width/height as brown:
        //DrawRect(new Rect2(0, 0, fullSize), Colors.Brown);
        //DrawRect(new Rect2(extraX / 2, extraY / 2, fullSize.x - extraX, fullSize.y - extraY), Colors.Black);

        // Occupant sprites are always 360x360 pixels
        float spriteScale = screenCellSize / 360.0f;
        var spriteScale2 = new Vector2(spriteScale, spriteScale);
        CurrentSpriteScale = spriteScale2;
        CurrentCellSize = screenCellSize;

        var temp = Model.PreviewPlummet();

        float burstProgress = Model.BurstProgress();

        for (int x = 0; x < grid.Width; x++)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                DrawRect(new Rect2(x * screenCellSize, y * screenCellSize, screenCellSize + 2, screenCellSize + 2), borderDark);
                DrawRect(new Rect2(x * screenCellSize + 1, y * screenCellSize + 1, screenCellSize, screenCellSize), borderLight);
                DrawRect(new Rect2(x * screenCellSize + 2, y * screenCellSize + 2, screenCellSize - 2, screenCellSize - 2), bgColor);

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
                var screenX = x * screenCellSize + extraX / 2 + 1f;

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
                    currentSprite = spritePool.Rent(kind, this);
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
                    shader.SetShaderParam("my_color", GameColors.ToVector(occ.Color));
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

    internal static SpriteKind GetSpriteKind(Occupant occ)
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
