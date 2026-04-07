using System.Collections;
using System.Collections.Generic;
using RTS.Commands;
using RTS.EventBus;
using RTS.Events;
using RTS.Player;
using RTS.TechTree;
using UnityEngine;
using UnityEngine.AI;

namespace RTS.Units
{
    public class BaseBuilding : AbstractCommandable
    {
        public int QueueSize => buildingQueue.Count;
        public int MaxQueueSize => MAX_QUEUE_SIZE;
        public bool IsQueueFull => buildingQueue.Count >= MAX_QUEUE_SIZE;
        public UnlockableSO[] Queue => buildingQueue.ToArray();

        [field: SerializeField] public float CurrentQueueStartTime { get; private set; }
        [field: SerializeField] public UnlockableSO SOBeingBuilt { get; private set; }

        [field: SerializeField] public MeshRenderer MainRenderer { get; private set; }

        [field: SerializeField]
        public BuildingProgress Progress { get; private set; } = new(
            BuildingProgress.BuildingState.Destroyed, 0, 0
        );

        [field: SerializeField] public BuildingSO BuildingSO { get; private set; }

        [Header("Visual/Physics")]
        [SerializeField] private Material primaryMaterial;
        [SerializeField] private NavMeshObstacle navMeshObstacle;
        [SerializeField] private new Collider collider;

        [Header("Commands")]
        [SerializeField] private CancelBuildingCommand cancelBuildingCommand;

        [Header("Spawn Settings")]
        [SerializeField] private bool startAsCompleted = true;

        public delegate void QueueUpdatedEvent(UnlockableSO[] unitsInQueue);
        public event QueueUpdatedEvent OnQueueUpdated;

        private bool unitHasSubtractedPopulationCost;
        private IBuildingBuilder unitBuildingThis;

        private readonly List<UnlockableSO> buildingQueue = new(MAX_QUEUE_SIZE);
        private const int MAX_QUEUE_SIZE = 5;

        private bool completedInitDone = false;

        public void SetStartAsCompleted(bool value) => startAsCompleted = value;

        protected override void Awake()
        {
            base.Awake();

            BuildingSO = UnitSO as BuildingSO;
            MaxHealth = BuildingSO.Health;
        }

        protected override void Start()
        {
            base.Start();

            if (!startAsCompleted)
            {
                SetupAsConstructionSite();
                return;
            }

            CompleteConstruction();
        }

        private void SetupAsConstructionSite()
        {
            completedInitDone = false;

            Progress = new BuildingProgress(BuildingProgress.BuildingState.Destroyed, 0, 0);
            CurrentHealth = 0;

            if (collider != null) collider.enabled = true;
            if (navMeshObstacle != null) navMeshObstacle.enabled = true;

            if (MainRenderer != null && BuildingSO != null && BuildingSO.PlacementMaterial != null)
            {
                MainRenderer.material = BuildingSO.PlacementMaterial;
            }
        }

        public void StartBuilding(IBuildingBuilder buildingBuilder)
        {
            unitBuildingThis = buildingBuilder;

            Owner = unitBuildingThis.Owner;

            if (BuildingSO != null && BuildingSO.PlacementMaterial != null && MainRenderer != null)
            {
                MainRenderer.material = BuildingSO.PlacementMaterial;
            }

            SetCommandOverrides(new BaseCommand[] { cancelBuildingCommand });

            Progress = new BuildingProgress(
                BuildingProgress.BuildingState.Building,
                Time.time - BuildingSO.BuildTime * Progress.Progress01,
                Progress.Progress01
            );

            if (Progress.Progress01 == 0)
            {
                Heal(1);
            }

            if (collider != null) collider.enabled = true;
            if (navMeshObstacle != null) navMeshObstacle.enabled = true;

            Bus<UnitDeathEvent>.OnEvent[Owner] -= HandleUnitDeath;
            Bus<UnitDeathEvent>.OnEvent[Owner] += HandleUnitDeath;
        }

