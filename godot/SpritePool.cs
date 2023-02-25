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

    public static readonly TrackedSprite Nothing = default(TrackedSprite);

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

        const bool addNumerals = true;
        if (addNumerals)
        {
            // We expect the first one to have the shader and we will copy it to all the other numerals.
            Material? numeralMaterial = null;

            for (var kind = SpriteKind.Num1; kind <= SpriteKind.Num16; kind++)
            {
                var sprite = owner.GetNode<Sprite>(kind.ToString());
                numeralMaterial = numeralMaterial ?? sprite.Material;

                if (sprite.Material == null)
                {
                    sprite.Material = numeralMaterial!.Duplicate() as Material;
                }

                pools[kind] = new Pool(sprite, kind);
            }
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

abstract class PooledSprite : Sprite
{
    public abstract SpriteKind Kind { get; }
    public abstract void Return();
}

sealed class SpritePoolV2
{
    private readonly int offset;
    private SpriteManager[] managers;

    public SpritePoolV2(Control owner, params SpriteKind[] kinds)
    {
        offset = (int)kinds[0];
        managers = new SpriteManager[kinds.Length];
        for (int i = 0; i < kinds.Length; i++)
        {
            var kind = kinds[i];
            if ((int)kind != i + offset)
            {
                throw new Exception("TODO cannot currently handle non-consecutive kinds here");
            }
            managers[i] = new SpriteManager(owner, kind);
        }
    }

    private int Index(SpriteKind kind) => (int)kind - offset;

    public PooledSprite Rent(SpriteKind kind)
    {
        return managers[Index(kind)].GetSprite();
    }

    sealed class SpriteManager
    {
        private readonly Control owner;
        private readonly SpriteKind kind;
        private readonly Texture texture;
        private readonly Stack<PooledSprite> pool = new Stack<PooledSprite>();

        public SpriteManager(Control owner, SpriteKind kind)
        {
            this.owner = owner;
            this.kind = kind;
            this.texture = ResourceLoader.Load<Texture>(TexturePath(kind));
        }

        private static string TexturePath(SpriteKind kind)
        {
            switch (kind)
            {
                case SpriteKind.Joined:
                    return "res://Sprites/joined.bmp";
                case SpriteKind.Single:
                    return "res://Sprites/single.bmp";
                case SpriteKind.BlankJoined:
                    return "res://Sprites/blank-joined.bmp";
                case SpriteKind.BlankSingle:
                    return "res://Sprites/blank-single.bmp";
                case SpriteKind.Enemy:
                    return "res://Sprites/enemy.bmp";
                default:
                    throw new Exception("Need texture for " + kind);
            }
        }

        public PooledSprite GetSprite()
        {
            if (pool.Count > 0)
            {
                var pooled = pool.Pop();
                pooled.Visible = true;
                return pooled;
            }

            var sprite = new ManagedSprite();
            sprite.OnCreated(kind, this);
            sprite.Texture = texture;

            switch (kind)
            {
                case SpriteKind.BlankJoined:
                case SpriteKind.BlankSingle:
                case SpriteKind.Joined:
                case SpriteKind.Single:
                    SetShader(sprite, "res://Shaders/catalyst.shader");
                    break;
                case SpriteKind.Enemy:
                    SetShader(sprite, "res://Shaders/enemy.shader");
                    break;
            }

            owner.AddChild(sprite);
            return sprite;
        }

        private static void SetShader(Sprite sprite, string path)
        {
            var shader = ResourceLoader.Load(path).Duplicate(true) as Shader
                ?? throw new Exception("Failed to load shader: " + path);
            var material = new ShaderMaterial();
            material.Shader = shader;
            sprite.Material = material;
        }

        class ManagedSprite : PooledSprite
        {
            private SpriteKind kind;
            private SpriteManager manager = null!;

            public void OnCreated(SpriteKind kind, SpriteManager manager)
            {
                this.kind = kind;
                this.manager = manager;
            }

            public override SpriteKind Kind => kind;

            public override void Return()
            {
                Visible = false;
                manager.pool.Push(this);
            }
        }
    }
}
