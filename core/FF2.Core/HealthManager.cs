using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    class HealthManager : Viewmodels.IHealthModel
    {
        const int PenaltyAreaStart = 0;
        const int PenaltyAreaEnd = 4;
        // 5-6 is a "DMZ" of sorts, which each attack always has to cross regardless of penalties
        const int HealthStart = 7;
        const int HealthEnd = 14;
        const int PartialHealthRow = 15;

        const int W = 4;
        const int H = PartialHealthRow + 1;

        const int HealthPerRow = 2;
        const int MaxPenaltiesPerColumn = PenaltyAreaEnd - PenaltyAreaStart + 1;
        const int MaxHealth = (HealthEnd - HealthStart + 1) * HealthPerRow;
        const int MaxPartialHealth = 8; // 2 hearts * 4 fragments/heart
        const int AttackPerCell = 1000 * 1000;
        const float AttackPerBlockFloat = AttackPerCell;

        // Could be a variable:
        const int AttackPerMillis = AttackPerCell / 1000;

        readonly struct AttackShape
        {
            public readonly Loc HealthStartLoc;
            public readonly int AttackColumn;

            public AttackShape(Loc a, int b) { this.HealthStartLoc = a; this.AttackColumn = b; }
        }

        private static readonly AttackShape[] Shapes = new[]
        {
            new AttackShape(new Loc(1, HealthStart), 0),
            new AttackShape(new Loc(3, HealthStart), 2),
            new AttackShape(new Loc(0, HealthStart), 1),
            new AttackShape(new Loc(2, HealthStart), 3),
        };

        private int shapeIndex = 0;

        // Progress counts down to zero
        private (int Progress, Loc position, float subposition) CurrentAttack;

        private int Health;
        private int PartialHealth;
        private readonly int[] penaltyLevels = new int[W];
        private readonly int[] penaltyCounts = new int[W];
        private readonly PayoutTable healthPayoutTable;
        private PenaltySchedule penaltySchedule;
        private readonly HealthGrid healthGrid;
        private int totalWaitingMillis = 0;

        public HealthManager(PayoutTable healthPayoutTable, PenaltySchedule penaltySchedule)
        {
            this.healthPayoutTable = healthPayoutTable;
            this.penaltySchedule = penaltySchedule;

            penaltyLevels[0] = 3;
            penaltyLevels[1] = 4;
            penaltyLevels[2] = 6;
            penaltyLevels[3] = 8;
            penaltyCounts.AsSpan().Fill(0);

            CurrentAttack = RebuildAttack(HealthStart * AttackPerCell, Shapes[shapeIndex]);
            Health = MaxHealth;
            PartialHealth = 0;

            this.healthGrid = new HealthGrid(this);
            healthGrid.Redraw();
        }

        private static (int, Loc, float) RebuildAttack(int attackProgress, AttackShape shape)
        {
            int blocksAway = attackProgress / AttackPerCell;
            float subposition = (attackProgress % AttackPerCell) / AttackPerBlockFloat;
            var loc = new Loc(shape.AttackColumn, shape.HealthStartLoc.Y - blocksAway);
            return (attackProgress, loc, -subposition);
        }

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

        public void OnWaitingFrame(int millis)
        {
            // TODO should be a data type that holds "elapsed" and "total"
            totalWaitingMillis += millis;

            var attackProgress = CurrentAttack.Progress - millis * AttackPerMillis;
            if (attackProgress <= 0)
            {
                Health--;
                shapeIndex = (shapeIndex + 1) % Shapes.Length;
                attackProgress += StartingAttackProgress();
            }
            CurrentAttack = RebuildAttack(attackProgress, Shapes[shapeIndex]);

            if (penaltySchedule.TryAdvance(totalWaitingMillis, out var penalty))
            {
                AddPenalty(penalty.Level);
            }

            healthGrid.Redraw();
        }

        private int StartingAttackProgress()
        {
            var penaltyHeight = penaltyCounts.Max();
            return (HealthStart - penaltyHeight) * AttackPerCell;
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

            int attackProgressRemaining = CurrentAttack.Progress;
            if (Health > 1)
            {
                attackProgressRemaining += StartingAttackProgress();
            }
            var millisUntilDeath = attackProgressRemaining / AttackPerMillis;
            const float range = 5000f;
            return (range - millisUntilDeath) / range;
        }

        public float GetAdder(Loc loc)
        {
            var attack = CurrentAttack;
            if (loc == attack.position)
            {
                return attack.subposition;
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
                    Put(new Loc(shape.AttackColumn - 2, HealthStart), HealthOccupants.Attack);
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
                    Put(manager.CurrentAttack.position, HealthOccupants.Attack);
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