        public void CompleteConstruction()
        {
            if (completedInitDone) return;
            completedInitDone = true;

            if (MainRenderer != null && primaryMaterial != null)
            {
                MainRenderer.material = primaryMaterial;
            }

            Progress = new BuildingProgress(BuildingProgress.BuildingState.Completed, Progress.StartTime, 1f);
            CurrentHealth = MaxHealth;

            unitBuildingThis = null;

            SetCommandOverrides(null);

            if (collider != null) collider.enabled = true;
            if (navMeshObstacle != null) navMeshObstacle.enabled = true;

            Bus<UnitDeathEvent>.OnEvent[Owner] -= HandleUnitDeath;
            Bus<BuildingSpawnEvent>.Raise(Owner, new BuildingSpawnEvent(Owner, this));

            if (BuildingSO != null)
            {
                foreach (UpgradeSO upgrade in BuildingSO.Upgrades)
                {
                    if (BuildingSO.TechTree.IsResearched(Owner, upgrade))
                    {
                        upgrade.Apply(BuildingSO);
                    }
                }
            }

            if (!SaveSystem.IsLoadingFromSave && UnitSO != null && UnitSO.PopulationConfig != null)
            {
                Bus<PopulationEvent>.Raise(Owner, new PopulationEvent(
                    Owner,
                    UnitSO.PopulationConfig.PopulationCost,
                    UnitSO.PopulationConfig.PopulationSupply
                ));
            }

            if (VisionTransform != null)
            {
                ApplyVisionLayer();
                VisionTransform.gameObject.SetActive(Owner != Owner.Invalid && Owner != Owner.Unowned);
            }
            else
            {
                Transform vision = transform.Find("Vision");
                if (vision != null)
                {
                    int layer = -1;

                    if (Owner == Owner.Player1)
                        layer = LayerMask.NameToLayer("Fog of War Vision");
                    else if (Owner == Owner.AI1)
                        layer = LayerMask.NameToLayer("Fog of War Vision AI");

                    if (layer >= 0)
                    {
                        SetLayerRecursively(vision.gameObject, layer);
                    }

                    vision.gameObject.SetActive(Owner != Owner.Invalid && Owner != Owner.Unowned);
                }
            }
        }

