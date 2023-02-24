using FF2.Core.Viewmodels;
using Godot;
using System;

#nullable enable

public class SwitchViewerControl : Control
{
    private ISwitchesViewmodel model = NullModel.Instance;
    const int numeralCount = 16;
    private Sprite[] numerals = null!;
    private float numeralWidth = 1;
    private float numeralHeight = 1;

    public GridViewerControl? AttackViewer { get; set; }

    public override void _Ready()
    {
        numerals = new Sprite[numeralCount];
        for (int i = 1; i <= numeralCount; i++)
        {
            var obj = ResourceLoader.Load($"res://Sprites/numerals/{i}.bmp");
            var tex = (Texture)obj;

            numeralWidth = Math.Max(tex.GetWidth(), numeralWidth);
            numeralHeight = Math.Max(tex.GetHeight(), numeralHeight);

            var sprite = new Sprite();
            sprite.Texture = tex;
            numerals[i - 1] = sprite;
            sprite.Visible = false;
            AddChild(sprite);
        }
    }

    public void SetModel(ISwitchesViewmodel model)
    {
        this.model = model;
    }

    public override void _Draw()
    {
        if (numerals == null) { return; }

        int minRank = model.MinRank;
        int maxRank = model.MaxRank;
        int numSquares = 1 + maxRank - minRank;

        float attackGridHeight = ((maxRank - 1) * AttackViewer?.CurrentCellSize) ?? 999f;
        var size = new Vector2(RectSize.x, Math.Min(RectSize.y, attackGridHeight));

        QueueViewerControl.DrawBorder(this, new Rect2(0, 0, size));

        const float yPadRatio = 0.1f;
        float foo = size.y / (numSquares + yPadRatio);

        float yPadding = foo * yPadRatio;
        float boxHeight = foo * (1 - yPadRatio);
        boxHeight = Math.Min(boxHeight, AttackViewer?.CurrentCellSize ?? 999f);
        var boxSize = new Vector2(size.x - 3, boxHeight);

        for (int rank = minRank; rank <= maxRank; rank++)
        {
            int index = maxRank - rank;// rank - minRank;
            float y = yPadding + foo * index;
            var upperLeft = new Vector2(1.5f, y);
            bool green = model.IsGreen(rank);
            var color = green ? Godot.Colors.Green : Godot.Colors.Orange;
            DrawRect(new Rect2(upperLeft, boxSize), color);


            var sprite = numerals[rank - 1];
            sprite.Position = upperLeft + boxSize / 2;
            sprite.Visible = true;
            float scale = Math.Min(boxSize.x / numeralWidth, boxSize.y / numeralHeight);
            sprite.Scale = new Vector2(scale, scale);
        }
    }

    class NullModel : ISwitchesViewmodel
    {
        private NullModel() { }
        public static readonly NullModel Instance = new NullModel();

        public int MinRank => 2;
        public int MaxRank => 12;

        public bool IsGreen(int rank)
        {
            return true;
        }
    }
}
