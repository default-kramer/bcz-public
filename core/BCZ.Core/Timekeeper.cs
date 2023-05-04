using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BCZ.Core
{
    /// <summary>
    /// The core logic of this game uses the millisecond as the basic unit of time.
    /// But obviously Godot will not run 1 frame every millisecond!
    /// So our timekeeping logic keeps track of <see cref="Appointment"/>s which it promises
    /// to uphold with millisecond precision.
    /// Let's say that an appointment is scheduled for 5500ms and the Godot framerate happens
    /// to land on FrameN at 5490ms and FrameN+1 at 5508ms.
    /// When FrameN arrives
    /// 1. We elapse to 5490ms
    /// 2. We handle any player commands
    /// Then when FrameN+1 arrives
    /// 3. We elapse to 5500ms (call it an "artificial frame")
    ///    The code that created the appointment sees that its time has arrived, and does something.
    /// 4. We elapse to 5508ms
    /// 5. We handle any player commands
    ///
    /// As a result, the core logic should behave the same regardless of framerate.
    /// This is significant for replays as a sequence of Command+Moment pairs -- we can just
    /// jump directly to each Moment in the sequence and the timekeeper will insert all the
    /// "artificial frames" necessary to update the state correctly.
    ///
    /// IMPORTANT: This interface <see cref="IScheduler"/> does not provide a way to access the
    /// current Moment, and this is by design. If code could access the current Moment, it would
    /// become too easy to accidentally write code that depends on the framerate.
    /// </summary>
    interface IScheduler
    {
        Appointment CreateAppointment(int millisFromNow);

        Appointment CreateWaitingAppointment(int millisFromNow);

        /// <summary>
        /// Unlike <see cref="CreateAppointment(int)"/> there is no guarantee that you will
        /// get called back the exact time of your appointment.
        /// So this is appropriate for cosmetic concerns such as animations,
        /// not for logic that needs to run with exact-millisecond precision.
        /// </summary>
        Appointment CreateAnimation(int millisFromNow);
    }

    /// <summary>
    /// Provides access to the current <see cref="Moment"/>.
    /// Prefer to use <see cref="IScheduler"/> if possible, see comments there.
    /// </summary>
    interface ITimer
    {
        Moment Now { get; }
    }

    sealed class Timekeeper : IScheduler, ITimer
    {
        private readonly SortedSet<int> appointments = new() { 0 };
        private readonly SortedSet<int> waitingAppointments = new() { 0 };
        private Moment cursor;
        private Moment waitingCursor;
        private readonly Func<Moment> cursorProvider;
        private readonly Func<Moment> waitingCursorProvider;

        Moment ITimer.Now => cursor;

        public Timekeeper()
        {
            this.cursorProvider = () => cursor;
            this.waitingCursorProvider = () => waitingCursor;
        }

        public void Elapse(Moment now, State state)
        {
            while (TryElapse(now, state)) { }
        }

        private bool TryElapse(Moment limit, State state)
        {
            bool hasAppointment = false;

            // At most, we are going to elapse up to the limit...
            int elapse = limit.Millis - cursor.Millis;

            // ... but we check for any appointment in that range ...
            if (appointments.Count > 0)
            {
                var temp = appointments.Min - cursor.Millis;
                if (temp <= elapse)
                {
                    elapse = temp;
                    hasAppointment = true;
                }
            }

            // .. and any waiting appointment in that range.
            bool waiting = state.IsWaitingOnUser;
            if (waiting && waitingAppointments.Count > 0)
            {
                var temp = waitingAppointments.Min - waitingCursor.Millis;
                if (temp <= elapse)
                {
                    elapse = temp;
                    hasAppointment = true;
                }
            }

            this.cursor = cursor.AddMillis(elapse);
            this.waitingCursor = waiting ? waitingCursor.AddMillis(elapse) : waitingCursor;
            if (!hasAppointment)
            {
                return false;
            }

            appointments.Remove(this.cursor.Millis);
            waitingAppointments.Remove(this.waitingCursor.Millis);
            //Console.WriteLine($"Elapsing to {cursor} (waiting: {waitingCursor})");
            state.TEMP_TimekeeperHook(cursor);
            return true;
        }

        public Appointment CreateAppointment(int millisFromNow)
        {
            var when = cursor.AddMillis(millisFromNow);
            appointments.Add(when.Millis);
            return new Appointment(when, cursorProvider, cursor);
        }

        public Appointment CreateAnimation(int millisFromNow)
        {
            var when = cursor.AddMillis(millisFromNow);
            return new Appointment(when, cursorProvider, cursor);
        }

        public Appointment CreateWaitingAppointment(int millisFromNow)
        {
            var when = waitingCursor.AddMillis(millisFromNow);
            waitingAppointments.Add(when.Millis);
            return new Appointment(when, waitingCursorProvider, waitingCursor);
        }
    }

    /// <summary>
    /// See documentation on <see cref="IScheduler"/>.
    /// </summary>
    public readonly struct Appointment
    {
        private readonly Moment created;
        private readonly Moment occurs;
        private readonly Func<Moment> now;

        public Appointment(Moment occurs, Func<Moment> now, Moment created)
        {
            this.occurs = occurs;
            this.now = now;
            this.created = created;
        }

        public bool HasArrived()
        {
            return occurs <= now();
        }

        /// <summary>
        /// This method is intended only to drive animations.
        /// Code should not make non-cosmetic decisions based on this method's return value; doing so could
        /// bypass the protections described on the <see cref="IScheduler"/>.
        /// </summary>
        public float Progress()
        {
            if (IsFrame0)
            {
                return 1f;
            }
            float total = occurs.Millis - created.Millis;
            float completed = now().Millis - created.Millis;
            return completed / total;
        }

        private static readonly Func<Moment> AlwaysZero = () => Moment.Zero;

        public static readonly Appointment Frame0 = new Appointment(Moment.Zero, AlwaysZero, Moment.Zero);

        public bool IsFrame0 => occurs.Millis == 0;

        public static readonly Appointment Never = new Appointment(Moment.Never, AlwaysZero, Moment.Zero);

        /// <summary>
        /// TODO does this bypass the protections of <see cref="IScheduler"/>?
        /// But I can't think of a better to implement the countdown...
        /// </summary>
        public int MillisRemaining()
        {
            return occurs.Millis - now().Millis;
        }
    }
}
