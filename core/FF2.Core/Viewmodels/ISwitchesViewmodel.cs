using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core.Viewmodels
{
    public interface ISwitchesViewmodel
    {
        int MinRank { get; }
        int MaxRank { get; }
        bool IsGreen(int rank);
    }
}
