using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    sealed class InfiniteDeck<T> : IDisposable
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
            shuffle = ArrayPool<T>.Shared.Rent(deck.Count * 2);
            this.index = 0;
            this.prng = prng;

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
            this.shuffle = ArrayPool<T>.Shared.Rent(otherSpan.Length);
            otherSpan.CopyTo(this.shuffle);
            this.index = other.index;
        }

        public InfiniteDeck<T> Clone() { return new InfiniteDeck<T>(this); }

        public void Peek(T[] buffer)
        {
            if (buffer.Length > shuffle.Length)
            {
                throw new ArgumentException("Cannot preview that much!");
            }

            int index = this.index;
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = shuffle[index];
                index++;
                if (index == shuffle.Length)
                {
                    index = 0;
                }
            }
        }

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

        public void Dispose()
        {
            if (shuffle != null)
            {
                ArrayPool<T>.Shared.Return(shuffle);
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
                shuffle = null;
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
            }
        }
    }
}
