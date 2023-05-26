using BCZ.Core;
using BCZ.Core.ReplayModel;
using Godot;
using System;
using System.Collections.Generic;

#nullable enable

public class GameViewerControl : Control
{
    private ILogic logic = NullLogic.Instance;

    internal void SetLogic(ILogic newLogic)
    {
        logic.Cleanup();
        logic = newLogic;
        logic.Initialize(members);

        OnSizeChanged(); // In case the grid size changed
    }

    public void WatchDemo()
    {
        logic.Cleanup();
        var driver = ReplayReader.BuildReplayDriver2(demoReplayContent);
        var newLogic = new WatchReplayLogic(driver);
        newLogic.DisablePausing = true;
        SetLogic(newLogic);
    }

    public void WatchReplay(string filepath)
    {
        logic.Cleanup();
        var driver = ReplayReader.BuildReplayDriver(filepath);
        var newLogic = new WatchReplayLogic(driver);
        SetLogic(newLogic);
    }

    private void NewGame(GamePackage gamePackage)
    {
        var settings = gamePackage.Settings;
        var state = State.Create(settings);
        ReplayWriter? replayWriter = null;
        var listReplayCollector = new ListReplayCollector();
        IReplayCollector replayCollector = listReplayCollector;

        string replayDir = System.Environment.GetEnvironmentVariable("ffreplaydir");
        if (replayDir != null)
        {
            replayDir = System.IO.Path.Combine(replayDir, "raw");
            var di = new System.IO.DirectoryInfo(replayDir);
            if (!di.Exists)
            {
                di.Create();
            }
            var filename = System.IO.Path.Combine(replayDir, $"{DateTime.Now.ToString("yyyyMMdd_HHmmss")}_{settings.Seed.Serialize()}.ffr");
            var writer = new System.IO.StreamWriter(filename);
            replayWriter = ReplayWriter.Begin(writer, settings, shouldDispose: true);
            replayCollector = replayCollector.Combine(replayWriter);
        }

        var server = NewRoot.FindRoot(this).GetServerConnection();
        if (server != null)
        {
            replayCollector = replayCollector.Combine(new GameUploader(server, settings));
        }

        var ticker = new DotnetTicker(state, replayCollector);
        var newLogic = new LiveGameLogic(replayWriter, ticker, members, gamePackage);
        SetLogic(newLogic);
    }

    internal readonly struct Members
    {
        public readonly GridViewerControl GridViewer;
        public readonly QueueViewerControl QueueViewer;
        public readonly CountdownViewerControl CountdownViewer;
        public readonly GameOverMenu GameOverMenu;
        public readonly HBoxContainer HBoxContainer;
        public readonly SwitchViewerControl SwitchViewerControl;
        public readonly GridViewerControl AttackGridViewer;
        public readonly GoalViewerControl GoalViewerControl;
        private readonly Control GridViewerContainer;
        public readonly Control PauseMenuContainer;
        public readonly Button ButtonResume;
        public readonly Button ButtonRestart;
        public readonly Button ButtonQuit;

        public Members(Control me)
        {
            me.FindNode(out GridViewer, nameof(GridViewer));
            me.FindNode(out QueueViewer, nameof(QueueViewer));
            me.FindNode(out CountdownViewer, nameof(CountdownViewer));
            me.FindNode(out GameOverMenu, nameof(GameOverMenu));
            me.FindNode(out HBoxContainer, nameof(HBoxContainer));
            me.FindNode(out SwitchViewerControl, nameof(SwitchViewerControl));
            me.FindNode(out AttackGridViewer, nameof(AttackGridViewer));
            me.FindNode(out GoalViewerControl, nameof(GoalViewerControl));
            me.FindNode(out GridViewerContainer, nameof(GridViewerContainer));
            me.FindNode(out PauseMenuContainer, nameof(PauseMenuContainer));
            me.FindNode(out ButtonResume, nameof(ButtonResume));
            me.FindNode(out ButtonRestart, nameof(ButtonRestart));
            me.FindNode(out ButtonQuit, nameof(ButtonQuit));

            QueueViewer.GridViewer = GridViewer;
        }

        public void SetGridViewerSize(Vector2 rectMinSize)
        {
            GridViewerContainer.RectMinSize = rectMinSize;
            // Ensure that these all take up the full space
            GridViewer.RectMinSize = rectMinSize;
            GameOverMenu.RectMinSize = rectMinSize;
            PauseMenuContainer.RectMinSize = rectMinSize;
        }
    }

