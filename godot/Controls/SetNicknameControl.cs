using Godot;
using System;

public class SetNicknameControl : CenterContainer
{
    readonly struct Members
    {
        public readonly CheckBox CheckBoxAnon;
        public readonly CheckBox CheckBoxNickname;
        public readonly CheckBox CheckBoxOffline;
        public readonly LineEdit LineEditNickname;
        public readonly Button ButtonConfirm;

        public Members(Control me)
        {
            me.FindNode(out CheckBoxAnon, nameof(CheckBoxAnon));
            me.FindNode(out CheckBoxNickname, nameof(CheckBoxNickname));
            me.FindNode(out CheckBoxOffline, nameof(CheckBoxOffline));
            me.FindNode(out LineEditNickname, nameof(LineEditNickname));
            me.FindNode(out ButtonConfirm, nameof(ButtonConfirm));
        }
    }

    private Members members;

    public override void _Ready()
    {
        members = new Members(this);
        members.LineEditNickname.MaxLength = 15;

        members.ButtonConfirm.Connect("pressed", this, nameof(Confirm));
        members.CheckBoxAnon.Connect("pressed", this, nameof(ChooseAnon));
        members.CheckBoxNickname.Connect("pressed", this, nameof(ChooseNickname));
        members.CheckBoxOffline.Connect("pressed", this, nameof(ChooseOffline));

        var data = SaveData.GetOnlineConfigData();
        if (data.Mode == SaveData.OnlineMode.NoSelection)
        {
            // First time, set up default values
            data.Mode = SaveData.OnlineMode.Anonymous;
            data.Nickname = "<player name>";
        }

        members.CheckBoxAnon.Pressed = data.Mode == SaveData.OnlineMode.Anonymous;
        members.CheckBoxNickname.Pressed = data.Mode == SaveData.OnlineMode.Online;
        members.CheckBoxOffline.Pressed = data.Mode == SaveData.OnlineMode.Offline;
        members.LineEditNickname.Text = data.Nickname;

        CallDeferred(nameof(SetupFocus));
    }

    private void SetupFocus()
    {
        if (members.CheckBoxAnon.Pressed)
        {
            members.CheckBoxAnon.GrabFocus();
        }
        else if (members.CheckBoxNickname.Pressed)
        {
            members.CheckBoxNickname.GrabFocus();
        }
        else if (members.CheckBoxOffline.Pressed)
        {
            members.CheckBoxOffline.GrabFocus();
        }
        else
        {
            GD.PushWarning("Expected one checked: Anon/Nickname/Offline");
            members.CheckBoxAnon.GrabFocus();
        }
    }

    private void Confirm()
    {
        var data = SaveData.GetOnlineConfigData();
        if (members.CheckBoxOffline.Pressed)
        {
            data.Mode = SaveData.OnlineMode.Offline;
        }
        else if (members.CheckBoxAnon.Pressed)
        {
            data.Mode = SaveData.OnlineMode.Anonymous;
        }
        else if (members.CheckBoxNickname.Pressed)
        {
            data.Mode = SaveData.OnlineMode.Online;
        }
        data.Nickname = members.LineEditNickname.Text?.Trim();

        SaveData.SaveOnlineConfig(data);

        var root = (NewRoot)NewRoot.FindRoot(this);
        root.ConfirmNickname(data);
    }

    private void ChooseAnon()
    {
        members.CheckBoxAnon.Pressed = true;
        members.CheckBoxNickname.Pressed = false;
        members.CheckBoxOffline.Pressed = false;
    }

    private void ChooseNickname()
    {
        members.CheckBoxAnon.Pressed = false;
        members.CheckBoxNickname.Pressed = true;
        members.CheckBoxOffline.Pressed = false;
    }

    private void ChooseOffline()
    {
        members.CheckBoxAnon.Pressed = false;
        members.CheckBoxNickname.Pressed = false;
        members.CheckBoxOffline.Pressed = true;
    }
}
