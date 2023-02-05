using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FF2.Core;
using Godot;

#nullable enable

    readonly struct TrackedSprite
    {
        public readonly SpriteKind Kind;
        public readonly Sprite Sprite;

        public TrackedSprite(SpriteKind kind, Sprite sprite)
        {
            this.Kind = kind;
            this.Sprite = sprite;
        }

        public bool IsNothing { get { return Kind == SpriteKind.None; } }
        public bool IsSomething { get { return Kind != SpriteKind.None; } }
    }

// TODO should implement IDisposable probably...
sealed class SpritePool
{
    private readonly IReadOnlyDictionary<SpriteKind, Pool> pools;

    public SpritePool(Control owner, params SpriteKind[] kinds)
    {
        var pools = new Dictionary<SpriteKind, Pool>();
        foreach (var kind in kinds)
        {
            var template = owner.GetNode<Sprite>(kind.ToString());
            pools[kind] = new Pool(template, kind);
        }
        this.pools = pools;
    }

    public TrackedSprite Rent(SpriteKind kind, Control owner)
    {
        return pools[kind].Rent(owner);
    }

    public void Return(TrackedSprite sprite)
    {
        pools[sprite.Kind].Return(sprite);
    }

    sealed class Pool
    {
        private readonly SpriteKind kind;
        private readonly Sprite template;
        private readonly Stack<TrackedSprite> available = new Stack<TrackedSprite>();
        private int RentCount = 0;

        public Pool(Sprite template, SpriteKind kind)
        {
            this.template = template;
            this.kind = kind;
        }

        public TrackedSprite Rent(Control owner)
        {
            if (++RentCount > 200)
            {
                throw new Exception("Got a leak here");
            }

            if (available.Count > 0)
            {
                var item = available.Pop();
                item.Sprite.Visible = true;
                if (item.Sprite.Owner != owner)
                {
                    item.Sprite.Owner.RemoveChild(item.Sprite);
                    owner.AddChild(item.Sprite);
                    item.Sprite.Owner = owner;
                }
                return item;
            }
            else
            {
                var clone = (Sprite)template.Duplicate();
                // Until Godot 4.0 gives us per-instance uniforms, we need to duplicate the shader also.
                // https://godotengine.org/article/godot-40-gets-global-and-instance-shader-uniforms
                var sm = template.Material as ShaderMaterial;
                if (sm != null)
                {
                    clone.Material = sm.Duplicate() as ShaderMaterial;
                }
                clone.Visible = true;
                owner.AddChild(clone);
                clone.Owner = owner;
                return new TrackedSprite(kind, clone);
            }
        }

        public void Return(TrackedSprite sprite)
        {
            if (sprite.Kind != kind)
            {
                throw new Exception("Invalid release");
            }
            RentCount--;
            sprite.Sprite.Visible = false;
            available.Push(sprite);
        }
    }
}
