using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    public enum StateEventKind
    {
        /// <summary>
        /// Should only be used when a state is first constructed.
        /// The state can recognize this and perform the first spawn to start the game.
        /// </summary>
        StateConstructed = 1,

        /// <summary>
        /// Based on personal playtesting, I conclude that it is better to allow the player to input
        /// commands during the spawn animation.
        /// A beginner won't care, and an expert will appreciate the quickness.
        /// So this event should be considered "for animation only" - not for state logic.
        /// TODO write a test to lock this behavior in.
        /// TODO should also consider those millis to be "waiting millis"... I think.
        /// </summary>
        Spawned = 2,

        Fell = 3,
        Destroyed = 4,
        GameEnded = 5,

        // Results of user actions
        BurstBegan = 101,
        Plummeted = 102,
    }

    /// <summary>
    /// A state will hold at most 1 state event at a time.
    /// </summary>
    public readonly struct StateEvent
    {
        public readonly StateEventKind Kind;
        public readonly Appointment Completion;
        private readonly StateTransitionSumType sum;

        public static readonly StateEvent StateConstructed
            = new StateEvent(StateEventKind.StateConstructed, Appointment.Frame0, Singletons.StateConstructed);

        public static readonly StateEvent GameEnded
            = new StateEvent(StateEventKind.GameEnded, Appointment.Never, Singletons.GameEnded);

        private StateEvent(StateEventKind kind, Appointment completion, StateTransitionSumType sum)
        {
            Kind = kind;
            Completion = completion;
            this.sum = sum;
        }

        public SpawnItem SpawnedPayload() => sum.SpawnedPayload();
        public FallAnimationSampler FellPayload() => sum.FellPayload();
        public ITickCalculations DestroyedPayload() => sum.DestroyedPayload();

        public float ProgressOr(StateEventKind kind, float elseVal)
        {
            if (Kind == kind)
            {
                return Completion.Progress();
            }
            return elseVal;
        }

        /// <summary>
        /// Warning - reuses the "payload containers" per event kind.
        /// For example, the return value of <see cref="Spawned(SpawnItem, Appointment)"/> will
        /// be mutated and reused each time you call it.
        /// </summary>
        public class Factory
        {
            private readonly SpawnedType spawned = new SpawnedType();
            private readonly FellType fell = new FellType();
            private readonly DestroyedType destroyed = new DestroyedType();

            public StateEvent Spawned(SpawnItem payload, Appointment completion)
            {
                return new StateEvent(StateEventKind.Spawned, completion, spawned.Reset(payload));
            }

            public StateEvent Fell(FallAnimationSampler payload, Appointment completion)
            {
                return new StateEvent(StateEventKind.Fell, completion, fell.Reset(payload));
            }

            public StateEvent Destroyed(ITickCalculations payload, Appointment completion)
            {
                return new StateEvent(StateEventKind.Destroyed, completion, destroyed.Reset(payload));
            }

            public StateEvent Plummeted(Appointment completion)
            {
                return new StateEvent(StateEventKind.Plummeted, completion, Singletons.Plummeted);
            }

            public StateEvent BurstBegan(Appointment completion)
            {
                return new StateEvent(StateEventKind.BurstBegan, completion, Singletons.BurstBegan);
            }
        }

        abstract class StateTransitionSumType
        {
            protected virtual string NameForException() => this.GetType().Name;
            private Exception WrongType()
            {
                return new Exception($"Cannot provide requested payload, this is {NameForException()}");
            }

            public virtual SpawnItem SpawnedPayload() => throw WrongType();
            public virtual FallAnimationSampler FellPayload() => throw WrongType();
            public virtual ITickCalculations DestroyedPayload() => throw WrongType();
        }

        class SpawnedType : StateTransitionSumType
        {
            private SpawnItem payload;
            public override SpawnItem SpawnedPayload() => payload;

            public SpawnedType Reset(SpawnItem payload)
            {
                this.payload = payload;
                return this;
            }
        }

        class FellType : StateTransitionSumType
        {
            private FallAnimationSampler payload = null!;
            public override FallAnimationSampler FellPayload() => payload;

            public FellType Reset(FallAnimationSampler payload)
            {
                this.payload = payload;
                return this;
            }
        }

        class DestroyedType : StateTransitionSumType
        {
            private ITickCalculations payload = null!;
            public override ITickCalculations DestroyedPayload() => payload;

            public DestroyedType Reset(ITickCalculations payload)
            {
                this.payload = payload;
                return this;
            }
        }

        /// <summary>
        /// Singletons can be used for events that lack a payload.
        /// </summary>
        class Singletons : StateTransitionSumType
        {
            private readonly string Name;
            private Singletons(string name) { this.Name = name; }
            protected override string NameForException() => Name;

            public static readonly Singletons StateConstructed = new("StateConstructed");
            public static readonly Singletons GameEnded = new("GameEnded");
            public static readonly Singletons Plummeted = new("Plummeted");
            public static readonly Singletons BurstBegan = new("BurstBegan");
        }
    }
}
