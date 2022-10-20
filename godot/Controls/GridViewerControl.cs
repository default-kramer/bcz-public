using FF2.Core;
using FF2.Godot;
using Godot;
using System;
using System.Collections.Generic;
using Color = FF2.Core.Color;

#nullable enable

public class GridViewerControl : Control
{
    private ILogic Logic = NullLogic.Instance;

    public void SetLogic(ILogic logic)
    {
        this.Logic = logic;
    }

    public void SetLogic(Ticker ticker)
    {
        this.Logic = new StandardLogic(ticker);
    }

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

    private static float GetCellSize(Vector2 maxSize, GridSize gridSize)
    {
        return Math.Min(maxSize.x / gridSize.Width, maxSize.y / gridSize.Height);
    }

    public Vector2 DesiredSize(Vector2 maxSize)
    {
        var gridSize = Logic.Grid.Size;
        float cellSize = GetCellSize(maxSize, gridSize);
        return new Vector2(cellSize * gridSize.Width, cellSize * gridSize.Height);
    }

    internal Vector2 CurrentSpriteScale { get; set; }
    internal float CurrentCellSize { get; set; }

    private static readonly Godot.Color borderLight = Godot.Color.Color8(24, 130, 110);
    private static readonly Godot.Color borderDark = borderLight.Darkened(0.3f);
    private static readonly Godot.Color bgColor = Godot.Color.Color8(0, 0, 0);
    private static readonly Godot.Color shroudColor = Godot.Color.Color8(0, 0, 0, 120);

    public override void _Draw()
    {
        var grid = Logic.Grid;
        var gridSize = grid.Size;

        spritePool = spritePool ?? NewRoot.GetSpritePool(this);

        if (Logic.ShouldFlicker)
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

        float screenCellSize = GetCellSize(fullSize, gridSize);
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

        var temp = Logic.PreviewPlummet();

        float burstProgress = Logic.BurstProgress();

        var fallSampler = Logic.GetFallSample();

        for (int x = 0; x < grid.Width; x++)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                var loc = new Loc(x, y);
                var previewOcc = temp?.GetOcc(loc);
                var occ = previewOcc ?? grid.Get(loc);

                var destroyedOcc = Logic.GetDestroyedOccupant(loc);
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

                var index = loc.ToIndex(grid);
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

                if (currentSprite.IsSomething && currentSprite.Sprite != null)
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
                    shader.SetShaderParam("destructionProgress", Logic.DestructionProgress(loc));

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

        if (Logic.ShouldFlicker)
        {
            var height = Logic.LastChanceProgress * RectSize.y;
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

    public interface ILogic
    {
        IReadOnlyGrid Grid { get; }

        /// <summary>
        /// This is used to make the grid flicker when there is very little time left.
        /// </summary>
        bool ShouldFlicker { get; }

        /// <summary>
        /// A value from 0 to 1, where 0 means "plenty of time left" and 0.98 means "you're almost done!"
        /// Note: Current implementation can return negative numbers I think.
        /// </summary>
        float LastChanceProgress { get; }

        Mover? PreviewPlummet();

        float BurstProgress();

        float DestructionProgress(Loc loc);

        Occupant GetDestroyedOccupant(Loc loc);

        FallSample? GetFallSample();
    }

    public sealed class NullLogic : ILogic
    {
        private static readonly IReadOnlyGrid defaultGrid = Grid.Create(Grid.DefaultWidth, Grid.DefaultHeight);

        private NullLogic() { }

        public static readonly NullLogic Instance = new NullLogic();

        IReadOnlyGrid ILogic.Grid => defaultGrid;

        public bool ShouldFlicker => false;

        public float LastChanceProgress => 0f;

        public float BurstProgress()
        {
            return 0f;
        }

        public float DestructionProgress(Loc loc)
        {
            return 0f;
        }

        public Occupant GetDestroyedOccupant(Loc loc)
        {
            return Occupant.None;
        }

        public FallSample? GetFallSample()
        {
            return null;
        }

        public Mover? PreviewPlummet()
        {
            return null;
        }
    }

    public sealed class StandardLogic : ILogic
    {
        private readonly Ticker ticker;
        private readonly State state;
        private readonly ITickCalculations tickCalculations;

        public StandardLogic(Ticker ticker)
        {
            this.ticker = ticker;
            this.state = ticker.state;
            this.tickCalculations = state.TickCalculations;
        }

        public IReadOnlyGrid Grid { get { return state.Grid; } }

        public decimal CorruptionProgress { get { return state.CorruptionProgress; } }

        const int LastChanceMillis = 5000;

        public bool ShouldFlicker { get { return state.RemainingMillis < LastChanceMillis; } }

        public float LastChanceProgress { get { return Convert.ToSingle(LastChanceMillis - state.RemainingMillis) / LastChanceMillis; } }

        public Mover? PreviewPlummet() { return state.PreviewPlummet(); }

        public int ColumnDestructionBitmap => tickCalculations.ColumnDestructionBitmap;
        public int RowDestructionBitmap => tickCalculations.RowDestructionBitmap;

        public float BurstProgress() { return ticker.BurstProgress(); }

        public float DestructionIntensity()
        {
            return ticker.DestructionIntensity();
        }

        public FallSample? GetFallSample()
        {
            return ticker.GetFallSample();
        }

        public Occupant GetDestroyedOccupant(Loc loc)
        {
            return tickCalculations.GetDestroyedOccupant(loc, state.Grid);
        }

        public float DestructionProgress(Loc loc)
        {
            if (GetDestroyedOccupant(loc) != Occupant.None)
            {
                return ticker.DestructionProgress();
            }
            else
            {
                return 0;
            }
        }
    }
}
