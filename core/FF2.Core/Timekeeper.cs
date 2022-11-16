using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    // This is all we pass in to State, Ticker, HealthMgr, etc...
    // They cannot accidentally grab the current Moment and do their own timing logic
    interface IScheduler
    {
        Appointment CreateAppointment(int millisFromNow);

        Appointment CreateWaitingAppointment(int millisFromNow);
    }

    sealed class Timekeeper : IScheduler
    {
        private readonly SortedSet<int> appointments = new SortedSet<int>();
        private readonly SortedSet<int> waitingAppointments = new SortedSet<int>();
        private Moment cursor;
        private Moment waitingCursor;
        private readonly Func<Moment> cursorProvider;
        private readonly Func<Moment> waitingCursorProvider;

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
            bool waiting = state.Kind == StateKind.Waiting;
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
            state.TEMP_TimekeeperHook(this);
            return true;
        }

        public Appointment CreateAppointment(int millisFromNow)
        {
            var when = cursor.AddMillis(millisFromNow);
            appointments.Add(when.Millis);
            return new Appointment(when, cursorProvider, cursor);
        }

        public Appointment CreateWaitingAppointment(int millisFromNow)
        {
            var when = waitingCursor.AddMillis(millisFromNow);
            waitingAppointments.Add(when.Millis);
            return new Appointment(when, waitingCursorProvider, waitingCursor);
        }
    }

    readonly struct Appointment
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

        public float Progress()
        {
            float total = occurs.Millis - created.Millis;
            float completed = now().Millis - created.Millis;
            return completed / total;
        }
    }
}
