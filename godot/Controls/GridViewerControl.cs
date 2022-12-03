using FF2.Core;
using FF2.Core.Viewmodels;
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

    public void SetLogicForhealth(Ticker ticker)
    {
        this.Logic = new HealthLogic(ticker.state.HealthModel);
    }

    public void SetLogicForMover(Ticker ticker)
    {
        this.Logic = new MoverLogic(ticker.state);
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

    public (Vector2 mainSize, Vector2 moverSize) DesiredSize(Vector2 maxSize)
    {
        var gridSize = Logic.Grid.Size;

        // reserve 2 rows for mover
        maxSize = new Vector2(maxSize.x, maxSize.y * gridSize.Height / (gridSize.Height + MoverLogic.GridHeight));

        float cellSize = GetCellSize(maxSize, gridSize);
        var mainSize = new Vector2(cellSize * gridSize.Width, cellSize * gridSize.Height);
        var moverSize = new Vector2(cellSize * gridSize.Width, cellSize * MoverLogic.GridHeight);
        return (mainSize, moverSize);
    }

    internal Vector2 CurrentSpriteScale { get; set; }
    internal float CurrentCellSize { get; set; }

    private static readonly Godot.Color defaultBorderLight = Godot.Color.Color8(24, 130, 110);
    private static readonly Godot.Color defaultBorderDark = defaultBorderLight.Darkened(0.3f);
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

        var (borderLight, borderDark) = Logic.BorderColor;

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

                float adder = 0f;
                if (fallSampler.HasValue)
                {
                    adder += fallSampler.Value.GetAdder(loc);
                }
                adder += Logic.FallSampleOverride(loc);
                screenY -= adder * screenCellSize;

                if (flicker.ShowGrid && !previewOcc.HasValue)
                {
                    var YY = canvasY;
                    DrawRect(new Rect2(screenX - 1, YY * screenCellSize, screenCellSize + 2, screenCellSize + 2), borderDark);
                    DrawRect(new Rect2(screenX, YY * screenCellSize + 1, screenCellSize, screenCellSize), borderLight);
                    DrawRect(new Rect2(screenX + 1, YY * screenCellSize + 2, screenCellSize - 2, screenCellSize - 2), bgColor);
                }

                if (!Logic.OverrideSpriteKind(occ, out var kind))
                {
                    kind = GetSpriteKind(occ);
                }

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

                    var shader = sprite.Material as ShaderMaterial;
                    if (shader != null)
                    {
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

        float FallSampleOverride(Loc loc);

        bool OverrideSpriteKind(Occupant occ, out SpriteKind spriteKind);

        (Godot.Color light, Godot.Color dark) BorderColor { get; }
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

        public float FallSampleOverride(Loc loc) { return 0; }

        public Mover? PreviewPlummet()
        {
            return null;
        }

        public bool OverrideSpriteKind(Occupant occ, out SpriteKind spriteKind)
        {
            spriteKind = default(SpriteKind);
            return false;
        }

        public (Godot.Color light, Godot.Color dark) BorderColor => (defaultBorderLight, defaultBorderDark);
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

        public bool ShouldFlicker { get { return state.LastGaspProgress() > 0; } }

        public float LastChanceProgress => state.LastGaspProgress();

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

        public float FallSampleOverride(Loc loc) { return 0; }

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

        public bool OverrideSpriteKind(Occupant occ, out SpriteKind spriteKind)
        {
            spriteKind = default(SpriteKind);
            return false;
        }

        public (Godot.Color light, Godot.Color dark) BorderColor => (defaultBorderLight, defaultBorderDark);
    }

    public sealed class HealthLogic : ILogic
    {
        private readonly IReadOnlyGrid grid;
        private readonly IHealthGridViewmodel health;

        public HealthLogic(IHealthGridViewmodel health)
        {
            this.grid = health.Grid;
            this.health = health;
        }

        public IReadOnlyGrid Grid => grid;

        public bool ShouldFlicker => health.LastGaspProgress() > 0;

        public float LastChanceProgress => health.LastGaspProgress();

        public float BurstProgress()
        {
            return 0f;
        }

        public float DestructionProgress(Loc loc)
        {
            return health.DestructionProgress(loc);
        }

        public Occupant GetDestroyedOccupant(Loc loc)
        {
            return Occupant.None;
        }

        public FallSample? GetFallSample()
        {
            return null;
        }

        public float FallSampleOverride(Loc loc) { return health.GetAdder(loc); }

        public Mover? PreviewPlummet()
        {
            return null;
        }

        public bool OverrideSpriteKind(Occupant occ, out SpriteKind spriteKind)
        {
            spriteKind = HealthOccupants.Translate(occ, SpriteKind.None);
            return spriteKind != SpriteKind.None;
        }

        public (Godot.Color light, Godot.Color dark) BorderColor => (defaultBorderLight, defaultBorderDark);
    }

    sealed class MoverLogic : ILogic
    {
        public IReadOnlyGrid Grid => grid;
        private readonly MoverGrid grid;

        public const int GridHeight = 2;

        public MoverLogic(State state)
        {
            this.grid = new MoverGrid(state);
        }

        class MoverGrid : IReadOnlyGrid
        {
            private readonly State state;
            public MoverGrid(State state)
            {
                this.state = state;
            }

            public int Width => state.Grid.Width;

            public int Height => GridHeight;

            public GridSize Size => new GridSize(Width, Height);

            public string PrintGrid => throw new NotImplementedException();

            public string DiffGridString(params string[] rows)
            {
                throw new NotImplementedException();
            }

            public Occupant Get(Loc loc)
            {
                var x = state.GetMover;
                if (x == null)
                {
                    return Occupant.None;
                }
                var mover = x.Value;
                if (loc == mover.LocA)
                {
                    return mover.OccA;
                }
                if (loc == mover.LocB)
                {
                    return mover.OccB;
                }
                return Occupant.None;
            }

            public int HashGrid()
            {
                throw new NotImplementedException();
            }

            public bool InBounds(Loc loc)
            {
                throw new NotImplementedException();
            }

            public bool IsVacant(Loc loc)
            {
                throw new NotImplementedException();
            }

            public IImmutableGrid MakeImmutable()
            {
                throw new NotImplementedException();
            }

            public Mover NewMover(SpawnItem item)
            {
                throw new NotImplementedException();
            }

            public ReadOnlySpan<Occupant> ToSpan()
            {
                throw new NotImplementedException();
            }
        }

        public bool ShouldFlicker => false;

        public float LastChanceProgress => 0;

        public float BurstProgress() => 0;

        public float DestructionProgress(Loc loc) => 0;

        public float FallSampleOverride(Loc loc) => 0;

        public Occupant GetDestroyedOccupant(Loc loc) => Occupant.None;

        public FallSample? GetFallSample() => null;

        public bool OverrideSpriteKind(Occupant occ, out SpriteKind spriteKind)
        {
            spriteKind = SpriteKind.None;
            return false;
        }

        public Mover? PreviewPlummet() => null;

        public (Godot.Color light, Godot.Color dark) BorderColor => (borderLight, borderDark);

        private static readonly Godot.Color borderLight = Godot.Color.Color8(215, 215, 215);
        private static readonly Godot.Color borderDark = Godot.Color.Color8(160, 160, 160);
    }
}
