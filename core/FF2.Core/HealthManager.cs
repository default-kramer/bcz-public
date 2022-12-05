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

    class HealthManager : IStateHook, Viewmodels.IHealthGridViewmodel
    {
        const int PenaltyAreaStartRow = 0;
        const int PenaltyAreaEndRow = 4;
        // Rows 5-6 will never have penalties or health.
        // So even when penalties are maxed, the attack will have to traverse these rows.
        const int HealthStartRow = 7;
        const int HealthEndRow = 14;
        const int PartialHealthRow = 15;

        const int W = 4;
        const int H = PartialHealthRow + 1;

        const int AttackStartRow = 1; // If no penalties, the Y coordinate where the attack starts
        const int AttackEndRow = HealthStartRow; // The Y coordinate where each attack ends

        const int HealthPerRow = 2;
        const int MaxPenaltiesPerColumn = PenaltyAreaEndRow - PenaltyAreaStartRow + 1;
        const int MaxHealth = (HealthEndRow - HealthStartRow + 1) * HealthPerRow;
        const int MaxPartialHealth = HealthPerRow * 4; // 4 fragments/heart

        readonly struct AttackShape
        {
            public readonly Loc HealthStartLoc;
            public readonly int AttackColumn;

            public AttackShape(Loc a, int b) { this.HealthStartLoc = a; this.AttackColumn = b; }
        }

        private static readonly AttackShape[] Shapes = new[]
        {
            new AttackShape(new Loc(1, HealthStartRow), 0),
            new AttackShape(new Loc(3, HealthStartRow), 2),
            new AttackShape(new Loc(0, HealthStartRow), 1),
            new AttackShape(new Loc(2, HealthStartRow), 3),
        };

        private int shapeIndex = 0;

        // On the grid, the attack will be placed at the target loc immediately.
        // Animation will make it slide up into that loc as the appointment arrives.
        private (Loc target, Appointment arrival) CurrentAttack;

        private int Health;
        private int PartialHealth;
        private readonly int[] penaltyLevels = new int[W];
        private readonly int[] penaltyCounts = new int[W];
        private readonly PayoutTable healthPayoutTable;
        private readonly HealthGrid healthGrid;
        private int penaltyIndex = 0;
        private Appointment penaltyAppointment;
        private (int numRows, Appointment appt)? earnedHealthDrop;
        private Appointment? lostHealthDrop;
        private (int columnMask, Appointment)? destoyedPenalties;
        private (int columnMask, Appointment)? fallingPenalties;

        public HealthManager(PayoutTable healthPayoutTable, IScheduler scheduler)
        {
            this.healthPayoutTable = healthPayoutTable;

            penaltyLevels[0] = 3;
            penaltyLevels[1] = 4;
            penaltyLevels[2] = 6;
            penaltyLevels[3] = 8;
            penaltyCounts.AsSpan().Fill(0);

            CurrentAttack = (new Loc(Shapes[shapeIndex].AttackColumn, AttackStartRow), scheduler.CreateWaitingAppointment(AttackMillisPerCell));

            Health = MaxHealth;
            PartialHealth = 0;

            this.healthGrid = new HealthGrid(this);
            healthGrid.Redraw();

            penaltyAppointment = scheduler.CreateAppointment(MillisPerPenalty);
        }

        const int AttackMillisPerCell = 1000; // millis for the attack to travel 1 cell
        const int MillisPerPenalty = 5000; // millis between penalties

        public IReadOnlyGrid Grid => healthGrid;

        public void OnComboCompleted(ComboInfo combo, IScheduler scheduler)
        {
            RestoreHealth(combo.ComboToReward, scheduler);
            RemovePenalties(combo.ComboToReward.AdjustedGroupCount, scheduler);
            healthGrid.Redraw();
        }

        private void RestoreHealth(Combo combo, IScheduler scheduler)
        {
            int payout = healthPayoutTable.GetPayout(combo.AdjustedGroupCount);
            PartialHealth = PartialHealth + payout;
            int newRows = 0;
            while (PartialHealth >= MaxPartialHealth)
            {
                newRows++;
                PartialHealth -= MaxPartialHealth;
            }

            if (newRows > 0)
            {
                Health += newRows * HealthPerRow;
                this.earnedHealthDrop = (newRows, scheduler.CreateAppointment(800));
            }

            if (Health >= MaxHealth)
            {
                PartialHealth = 0; // No room to accumulate lost health while at full health
            }
        }

        private void RemovePenalties(int AGC, IScheduler scheduler)
        {
            int mask = 0;
            for (int x = 0; x < W; x++)
            {
                if (penaltyLevels[x] <= AGC)
                {
                    int count = penaltyCounts[x];
                    if (count > 0)
                    {
                        penaltyCounts[x]--;
                        mask = Mask.Set(mask, x);
                    }
                }
            }

            if (mask != 0)
            {
                destoyedPenalties = (mask, scheduler.CreateAppointment(500));
            }
        }

        public void Elapse(IScheduler scheduler)
        {
            if (earnedHealthDrop.HasValue && earnedHealthDrop.Value.appt.HasArrived())
            {
                earnedHealthDrop = null;
            }
            if (lostHealthDrop.HasValue && lostHealthDrop.Value.HasArrived())
            {
                lostHealthDrop = null;
            }
            if (fallingPenalties.HasValue && fallingPenalties.Value.Item2.HasArrived())
            {
                fallingPenalties = null;
            }

            CompletePenaltyDestruction(scheduler);
            UpdateAttack(scheduler);
            UpdatePenalties(scheduler);
            healthGrid.Redraw();
        }

        private void CompletePenaltyDestruction(IScheduler scheduler)
        {
            if (!destoyedPenalties.HasValue)
            {
                return;
            }
            var (mask, appt) = destoyedPenalties.Value;
            if (!appt.HasArrived())
            {
                return;
            }

            destoyedPenalties = null;
            fallingPenalties = (mask, scheduler.CreateAppointment(500));
        }

        private int NextShapeIndex => (shapeIndex + 1) % Shapes.Length;

        private static Loc GetAttackStart(int shapeIndex, ReadOnlySpan<int> penaltyCounts)
        {
            var shape = Shapes[shapeIndex];
            return new Loc(shape.AttackColumn, penaltyCounts[shape.AttackColumn] + AttackStartRow);
        }

        private void UpdateAttack(IScheduler scheduler)
        {
            var (target, appt) = CurrentAttack;
            if (appt.HasArrived())
            {
                if (target.Y < AttackEndRow)
                {
                    CurrentAttack = (target.Add(0, 1), scheduler.CreateWaitingAppointment(AttackMillisPerCell));
                }
                else
                {
                    Health--;
                    if (Health % HealthPerRow == 0)
                    {
                        lostHealthDrop = scheduler.CreateAppointment(400);
                    }
                    shapeIndex = NextShapeIndex;
                    target = GetAttackStart(shapeIndex, penaltyCounts);
                    CurrentAttack = (target, scheduler.CreateWaitingAppointment(AttackMillisPerCell));
                }
            }
        }

        // TODO this should get pulled out into a separate class
        private static readonly Penalty[] penaltySchedule = new Penalty[]
        {
            new Penalty(PenaltyKind.Levelled, 0),
            new Penalty(PenaltyKind.Levelled, 1),
            new Penalty(PenaltyKind.Levelled, 2),
            new Penalty(PenaltyKind.Levelled, 3),
        };

        private void UpdatePenalties(IScheduler scheduler)
        {
            if (penaltyAppointment.HasArrived())
            {
                AddPenalty(penaltySchedule[penaltyIndex].Level);
                penaltyIndex = (penaltyIndex + 1) % penaltySchedule.Length;
                penaltyAppointment = scheduler.CreateAppointment(MillisPerPenalty);
            }
        }

        private void AddPenalty(int index)
        {
            int next = penaltyCounts[index] + 1;
            if (next <= MaxPenaltiesPerColumn)
            {
                penaltyCounts[index] = next;
            }
        }

        public bool GameOver => Health <= 0;

        public float LastGaspProgress()
        {
            if (Health > 2)
            {
                return 0f;
            }
            if (Health <= 0)
            {
                return 1f;
            }

            int cellsUntilDeath = 1 + AttackEndRow - CurrentAttack.target.Y;
            if (Health > 1)
            {
                var next = GetAttackStart(NextShapeIndex, penaltyCounts);
                cellsUntilDeath += 1 + AttackEndRow - next.Y;
            }

            var millisRemaining = cellsUntilDeath * AttackMillisPerCell - CurrentAttack.arrival.Progress() * AttackMillisPerCell;
            const float WarningRange = 5000f; // 5 seconds of warning
            return (WarningRange - millisRemaining) / WarningRange;
        }

        public float DestructionProgress(Loc loc)
        {
            // Penalties are always destroyed from the bottom row.
            // Each column never loses more than one penalty per combo.
            if (loc.Y == 0 && destoyedPenalties.HasValue)
            {
                var (mask, appt) = destoyedPenalties.Value;
                if (Mask.Test(mask, loc.X))
                {
                    return appt.Progress();
                }
            }

            return 0;
        }

        public float GetAdder(Loc loc)
        {
            var (attackTarget, attackProgress) = CurrentAttack;
            if (loc == attackTarget)
            {
                return attackProgress.Progress() - 1;
            }

            var occ = healthGrid.Get(loc);

            if (occ == HealthOccupants.Heart)
            {
                float adder = 0; // adds up "drop due to earned health" and "drop due to lost health"

                if (lostHealthDrop.HasValue)
                {
                    adder = 1 - lostHealthDrop.Value.Progress();
                }

                if (earnedHealthDrop.HasValue)
                {
                    var (numFallingRows, appt) = earnedHealthDrop.Value;
                    int totalHealthRows = (Health + 1) / HealthPerRow;
                    int earnedRowStart = HealthStartRow + totalHealthRows - numFallingRows;
                    if (loc.Y >= earnedRowStart)
                    {
                        var distance = H - numFallingRows - loc.Y;
                        adder += distance * (1 - appt.Progress());
                    }
                }

                return adder;
            }

            if (occ == HealthOccupants.Penalty && fallingPenalties.HasValue)
            {
                var (mask, appt) = fallingPenalties.Value;
                if (Mask.Test(mask, loc.X))
                {
                    return 1 - appt.Progress();
                }
            }

            return 0f;
        }

        public void AddPenalty(SpawnItem penalty)
        {
            throw new NotImplementedException();
        }

        class HealthGrid : GridBase
        {
            private readonly HealthManager manager;

            public HealthGrid(HealthManager manager) : base(W, H)
            {
                this.manager = manager;
            }

            public void Redraw()
            {
                this.Clear();

                // Place penalties
                int mask = 0;
                if (manager.destoyedPenalties.HasValue)
                {
                    mask = manager.destoyedPenalties.Value.columnMask;
                }
                for (int x = 0; x < W; x++)
                {
                    int yLimit = manager.penaltyCounts[x];
                    if (Mask.Test(mask, x))
                    {
                        // While destruction is in progress, we draw one extra penalty
                        // so that we can animate its destruction.
                        yLimit++;
                    }
                    for (int y = 0; y < yLimit; y++)
                    {
                        Put(new Loc(x, y), HealthOccupants.Penalty);
                    }
                }

                // Place hearts
                var shape = Shapes[manager.shapeIndex];
                var loc = shape.HealthStartLoc;
                int partialHealthOffset = 0;
                if (shape.AttackColumn > 1)
                {
                    // The health is gone, but we keep showing the heart until the row is destroyed:
                    Put(loc.Add(-2, 0), HealthOccupants.Heart);
                    // And show the attack that already landed
                    Put(new Loc(shape.AttackColumn - 2, HealthStartRow), HealthOccupants.Attack);
                }
                for (int i = 0; i < manager.Health && loc.Y < H; i++)
                {
                    Put(loc, HealthOccupants.Heart);
                    partialHealthOffset = (loc.X + 1) % 2;
                    loc = loc.X switch
                    {
                        0 => loc.Add(2, 0),
                        1 => loc.Add(2, 0),
                        2 => loc.Add(-1, 1),
                        3 => loc.Add(-3, 1),
                        _ => throw new Exception("X should never exceed " + W),
                    };
                }

                // Place partial hearts
                var partialLoc = new Loc(partialHealthOffset, PartialHealthRow);
                Put(partialLoc, GetPartialHeart(manager.PartialHealth));
                Put(partialLoc.Add(2, 0), GetPartialHeart(manager.PartialHealth - 4));

                // Place current attack
                if (manager.Health > 0)
                {
                    Put(manager.CurrentAttack.target, HealthOccupants.Attack);
                }
            }

            private static Occupant GetPartialHeart(int partialHealth)
            {
                if (partialHealth == 1) { return HealthOccupants.Heart25; }
                else if (partialHealth == 2) { return HealthOccupants.Heart50; }
                else if (partialHealth == 3) { return HealthOccupants.Heart75; }
                else if (partialHealth >= 4) { return HealthOccupants.Heart100; }
                return HealthOccupants.Heart0;
            }

            public override IImmutableGrid MakeImmutable()
            {
                throw new NotImplementedException();
            }
        }
    }
}
