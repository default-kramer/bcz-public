using BCZ.Core;
using BCZ.Core.Viewmodels;
using Godot;
using System;
using System.Collections.Generic;
using Color = BCZ.Core.Color;

#nullable enable

public class GridViewerControl : Control
{
    private ILogic Logic = NullLogic.Instance;
    private float countdown = 0; // 1f is "no time remains"?

    public void SetLogic(ILogic logic)
    {
        this.Logic = logic;
        countdown = logic.LastChanceProgress;
    }

    public void SetLogic(Ticker ticker)
    {
        SetLogic(new StandardLogic(ticker));
    }

    public void SetLogicForAttackGrid(IAttackGridViewmodel vm)
    {
        SetLogic(new AttackGridLogic(vm));
    }

    /// <summary>
    /// Loc -> Index -> Sprite, allowing us to detect a sprite that has not moved
    /// since the last Draw()
    /// </summary>
    private PooledSprite<SpriteKind>?[] activeSprites = new PooledSprite<SpriteKind>?[400]; // should be way more than we need

    private SpritePoolV2 spritePool = null!;

    private FlickerState flicker = FlickerState.Initial;

    public override void _Ready()
    {
        spritePool = SpritePoolV2.Make(this, SpriteKind.Single, SpriteKind.Joined,
            SpriteKind.BlankJoined, SpriteKind.BlankSingle, SpriteKind.Enemy,
            SpriteKind.Barrier0, SpriteKind.Barrier1, SpriteKind.Barrier2, SpriteKind.Barrier3,
            SpriteKind.Barrier4, SpriteKind.Barrier5, SpriteKind.Barrier6, SpriteKind.Barrier7);
    }

    float elapsedSeconds = 0;
    float drawSeconds = 0; // seconds since last Draw() call
    public override void _Process(float delta)
    {
        elapsedSeconds += delta;
        if (!paused)
        {
            drawSeconds += delta;
        }
    }

    private bool paused = false;
    public void SetPaused(bool paused)
    {
        this.paused = paused;
    }

    private static float GetCellSize(Vector2 maxSize, GridSize gridSize)
    {
        return Math.Min(maxSize.x / gridSize.Width, maxSize.y / gridSize.Height);
    }

    const int yPadding = 0; // No longer needed... but maybe it will be needed in the future.

    public float DesiredWidth(float height)
    {
        var size = Logic.Grid.Size;
        var cellSize = GetCellSize(new Vector2(9999, height - yPadding * 2), size);
        return cellSize * size.Width;
    }

    internal Vector2 CurrentSpriteScale { get; set; }
    internal float CurrentCellSize { get; set; }

    private static readonly Godot.Color defaultBorderLight = Godot.Color.Color8(24, 130, 110);
    private static readonly Godot.Color defaultBorderDark = defaultBorderLight.Darkened(0.3f);
    private static readonly Godot.Color bgColor = Godot.Color.Color8(0, 0, 0);
    private static readonly Godot.Color shroudColor = Godot.Color.Color8(0, 0, 0, 120);

    /// <summary>
    /// Should be calculated once per <see cref="_Draw"/>
    /// </summary>
    ref struct Dimensions
    {
        public GridSize gridSize;
        public float screenCellSize;
        public float extraX;
        public float extraY;
        public float spriteWTF;

        /// <summary>
        /// Should be calculated once per <paramref name="loc"/>.
        /// </summary>
        public CellDimensions CalculateCellDimensions(Loc loc)
        {
            var gridY = gridSize.Height - (loc.Y + 1);
            var screenY = gridY * screenCellSize + extraY / 2 + yPadding;
            var screenX = loc.X * screenCellSize + extraX / 2 + 1f;
            return new CellDimensions()
            {
                dimensions = this,
                gridY = gridY,
                screenX = screenX,
                screenY = screenY,
            };
        }
    }

    ref struct CellDimensions
    {
        public Dimensions dimensions;
        public float gridY;
        public float screenX;
        public float screenY;

        public void DrawBorder(Control control, Loc loc, ILogic Logic)
        {
            var (borderLight, borderDark) = Logic.BorderColor(loc);
            var screenCellSize = dimensions.screenCellSize;

            control.DrawRect(new Rect2(screenX - 1, gridY * screenCellSize + yPadding, screenCellSize + 2, screenCellSize + 2), borderDark);
            control.DrawRect(new Rect2(screenX, gridY * screenCellSize + 1 + yPadding, screenCellSize, screenCellSize), borderLight);
            control.DrawRect(new Rect2(screenX + 1, gridY * screenCellSize + 2 + yPadding, screenCellSize - 2, screenCellSize - 2), bgColor);
        }
    }

