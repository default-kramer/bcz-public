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

    public override void _Draw()
    {
        spritePool = spritePool ?? NewRoot.GetSpritePool(this);

        DrawRect(new Rect2(0, 0, this.RectSize), new Godot.Color(0.03f, 0.03f, 0.03f));

        float cellSize = GridViewer.CurrentCellSize;
        float marginLeft = (RectSize.x - cellSize - cellSize) / 2f;
        marginLeft = Math.Max(marginLeft, 0);

        for (int i = 0; i < count; i++)
        {
            var (left, right) = Model[i];
            float y = 40f * i + 100f;
            Draw(left, marginLeft, y, i);
            Draw(right, marginLeft + cellSize, y, i + count);
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
