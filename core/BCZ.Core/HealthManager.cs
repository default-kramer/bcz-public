using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BCZ.Core.Viewmodels;

namespace BCZ.Core
{
    sealed class CountdownHook : EmptyStateHook, ICountdownViewmodel
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

        public override bool GameOver => countdown.HasArrived();

        public override void OnComboLikelyCompleted(State state, ComboInfo combo, IScheduler scheduler)
        {
            int millisRemaining = countdown.MillisRemaining();
            millisRemaining += combo.NumEnemiesDestroyed * millisRestoredPerEnemy;
            millisRemaining = Math.Min(millisRemaining, maxCountdownMillis);
            countdown = scheduler.CreateWaitingAppointment(millisRemaining);
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

    sealed class NewHealth : EmptyStateHook, ICountdownViewmodel, ISlidingPenaltyViewmodel
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

        public override bool GameOver => hitPoints <= 0 || countdown.HasArrived();

        public override void OnCatalystSpawned(SpawnItem catalyst)
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

        public override void OnComboLikelyCompleted(State state, ComboInfo combo, IScheduler scheduler)
        {
            int millisRemaining = countdown.MillisRemaining();
            millisRemaining += combo.NumEnemiesDestroyed * millisRestoredPerEnemy;
            millisRemaining = Math.Min(millisRemaining, maxCountdownMillis);
            countdown = scheduler.CreateWaitingAppointment(millisRemaining);
        }

        public override void OnComboUpdated(ComboInfo previous, ComboInfo current, IScheduler scheduler)
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
