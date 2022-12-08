using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    static class Mask
    {
        public static int Set(int mask, int bit)
        {
            return mask | (1 << bit);
        }

        public static bool Test(int mask, int bit)
        {
            return (mask & (1 << bit)) != 0;
        }
    }

    sealed class HealthV2 : IStateHook
    {
        public readonly Viewmodels.IPenaltyViewmodel LEFT_VM;
        public readonly Viewmodels.IPenaltyViewmodel RIGHT_VM;

        sealed class PenaltyViewmodel : Viewmodels.IPenaltyViewmodel
        {
            private readonly HealthV2 parent;
            private readonly bool leftSide;

            public PenaltyViewmodel(HealthV2 parent, bool leftSide)
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

        public RestoreHealthAnimation RestoreHealthAnimation => restoreHealthAnimation;

        public int CurrentHealth => Health;

        public HealthV2(ISpawnDeck spawnDeck)
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

            const int destructionMillis = 550; // TODO should share this value

            int agc = currentCombo.PermissiveCombo.AdjustedGroupCount;
            if (leftSidePenaltyRemoval == null && agc >= leftPenalty.AGCToClear)
            {
                leftPenalty = leftPenalty.MaybeDecrement(agc);
                leftSidePenaltyRemoval = (scheduler.CreateAppointment(destructionMillis), leftPenalty.HeightPerPenalty);
            }
            if (rightSidePenaltyRemoval == null && agc >= rightPenalty.AGCToClear)
            {
                rightPenalty = rightPenalty.MaybeDecrement(agc);
                rightSidePenaltyRemoval = (scheduler.CreateAppointment(destructionMillis), rightPenalty.HeightPerPenalty);
            }
        }

        int comboCount = 0;

        public void OnComboCompleted(ComboInfo combo, IScheduler scheduler)
        {
            comboCount++;
            if (comboCount % 2 == 0)
            {
                spawnDeck.AddPenalty(SpawnItem.PENALTY); // For now, just add penalty every other destruction
            }

            // To ensure a finite game (as long as enemy count never increases), we use the Strict Combo
            // to get the payout. This means that you cannot gain health without destroying enemies.
            int gainedHealth = combo.NumEnemiesDestroyed + healthPayoutTable.GetPayout(combo.StrictCombo.AdjustedGroupCount);
            Health += gainedHealth;
            restoreHealthAnimation = new(gainedHealth, scheduler.CreateAppointment(gainedHealth * 200));
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
