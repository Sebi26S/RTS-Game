using System.Collections.Generic;
using RTS.EventBus;
using RTS.Events;
using RTS.Units;
using UnityEngine;

namespace RTS.Player
{
    [RequireComponent(typeof(Camera))]
    public class FogVisibilityManager : MonoBehaviour
    {
        [Header("Vision")]
        [SerializeField] private Owner visionOwner = Owner.Player1;

        public Owner VisionOwner => visionOwner;

        private static readonly Dictionary<Owner, FogVisibilityManager> InstancesByOwner = new();

        private Camera fogOfWarCamera;
        private Texture2D visionTexture;
        private Rect textureRect;

        private readonly HashSet<IHideable> hideables = new(1000);

        public static FogVisibilityManager Get(Owner owner)
        {
            InstancesByOwner.TryGetValue(owner, out FogVisibilityManager manager);
            return manager;
        }

        private void Awake()
        {
            if (InstancesByOwner.ContainsKey(visionOwner) && InstancesByOwner[visionOwner] != null && InstancesByOwner[visionOwner] != this)
            {
                Debug.LogError($"Multiple FogVisibilityManagers found for owner {visionOwner} on {name}.");
                enabled = false;
                return;
            }

            InstancesByOwner[visionOwner] = this;

            fogOfWarCamera = GetComponent<Camera>();

            if (fogOfWarCamera.targetTexture == null)
            {
                Debug.LogError($"{name} is missing a target texture on its Camera component.");
                enabled = false;
                return;
            }

            visionTexture = new Texture2D(
                fogOfWarCamera.targetTexture.width,
                fogOfWarCamera.targetTexture.height,
                TextureFormat.RGB24,
                false
            );

            textureRect = new Rect(0, 0, visionTexture.width, visionTexture.height);

            Bus<UnitSpawnEvent>.RegisterForAll(HandleUnitSpawn);
            Bus<UnitDeathEvent>.RegisterForAll(HandleUnitDeath);

            Bus<BuildingSpawnEvent>.RegisterForAll(HandleBuildingSpawn);
            Bus<BuildingDeathEvent>.RegisterForAll(HandleBuildingDeath);

            Bus<PlaceholderSpawnEvent>.RegisterForAll(HandlePlaceholderSpawn);
            Bus<PlaceholderDestroyEvent>.RegisterForAll(HandlePlaceholderDestroy);

            Bus<SupplySpawnEvent>.OnEvent[Owner.Unowned] += HandleSupplySpawn;
            Bus<SupplyDepletedEvent>.OnEvent[Owner.Unowned] += HandleSupplyDepleted;
        }

        private void OnDestroy()
        {
            if (InstancesByOwner.TryGetValue(visionOwner, out FogVisibilityManager manager) && manager == this)
            {
                InstancesByOwner.Remove(visionOwner);
            }

            Bus<UnitSpawnEvent>.UnregisterForAll(HandleUnitSpawn);
            Bus<UnitDeathEvent>.UnregisterForAll(HandleUnitDeath);

            Bus<BuildingSpawnEvent>.UnregisterForAll(HandleBuildingSpawn);
            Bus<BuildingDeathEvent>.UnregisterForAll(HandleBuildingDeath);

            Bus<PlaceholderSpawnEvent>.UnregisterForAll(HandlePlaceholderSpawn);
            Bus<PlaceholderDestroyEvent>.UnregisterForAll(HandlePlaceholderDestroy);

            Bus<SupplySpawnEvent>.OnEvent[Owner.Unowned] -= HandleSupplySpawn;
            Bus<SupplyDepletedEvent>.OnEvent[Owner.Unowned] -= HandleSupplyDepleted;
        }

        private void LateUpdate()
        {
            if (fogOfWarCamera == null || fogOfWarCamera.targetTexture == null || visionTexture == null)
                return;

            ReadPixelsToVisionTexture();

            foreach (IHideable hideable in hideables)
            {
                SetHideableVisibilityStatus(hideable);
            }
        }

        public bool IsVisible(Vector3 position)
        {
            Vector3 screenPoint = fogOfWarCamera.WorldToScreenPoint(position);

            int x = Mathf.RoundToInt(screenPoint.x);
            int y = Mathf.RoundToInt(screenPoint.y);

            if (x < 0 || x >= visionTexture.width || y < 0 || y >= visionTexture.height)
                return false;

            Color visibilityColor = visionTexture.GetPixel(x, y);
            return visibilityColor.r > 0.9f;
        }

        private void ReadPixelsToVisionTexture()
        {
            RenderTexture previousRenderTexture = RenderTexture.active;

            RenderTexture.active = fogOfWarCamera.targetTexture;
            visionTexture.ReadPixels(textureRect, 0, 0);
            visionTexture.Apply(false);

            RenderTexture.active = previousRenderTexture;
        }

        private void SetHideableVisibilityStatus(IHideable hideable)
        {
            if (hideable == null || hideable.Transform == null)
                return;

            hideable.SetVisibleForOwner(visionOwner, IsVisible(hideable.Transform.position));
        }

        private void HandleUnitSpawn(UnitSpawnEvent evt)
        {
            if (evt.Unit == null) return;

            if (evt.Unit.Owner != visionOwner)
            {
                hideables.Add(evt.Unit);
            }
        }

        private void HandleUnitDeath(UnitDeathEvent evt)
        {
            if (evt.Unit == null) return;
            hideables.Remove(evt.Unit);
        }

        private void HandleBuildingSpawn(BuildingSpawnEvent evt)
        {
            if (evt.Building == null) return;

            if (evt.Building.Owner != visionOwner)
            {
                hideables.Add(evt.Building);
            }
        }

        private void HandleBuildingDeath(BuildingDeathEvent evt)
        {
            if (evt.Building == null) return;
            hideables.Remove(evt.Building);
        }

        private void HandleSupplySpawn(SupplySpawnEvent evt)
        {
            if (evt.Supply == null) return;
            hideables.Add(evt.Supply);
        }

        private void HandleSupplyDepleted(SupplyDepletedEvent evt)
        {
            if (evt.Supply == null) return;
            hideables.Remove(evt.Supply);
        }

        private void HandlePlaceholderSpawn(PlaceholderSpawnEvent evt)
        {
            if (evt.Placeholder == null) return;
            hideables.Add(evt.Placeholder);
        }

        private void HandlePlaceholderDestroy(PlaceholderDestroyEvent evt)
        {
            if (evt.Placeholder == null) return;
            hideables.Remove(evt.Placeholder);
        }
    }
}