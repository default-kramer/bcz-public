using FF2.Core;
using FF2.Core.Viewmodels;
using Godot;
using System;
using System.Linq;
using System.Collections.Generic;

#nullable enable

public class HealthViewerControl : Control
{
    private ISlidingPenaltyViewmodel vm = NullViewmodel.Instance;
    private Font font = null!;
    private static readonly Godot.Color BoxColor = Godot.Colors.Orange;
    private static readonly Godot.Color TextColor = Godot.Colors.Black;
    private SpritePoolV2 numeralPool = null!;
    private Hearts hearts = null!;
    private const int MaxHearts = 3;

    public void SetNullModel()
    {
        this.vm = NullViewmodel.Instance;
    }

    public void SetModel(ISlidingPenaltyViewmodel viewmodel)
    {
        this.vm = viewmodel ?? NullViewmodel.Instance;
    }

    const int skNum1 = (int)SpriteKind.Num1;
    const int skNumLast = (int)SpriteKind.Num16;
    private static readonly SpriteKind[] numerals = Enumerable.Range(skNum1, 1 + skNumLast - skNum1)
        .Cast<SpriteKind>().ToArray();

    public override void _Ready()
    {
        font = this.GetFont("");
        numeralPool = new SpritePoolV2(this, numerals);
        hearts = new Hearts(this);
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
        hearts.HideAll();
        numeralPool.ReturnAll();

        bool hasHealth = vm.GetHealth(out var health);
        int healthAdder = hasHealth ? 1 : 0;
        int numSlots = vm.NumSlots + healthAdder;

        float boxHeight = RectSize.y / numSlots;
        float boxWidth = RectSize.x;

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
                DrawPenalty(i - healthAdder, rect);
            }
        }
    }

    private void DrawPenalty(int index, Rect2 rect)
    {
        bool draw = true;
        if (index == 0)
        {
            draw = FastBlinkOn;
        }
        else if (index == 1)
        {
            draw = SlowBlinkOn;
        }

        if (!draw)
        {
            return;
        }

        var penalty = vm.GetPenalty(index);
        if (penalty.Size > 0)
        {
            var neededKind = SpriteKind.Num1 - 1 + penalty.Size;
            var sprite = numeralPool.Rent(neededKind);
            DrawRect(rect, BoxColor, filled: true);
            DrawString(font, rect.Position + new Vector2(8, 15), penalty.Size.ToString(), TextColor);
            sprite.ScaleAndCenter(rect);
            var shader = sprite.Material as ShaderMaterial;
            if (shader != null)
            {
                shader.SetShaderParam("destructionProgress", penalty.DestructionProgress);
            }
        }
    }

    private void DrawHealth(Rect2 box, HealthStatus health)
    {
        float padding = 0.03f * box.Size.x;

        float availX = box.Size.x - padding * 2;
        float availY = box.Size.y - padding * 2;

        float xScale = (availX / health.MaxHealth) / hearts.SpriteWidth;
        float yScale = availY / hearts.SpriteHeight;
        float scale = Math.Min(xScale, yScale);

        float spriteW = scale * hearts.SpriteWidth;
        float spriteH = scale * hearts.SpriteHeight;
        float yOffset = padding + spriteH / 2;

        for (int i = 0; i < health.MaxHealth; i++)
        {
            var sprite = i < health.CurrentHealth ? hearts.FullHearts[i] : hearts.EmptyHearts[i];
            sprite.Visible = true;
            sprite.Scale = new Vector2(scale, scale);

            float xOffset = padding * (i + 1) + spriteW * (0.5f + i);
            sprite.Position = box.Position + new Vector2(xOffset, yOffset);
        }
    }

    class NullViewmodel : ISlidingPenaltyViewmodel
    {
        private NullViewmodel() { }
        public static readonly NullViewmodel Instance = new NullViewmodel();

        public int NumSlots => 20;

        public PenaltyViewmodel GetPenalty(int index)
        {
            return PenaltyViewmodel.None;
        }

        public bool GetHealth(out HealthStatus status)
        {
            status = default(HealthStatus);
            return false;
        }
    }

    private class Hearts
    {
        public readonly PooledSprite[] FullHearts;
        public readonly PooledSprite[] EmptyHearts;
        public readonly int SpriteWidth;
        public readonly int SpriteHeight;

        public Hearts(Control owner)
        {
            var pool = new SpritePoolV2(owner, SpriteKind.Heart, SpriteKind.Heart0);

            FullHearts = new PooledSprite[MaxHearts];
            EmptyHearts = new PooledSprite[MaxHearts];
            for (int i = 0; i < MaxHearts; i++)
            {
                FullHearts[i] = pool.Rent(SpriteKind.Heart);
                EmptyHearts[i] = pool.Rent(SpriteKind.Heart0);
            }
            // Assume both sprites are the same size
            var tex = FullHearts[0].Texture;
            SpriteWidth = tex.GetWidth();
            SpriteHeight = tex.GetHeight();
        }

        public void HideAll()
        {
            for (int i = 0; i < MaxHearts; i++)
            {
                FullHearts[i].Visible = false;
                EmptyHearts[i].Visible = false;
            }
        }
    }
}
