using RTS.Units;
using UnityEngine;

namespace RTS.Commands
{
    [CreateAssetMenu(fileName = "Attack", menuName = "Units/Commands/Attack", order = 99)]
    public class AttackCommand : BaseCommand
    {
        [SerializeField] private MoveCommand moveCommand;

        public override bool CanHandle(CommandContext context)
        {
            return context.Commandable is IAttacker && context.Hit.collider != null;
        }

        public override void Handle(CommandContext context)
        {
            if (context.Commandable is not IAttacker attacker) return;

            if (context.Hit.collider != null
                && context.Hit.collider.TryGetComponent(out IDamageable damageable)
                && IsHitColliderVisible(context))
            {
                if (damageable.Owner == Owner.Player1 && damageable.Owner == Owner.Player1)
                    return;

                attacker.Attack(damageable);
                return;
            }

            if (moveCommand != null)
            {
                attacker.Attack(moveCommand.GetSmartMoveLocation(context));
            }
            else
            {
                attacker.Attack(context.Hit.point);
            }
        }

        public override bool IsLocked(CommandContext context) => false;
    }
}