using RTS.Units;
using UnityEngine;
using UnityEngine.AI;

namespace RTS.Commands
{
    [CreateAssetMenu(fileName = "Move Action", menuName = "Units/Commands/Move", order = 100)]
    public class MoveCommand : BaseCommand
    {
        [SerializeField] private float radiusMultiplier = 3.5f;

        private int unitsOnLayer = 0;
        private int maxUnitsOnLayer = 1;
        private float circleRadius = 0;
        private float radialOffset = 0;
        
        public override bool CanHandle(CommandContext context)
        {
            return context.Commandable is AbstractUnit;
        }

        public override void Handle(CommandContext context)
        {
            AbstractUnit unit = (AbstractUnit)context.Commandable;

            if (context.Hit.collider != null
                && context.Hit.collider.TryGetComponent(out AbstractCommandable commandable)
                && commandable.IsVisibleTo(context.Owner))
            {
                unit.MoveTo(commandable.transform);
                return;
            }

            unit.MoveTo(GetSmartMoveLocation(context));
        }

        public override bool IsLocked(CommandContext context) => false;

        public Vector3 GetSmartMoveLocation(CommandContext context)
        {
            if (context.Commandable is not AbstractUnit unit) return context.Hit.point;

            if (context.UnitIndex == 0)
            {
                unitsOnLayer = 0;
                maxUnitsOnLayer = 1;
                circleRadius = 0;
                radialOffset = 0;
            }

            Vector3 targetPosition = new(
                context.Hit.point.x + circleRadius * Mathf.Cos(radialOffset * unitsOnLayer),
                context.Hit.point.y,
                context.Hit.point.z + circleRadius * Mathf.Sin(radialOffset * unitsOnLayer)
            );

            unitsOnLayer++;

            if (unitsOnLayer >= maxUnitsOnLayer)
            {
                unitsOnLayer = 0;
                circleRadius += unit.AgentRadius * radiusMultiplier;
                maxUnitsOnLayer = Mathf.FloorToInt(2 * Mathf.PI * circleRadius / (unit.AgentRadius * 2));
                radialOffset = 2 * Mathf.PI / maxUnitsOnLayer;
            }

            return targetPosition;
        }
    }
}