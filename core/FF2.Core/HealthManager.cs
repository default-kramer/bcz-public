using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    class HealthManager : Viewmodels.IHealthModel
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

        public void OnComboCompleted(ComboInfo combo)
        {
            RestoreHealth(combo.ComboToReward);
            RemovePenalties(combo.ComboToReward.AdjustedGroupCount);
            healthGrid.Redraw();
        }

        private void RestoreHealth(Combo combo)
        {
            int payout = healthPayoutTable.GetPayout(combo.AdjustedGroupCount);
            PartialHealth = PartialHealth + payout;
            while (PartialHealth >= MaxPartialHealth)
            {
                Health += 2;
                PartialHealth -= MaxPartialHealth;
            }

            if (Health >= MaxHealth)
            {
                PartialHealth = 0; // No room to accumulate lost health while at full health
            }
        }

        private void RemovePenalties(int N)
        {
            int index = W - 1;
            while (index >= 0)
            {
                if (penaltyLevels[index] <= N)
                {
                    Remove2(index);
                    return;
                }
                index--;
            }
        }

        private void Remove2(int index)
        {
            int minCount = penaltyCounts[index];

            while (index >= 0)
            {
                int count = penaltyCounts[index];
                if (count > 0 && count >= minCount)
                {
                    penaltyCounts[index] = count - 1;
                }
                index--;
            }
        }

        public void Elapse(IScheduler scheduler)
        {
            UpdateAttack(scheduler);
            UpdatePenalties(scheduler);
            healthGrid.Redraw();
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

        public float GetAdder(Loc loc)
        {
            var (target, appt) = CurrentAttack;
            if (loc == target)
            {
                return appt.Progress() - 1;
            }
            return 0f;
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
                for (int x = 0; x < W; x++)
                {
                    int yLimit = manager.penaltyCounts[x];
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
