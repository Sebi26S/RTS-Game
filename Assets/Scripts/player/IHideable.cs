using RTS.Units;
using UnityEngine;

namespace RTS.Player
{
    public interface IHideable
    {
        public delegate void VisibilityChangeEvent(IHideable hideable, Owner owner, bool isVisible);

        public Transform Transform { get; }

        public bool IsVisibleTo(Owner owner);
        public bool WasEverVisibleTo(Owner owner);

        public void SetVisibleForOwner(Owner owner, bool isVisible);

        public event VisibilityChangeEvent OnVisibilityChanged;
    }
}