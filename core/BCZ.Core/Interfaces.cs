using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BCZ.Core
{
    public readonly ref struct GoalArgs
    {
        public readonly int ElapsedMillis;
        public readonly int SpawnCount;

        public GoalArgs(int elapsedMillis, int spawnCount)
        {
            this.ElapsedMillis = elapsedMillis;
            this.SpawnCount = spawnCount;
        }
    }

    public interface IGoal
    {
        int GetTargetScore(GoalArgs args);
        GoalKind Kind { get; }
    }

    class FixedGoal : IGoal
    {
        public GoalKind Kind { get; }
        private readonly int target;

        public FixedGoal(GoalKind kind, int target)
        {
            Kind = kind;
            this.target = target;
        }

        public int GetTargetScore(GoalArgs args) => target;
    }

    class VariableGoal : IGoal
    {
        private readonly int baseScore;
        private readonly Formula millisFormula;
        private readonly Formula spawnCountFormula;
        public GoalKind Kind { get; }

        public VariableGoal(int baseScore, Formula millisFormula, Formula spawnCountFormula, GoalKind kind)
        {
            this.baseScore = baseScore;
            this.millisFormula = millisFormula;
            this.spawnCountFormula = spawnCountFormula;
            Kind = kind;
        }

        public int GetTargetScore(GoalArgs args)
        {
            return baseScore
                + millisFormula.Calculate(args.ElapsedMillis)
                + spawnCountFormula.Calculate(args.SpawnCount);
        }

        public readonly struct Formula
        {
            public readonly int target;
            public readonly int factor;
            public readonly double penalizer;

            public Formula(int target, int factor, double penalizer)
            {
                this.target = target;
                this.factor = factor;
                this.penalizer = penalizer;
            }

            public int Calculate(int actual)
            {
                double ratio = Convert.ToDouble(actual) / target;
                if (actual > target)
                {
                    ratio = Math.Pow(ratio, penalizer);
                }
                return Convert.ToInt32(factor * ratio);
            }
        }
    }

    public enum GoalKind
    {
        None,
        Bronze,
        Silver,
        Gold,
        Arbitrary,
    }

    public interface IReplayCollector
    {
        void Collect(Stamped<Command> command);

        void AfterCommand(Moment moment, State state);
    }

    public interface ISettingsCollection
    {
        int MaxLevel { get; }

        ISinglePlayerSettings GetSettings(int level);

        // Putting goals into the settings and/or the state felt wrong.
        // Putting goals here feels a little better... but not quite right.
        IReadOnlyList<IGoal> GetGoals(int level);
    }

    public interface ISpawnDeck
    {
        /// <summary>
        /// Removes the top item from the deck and returns it.
        /// </summary>
        SpawnItem Pop();

        /// <summary>
        /// Returns the Nth item from the top of the deck without removing it.
        /// </summary>
        /// <param name="index">Must be at least 0 and less than <see cref="PeekLimit"/></param>
        SpawnItem Peek(int index);

        int PeekLimit { get; }
    }

    public interface IReplayDriver
    {
        void Advance(Moment now);

        Ticker Ticker { get; }
    }

    interface IStateHook
    {
        void OnComboUpdated(ComboInfo previous, ComboInfo current, IScheduler scheduler);

        /// <summary>
        /// Called when the combo is probably done, but barrier removal might allow it to continue.
        /// </summary>
        void OnComboLikelyCompleted(State state, ComboInfo combo, IScheduler scheduler);

        void OnCatalystSpawned(SpawnItem catalyst);

        /// <summary>
        /// Can be called multiple times per spawn. Use the <paramref name="spawnCount"/>
        /// if you need to detect these "duplicate" calls.
        /// </summary>
        void PreSpawn(State state, int spawnCount);

        bool GameOver { get; }
    }

    abstract class EmptyStateHook : IStateHook
    {
        public virtual bool GameOver => false;

        public virtual void OnCatalystSpawned(SpawnItem catalyst) { }

        public virtual void OnComboLikelyCompleted(State state, ComboInfo combo, IScheduler scheduler) { }

        public virtual void OnComboUpdated(ComboInfo previous, ComboInfo current, IScheduler scheduler) { }

        public virtual void PreSpawn(State state, int spawnCount) { }
    }

    sealed class NullStateHook : EmptyStateHook
    {
        private NullStateHook() { }
        public static readonly NullStateHook Instance = new NullStateHook();
    }
}
