using System;
using System.Collections.Generic;
using RTS.EventBus;
using RTS.Events;
using RTS.Player;
using RTS.Units;
using UnityEngine;

namespace RTS.Environment
{
    public class GatherableSupply : MonoBehaviour, IGatherable, IHideable
    {
        [field: SerializeField] public SupplySO Supply { get; private set; }
        [field: SerializeField] public int Amount { get; private set; }
        [field: SerializeField] public bool IsBusy { get; private set; }
        [SerializeField] private List<Owner> visibleToOwners = new();
        [SerializeField] private List<Owner> everVisibleToOwners = new();

        public bool IsVisibleTo(Owner owner) => visibleOwnerSet.Contains(owner);
        public bool WasEverVisibleTo(Owner owner) => everVisibleOwnerSet.Contains(owner);

        private HashSet<Owner> visibleOwnerSet = new();
        private HashSet<Owner> everVisibleOwnerSet = new();
        public Transform Transform => this == null ? null : transform;

        private Placeholder culledVisuals;
        private Renderer[] renderers = Array.Empty<Renderer>();
        private ParticleSystem[] particleSystems = Array.Empty<ParticleSystem>();
        private bool hasBeenRestoredFromSave;

        public event IHideable.VisibilityChangeEvent OnVisibilityChanged;

        private void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>();
            particleSystems = GetComponentsInChildren<ParticleSystem>();

            visibleOwnerSet = new HashSet<Owner>(visibleToOwners);
            everVisibleOwnerSet = new HashSet<Owner>(everVisibleToOwners);
        }

        private void Start()
        {
            if (!hasBeenRestoredFromSave)
            {
                Amount = Supply.MaxAmount;
            }

            Bus<SupplySpawnEvent>.Raise(Owner.Unowned, new SupplySpawnEvent(this));
        }

        private void OnDestroy()
        {
            Bus<SupplyDepletedEvent>.Raise(Owner.Unowned, new SupplyDepletedEvent(this));
        }

        private void SyncVisibilityListsForInspector()
        {
            visibleToOwners = new List<Owner>(visibleOwnerSet);
            everVisibleToOwners = new List<Owner>(everVisibleOwnerSet);
        }

        public bool BeginGather()
        {
            if (IsBusy)
            {
                return false;
            }

            IsBusy = true;
            return true;
        }

        public int EndGather(int maxCanTake)
        {
            IsBusy = false;

            if (maxCanTake <= 0)
            {
                return 0;
            }

            int amountGathered = Mathf.Min(Supply.AmountPerGather, Amount, maxCanTake);
            Amount -= amountGathered;

            if (Amount <= 0)
            {
                SaveableEntityId idComponent = GetComponent<SaveableEntityId>();
                if (idComponent != null)
                {
                    DepletedResourceRegistry.MarkDepleted(idComponent.UniqueId);
                }

                Destroy(gameObject);
            }

            return amountGathered;
        }

        public void AbortGather()
        {
            IsBusy = false;
        }

        public void RestoreAmountFromSave(int amount)
        {
            hasBeenRestoredFromSave = true;
            Amount = Mathf.Clamp(amount, 0, Supply != null ? Supply.MaxAmount : amount);
            IsBusy = false;
        }

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

            if (owner == Owner.Player1)
            {
                if (isVisible) OnGainVisibility();
                else OnLoseVisibility();
            }
        }

        private void OnGainVisibility()
        {
            foreach (Renderer renderer in renderers)
            {
                renderer.enabled = true;
            }

            foreach (ParticleSystem particleSystem in particleSystems)
            {
                particleSystem.gameObject.SetActive(true);
            }

            if (culledVisuals != null)
            {
                culledVisuals.gameObject.SetActive(false);
            }
        }

        private void OnLoseVisibility()
        {
            foreach (Renderer renderer in renderers)
            {
                renderer.enabled = false;
            }

            foreach (ParticleSystem particleSystem in particleSystems)
            {
                particleSystem.gameObject.SetActive(false);
            }

            if (culledVisuals == null)
            {
                MeshRenderer mainRenderer = GetComponentInChildren<MeshRenderer>();
                Transform originalRendererTransform = mainRenderer.transform;
                GameObject culledGO = new($"Culled {name} Visuals")
                {
                    layer = LayerMask.NameToLayer("Supplies"),
                    transform =
                    {
                        position = originalRendererTransform.position,
                        rotation = originalRendererTransform.rotation,
                        localScale = originalRendererTransform.localScale
                    }
                };
                culledVisuals = culledGO.AddComponent<Placeholder>();
                culledVisuals.ParentObject = gameObject;
                culledVisuals.Owner = Owner.Unowned;
                MeshFilter meshFilter = culledGO.AddComponent<MeshFilter>();
                meshFilter.mesh = mainRenderer.GetComponent<MeshFilter>().mesh;
                MeshRenderer renderer = culledGO.AddComponent<MeshRenderer>();
                renderer.materials = mainRenderer.materials;
            }
            else
            {
                culledVisuals.gameObject.SetActive(true);
            }
        }
    }
}