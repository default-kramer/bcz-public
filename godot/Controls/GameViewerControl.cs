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
        members.GameOverMenu.Visible = false;

        OnSizeChanged(); // In case the grid size changed
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
            replayWriter = ReplayWriter.Begin(writer, settings);
            replayCollector = replayCollector.Combine(replayWriter);
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

            QueueViewer.GridViewer = GridViewer;
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
        members.GridViewer.RectMinSize = new Vector2(gridWidth, 0);
        minWidth += gridWidth;
        separationCount++;

        members.QueueViewer.RectMinSize = new Vector2(ladderWidth, 0);
        members.CountdownViewer.RectMinSize = new Vector2(ladderWidth, 0);
        minWidth += ladderWidth;

        if (true)
        {
            separationCount++;
            members.GoalViewerControl.Visible = true;
            members.GoalViewerControl.RectMinSize = new Vector2(ladderWidth, 0);
            minWidth += ladderWidth;
        }
        else
        {
            members.GoalViewerControl.Visible = false;
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

    public override void _Process(float delta)
    {
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

    internal interface ILogic
    {
        void Cleanup();
        void Process(float delta);
        void HandleInput();
        void CheckGameOver();
        void Initialize(Members members);
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
    }

    private static void StandardInitialize(Members members, Ticker ticker, IReadOnlyList<IGoal> goals)
    {
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

        if (goals.Count > 0)
        {
            members.GoalViewerControl.TODO(state, goals);
            members.GoalViewerControl.Visible = true;
        }
        else
        {
            members.GoalViewerControl.Disable();
            members.GoalViewerControl.Visible = false;
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

        public void Initialize(Members members)
        {
            StandardInitialize(members, ticker, Goals);
        }
    }

    sealed class LiveGameLogic : LogicBase
    {
        private ReplayWriter? replayWriter;
        private readonly Members members;
        private readonly GamePackage gamePackage;

        public LiveGameLogic(ReplayWriter? replayWriter, DotnetTicker ticker, Members members, GamePackage gamePackage)
            : base(ticker)
        {
            this.replayWriter = replayWriter;
            this.members = members;
            this.gamePackage = gamePackage;
        }

        protected override IReadOnlyList<IGoal> Goals => gamePackage.Goals;

        public override void Cleanup()
        {
            if (replayWriter != null)
            {
                replayWriter.Flush();
                replayWriter.Close();
                replayWriter.Dispose();
                replayWriter = null;
            }
        }

        public override void CheckGameOver()
        {
            var state = ticker.state;
            if (state.IsGameOver && !members.GameOverMenu.Visible)
            {
                Cleanup();
                members.GameOverMenu.OnGameOver(state, gamePackage);
            }
        }
    }

    class WatchReplayLogic : ILogic
    {
        private readonly IReplayDriver replayDriver;

        public WatchReplayLogic(IReplayDriver replayDriver)
        {
            this.replayDriver = replayDriver;
        }

        public void CheckGameOver() { }

        public void Cleanup() { }

        public void HandleInput() { }

        private Moment now = new Moment(-1);
        public void Process(float delta)
        {
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
            StandardInitialize(members, replayDriver.Ticker, NoGoals);
        }
    }

    private static IReadOnlyList<IGoal> NoGoals = new List<IGoal>();
}
