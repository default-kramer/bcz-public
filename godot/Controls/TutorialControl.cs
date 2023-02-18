using FF2.Core;
using Godot;
using System;

#nullable enable

public class TutorialControl : Control
{
    const FF2.Core.Color yellow = FF2.Core.Color.Yellow;
    const FF2.Core.Color blue = FF2.Core.Color.Blue;
    const FF2.Core.Color red = FF2.Core.Color.Red;
    const FF2.Core.Color blank = FF2.Core.Color.Blank;
    static readonly Occupant enemyYellow = Occupant.MakeEnemy(yellow);
    static readonly Occupant enemyBlue = Occupant.MakeEnemy(blue);
    static readonly Occupant enemyRed = Occupant.MakeEnemy(red);

    readonly struct Members
    {
        public readonly GameViewerControl GameViewerControl;
        public readonly RichTextLabel RichTextLabel;
        public readonly Texture EnemyTexture;

        public Members(Control me)
        {
            me.FindNode(out GameViewerControl, nameof(GameViewerControl));
            me.FindNode(out RichTextLabel, nameof(RichTextLabel));

            EnemyTexture = (Texture)ResourceLoader.Load("res://Sprites/enemy.bmp");
        }
    }

    private Members members;
    private Challenge challenge;
    private Progress progress;

    public override void _Ready()
    {
        members = new Members(this);
        Reset();
    }

    public void Reset()
    {
        StartChallenge(Challenge0());
    }

    private void StartChallenge(Challenge challenge)
    {
        this.challenge = challenge;
        progress = challenge.Id;
    }

    public override void _Process(float delta)
    {
        if (Input.IsActionJustPressed("game_drop")
            || Input.IsActionJustPressed("game_rotate_cw")
            || Input.IsActionJustPressed("game_rotate_ccw")
            || Input.IsActionJustPressed("ui_select")
            || Input.IsActionJustPressed("ui_accept")
            || Input.IsActionJustPressed("ui_cancel"))
        {
            if (progress != challenge.Id)
            {
                CallDeferred(nameof(AdvanceText));
            }
        }
    }

    private void AdvanceText()
    {
        if (progress == Progress.Challenge0_Fail)
        {
            StartChallenge(Challenge0());
        }
        else if (progress == Progress.Challenge0_Success || progress == Progress.Challenge1_Fail)
        {
            StartChallenge(Challenge1());
        }
        else if (progress == Progress.Challenge1_Success || progress == Progress.Challenge2_Fail)
        {
            StartChallenge(Challenge2());
        }
        else if (progress == Progress.Challenge2_Success || progress == Progress.Challenge3_Fail)
        {
            StartChallenge(Challenge3());
        }
        else if (progress == Progress.Challenge3_Success)
        {
            SetText("This concludes the tutorial. If you're having trouble, just focus on destroying [icon] targets, and worry about combos and blanks later.");
            progress = Progress.Farewell;
        }
        else if (progress == Progress.Farewell)
        {
            NewRoot.FindRoot(this).BackToMainMenu();
        }
    }

    private Challenge Challenge0()
    {
        var lbl = members.RichTextLabel;
        lbl.Clear();

        lbl.AddText("Drop pieces to make groups of matching colors. Groups of 4 or more are destroyed. Destroy all ");
        lbl.AddImage(members.EnemyTexture, 20, 0);
        lbl.AddText(" to proceed.");

        var grid = Grid.Create();
        grid.Set(new Loc(1, 2), enemyYellow);
        grid.Set(new Loc(6, 2), enemyBlue);

        return MakeChallenge(Progress.Challenge0_Active, grid, spawns0);
    }

    static readonly SpawnItem[] spawns0 =
    {
        SpawnItem.MakeCatalystPair(yellow, yellow),
        SpawnItem.MakeCatalystPair(blue, blue),
        SpawnItem.MakeCatalystPair(yellow, yellow),
        SpawnItem.MakeCatalystPair(blue, blue),
        SpawnItem.MakeCatalystPair(yellow, yellow),
        SpawnItem.MakeCatalystPair(blue, blue),
    };

