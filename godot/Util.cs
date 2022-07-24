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
}
