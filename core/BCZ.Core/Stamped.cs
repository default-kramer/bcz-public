using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    /// <summary>
    /// A timestamped item.
    /// </summary>
    public readonly struct Stamped<T>
    {
        public Stamped(Moment moment, T value)
        {
            this.Moment = moment;
            this.Value = value;
        }

        public readonly Moment Moment;
        public readonly T Value;

        public Stamped<T> AdjustStamp(int millis)
        {
            return new Stamped<T>(this.Moment.AddMillis(millis), this.Value);
        }
    }
}
