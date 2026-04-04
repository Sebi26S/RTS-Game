using System;
using System.Collections.Generic;
using System.Linq;
using RTS.EventBus;
using RTS.Events;
using RTS.Player;
using UnityEngine;

namespace RTS.Units
{
    [RequireComponent(typeof(SphereCollider))]
    public class DamageableSensor : MonoBehaviour
    {
        public List<IDamageable> Damageables => visibleDamageables.Where(d => d != null && d.Transform != null).ToList();
        [field: SerializeField] public Owner Owner { get; set; }

        public delegate void UnitDetectionEvent(IDamageable damageable);
        public event UnitDetectionEvent OnUnitEnter;
        public event UnitDetectionEvent OnUnitExit;

        private new SphereCollider collider;
        private HashSet<IDamageable> visibleDamageables = new();
        private HashSet<IDamageable> allDamageables = new();

        private void Awake()
        {
            collider = GetComponent<SphereCollider>();
        }

        private void OnTriggerEnter(Collider collider)
        {
            if (!collider.TryGetComponent(out IDamageable damageable)) return;

            if (Owner == RTS.Units.Owner.Player1 && damageable.Owner == RTS.Units.Owner.Player1)
                return;

            allDamageables.Add(damageable);

            if (collider.TryGetComponent(out IHideable hideable))
            {
                hideable.OnVisibilityChanged += HandleVisibilityChange;
                if (hideable.IsVisibleTo(Owner))
                {
                    visibleDamageables.Add(damageable);
                    OnUnitEnter?.Invoke(damageable);
                }
            }
            else
            {
                visibleDamageables.Add(damageable);
                OnUnitEnter?.Invoke(damageable);
            }

            if (allDamageables.Count == 1)
            {
                Bus<UnitDeathEvent>.RegisterForAll(HandleUnitDeath);
            }
        }

        private void OnTriggerExit(Collider collider)
        {
            if (collider.TryGetComponent(out IDamageable damageable)
                && allDamageables.Remove(damageable) && visibleDamageables.Remove(damageable))
            {
                OnUnitExit?.Invoke(damageable);
            }

            if (collider.TryGetComponent(out IHideable hideable))
            {
                hideable.OnVisibilityChanged -= HandleVisibilityChange;
            }

            if (allDamageables.Count == 0)
            {
                Bus<UnitDeathEvent>.UnregisterForAll(HandleUnitDeath);
            }
        }

        private void OnDestroy()
        {
            foreach (IDamageable damageable in allDamageables.ToArray())
            {
                if (damageable == null) continue;

                Transform t = damageable.Transform;
                if (t == null) continue;

                if (t.TryGetComponent(out IHideable hideable))
                {
                    hideable.OnVisibilityChanged -= HandleVisibilityChange;
                }
            }

            Bus<UnitDeathEvent>.UnregisterForAll(HandleUnitDeath);

            allDamageables.Clear();
            visibleDamageables.Clear();
        }

        private void HandleVisibilityChange(IHideable hideable, Owner visibilityOwner, bool isVisible)
        {
            if (visibilityOwner != Owner) return;
            if (hideable == null) return;

            Transform t = hideable.Transform;
            if (t == null) return;

            IDamageable damageable = t.GetComponent<IDamageable>();
            if (damageable == null) return;

            if (damageable.Owner == Owner) return;
            if (damageable.Owner == Owner.Invalid) return;
            if (damageable.Owner == Owner.Unowned) return;

            if (isVisible)
            {
                if (visibleDamageables.Add(damageable))
                {
                    OnUnitEnter?.Invoke(damageable);
                }
            }
            else
            {
                if (visibleDamageables.Remove(damageable))
                {
                    OnUnitExit?.Invoke(damageable);
                }
            }
        }

        private void HandleUnitDeath(UnitDeathEvent evt)
        {
            if (evt.Unit == null) return;
            if (!allDamageables.Contains(evt.Unit)) return;

            Collider col = evt.Unit.GetComponent<Collider>();
            if (col == null)
            {
                allDamageables.Remove(evt.Unit);
                visibleDamageables.Remove(evt.Unit);
                OnUnitExit?.Invoke(evt.Unit);
                return;
            }

            OnTriggerExit(col);
        }

        public void SetupFrom(AttackConfigSO attackConfig)
        {
            collider.radius = attackConfig.AttackRange;
        }
    }
}