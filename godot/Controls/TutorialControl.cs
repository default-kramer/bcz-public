using FF2.Core;
using Godot;
using System;

#nullable enable

public class TutorialControl : Control
{
    static readonly FF2.Core.Color yellow = FF2.Core.Color.Yellow;
    static readonly FF2.Core.Color blue = FF2.Core.Color.Blue;
    static readonly FF2.Core.Color red = FF2.Core.Color.Red;

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
        if (progress == Progress.Challenge0_Success)
        {
            StartChallenge(Challenge1());
        }
        else if (progress == Progress.Challenge1_Success)
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
        grid.Set(new Loc(1, 2), Occupant.MakeEnemy(yellow));
        grid.Set(new Loc(6, 2), Occupant.MakeEnemy(blue));

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
        grid.Set(new Loc(4, 0), Occupant.MakeEnemy(red));
        grid.Set(new Loc(4, 1), Occupant.MakeEnemy(red));
        grid.Set(new Loc(4, 2), Occupant.MakeEnemy(red));
        grid.Set(new Loc(6, 0), Occupant.MakeEnemy(yellow));
        grid.Set(new Loc(6, 1), Occupant.MakeEnemy(yellow));
        grid.Set(new Loc(6, 2), Occupant.MakeEnemy(yellow));
        grid.Set(new Loc(5, 2), Occupant.MakeEnemy(blue));
        grid.Set(new Loc(5, 3), Occupant.MakeEnemy(blue));

        return MakeChallenge(Progress.Challenge1_Active, grid, spawns1);
    }

    static readonly SpawnItem[] spawns1 =
    {
        SpawnItem.MakeCatalystPair(blue, yellow),
        SpawnItem.MakeCatalystPair(blue, red),
    };

    private Challenge MakeChallenge(Progress id, Grid grid, SpawnItem[] spawns)
    {
        var deck = new CannedSpawnDeck(spawns);
        var state = State.CreateWithInfiniteHealth(grid, deck);
        var ticker = new DotnetTicker(state, NullReplayCollector.Instance);
        members.GameViewerControl.SetLogic(new Logic(ticker, this));
        return new Challenge(id, deck, state);
    }

    void CheckGameOver(State state)
    {
        if (state.ClearedAllEnemies)
        {
            if (challenge.Id == Progress.Challenge0_Active)
            {
                var lbl = members.RichTextLabel;
                lbl.Clear();
                lbl.AddText("NOICE!\n\nThe 'Levels' game mode requires you to destroy all targets to win.");
                progress = Progress.Challenge0_Success;
            }
            else if (challenge.Id == Progress.Challenge1_Active)
            {
                var lbl = members.RichTextLabel;
                lbl.Clear();
                lbl.AddText("Brilliant! Todo on to the next one...");
                progress = Progress.Challenge1_Success;
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
            // May want to stop the player early??
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
    }
}
