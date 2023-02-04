using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FF2.Core.Viewmodels;

namespace FF2.Core
{
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
        private int hitPoints;

        public int HitPoints => hitPoints * 4; // current UI draws quarter hearts...

        public NewHealth(IScheduler scheduler)
        {
            countdown = scheduler.CreateWaitingAppointment(maxCountdownMillis);
            hitPoints = MaxHP;
            penaltySchedule = PenaltySchedule.Create();
            penaltyProgress = new int[20];
            penaltyProgress.AsSpan().Fill(0);
        }

        public bool GameOver => hitPoints <= 0 || countdown.HasArrived();

        public void Elapse(IScheduler scheduler) { }

        public StateEvent? AddPenalty(SpawnItem penalty, StateEvent.Factory eventFactory, IScheduler scheduler)
        {
            return null;
        }

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

            int agc = combo.PermissiveCombo.AdjustedGroupCount;
            for (int i = 0; i < penaltyProgress.Length; i++)
            {
                int rank = penaltyProgress[i];
                if (agc >= rank)
                {
                    penaltyProgress[i] = 0;
                }
            }
        }

        public void OnComboUpdated(ComboInfo previous, ComboInfo current, IScheduler scheduler)
        {
        }

        int ICountdownViewmodel.MaxMillis => maxCountdownMillis;

        int ICountdownViewmodel.CurrentMillis => countdown.MillisRemaining();

        int ISlidingPenaltyViewmodel.NumSlots => penaltyProgress.Length;

        PenaltyItem ISlidingPenaltyViewmodel.GetPenalty(int index)
        {
            var rank = penaltyProgress[index];
            if (rank > 0)
            {
                return new PenaltyItem(index + 1, rank, false);
            }
            return PenaltyItem.None;
        }

        public bool GetHealth(out HealthStatus status)
        {
            status = new HealthStatus(hitPoints, MaxHP);
            return true;
        }
    }

    sealed class HealthManager : IStateHook
    {
        readonly struct PenaltyStatus
        {
            public readonly int AGCToClear;
            public readonly int HeightPerPenalty;
            public readonly int PenaltyCount;

            public PenaltyStatus(int agc, int height, int count)
            {
                this.AGCToClear = agc;
                this.HeightPerPenalty = height;
                this.PenaltyCount = count;
            }

            public PenaltyStatus MaybeDecrement(int agc)
            {
                if (agc >= AGCToClear && PenaltyCount > 0)
                {
                    return new PenaltyStatus(AGCToClear, HeightPerPenalty, PenaltyCount - 1);
                }
                return this;
            }

            public PenaltyStatus Increment()
            {
                return new PenaltyStatus(AGCToClear, HeightPerPenalty, PenaltyCount + 1);
            }
        }

        readonly struct Attack
        {
            public readonly bool LeftSide;
            public readonly int StartingHeight;
            public readonly Appointment HitTime;

            public Attack(bool leftSide, int startingHeight, Appointment hitTime)
            {
                this.LeftSide = leftSide;
                this.StartingHeight = startingHeight;
                this.HitTime = hitTime;
            }
        }

        const int GridHeight = 20;

        private int Health = 20;
        private PenaltyStatus leftPenalty;
        private PenaltyStatus rightPenalty;
        private Attack attack;
        private readonly PayoutTable healthPayoutTable;
        private readonly ISpawnDeck spawnDeck;
        private RestoreHealthAnimation restoreHealthAnimation; // For display only! Do not use in logic!
        private StateEvent? penaltyCreatedEvent = null;
        public readonly Viewmodels.IPenaltyViewmodel LEFT_VM;
        public readonly Viewmodels.IPenaltyViewmodel RIGHT_VM;

        public RestoreHealthAnimation RestoreHealthAnimation => restoreHealthAnimation;

        public int CurrentHealth => Health;

        static class NextPenalty
        {
            public const int CostLimit = SpawnCost * 25;
            public const int SpawnCost = 1;
            public const int DestructionCost = SpawnCost * 2;
        }

        int NextPenaltyCountdown = NextPenalty.CostLimit;

        public HealthManager(ISpawnDeck spawnDeck)
        {
            leftPenalty = new PenaltyStatus(3, 2, 1);
            rightPenalty = new PenaltyStatus(6, 2, 2);
            attack = new Attack(true, 0, Appointment.Frame0);
            healthPayoutTable = PayoutTable.DefaultHealthPayoutTable;
            this.spawnDeck = spawnDeck;
            restoreHealthAnimation = new RestoreHealthAnimation(0, Appointment.Frame0);
            this.LEFT_VM = new PenaltyViewmodel(this, true);
            this.RIGHT_VM = new PenaltyViewmodel(this, false);
        }

        public bool GameOver => Health <= 0;

        public void Elapse(IScheduler scheduler)
        {
            if (attack.HitTime.IsFrame0)
            {
                StartNewAttack(scheduler, leftSide: true);
            }
            else if (attack.HitTime.HasArrived())
            {
                Health -= 4;
                StartNewAttack(scheduler, !attack.LeftSide);
            }
        }

        private void StartNewAttack(IScheduler scheduler, bool leftSide)
        {
            PenaltyStatus ps = leftSide ? leftPenalty : rightPenalty;
            int startingHeight = ps.PenaltyCount * ps.HeightPerPenalty;
            int remain = Math.Max(1, GridHeight - startingHeight);
            attack = new Attack(leftSide, startingHeight, scheduler.CreateWaitingAppointment(remain * 400));
        }

        private (Appointment, int size)? leftSidePenaltyRemoval = null;
        private (Appointment, int size)? rightSidePenaltyRemoval = null;

        public void OnComboUpdated(ComboInfo previous, ComboInfo currentCombo, IScheduler scheduler)
        {
            if (previous.TotalNumGroups < 1)
            {
                leftSidePenaltyRemoval = null;
                rightSidePenaltyRemoval = null;
            }

            // The animation to remove a penalty will run concurrently with the destruction animation.
            const int millis = Constants.DestructionMillis;

            int agc = currentCombo.PermissiveCombo.AdjustedGroupCount;
            if (leftSidePenaltyRemoval == null && agc >= leftPenalty.AGCToClear)
            {
                leftPenalty = leftPenalty.MaybeDecrement(agc);
                leftSidePenaltyRemoval = (scheduler.CreateAnimation(millis), leftPenalty.HeightPerPenalty);
            }
            if (rightSidePenaltyRemoval == null && agc >= rightPenalty.AGCToClear)
            {
                rightPenalty = rightPenalty.MaybeDecrement(agc);
                rightSidePenaltyRemoval = (scheduler.CreateAnimation(millis), rightPenalty.HeightPerPenalty);
            }
        }

        public void OnComboCompleted(ComboInfo combo, IScheduler scheduler)
        {
            if (combo.NumEnemiesDestroyed > 0)
            {
                MaybeAddPenalty(NextPenalty.DestructionCost);
            }

            // To ensure a finite game (as long as enemy count never increases), we use the Strict Combo
            // to get the payout. This means that you cannot gain health without destroying enemies.
            int gainedHealth = combo.NumEnemiesDestroyed + healthPayoutTable.GetPayout(combo.StrictCombo.AdjustedGroupCount);
            Health += gainedHealth;
            restoreHealthAnimation = new(gainedHealth, scheduler.CreateAppointment(gainedHealth * 200));
        }

        public void OnCatalystSpawned(SpawnItem catalyst)
        {
            MaybeAddPenalty(NextPenalty.SpawnCost);
        }

        private void MaybeAddPenalty(int amount)
        {
            NextPenaltyCountdown -= amount;
            while (NextPenaltyCountdown <= 0)
            {
                spawnDeck.AddPenalty(SpawnItem.PENALTY);
                NextPenaltyCountdown += NextPenalty.CostLimit;
            }
        }

        bool addToLeft = true;
        public StateEvent? AddPenalty(SpawnItem penalty, StateEvent.Factory factory, IScheduler scheduler)
        {
            var payload = new PenaltyAddedInfo(addToLeft, 2);

            if (addToLeft)
            {
                leftPenalty = leftPenalty.Increment();
            }
            else
            {
                rightPenalty = rightPenalty.Increment();
            }
            addToLeft = !addToLeft;

            penaltyCreatedEvent = factory.PenaltyAdded(payload, scheduler.CreateAppointment(1000));
            return penaltyCreatedEvent;
        }

        sealed class PenaltyViewmodel : Viewmodels.IPenaltyViewmodel
        {
            private readonly HealthManager parent;
            private readonly bool leftSide;

            public PenaltyViewmodel(HealthManager parent, bool leftSide)
            {
                this.parent = parent;
                this.leftSide = leftSide;
            }

            public int Height => GridHeight;

            public bool LeftSide => leftSide;

            public (int startingHeight, float progress)? CurrentAttack()
            {
                var attack = parent.attack;
                if (attack.LeftSide == this.leftSide)
                {
                    return (attack.StartingHeight, attack.HitTime.Progress());
                }
                return null;
            }

            public void GetPenalties(Span<Viewmodels.PenaltyItem> buffer, out float destructionProgress)
            {
                var ps = leftSide ? parent.leftPenalty : parent.rightPenalty;
                var removal = leftSide ? parent.leftSidePenaltyRemoval : parent.rightSidePenaltyRemoval;

                buffer.Slice(0, GridHeight).Fill(Viewmodels.PenaltyItem.None);

                int index = 0;

                if (removal.HasValue && !removal.Value.Item1.HasArrived())
                {
                    destructionProgress = removal.Value.Item1.Progress();

                    var size = removal.Value.size;
                    buffer.Slice(0, size).Fill(new Viewmodels.PenaltyItem(999, size, true));
                    index += size;
                }
                else
                {
                    destructionProgress = -1f;
                }

                for (int penaltyNum = 0; penaltyNum < ps.PenaltyCount && index < GridHeight; penaltyNum++)
                {
                    for (int j = 0; j < ps.HeightPerPenalty && index < GridHeight; j++)
                    {
                        buffer[index] = new Viewmodels.PenaltyItem(penaltyNum + 1, ps.HeightPerPenalty, false);
                        index++;
                    }
                }
            }

            public (int size, float progress)? PenaltyCreationAnimation()
            {
                if (!parent.penaltyCreatedEvent.HasValue)
                {
                    return null;
                }
                var evnt = parent.penaltyCreatedEvent.Value;
                if (evnt.Completion.HasArrived())
                {
                    return null;
                }

                var payload = evnt.PenaltyAddedPayload();
                if (payload.LeftSide == this.leftSide)
                {
                    return (payload.Height, evnt.Completion.Progress());
                }
                return null;
            }
        }
    }

    // TODO do not check in. The viewmodel can do this itself if it has the scheduler available
    // Aha - and animation appointments do not need millisecond-perfection!
    // Also - this animation is not super obvious... Perhaps each quarter-heart should shoot in from
    // the side?
    public readonly struct RestoreHealthAnimation
    {
        public readonly int HealthGained;
        public readonly Appointment Appointment;

        public RestoreHealthAnimation(int healthGained, Appointment appointment)
        {
            this.HealthGained = healthGained;
            this.Appointment = appointment;
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
