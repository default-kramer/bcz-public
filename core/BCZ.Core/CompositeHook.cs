using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BCZ.Core
{
    sealed class CompositeHook : IStateHook
    {
        private readonly IStateHook a;
        private readonly IStateHook b;
        public CompositeHook(IStateHook a, IStateHook b)
        {
            this.a = a;
            this.b = b;
        }

        public bool GameOver => a.GameOver || b.GameOver;

        public void OnCatalystSpawned(SpawnItem catalyst)
        {
            a.OnCatalystSpawned(catalyst);
            b.OnCatalystSpawned(catalyst);
        }

        public void OnComboLikelyCompleted(State state, ComboInfo combo, IScheduler scheduler)
        {
            a.OnComboLikelyCompleted(state, combo, scheduler);
            b.OnComboLikelyCompleted(state, combo, scheduler);
        }

        public void OnComboUpdated(ComboInfo previous, ComboInfo current, IScheduler scheduler)
        {
            a.OnComboUpdated(previous, current, scheduler);
            b.OnComboUpdated(previous, current, scheduler);
        }

        public void PreSpawn(State state, int spawnCount)
        {
            a.PreSpawn(state, spawnCount);
            b.PreSpawn(state, spawnCount);
        }
    }
}