    private Dimensions CalculateDimensions(GridSize gridSize)
    {
        var fullSize = this.RectSize - new Vector2(0, yPadding * 2);
        float screenCellSize = GetCellSize(fullSize, gridSize);
        float extraX = Math.Max(0, fullSize.x - screenCellSize * gridSize.Width);
        float extraY = Math.Max(0, fullSize.y - screenCellSize * gridSize.Height);
        float spriteWTF = -extraY / 2; // not sure why this is needed, but it works
        return new Dimensions()
        {
            gridSize = gridSize,
            screenCellSize = screenCellSize,
            extraX = extraX,
            extraY = extraY,
            spriteWTF = spriteWTF,
        };
    }

    public override void _Draw()
    {
        var drawSeconds = this.drawSeconds;
        this.drawSeconds = 0;

        Logic.Update();

        // Update countdown, limiting how fast it can change
        {
            var blah = Logic.LastChanceProgress;
            var delta = blah - countdown;
            var maxDelta = drawSeconds * CountdownViewerControl.TODO_FACTOR;
            if (delta > maxDelta) { delta = maxDelta; }
            else if (delta < -maxDelta) { delta = -maxDelta; }
            countdown += delta;
        }

        var grid = Logic.Grid;
        var gridSize = grid.Size;

        DrawRect(new Rect2(default(Vector2), this.RectSize), bgColor);
        // For debugging, show the excess width/height as brown:
        //DrawRect(new Rect2(0, 0, fullSize), Colors.Brown);
        //DrawRect(new Rect2(extraX / 2, extraY / 2, fullSize.x - extraX, fullSize.y - extraY), Colors.Black);

        var dimensions = CalculateDimensions(gridSize);

        if (paused)
        {
            DrawPaused(grid, ref dimensions);
            return;
        }

        if (Logic.ShouldFlicker)
        {
            flicker = flicker.Elapse(elapsedSeconds);
            elapsedSeconds = 0;
        }
        else
        {
            flicker = FlickerState.Initial;
        }

        // Occupant sprites are always 360x360 pixels
        var screenCellSize = dimensions.screenCellSize;
        float spriteScale = screenCellSize / 360.0f;
        var spriteScale2 = new Vector2(spriteScale, spriteScale);
        CurrentSpriteScale = spriteScale2;
        CurrentCellSize = screenCellSize;
        var offset = screenCellSize / 2; // Used to position sprites in the center (instead of the upper left)

        var temp = Logic.PreviewPlummet();

        float burstProgress = Logic.BurstProgress();

        var fallAnimator = Logic.GetFallAnimator();
        var destructionAnimator = Logic.GetDestructionAnimator();

        for (int x = 0; x < gridSize.Width; x++)
        {
            for (int y = 0; y < gridSize.Height; y++)
            {
                var loc = new Loc(x, y);
                var previewOcc = temp?.GetOcc(loc);
                var occ = previewOcc ?? grid.Get(loc);

                var (destroyedOcc, destructionProgress) = destructionAnimator.GetDestroyedOccupant(loc);
                if (destroyedOcc != Occupant.None)
                {
                    occ = destroyedOcc;
                }

                var cellDimensions = dimensions.CalculateCellDimensions(loc);

                float adder = fallAnimator.GetAdder(loc);
                adder += Logic.FallSampleOverride(loc);
                var screenY = cellDimensions.screenY - adder * screenCellSize;

                if (flicker.ShowGrid && !previewOcc.HasValue)
                {
                    cellDimensions.DrawBorder(this, loc, Logic);
                }

                if (!Logic.OverrideSpriteKind(occ, loc, out var kind))
                {
                    kind = GetSpriteKind(occ, loc);
                }

                var index = loc.ToIndex(gridSize);
                var previousSprite = activeSprites[index];
                PooledSprite<SpriteKind>? currentSprite = null;

                if (previousSprite != null)
                {
                    if (previousSprite.Kind == kind)
                    {
                        currentSprite = previousSprite;
                    }
                    else
                    {
                        previousSprite.Return();
                        activeSprites[index] = null;
                    }
                }

                if (kind != SpriteKind.None && kind != currentSprite?.Kind)
                {
                    currentSprite = spritePool.Rent(kind);
                }

                if (currentSprite != null)
                {
                    activeSprites[index] = currentSprite;
                    var sprite = currentSprite;
                    sprite.Visible = true;
                    sprite.Position = new Vector2(cellDimensions.screenX + offset, screenY + offset + dimensions.spriteWTF);

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
                        shader.SetShaderParam("destructionProgress", destructionProgress);

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

        if (true || Logic.ShouldFlicker)
        {
            const float RESERVED_ROWS = 2f;
            const float GAME_ROWS = 20f;
            const float TOTAL_ROWS = 22f;

            const float xOffset = 2;
            float width = RectSize.x - xOffset;

            float topArea = RectSize.y * RESERVED_ROWS / TOTAL_ROWS;
            const float yOffset = 2;
            topArea += yOffset;

            float topEnd = RectSize.y - topArea;

            // Apply shroud to partially depleted rows (and completely depleted rows too)
            var height = GAME_ROWS * countdown / GAME_ROWS * topEnd;
            //height = Math.Max(height - yOffset, 0);
            DrawRect(new Rect2(xOffset, topArea, width, height), shroudColor, filled: true);

            // Hide rows that are completely depleted.
            const float magic = 0.5f; // I can't explain why this is needed
            height = Convert.ToInt32(GAME_ROWS * countdown - magic) / GAME_ROWS * topEnd;
            height = Math.Max(height - yOffset, 0);
            DrawRect(new Rect2(xOffset, topArea, width, height), bgColor, filled: true);
        }

        // help debug size
        //DrawRect(new Rect2(5, 5, this.RectSize - new Vector2(10, 10)), Godot.Colors.LightGreen, filled: false);
    }

    private void DrawPaused(IReadOnlyGridSlim grid, ref Dimensions dimensions)
    {
        // I had a problem where it was difficult to make the occupant sprites draw *behind* the shroud.
        // Then I realized "why not just hide the sprites while the game is paused?"
        // Otherwise the player could just pause the game, take all the time they need to find the best move
        // unpause, move, and repeat the process.
        // Then I realized the shroud should be a separate control because the z-index problem was also affecting
        // the Game Over menu, where we don't want to hide the sprites.
        // So now I am only hiding the sprites for game design reasons, no longer for technical reasons.
        // Anyway...
        //
        // We're not going to respect recently-destroyed occupants or the fall adder.
        // Look at the grid only; it's good enough and maybe better.

        var gridSize = grid.Size;
        var screenCellSize = dimensions.screenCellSize;
        var padding = screenCellSize * 0.15f;
        var smallerSize = screenCellSize - padding * 2;

        for (int x = 0; x < gridSize.Width; x++)
        {
            for (int y = 0; y < gridSize.Height; y++)
            {
                var loc = new Loc(x, y); var sprite = activeSprites[loc.ToIndex(gridSize)];
                if (sprite != null)
                {
                    sprite.Visible = false;
                }

                var cellDimensions = dimensions.CalculateCellDimensions(loc);
                cellDimensions.DrawBorder(this, loc, Logic);

                var occ = grid.Get(loc);
                if (occ != Occupant.None)
                {
                    var rect = new Rect2(cellDimensions.screenX + padding, cellDimensions.screenY + padding, smallerSize, smallerSize);
                    DrawRect(rect, GameColors.OccupantDuringPause);
                }
            }
        }
    }

    internal static SpriteKind GetSpriteKind(Occupant occ, Loc loc)
    {
        var kind = occ.Kind;
        if (kind == OccupantKind.Catalyst)
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
        if (kind == OccupantKind.Enemy)
        {
            return SpriteKind.Enemy;
        }
        if (kind == OccupantKind.Barrier)
        {
            int index = loc.X % NumBarriers;
            return SpriteKind.Barrier0 + index;
        }

        return SpriteKind.None;
    }

    private const int BarrierFirst = (int)SpriteKind.Barrier0;
    private const int BarrierLast = (int)SpriteKind.Barrier7;
    private const int NumBarriers = BarrierLast - BarrierFirst + 1;

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

    public abstract class ILogic
    {
        public abstract IReadOnlyGridSlim Grid { get; }

        /// <summary>
        /// This is used to make the grid flicker when there is very little time left.
        /// </summary>
        public virtual bool ShouldFlicker => false;

        /// <summary>
        /// A value from 0 to 1, where 0 means "plenty of time left" and 0.98 means "you're almost done!"
        /// Note: Current implementation can return negative numbers I think.
        /// </summary>
        public virtual float LastChanceProgress => 0;

        public virtual Mover? PreviewPlummet() => null;

        public virtual float BurstProgress() => 0;

        public virtual IDestructionAnimator GetDestructionAnimator() => NullDestructionAnimator.Instance;

        public virtual IFallAnimator GetFallAnimator() => NullFallAnimator.Instance;

        public virtual float FallSampleOverride(Loc loc) => 0;

        public virtual bool OverrideSpriteKind(Occupant occ, Loc loc, out SpriteKind spriteKind)
        {
            spriteKind = default(SpriteKind);
            return false;
        }

        public virtual (Godot.Color light, Godot.Color dark) BorderColor(Loc loc) => (defaultBorderLight, defaultBorderDark);

        /// <summary>
        /// Allows the implementation to collect/cache data to be used during the Draw() routine.
        /// </summary>
        public virtual void Update() { }
    }

    public sealed class NullLogic : ILogic
    {
        private static readonly IReadOnlyGridSlim defaultGrid = BCZ.Core.Grid.Create();

        private NullLogic() { }

        public static readonly NullLogic Instance = new NullLogic();

        public override IReadOnlyGridSlim Grid => defaultGrid;
    }

    public sealed class StandardLogic : ILogic
    {
        private readonly Ticker ticker;
        private readonly State state;
        private readonly ITickCalculations tickCalculations;
        private readonly IReadOnlyGridSlim grid;

        public StandardLogic(Ticker ticker)
        {
            this.ticker = ticker;
            this.state = ticker.state;
            this.tickCalculations = state.TickCalculations;

            grid = new GridWithMover(state);
        }

        public override IReadOnlyGridSlim Grid => grid;

        public override bool ShouldFlicker => false;// state.LastGaspProgress() > 0;

        public override float LastChanceProgress => state.LastGaspProgress();

        public override Mover? PreviewPlummet() => state.PreviewPlummet();

        public int ColumnDestructionBitmap => tickCalculations.ColumnDestructionBitmap;
        public int RowDestructionBitmap => tickCalculations.RowDestructionBitmap;

        public override float BurstProgress() => ticker.BurstProgress();

        public override IFallAnimator GetFallAnimator() => ticker.GetFallAnimator();

        public override float FallSampleOverride(Loc loc) => 0;

        public override IDestructionAnimator GetDestructionAnimator() => state.GetDestructionAnimator();

        public override (Godot.Color light, Godot.Color dark) BorderColor(Loc loc)
        {
            if (state.Grid.InBounds(loc))
            {
                return base.BorderColor(loc);
            }
            return (moverBorderLight, moverBorderDark);
        }

        private static readonly Godot.Color moverBorderLight = Godot.Color.Color8(215, 215, 215);
        private static readonly Godot.Color moverBorderDark = Godot.Color.Color8(160, 160, 160);

        private float MoverDestructionProgress(Loc loc)
        {
            var x = state.GetMover;
            if (x == null)
            {
                return 0;
            }

            var mover = x.Value;
            if (loc == mover.LocA || loc == mover.LocB)
            {
                var ev = state.CurrentEvent;
                if (ev.Kind == StateEventKind.Spawned)
                {
                    var progress = ev.Completion.Progress();
                    if (progress < 1f)
                    {
                        return 1 - progress;
                    }
                }
            }

            return 0;
        }

        const int MoverRows = 2;

        class GridWithMover : IReadOnlyGridSlim
        {
            private readonly State state;

            public GridWithMover(State state)
            {
                this.state = state;
            }

            public GridSize Size => new GridSize(state.Grid.Width, state.Grid.Height + MoverRows);

            public Occupant Get(Loc loc)
            {
                if (!state.Grid.InBounds(loc))
                {
                    loc = loc.Add(0, 0 - state.Grid.Height);

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

                return state.Grid.Get(loc);
            }
        }
    }

    sealed class AttackGridLogic : ILogic
    {
        private readonly IAttackGridViewmodel vm;
        private readonly IDestructionAnimator destructionAnimator;
        public override IReadOnlyGridSlim Grid => vm.Grid;

        public AttackGridLogic(IAttackGridViewmodel vm)
        {
            this.vm = vm;
            this.destructionAnimator = new DestructionAnimator(vm);
        }

        public override IDestructionAnimator GetDestructionAnimator() => destructionAnimator;

        class DestructionAnimator : IDestructionAnimator
        {
            private readonly IAttackGridViewmodel vm;

            public DestructionAnimator(IAttackGridViewmodel vm)
            {
                this.vm = vm;
            }

            public (Occupant, float) GetDestroyedOccupant(Loc loc)
            {
                var occ = vm.Grid.Get(loc);
                if (vm.IsFrozen(loc))
                {
                    return (occ, 0.5f);
                }
                return (occ, 0f);
            }
        }
    }
}
