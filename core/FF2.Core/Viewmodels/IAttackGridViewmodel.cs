using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core.Viewmodels
{
    public interface IAttackGridViewmodel
    {
        IReadOnlyGridSlim Grid { get; }

        bool IsFrozen(Loc loc);
    }
}