    private Members members;

    public bool ShowQueue
    {
        get { return members.QueueViewer.Visible; }
        set { members.QueueViewer.Visible = value; }
    }

    public override void _Ready()
    {
        this.members = new Members(this);
        members.ButtonResume.Connect("pressed", this, nameof(PressedResume));
        members.ButtonRestart.Connect("pressed", this, nameof(PressedRestart));
        members.ButtonQuit.Connect("pressed", this, nameof(PressedQuit));
        OnSizeChanged();
    }

    public override void _Notification(int what)
    {
        base._Notification(what);

        if (what == NotificationResized)
        {
            OnSizeChanged();
        }
    }

    /// <summary>
    /// We need to set our min width to match our contents.
    /// I tried letting the HBox calculate the width and using that, but it got squirrelly.
    /// </summary>
    private float minWidth = 0;

    public override Vector2 _GetMinimumSize()
    {
        return new Vector2(minWidth, 0);
    }

    public void OnSizeChanged()
    {
        // SIZING NOTES:
        // We want this control to be full-height, minus a little bit dictated by the outermost MarginContainer.
        // This y-margin is important because the drawing code is not super-precise and I think it draws beyond
        // its bounds by a few pixels.
        //
        // The width should be dynamically controlled based on the height.
        // The AspectRatioContainer does not seem to work the way I want.
        // So instead we calculate how wide each component should be given the height,
        // and we specify a RectMinSize.x for each component (but never RectMinSize.y because we want full-height).
        //
        // The HBoxContainer.Alignment property will take care of centering the components.
        // I overlooked this property for way too long. Thanks to lewiji on the Godot Discord!

        // WARNING - These must match what you set in the editor. (Of course, I could grab the values here, but meh...)
        const float yMargin = 20f;
        const float xSeparation = 13;

        float availHeight = this.RectSize.y - yMargin;
        if (availHeight < 0)
        {
            return;
        }

        float ladderWidth = RectSize.y * 0.16f;

        minWidth = 0;
        int separationCount = 0;

        float gridWidth = members.GridViewer.DesiredWidth(availHeight);
        members.SetGridViewerSize(new Vector2(gridWidth, 0));
        minWidth += gridWidth;
        separationCount++;

        members.QueueViewer.RectMinSize = new Vector2(ladderWidth, 0);
        members.CountdownViewer.RectMinSize = new Vector2(ladderWidth, 0);
        minWidth += ladderWidth;

        if (members.GoalViewerControl.Visible)
        {
            separationCount++;
            members.GoalViewerControl.Visible = true;
            members.GoalViewerControl.RectMinSize = new Vector2(ladderWidth, 0);
            minWidth += ladderWidth;
        }

        if (members.SwitchViewerControl.Visible)
        {
            separationCount++;
            // We set this via the editor
            minWidth += members.SwitchViewerControl.RectMinSize.x;
        }

        if (members.AttackGridViewer.Visible)
        {
            separationCount++;
            float attackGridWidth = gridWidth * 1.5f;
            members.AttackGridViewer.RectMinSize = new Vector2(attackGridWidth, 0);
            minWidth += attackGridWidth;
        }

        // I don't know why this is needed... but who cares?
        // (Maybe some margins hiding somewhere in the tree?)
        const float fudge = 20f;
        minWidth += fudge + separationCount * xSeparation;
    }

    private bool _firstDraw = true;
    public override void _Draw()
    {
        if (_firstDraw)
        {
            OnSizeChanged();
            Update();
            _firstDraw = false;
        }

        base._Draw();
    }

    private PauseMenuActions pauseMenuAllowedActions = PauseMenuActions.None;
    private void ForceSetPaused(bool paused)
    {
        this.paused = paused;
        members.GridViewer.SetPaused(paused);
        members.GridViewer.Update();
        members.PauseMenuContainer.Visible = paused;
        if (paused)
        {
            members.PauseMenuContainer.FindNextValidFocus().GrabFocus();
            members.ButtonRestart.Visible = pauseMenuAllowedActions.HasFlag(PauseMenuActions.Restart);
            members.ButtonQuit.Visible = pauseMenuAllowedActions.HasFlag(PauseMenuActions.Quit);
        }
    }

