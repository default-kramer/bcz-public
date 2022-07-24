using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

public class MenuChoiceControl : Control
{
    readonly struct Members
    {
        public readonly Button ButtonLeft;
        public readonly Button ButtonRight;
        public readonly Label LabelValue;
        public readonly Color FocusColor;

        public Members(MenuChoiceControl me)
        {
            var node = me.FindNode("HBoxContainer");
            ButtonLeft = node.GetNode<Button>("ButtonLeft");
            ButtonRight = node.GetNode<Button>("ButtonRight");
            LabelValue = node.GetNode<Label>("LabelValue");

            FocusColor = ButtonLeft.GetColor("font_color_focus");

            ButtonLeft.Connect("pressed", me, nameof(LeftPressed));
            ButtonRight.Connect("pressed", me, nameof(RightPressed));

            me.Connect("focus_entered", me, nameof(GotFocus));
            me.Connect("focus_exited", me, nameof(LostFocus));
        }
    }

    private Members members;

    private IChoiceModel? model;
    public IChoiceModel? Model
    {
        get { return model; }
        set
        {
            model = value;
            members.LabelValue.Text = value?.DisplayValue;
        }
    }

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        members = new Members(this);
    }

    private void StyleFocus(Control control)
    {
        control.AddColorOverride("font_color", members.FocusColor);
    }

    private void StyleUnfocus(Control control)
    {
        // https://godotengine.org/qa/11535/remove-color-override
        // Seems this is the only way to undo AddColorOverride
        control.Set("custom_colors/font_color", null);
    }

    private void GotFocus()
    {
        StyleFocus(members.ButtonLeft);
        StyleFocus(members.LabelValue);
        StyleFocus(members.ButtonRight);
    }

    private void LostFocus()
    {
        StyleUnfocus(members.ButtonLeft);
        StyleUnfocus(members.LabelValue);
        StyleUnfocus(members.ButtonRight);
    }

    private void LeftPressed()
    {
        Cycle(x => x.Previous());
    }

    private void RightPressed()
    {
        Cycle(x => x.Next());
    }

    private void Cycle(Action<IChoiceModel> cycler)
    {
        if (model != null)
        {
            cycler(model);
            members.LabelValue.Text = model.DisplayValue;
            // We need to grab focus after the left/right button gets a mouse click
            this.GrabFocus();
        }
    }

    public override void _Input(InputEvent e)
    {
        if (this.HasFocus())
        {
            if (e.IsActionPressed("game_left") || e.IsActionPressed("ui_left"))
            {
                LeftPressed();
                GetTree().SetInputAsHandled();
            }
            if (e.IsActionPressed("game_right") || e.IsActionPressed("ui_right"))
            {
                RightPressed();
                GetTree().SetInputAsHandled();
            }
        }
    }

    public override Vector2 _GetMinimumSize()
    {
        var displayValues = Model?.AllDisplayValues;
        if (displayValues != null)
        {
            // See which value makes the label the largest and use that as its min size
            var origText = members.LabelValue.Text;
            Vector2 max = members.LabelValue.GetMinimumSize();

            foreach (var text in displayValues)
            {
                members.LabelValue.Text = text;
                var temp = members.LabelValue.GetMinimumSize();
                max = new Vector2(Math.Max(max.x, temp.x), Math.Max(max.y, temp.y));
            }

            //Console.WriteLine($"New max: {max} for label: {origText}");
            members.LabelValue.RectMinSize = max;
            members.LabelValue.Text = origText;
        }

        return base._GetMinimumSize();
    }
}

