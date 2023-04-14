using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BCZ.Core.Viewmodels
{
    public interface ISwitchesViewmodel
    {
        int MinRank { get; }
        int MaxRank { get; }
        bool IsGreen(int rank);
    }

    public interface IBarrierTogglesViewmodel
    {
        IReadOnlyList<ToggleViewmodel> GetToggles();
    }

    public readonly struct ToggleViewmodel
    {
        public readonly int Rank;
        public readonly bool IsGreen;

        public ToggleViewmodel(int rank, bool isGreen)
        {
            this.Rank = rank;
            this.IsGreen = isGreen;
        }

        public static ToggleViewmodel Nothing = new ToggleViewmodel(0, false);

        public ToggleViewmodel MakeGreen()
        {
            return new ToggleViewmodel(Rank, true);
        }
    }
}