    private bool TryTogglePause()
    {
        if (logic.TrySetPaused(!paused, out pauseMenuAllowedActions))
        {
            ForceSetPaused(!paused);
            return true;
        }
        return false;
    }

    private bool paused;
    public override void _Process(float delta)
    {
        if (Input.IsActionJustPressed("game_pause"))
        {
            if (members.GameOverMenu.Visible)
            {
                // Cannot pause. Might as well make sure we are unpaused.
                ForceSetPaused(false);
            }
            else
            {
                TryTogglePause();
            }
        }

        if (paused)
        {
            return;
        }

        this.logic.Process(delta);
        this.logic.HandleInput();

        members.GridViewer.Update();
        members.QueueViewer.Update();
        members.CountdownViewer.Update();
        members.SwitchViewerControl.Update();
        members.AttackGridViewer.Update();
        members.GoalViewerControl.Update();

        this.logic.CheckGameOver();
    }

    public void StartGame(GamePackage package)
    {
        NewGame(package);
        members.GameOverMenu.Visible = false;
    }

    private void ForceUnpause()
    {
        if (paused)
        {
            if (!TryTogglePause())
            {
                ForceSetPaused(false);
            }
        }
    }

    void PressedResume() => ForceUnpause();
    void PressedRestart()
    {
        ForceUnpause();
        var root = NewRoot.FindRoot(this);
        logic.HandlePauseAction(PauseMenuActions.Restart, root);
    }
    void PressedQuit()
    {
        ForceUnpause();
        var root = NewRoot.FindRoot(this);
        logic.HandlePauseAction(PauseMenuActions.Quit, root);
    }

    internal interface ILogic
    {
        void Cleanup();
        void Process(float delta);
        void HandleInput();
        void CheckGameOver();
        void Initialize(Members members);

        /// <summary>
        /// Return true iff the game was succesfully paused or unpaused.
        /// </summary>
        bool TrySetPaused(bool paused, out PauseMenuActions allowedActions);

        /// <summary>
        /// Even though <paramref name="action"/> is a flags enum, this will only ever
        /// be called with a single value from the enum definition.
        /// </summary>
        void HandlePauseAction(PauseMenuActions action, IRoot root);
    }

    sealed class NullLogic : ILogic
    {
        private NullLogic() { }

        public static readonly NullLogic Instance = new NullLogic();

        public void Cleanup() { }
        public void Process(float delta) { }
        public void HandleInput() { }
        public void CheckGameOver() { }

        public void Initialize(Members members)
        {
            members.GridViewer.SetLogic(GridViewerControl.NullLogic.Instance);
            members.GridViewer.Visible = true;
            // TODO should set a null model here... but we'll just make them invisible for now
            members.QueueViewer.Visible = false;
        }

        public bool TrySetPaused(bool paused, out PauseMenuActions allowedActions)
        {
            allowedActions = PauseMenuActions.None;
            return false;
        }

        public void HandlePauseAction(PauseMenuActions action, IRoot root)
        {
            throw new Exception("Should never be called");
        }
    }

    internal abstract class LogicBase : ILogic
    {
        protected readonly DotnetTicker ticker;
        private bool holdingDrop = false;

        protected LogicBase(DotnetTicker ticker)
        {
            this.ticker = ticker;
        }

        private bool HandleCommand(Command command)
        {
            return ticker.HandleCommand(command);
        }

        public abstract bool TrySetPaused(bool paused, out PauseMenuActions allowedActions);
        public abstract void HandlePauseAction(PauseMenuActions action, IRoot root);

        public virtual void HandleInput()
        {
            if (Input.IsActionJustPressed("game_left"))
            {
                HandleCommand(Command.Left);
            }
            if (Input.IsActionJustPressed("game_right"))
            {
                HandleCommand(Command.Right);
            }
            if (Input.IsActionJustPressed("game_rotate_cw"))
            {
                HandleCommand(Command.RotateCW);
            }
            if (Input.IsActionJustPressed("game_rotate_ccw"))
            {
                HandleCommand(Command.RotateCCW);
            }

            if (Input.IsActionPressed("game_drop"))
            {
                if (!holdingDrop)
                {
                    HandleCommand(Command.BurstBegin);
                    holdingDrop = true;
                }
            }
            else
            {
                if (holdingDrop)
                {
                    HandleCommand(Command.BurstCancel);
                    holdingDrop = false;
                }
            }
        }

