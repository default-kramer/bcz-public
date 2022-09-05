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

    private Font font;
    private Members members;

    readonly struct Members
    {
        public readonly Sprite TankBackground;
        public readonly Sprite Liquid;
        public readonly Sprite TankBorder;
        public readonly Sprite Engine;
        public readonly ColorRect[] Cylinders;

        public readonly ShaderMaterial LiquidShader;

        public Members(Control me)
        {
            me.FindNode(out TankBackground, nameof(TankBackground));
            me.FindNode(out Liquid, nameof(Liquid));
            me.FindNode(out TankBorder, nameof(TankBorder));
            me.FindNode(out Engine, nameof(Engine));

            ColorRect cylinderTemplate;
            me.FindNode(out cylinderTemplate, "Cylinder");
            Cylinders = new ColorRect[8];
            CloneCylinders(cylinderTemplate, Cylinders);

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
        var label = new Label();
        font = label.GetFont("");
        label.Free();
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
        // TODO here is where we could slow down time while the user has no control
        AdjustedTime += delta;
    }

    public override void _Draw()
    {
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
        /*

        float penaltyMargin = this.RectSize.x * 0.1f;
        float penaltyW = this.RectSize.x - penaltyMargin - penaltyMargin;
        float penaltyH = penaltyW;
        float x = penaltyMargin;
        var penaltySize = new Vector2(penaltyW, penaltyH);
        float yBase = this.RectSize.y - penaltyH;

        for (int i = 0; i < Model.Count; i++)
        {
            float y = penaltyMargin * (i + 1) + penaltyH * i;
            y = yBase - y;
            DrawRect(new Rect2(x, y, penaltySize), GameColors.Corrupt);
            DrawString(font, new Vector2(x + 4f, y + 20f), Model[i].Level.ToString());
        }
        */
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
            var cylinder = members.Cylinders[i];
            cylinder.RectSize = new Vector2(cylinderWidth, cylinderWidth);

            int row = i / 2;
            int column = i % 2;
            float xOffset = borderWidth + (cylinderWidth + borderWidth) * column;
            float yOffset = borderWidth + (cylinderWidth + borderWidth) * row;
            //xOffset += cylinderWidth / 2;
            //yOffset += cylinderWidth / 2;
            cylinder.RectPosition = engineArea.Position + new Vector2(xOffset, yOffset);

            var shader = (ShaderMaterial)cylinder.Material;
            float offset = firingOrder[i] / Convert.ToSingle(members.Cylinders.Length);
            shader.SetShaderParam("sp_adjustedTime", AdjustedTime + offset * cylinderPeriod);
            shader.SetShaderParam("sp_period", cylinderPeriod);
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
}
