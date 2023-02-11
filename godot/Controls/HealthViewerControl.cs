using FF2.Core.Viewmodels;
using Godot;
using System;
using System.Collections.Generic;

#nullable enable

public class HealthViewerControl : Control
{
    private ISlidingPenaltyViewmodel vm = NullViewmodel.Instance;
    private Font font = null!;
    private static readonly Color BoxColor = Godot.Colors.Orange;
    private static readonly Color TextColor = Godot.Colors.Black;
    private Sprites sprites = null!;
    private const int MaxHearts = 3;

    public void SetNullModel()
    {
        this.vm = NullViewmodel.Instance;
    }

    public void SetModel(ISlidingPenaltyViewmodel viewmodel)
    {
        this.vm = viewmodel ?? NullViewmodel.Instance;
    }

    public override void _Ready()
    {
        font = this.GetFont("");
        sprites = new Sprites(NewRoot.GetSpritePool(this), this);
    }

    private const float slowBlinkRate = 0.5f;
    private const float fastBlinkRate = 0.2f;
    float slowBlinker = 0;
    float fastBlinker = 0;
    bool SlowBlinkOn => slowBlinker < slowBlinkRate / 2;
    bool FastBlinkOn => fastBlinker < fastBlinkRate / 2;

    public override void _Process(float delta)
    {
        slowBlinker = (slowBlinker + delta) % slowBlinkRate;
        fastBlinker = (fastBlinker + delta) % fastBlinkRate;
    }

    public override void _Draw()
    {
        sprites.HideAll();

        float boxHeight = RectSize.y / vm.NumSlots;
        float boxWidth = RectSize.x;

        bool hasHealth = vm.GetHealth(out var health);
        int healthAdder = hasHealth ? 1 : 0;
        int numSlots = vm.NumSlots + healthAdder;

        for (int i = 0; i < numSlots; i++)
        {
            var rect = new Rect2(0, i * boxHeight, boxWidth, boxHeight);
            DrawRect(rect, BoxColor, filled: false, width: 2);

            if (hasHealth && i == 0)
            {
                DrawHealth(rect, health);
            }
            else
            {
                int index = i - healthAdder;

                var penalty = vm.GetPenalty(index);
                bool draw = penalty.Size > 0;
                if (draw)
                {
                    if (index == 0)
                    {
                        draw = FastBlinkOn;
                    }
                    else if (index == 1)
                    {
                        draw = SlowBlinkOn;
                    }
                }

                if (draw)
                {
                    DrawRect(rect, BoxColor, filled: true);
                    DrawString(font, rect.Position + new Vector2(8, 15), levels[penalty.Size], TextColor);
                }
            }
        }
    }

    private void DrawHealth(Rect2 box, HealthStatus health)
    {
        float padding = 0.03f * box.Size.x;

        float availX = box.Size.x - padding * 2;
        float availY = box.Size.y - padding * 2;

        float xScale = (availX / health.MaxHealth) / sprites.SpriteWidth;
        float yScale = availY / sprites.SpriteHeight;
        float scale = Math.Min(xScale, yScale);

        float spriteW = scale * sprites.SpriteWidth;
        float spriteH = scale * sprites.SpriteHeight;
        float yOffset = padding + spriteH / 2;

        for (int i = 0; i < health.MaxHealth; i++)
        {
            var item = i < health.CurrentHealth ? sprites.FullHearts[i] : sprites.EmptyHearts[i];
            var sprite = item.Sprite;
            sprite.Visible = true;
            sprite.Scale = new Vector2(scale, scale);

            float xOffset = padding * (i + 1) + spriteW * (0.5f + i);
            sprite.Position = box.Position + new Vector2(xOffset, yOffset);
        }
    }

    private static readonly string[] levels =
    {
        "",
        "o",
        "oo",
        "ooo",
        "oooo",
        "W",
        "W o",
        "W oo",
        "W ooo",
        "W oooo",
        "WW",
        "WW o",
        "WW oo",
        "WW ooo",
        "WW oooo",
    };

    class NullViewmodel : ISlidingPenaltyViewmodel
    {
        private NullViewmodel() { }
        public static readonly NullViewmodel Instance = new NullViewmodel();

        public int NumSlots => 20;

        public PenaltyItem GetPenalty(int index)
        {
            return PenaltyItem.None;
        }

        public bool GetHealth(out HealthStatus status)
        {
            status = default(HealthStatus);
            return false;
        }
    }

    // TODO: Sprites will leak if this control is ever created+destroyed dynamically.
    // There is no reason to use the SpritePool here anyway.
    private class Sprites
    {
        public readonly SpritePool Pool;
        public readonly TrackedSprite[] FullHearts;
        public readonly TrackedSprite[] EmptyHearts;
        public readonly int SpriteWidth;
        public readonly int SpriteHeight;

        public Sprites(SpritePool pool, Control owner)
        {
            this.Pool = pool;
            FullHearts = new TrackedSprite[MaxHearts];
            EmptyHearts = new TrackedSprite[MaxHearts];

            for (int i = 0; i < MaxHearts; i++)
            {
                FullHearts[i] = pool.Rent(FF2.Core.SpriteKind.Heart, owner);
                EmptyHearts[i] = pool.Rent(FF2.Core.SpriteKind.Heart0, owner);
            }

            // Assume both sprites are the same size
            var tex = FullHearts[0].Sprite.Texture;
            SpriteWidth = tex.GetWidth();
            SpriteHeight = tex.GetHeight();
        }

        public void HideAll()
        {
            for (int i = 0; i < MaxHearts; i++)
            {
                FullHearts[i].Sprite.Visible = false;
                EmptyHearts[i].Sprite.Visible = false;
            }
        }

        public void ReturnAll()
        {
            for (int i = 0; i < MaxHearts; i++)
            {
                Pool.Return(FullHearts[i]);
                Pool.Return(EmptyHearts[i]);
            }
        }
    }
}
