using BCZ.Core;
using BCZ.Core.Viewmodels;
using Godot;
using System;

#nullable enable

public class CountdownViewerControl : Control
{
    private ICountdownViewmodel vm = NullCountdownViewmodel.Instance;
    private readonly CountdownSmoother countdown = new CountdownSmoother(NullCountdownViewmodel.Instance);
    private Members members;

    readonly struct Members
    {
        public readonly Label TimeValue;
        public readonly Label RemainBlueValue;
        public readonly Label RemainRedValue;
        public readonly Label RemainYellowValue;
        public readonly Label ScoreValue;
        public readonly Label ComboRankValue;
        public readonly Label ComboDescriptionValue;
        public readonly Label ComboScoreValue;

        public Members(Control me)
        {
            me.FindNode(out TimeValue, nameof(TimeValue));
            me.FindNode(out RemainBlueValue, nameof(RemainBlueValue));
            me.FindNode(out RemainRedValue, nameof(RemainRedValue));
            me.FindNode(out RemainYellowValue, nameof(RemainYellowValue));
            me.FindNode(out ScoreValue, nameof(ScoreValue));
            me.FindNode(out ComboRankValue, nameof(ComboRankValue));
            me.FindNode(out ComboDescriptionValue, nameof(ComboDescriptionValue));
            me.FindNode(out ComboScoreValue, nameof(ComboScoreValue));
        }
    }

    public override void _Ready()
    {
        members = new Members(this);
    }

    public void SetModel(ICountdownViewmodel? viewmodel)
    {
        this.vm = viewmodel ?? NullCountdownViewmodel.Instance;
        this.countdown.Reset(this.vm);
    }

    public void BeforePreparingGame()
    {
        SetModel(null);
    }

    private const float slowBlinkRate = 0.5f;
    private const float fastBlinkRate = 0.1f;
    float slowBlinker = 0;
    float fastBlinker = 0;
    bool SlowBlinkOn => slowBlinker < slowBlinkRate / 2;
    bool FastBlinkOn => fastBlinker < fastBlinkRate / 2;

    public override void _Process(float delta)
    {
        slowBlinker = (slowBlinker + delta) % slowBlinkRate;
        fastBlinker = (fastBlinker + delta) % fastBlinkRate;

        countdown.Update(delta);
    }

    static readonly Godot.Color darkGreen = Godot.Colors.Black;// green.Darkened(0.7f);
    static readonly Godot.Color orange = Godot.Colors.Orange;// Godot.Color.Color8(244, 185, 58);

    public override void _Draw()
    {
        DrawRect(new Rect2(0, 0, RectSize), Godot.Colors.Black);

        var ts = TimeSpan.FromMilliseconds(vm.RemainingMillis);
        bool draw = true;
        if (ts.TotalSeconds < 2)
        {
            draw = FastBlinkOn;
        }
        else if (ts.TotalSeconds < 5)
        {
            draw = SlowBlinkOn;
        }

        float padding = 2f;// RectSize.x * 0.2f;
        float left = padding;
        float top = padding + 1f;
        float bottom = RectSize.y - padding;
        var size = new Vector2(RectSize.x * 0.14f, bottom - top);
        var timerRect = new Rect2(left, top, size);
        if (draw)
        {
            DrawRect(timerRect, orange);
        }
        if (true)
        {
            var position = countdown.Smoothed;
            float y = top + size.y * (1f - position);
            DrawRect(new Rect2(left, y, size * new Vector2(1, position)), GameColors.Green, filled: true);
            DrawRect(timerRect, darkGreen, filled: false, width: 2);
        }

        QueueViewerControl.DrawBorder(this, new Rect2(0, 0, RectSize));

        members.TimeValue.Text = vm.Time.ToString("mm\\:ss", System.Globalization.CultureInfo.InvariantCulture);
        members.RemainBlueValue.Text = vm.EnemiesRemaining(BCZ.Core.Color.Blue).ToString();
        members.RemainRedValue.Text = vm.EnemiesRemaining(BCZ.Core.Color.Red).ToString();
        members.RemainYellowValue.Text = vm.EnemiesRemaining(BCZ.Core.Color.Yellow).ToString();
        members.ScoreValue.Text = vm.Score.ToString();

        var item = vm.LastCombo;
        if (item.score > 0)
        {
            var combo = item.Item1;
            members.ComboRankValue.Text = $"Rank {combo.Numeral}";
            members.ComboDescriptionValue.Text = combo.Description();
            members.ComboScoreValue.Text = item.score.ToString();
        }
        else
        {
            members.ComboRankValue.Text = "";
            members.ComboDescriptionValue.Text = "";
            members.ComboScoreValue.Text = "";
        }
    }
}
