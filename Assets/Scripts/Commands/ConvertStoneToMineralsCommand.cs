using RTS.Player;
using RTS.Units;
using UnityEngine;

namespace RTS.Commands
{
    [CreateAssetMenu(fileName = "Convert Stone To Minerals", menuName = "Buildings/Commands/Convert Stone To Minerals")]
    public class ConvertStoneToMineralsCommand : BaseCommand
    {
        [field: SerializeField] public ResourceConversionSO Conversion { get; private set; }

        public override bool CanHandle(CommandContext context)
        {
            return context.Commandable is BaseBuilding && HasEnoughSupplies(context);
        }

        public override void Handle(CommandContext context)
        {
            if (context.Commandable is not BaseBuilding building) return;
            if (!HasEnoughSupplies(context)) return;

            building.BuildUnlockable(Conversion);
        }

        public override bool IsLocked(CommandContext context)
        {
            return !HasEnoughSupplies(context);
        }

        private bool HasEnoughSupplies(CommandContext context)
        {
            return Conversion != null
                && Conversion.Cost.Minerals <= Supplies.Minerals[context.Owner]
                && Conversion.Cost.Wood <= Supplies.Wood[context.Owner]
                && Conversion.Cost.Stone <= Supplies.Stone[context.Owner];
        }
    }
}