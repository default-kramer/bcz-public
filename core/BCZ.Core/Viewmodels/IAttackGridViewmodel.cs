using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BCZ.Core.Viewmodels
{
    public interface IAttackGridViewmodel
    {
        IReadOnlyGridSlim Grid { get; }

        bool IsFrozen(Loc loc);
    }
}
