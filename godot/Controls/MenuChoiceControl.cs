using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

// Sizing Notes:
// In WPF, there is Visible/Hidden/Collapsed.
// It doesn't seem like Godot supports Hidden; setting Visible=false implies Collapsed.
// We don't want the size of the control to change when the user selects a longer
// or shorter string, so we add clone Labels for all possible strings and manually
// update the RectMinSize of all of them to match the biggest one.
public class MenuChoiceControl : Control
{
    readonly struct Members
    {
        public readonly Button ButtonLeft;
        public readonly Button ButtonRight;
        public readonly Label LabelValue;
        public readonly HBoxContainer HBoxContainer;
        public readonly Color FocusColor;

        public Members(MenuChoiceControl me)
        {
            me.FindNode(out ButtonLeft, nameof(ButtonLeft));
            me.FindNode(out ButtonRight, nameof(ButtonRight));
            me.FindNode(out LabelValue, nameof(LabelValue));
            me.FindNode(out HBoxContainer, nameof(HBoxContainer));

            FocusColor = ButtonLeft.GetColor("font_color_focus");

            ButtonLeft.Connect("pressed", me, nameof(LeftPressed));
            ButtonRight.Connect("pressed", me, nameof(RightPressed));

            me.Connect("focus_entered", me, nameof(GotFocus));
            me.Connect("focus_exited", me, nameof(LostFocus));
        }
    }

    private Members members;
    private IHelpText helpText = NullHelpText.Instance;

    private IChoiceModel? model;

    /// <summary>
    /// Holds dynamically cloned Labels (and the original Label at position 0).
    /// I'm now realizing we could probably just iterate over the parent's child collection...
    /// but whatever - this code is already written.
    /// </summary>
    private List<Label> labels = new List<Label>();

    public IChoiceModel? Model
    {
        get { return model; }
        set
        {
            model = value;
            UpdateLabelSizes(value?.AllDisplayValues ?? NoChoices);
            UpdateLabelVisibility(model?.SelectedIndex ?? -1);
        }
    }

    private static readonly IReadOnlyList<string> NoChoices = new List<string>();

    private void UpdateLabelSizes(IReadOnlyList<string> choices)
    {
        Vector2 max = default;
        for (int i = 0; i < choices.Count; i++)
        {
            if (labels.Count == i)
            {
                object obj = members.LabelValue.Duplicate();
                var clone = (Label)obj;
                labels.Add(clone);
                members.LabelValue.GetParent().AddChild(clone);
            }

            var label = labels[i];
            label.Text = choices[i];
            var temp = label.GetMinimumSize();
            max = new Vector2(Math.Max(temp.x, max.x), Math.Max(temp.y, max.y));
        }

        for (int i = 0; i < labels.Count; i++)
        {
            labels[i].RectMinSize = max;
        }
    }

    private void UpdateLabelVisibility(int selectedIndex)
    {
        int count = members.LabelValue.GetParent().GetChildCount();
        for (int i = 0; i < labels.Count; i++)
        {
            labels[i].Visible = i == selectedIndex;
        }
    }

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        members = new Members(this);
        labels.Add(members.LabelValue);
        helpText = this.FindAncestor<IHelpText>() ?? helpText;
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

    private void SetHelpText()
    {
        helpText.SetText(Model?.HelpText);
    }

    private void GotFocus()
    {
        StyleFocus(members.ButtonLeft);
        StyleFocus(members.ButtonRight);
        for (int i = 0; i < labels.Count; i++)
        {
            StyleFocus(labels[i]);
        }

        SetHelpText();
    }

    private void LostFocus()
    {
        StyleUnfocus(members.ButtonLeft);
        StyleUnfocus(members.ButtonRight);
        for (int i = 0; i < labels.Count; i++)
        {
            StyleUnfocus(labels[i]);
        }
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
            UpdateLabelVisibility(model.SelectedIndex);
            // We need to grab focus after the left/right button gets a mouse click
            this.GrabFocus();
            SetHelpText();
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

    /// <summary>
    /// It seems this is not actually necessary for normal operation, but it is necessary if
    /// you want to do some ColorRect debugging.
    /// </summary>
    public override Vector2 _GetMinimumSize()
    {
        var result = members.HBoxContainer?.GetCombinedMinimumSize();
        return result.GetValueOrDefault();
    }
}