        public virtual void Process(float delta)
        {
            ticker._Process(delta);
        }

        public virtual void Cleanup() { }

        public virtual void CheckGameOver() { }

        protected virtual IReadOnlyList<IGoal> Goals => NoGoals;

        public virtual void Initialize(Members members)
        {
            StandardInitialize(members, ticker);
        }

        public static void StandardInitialize(Members members, Ticker ticker)
        {
            members.GridViewer.SetShrouded(false);
            members.GameOverMenu.Visible = false;

            members.GridViewer.SetLogic(ticker);
            var state = ticker.state;
            members.QueueViewer.Model = state.MakeQueueModel();

            members.GridViewer.Visible = true;
            members.QueueViewer.Visible = true;

            members.CountdownViewer.SetModel(state.CountdownViewmodel);

            if (state.SwitchesViewmodel != null)
            {
                members.SwitchViewerControl.SetModel(state.SwitchesViewmodel);
                members.SwitchViewerControl.Visible = true;
            }
            else
            {
                members.SwitchViewerControl.Visible = false;
            }

            if (state.AttackGridViewmodel != null)
            {
                members.AttackGridViewer.SetLogicForAttackGrid(state.AttackGridViewmodel);
                members.SwitchViewerControl.AttackViewer = members.AttackGridViewer;
                members.AttackGridViewer.Visible = true;
            }
            else
            {
                members.AttackGridViewer.Visible = false;
                members.SwitchViewerControl.AttackViewer = null;
            }
        }
    }

    sealed class LiveGameLogic : LogicBase
    {
        private IReplayCollector? replayWriter;
        private readonly Members members;
        private readonly GamePackage gamePackage;

        public LiveGameLogic(ReplayWriter? replayWriter, DotnetTicker ticker, Members members, GamePackage gamePackage)
            : base(ticker)
        {
            this.replayWriter = replayWriter;
            this.members = members;
            this.gamePackage = gamePackage;
        }

        public override void CheckGameOver()
        {
            var state = ticker.state;
            if (state.IsGameOver && !members.GameOverMenu.Visible)
            {
                replayWriter?.OnGameEnded();
                members.GridViewer.SetShrouded(true);
                members.GameOverMenu.OnGameOver(state, gamePackage);
            }
        }

        public override void Initialize(Members members)
        {
            base.Initialize(members);
            gamePackage.Initialize(members, ticker);
        }

        public override bool TrySetPaused(bool paused, out PauseMenuActions allowedActions)
        {
            ticker.SetPaused(paused);
            allowedActions = PauseMenuActions.Restart | PauseMenuActions.Quit;
            return true;
        }

        public override void HandlePauseAction(PauseMenuActions action, IRoot root)
        {
            switch (action)
            {
                case PauseMenuActions.Restart:
                    root.ReplayCurrentLevel();
                    break;
                case PauseMenuActions.Quit:
                    root.BackToMainMenu();
                    break;
                default:
                    throw new Exception("Cannot handle: " + action);
            }
        }
    }

    class WatchReplayLogic : ILogic
    {
        private readonly IReplayDriver replayDriver;
        private bool paused = false;

        public bool DisablePausing { get; set; }

        public WatchReplayLogic(IReplayDriver replayDriver)
        {
            this.replayDriver = replayDriver;
        }

        public void CheckGameOver() { }

        public void Cleanup() { }

        public void HandleInput() { }

        public bool TrySetPaused(bool paused, out PauseMenuActions allowedActions)
        {
            allowedActions = PauseMenuActions.Quit;

            if (DisablePausing)
            {
                return false;
            }

            this.paused = paused;
            return true;
        }

        public void HandlePauseAction(PauseMenuActions action, IRoot root)
        {
            switch (action)
            {
                case PauseMenuActions.Quit:
                    root.BackToMainMenu();
                    break;
                default:
                    throw new Exception("Cannot handle: " + action);
            }
        }

        private Moment now = new Moment(-1);
        public void Process(float delta)
        {
            if (paused)
            {
                return;
            }

            if (now.Millis < 0)
            {
                now = new Moment(0);
            }
            else
            {
                int millis = Convert.ToInt32(delta * 1000);
                now = now.AddMillis(millis);
            }
            replayDriver.Advance(now);
        }

