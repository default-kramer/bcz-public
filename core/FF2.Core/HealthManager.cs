using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FF2.Core.Viewmodels;

namespace FF2.Core
{
    sealed class BarrierHook : IStateHook
    {
        private readonly Grid grid;

        public BarrierHook(Grid grid)
        {
            this.grid = grid;
        }

        public void OnComboUpdated(ComboInfo previous, ComboInfo current, IScheduler scheduler) { CheckIt(current); }
        public void OnComboCompleted(ComboInfo combo, IScheduler scheduler) { }

        // TODO - I think this needs to determine what the completed combo will be so that it can know
        // which lock will actually be unlocked. Then OnComboUpdated needs to do that as soon as that
        // rank is reached, and then set a flag "AlreadyRewarded=true" which will be set to false OnComboCompleted.
        // Also should probably coordinate the destruction through the State...
        //
        // Where to store ranks needed to unlock each barrier? Probably right here, in this class.
        private void CheckIt(ComboInfo combo)
        {
            if (combo.PermissiveCombo.AdjustedGroupCount > 3)
            {
                for (int y = grid.Height - 1; y >= 0; y--)
                {
                    if (grid.Get(new Loc(0, y)) == Occupant.IndestructibleEnemy)
                    {
                        for (int x = 0; x < grid.Width; x++)
                        {
                            grid.Set(new Loc(x, y), Occupant.None);
                        }
                        return;
                    }
                }
            }
        }

        public bool GameOver => false;
        public void OnCatalystSpawned(SpawnItem catalyst) { }
        public void PreSpawn(int spawnCount) { }
    }

    sealed class CountdownHook : IStateHook, ICountdownViewmodel
    {
        private const int maxCountdownMillis = 1000 * 60;
        private const int millisRestoredPerEnemy = 1000 * 5;

        private Appointment countdown;

        public CountdownHook(IScheduler scheduler)
        {
            countdown= scheduler.CreateWaitingAppointment(maxCountdownMillis);
        }

        int ICountdownViewmodel.MaxMillis => maxCountdownMillis;

        int ICountdownViewmodel.CurrentMillis => countdown.MillisRemaining();

        public bool GameOver => countdown.HasArrived();

        public void OnComboCompleted(ComboInfo combo, IScheduler scheduler)
        {
            int millisRemaining = countdown.MillisRemaining();
            millisRemaining += combo.NumEnemiesDestroyed * millisRestoredPerEnemy;
            millisRemaining = Math.Min(millisRemaining, maxCountdownMillis);
            countdown = scheduler.CreateWaitingAppointment(millisRemaining);
        }

        public void OnCatalystSpawned(SpawnItem catalyst) { }
        public void OnComboUpdated(ComboInfo previous, ComboInfo current, IScheduler scheduler) { }
        public void PreSpawn(int spawnCount) { }
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

        public void PreSpawn(int spawnCount) { }

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
