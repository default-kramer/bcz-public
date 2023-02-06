using Godot;
using System;

#nullable enable

public class ControllerSetupControl : Control
{
    const int RepeatCount = 3;
    private readonly InputEvent[] capturedEvents = new InputEvent[RepeatCount];
    private int capturedEventIndex = 0;
    private int PromptIndex = 0;
    private Font font = null!;

    readonly struct Members
    {
        public readonly Control PromptLabelContainer;
        public readonly GridViewerControl GridViewerControl;

        public Members(Control me)
        {
            me.FindNode(out PromptLabelContainer, nameof(PromptLabelContainer));
            me.FindNode(out GridViewerControl, nameof(GridViewerControl));
        }
    }

    private Members members;

    public override void _Ready()
    {
        font = GetFont("");
        members = new Members(this);
        Reset();
    }

    public void Reset()
    {
        capturedEventIndex = 0;
        PromptIndex = 0;
        Refresh();
    }

    public override void _Input(InputEvent e)
    {
        if (PromptIndex >= Prompts.Length)
        {
            return;
        }

        if (e is InputEventJoypadButton button && button.Pressed)
        {
            //Console.WriteLine($"Button: {button.Device}/{button.ButtonIndex}, Pressure: {button.Pressure}");
            Capture(e);
        }
        else if (e is InputEventKey key && key.Pressed && !key.Echo)
        {
            //Console.WriteLine($"Key: {key.Device}/{key.Scancode}");
            Capture(e);
        }
    }

    private void Capture(InputEvent e)
    {
        capturedEvents[capturedEventIndex] = e;
        capturedEventIndex++;
        if (!AllMatch())
        {
            capturedEventIndex = 0;
        }
        else if (capturedEventIndex == RepeatCount)
        {
            Mapper[PromptIndex] = e;
            PromptIndex++;
            capturedEventIndex = 0;
        }

        Refresh();
    }

    private void Refresh()
    {
        for (int i = 0; i < Prompts.Length; i++)
        {
            var prompt = Prompts[i];

            int completedReps = 0;
            if (i == PromptIndex)
            {
                completedReps = capturedEventIndex;
            }
            else if (i < PromptIndex)
            {
                completedReps = RepeatCount;
            }
            string text = $"{prompt.Text} ({completedReps}/{RepeatCount})";

            var label = (Label)(members.PromptLabelContainer.GetChild(i));
            label.AddColorOverride("font_color", i == PromptIndex ? Colors.Green : Colors.White);
            label.Text = text;
        }
    }

    private bool AllMatch()
    {
        for (int i = 0; i < capturedEventIndex - 1; i++)
        {
            if (!Match(capturedEvents[i], capturedEvents[i + 1]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool Match(InputEvent a, InputEvent b)
    {
        if (a is InputEventJoypadButton btnA)
        {
            return b is InputEventJoypadButton btnB
                && btnA.Device == btnB.Device
                && btnA.ButtonIndex == btnB.ButtonIndex;
        }
        else if (a is InputEventKey keyA)
        {
            return b is InputEventKey keyB
                && keyA.Device == keyB.Device
                && keyA.Scancode == keyB.Scancode;
        }

        return false;
    }

    readonly struct Prompt
    {
        public readonly string Text;

        public Prompt(string text)
        {
            this.Text = text;
        }
    }

    private static readonly Prompt[] Prompts = new[]
    {
        new Prompt("Left"),
        new Prompt("Right"),
        new Prompt("Drop"),
        new Prompt("Rotate Clockwise"),
        new Prompt("Rotate Anticlockwise"),
    };

    private readonly InputEvent[] Mapper = new InputEvent[Prompts.Length];
}
