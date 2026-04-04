using System.Collections.Generic;
using RTS.EventBus;
using RTS.Events;
using RTS.Units;
using UnityEngine;

namespace RTS.Player
{
    public class Placeholder : MonoBehaviour, IHideable
    {
        public Transform Transform => this == null ? null : transform;
        public Owner Owner { get; set; }
        public GameObject ParentObject { get; set; }

        [SerializeField] private List<Owner> visibleToOwners = new();
        [SerializeField] private List<Owner> everVisibleToOwners = new();

        private HashSet<Owner> visibleOwnerSet = new();
        private HashSet<Owner> everVisibleOwnerSet = new();

        public event IHideable.VisibilityChangeEvent OnVisibilityChanged;

        private void Awake()
        {
            visibleOwnerSet = new HashSet<Owner>(visibleToOwners);
            everVisibleOwnerSet = new HashSet<Owner>(everVisibleToOwners);
        }

        private void Start()
        {
            Bus<PlaceholderSpawnEvent>.Raise(Owner, new PlaceholderSpawnEvent(this));
        }

        public bool IsVisibleTo(Owner owner) => visibleOwnerSet.Contains(owner);

        public bool WasEverVisibleTo(Owner owner) => everVisibleOwnerSet.Contains(owner);

        public void SetVisibleForOwner(Owner owner, bool isVisible)
        {
            bool wasVisible = visibleOwnerSet.Contains(owner);

            if (wasVisible == isVisible)
                return;

            if (isVisible)
            {
                visibleOwnerSet.Add(owner);
                everVisibleOwnerSet.Add(owner);
            }
            else
            {
                visibleOwnerSet.Remove(owner);
            }

            SyncVisibilityListsForInspector();

            OnVisibilityChanged?.Invoke(this, owner, isVisible);

            if (isVisible && ParentObject == null)
            {
                Destroy(gameObject);
            }
        }

        private void SyncVisibilityListsForInspector()
        {
            visibleToOwners = new List<Owner>(visibleOwnerSet);
            everVisibleToOwners = new List<Owner>(everVisibleOwnerSet);
        }

        private void OnDestroy()
        {
            Bus<PlaceholderDestroyEvent>.Raise(Owner, new PlaceholderDestroyEvent(this));
        }
    }
}