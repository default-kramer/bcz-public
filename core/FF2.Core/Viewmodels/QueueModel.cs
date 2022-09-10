using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core.Viewmodels
{
    public sealed class QueueModel
    {
        private readonly InfiniteDeck<DeckItem> deck;

        internal QueueModel(InfiniteDeck<DeckItem> deck)
        {
            this.deck = deck;
        }

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