        public void CancelBuilding()
        {
            if (unitBuildingThis != null)
            {
                unitBuildingThis.CancelBuilding();
            }
            else
            {
                Destroy(gameObject);

                Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(
                    Owner,
                    Mathf.FloorToInt(0.75f * UnitSO.Cost.Minerals),
                    UnitSO.Cost.MineralsSO
                ));
                Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(
                    Owner,
                    Mathf.FloorToInt(0.75f * UnitSO.Cost.Wood),
                    UnitSO.Cost.WoodSO
                ));
                Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(
                    Owner,
                    Mathf.FloorToInt(0.75f * UnitSO.Cost.Stone),
                    UnitSO.Cost.StoneSO
                ));
            }
        }

        private void HandleUnitDeath(UnitDeathEvent evt)
        {
            if (!evt.Unit.TryGetComponent(out IBuildingBuilder buildingBuilder)) return;
            if (buildingBuilder != unitBuildingThis) return;

            Bus<UnitDeathEvent>.OnEvent[Owner] -= HandleUnitDeath;

            Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(
                Owner,
                Mathf.FloorToInt(0.75f * UnitSO.Cost.Minerals),
                UnitSO.Cost.MineralsSO
            ));
            Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(
                Owner,
                Mathf.FloorToInt(0.75f * UnitSO.Cost.Wood),
                UnitSO.Cost.WoodSO
            ));
            Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(
                Owner,
                Mathf.FloorToInt(0.75f * UnitSO.Cost.Stone),
                UnitSO.Cost.StoneSO
            ));

            Destroy(gameObject);
        }

        public void BuildUnlockable(UnlockableSO unlockable)
        {
            if (IsQueueFull)
            {
                Debug.LogWarning("BuildUnlockable ignored because the queue is already full.");
                return;
            }

            Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(Owner, -unlockable.Cost.Minerals, unlockable.Cost.MineralsSO));
            Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(Owner, -unlockable.Cost.Wood, unlockable.Cost.WoodSO));
            Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(Owner, -unlockable.Cost.Stone, unlockable.Cost.StoneSO));

            buildingQueue.Add(unlockable);
            if (buildingQueue.Count == 1)
            {
                StartCoroutine(DoBuildUnits());
            }
            else
            {
                OnQueueUpdated?.Invoke(buildingQueue.ToArray());
            }
        }

        public void CancelBuildingUnit(int index)
        {
            if (index < 0 || index >= buildingQueue.Count)
            {
                Debug.LogError("Attempting to cancel building a unit outside the bounds of the queue!");
                return;
            }

            UnlockableSO unlockableSO = buildingQueue[index];
            Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(Owner, unlockableSO.Cost.Minerals, unlockableSO.Cost.MineralsSO));
            Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(Owner, unlockableSO.Cost.Wood, unlockableSO.Cost.WoodSO));
            Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(Owner, unlockableSO.Cost.Stone, unlockableSO.Cost.StoneSO));

            buildingQueue.RemoveAt(index);

            if (index == 0)
            {
                StopAllCoroutines();

                if (unlockableSO is AbstractUnitSO unitSO && unitHasSubtractedPopulationCost)
                {
                    Bus<PopulationEvent>.Raise(Owner, new PopulationEvent(
                        Owner,
                        -unitSO.PopulationConfig.PopulationCost,
                        0
                    ));
                }

                if (buildingQueue.Count > 0) StartCoroutine(DoBuildUnits());
                else OnQueueUpdated?.Invoke(buildingQueue.ToArray());
            }
            else
            {
                OnQueueUpdated?.Invoke(buildingQueue.ToArray());
            }
        }

        private IEnumerator DoBuildUnits()
        {
            while (buildingQueue.Count > 0)
            {
                SOBeingBuilt = buildingQueue[0];
                CurrentQueueStartTime = Time.time;
                OnQueueUpdated?.Invoke(buildingQueue.ToArray());
                unitHasSubtractedPopulationCost = false;

                bool isUnit = SOBeingBuilt is AbstractUnitSO;
                if (isUnit)
                {
                    AbstractUnitSO unitSO = SOBeingBuilt as AbstractUnitSO;

                    int currentPopulation = Supplies.Population[Owner];
                    int populationCost = unitSO.PopulationConfig.PopulationCost;
                    int populationLimit = Supplies.PopulationLimit[Owner];

                    while (currentPopulation + populationCost > populationLimit)
                    {
                        yield return null;
                        CurrentQueueStartTime = Time.time;

                        currentPopulation = Supplies.Population[Owner];
                        populationLimit = Supplies.PopulationLimit[Owner];
                    }

                    Bus<PopulationEvent>.Raise(Owner, new PopulationEvent(
                        Owner,
                        unitSO.PopulationConfig.PopulationCost,
                        0
                    ));
                    unitHasSubtractedPopulationCost = true;
                }

                yield return new WaitForSeconds(SOBeingBuilt.BuildTime);

                if (isUnit)
                {
                    AbstractUnitSO unitSO = SOBeingBuilt as AbstractUnitSO;
                    GameObject instance = Instantiate(unitSO.Prefab, transform.position, Quaternion.identity);

                    if (instance.TryGetComponent(out AbstractCommandable commandable))
                        commandable.Owner = Owner;
                }
                else if (SOBeingBuilt is UpgradeSO upgrade)
                {
                    Bus<UpgradeResearchedEvent>.Raise(Owner, new UpgradeResearchedEvent(Owner, upgrade));
                }
                else if (SOBeingBuilt is ResourceConversionSO conversion)
                {
                    if (conversion.MineralsSO != null && conversion.MineralsGained > 0)
                    {
                        Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(
                            Owner,
                            conversion.MineralsGained,
                            conversion.MineralsSO
                        ));
                    }
                }

                buildingQueue.RemoveAt(0);
            }

            unitHasSubtractedPopulationCost = false;
            OnQueueUpdated?.Invoke(buildingQueue.ToArray());
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            Bus<UnitDeathEvent>.OnEvent[Owner] -= HandleUnitDeath;
            Bus<BuildingDeathEvent>.Raise(Owner, new BuildingDeathEvent(Owner, this));
        }

        public override void Deselect()
        {
            base.Deselect();
            if (Progress.State != BuildingProgress.BuildingState.Completed)
            {
                SetCommandOverrides(new BaseCommand[] { cancelBuildingCommand });
            }
        }

        public void SetObstacleActive(bool active)
        {
            if (navMeshObstacle != null) navMeshObstacle.enabled = active;
        }

        public void PauseBuilding()
        {
            if (Progress.State != BuildingProgress.BuildingState.Building) return;

            float progress01 = (Time.time - Progress.StartTime) / BuildingSO.BuildTime;

            Progress = new BuildingProgress(
                BuildingProgress.BuildingState.Paused,
                Progress.StartTime,
                Mathf.Clamp01(progress01)
            );
        }
    }
}














