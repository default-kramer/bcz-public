using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FF2.Core.Viewmodels;

namespace FF2.Core
{
    public sealed class BarrierDefinition
    {
        public readonly int RowY;
        public readonly IReadOnlyList<int> RanksNeeded;

        public BarrierDefinition(int y, IReadOnlyList<int> ranks)
        {
            this.RowY = y;
            this.RanksNeeded = ranks;
        }
    }

    sealed class BarrierHook : EmptyStateHook, IBarrierTogglesViewmodel
    {
        private readonly List<(int, List<ToggleViewmodel>)> models; // Item1 is Y-coordinate
        private bool hasDestroyedBarrier = false;
        private bool hasFlippedToggle = false;

        public BarrierHook(IReadOnlyList<BarrierDefinition> barriers)
        {
            this.models = barriers
                .OrderByDescending(x => x.RowY)
                .Select(b => (b.RowY, b.RanksNeeded.Select(rank => new ToggleViewmodel(rank, false)).ToList())).ToList();
        }

        private static IReadOnlyList<ToggleViewmodel> NoToggles = new List<ToggleViewmodel>();
        IReadOnlyList<ToggleViewmodel> IBarrierTogglesViewmodel.GetToggles() => models.Count > 0 ? models[0].Item2 : NoToggles;

        public override void OnCatalystSpawned(SpawnItem catalyst)
        {
            if (hasDestroyedBarrier)
            {
                models.RemoveAt(0);
            }
            hasDestroyedBarrier = false;
            hasFlippedToggle = false;
        }

        public override void OnComboLikelyCompleted(State state, ComboInfo combo, IScheduler scheduler)
        {
            CheckIt(combo, state);
        }

        private void CheckIt(ComboInfo combo, State state)
        {
            if (hasDestroyedBarrier || hasFlippedToggle || models.Count == 0)
            {
                return;
            }

            var item = models[0];
            var toggles = item.Item2;
            for (int i = toggles.Count - 1; i >= 0; i--)
            {
                var toggle = toggles[i];
                if (!toggle.IsGreen && combo.PermissiveCombo.AdjustedGroupCount >= toggle.Rank)
                {
                    toggles[i] = toggle.MakeGreen();
                    hasFlippedToggle = true;
                    break;
                }
            }

            if (hasFlippedToggle && toggles.All(x => x.IsGreen))
            {
                state.EnqueueBarrierDestruction(item.Item1);
                hasDestroyedBarrier = true;
            }
        }
    }
}