    private Challenge Challenge1()
    {
        var lbl = members.RichTextLabel;
        lbl.Clear();

        lbl.AddText("Making combos will become an important skill. Place your pieces carefully to create a combo.");

        var grid = Grid.Create();
        grid.Set(new Loc(4, 0), enemyRed);
        grid.Set(new Loc(4, 1), enemyRed);
        grid.Set(new Loc(4, 2), enemyRed);
        grid.Set(new Loc(6, 0), enemyYellow);
        grid.Set(new Loc(6, 1), enemyYellow);
        grid.Set(new Loc(6, 2), enemyYellow);
        grid.Set(new Loc(5, 2), enemyBlue);
        grid.Set(new Loc(5, 3), enemyBlue);

        return MakeChallenge(Progress.Challenge1_Active, grid, spawns1);
    }

    static readonly SpawnItem[] spawns1 =
    {
        SpawnItem.MakeCatalystPair(blue, yellow),
        SpawnItem.MakeCatalystPair(blue, red),
    };

    private Challenge Challenge2()
    {
        SetText("Blank pieces [icon] have no color and do not form groups."
            + " Holding the drop button Bursts all blanks."
            + " A nice combo is already set up here, all you have to do is hold the drop button.");

        var grid = Grid.Create();

        grid.Set(new Loc(3, 0), enemyRed);
        grid.Set(new Loc(3, 1), enemyRed);
        grid.Set(new Loc(3, 2), enemyRed);
        grid.Set(new Loc(3, 3), Occupant.MakeCatalyst(blank, Direction.Up));
        grid.Set(new Loc(3, 4), Occupant.MakeCatalyst(red, Direction.Down));
        grid.Set(new Loc(3, 5), Occupant.MakeCatalyst(red, Direction.Right));
        grid.Set(new Loc(4, 5), Occupant.MakeCatalyst(blue, Direction.Left));

        grid.Set(new Loc(4, 0), enemyBlue);
        grid.Set(new Loc(4, 1), enemyBlue);
        grid.Set(new Loc(4, 2), enemyBlue);

        return MakeChallenge(Progress.Challenge2_Active, grid, spawns2);
    }

    static readonly SpawnItem[] spawns2 = { SpawnItem.MakeCatalystPair(red, blue) };

    private Challenge Challenge3()
    {
        SetText("Use the blank to set up a 3-group combo. Don't forget to Burst, but don't Burst too early!");

        var grid = Grid.Create();
        grid.Set(new Loc(2, 0), enemyYellow);
        grid.Set(new Loc(2, 1), enemyYellow);
        grid.Set(new Loc(3, 1), enemyRed);
        grid.Set(new Loc(3, 2), enemyRed);
        grid.Set(new Loc(4, 0), enemyBlue);
        grid.Set(new Loc(4, 1), enemyBlue);
        grid.Set(new Loc(4, 2), enemyBlue);

        return MakeChallenge(Progress.Challenge3_Active, grid, spawns3);
    }

    static readonly SpawnItem[] spawns3 =
    {
        SpawnItem.MakeCatalystPair(yellow, blank),
        SpawnItem.MakeCatalystPair(yellow, red),
        SpawnItem.MakeCatalystPair(red, blue),
    };

    private Challenge MakeChallenge(Progress id, Grid grid, SpawnItem[] spawns)
    {
        var deck = new CannedSpawnDeck(spawns);
        var state = State.CreateWithInfiniteHealth(grid, deck);
        var ticker = new DotnetTicker(state, NullReplayCollector.Instance);
        ticker.DelayStart(0.5f);
        members.GameViewerControl.SetLogic(new Logic(ticker, this));
        return new Challenge(id, deck, state);
    }

