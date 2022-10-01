using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core.Viewmodels
{
    public sealed class QueueModel
    {
        private readonly ISpawnDeck deck;

        internal QueueModel(ISpawnDeck deck)
        {
            this.deck = deck;
        }

        public int LookaheadLimit => deck.PeekLimit;

        public (Occupant, Occupant) this[int index]
        {
            get
            {
                var colors = deck.Peek(index);
                return (left.SetColor(colors.LeftColor), right.SetColor(colors.RightColor));
            }
        }

        private static readonly Occupant left = new Occupant(OccupantKind.Catalyst, Color.Yellow, Direction.Right);
        private static readonly Occupant right = new Occupant(OccupantKind.Catalyst, Color.Yellow, Direction.Left);
    }
}
