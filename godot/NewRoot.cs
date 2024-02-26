using BCZ.Core;
using Godot;
using System;

public class NewRoot : Control, IRoot
{
    readonly struct Members
    {
        public readonly GameViewerControl GameViewer;
        public readonly PuzzleControl PuzzleControl;
        public readonly MainMenu MainMenu;
        public readonly ControllerSetupControl ControllerSetupControl;
        public readonly TutorialControl TutorialControl;
        public readonly CreditsControl CreditsControl;
        public readonly SetNicknameControl SetNicknameControl;

        public Members(Control me)
        {
            me.FindNode(out GameViewer, nameof(GameViewer));
            me.FindNode(out PuzzleControl, nameof(PuzzleControl));
            me.FindNode(out MainMenu, nameof(MainMenu));
            me.FindNode(out ControllerSetupControl, nameof(ControllerSetupControl));
            me.FindNode(out TutorialControl, nameof(TutorialControl));
            me.FindNode(out CreditsControl, nameof(CreditsControl));
            me.FindNode(out SetNicknameControl, nameof(SetNicknameControl));
        }
    }

    private Members members;
    private IServerConnection? serverConnection = null;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        this.members = new Members(this);

        if (OS.GetName() == "Web")
        {
            var location = JavaScript.Eval("window.location.toString()") as string;
            Console.WriteLine("Got location: " + location);
            if (location != null)
            {
                var uri = new Uri(location);
                SetupServerConnection($"{uri.Scheme}://{uri.Host}:{uri.Port}");
            }

            BackToMainMenu();
        }
        else
        {
            SwitchTo(members.SetNicknameControl);
        }
    }

    private ServerConnection SetupServerConnection(string baseUrl)
    {
        var conn = new ServerConnection(baseUrl);
        serverConnection = conn;
        this.AddChild(conn);
        conn.PauseMode = AlwaysPausedHack.Process;
        return conn;
    }

    internal static IRoot FindRoot(Node child)
    {
        return child.FindAncestor<IRoot>() ?? throw new Exception("Failed to find root node");
    }

    private SinglePlayerMenu.LevelToken? levelToken;

    public void StartGame(SinglePlayerMenu.LevelToken levelToken)
    {
        this.levelToken = levelToken;
        StartGame(levelToken.CreateGamePackage());
    }

    public bool CanAdvanceToNextLevel()
    {
        return levelToken != null && levelToken.CanAdvance;
    }

    public void AdvanceToNextLevel()
    {
        if (!CanAdvanceToNextLevel() || levelToken == null)
        {
            throw new InvalidOperationException("cannot advance");
        }

        if (levelToken.NextLevel())
        {
            StartGame(levelToken.CreateGamePackage());
        }
        else
        {
            throw new Exception("TODO");
        }
    }

    public void ReplayCurrentLevel()
    {
        if (levelToken == null)
        {
            throw new InvalidOperationException("cannot replay");
        }
        StartGame(levelToken);
    }


    /// <summary>
    /// It's difficult to remove children without leaking them at shutdown.
    /// So let's stop processing instead.
    /// Hmm... I thought that SetProcess(bool) would affect the entire subtree,
    /// but unfortunately it only affects that one node.
    /// (In Godot 4, I could use PROCESS_MODE_DISABLED.)
    /// But there is still a way! Because I am not planning on using SceneTree.Paused to
    /// implement pausing the game, I can hijack it for this purpose.
    /// I will keep the SceneTree paused *at all times* which will stop processing on all
    /// nodes except those which are set to <see cref="Godot.Node.PauseModeEnum.Process"/>.
    ///
    /// The "proper" way to do this is probably using SceneTree.ChangeScene,
    /// but that's more work than I care to do right now.
    /// </summary>
    static class AlwaysPausedHack
    {
        public const bool True = true;
        public static readonly PauseModeEnum Process = PauseModeEnum.Process;
    }

    private static void SetEnabled(Control c, bool enabled)
    {
        c.Visible = enabled;
        c.PauseMode = enabled ? AlwaysPausedHack.Process : PauseModeEnum.Inherit;
        // These are probably redundant now:
        c.SetProcess(enabled);
        c.SetProcessInput(enabled);
    }

    private void SwitchTo(Control control)
    {
        GetTree().Paused = AlwaysPausedHack.True;

        SetEnabled(members.GameViewer, false);
        SetEnabled(members.PuzzleControl, false);
        SetEnabled(members.MainMenu, false);
        SetEnabled(members.ControllerSetupControl, false);
        SetEnabled(members.TutorialControl, false);
        SetEnabled(members.CreditsControl, false);
        SetEnabled(members.SetNicknameControl, false);

        SetEnabled(control, true);
    }

    private void StartGame(GamePackage package)
    {
        SwitchTo(members.GameViewer);
        members.GameViewer.StartGame(package);
    }

    public void BackToMainMenu()
    {
        SwitchTo(members.MainMenu);
        members.MainMenu.ShowMainMenu();
    }

    public void WatchReplay(string replayFile)
    {
        members.GameViewer.WatchReplay(replayFile);
        SwitchTo(members.GameViewer);
    }

    public void ShowCredits()
    {
        SwitchTo(members.CreditsControl);
        members.CreditsControl.OnShown();
    }

    public void SolvePuzzles()
    {
        SwitchTo(members.PuzzleControl);
        members.PuzzleControl.TEST_SolvePuzzle();
    }

    public void ControllerSetup()
    {
        SwitchTo(members.ControllerSetupControl);
        members.ControllerSetupControl.Reset();
    }

    public void StartTutorial()
    {
        SwitchTo(members.TutorialControl);
        members.TutorialControl.Reset();
    }

    internal void ConfirmNickname(SaveData.OnlineConfigData data)
    {
        if (data.Mode == SaveData.OnlineMode.Offline)
        {
            serverConnection = UnavailableServer.Instance;
        }
        else
        {
            var conn = SetupServerConnection("https://blockcipherz.com");
            if (data.Mode == SaveData.OnlineMode.Online)
            {
                conn.PlayerNickname = data.Nickname;
            }
        }

        BackToMainMenu();
    }

    IServerConnection IRoot.GetServerConnection()
    {
        return serverConnection ?? throw new Exception("Premature call to GetServerConnection");
    }
}
