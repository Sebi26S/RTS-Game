using System;
using System.Collections.Generic;
using System.Linq;
using RTS.Commands;
using RTS.EventBus;
using RTS.Events;
using RTS.Player;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RTS.Units
{
    public abstract class AbstractCommandable : MonoBehaviour, ISelectable, IDamageable, IHideable
    {
        [Header("State")]
        [field: SerializeField] public bool IsSelected { get; protected set; }
        [field: SerializeField] public int CurrentHealth { get; protected set; }
        [field: SerializeField] public int MaxHealth { get; protected set; }

        public IDamageable LastAttacker { get; private set; }

        public delegate void DamagedEvent(AbstractCommandable commandable, IDamageable attacker);
        public event DamagedEvent OnDamaged;

        public Transform Transform => this == null ? null : transform;

        [Header("Ownership")]
        [SerializeField] private Owner owner = Owner.Invalid;

        public Owner Owner
        {
            get => owner;
            set
            {
                if (owner == value) return;

                if (started)
                {
                    Bus<UpgradeResearchedEvent>.OnEvent[owner] -= HandleUpgradeResearched;
                }

                owner = value;

                if (started)
                {
                    Bus<UpgradeResearchedEvent>.OnEvent[owner] += HandleUpgradeResearched;
                }

                RefreshVision();
                RefreshMinimapState();
                RefreshHealthUIOwner();
                RefreshUnitUIVisibility();
                ApplyPlayerVisibilityPresentation();
            }
        }

        [Header("Visibility")]
        [SerializeField] private List<Owner> visibleToOwners = new();
        [SerializeField] private List<Owner> everVisibleToOwners = new();
        private HashSet<Owner> visibleOwnerSet = new();
        private HashSet<Owner> everVisibleOwnerSet = new();

        public bool IsVisibleTo(Owner owner) => visibleOwnerSet.Contains(owner);
        public bool WasEverVisibleTo(Owner owner) => everVisibleOwnerSet.Contains(owner);
        private Transform minimapOriginalParent;
        private bool minimapDetached;
        private GameObject lastKnownMarkerObject;
        private bool wasVisibleToPlayerLastFrame;
        private FogVisibilityManager playerFogVisibilityManager;

        [Header("Vision Layers")]
        [SerializeField] private string playerVisionLayerName = "Fog of War Vision";
        [SerializeField] private string ai1VisionLayerName = "Fog of War Vision AI";

        [Header("Commands")]
        [field: SerializeField] public BaseCommand[] AvailableCommands { get; private set; }

        [Header("Config")]
        [field: SerializeField] public AbstractUnitSO UnitSO { get; private set; }

        [Header("References")]
        [SerializeField] protected DecalProjector decalProjector;
        [SerializeField] protected Transform VisionTransform;
        [SerializeField] protected Renderer MinimapRenderer;

        [Header("UI")]
        [SerializeField] private HealthTracker healthTracker;
        [SerializeField] private GameObject unitUIRoot;

        [Header("Minimap Colors (Inspector)")]
        [SerializeField] private Color player1MinimapColor = Color.green;
        [SerializeField] private Color enemyMinimapColor = Color.red;

        public delegate void HealthUpdatedEvent(AbstractCommandable commandable, int lastHealth, int newHealth);
        public event HealthUpdatedEvent OnHealthUpdated;

        public event IHideable.VisibilityChangeEvent OnVisibilityChanged;

        private BaseCommand[] initialCommands;
        private Renderer[] renderers = Array.Empty<Renderer>();
        private ParticleSystem[] particleSystems = Array.Empty<ParticleSystem>();

        private static readonly int COLOR_ID = Shader.PropertyToID("_BaseColor");

        private bool started = false;

        protected virtual void Awake()
        {
            UnitSO = UnitSO.Clone() as AbstractUnitSO;

            Renderer[] allRenderers = GetComponentsInChildren<Renderer>(true);
            particleSystems = GetComponentsInChildren<ParticleSystem>(true);

            List<Renderer> filteredRenderers = new();
            foreach (Renderer r in allRenderers)
            {
                if (r == null) continue;

                if (VisionTransform != null && r.transform.IsChildOf(VisionTransform))
                    continue;

                filteredRenderers.Add(r);
            }

            renderers = filteredRenderers.ToArray();

            visibleOwnerSet = new HashSet<Owner>(visibleToOwners);
            everVisibleOwnerSet = new HashSet<Owner>(everVisibleToOwners);

            EnsureMinimapMaterialInstance();

            if (MinimapRenderer != null)
            {
                minimapOriginalParent = MinimapRenderer.transform.parent;
            }
        }

        protected virtual void Start()
        {
            started = true;

            foreach (Owner owner in visibleOwnerSet)
            {
                everVisibleOwnerSet.Add(owner);
            }
            SyncVisibilityListsForInspector();

            RefreshVision();

            initialCommands = UnitSO.Prefab.GetComponent<AbstractCommandable>().AvailableCommands;
            SetCommandOverrides(null);

            playerFogVisibilityManager = FogVisibilityManager.Get(Owner.Player1);
            wasVisibleToPlayerLastFrame = IsVisibleTo(Owner.Player1);

            RefreshMinimapState();

            Bus<UpgradeResearchedEvent>.OnEvent[Owner] += HandleUpgradeResearched;

            RefreshHealthUIOwner();
            RefreshHealthUIValues();
            RefreshUnitUIVisibility();

            ApplyPlayerVisibilityPresentation();
        }
        protected virtual void LateUpdate()
        {
            if (Owner == Owner.Player1) return;
            if (lastKnownMarkerObject == null) return;

            if (IsVisibleTo(Owner.Player1))
            {
                DestroyLastKnownMarker();
                return;
            }

            if (IsLastKnownMarkerLocationVisibleToPlayer())
            {
                DestroyLastKnownMarker();
            }
        }

        protected virtual void OnDestroy()
        {
            if (started)
            {
                Bus<UpgradeResearchedEvent>.OnEvent[Owner] -= HandleUpgradeResearched;
            }

            if (UnitSO.PopulationConfig != null)
            {
                Bus<PopulationEvent>.Raise(Owner, new PopulationEvent(
                    Owner,
                    -UnitSO.PopulationConfig.PopulationCost,
                    -UnitSO.PopulationConfig.PopulationSupply
                ));
            }

            DestroyLastKnownMarker();
        }

        private void SyncVisibilityListsForInspector()
        {
            visibleToOwners = new List<Owner>(visibleOwnerSet);
            everVisibleToOwners = new List<Owner>(everVisibleOwnerSet);
        }

        protected void ApplyVisionLayer()
        {
            if (VisionTransform == null) return;

            string layerName = null;

            switch (Owner)
            {
                case Owner.Player1:
                    layerName = playerVisionLayerName;
                    break;

                case Owner.AI1:
                    layerName = ai1VisionLayerName;
                    break;

                default:
                    return;
            }

            int layer = LayerMask.NameToLayer(layerName);
            if (layer < 0)
            {
                return;
            }

            SetLayerRecursively(VisionTransform.gameObject, layer);
        }

        protected void SetLayerRecursively(GameObject target, int layer)
        {
            if (target == null) return;

            target.layer = layer;

            foreach (Transform child in target.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        public virtual void Select()
        {
            if (Owner != Owner.Player1) return;

            if (decalProjector != null)
                decalProjector.gameObject.SetActive(true);

            IsSelected = true;
            Bus<UnitSelectedEvent>.Raise(Owner, new UnitSelectedEvent(this));
        }

        public virtual void Deselect()
        {
            if (Owner != Owner.Player1) return;

            if (decalProjector != null)
                decalProjector.gameObject.SetActive(false);

            IsSelected = false;

            SetCommandOverrides(null);
            Bus<UnitDeselectedEvent>.Raise(Owner, new UnitDeselectedEvent(this));
        }

        public void SetCommandOverrides(BaseCommand[] commands)
        {
            AvailableCommands = (commands == null || commands.Length == 0) ? initialCommands : commands;

            if (IsSelected)
                Bus<UnitSelectedEvent>.Raise(Owner, new UnitSelectedEvent(this));
        }

        public void TakeDamage(int damage, IDamageable source)
        {
            int lastHealth = CurrentHealth;
            CurrentHealth = Mathf.Max(CurrentHealth - damage, 0);

            LastAttacker = source;

            OnHealthUpdated?.Invoke(this, lastHealth, CurrentHealth);
            OnDamaged?.Invoke(this, source);
            RefreshHealthUIValues();

            if (CurrentHealth == 0)
                Die();
        }

        public void Heal(int amount)
        {
            int lastHealth = CurrentHealth;
            CurrentHealth = Mathf.Clamp(CurrentHealth + amount, 0, MaxHealth);

            OnHealthUpdated?.Invoke(this, lastHealth, CurrentHealth);
            RefreshHealthUIValues();
        }

        public void SetHealthDirect(int currentHealth)
        {
            int lastHealth = CurrentHealth;
            CurrentHealth = Mathf.Clamp(currentHealth, 0, MaxHealth);

            OnHealthUpdated?.Invoke(this, lastHealth, CurrentHealth);
            RefreshHealthUIValues();
        }

        public void Die()
        {
            Destroy(gameObject);
        }

        public void SetVisibleForOwner(Owner owner, bool isVisible)
        {
            bool wasVisible = visibleOwnerSet.Contains(owner);
            bool changed = wasVisible != isVisible;

            if (isVisible)
            {
                visibleOwnerSet.Add(owner);
                everVisibleOwnerSet.Add(owner);
            }
            else
            {
                visibleOwnerSet.Remove(owner);
            }

            if (changed)
            {
                SyncVisibilityListsForInspector();
                OnVisibilityChanged?.Invoke(this, owner, isVisible);
            }
            else
            {
                SyncVisibilityListsForInspector();
            }

            if (owner == Owner.Player1)
            {
                ApplyPlayerVisibilityPresentation();
                RefreshUnitUIVisibility();
                RefreshMinimapState();
            }
        }

        private void ApplyPlayerVisibilityPresentation()
        {
            bool shouldBeVisible = Owner == Owner.Player1 || IsVisibleTo(Owner.Player1);

            if (shouldBeVisible)
                OnGainVisibility();
            else
                OnLoseVisibility();
        }

        protected virtual void OnGainVisibility()
        {
            foreach (Renderer r in renderers)
            {
                if (r != null)
                    r.enabled = true;
            }

            foreach (ParticleSystem ps in particleSystems)
            {
                if (ps != null)
                    ps.gameObject.SetActive(true);
            }
        }

        protected virtual void OnLoseVisibility()
        {
            foreach (Renderer r in renderers)
            {
                if (r != null)
                    r.enabled = false;
            }

            foreach (ParticleSystem ps in particleSystems)
            {
                if (ps != null)
                    ps.gameObject.SetActive(false);
            }
        }

        private void EnsureMinimapMaterialInstance()
        {
            if (MinimapRenderer == null) return;

            if (Application.isPlaying && MinimapRenderer.sharedMaterial != null)
            {
                MinimapRenderer.material = new Material(MinimapRenderer.sharedMaterial);
            }
        }

        private void AttachMinimapToUnit()
        {
            if (MinimapRenderer == null || !minimapDetached) return;

            MinimapRenderer.transform.SetParent(minimapOriginalParent, true);
            minimapDetached = false;
        }

        private void DetachMinimapAtCurrentPosition()
        {
            if (MinimapRenderer == null || minimapDetached) return;

            MinimapRenderer.transform.SetParent(null, true);
            minimapDetached = true;
        }


        protected void RefreshMinimapState()
        {
            if (MinimapRenderer == null) return;

            if (Owner == Owner.Player1)
            {
                MinimapRenderer.enabled = true;
                MinimapRenderer.material.SetColor(COLOR_ID, player1MinimapColor);
                DestroyLastKnownMarker();
                return;
            }

            bool isVisible = IsVisibleTo(Owner.Player1);
            bool wasEverVisible = WasEverVisibleTo(Owner.Player1);

            if (isVisible)
            {
                MinimapRenderer.enabled = true;
                MinimapRenderer.material.SetColor(COLOR_ID, enemyMinimapColor);

                DestroyLastKnownMarker();
                wasVisibleToPlayerLastFrame = true;
                return;
            }

            MinimapRenderer.enabled = false;

            if (wasVisibleToPlayerLastFrame && wasEverVisible)
            {
                CreateLastKnownMarker();
                UpdateLastKnownMarkerPosition();
            }

            if (!isVisible && wasEverVisible && lastKnownMarkerObject != null && IsLastKnownMarkerLocationVisibleToPlayer())
            {
                DestroyLastKnownMarker();
            }

            wasVisibleToPlayerLastFrame = false;
        }

        private void CreateLastKnownMarker()
        {
            if (MinimapRenderer == null) return;
            if (lastKnownMarkerObject != null) return;

            lastKnownMarkerObject = Instantiate(
                MinimapRenderer.gameObject,
                MinimapRenderer.transform.position,
                MinimapRenderer.transform.rotation
            );

            lastKnownMarkerObject.transform.localScale = MinimapRenderer.transform.lossyScale;

            Renderer markerRenderer = lastKnownMarkerObject.GetComponent<Renderer>();
            if (markerRenderer != null)
            {
                if (markerRenderer.sharedMaterial != null)
                {
                    markerRenderer.material = new Material(markerRenderer.sharedMaterial);
                }

                markerRenderer.material.SetColor(COLOR_ID, enemyMinimapColor);
                markerRenderer.enabled = true;
            }
        }
        private void DestroyLastKnownMarker()
        {
            if (lastKnownMarkerObject == null) return;

            Destroy(lastKnownMarkerObject);
            lastKnownMarkerObject = null;
        }
        private void UpdateLastKnownMarkerPosition()
        {
            if (lastKnownMarkerObject == null || MinimapRenderer == null) return;

            lastKnownMarkerObject.transform.position = MinimapRenderer.transform.position;
            lastKnownMarkerObject.transform.rotation = MinimapRenderer.transform.rotation;
        }
        private bool IsLastKnownMarkerLocationVisibleToPlayer()
        {
            if (lastKnownMarkerObject == null) return false;

            if (playerFogVisibilityManager == null)
            {
                playerFogVisibilityManager = FogVisibilityManager.Get(Owner.Player1);
            }

            if (playerFogVisibilityManager == null) return false;

            return playerFogVisibilityManager.IsVisible(lastKnownMarkerObject.transform.position);
        }

        protected void RefreshVision()
        {
            if (UnitSO?.SightConfig == null || VisionTransform == null) return;

            float size = UnitSO.SightConfig.SightRadius * 2f;
            VisionTransform.localScale = new Vector3(size, size, size);

            ApplyVisionLayer();
            VisionTransform.gameObject.SetActive(Owner != Owner.Invalid && Owner != Owner.Unowned);
        }

        private void RefreshHealthUIOwner()
        {
            if (healthTracker == null) return;
            healthTracker.SetOwner(Owner);
        }

        private void RefreshHealthUIValues()
        {
            if (healthTracker == null) return;
            healthTracker.UpdateBar(CurrentHealth, MaxHealth);
        }

        private void RefreshUnitUIVisibility()
        {
            if (unitUIRoot == null) return;

            bool showUI = Owner == Owner.Player1 || IsVisibleTo(Owner.Player1);
            unitUIRoot.SetActive(showUI);
        }

        private void HandleUpgradeResearched(UpgradeResearchedEvent evt)
        {
            if (evt.Owner == Owner && UnitSO.Upgrades.Contains(evt.Upgrade))
            {
                evt.Upgrade.Apply(UnitSO);
            }
        }
    }
}

