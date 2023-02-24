using FF2.Core;
using FF2.Core.Viewmodels;
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

    public void SetLogicForAttackGrid(IReadOnlyGridSlim grid)
    {
        this.Logic = new AttackGridLogic(grid);
    }

    private TrackedSprite[] activeSprites = new TrackedSprite[400]; // should be way more than we need

    private SpritePool spritePool = null!;

    private FlickerState flicker = FlickerState.Initial;

    public override void _Ready()
    {
        spritePool = NewRoot.GetSpritePool(this);
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

    public override void _Draw()
    {
        Logic.Update();

        var grid = Logic.Grid;
        var gridSize = grid.Size;

        if (Logic.ShouldFlicker)
        {
            flicker = flicker.Elapse(elapsedSeconds);
            elapsedSeconds = 0;
        }
        else
        {
            flicker = FlickerState.Initial;
        }

        DrawRect(new Rect2(default(Vector2), this.RectSize), bgColor);

        var fullSize = this.RectSize - new Vector2(0, yPadding * 2);

        float screenCellSize = GetCellSize(fullSize, gridSize);
        float extraX = Math.Max(0, fullSize.x - screenCellSize * gridSize.Width);
        float extraY = Math.Max(0, fullSize.y - screenCellSize * gridSize.Height);
        float spriteWTF = -extraY / 2; // not sure why this is needed, but it works

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

        for (int x = 0; x < gridSize.Width; x++)
        {
            for (int y = 0; y < gridSize.Height; y++)
            {
                var loc = new Loc(x, y);
                var previewOcc = temp?.GetOcc(loc);
                var occ = previewOcc ?? grid.Get(loc);

                var destroyedOcc = Logic.GetDestroyedOccupant(loc);
                if (destroyedOcc != Occupant.None)
                {
                    occ = destroyedOcc;
                }

                var gridY = gridSize.Height - (y + 1);
                var screenY = gridY * screenCellSize + extraY / 2 + yPadding;
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
                    var (borderLight, borderDark) = Logic.BorderColor(loc);

                    var YY = gridY;
                    DrawRect(new Rect2(screenX - 1, YY * screenCellSize + yPadding, screenCellSize + 2, screenCellSize + 2), borderDark);
                    DrawRect(new Rect2(screenX, YY * screenCellSize + 1 + yPadding, screenCellSize, screenCellSize), borderLight);
                    DrawRect(new Rect2(screenX + 1, YY * screenCellSize + 2 + yPadding, screenCellSize - 2, screenCellSize - 2), bgColor);
                }

                if (!Logic.OverrideSpriteKind(occ, loc, out var kind))
                {
                    kind = GetSpriteKind(occ);
                }

                var index = loc.ToIndex(gridSize);
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

                    sprite.Position = new Vector2(screenX + offset, screenY + offset + spriteWTF);
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

        // help debug size
        //DrawRect(new Rect2(5, 5, this.RectSize - new Vector2(10, 10)), Godot.Colors.LightGreen, filled: false);
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

        public virtual float DestructionProgress(Loc loc) => 0;

        public virtual Occupant GetDestroyedOccupant(Loc loc) => Occupant.None;

        public virtual FallSample? GetFallSample() => null;

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
        private static readonly IReadOnlyGridSlim defaultGrid = FF2.Core.Grid.Create();

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

        public override bool ShouldFlicker => state.LastGaspProgress() > 0;

        public override float LastChanceProgress => state.LastGaspProgress();

        public override Mover? PreviewPlummet() => state.PreviewPlummet();

        public int ColumnDestructionBitmap => tickCalculations.ColumnDestructionBitmap;
        public int RowDestructionBitmap => tickCalculations.RowDestructionBitmap;

        public override float BurstProgress() => ticker.BurstProgress();

        public override FallSample? GetFallSample() => ticker.GetFallSample();

        public override float FallSampleOverride(Loc loc) => 0;

        public override Occupant GetDestroyedOccupant(Loc loc)
        {
            if (state.Grid.InBounds(loc))
            {
                return tickCalculations.GetDestroyedOccupant(loc, state.Grid);
            }
            return Occupant.None;
        }

        public override float DestructionProgress(Loc loc)
        {
            if (GetDestroyedOccupant(loc) != Occupant.None)
            {
                return ticker.DestructionProgress();
            }
            else if (!state.Grid.InBounds(loc))
            {
                return MoverDestructionProgress(loc.Add(0, 0 - state.Grid.Height));
            }
            else
            {
                return 0;
            }
        }

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
        private readonly IReadOnlyGridSlim grid;
        public override IReadOnlyGridSlim Grid => grid;

        public AttackGridLogic(IReadOnlyGridSlim grid)
        {
            this.grid = grid;
        }
    }
}
