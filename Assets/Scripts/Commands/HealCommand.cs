using RTS.Units;
using UnityEngine;

namespace RTS.Commands
{
    [CreateAssetMenu(fileName = "Heal", menuName = "Units/Commands/Heal", order = 99)]
    public class HealCommand : BaseCommand
    {
        public override bool CanHandle(CommandContext context)
        {
            if (context.Commandable is not IHealer)
                return false;

            if (context.Hit.collider == null)
                return false;

            if (!TryGetDamageable(context, out IDamageable damageable))
                return false;

            if (damageable.Owner != context.Commandable.Owner)
                return false;

            if (damageable.CurrentHealth >= damageable.MaxHealth)
                return false;


            return true;
        }

        public override void Handle(CommandContext context)
        {
            if (context.Commandable is not IHealer healer)
                return;

            if (!TryGetDamageable(context, out IDamageable damageable))
                return;

            if (damageable.Owner != context.Commandable.Owner)
                return;

            if (damageable.CurrentHealth >= damageable.MaxHealth)
                return;

            healer.HealTarget(damageable);
        }

        public override bool IsLocked(CommandContext context) => false;

        private bool TryGetDamageable(CommandContext context, out IDamageable damageable)
        {
            damageable = null;

            if (context.Hit.collider == null)
                return false;

            if (context.Hit.collider.TryGetComponent(out damageable))
                return true;

            damageable = context.Hit.collider.GetComponentInParent<IDamageable>();
            return damageable != null;
        }
    }
}