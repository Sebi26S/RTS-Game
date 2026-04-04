using RTS.Units;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

namespace RTS.Commands
{
    [CreateAssetMenu(fileName = "Cancel Building", menuName = "Units/Commands/Cancel Building")]
    public class CancelBuildingCommand : BaseCommand
    {
        public override bool CanHandle(CommandContext context)
        {
            return context.Commandable is BaseBuilding || (
                    context.Commandable is IBuildingBuilder
                    && context.Button == MouseButton.Left
            );
        }

        public override void Handle(CommandContext context)
        {
            if (context.Commandable is BaseBuilding building)
            {
                building.CancelBuilding();
            }
            else if (context.Commandable is IBuildingBuilder buildingBuilder)
            {
                buildingBuilder.CancelBuilding();
            }
        }

        public override bool IsLocked(CommandContext context) => false;
    }
}
