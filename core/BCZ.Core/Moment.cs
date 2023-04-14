using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    /// <summary>
    /// A Moment is the number of milliseconds elapsed since the start of a game.
    /// </summary>
    public readonly struct Moment
    {
        public readonly int Millis;

        public Moment(int millis) { this.Millis = millis; }

        public static readonly Moment Zero = new Moment(0);
        public static readonly Moment Never = new Moment(int.MaxValue);

        public Moment AddMillis(int millis)
        {
            return new Moment(this.Millis + millis);
        }

        public static bool operator <(Moment a, Moment b)
        {
            return a.Millis < b.Millis;
        }

        public static bool operator >(Moment a, Moment b)
        {
            return a.Millis > b.Millis;
        }

        public static bool operator <=(Moment a, Moment b)
        {
            return a.Millis <= b.Millis;
        }

        public static bool operator >=(Moment a, Moment b)
        {
            return a.Millis >= b.Millis;
        }

        public override string ToString()
        {
            return $"{Millis}ms";
        }
    }
}
