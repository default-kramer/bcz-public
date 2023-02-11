using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    public interface IReplayCollector
    {
        void Collect(Stamped<Command> command);

        void AfterCommand(Moment moment, State state);
    }

    public interface ISettingsCollection
    {
        int MaxLevel { get; }

        ISinglePlayerSettings GetSettings(int level);
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

        void AddPenalty(SpawnItem penalty);
    }

    public interface IReplayDriver
    {
        void Advance(Moment now);

        Ticker Ticker { get; }
    }

    interface IStateHook
    {
        void Elapse(IScheduler scheduler);

        void OnComboUpdated(ComboInfo previous, ComboInfo current, IScheduler scheduler);

        void OnComboCompleted(ComboInfo combo, IScheduler scheduler);

        void OnCatalystSpawned(SpawnItem catalyst);

        bool GameOver { get; }

        StateEvent? AddPenalty(SpawnItem penalty, StateEvent.Factory eventFactory, IScheduler scheduler);
    }

    sealed class NullStateHook : IStateHook
    {
        private NullStateHook() { }
        public static readonly NullStateHook Instance = new NullStateHook();

        public bool GameOver => false;

        public StateEvent? AddPenalty(SpawnItem penalty, StateEvent.Factory eventFactory, IScheduler scheduler) => null;

        public void Elapse(IScheduler scheduler) { }

        public void OnComboCompleted(ComboInfo combo, IScheduler scheduler) { }

        public void OnComboUpdated(ComboInfo previous, ComboInfo current, IScheduler scheduler) { }

        public void OnCatalystSpawned(SpawnItem catalyst) { }
    }
}
