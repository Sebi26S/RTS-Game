using System.Linq;
using RTS.Player;
using RTS.Units;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RTS.Commands
{
    public abstract class BaseCommand : ScriptableObject, ICommand
    {
        [field: SerializeField] public string Name { get; private set; } = "Command";
        [field: SerializeField] public Key Hotkey { get; private set; } = Key.None;
        [field: SerializeField] public Sprite Icon { get; private set; }
        [field: Range(-1, 8)] [field: SerializeField] public int Slot { get; private set; }
        [field: SerializeField] public bool RequiresClickToActivate { get; private set; } = true;
        [field: SerializeField] public bool IsSingleUnitCommand { get; private set; }
        [field: SerializeField] public GameObject GhostPrefab { get; private set; }
        [field: SerializeField] public BuildingRestrictionSO[] Restrictions { get; private set; }

        public abstract bool CanHandle(CommandContext context);
        public abstract void Handle(CommandContext context);

        public abstract bool IsLocked(CommandContext context);

        public virtual bool IsAvailable(CommandContext context) => true;

        public bool AllRestrictionsPass(CommandContext context, Vector3 point) =>
            Restrictions.Length == 0 || Restrictions.All(restriction => restriction.CanPlace(point, context.Owner));

        public bool IsHitColliderVisible(CommandContext context) => context.Hit.collider != null
            && context.Hit.collider.TryGetComponent(out IHideable hideable)
            && hideable.IsVisibleTo(context.Owner);
    }
}