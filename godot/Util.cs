using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Godot;

#nullable enable

static class Util
{
    // This would be a great place to use CallerArgumentExpression but it's not available on Mono
    public static void FindNode<T>(this Control parent, out T target, string name)
    {
        object node = parent.FindNode(name)
            ?? throw new ArgumentException("Couldn't find: " + name);
        target = (T)node;
    }

    public static void ScaleAndCenter(this Sprite sprite, Rect2 box)
    {
        var texW = sprite.Texture.GetWidth();
        var texH = sprite.Texture.GetHeight();
        var wScale = box.Size.x / texW;
        var hScale = box.Size.y / texH;
        var scale = Math.Min(wScale, hScale);
        sprite.Scale = new Vector2(scale, scale);

        // position determines the center of the sprite, so we have to add half of the scaled W and H
        var spriteW = texW * scale;
        var spriteH = texH * scale;
        var extraW = box.Size.x - spriteW;
        var extraH = box.Size.y - spriteH;
        sprite.Position = box.Position + new Vector2(spriteW / 2 + extraW / 2, spriteH / 2 + extraH / 2);
    }

    /// <summary>
    /// For experimental stuff that I don't want the public to see (at least not by default)
    /// </summary>
    public static bool IsSuperuser = !string.IsNullOrWhiteSpace(System.Environment.GetEnvironmentVariable("BCZ_SUPERUSER"));
}

static class Empty<T>
{
    public static readonly IReadOnlyList<T> List = new List<T>();
}
