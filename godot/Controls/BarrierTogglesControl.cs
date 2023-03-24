using FF2.Core.Viewmodels;
using Godot;
using System;
using System.Collections.Generic;

// To help the Godot parser, seems similar to https://github.com/godotengine/godot/issues/43751
using Kind = System.ValueTuple<bool, int>;

public class BarrierTogglesControl : Control
{
    private IBarrierTogglesViewmodel vm = NullModel.Instance;
    private ToggleSpritePool onSpritePool = null!;
    private ToggleSpritePool offSpritePool = null!;
    private List<PooledSprite<Kind>?> activeSprites = new();

    public void SetModel(IBarrierTogglesViewmodel? vm)
    {
        this.vm = vm ?? NullModel.Instance;
    }

    public override void _Ready()
    {
        onSpritePool = ToggleSpritePool.Create(this, true);
        offSpritePool = ToggleSpritePool.Create(this, false);
    }

    private static Color b1 = Godot.Color.Color8(45, 55, 72);

    public override void _Draw()
    {
        DrawRect(new Rect2(0, 0, RectSize), b1);
        QueueViewerControl.DrawBorder(this, new Rect2(0, 0, RectSize));
        //DrawRect(new Rect2(0, 0, RectSize), Colors.AliceBlue);

        var toggles = vm.GetToggles();
        while (activeSprites.Count < toggles.Count)
        {
            activeSprites.Add(null);
        }

        for (int i = 0; i < activeSprites.Count; i++)
        {
            var toggle = ToggleViewmodel.Nothing;
            if (toggles.Count > i)
            {
                toggle = toggles[i];
            }

            if (toggle.Rank < 1)
            {
                activeSprites[i]?.Return();
                activeSprites[i] = null;
            }
            else
            {
                var key = (toggle.IsGreen, toggle.Rank);
                var sprite = activeSprites[i];
                if (sprite?.Kind != key)
                {
                    sprite?.Return();
                    sprite = key.Item1 ? onSpritePool.Rent(key) : offSpritePool.Rent(key);
                    activeSprites[i] = sprite;
                }
            }
        }

        for (int i = 0; i < activeSprites.Count; i++)
        {
            var sprite = activeSprites[i];
            if (sprite != null)
            {
                sprite.Position = new Vector2(50, 50 + 50 * i);
                sprite.Scale = new Vector2(0.6f, 0.6f);
            }
        }
    }

    private class NullModel : IBarrierTogglesViewmodel
    {
        private static readonly List<ToggleViewmodel> toggles = new();
        public IReadOnlyList<ToggleViewmodel> GetToggles() => toggles;

        private NullModel() { }
        public static readonly NullModel Instance = new NullModel();
    }

    class ToggleSpritePool : SpritePoolBase<Kind>
    {
        public const int MinRank = 2;
        public const int MaxRank = 8;
        public static ToggleSpritePool Create(Control owner, bool on)
        {
            string textureFormat = on ? "res://Sprites/numerals/toggle-on-{0}.bmp" : "res://Sprites/numerals/toggle-off-{0}.bmp";
            var managers = new SpriteManager[MaxRank - MinRank + 1];
            for (int rank = MinRank; rank <= MaxRank; rank++)
            {
                var texture = ResourceLoader.Load<Texture>(string.Format(textureFormat, rank));// "res://Sprites/numerals/toggle-off-6.bmp");
                var manager = new SpriteManager(owner, texture, null, (on, rank));
                managers[Index(rank)] = manager;
            }
            return new ToggleSpritePool(managers);
        }

        private ToggleSpritePool(SpriteManager[] managers) : base(managers) { }

        protected override int GetIndex(Kind kind)
        {
            return Index(kind.Item2);
        }

        private static int Index(int kind)
        {
            return kind - MinRank;
        }
    }
}
