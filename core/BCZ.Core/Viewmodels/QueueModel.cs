using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BCZ.Core.Viewmodels
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

        /// <summary>
        /// Placeholder to be used while preparing the game.
        /// </summary>
        public static readonly QueueModel EmptyModel = new QueueModel(EmptyDeck.Instance);

        private class EmptyDeck : ISpawnDeck
        {
            private EmptyDeck() { }
            public static readonly EmptyDeck Instance = new EmptyDeck();

            private static readonly SpawnItem Placeholder = SpawnItem.MakeCatalystPair(Color.Blank, Color.Blank);

            public int PeekLimit => 5;
            public SpawnItem Peek(int index) => Placeholder;
            public SpawnItem Pop() => Placeholder;
        }
    }
}
