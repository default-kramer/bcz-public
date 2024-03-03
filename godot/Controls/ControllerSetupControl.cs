using Godot;
using System;
using System.Linq;

#nullable enable

public class ControllerSetupControl : Control
{
    const int RepeatCount = 3;
    private readonly InputEvent[] capturedEvents = new InputEvent[RepeatCount];
    private int capturedEventIndex = 0;
    private int PromptIndex = 0;

    readonly struct Members
    {
        public readonly Control PromptLabelContainer;
        public readonly TextureRect ControllerTextureRect;
        public readonly VBoxContainer ContainerDebug;

        public Members(Control me)
        {
            me.FindNode(out PromptLabelContainer, nameof(PromptLabelContainer));
            me.FindNode(out ControllerTextureRect, nameof(ControllerTextureRect));
            me.FindNode(out ContainerDebug, nameof(ContainerDebug));
        }
    }

    private Members members;

    public override void _Ready()
    {
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
            DebugLog(button);
            Capture(e);
        }
        else if (e is InputEventKey key && key.Pressed && !key.Echo)
        {
            const uint unknown = (uint)KeyList.Unknown;
            if (key.Scancode == unknown && key.PhysicalScancode == unknown)
            {
                // On my Xbox+Edge, every controller button press is accompanied by this event.
                // So ignoring it makes sense for this platform at least, and I haven't seen it anywhere else.
                AddToDebugWindow($"Ignoring 'unknown' key {unknown}");
            }
            else
            {
                DebugLog(key);
                Capture(e);
            }
        }
    }

    private Label GetDebugLabel(int index)
    {
        var node = members.ContainerDebug.GetChild(index);
        return (Label)node;
    }

    private void AddToDebugWindow(string str)
    {
        int debugCount = members.ContainerDebug.GetChildCount();

        for (int i = 0; i < debugCount; i++)
        {
            var label = GetDebugLabel(i);
            if (string.IsNullOrEmpty(label.Text))
            {
                label.Text = str;
                return;
            }
        }

        // Slide everything back one slot...
        for (int i = 0; i < debugCount - 1; i++)
        {
            var current = GetDebugLabel(i);
            var next = GetDebugLabel(i + 1);
            current.Text = next.Text;
        }
        // ... and add new entry to the last slot
        GetDebugLabel(debugCount - 1).Text = str;
    }

    private void DebugLog(InputEventJoypadButton button)
    {
        AddToDebugWindow($"Joypad{button.Device}: {button.ButtonIndex} / {button.Pressure}");
    }

    private void DebugLog(InputEventKey key)
    {
        AddToDebugWindow($"Key{key.Device}: {key.Scancode} / {key.PhysicalScancode} / {key.Command} / {key.Meta}");
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

            if (i == PromptIndex)
            {
                members.ControllerTextureRect.Texture = prompt.GetTexture();
            }
        }

        if (PromptIndex >= Prompts.Length)
        {
            UpdateMapping();
            // TODO should allow them to [Retry, Save Changes, Cancel Changes]
            NewRoot.FindRoot(this).BackToMainMenu();
        }
    }

    private void UpdateMapping()
    {
        EraseMapping();

        for (int i = 0; i < Prompts.Length; i++)
        {
            var prompt = Prompts[i];
            var ev = Mapper[i];

            foreach (var action in prompt.Actions)
            {
                InputMap.ActionAddEvent(action, ev);
            }
        }
    }

    private void EraseMapping()
    {
        foreach (var action in Actions)
        {
            foreach (var oldEv in InputMap.GetActionList(action).Cast<InputEvent>())
            {
                InputMap.ActionEraseEvent(action, oldEv);
            }
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

    sealed class Prompt
    {
        public readonly string Text;
        private readonly string imageName;
        public readonly string[] Actions;

        public Prompt(string text, string image, int ignored, params string[] actions)
        {
            this.Text = text;
            this.imageName = image;
            this.Actions = actions;
        }

        private Texture? texture = null;
        public Texture GetTexture()
        {
            // Godot suggested I import these resources as Images, but I don't know how to do that right now.
            // It seems that they imported as StreamTextures, which works for me.
            texture = texture ?? ResourceLoader.Load<Texture>($"res://Sprites/controller{imageName}.bmp");
            return texture;
        }
    }

    private static readonly Prompt[] Prompts = new[]
    {
        new Prompt("Up", "-up", 0, "ui_up"),
        new Prompt("Down", "-down", 0, "ui_down"),
        new Prompt("Left", "-left", 0, "ui_left", "game_left"),
        new Prompt("Right", "-right", 0, "ui_right", "game_right"),

        // Controller filenames use PS naming convention: cross, circle, square, triangle
        new Prompt("Drop", "-triangle", 0, "game_drop"),
        new Prompt("Rotate Clockwise", "-cross", 0, "game_rotate_cw"),
        new Prompt("Rotate Anticlockwise", "-circle", 0, "game_rotate_ccw"),

        new Prompt("Confirm", "-cross", 0, "ui_accept"),
        new Prompt("Cancel", "-circle", 0, "ui_cancel"),
        new Prompt("Pause", "-start", 0, "game_pause"),
    };

    private static readonly string[] Actions = Prompts.SelectMany(x => x.Actions).ToArray();

    private readonly InputEvent[] Mapper = new InputEvent[Prompts.Length];
}