    void SetText(string text)
    {
        members.RichTextLabel.Clear();
        members.RichTextLabel.AddText(text);
    }

    void CheckGameOver(State state)
    {
        if (state.ClearedAllEnemies)
        {
            if (progress == Progress.Challenge0_Active)
            {
                SetText("NOICE!\n\nThe Levels game mode requires you to destroy all targets to win.");
                progress = Progress.Challenge0_Success;
            }
            else if (progress == Progress.Challenge1_Active)
            {
                SetText("Brilliant! Combos keep you healthy in Levels mode, and earn bonus points in Score Attack.");
                progress = Progress.Challenge1_Success;
            }
            else if (progress == Progress.Challenge2_Active)
            {
                SetText("Too easy, right?");
                progress = Progress.Challenge2_Success;
            }
            else if (progress == Progress.Challenge3_Active)
            {
                bool singleCombo = state.ActiveOrPreviousCombo?.TotalNumGroups >= 3;
                if (singleCombo)
                {
                    SetText("Flawless victory! Try out Puzzle mode to train your combo-vision.");
                    progress = Progress.Challenge3_Success;
                }
                else
                {
                    SetText("Good, but you can do better. Destroy all 3 colors with a single combo."
                        + " Hint: It is possible to do this without using rotation.");
                    progress = Progress.Challenge3_Fail;
                }
            }
        }
        else if (state.IsGameOver)
        {
            if (progress == Progress.Challenge0_Active)
            {
                SetText("Try again. Drop the yellow pieces onto the yellow target, and the blue pieces onto the blue target.");
                progress = Progress.Challenge0_Fail;
            }
            else if (progress == Progress.Challenge1_Active)
            {
                SetText("Try again. Use rotation to align both halves of each piece you drop.");
                progress = Progress.Challenge1_Fail;
            }
            else if (progress == Progress.Challenge2_Active)
            {
                SetText("Try again. Hold the drop button to Burst the [icon] blank piece.");
                progress = Progress.Challenge2_Fail;
            }
            else if (progress == Progress.Challenge3_Active)
            {
                // TODO check if they Bursted or not. If not, remind them to hold the drop button.
                SetText("Try again. Make sure each color matches the column it is dropped into.");
                progress = Progress.Challenge3_Fail;
            }
        }
    }

    readonly struct Challenge
    {
        public readonly Progress Id;
        public readonly CannedSpawnDeck SpawnDeck;
        public readonly State State;

        public Challenge(Progress id, CannedSpawnDeck spawnDeck, State state)
        {
            this.Id = id;
            this.SpawnDeck = spawnDeck;
            this.State = state;
        }
    }

    class CannedSpawnDeck : ISpawnDeck
    {
        private readonly SpawnItem[] items;
        private int offset = 0;

        public CannedSpawnDeck(params SpawnItem[] items)
        {
            this.items = items;
        }

        public int PeekLimit => items.Length - offset;

        public SpawnItem Peek(int index)
        {
            return items[offset + index];
        }

        public SpawnItem Pop()
        {
            var item = items[offset];
            offset++;
            return item;
        }
    }

    class Logic : GameViewerControl.LogicBase
    {
        private readonly TutorialControl tutorialControl;

        public Logic(DotnetTicker ticker, TutorialControl parent) : base(ticker)
        {
            this.tutorialControl = parent;
        }

        public override void HandleInput()
        {
            // Using the ticker's DelayStart seems good enough for now
            base.HandleInput();
        }

        public override void CheckGameOver()
        {
            tutorialControl.CheckGameOver(ticker.state);
        }
    }

    enum Progress
    {
        Challenge0_Active,
        Challenge0_Success,
        Challenge0_Fail,
        Challenge1_Active,
        Challenge1_Success,
        Challenge1_Fail,
        Challenge2_Active,
        Challenge2_Success,
        Challenge2_Fail,
        Challenge3_Active,
        Challenge3_Success,
        Challenge3_Fail,
        Farewell,
    }
}