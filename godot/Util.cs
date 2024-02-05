using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Godot;

#nullable enable

static class Util
{
    /// <summary>
    /// For use by the UI thread only!!
    /// </summary>
    public static readonly Random seeder = new Random();

    /// <summary>
    /// Similar to the built-in FindNode except that it will not recurse into nested tscn files.
    /// The problem I had was something like this:
    /// * GameViewerControl
    /// * * stuff
    /// * * * more stuff
    /// * * * * GameOverMenu [nested tscn]
    /// * * other stuff
    /// * * * * ButtonQuit
    /// And when I used FindNode("ButtonQuit") I was expecting to get the ButtonQuit that is local
    /// to the GameViewerControl, but instead it found a different ButtonQuit inside the GameOverMenu.
    /// Tricky!
    ///
    /// This method solves this problem by using GetType().Assembly to detect that GameOverMenu
    /// is a custom control and avoids recursing into it.
    /// This means that it will find the local "ButtonQuit" instead.
    /// </summary>
    /// <remarks>
    /// This would be a great place to use CallerArgumentExpression but it's not available on Mono.
    /// </remarks>
    public static void FindNode<T>(this Control parent, out T target, string name) where T : Node
    {
        var result = FindNode<T>(parent, name, depth: 0);
        if (result != null)
        {
            target = result;
        }
        else
        {
            throw new Exception($"Couldn't find node: {name}");
        }
    }

    private static T? FindNode<T>(Node candidate, string name, int depth) where T : Node
    {
        if (candidate.Name == name)
        {
            return (T)candidate;
        }
        // Check that depth > 0, obviously the caller meant to explore beyond just the root node.
        else if (depth > 0 && candidate.GetType().Assembly == typeof(GameViewerControl).Assembly)
        {
            //Console.WriteLine($"Skipping custom control: {candidate.Name} / {candidate.GetType().FullName}");
            return null;
        }

        int count = candidate.GetChildCount();
        for (int i = 0; i < count; i++)
        {
            var child = candidate.GetChild(i);
            var result = FindNode<T>(child, name, depth + 1);
            if (result != null)
            {
                return result;
            }
        }
        return null;
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

    public static T? FindAncestor<T>(this Node? node) where T : class
    {
        while (node != null)
        {
            if (node is T t)
            {
                return t;
            }
            node = node.GetParent();
        }
        return null;
    }
}

static class Empty<T>
{
    public static readonly IReadOnlyList<T> List = new List<T>();
}
