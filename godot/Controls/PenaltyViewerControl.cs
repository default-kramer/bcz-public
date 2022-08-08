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
        public readonly ShaderMaterial LiquidShader;

        public Members(Control me)
        {
            me.FindNode(out TankBackground, nameof(TankBackground));
            me.FindNode(out Liquid, nameof(Liquid));
            LiquidShader = (ShaderMaterial)Liquid.Material;
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

    public override void _Draw()
    {
        var sample = Model.SampleCorruption(corruptionSample);
        corruptionSample = sample;
        members.Liquid.Transform = members.TankBackground.Transform.Translated(new Vector2(0, sample.CorruptionProgress * 500));
        members.LiquidShader.SetShaderParam("crop", 1f - sample.CorruptionProgress - 0.02f);

        DrawRect(new Rect2(0, 0, RectSize), Colors.DarkGray);

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
    }
}
