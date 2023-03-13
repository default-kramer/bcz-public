using Godot;
using System;

public class BarrierTogglesControl : Control
{
    private Sprite theSprite = null!;

    public override void _Ready()
    {
        var texture = ResourceLoader.Load<Texture>("res://Sprites/numerals/toggle-off-6.bmp");
        theSprite = new Sprite();
        theSprite.Texture = texture;
        AddChild(theSprite);
    }

    private static Color b1 = Godot.Color.Color8(45, 55, 72);

    public override void _Draw()
    {
        DrawRect(new Rect2(0, 0, RectSize), b1);
        QueueViewerControl.DrawBorder(this, new Rect2(0, 0, RectSize));
        //DrawRect(new Rect2(0, 0, RectSize), Colors.AliceBlue);

        theSprite.Position = new Vector2(50, 50);
        theSprite.Visible = true;
        theSprite.Scale = new Vector2(0.6f, 0.6f);
    }
}
