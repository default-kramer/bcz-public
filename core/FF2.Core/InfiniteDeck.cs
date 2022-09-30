using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    public sealed class InfiniteSpawnDeck : InfiniteDeck<SpawnItem>, ISpawnDeck
    {
        public InfiniteSpawnDeck(IReadOnlyList<SpawnItem> deck, PRNG prng) : base(deck, prng) { }
    }

    public class InfiniteDeck<T>
    {
        private readonly PRNG prng;

        // The idea is that `shuffle` will contain 2 copies of the same deck,
        // occupying the front half and the back half of the array.
        // Whenever we take the last item from one half, we immediately reshuffle that half.
        // This guarantees that we can always peek at least 1 deck into the future
        // without having to mutate the PRNG.
        private T[] shuffle;
        private int index;

        public InfiniteDeck(IReadOnlyList<T> deck, PRNG prng)
        {
            this.prng = prng;
            this.shuffle = new T[deck.Count * 2];
            this.index = 0;

            for (int i = 0; i < shuffle.Length; i++)
            {
                shuffle[i] = deck[i % deck.Count];
            }
            Reshuffle(true);
            Reshuffle(false);
        }

        private InfiniteDeck(InfiniteDeck<T> other)
        {
            Span<T> otherSpan = other.shuffle; // retype as Span

            this.prng = other.prng.Clone();
            this.shuffle = new T[otherSpan.Length];
            otherSpan.CopyTo(this.shuffle);
            this.index = other.index;
        }

        public InfiniteDeck<T> Clone() { return new InfiniteDeck<T>(this); }

        public T Peek(int i)
        {
            i = (index + i) % shuffle.Length;
            return shuffle[i];
        }

        public int PeekLimit => shuffle.Length / 2;

        public T Pop()
        {
            T retval = shuffle[index];
            index++;
            if (index == shuffle.Length)
            {
                Reshuffle(frontHalf: false);
                index = 0;
            }
            else if (index == shuffle.Length / 2)
            {
                Reshuffle(frontHalf: true);
            }
            return retval;
        }

        private void Reshuffle(bool frontHalf)
        {
            int half = shuffle.Length / 2;
            int adder = frontHalf ? 0 : half;

            // Fisher–Yates
            for (int loop = half - 1; loop > 0; loop--)
            {
                int i = loop + adder;
                int j = prng.NextInt32(loop + 1) + adder;
                var temp = shuffle[i];
                shuffle[i] = shuffle[j];
                shuffle[j] = temp;
            }
        }
    }
}
