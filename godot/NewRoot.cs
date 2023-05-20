using BCZ.Core;
using Godot;
using System;

#nullable enable

interface IServerConnection
{
    void UploadGame(string replayFile);
}

class BrowserBasedServerConnection : Godot.Node, IServerConnection
{
    private readonly HTTPRequest http = new HTTPRequest();
    private readonly string baseUrl;

    public BrowserBasedServerConnection(string baseUrl)
    {
        this.baseUrl = baseUrl;
        AddChild(http);
        http.Connect("request_completed", this, nameof(OnRequestCompleted));
    }

    private void OnRequestCompleted(long result, long responseCode, string[] headers, byte[] body)
    {
        Console.WriteLine($"Request completed! Got {result} / {responseCode} / {body.GetStringFromUTF8()}");
    }

    public void UploadGame(string replayFile)
    {
        http.Request($"{baseUrl}/api/upload-game/v1", method: HTTPClient.Method.Post, requestData: replayFile);
    }
}

public class NewRoot : Control
{
    readonly struct Members
    {
        public readonly GameViewerControl GameViewer;
        public readonly PuzzleControl PuzzleControl;
        public readonly MainMenu MainMenu;
        public readonly ControllerSetupControl ControllerSetupControl;
        public readonly TutorialControl TutorialControl;
        public readonly CreditsControl CreditsControl;

        public Members(Control me)
        {
            me.FindNode(out GameViewer, nameof(GameViewer));
            me.FindNode(out PuzzleControl, nameof(PuzzleControl));
            me.FindNode(out MainMenu, nameof(MainMenu));
            me.FindNode(out ControllerSetupControl, nameof(ControllerSetupControl));
            me.FindNode(out TutorialControl, nameof(TutorialControl));
            me.FindNode(out CreditsControl, nameof(CreditsControl));
        }
    }

    private Members members;
    private IServerConnection? serverConnection = null;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        var location = JavaScript.Eval("window.location.toString()") as string;
        Console.WriteLine("Got location: " + location);
        if (location != null)
        {
            var uri = new Uri(location);
            var conn = new BrowserBasedServerConnection($"{uri.Scheme}://{uri.Host}:{uri.Port}");
            serverConnection = conn;
            this.AddChild(conn);
        }

        var userAgent = JavaScript.Eval("navigator.userAgent") as string;
        if (userAgent != null)
        {
            Console.WriteLine("Got user agent: " + userAgent);
        }

        this.members = new Members(this);
        BackToMainMenu();
    }

    internal static NewRoot FindRoot(Node child)
    {
        return child.FindAncestor<NewRoot>() ?? throw new Exception("Failed to find root node");
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

    private static void Remove(Control c)
    {
        c.GetParent()?.RemoveChild(c);
    }

    // It's difficult to remove children without leaking them at shutdown.
    // This seems to work just as well:
    private static void SetEnabled(Control c, bool enabled)
    {
        c.Visible = enabled;
        c.SetProcess(enabled);
        c.SetProcessInput(enabled);
    }

    private void SwitchTo(Control control)
    {
        SetEnabled(members.GameViewer, false);
        SetEnabled(members.PuzzleControl, false);
        SetEnabled(members.MainMenu, false);
        SetEnabled(members.ControllerSetupControl, false);
        SetEnabled(members.TutorialControl, false);
        SetEnabled(members.CreditsControl, false);

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
        SwitchTo(members.GameViewer);
        members.GameViewer.WatchReplay(replayFile);
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

    internal IServerConnection? GetServerConnection() => serverConnection;
}
