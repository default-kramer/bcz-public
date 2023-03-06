using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FF2.Core.Viewmodels;

// Prototyping some PvP ideas here...
//
// I haven't really figured out if I want a Middle switch or not...
// I believe my thinking at the time was that the switches would be shared for both
// players and dumps would generate immediately (insted of putting on a grid).
// By resetting to Middle the player who just attacked would still be defended,
// but could not immediately attack again.
// But shared switches is a flawed design I think; perverse incentives ("you combo first please!")
// seem very hard to avoid in a way that makes any sense to the players.
//
// Instead, what I have now is that each player has their own set of switches.
// A switch is either "defended" (Safe) or "undefended" (Unsafe).
// A combo always puts an attack onto the other player's "attack grid".
// (The attack currently advances per spawn, but I think per millis will solve everything!)
// A combo of rank N also flips all your switches up to rank N to Defended.
// When an attack hits an undefended switch, you get dumped on and ALL your switches
// reset to Defended status, to give you some time to recover.
// I like this because it allows single-player PvP simulation easily.
// It also avoids all the perverse incentives I can think of, thanks mostly to the delay
// between the time an attack is generated and when it lands.
// Now if you and your opponent both have a combo ready:
// * If your switches are Undefended, you want to combo ASAP; waiting is useless.
// * If they are Defended, you would have to waste a lot of time waiting for your opponent's
//   attack to arrive.
//
// == On Freezing ==
// Without freeze, it is confusing when your combo and an attack arrive at the same time.
// Both elements want to flip the switches, what should happen?
// The solution is to generate freeze on all combos (rank > 1).
// This way, an attack can never arrive at the same time as your combo.
//
// TODO - But when attacks are generated "live" rather than per catalyst spent,
// freezing does not really help the player. It would cause attacks to bunch
// together which is harder to defend against.
// Idea: Only apply +1 freeze regardless of combo rank.
// Better Idea??: The attack grid advances per millis, not per spawn.
// * Oh hell yeah, now a new skill is: When your switches are green and an attack
//   is T seconds away, is it worth sitting on your combo waiting for that attack
//   to land so you can immediately counter it? It depends on a lot of factors!
// * Freeze is not needed at all. When an attack hits an undefended switch,
//   your queue no longer shows the next 5 catalysts, it shows a dump indicator.
//   Your next spawn will dump instead.
//
// == Cosmetic Improvement ==
// Only show orange when there is an attack on the grid that will hit, otherwise show gray/neutral
// * Green - defended
// * Gray - undefended but not threatened
// * Orange - undefended and threatened
// And of course, the Orange should flash when the attack is imminent.

namespace FF2.Core
{
    interface IDumpCallback
    {
        // TODO would need to figure out how to specify the ferocity of the dump.
        // Right now we just always dump 1 item per column, which actually seems
        // like it would work well for experienced players.
        void Dump(int numAttacks);
    }

    enum SwitchStatus
    {
        Middle,
        Safe,
        Unsafe,
    }

    sealed class Switches : ISwitchesViewmodel
    {
        public const int MinRank = 2;
        public const int MaxRank = 12;
        public const int ArraySize = MaxRank - MinRank + 1; // +1 for inclusive

        private readonly SwitchStatus[] switches = new SwitchStatus[ArraySize];

        public SwitchStatus this[int rank] => switches[rank - MinRank];

        /// <summary>
        /// Flips switches as needed. Populates the <paramref name="attackBuffer"/>
        /// (which must have length <see cref="ArraySize"/>) with true values for each
        /// rank for which an attack was generated. Returns count of attacks.
        /// </summary>
        public int OnFriendlyComboSinglePlayer(int rank, bool[] attackBuffer)
        {
            // Against a simulated opponent, outgoing attacks really aren't that potent.
            // So we keep the switch green even after the player attacks.
            return Process(rank, attackBuffer, SwitchStatus.Safe, SwitchStatus.Safe);
        }

        public int OnFriendlyComboPvP(int rank, bool[] attackBuffer)
        {
            return Process(rank, attackBuffer, SwitchStatus.Safe, SwitchStatus.Middle);
        }

        /// <summary>
        /// Same as <see cref="OnFriendlyComboSinglePlayer"/>
        /// </summary>
        public int OnEnemyCombo(int rank, bool[] attackBuffer)
        {
            return Process(rank, attackBuffer, SwitchStatus.Unsafe, SwitchStatus.Middle);
        }

        public void OnDump()
        {
            switches.AsSpan().Fill(SwitchStatus.Safe);
        }

