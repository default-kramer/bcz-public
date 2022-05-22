using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Godot;

namespace FF2.Godot
{
    enum SpriteKind
    {
        None,
        Single,
    }

    struct TrackedSprite
    {
        public readonly SpriteKind Kind;
        public readonly Sprite Sprite;

        public TrackedSprite(SpriteKind kind, Sprite sprite)
        {
            this.Kind = kind;
            this.Sprite = sprite;
        }
    }

    // TODO should implement IDisposable probably...
    sealed class SpritePool
    {
        private readonly Control owner;
        private readonly IReadOnlyDictionary<SpriteKind, Pool> pools;

        public SpritePool(Control owner, params SpriteKind[] kinds)
        {
            this.owner = owner;

            var pools = new Dictionary<SpriteKind, Pool>();
            foreach (var kind in kinds)
            {
                pools[kind] = new Pool(owner, kind);
            }
            this.pools = pools;
        }

        public TrackedSprite Rent(SpriteKind kind)
        {
            return pools[kind].Rent();
        }

        public void Return(TrackedSprite sprite)
        {
            pools[sprite.Kind].Return(sprite);
        }

        sealed class Pool
        {
            private readonly SpriteKind kind;
            private readonly Control owner;
            private readonly Sprite template;
            private readonly Stack<TrackedSprite> available = new Stack<TrackedSprite>();
            private int RentCount = 0;

            public Pool(Control owner, SpriteKind kind)
            {
                this.kind = kind;
                this.owner = owner;
                template = owner.GetNode<Sprite>(kind.ToString());
            }

            public TrackedSprite Rent()
            {
                if (++RentCount > 100)
                {
                    throw new Exception("Got a leak here");
                }

                if (available.Count > 0)
                {
                    var item = available.Pop();
                    item.Sprite.Visible = true;
                    return item;
                }
                else
                {
                    var clone = (Sprite)template.Duplicate();
                    clone.Visible = true;
                    owner.AddChild(clone);
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
}
