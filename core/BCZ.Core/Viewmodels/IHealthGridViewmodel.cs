using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core.Viewmodels
{
    public interface ICountdownViewmodel
    {
        int MaxMillis { get; }

        int CurrentMillis { get; }
    }

    public interface ISlidingPenaltyViewmodel
    {
        int NumSlots { get; }

        /// <summary>
        /// Contract: A penalty at index N is (N+1) steps away from landing.
        /// So a penalty at index 0 will land during the next spawn.
        /// The given index must be less than <see cref="NumSlots"/>.
        /// </summary>
        PenaltyViewmodel GetPenalty(int index);

        bool GetHealth(out HealthStatus status);
    }

    public struct HealthStatus
    {
        public readonly int CurrentHealth;
        public readonly int MaxHealth;

        public HealthStatus(int current, int max)
        {
            this.CurrentHealth = current;
            this.MaxHealth = max;
        }
    }

    public readonly struct PenaltyViewmodel
    {
        public readonly int Size;
        public readonly float DestructionProgress;

        public PenaltyViewmodel(int size, float destructionProgress)
        {
            this.Size = size;
            this.DestructionProgress = destructionProgress;
        }

        public static PenaltyViewmodel None = new PenaltyViewmodel(0, 0);
    }
}
