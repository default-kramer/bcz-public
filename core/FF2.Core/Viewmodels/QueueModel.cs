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

        public SpawnItem this[int index] => deck.Peek(index);
    }
}
