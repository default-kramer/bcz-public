using FF2.Core;
using FF2.Godot;
using Godot;
using System;

#nullable enable

public class NewRoot : Control
{
    readonly struct Members
    {
        public readonly SpritePool SpritePool;
        public readonly GameViewerControl GameViewer;
        public readonly MainMenu MainMenu;

        public Members(Control me)
        {
            SpritePool = new SpritePool(me, SpriteKind.Single, SpriteKind.Joined,
                SpriteKind.Enemy, SpriteKind.BlankJoined, SpriteKind.BlankSingle);

            me.FindNode(out GameViewer, nameof(GameViewer));
            me.FindNode(out MainMenu, nameof(MainMenu));
        }
    }

    private Members members;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        this.members = new Members(this);
        BackToMainMenu();
    }

    internal static SpritePool GetSpritePool(Node child)
    {
        return FindRoot(child).members.SpritePool;
    }

    internal static NewRoot FindRoot(Node child)
    {
        if (child == null)
        {
            throw new Exception("Failed to find root node");
        }
        if (child is NewRoot me)
        {
            return me;
        }
        return FindRoot(child.GetParent());
    }

    public void StartGame(SeededSettings ss)
    {
        members.MainMenu.Visible = false;
        members.GameViewer.Visible = true;
        members.GameViewer.StartGame(ss);
    }

    public void BackToMainMenu()
    {
        members.GameViewer.Visible = false;
        members.MainMenu.Visible = true;
        members.MainMenu.ShowMainMenu();
    }
}
