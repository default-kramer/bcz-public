using FF2.Core;
using FF2.Godot;
using FF2.Godot.Controls;
using Godot;
using System;
using System.Collections.Generic;
using Color = FF2.Core.Color;


public class GridViewerControl : Control
{
    private static readonly IReadOnlyGrid defaultGrid = Grid.Create(Grid.DefaultWidth, Grid.DefaultHeight);

    private GridViewerModel Model;

    public void SetModel(GridViewerModel model)
    {
        this.Model = model;
    }

    private IReadOnlyGrid grid { get { return Model?.Grid ?? defaultGrid; } }
    private TrackedSprite[] activeSprites = new TrackedSprite[400]; // should be way more than we need

    private SpritePool spritePool = null!;

    private FlickerState flicker = FlickerState.Initial;

    public override void _Ready()
    {
    }

    float elapsedSeconds = 0;
    public override void _Process(float delta)
    {
        elapsedSeconds += delta;
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
    private static readonly Godot.Color shroudColor = Godot.Color.Color8(0, 0, 0, 120);

    public override void _Draw()
    {
        if (Model?.Grid == null)
        {
            return;
        }

        spritePool = spritePool ?? NewRoot.GetSpritePool(this);

        if (Model.ShouldFlicker)
        {
            flicker = flicker.Elapse(elapsedSeconds);
            elapsedSeconds = 0;
        }
        else
        {
            flicker = FlickerState.Initial;
        }

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

        var fallSampler = Model.GetFallSample();

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
                var screenX = x * screenCellSize + extraX / 2 + 1f;
                if (fallSampler.HasValue)
                {
                    screenY -= fallSampler.Value.GetAdder(loc) * screenCellSize;
                }

                if (flicker.ShowGrid && !previewOcc.HasValue)
                {
                    var YY = canvasY;
                    DrawRect(new Rect2(x * screenCellSize, YY * screenCellSize, screenCellSize + 2, screenCellSize + 2), borderDark);
                    DrawRect(new Rect2(x * screenCellSize + 1, YY * screenCellSize + 1, screenCellSize, screenCellSize), borderLight);
                    DrawRect(new Rect2(x * screenCellSize + 2, YY * screenCellSize + 2, screenCellSize - 2, screenCellSize - 2), bgColor);
                }

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
                    var offset = screenCellSize / 2;
                    sprite.Position = new Vector2(screenX + offset, screenY + offset);
                    sprite.Scale = spriteScale2;
                    if (previewOcc.HasValue)
                    {
                        sprite.Scale *= new Vector2(0.8f, 0.8f);
                    }

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
                    shader.SetShaderParam("my_alpha", previewOcc.HasValue ? 0.75f : 1.0f);
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

        if (Model.ShouldFlicker)
        {
            var height = Model.LastChanceProgress * RectSize.y;
            DrawRect(new Rect2(0, 0, RectSize.x, height), shroudColor, filled: true);
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

    readonly struct FlickerState
    {
        public readonly int Index;
        public readonly bool ShowGrid;
        public readonly float RemainingSeconds;

        private FlickerState(int index, bool showGrid, float remainingSeconds)
        {
            this.Index = index;
            this.ShowGrid = showGrid;
            this.RemainingSeconds = remainingSeconds;
        }

        public static readonly FlickerState Initial = new FlickerState(-1, true, 0);

        public FlickerState Elapse(float elapsedSeconds)
        {
            float remain = this.RemainingSeconds - elapsedSeconds;
            if (remain > 0)
            {
                return new FlickerState(Index, ShowGrid, remain);
            }
            int index = (this.Index + 1) % Flickers.Length;
            return new FlickerState(index, !ShowGrid, Flickers[index]);
        }

        private const float quick = 0.033f;
        private static readonly float[] Flickers = new float[] { quick, quick, quick, 1.4f, quick, quick * 2, quick, quick, quick, 0.8f, quick, 0.4f, quick, quick, quick, 0.5f, quick * 2, 0.4f };
    }
}
