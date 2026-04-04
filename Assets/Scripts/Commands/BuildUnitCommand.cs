using UnityEngine;
using RTS.Units;
using RTS.Player;
using RTS.TechTree;

namespace RTS.Commands
{
    [CreateAssetMenu(fileName = "Build Unit", menuName = "Buildings/Commands/Build Unit", order = 120)]
    public class BuildUnitCommand : BaseCommand, IUnlockableCommand
    {
        [field: SerializeField] public AbstractUnitSO Unit { get; private set; }

        public override bool CanHandle(CommandContext context)
        {
            if (context.Commandable is not BaseBuilding building)
                return false;

            if (building.IsQueueFull)
                return false;

            return HasEnoughSupplies(context);
        }

        public override void Handle(CommandContext context)
        {
            if (context.Commandable is not BaseBuilding building)
                return;

            if (building.IsQueueFull)
                return;

            if (!HasEnoughSupplies(context))
                return;

            if (building.QueueSize == 0 && !HasEnoughPopulation(context))
                return;

            building.BuildUnlockable(Unit);
        }

        public override bool IsLocked(CommandContext context) =>
            context.Commandable is not BaseBuilding building
            || building.IsQueueFull
            || !HasEnoughSupplies(context)
            || !Unit.TechTree.IsUnlocked(context.Owner, Unit)
            || (building.QueueSize == 0 && !HasEnoughPopulation(context));

        public UnlockableSO[] GetUnmetDependencies(Owner owner)
        {
            return Unit.TechTree.GetUnmetDependencies(owner, Unit);
        }

        private bool HasEnoughSupplies(CommandContext context)
        {
            return Unit.Cost.Minerals <= Supplies.Minerals[context.Owner]
                && Unit.Cost.Wood <= Supplies.Wood[context.Owner]
                && Unit.Cost.Stone <= Supplies.Stone[context.Owner];
        }

        private bool HasEnoughPopulation(CommandContext context)
        {
            if (Unit.PopulationConfig == null) return true;

            int newPopulation = Unit.PopulationConfig.PopulationCost + Supplies.Population[context.Owner];

            return newPopulation <= Supplies.PopulationLimit[context.Owner];
        }
    }
}