        public void Initialize(Members members)
        {
            LogicBase.StandardInitialize(members, replayDriver.Ticker);
        }
    }

    private static IReadOnlyList<IGoal> NoGoals = new List<IGoal>();

    const string demoReplayContent = @"version -1
s seed 3908840814-3639597817-881223993-805301161-1430385061-2642742980
s mode Levels
s enemyCount 24
s spawnBlanks True
s gridWidth 8
s gridHeight 20
s enemiesPerStripe 5
s rowsPerStripe 2
s scorePerEnemy 100
c 2 6659
h 121814359
c 6 7759
h -1108239849
c 7 7859
h -1108239849
c 1 8644
h -1108239849
c 1 8827
h -1108239849
c 4 9394
h -1108239849
c 6 10847
h -1060670533
c 7 10947
h -1060670533
c 1 11849
h -1060670533
c 4 12282
h -1060670533
c 6 13201
h 243341431
c 7 13317
h 243341431
c 1 13784
h 243341431
c 1 13968
h 243341431
c 3 14518
h 243341431
c 3 14736
h 243341431
c 6 15937
h 876323495
c 7 16054
h 876323495
c 3 18140
h 1758635855
c 6 20594
h 190414755
c 7 20710
h 190414755
c 1 21194
h 190414755
c 3 21478
h 190414755
c 3 21645
h 190414755
c 6 22530
h 449715267
c 7 22629
h 449715267
c 2 22964
h 449715267
c 3 23197
h 449715267
c 6 25366
h 1420315007
c 7 25450
h 1420315007
c 3 31692
h 1420315007
c 2 31725
h 1420315007
c 2 31959
h 1420315007
c 2 32193
h 1420315007
c 1 33361
h 1420315007
c 6 34262
h 974740659
c 7 34362
h 974740659
c 1 34546
h 974740659
c 6 40120
h 1900548435
c 7 40203
h 1900548435
c 3 41872
h 1900548435
c 2 41928
h 1900548435
c 2 42123
h 1900548435
c 3 42140
h 1900548435
c 6 43124
h -612791101
c 7 43241
h -612791101
c 4 43708
h -612791101
c 2 43892
h -612791101
c 6 44843
h 965311711
c 3 47547
h 7850135
c 1 47781
h 7850135
c 6 50351
h 896414179
c 7 50451
h 896414179
c 1 52821
h -1597806622
c 1 53087
h -1597806622
c 6 54456
h 1891189026
c 7 54572
h 1891189026
c 1 54790
h 1891189026
c 3 54940
h 1891189026
c 3 55124
h 1891189026
c 1 55341
h 1891189026
c 6 57193
h 238766690
c 7 57310
h 238766690
c 4 58328
h -1947836639
c 1 58344
h -1947836639
c 1 58628
h -1947836639
c 1 58795
h -1947836639
c 6 60114
h -2136781379
c 7 60214
h -2136781379
c 3 62634
h -2136781379
c 2 62868
h -2136781379
c 6 64503
h 659769737
c 7 64604
h 659769737
c 2 66055
h 659769737
c 3 66622
h 659769737
c 3 66907
h 659769737
c 6 67607
h -314184503
c 7 67708
h -314184503
c 2 68826
h -314184503
c 6 70428
h 1973829641
c 7 70512
h 1973829641
c 1 74583
h 1973829641
c 4 74617
h 1973829641
c 1 75151
h 1973829641
c 1 75585
h 1973829641
c 6 76586
h -1700928571
c 7 76670
h -1700928571
c 3 78372
h -1700928571
c 3 78773
h -1700928571
c 1 79073
h -1700928571
c 6 80241
h -49236539
c 7 80325
h -49236539
c 4 81242
h -49236539
c 6 85482
h -487656663
c 7 85548
h -487656663
c 1 87285
h -487656663
c 4 87318
h -487656663
c 6 88286
h 1103707365
c 7 88369
h 1103707365
c 6 91490
h 1962958869
c 2 95846
h -655808435
c 2 96012
h -655808435
c 2 96246
h -655808435
c 3 96497
h -655808435
c 3 96663
h -655808435
c 6 97615
h 1534961565
c 7 97715
h 1534961565
c 3 100702
h 1534961565
c 3 100886
h 1534961565
c 2 101170
h 1534961565
c 1 102671
h 1534961565
c 3 102689
h 1534961565
c 1 102888
h 1534961565
c 3 102939
h 1534961565
c 1 103373
h 1534961565
c 6 104658
h -117460771
c 7 104774
h -117460771
c 2 106577
h -963829690
c 2 106777
h -963829690
c 6 107712
h -799938906
c 7 107845
h -799938906
c 6 109948
h 1715671247
c 7 110017
h 1715671247
c 4 113403
h 1715671247
c 6 115990
h -2110329261
c 7 116057
h -2110329261
c 2 116724
h -2110329261
c 3 116741
h -2110329261
c 2 116974
h -2110329261
c 3 116991
h -2110329261
c 6 117708
h -579937101
c 7 117809
h -579937101
c 1 120596
h 426091707
c 3 120646
h 426091707
c 1 120946
h 426091707
c 3 120963
h 426091707
c 6 122298
h 1786995435
c 7 122415
h 1786995435
c 1 123249
h 1786995435
c 1 123934
h 1786995435
c 6 125052
h 2130310427
c 7 125119
h 2130310427
c 1 126654
h 2130310427
c 1 127472
h 2130310427
c 1 127789
h 2130310427
c 6 129207
h 1543150923
c 7 129307
h 1543150923
c 4 131427
h 36272755
c 2 131444
h 36272755
c 6 132328
h -1771262865
c 3 135666
h 23311838
c 2 135683
h 23311838
c 6 136084
h -1699963478
c 7 136167
h -1699963478
c 4 136718
h -1699963478
c 6 137335
h 1149192206
c 1 139238
h 1040617367
c 6 140023
h -2000990793
c 7 140122
h -2000990793
c 2 141758
h -2000990793
c 3 141775
h -2000990793
c 6 142275
h -2005083517
c 7 142359
h -2005083517
c 3 142809
h -2005083517
c 3 143110
h -2005083517
c 3 143627
h -2005083517
c 3 143911
h -2005083517
c 3 144478
h -2005083517
c 2 144878
h -2005083517
c 2 145162
h -2005083517
c 2 145496
h -2005083517
c 6 147433
h -560688273
c 7 147516
h -560688273
c 1 148217
h -560688273
c 1 148500
h -560688273
c 1 148951
h -560688273
c 6 150821
h 14327231
c 7 150921
h 14327231
c 1 153107
h 14327231
c 3 153256
h 14327231
c 3 153440
h 14327231
c 6 154009
h -370928673
c 7 154109
h -370928673
c 1 155593
h 883700697
c 3 155611
h 883700697
c 2 155945
h 883700697
c 6 157079
h 1873398845
c 7 157179
h 1873398845
c 6 159365
h 1302253229
c 7 159465
h 1302253229
c 2 160266
h 1302253229
c 4 160300
h 1302253229
c 2 160867
h 1302253229
c 2 161118
h 1302253229
c 6 161518
h 11931113
c 7 161619
h 11931113
c 4 161869
h 11931113
c 1 161885
h 11931113
c 1 162219
h 11931113
c 1 162569
h 11931113
c 6 164072
h -1779566683
c 3 166825
h -2094090426
c 3 167092
h -2094090426
c 2 167259
h -2094090426
c 6 168478
h -1140598138
c 7 168577
h -1140598138
c 2 170613
h -1140598138
c 6 171648
h -756178378
c 7 171765
h -756178378
c 3 172250
h -756178378
c 6 172901
h -476616022
c 7 173017
h -476616022
c 2 174419
h -1826959199
c 3 174436
h -1826959199
c 2 174753
h -1826959199
c 3 174786
h -1826959199
c 2 175086
h -1826959199
c 6 175921
h -1637576351
c 7 176021
h -1637576351
c 4 176388
h -1637576351
c 2 176688
h -1637576351
c 6 177424
h 429149341
c 7 177507
h 429149341
c 3 178724
h 941146668
c 6 179175
h -39479936
c 7 179276
h -39479936
c 2 179643
h -39479936
c 3 179776
h -39479936
c 3 179977
h -39479936
c 2 180210
h -39479936
c 2 180460
h -39479936
c 6 181145
h 1496360512
c 7 181262
h 1496360512
";
}
