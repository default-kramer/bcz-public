using FF2.Core;
using FF2.Core.Viewmodels;
using Godot;
using System;

/// <summary>
/// Assumes that the TankBackground and Liquid sprites are the same size and should have the same
/// transforms when the tank is 100% full.
/// </summary>
public class PenaltyViewerControl : Control
{
    private PenaltyModel _model;
    public PenaltyModel Model
    {
        get { return _model; }
        set
        {
            corruptionSample = null;
            _model = value;
        }
    }

    const int cylinderCount = 8;

    private static Font font;
    private Members members;

    readonly struct Members
    {
        public readonly Sprite TankBackground;
        public readonly Sprite Liquid;
        public readonly Sprite TankBorder;
        public readonly Sprite Engine;
        public readonly ColorRect[] Cylinders;
        public readonly PenaltyRect[] Penalties;
        public readonly ShaderMaterial LiquidShader;

        public Members(Control me)
        {
            me.FindNode(out TankBackground, nameof(TankBackground));
            me.FindNode(out Liquid, nameof(Liquid));
            me.FindNode(out TankBorder, nameof(TankBorder));
            me.FindNode(out Engine, nameof(Engine));

            ColorRect cylinderTemplate;
            me.FindNode(out cylinderTemplate, "Cylinder");
            Cylinders = new ColorRect[cylinderCount];
            CloneCylinders(cylinderTemplate, Cylinders);

            Penalties = new PenaltyRect[cylinderCount];
            for (int i = 0; i < cylinderCount; i++)
            {
                var pr = new PenaltyRect();
                me.AddChild(pr);
                pr.Visible = false;
                Penalties[i] = pr;
                VisualServer.CanvasItemSetZIndex(pr.GetCanvasItem(), zIndex: 100);
            }

            LiquidShader = (ShaderMaterial)Liquid.Material;
        }

        private static void CloneCylinders(ColorRect template, ColorRect[] array)
        {
            array[0] = template;
            var owner = template.Owner;
            for (int i = 1; i < array.Length; i++)
            {
                var clone = (ColorRect)template.Duplicate();
                // Until Godot 4.0 gives us per-instance uniforms, we need to duplicate the shader also.
                // https://godotengine.org/article/godot-40-gets-global-and-instance-shader-uniforms
                clone.Material = (ShaderMaterial)template.Material.Duplicate();
                template.Owner.AddChild(clone);
                clone.Owner = template.Owner;
                array[i] = clone;
            }
        }
    }

    public override void _Ready()
    {
        if (font == null)
        {
            var label = new Label();
            font = label.GetFont("");
            label.Free();
        }
        members = new Members(this);
    }

    private PenaltyModel.CorruptionSample? corruptionSample = null;

    /// <summary>
    /// Scale and position the sprite so that it is centered horizontally in the given area.
    /// Vertical position is either the top or bottom of the area.
    /// </summary>
    private static void HCenter(Rect2 area, Sprite sprite, bool top)
    {
        var textureSize = sprite.Texture.GetSize();
        var scale = Math.Min(area.Size.x / textureSize.x, area.Size.y / textureSize.y);
        sprite.Scale = new Vector2(scale, scale);
        var scaledSize = textureSize * scale;

        float xOffset = (scaledSize.x + area.Size.x - scaledSize.x) / 2;
        float yOffset = scaledSize.y / 2; // assume top
        if (!top)
        {
            yOffset = area.Size.y - yOffset;
        }
        sprite.Position = area.Position + new Vector2(xOffset, yOffset);
    }

    private float AdjustedTime = 0;
    public override void _Process(float delta)
    {
        if (Model?.Slowmo ?? false)
        {
            delta /= 10f;
        }
        AdjustedTime += delta;
    }

