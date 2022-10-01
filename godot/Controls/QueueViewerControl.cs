using FF2.Core;
using FF2.Core.Viewmodels;
using FF2.Godot;
using Godot;
using System;

#nullable enable

public class QueueViewerControl : Control
{
    public QueueModel Model { get; set; } = null!;
    public GridViewerControl GridViewer { get; set; } = null!;
    private SpritePool spritePool = null!;

    const int count = 5;
    private readonly TrackedSprite[] sprites = new TrackedSprite[count * 2];
    private static int Index1(int i) { return i; }
    private static int Index2(int i) { return i + count; }

    public override void _Draw()
    {
        if (Model == null)
        {
            return;
        }

        spritePool = spritePool ?? NewRoot.GetSpritePool(this);

        DrawRect(new Rect2(0, 0, this.RectSize), new Godot.Color(0.03f, 0.03f, 0.03f));

        float cellSize = GridViewer.CurrentCellSize;
        float marginLeft = (RectSize.x - cellSize - cellSize) / 2f;
        marginLeft = Math.Max(marginLeft, 0);

        for (int i = 0; i < Math.Min(count, Model.LookaheadLimit); i++)
        {
            var (left, right) = Model[i];
            float y = 40f * i + 100f;
            Draw(left, marginLeft, y, Index1(i));
            Draw(right, marginLeft + cellSize, y, Index2(i));
        }

        for (int i = Model.LookaheadLimit; i < count; i++)
        {
            HideSprite(Index1(i));
            HideSprite(Index2(i));
        }
    }

    private void HideSprite(int index)
    {
        var sprite = sprites[index].Sprite;
        if (sprite != null)
        {
            sprite.Visible = false;
        }
    }

    private void Draw(Occupant occ, float x, float y, int index)
    {
        var kind = GridViewerControl.GetSpriteKind(occ);

        var ts = sprites[index];

        if (ts.Kind != kind)
        {
            if (ts.IsSomething)
            {
                spritePool.Return(ts);
            }
            ts = spritePool.Rent(kind, this);
            sprites[index] = ts;
        }

        var sprite = ts.Sprite;
        sprite.Visible = true;
        sprite.Position = new Vector2(x, y);
        sprite.Scale = GridViewer.CurrentSpriteScale;

        // TODO some duplicate code here and GridViewer
        if (kind == SpriteKind.Joined || kind == SpriteKind.BlankJoined)
        {
            sprite.RotationDegrees = occ.Direction switch
            {
                Direction.Down => 0,
                Direction.Left => 90,
                Direction.Up => 180,
                Direction.Right => 270,
                _ => throw new Exception($"assert failed: {occ.Direction}"),
            };
        }

        var shader = (ShaderMaterial)sprite.Material;
        shader.SetShaderParam("my_color", GameColors.ToVector(occ.Color));
        shader.SetShaderParam("my_alpha", 1f);
        shader.SetShaderParam("destructionProgress", 0f);
    }
}
