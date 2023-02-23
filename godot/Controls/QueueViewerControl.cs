using FF2.Core;
using FF2.Core.Viewmodels;
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

    public override void _Ready()
    {
        spritePool = NewRoot.GetSpritePool(this);
    }

    public static void DrawBorder(CanvasItem me, Rect2 box)
    {
        me.DrawRect(box, Godot.Colors.LightGray, filled: false, width: 1);
        me.DrawRect(new Rect2(box.Position + new Vector2(1, 1), box.Size - new Vector2(2, 2)), Godot.Colors.WhiteSmoke, filled: false, width: 1);
    }

    private void DrawBorder(float bottomY)
    {
        DrawRect(new Rect2(0, 0, RectSize.x, bottomY), Godot.Colors.LightGray, filled: false, width: 1);
        DrawRect(new Rect2(1, 1, RectSize.x - 2, bottomY - 2), Godot.Colors.WhiteSmoke, filled: false, width: 1);
    }

    public override void _Draw()
    {
        if (Model == null)
        {
            return;
        }

        float mid = RectSize.x / 2;
        float cellSize = GridViewer.CurrentCellSize;
        float leftX = mid - cellSize / 2;
        leftX = Math.Max(leftX, 0);

        int itemsToDraw = Math.Min(count, Model.LookaheadLimit);

        // When the queue is empty, it looks silly to draw a very small box.
        // So always size as if there is at least 1 item in the queue.
        var bottomY = GetScreenY(cellSize, Math.Max(1, itemsToDraw)) - cellSize / 2;

        DrawRect(new Rect2(0, 0, RectSize.x, bottomY), Godot.Colors.Black);
        DrawBorder(this, new Rect2(RectPosition, RectSize.x, bottomY));

        for (int i = 0; i < itemsToDraw; i++)
        {
            var item = Model[i];
            Occupant left;
            Occupant right;

            if (item.IsCatalyst(out var pair))
            {
                left = pair.left;
                right = pair.right;
            }
            else
            {
                left = Occupant.IndestructibleEnemy;
                right = Occupant.IndestructibleEnemy;
            }

            float y = GetScreenY(cellSize, i);
            Draw(left, leftX, y, Index1(i));
            Draw(right, leftX + cellSize, y, Index2(i));
        }

        for (int i = Model.LookaheadLimit; i < count; i++)
        {
            HideSprite(Index1(i));
            HideSprite(Index2(i));
        }
    }

    private float GetScreenY(float cellSize, int index)
    {
        const float spacing = 1.5f;
        return cellSize + index * cellSize * spacing;
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

        if (ts.Sprite == null) { return; }

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
