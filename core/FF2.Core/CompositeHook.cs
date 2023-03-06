using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
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

        public void OnComboCompleted(ComboInfo combo, IScheduler scheduler)
        {
            a.OnComboCompleted(combo, scheduler);
            b.OnComboCompleted(combo, scheduler);
        }

        public void OnComboUpdated(ComboInfo previous, ComboInfo current, IScheduler scheduler)
        {
            a.OnComboUpdated(previous, current, scheduler);
            b.OnComboUpdated(previous, current, scheduler);
        }

        public void PreSpawn(int spawnCount)
        {
            a.PreSpawn(spawnCount);
            b.PreSpawn(spawnCount);
        }
    }
}
