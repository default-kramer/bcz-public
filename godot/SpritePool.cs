using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BCZ.Core;
using Godot;

#nullable enable

abstract class PooledSprite<TKind> : Sprite
{
    public abstract TKind Kind { get; }
    public abstract void Return();
}

abstract class SpritePoolBase<TKind>
{
    private readonly SpriteManager[] managers;

    protected SpritePoolBase(SpriteManager[] managers)
    {
        this.managers = managers;
    }

    // Converts the TKind to an array index
    protected abstract int GetIndex(TKind kind);

    public PooledSprite<TKind> Rent(TKind kind)
    {
        return managers[GetIndex(kind)].GetSprite();
    }

    public void ReturnAll()
    {
        for (int i = 0; i < managers.Length; i++)
        {
            managers[i].ReturnAll();
        }
    }

    protected sealed class SpriteManager
    {
        private readonly Control owner;
        private readonly Texture texture;
        private readonly Material? material;
        private readonly TKind kind;
        private readonly Stack<ManagedSprite> pool = new();
        private readonly List<ManagedSprite> allSprites = new();

        public SpriteManager(Control owner, Texture texture, Material? material, TKind kind)
        {
            this.owner = owner;
            this.texture = texture;// ResourceLoader.Load<Texture>(TexturePath(kind));
            this.material = material;
            this.kind = kind;
        }


        public PooledSprite<TKind> GetSprite()
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
            if (material != null)
            {
                var newMaterial = (Material)material.Duplicate(subresources: true);
                sprite.Material = newMaterial;
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

        class ManagedSprite : PooledSprite<TKind>
        {
            private readonly TKind kind;
            private readonly SpriteManager manager = null!;
            private bool Rented = false;

            public ManagedSprite(TKind kind, SpriteManager manager)
            {
                this.kind = kind;
                this.manager = manager;
            }

            public override TKind Kind => kind;

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

sealed class SpritePoolV2 : SpritePoolBase<SpriteKind>
{
    private readonly int offset;

    private SpritePoolV2(SpriteManager[] managers, int offset) : base(managers)
    {
        this.offset = offset;
    }

    public static SpritePoolV2 Make(Control owner, params SpriteKind[] kinds)
    {
        int offset = (int)kinds[0];
        var managers = new SpriteManager[kinds.Length];
        for (int i = 0; i < kinds.Length; i++)
        {
            var kind = kinds[i];
            if ((int)kind != i + offset)
            {
                throw new Exception("TODO cannot currently handle non-consecutive kinds here");
            }
            var texture = ResourceLoader.Load<Texture>(TexturePath(kind));
            var material = GetMaterial(kind);
            managers[i] = new SpriteManager(owner, texture, material, kind);
        }
        return new SpritePoolV2(managers, offset);
    }

    private int Index(SpriteKind kind) => (int)kind - offset;

    protected override int GetIndex(SpriteKind kind) => Index(kind);

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

    private static Material? GetMaterial(SpriteKind kind)
    {
        switch (kind)
        {
            case SpriteKind.BlankJoined:
            case SpriteKind.BlankSingle:
            case SpriteKind.Joined:
            case SpriteKind.Single:
                return LoadShader("res://Shaders/catalyst.shader");
            case SpriteKind.Enemy:
                return LoadShader("res://Shaders/enemy.shader");
            case SpriteKind.Barrier0:
            case SpriteKind.Barrier1:
            case SpriteKind.Barrier2:
            case SpriteKind.Barrier3:
            case SpriteKind.Barrier4:
            case SpriteKind.Barrier5:
            case SpriteKind.Barrier6:
            case SpriteKind.Barrier7:
                return LoadShader("res://Shaders/barrier.shader");
        }

        return null;
    }

    private static Material LoadShader(string path)
    {
        var shader = ResourceLoader.Load(path) as Shader
            ?? throw new Exception("Failed to load shader: " + path);
        var material = new ShaderMaterial();
        material.Shader = shader;
        return material;
    }
}