        private int Process(int rank, bool[] attackBuffer, SwitchStatus attackIf, SwitchStatus resetTo)
        {
            int limit = rank - MinRank;
            int attackCount = 0;

            for (int i = 0; i < ArraySize; i++)
            {
                attackBuffer[i] = false;

                if (i <= limit)
                {
                    if (switches[i] == attackIf)
                    {
                        attackBuffer[i] = true;
                        switches[i] = resetTo;
                        attackCount++;
                    }
                    else
                    {
                        switches[i] = attackIf;
                    }
                }
            }

            return attackCount;
        }

        int ISwitchesViewmodel.MinRank => MinRank;

        int ISwitchesViewmodel.MaxRank => MaxRank;

        bool ISwitchesViewmodel.IsGreen(int rank)
        {
            return this[rank] != SwitchStatus.Unsafe;
        }
    }

    sealed class SimulatedAttacker : IStateHook
    {
        readonly struct Attack
        {
            public readonly int Rank;

            public Attack(int rank)
            {
                this.Rank = rank;
            }

            public bool IsSomething => Rank > 0;
            public static readonly Attack Nothing = default(Attack);
        }

        /// <summary>
        /// Mutates the contents of the given array, advancing each attack one step.
        /// Returns the Rank of the attack that just landed, or zero.
        /// </summary>
        private static int Advance(Attack[] Attacks)
        {
            int rank = 0;

            for (int x = 0; x < Attacks.Length; x++)
            {
                var attack = Attacks[x];
                if (attack.IsSomething)
                {
                    if (x > 0)
                    {
                        Attacks[x - 1] = attack;
                        Attacks[x] = Attack.Nothing;
                    }
                    else
                    {
                        Attacks[x] = Attack.Nothing;
                        rank = attack.Rank;
                    }
                }
            }

            return rank;
        }

        const int Width = 12;
        private readonly Attack[] Attacks = new Attack[Width];
        private readonly Switches switches;
        private readonly bool[] attackBuffer = new bool[Switches.ArraySize];
        private readonly IDumpCallback dumper;
        private (int delay, int rank) nextAttack;
        public readonly IAttackGridViewmodel VM;
        private int freeze = 0; // See notes on Freezing above

        public SimulatedAttacker(Switches switches, IDumpCallback dumper)
        {
            this.switches = switches;
            this.dumper = dumper;
            Attacks[4] = new Attack(2);
            Attacks[10] = new Attack(3);
            nextAttack = (3, 5);
            this.VM = new Viewmodel(this);
        }

        public ISwitchesViewmodel SwitchVM => switches;

        public bool GameOver => false;

        public void OnCatalystSpawned(SpawnItem catalyst) { }

        private int lastSpawnCount = -1;
        public void PreSpawn(int spawnCount)
        {
            if (spawnCount == lastSpawnCount) { return; }
            lastSpawnCount = spawnCount;

            if (freeze > 0)
            {
                freeze--;
                return;
            }

            int attackRank = Advance(Attacks);
            if (attackRank > 0)
            {
                int numAttacks = switches.OnEnemyCombo(attackRank, attackBuffer);
                if (numAttacks > 0)
                {
                    switches.OnDump();
                    dumper.Dump(numAttacks);
                }
            }

            var (delay, rank) = nextAttack;
            if (delay <= 0)
            {
                Attacks[Width - 1] = new Attack(rank);
                if (rank < 7)
                {
                    nextAttack = (rank * 2, rank + 1);
                }
                else
                {
                    nextAttack = (10, 4);
                }
            }
            else
            {
                nextAttack = (delay - 1, rank);
            }
        }

        public void OnComboCompleted(ComboInfo combo, IScheduler scheduler)
        {
            int rank = combo.PermissiveCombo.AdjustedGroupCount;
            int numAttacks = switches.OnFriendlyComboSinglePlayer(rank, attackBuffer);

            if (rank > 1)
            {
                var maxRankAttack = Attacks.Max(x => x.Rank);
                freeze += rank;
                freeze = Math.Min(freeze, maxRankAttack + 1);
            }
        }

        public void OnComboUpdated(ComboInfo previous, ComboInfo current, IScheduler scheduler) { }

        sealed class Viewmodel : IAttackGridViewmodel, IReadOnlyGridSlim
        {
            private readonly SimulatedAttacker data;
            const int Width = SimulatedAttacker.Width;
            const int Height = Switches.MaxRank;

            public Viewmodel(SimulatedAttacker data)
            {
                this.data = data;
            }

            public GridSize Size => new GridSize(Width, Height);

            public IReadOnlyGridSlim Grid => this;

            public Occupant Get(Loc loc)
            {
                var attack = data.Attacks[loc.X];
                if (attack.Rank > loc.Y)
                {
                    return Occupant.IndestructibleEnemy;
                }
                return Occupant.None;
            }

            public bool IsFrozen(Loc loc)
            {
                var attack = data.Attacks[loc.X];
                return data.freeze > loc.Y;
            }
        }
    }
}
