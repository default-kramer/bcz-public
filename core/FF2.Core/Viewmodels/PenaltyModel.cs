using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core.Viewmodels
{
    public sealed class PenaltyModel
    {
        private readonly PenaltyManager penalties;

        internal PenaltyModel(PenaltyManager penalties)
        {
            this.penalties = penalties;
        }

        public int Count { get { return penalties.Count; } }

        public Penalty this[int index] { get { return penalties[index]; } }
    }
}
