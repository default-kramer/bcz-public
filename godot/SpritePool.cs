using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FF2.Core;
using Godot;

#nullable enable

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

    public void ReturnAll()
    {
        for (int i = 0; i < managers.Length; i++)
        {
            managers[i].ReturnAll();
        }
    }

    sealed class SpriteManager
    {
        private readonly Control owner;
        private readonly SpriteKind kind;
        private readonly Texture texture;
        private readonly Stack<ManagedSprite> pool = new Stack<ManagedSprite>();
        private readonly List<ManagedSprite> allSprites = new List<ManagedSprite>();

        public SpriteManager(Control owner, SpriteKind kind)
        {
            this.owner = owner;
            this.kind = kind;
            this.texture = ResourceLoader.Load<Texture>(TexturePath(kind));
        }

        private static string TexturePath(SpriteKind kind)
        {
            if (kind >= SpriteKind.Num1 && kind <= SpriteKind.Num16)
            {
                int i = 1 + (int)kind - (int)SpriteKind.Num1;
                return $"res://Sprites/numerals/{i}.bmp";
            }

            if (kind >= SpriteKind.Barrier0 && kind <= SpriteKind.Barrier7)
            {
                int i = (int)kind - (int)SpriteKind.Barrier0;
                return $"res://Sprites/barriers/barrier-{i}.bmp";
            }

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
                case SpriteKind.Heart:
                    return "res://Sprites/heart.bmp";
                case SpriteKind.Heart0:
                    return "res://Sprites/heart0.bmp";
                default:
                    throw new Exception("Need texture for " + kind);
            }
        }

        public PooledSprite GetSprite()
        {
            if (pool.Count > 0)
            {
                return pool.Pop().Rent();
            }

            var sprite = new ManagedSprite(kind, this);
            allSprites.Add(sprite);
            if (allSprites.Count > 200)
            {
                throw new Exception("Sprite Leak??");
            }

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
                case SpriteKind.Barrier0:
                case SpriteKind.Barrier1:
                case SpriteKind.Barrier2:
                case SpriteKind.Barrier3:
                case SpriteKind.Barrier4:
                case SpriteKind.Barrier5:
                case SpriteKind.Barrier6:
                case SpriteKind.Barrier7:
                    SetShader(sprite, "res://Shaders/barrier.shader");
                    break;
            }

            owner.AddChild(sprite);

            return sprite.Rent();
        }

        public void ReturnAll()
        {
            foreach (var item in allSprites)
            {
                item.Return();
            }
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
            private readonly SpriteKind kind;
            private readonly SpriteManager manager = null!;
            private bool Rented = false;

            public ManagedSprite(SpriteKind kind, SpriteManager manager)
            {
                this.kind = kind;
                this.manager = manager;
            }

            public override SpriteKind Kind => kind;

            public override void Return()
            {
                if (Rented)
                {
                    Visible = false;
                    Rented = false;
                    manager.pool.Push(this);
                }
            }

            public ManagedSprite Rent()
            {
                Visible = true;
                Rented = true;
                return this;
            }
        }
    }
}