    public override void _Draw()
    {
        if (Model == null)
        {
            return;
        }

        DrawRect(new Rect2(0, 0, RectSize), Colors.BlanchedAlmond);

        var padding = RectSize.x * 0.1f;
        var size = new Vector2(RectSize.x - padding * 2, RectSize.y);

        var tankArea = new Rect2(padding, 0, size.x, size.y * 2 / 3);
        var engineArea = new Rect2(padding, tankArea.Size.y, size.x, size.y - tankArea.Size.y);

        HCenter(tankArea, members.TankBackground, false);
        HCenter(tankArea, members.Liquid, false);
        HCenter(tankArea, members.TankBorder, false);
        HCenter(engineArea, members.Engine, true);

        var sample = Model.SampleCorruption(corruptionSample);
        corruptionSample = sample;
        members.Liquid.Transform = members.TankBackground.Transform.Translated(new Vector2(0, sample.CorruptionProgress * 500));
        members.LiquidShader.SetShaderParam("crop", 1f - sample.CorruptionProgress - 0.02f);

        PlaceCylinders(engineArea);
    }

    const float cylinderPeriod = 0.8f; // seconds for all 8 cylinders to fire

    private void PlaceCylinders(Rect2 engineArea)
    {
        var engineSize = engineArea.Size;// members.Engine.Texture.GetSize() * members.Engine.Scale;

        // 5B = 1C ::: Border width is 1/5 cylinder width (currently 16px and 80px)
        // T = 3B + 2C ::: Total width is 3 borders + 2 cylinders
        // If we set B=1 we get C=5 and T=13, meaning that:
        // * Each border is 1/13 of the total width
        // * Each cylinder is 5/13 of the total width
        var borderWidth = engineSize.x * 1 / 13;
        var cylinderWidth = engineSize.x * 5 / 13;

        for (int i = 0; i < members.Cylinders.Length; i++)
        {
            int order = firingOrder[i];
            int row = i / 2;
            int column = i % 2;
            float xOffset = borderWidth + (cylinderWidth + borderWidth) * column;
            float yOffset = borderWidth + (cylinderWidth + borderWidth) * row;
            //xOffset += cylinderWidth / 2;
            //yOffset += cylinderWidth / 2;
            var position = engineArea.Position + new Vector2(xOffset, yOffset);
            var size = new Vector2(cylinderWidth, cylinderWidth);

            var cylinder = members.Cylinders[i];
            var penaltyRect = members.Penalties[i];
            cylinder.RectSize = size;
            cylinder.RectPosition = position;
            penaltyRect.RectSize = size;
            penaltyRect.RectPosition = position;

            if (order < Model.Count)
            {
                cylinder.Visible = false;
                penaltyRect.Visible = true;
                penaltyRect.Message = Model[order].Level.ToString();
            }
            else
            {
                cylinder.Visible = true;
                penaltyRect.Visible = false;
                var shader = (ShaderMaterial)cylinder.Material;
                float offset = order / Convert.ToSingle(members.Cylinders.Length);
                shader.SetShaderParam("sp_adjustedTime", AdjustedTime + offset * cylinderPeriod);
                shader.SetShaderParam("sp_period", cylinderPeriod);
            }
        }
    }

    private static readonly int[] firingOrder = BuildFiringOrder(0, 7, 2, 5, 6, 1, 4, 3);
    private static int[] BuildFiringOrder(params int[] cylinders)
    {
        int[] order = new int[cylinders.Length];
        for (int i = 0; i < cylinders.Length; i++)
        {
            order[cylinders[i]] = i;
        }
        return order;
    }

    /// <summary>
    /// When a penalty is active, show this instead of a firing cylinder.
    /// </summary>
    class PenaltyRect : ColorRect
    {
        public string Message = null;

        public override void _Draw()
        {
            var message = this.Message;
            if (message != null)
            {
                DrawRect(new Rect2(0, 0, RectSize), Colors.Red);
                DrawString(font, new Vector2(4f, 20f), message);
            }
        }
    }
}
