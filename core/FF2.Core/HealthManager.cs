using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FF2.Core.Viewmodels;

namespace FF2.Core
{
    sealed class CompositeHook : IStateHook
    {
        private readonly IStateHook a;
        private readonly IStateHook b;
        public CompositeHook(IStateHook a, IStateHook b)
        {
            this.a = a;
            this.b = b;
        }

        public bool GameOver => a.GameOver || b.GameOver;

        public void Elapse(IScheduler scheduler)
        {
            a.Elapse(scheduler);
            b.Elapse(scheduler);
        }

        public void OnCatalystSpawned(SpawnItem catalyst)
        {
            a.OnCatalystSpawned(catalyst);
            b.OnCatalystSpawned(catalyst);
        }

        public void OnComboCompleted(ComboInfo combo, IScheduler scheduler)
        {
            a.OnComboCompleted(combo, scheduler);
            b.OnComboCompleted(combo, scheduler);
        }

        public void OnComboUpdated(ComboInfo previous, ComboInfo current, IScheduler scheduler)
        {
            a.OnComboUpdated(previous, current, scheduler);
            b.OnComboUpdated(previous, current, scheduler);
        }
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

            if (attackCount > 0)
            {
                switches.AsSpan().Fill(resetTo);
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

    interface IDumpCallback // TODO figure out what to do
    {
        void Dump(int numAttacks);
    }

    sealed class SimulatedAttacker : IStateHook
    {
        readonly struct Attack
        {
            public readonly int Rank;
            public readonly int Frozen;

            public Attack(int rank, int frozen)
            {
                this.Rank = rank;
                this.Frozen = frozen;
            }

            public bool IsSomething => Rank > 0;
            public static readonly Attack Nothing = default(Attack);

            public Attack Freeze(int amount)
            {
                int frozen = this.Frozen + amount;
                return new Attack(Rank, Math.Min(Rank, frozen));
            }
        }

        /// <summary>
        /// Mutates the contents of the given array, advancing each attack one step.
        /// Returns the Rank of the attack that just landed, or zero.
        /// </summary>
        private static int Advance(Attack[] Attacks, ref int distance)
        {
            int rank = 0;

            for (int x = 0; x < Attacks.Length; x++)
            {
                var attack = Attacks[x];
                if (attack.IsSomething)
                {
                    distance = Math.Min(x, distance);
                    if (attack.Frozen > 0)
                    {
                        Attacks[x] = attack.Freeze(-1);
                        return rank;
                    }
                    else if (x > 0)
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
        public readonly IReadOnlyGridSlim GRID;

        public SimulatedAttacker(Switches switches, IDumpCallback dumper)
        {
            this.switches = switches;
            this.dumper = dumper;
            Attacks[4] = new Attack(2, 0);
            Attacks[10] = new Attack(3, 0);
            nextAttack = (3, 5);
            GRID = new GridRep(this);
        }

        public bool GameOver => false;

        public void Elapse(IScheduler scheduler) { }

        public void OnCatalystSpawned(SpawnItem catalyst)
        {
            int distance = 999;
            int attackRank = Advance(Attacks, ref distance);
            Console.WriteLine($"Attack is now {distance} away");
            if (attackRank > 0)
            {
                Console.WriteLine("Attack landed! " + attackRank);
                int numAttacks = switches.OnEnemyCombo(attackRank, attackBuffer);
                WriteSwitches();
                if (numAttacks > 0)
                {
                    Console.WriteLine("DUMPING!");
                    dumper.Dump(numAttacks);
                }
            }

            var (delay, rank) = nextAttack;
            if (delay <= 0)
            {
                Attacks[Width - 1] = new Attack(rank, 0);
                if (false && rank < 7)
                {
                    nextAttack = (rank * 2, rank);
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
            WriteSwitches();

            if (numAttacks > 0)
            {
                for (int x = 0; x < Width; x++)
                {
                    var attack = Attacks[x];
                    if (attack.IsSomething)
                    {
                        Attacks[x] = attack.Freeze(numAttacks);
                    }
                }
            }
        }

        private void WriteSwitches()
        {
            Console.Write("Switch status: ");
            for (int i = Switches.MinRank; i <= Switches.MaxRank; i++)
            {
                Console.Write($"{i}{switches[i].ToString().Substring(0, 1)} ");
            }
            Console.WriteLine();
        }

        public void OnComboUpdated(ComboInfo previous, ComboInfo current, IScheduler scheduler) { }

        sealed class GridRep : IReadOnlyGridSlim
        {
            private readonly SimulatedAttacker data;
            const int Width = SimulatedAttacker.Width;
            const int Height = Switches.MaxRank;

            public GridRep(SimulatedAttacker data)
            {
                this.data = data;
            }

            public GridSize Size => new GridSize(Width, Height);

            public Occupant Get(Loc loc)
            {
                var attack = data.Attacks[loc.X];
                if (attack.Rank > loc.Y)
                {
                    return Occupant.IndestructibleEnemy;
                }
                return Occupant.None;
            }
        }
    }

    class PenaltySchedule
    {
        readonly struct PenaltyItem
        {
            public readonly int Delay;
            public readonly int Level;
            public readonly int NextPointer;

            public PenaltyItem(int delay, int level, int nextPointer)
            {
                this.Delay = delay;
                this.Level = level;
                this.NextPointer = nextPointer;
            }
        }

        private readonly PenaltyItem[] items;
        private int index;
        private int countdown;

        private PenaltySchedule(PenaltyItem[] items)
        {
            this.items = items;
            this.index = 0;
            this.countdown = items[index].Delay;
        }

        public bool HasCurrentPenalty(out Penalty item)
        {
            if (countdown <= 0)
            {
                item = new Penalty(PenaltyKind.Levelled, items[index].Level);
                return true;
            }
            item = default(Penalty);
            return false;
        }

        public void OnCatalystSpawned()
        {
            if (countdown <= 0)
            {
                index = index + items[index].NextPointer;
                countdown = items[index].Delay;
            }
            else
            {
                countdown--;
            }
        }

        public static PenaltySchedule Create()
        {
            return new PenaltySchedule(array);
        }

        private static readonly PenaltyItem[] array = Build();

        private static PenaltyItem[] Build()
        {
            const int delay = 8;

            var items = new PenaltyItem[8];

            // 2, 3, 4, 5
            for (int i = 0; i < 4; i++)
            {
                items[i] = new PenaltyItem(delay, i + 2, 1);
            }

            // another 2, 3, 4, 5
            for (int i = 4; i < 8; i++)
            {
                items[i] = items[i - 4];
            }

            // loop the last 2 items (4, 5) forever
            items[7] = new PenaltyItem(delay, items[7].Level, -1);

            return items;
        }
    }

    sealed class NewHealth : IStateHook, ICountdownViewmodel, ISlidingPenaltyViewmodel
    {
        private const int maxCountdownMillis = 1000 * 60;
        private const int millisRestoredPerEnemy = 1000 * 5;
        private const int MaxHP = 3;
        private readonly PenaltySchedule penaltySchedule;
        // These ^^ should be parameterized

        private Appointment countdown;
        private readonly int[] penaltyProgress;

        // When a penalty is destroyed, we will save it in this array to support the destruction animation.
        // We say that if the slot's appointment has arrived, there is nothing relevant in this slot.
        // This allows us to avoid doing any cleanup after an animation completes.
        private readonly (int, Appointment)[] destroyedPenalties;

        private int hitPoints;

        public int HitPoints => hitPoints * 4; // current UI draws quarter hearts...

        public NewHealth(IScheduler scheduler)
        {
            countdown = scheduler.CreateWaitingAppointment(maxCountdownMillis);
            hitPoints = MaxHP;
            penaltySchedule = PenaltySchedule.Create();
            penaltyProgress = new int[20];
            penaltyProgress.AsSpan().Fill(0);
            destroyedPenalties = new (int, Appointment)[penaltyProgress.Length];
            destroyedPenalties.AsSpan().Fill((0, Appointment.Frame0));
        }

        public bool GameOver => hitPoints <= 0 || countdown.HasArrived();

        public void Elapse(IScheduler scheduler) { }

        public void OnCatalystSpawned(SpawnItem catalyst)
        {
            if (penaltyProgress[0] > 0)
            {
                hitPoints--;
            }
            for (int i = 0; i < penaltyProgress.Length - 1; i++)
            {
                penaltyProgress[i] = penaltyProgress[i + 1];
            }
            penaltyProgress[penaltyProgress.Length - 1] = 0;

            penaltySchedule.OnCatalystSpawned();
            if (penaltySchedule.HasCurrentPenalty(out var penalty))
            {
                penaltyProgress[penaltyProgress.Length - 1] = penalty.Level;
            }
        }

        public void OnComboCompleted(ComboInfo combo, IScheduler scheduler)
        {
            int millisRemaining = countdown.MillisRemaining();
            millisRemaining += combo.NumEnemiesDestroyed * millisRestoredPerEnemy;
            millisRemaining = Math.Min(millisRemaining, maxCountdownMillis);
            countdown = scheduler.CreateWaitingAppointment(millisRemaining);
        }

        public void OnComboUpdated(ComboInfo previous, ComboInfo current, IScheduler scheduler)
        {
            int agc = current.PermissiveCombo.AdjustedGroupCount;
            for (int i = 0; i < penaltyProgress.Length; i++)
            {
                int rank = penaltyProgress[i];
                if (agc >= rank)
                {
                    destroyedPenalties[i] = (penaltyProgress[i], scheduler.CreateAppointment(350));
                    penaltyProgress[i] = 0;
                }
            }
        }

        int ICountdownViewmodel.MaxMillis => maxCountdownMillis;

        int ICountdownViewmodel.CurrentMillis => countdown.MillisRemaining();

        int ISlidingPenaltyViewmodel.NumSlots => penaltyProgress.Length;

        PenaltyViewmodel ISlidingPenaltyViewmodel.GetPenalty(int index)
        {
            var rank = penaltyProgress[index];
            if (rank > 0)
            {
                return new PenaltyViewmodel(rank, 0f);
            }

            var item = destroyedPenalties[index];
            if (!item.Item2.HasArrived())
            {
                return new PenaltyViewmodel(item.Item1, item.Item2.Progress());
            }

            return PenaltyViewmodel.None;
        }

        public bool GetHealth(out HealthStatus status)
        {
            status = new HealthStatus(hitPoints, MaxHP);
            return true;
        }
    }

    public readonly struct PenaltyAddedInfo
    {
        public readonly bool LeftSide;
        public readonly int Height;

        public PenaltyAddedInfo(bool left, int height)
        {
            this.LeftSide = left;
            this.Height = height;
        }
    }
}
