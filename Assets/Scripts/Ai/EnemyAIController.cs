using System.Collections;
using System.Collections.Generic;
using System.Text;
using RTS.Environment;
using RTS.Player;
using RTS.TechTree;
using RTS.Units;
using UnityEngine;
using UnityEngine.AI;

namespace RTS.AI
{
    public class EnemyAIController : MonoBehaviour
    {
        [Header("AI Config")]
        [SerializeField] private Owner aiOwner = Owner.AI1;
        [SerializeField] private float refreshInterval = 1f;
        [SerializeField] private bool debugLogState = true;
        [SerializeField] private bool debugOnlyLogWhenChanged = true;

        [Header("Building SO References")]
        [SerializeField] private BuildingSO commandPostSO;
        [SerializeField] private BuildingSO barracksSO;
        [SerializeField] private BuildingSO supplyHutSO;
        [SerializeField] private BuildingSO infantrySchoolSO;

        [Header("Unit SO References")]
        [SerializeField] private AbstractUnitSO workerSO;
        [SerializeField] private AbstractUnitSO scoutSO;
        [SerializeField] private AbstractUnitSO rangedSO;

        [Header("Economy")]
        [SerializeField] private SupplySO woodSO;
        [SerializeField] private SupplySO stoneSO;
        [SerializeField] private int desiredWoodWorkers = 1;
        [SerializeField] private int desiredStoneWorkers = 1;
        [SerializeField] private ResourceConversionSO stoneToMineralsConversionSO;
        [SerializeField] private bool convertStoneToMinerals = true;
        [SerializeField] private int desiredMineralsBuffer = 150;
        [SerializeField] private int minimumWoodStock = 150;
        [SerializeField] private int minimumStoneStock = 150;
        [SerializeField] private int maxQueuedConversionsPerSupplyHut = 1;

        [Header("Resource Search")]
        [SerializeField] private bool enableWorkerResourceSearch = true;
        [SerializeField] private float resourceSearchFromCommandPostRadius = 25f;
        [SerializeField] private float resourceSearchArrivalDistance = 2.5f;
        [SerializeField] private float resourceSearchPointMinSpacing = 6f;
        [SerializeField] private int resourceSearchPointAttempts = 12;
        [SerializeField] private float resourceSearchCommandInterval = 1.5f;

        [Header("Production")]
        [SerializeField] private int desiredWorkerCount = 6;
        [SerializeField] private bool trainWorkers = true;
        [SerializeField] private bool trainMilitaryUnits = true;
        [SerializeField] private int maxQueuedRangedPerBarracks = 1;
        [SerializeField] private int maxQueuedScoutPerBarracks = 1;

        [Header("Building")]
        [SerializeField] private bool buildSupplyHuts = true;
        [SerializeField] private bool buildMilitaryBuildings = true;
        [SerializeField] private int desiredBarracksCount = 1;
        [SerializeField] private int desiredInfantrySchoolCount = 1;
        [SerializeField] private int supplyBuffer = 2;
        [SerializeField] private float buildSearchStartRadius = 10f;
        [SerializeField] private float buildSearchStep = 3f;
        [SerializeField] private int buildSearchRings = 4;
        [SerializeField] private int buildSpotsPerRing = 12;
        [SerializeField] private float minDistanceFromBuildings = 6f;
        [SerializeField] private float navMeshSampleDistance = 4f;

        [Header("Scouting")]
        [SerializeField] private bool enableScouting = true;
        [SerializeField] private Transform worldBoundsRoot;
        [SerializeField] private float scoutArrivalDistance = 2.5f;
        [SerializeField] private float scoutEdgePadding = 6f;
        [SerializeField] private int scoutRandomPointAttempts = 20;
        [SerializeField] private float scoutNavMeshSampleDistance = 8f;
        [SerializeField] private float minDistanceFromLastScoutTarget = 12f;
        [SerializeField] private float enemyDetectionRadius = 12f;
        [SerializeField] private float scoutExplorationCellSize = 12f;
        [SerializeField] private float scoutVisitedPointRadius = 10f;
        [SerializeField] private int scoutExplorationMemoryPaddingCells = 1;
        

        [Header("Attack / Retreat")]
        [SerializeField] private bool enableAttacking = true;
        [SerializeField] private float attackCommandInterval = 2f;

        [SerializeField] private bool enableRetreat = true;
        [SerializeField] private int retreatThreshold = 2;
        [SerializeField] private float retreatCommandInterval = 2f;
        [SerializeField] private float retreatRadiusFromCommandPost = 8f;
        [SerializeField] private float retreatWhenAttackForceRemainingRatio = 0.4f;

        [Header("Attack Waves")]
        [SerializeField] private bool useAttackWaves = true;
        [SerializeField] private int currentWaveIndex = 0;
        [SerializeField] private int maxWaveIndex = 4; 

        [SerializeField] private int baseAttackThreshold = 5;
        [SerializeField] private int attackThresholdIncreasePerWave = 2;

        [SerializeField] private int baseDesiredRangedCount = 4;
        [SerializeField] private int rangedIncreasePerWave = 2;

        [SerializeField] private int fixedDesiredScoutCount = 1;

        [SerializeField] private float baseAttackCooldownAfterRetreat = 25f;
        [SerializeField] private float attackCooldownReductionPerWave = 1f;
        [SerializeField] private float minimumAttackCooldownAfterRetreat = 20f;

        [Header("Defense")]
        [SerializeField] private bool enableDefense = true;
        [SerializeField] private float defenseRadius = 20f;
        [SerializeField] private float defenseCommandInterval = 1.5f;

        [Header("Worker Defense")]
        [SerializeField] private bool sendIdleWorkersToCommandPost = true;
        [SerializeField] private float idleWorkerStandRadius = 6f;
        [SerializeField] private float idleWorkerReturnDistance = 10f;
        [SerializeField] private float idleWorkerCommandInterval = 2f;
        [SerializeField] private bool workersHelpOnDefense = true;

        private Worker activeResourceSearcher;
        private SupplyType searchedSupplyType;
        private Vector3 currentResourceSearchPoint;
        private bool hasResourceSearchPoint;
        private float nextResourceSearchCommandTime;

        private readonly HashSet<AbstractUnit> activeAttackForce = new();
        private float attackBlockedUntilTime;
        private int attackForceStartCount;
        private bool isRetreating;
        private float nextRetreatCommandTime;

        private float nextIdleWorkerCommandTime;
        private IDamageable currentDefenseTarget;
        private float nextDefenseCommandTime;
        private bool isDefending;

        private float nextAttackCommandTime;
        private bool hasLaunchedAttack;

        public BaseBuilding KnownEnemyTargetBuilding => knownEnemyTargetBuilding;
        public bool HasKnownEnemyTargetBuilding => knownEnemyTargetBuilding != null;

        private readonly List<BaseBuilding> knownEnemyBuildings = new();

        private AbstractUnit activeScoutExplorer;
        private Vector3 currentScoutTarget;
        private bool hasScoutTarget;
        private BaseBuilding knownEnemyTargetBuilding;
        private readonly Queue<Vector3> recentScoutTargets = new();
        private const int MAX_RECENT_SCOUT_TARGETS = 6;
        private readonly HashSet<Vector2Int> exploredScoutCells = new();

        private readonly List<AbstractUnit> allUnits = new();
        private readonly List<Worker> workers = new();
        private readonly List<AbstractUnit> scouts = new();
        private readonly List<AbstractUnit> rangedUnits = new();

        private readonly List<BaseBuilding> allBuildings = new();

        private readonly List<BaseBuilding> allBarrackses = new();
        private readonly List<BaseBuilding> completedBarrackses = new();

        private readonly List<BaseBuilding> allSupplyHuts = new();
        private readonly List<BaseBuilding> completedSupplyHuts = new();

        private readonly List<BaseBuilding> allInfantrySchools = new();
        private readonly List<BaseBuilding> completedInfantrySchools = new();

        private readonly Dictionary<Worker, SupplyType> workerAssignments = new();

        private BaseBuilding commandPost;
        private BaseBuilding completedCommandPost;

        private BaseBuilding pendingSupplyHutConstruction;
        private BaseBuilding pendingBarracksConstruction;
        private BaseBuilding pendingInfantrySchoolConstruction;

        private Worker pendingSupplyHutBuilder;
        private Worker pendingBarracksBuilder;
        private Worker pendingInfantrySchoolBuilder;

        private float pendingSupplyHutStartTime;
        private float pendingBarracksStartTime;
        private float pendingInfantrySchoolStartTime;

        [SerializeField] private float pendingBuildTimeout = 8f;

        private string lastDebugSnapshot;

        public Owner AIOwner => aiOwner;

        public IReadOnlyList<AbstractUnit> AllUnits => allUnits;
        public IReadOnlyList<Worker> Workers => workers;
        public IReadOnlyList<AbstractUnit> Scouts => scouts;
        public IReadOnlyList<AbstractUnit> RangedUnits => rangedUnits;

        public IReadOnlyList<BaseBuilding> AllBuildings => allBuildings;

        public IReadOnlyList<BaseBuilding> AllBarrackses => allBarrackses;
        public IReadOnlyList<BaseBuilding> CompletedBarrackses => completedBarrackses;

        public IReadOnlyList<BaseBuilding> AllSupplyHuts => allSupplyHuts;
        public IReadOnlyList<BaseBuilding> CompletedSupplyHuts => completedSupplyHuts;

        public IReadOnlyList<BaseBuilding> AllInfantrySchools => allInfantrySchools;
        public IReadOnlyList<BaseBuilding> CompletedInfantrySchools => completedInfantrySchools;

        public BaseBuilding CommandPost => commandPost;
        public BaseBuilding CompletedCommandPost => completedCommandPost;

        public int WorkerCount => workers.Count;
        public int ScoutCount => scouts.Count;
        public int RangedCount => rangedUnits.Count;
        public int MilitaryCount => scouts.Count + rangedUnits.Count;

        public int BarracksCount => allBarrackses.Count;
        public int CompletedBarracksCount => completedBarrackses.Count;

        public int SupplyHutCount => allSupplyHuts.Count;
        public int CompletedSupplyHutCount => completedSupplyHuts.Count;

        public int InfantrySchoolCount => allInfantrySchools.Count;
        public int CompletedInfantrySchoolCount => completedInfantrySchools.Count;

        public bool HasCommandPost => commandPost != null;
        public bool HasCompletedCommandPost => completedCommandPost != null;

        private int ClampedWaveIndex => Mathf.Clamp(currentWaveIndex, 0, maxWaveIndex);

        private int CurrentWaveAttackThreshold =>
            baseAttackThreshold + ClampedWaveIndex * attackThresholdIncreasePerWave;

        private int CurrentWaveDesiredRangedCount =>
            baseDesiredRangedCount + ClampedWaveIndex * rangedIncreasePerWave;

        private int CurrentWaveDesiredScoutCount =>
            fixedDesiredScoutCount;

        private float CurrentWaveAttackCooldownAfterRetreat =>
            Mathf.Max(
                minimumAttackCooldownAfterRetreat,
                baseAttackCooldownAfterRetreat - ClampedWaveIndex * attackCooldownReductionPerWave
            );

        private void Start()
        {
            exploredScoutCells.Clear();
            StartCoroutine(AIRefreshLoop());
        }

        private IEnumerator AIRefreshLoop()
        {
            while (true)
            {
                RefreshAIState();

                RunDefense();

                if (!isDefending)
                {
                    RunEconomy();
                    RunResourceSearch();
                    RunProduction();
                    RunConversion();
                    RunBuilding();
                    RunScouting();
                    RunIdleWorkerDefensePositioning();
                    RunRetreating();
                    RunAttacking();
                }
                else
                {
                    StopRetreat(false);
                }

                if (debugLogState)
                {
                    LogStateIfNeeded();
                }

                yield return new WaitForSeconds(refreshInterval);
            }
        }

        public void RefreshAIState()
        {
            ClearRuntimeCollections();
            RefreshUnits();
            RefreshBuildings();
        }

        private void ClearRuntimeCollections()
        {
            allUnits.Clear();
            workers.Clear();
            scouts.Clear();
            rangedUnits.Clear();

            allBuildings.Clear();

            allBarrackses.Clear();
            completedBarrackses.Clear();

            allSupplyHuts.Clear();
            completedSupplyHuts.Clear();

            allInfantrySchools.Clear();
            completedInfantrySchools.Clear();

            commandPost = null;
            completedCommandPost = null;
        }

        private void RefreshUnits()
        {
            AbstractUnit[] foundUnits = FindObjectsByType<AbstractUnit>(FindObjectsSortMode.None);

            foreach (AbstractUnit unit in foundUnits)
            {
                if (unit == null) continue;
                if (unit.Owner != aiOwner) continue;

                allUnits.Add(unit);

                if (MatchesUnitSO(unit, workerSO) && unit is Worker worker)
                {
                    workers.Add(worker);
                    continue;
                }

                if (MatchesUnitSO(unit, scoutSO))
                {
                    scouts.Add(unit);
                    continue;
                }

                if (MatchesUnitSO(unit, rangedSO))
                {
                    rangedUnits.Add(unit);
                }
            }
        }

        private void RefreshBuildings()
        {
            BaseBuilding[] foundBuildings = FindObjectsByType<BaseBuilding>(FindObjectsSortMode.None);

            foreach (BaseBuilding building in foundBuildings)
            {
                if (building == null) continue;
                if (building.Owner != aiOwner) continue;

                allBuildings.Add(building);

                bool isCompleted = IsCompleted(building);

                if (MatchesBuildingSO(building, commandPostSO))
                {
                    commandPost ??= building;

                    if (isCompleted)
                    {
                        completedCommandPost ??= building;
                    }

                    continue;
                }

                if (MatchesBuildingSO(building, barracksSO))
                {
                    allBarrackses.Add(building);

                    if (isCompleted)
                    {
                        completedBarrackses.Add(building);
                    }

                    continue;
                }

                if (MatchesBuildingSO(building, supplyHutSO))
                {
                    allSupplyHuts.Add(building);

                    if (isCompleted)
                    {
                        completedSupplyHuts.Add(building);
                    }

                    continue;
                }

                if (MatchesBuildingSO(building, infantrySchoolSO))
                {
                    allInfantrySchools.Add(building);

                    if (isCompleted)
                    {
                        completedInfantrySchools.Add(building);
                    }
                }
            }
        }

        private void RunEconomy()
        {
            CleanupInvalidAssignments();
            ReleaseWorkersFromSatisfiedResources();
            AssignIdleWorkersIntelligently();
        }

        private void CleanupInvalidAssignments()
        {
            List<Worker> toRemove = new();

            foreach (KeyValuePair<Worker, SupplyType> pair in workerAssignments)
            {
                Worker worker = pair.Key;
                SupplyType assignedType = pair.Value;

                if (worker == null || worker.Transform == null || worker.CurrentHealth <= 0)
                {
                    toRemove.Add(worker);
                    continue;
                }

                if (worker.Owner != aiOwner)
                {
                    toRemove.Add(worker);
                    continue;
                }

                if (worker.IsBuilding || worker.IsHealing)
                {
                    toRemove.Add(worker);
                    continue;
                }

                if (!worker.IsGathering)
                {
                    toRemove.Add(worker);
                    continue;
                }

                if (worker.CurrentGatherType == null || worker.CurrentGatherType.Type != assignedType)
                {
                    toRemove.Add(worker);
                }
            }

            foreach (Worker worker in toRemove)
            {
                workerAssignments.Remove(worker);
            }
        }

        private void ReleaseWorkersFromSatisfiedResources()
        {
            List<Worker> toRelease = new();

            foreach (KeyValuePair<Worker, SupplyType> pair in workerAssignments)
            {
                Worker worker = pair.Key;
                SupplyType type = pair.Value;

                if (worker == null || worker.Transform == null || worker.CurrentHealth <= 0)
                {
                    toRelease.Add(worker);
                    continue;
                }

                if (NeedsMoreResourceStock(type))
                    continue;

                toRelease.Add(worker);
                worker.Stop();
            }

            foreach (Worker worker in toRelease)
            {
                workerAssignments.Remove(worker);
            }
        }

        private void AssignIdleWorkersIntelligently()
        {
            while (true)
            {
                int woodMissing = GetMissingWorkersFor(SupplyType.Wood);
                int stoneMissing = GetMissingWorkersFor(SupplyType.Stone);

                if (woodMissing == 0 && stoneMissing == 0)
                    return;

                SupplyType primaryType;
                SupplyType secondaryType;

                float woodPriority = GetGatherPriorityScore(SupplyType.Wood);
                float stonePriority = GetGatherPriorityScore(SupplyType.Stone);

                if (woodPriority >= stonePriority)
                {
                    primaryType = SupplyType.Wood;
                    secondaryType = SupplyType.Stone;
                }
                else
                {
                    primaryType = SupplyType.Stone;
                    secondaryType = SupplyType.Wood;
                }

                if (TryAssignWorkerToSupplyType(primaryType))
                    continue;

                if (TryAssignWorkerToSupplyType(secondaryType))
                    continue;

                return;
            }
        }

        private bool TryAssignWorkerToSupplyType(SupplyType targetType)
        {
            if (GetMissingWorkersFor(targetType) <= 0)
                return false;

            SupplySO targetSO = GetSupplySO(targetType);
            if (targetSO == null)
                return false;

            Vector3 referencePosition = completedCommandPost != null
                ? completedCommandPost.transform.position
                : transform.position;

            GatherableSupply nearestNode = FindClosestSupplyNode(targetSO, referencePosition);
            if (nearestNode == null)
                return false;

            Worker worker = FindWorkerForGatherTask(targetType, nearestNode.transform.position);
            if (worker == null)
                return false;

            worker.Gather(nearestNode);
            workerAssignments[worker] = targetType;
            return true;
        }

        private Worker FindWorkerForGatherTask(SupplyType targetType, Vector3 targetPosition)
        {
            Worker freeWorker = FindFreeWorker();
            if (freeWorker != null)
                return freeWorker;

            float targetPriority = GetGatherPriorityScore(targetType);

            Worker bestWorker = null;
            float bestScore = float.MaxValue;

            foreach (Worker worker in workers)
            {
                if (worker == null) continue;
                if (worker.Transform == null) continue;
                if (worker.CurrentHealth <= 0) continue;
                if (worker.IsBuilding) continue;
                if (worker.IsHealing) continue;
                if (!worker.IsGathering) continue;
                if (!workerAssignments.TryGetValue(worker, out SupplyType assignedType)) continue;
                if (assignedType == targetType) continue;

                float assignedPriority = GetGatherPriorityScore(assignedType);
                if (assignedPriority >= targetPriority)
                    continue;

                float distanceToTarget = Vector3.Distance(worker.transform.position, targetPosition);

                if (distanceToTarget < bestScore)
                {
                    bestScore = distanceToTarget;
                    bestWorker = worker;
                }
            }

            if (bestWorker != null)
            {
                workerAssignments.Remove(bestWorker);
                return bestWorker;
            }

            return null;
        }

        private int CountAssignedWorkers(SupplyType supplyType)
        {
            int count = 0;

            foreach (KeyValuePair<Worker, SupplyType> pair in workerAssignments)
            {
                Worker worker = pair.Key;
                if (worker == null) continue;
                if (pair.Value != supplyType) continue;

                count++;
            }

            return count;
        }

        private Worker FindFreeWorker()
        {
            foreach (Worker worker in workers)
            {
                if (worker == null) continue;
                if (worker.Transform == null) continue;
                if (worker.CurrentHealth <= 0) continue;
                if (worker.IsBuilding) continue;
                if (worker.IsHealing) continue;
                if (worker.IsGathering) continue;
                if (workerAssignments.ContainsKey(worker)) continue;
                if (isDefending) continue;

                return worker;
            }

            return null;
        }

        private GatherableSupply FindClosestSupplyNode(SupplySO targetSupplySO, Vector3 fromPosition)
        {
            GatherableSupply[] allSupplies = FindObjectsByType<GatherableSupply>(FindObjectsSortMode.None);

            GatherableSupply best = null;
            float bestDistanceSqr = float.MaxValue;

            foreach (GatherableSupply supply in allSupplies)
            {
                if (supply == null) continue;
                if (supply.Supply == null) continue;
                if (supply.Supply.Type != targetSupplySO.Type) continue;
                if (supply.Amount <= 0) continue;
                if (!supply.WasEverVisibleTo(aiOwner)) continue;

                float distanceSqr = (supply.transform.position - fromPosition).sqrMagnitude;

                if (distanceSqr < bestDistanceSqr)
                {
                    bestDistanceSqr = distanceSqr;
                    best = supply;
                }
            }

            return best;
        }

        private bool NeedsMoreResourceStock(SupplyType type)
        {
            return type switch
            {
                SupplyType.Wood => Supplies.Wood[aiOwner] < minimumWoodStock,
                SupplyType.Stone => Supplies.Stone[aiOwner] < minimumStoneStock,
                _ => false
            };
        }

        private int GetMissingWorkersFor(SupplyType type)
        {
            if (!NeedsMoreResourceStock(type))
                return 0;

            return type switch
            {
                SupplyType.Wood => Mathf.Max(0, desiredWoodWorkers - CountAssignedWorkers(SupplyType.Wood)),
                SupplyType.Stone => Mathf.Max(0, desiredStoneWorkers - CountAssignedWorkers(SupplyType.Stone)),
                _ => 0
            };
        }

        private bool ShouldGatherType(SupplyType type)
        {
            return GetMissingWorkersFor(type) > 0 && NeedsMoreResourceStock(type);
        }

        private float GetGatherPriorityScore(SupplyType type)
        {
            int missing = GetMissingWorkersFor(type);
            int assigned = CountAssignedWorkers(type);

            float score = missing * 100f;
            score -= assigned * 5f;

            if (type == SupplyType.Wood)
                score += 1f;

            return score;
        }

        private SupplySO GetSupplySO(SupplyType type)
        {
            return type switch
            {
                SupplyType.Wood => woodSO,
                SupplyType.Stone => stoneSO,
                _ => null
            };
        }

        private void RunResourceSearch()
        {
            if (isDefending) return;
            if (!enableWorkerResourceSearch) return;
            if (completedCommandPost == null) return;

            CleanupResourceSearchState();

            if (activeResourceSearcher == null)
            {
                TryStartResourceSearch();
                return;
            }

            if (HasKnownSupplyForType(searchedSupplyType))
            {
                SupplySO targetSO = searchedSupplyType == SupplyType.Wood ? woodSO : stoneSO;
                GatherableSupply node = FindClosestSupplyNode(targetSO, activeResourceSearcher.transform.position);

                if (node != null)
                {
                    activeResourceSearcher.Gather(node);
                    workerAssignments[activeResourceSearcher] = searchedSupplyType;
                }

                StopResourceSearch();
                return;
            }

            if (!hasResourceSearchPoint || HasWorkerReachedPoint(activeResourceSearcher, currentResourceSearchPoint))
            {
                if (TryGetRandomResourceSearchPoint(out Vector3 nextPoint))
                {
                    currentResourceSearchPoint = nextPoint;
                    hasResourceSearchPoint = true;
                }
                else
                {
                    return;
                }
            }

            if (Time.time >= nextResourceSearchCommandTime)
            {
                activeResourceSearcher.MoveTo(currentResourceSearchPoint);
                nextResourceSearchCommandTime = Time.time + resourceSearchCommandInterval;
            }
        }

        private void TryStartResourceSearch()
        {
            bool needsWood = ShouldGatherType(SupplyType.Wood);
            bool needsStone = ShouldGatherType(SupplyType.Stone);

            bool hasKnownWood = HasKnownSupplyForType(SupplyType.Wood);
            bool hasKnownStone = HasKnownSupplyForType(SupplyType.Stone);

            SupplyType? missingType = null;

            if (needsWood && !hasKnownWood)
                missingType = SupplyType.Wood;
            else if (needsStone && !hasKnownStone)
                missingType = SupplyType.Stone;

            if (missingType == null)
                return;

            Worker worker = FindFreeWorker();
            if (worker == null)
                return;

            activeResourceSearcher = worker;
            searchedSupplyType = missingType.Value;
            hasResourceSearchPoint = false;
            nextResourceSearchCommandTime = 0f;

            workerAssignments.Remove(worker);
        }

        private bool HasKnownSupplyForType(SupplyType type)
        {
            GatherableSupply[] allSupplies = FindObjectsByType<GatherableSupply>(FindObjectsSortMode.None);

            foreach (GatherableSupply supply in allSupplies)
            {
                if (supply == null) continue;
                if (supply.Supply == null) continue;
                if (supply.Supply.Type != type) continue;
                if (supply.Amount <= 0) continue;
                if (!supply.WasEverVisibleTo(aiOwner)) continue;

                return true;
            }

            return false;
        }

        private bool TryGetRandomResourceSearchPoint(out Vector3 point)
        {
            point = Vector3.zero;

            if (completedCommandPost == null)
                return false;

            Vector3 center = completedCommandPost.transform.position;

            for (int i = 0; i < resourceSearchPointAttempts; i++)
            {
                Vector2 circle = Random.insideUnitCircle * resourceSearchFromCommandPostRadius;
                Vector3 rawPoint = new Vector3(center.x + circle.x, center.y, center.z + circle.y);

                if (!NavMesh.SamplePosition(rawPoint, out NavMeshHit navHit, navMeshSampleDistance, NavMesh.AllAreas))
                    continue;

                if (hasResourceSearchPoint &&
                    Vector3.Distance(navHit.position, currentResourceSearchPoint) < resourceSearchPointMinSpacing)
                    continue;

                point = navHit.position;
                return true;
            }

            return false;
        }

        private bool HasWorkerReachedPoint(Worker worker, Vector3 point)
        {
            if (worker == null) return false;
            return Vector3.Distance(worker.transform.position, point) <= resourceSearchArrivalDistance;
        }

        private void CleanupResourceSearchState()
        {
            if (activeResourceSearcher == null)
            {
                hasResourceSearchPoint = false;
                return;
            }

            if (activeResourceSearcher.Transform == null)
            {
                StopResourceSearch();
                return;
            }

            if (activeResourceSearcher.Owner != aiOwner)
            {
                StopResourceSearch();
                return;
            }

            if (!workers.Contains(activeResourceSearcher))
            {
                StopResourceSearch();
                return;
            }

            if (activeResourceSearcher.CurrentHealth <= 0)
            {
                StopResourceSearch();
                return;
            }

            if (activeResourceSearcher.IsBuilding || activeResourceSearcher.IsHealing)
            {
                StopResourceSearch();
                return;
            }
        }

        private void StopResourceSearch()
        {
            activeResourceSearcher = null;
            hasResourceSearchPoint = false;
            nextResourceSearchCommandTime = 0f;
        }

        private void RunProduction()
        {
            if (trainWorkers)
            {
                TryTrainWorker();
            }

            if (trainMilitaryUnits)
            {
                TryTrainMilitaryFromBarracks();
            }
        }

        private void TryTrainWorker()
        {
            if (workerSO == null) return;
            if (completedCommandPost == null) return;
            if (WorkerCount >= desiredWorkerCount) return;
            if (IsWorkerAlreadyQueued(completedCommandPost)) return;
            if (!HasEnoughSupplies(workerSO, aiOwner)) return;
            if (!HasEnoughPopulation(workerSO, aiOwner)) return;

            completedCommandPost.BuildUnlockable(workerSO);
        }

        private bool IsWorkerAlreadyQueued(BaseBuilding building)
        {
            if (building == null || workerSO == null) return false;

            UnlockableSO[] queue = building.Queue;
            if (queue == null || queue.Length == 0) return false;

            foreach (UnlockableSO queuedItem in queue)
            {
                if (queuedItem == null) continue;

                if (queuedItem == workerSO)
                {
                    return true;
                }

                if (queuedItem is AbstractUnitSO queuedUnitSO && queuedUnitSO.Prefab == workerSO.Prefab)
                {
                    return true;
                }
            }

            return false;
        }

        private void TryTrainMilitaryFromBarracks()
        {
            if (completedBarrackses.Count == 0) return;

            TryTrainRangedFromBarracks();
            TryTrainScoutFromBarracks();
        }

        private void TryTrainRangedFromBarracks()
        {
            if (rangedSO == null) return;
            if (!IsUnlockedForAI(rangedSO)) return;

            int totalRanged = RangedCount + CountQueuedUnitsInCompletedBarrackses(rangedSO);
            if (totalRanged >= CurrentWaveDesiredRangedCount) return;

            foreach (BaseBuilding barracks in completedBarrackses)
            {
                if (barracks == null) continue;

                int queuedInThisBarracks = CountQueuedUnlockable(barracks, rangedSO);
                if (queuedInThisBarracks >= maxQueuedRangedPerBarracks) continue;

                if (!HasEnoughSupplies(rangedSO, aiOwner)) return;
                if (!HasEnoughPopulation(rangedSO, aiOwner)) return;

                barracks.BuildUnlockable(rangedSO);
                return;
            }
        }

        private void TryTrainScoutFromBarracks()
        {
            if (scoutSO == null) return;
            if (!IsUnlockedForAI(scoutSO)) return;

            int totalScouts = ScoutCount + CountQueuedUnitsInCompletedBarrackses(scoutSO);
            if (totalScouts >= CurrentWaveDesiredScoutCount) return;

            foreach (BaseBuilding barracks in completedBarrackses)
            {
                if (barracks == null) continue;

                int queuedInThisBarracks = CountQueuedUnlockable(barracks, scoutSO);
                if (queuedInThisBarracks >= maxQueuedScoutPerBarracks) continue;

                if (!HasEnoughSupplies(scoutSO, aiOwner)) return;
                if (!HasEnoughPopulation(scoutSO, aiOwner)) return;

                barracks.BuildUnlockable(scoutSO);
                return;
            }
        }

        private int CountQueuedUnitsInCompletedBarrackses(AbstractUnitSO unitSO)
        {
            if (unitSO == null) return 0;

            int count = 0;

            foreach (BaseBuilding barracks in completedBarrackses)
            {
                if (barracks == null) continue;
                count += CountQueuedUnlockable(barracks, unitSO);
            }

            return count;
        }

        private int CountQueuedUnlockable(BaseBuilding building, UnlockableSO target)
        {
            if (building == null || target == null) return 0;

            int count = 0;
            UnlockableSO[] queue = building.Queue;

            if (queue == null || queue.Length == 0) return 0;

            foreach (UnlockableSO queuedItem in queue)
            {
                if (queuedItem == null) continue;

                if (queuedItem == target)
                {
                    count++;
                    continue;
                }

                if (queuedItem is AbstractUnitSO queuedUnitSO
                    && target is AbstractUnitSO targetUnitSO
                    && queuedUnitSO.Prefab == targetUnitSO.Prefab)
                {
                    count++;
                }
            }

            return count;
        }

        private void RunConversion()
        {
            if (!convertStoneToMinerals) return;
            if (stoneToMineralsConversionSO == null) return;
            if (completedSupplyHuts.Count == 0) return;

            TryQueueStoneToMinerals();
        }

        private void TryQueueStoneToMinerals()
        {
            int currentMinerals = Supplies.Minerals[aiOwner];

            if (currentMinerals >= desiredMineralsBuffer)
                return;

            foreach (BaseBuilding supplyHut in completedSupplyHuts)
            {
                if (supplyHut == null) continue;

                int queuedConversions = CountQueuedConversions(supplyHut, stoneToMineralsConversionSO);
                if (queuedConversions >= maxQueuedConversionsPerSupplyHut)
                    continue;

                if (!HasEnoughSupplies(stoneToMineralsConversionSO, aiOwner))
                    return;

                supplyHut.BuildUnlockable(stoneToMineralsConversionSO);
                return;
            }
        }

        private int CountQueuedConversions(BaseBuilding building, ResourceConversionSO targetConversion)
        {
            if (building == null || targetConversion == null)
                return 0;

            int count = 0;
            UnlockableSO[] queue = building.Queue;

            if (queue == null || queue.Length == 0)
                return 0;

            foreach (UnlockableSO queuedItem in queue)
            {
                if (queuedItem == null) continue;

                if (queuedItem == targetConversion)
                {
                    count++;
                    continue;
                }

                if (queuedItem is ResourceConversionSO queuedConversion && queuedConversion == targetConversion)
                {
                    count++;
                }
            }

            return count;
        }

        private void RunBuilding()
        {
            CleanupPendingConstructionReferences();

            if (buildSupplyHuts)
            {
                TryBuildSupplyHut();
            }

            if (buildMilitaryBuildings)
            {
                TryBuildBarracks();
                TryBuildInfantrySchool();
            }
        }

        private void CleanupPendingConstructionReferences()
        {
            CleanupPendingBuilding(
                ref pendingSupplyHutConstruction,
                ref pendingSupplyHutBuilder,
                ref pendingSupplyHutStartTime
            );

            CleanupPendingBuilding(
                ref pendingBarracksConstruction,
                ref pendingBarracksBuilder,
                ref pendingBarracksStartTime
            );

            CleanupPendingBuilding(
                ref pendingInfantrySchoolConstruction,
                ref pendingInfantrySchoolBuilder,
                ref pendingInfantrySchoolStartTime
            );
        }

        private void CleanupPendingBuilding(
            ref BaseBuilding pendingBuilding,
            ref Worker pendingBuilder,
            ref float pendingStartTime
        )
        {
            if (pendingBuilding == null)
            {
                pendingBuilder = null;
                pendingStartTime = 0f;
                return;
            }

            if (IsCompleted(pendingBuilding))
            {
                pendingBuilding = null;
                pendingBuilder = null;
                pendingStartTime = 0f;
                return;
            }

            if (pendingBuilder == null || pendingBuilder.Transform == null || pendingBuilder.CurrentHealth <= 0 || pendingBuilder.Owner != aiOwner)
            {
                Destroy(pendingBuilding.gameObject);
                pendingBuilding = null;
                pendingBuilder = null;
                pendingStartTime = 0f;
                return;
            }

            if (Time.time - pendingStartTime > pendingBuildTimeout)
            {
                if (!pendingBuilder.IsBuilding)
                {
                    pendingBuilder.Stop();

                    Destroy(pendingBuilding.gameObject);
                    pendingBuilding = null;
                    pendingBuilder = null;
                    pendingStartTime = 0f;
                }
            }
        }

        private void TryBuildSupplyHut()
        {
            if (supplyHutSO == null) return;
            if (completedCommandPost == null) return;
            if (pendingSupplyHutConstruction != null) return;
            if (SupplyHutCount > CompletedSupplyHutCount) return;

            int currentPopulation = Supplies.Population[aiOwner];
            int populationLimit = Supplies.PopulationLimit[aiOwner];
            int remainingPopulation = populationLimit - currentPopulation;

            if (remainingPopulation > supplyBuffer) return;
            if (!HasEnoughSupplies(supplyHutSO, aiOwner)) return;

            if (!TryStartBuilding(supplyHutSO, out BaseBuilding building))
                return;

            pendingSupplyHutConstruction = building;
        }

        private void TryBuildBarracks()
        {
            if (barracksSO == null) return;
            if (completedCommandPost == null) return;
            if (pendingSupplyHutConstruction != null) return;
            if (pendingBarracksConstruction != null) return;
            if (BarracksCount > CompletedBarracksCount) return;
            if (CompletedBarracksCount >= desiredBarracksCount) return;
            if (!HasEnoughSupplies(barracksSO, aiOwner)) return;
            if (!IsUnlockedForAI(barracksSO)) return;

            if (!TryStartBuilding(barracksSO, out BaseBuilding building))
                return;

            pendingBarracksConstruction = building;
        }

        private void TryBuildInfantrySchool()
        {
            if (infantrySchoolSO == null) return;
            if (completedCommandPost == null) return;
            if (pendingSupplyHutConstruction != null) return;
            if (pendingBarracksConstruction != null) return;
            if (pendingInfantrySchoolConstruction != null) return;
            if (CompletedBarracksCount == 0) return;
            if (InfantrySchoolCount > CompletedInfantrySchoolCount) return;
            if (CompletedInfantrySchoolCount >= desiredInfantrySchoolCount) return;
            if (!HasEnoughSupplies(infantrySchoolSO, aiOwner)) return;
            if (!IsUnlockedForAI(infantrySchoolSO)) return;

            if (!TryStartBuilding(infantrySchoolSO, out BaseBuilding building))
                return;

            pendingInfantrySchoolConstruction = building;
        }

        private bool TryStartBuilding(BuildingSO buildingSO, out BaseBuilding building)
        {
            building = null;

            if (buildingSO == null) return false;

            Worker builder = FindBuilderWorker();
            if (builder == null) return false;

            if (!TryFindBuildLocationNearCommandPost(out Vector3 buildPosition))
                return false;

            if (builder == activeResourceSearcher)
            {
                StopResourceSearch();
            }

            workerAssignments.Remove(builder);
            builder.Stop();

            GameObject instance = builder.Build(buildingSO, buildPosition);
            if (instance == null) return false;

            if (!instance.TryGetComponent(out building))
                return false;

            if (buildingSO == supplyHutSO)
            {
                pendingSupplyHutBuilder = builder;
                pendingSupplyHutStartTime = Time.time;
            }
            else if (buildingSO == barracksSO)
            {
                pendingBarracksBuilder = builder;
                pendingBarracksStartTime = Time.time;
            }
            else if (buildingSO == infantrySchoolSO)
            {
                pendingInfantrySchoolBuilder = builder;
                pendingInfantrySchoolStartTime = Time.time;
            }

            return true;
        }

        private Worker FindBuilderWorker()
        {
            Vector3 targetCenter = completedCommandPost != null
                ? completedCommandPost.transform.position
                : transform.position;

            Worker bestIdleWorker = null;
            float bestIdleDistanceSqr = float.MaxValue;

            foreach (Worker worker in workers)
            {
                if (worker == null) continue;
                if (worker.Transform == null) continue;
                if (worker.CurrentHealth <= 0) continue;
                if (worker.IsBuilding) continue;
                if (worker.IsHealing) continue;
                if (worker == activeResourceSearcher) continue;

                if (worker.IsGathering) continue;

                float distanceSqr = (worker.transform.position - targetCenter).sqrMagnitude;
                if (distanceSqr < bestIdleDistanceSqr)
                {
                    bestIdleDistanceSqr = distanceSqr;
                    bestIdleWorker = worker;
                }
            }

            if (bestIdleWorker != null)
                return bestIdleWorker;

            Worker bestGatherWorker = null;
            float bestGatherDistanceSqr = float.MaxValue;

            foreach (Worker worker in workers)
            {
                if (worker == null) continue;
                if (worker.Transform == null) continue;
                if (worker.CurrentHealth <= 0) continue;
                if (worker.IsBuilding) continue;
                if (worker.IsHealing) continue;
                if (worker == activeResourceSearcher) continue;

                float distanceSqr = (worker.transform.position - targetCenter).sqrMagnitude;
                if (distanceSqr < bestGatherDistanceSqr)
                {
                    bestGatherDistanceSqr = distanceSqr;
                    bestGatherWorker = worker;
                }
            }

            return bestGatherWorker;
        }

        private bool TryFindBuildLocationNearCommandPost(out Vector3 buildPosition)
        {
            buildPosition = Vector3.zero;

            if (completedCommandPost == null)
                return false;

            Vector3 center = completedCommandPost.transform.position;

            for (int ring = 0; ring < buildSearchRings; ring++)
            {
                float radius = buildSearchStartRadius + ring * buildSearchStep;

                for (int i = 0; i < buildSpotsPerRing; i++)
                {
                    float angle = (360f / buildSpotsPerRing) * i;
                    Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                    Vector3 rawPoint = center + dir * radius;

                    if (!NavMesh.SamplePosition(rawPoint, out NavMeshHit navHit, navMeshSampleDistance, NavMesh.AllAreas))
                        continue;

                    Vector3 candidate = navHit.position;

                    if (!IsFarEnoughFromBuildings(candidate))
                        continue;

                    buildPosition = candidate;
                    return true;
                }
            }

            return false;
        }

        private bool IsFarEnoughFromBuildings(Vector3 position)
        {
            foreach (BaseBuilding building in allBuildings)
            {
                if (building == null) continue;

                float distance = Vector3.Distance(position, building.transform.position);
                if (distance < minDistanceFromBuildings)
                {
                    return false;
                }
            }

            return true;
        }

        private bool HasEnoughSupplies(UnlockableSO unlockable, Owner owner)
        {
            if (unlockable == null) return false;

            return Supplies.Minerals[owner] >= unlockable.Cost.Minerals
                && Supplies.Wood[owner] >= unlockable.Cost.Wood
                && Supplies.Stone[owner] >= unlockable.Cost.Stone;
        }

        private bool HasEnoughPopulation(AbstractUnitSO unitSO, Owner owner)
        {
            if (unitSO == null) return false;
            if (unitSO.PopulationConfig == null) return true;

            int newPopulation = Supplies.Population[owner] + unitSO.PopulationConfig.PopulationCost;
            return newPopulation <= Supplies.PopulationLimit[owner];
        }

        private bool IsUnlockedForAI(UnlockableSO unlockable)
        {
            if (unlockable == null) return false;
            if (unlockable.TechTree == null) return true;

            return unlockable.TechTree.IsUnlocked(aiOwner, unlockable);
        }

        private void RunScouting()
        {
            if (!enableScouting) return;
            if (hasLaunchedAttack) return;
            if (isDefending) return;

            CleanupScoutState();
            TryDetectEnemyBuilding();

            if (knownEnemyTargetBuilding != null)
                return;

            AssignScoutExplorerIfNeeded();

            if (activeScoutExplorer == null)
                return;

            MarkScoutAreaAsExplored(activeScoutExplorer.transform.position);

            if (!hasScoutTarget || HasScoutReachedTarget(activeScoutExplorer, currentScoutTarget))
            {
                if (hasScoutTarget)
                {
                    MarkScoutAreaAsExplored(currentScoutTarget);
                }

                if (TryGetRandomScoutPoint(out Vector3 nextTarget))
                {
                    currentScoutTarget = nextTarget;
                    hasScoutTarget = true;
                    RememberScoutTarget(nextTarget);
                    activeScoutExplorer.MoveTo(currentScoutTarget);
                }

                return;
            }

            activeScoutExplorer.MoveTo(currentScoutTarget);
        }

        private Vector2Int GetScoutCell(Vector3 worldPosition)
        {
            int x = Mathf.FloorToInt(worldPosition.x / scoutExplorationCellSize);
            int z = Mathf.FloorToInt(worldPosition.z / scoutExplorationCellSize);
            return new Vector2Int(x, z);
        }

        private bool IsScoutPointExplored(Vector3 worldPosition)
        {
            return exploredScoutCells.Contains(GetScoutCell(worldPosition));
        }

        private void MarkScoutAreaAsExplored(Vector3 worldPosition)
        {
            Vector2Int centerCell = GetScoutCell(worldPosition);

            int radiusInCells = Mathf.CeilToInt(scoutVisitedPointRadius / scoutExplorationCellSize);
            radiusInCells += scoutExplorationMemoryPaddingCells;

            for (int x = -radiusInCells; x <= radiusInCells; x++)
            {
                for (int z = -radiusInCells; z <= radiusInCells; z++)
                {
                    Vector2Int cell = new Vector2Int(centerCell.x + x, centerCell.y + z);
                    exploredScoutCells.Add(cell);
                }
            }
        }

        private void CleanupScoutState()
        {
            if (activeScoutExplorer == null)
            {
                hasScoutTarget = false;
                return;
            }

            if (activeScoutExplorer.Owner != aiOwner)
            {
                activeScoutExplorer = null;
                hasScoutTarget = false;
                return;
            }

            if (!scouts.Contains(activeScoutExplorer))
            {
                activeScoutExplorer = null;
                hasScoutTarget = false;
            }
        }

        private void AssignScoutExplorerIfNeeded()
        {
            if (activeScoutExplorer != null)
                return;

            if (scouts.Count == 0)
                return;

            foreach (AbstractUnit scout in scouts)
            {
                if (scout == null) continue;
                activeScoutExplorer = scout;
                hasScoutTarget = false;
                return;
            }
        }

        private bool HasScoutReachedTarget(AbstractUnit scout, Vector3 target)
        {
            if (scout == null) return false;
            return Vector3.Distance(scout.transform.position, target) <= scoutArrivalDistance;
        }

        private void TryDetectEnemyBuilding()
        {
            if (activeScoutExplorer == null)
                return;

            BaseBuilding[] buildings = FindObjectsByType<BaseBuilding>(FindObjectsSortMode.None);

            BaseBuilding bestDetected = null;
            float bestPriority = float.MinValue;
            float bestDistanceSqr = float.MaxValue;

            foreach (BaseBuilding building in buildings)
            {
                if (building == null) continue;
                if (building.Owner != Owner.Player1) continue;
                if (building.Transform == null) continue;
                if (building.CurrentHealth <= 0) continue;
                if (!building.IsVisibleTo(aiOwner)) continue;

                float distance = Vector3.Distance(activeScoutExplorer.transform.position, building.transform.position);
                if (distance > enemyDetectionRadius) continue;

                if (!knownEnemyBuildings.Contains(building))
                {
                    knownEnemyBuildings.Add(building);
                }

                float priority = MatchesBuildingSO(building, commandPostSO) ? 1000f : 0f;
                float distanceSqr = (building.transform.position - activeScoutExplorer.transform.position).sqrMagnitude;

                if (priority > bestPriority || (Mathf.Approximately(priority, bestPriority) && distanceSqr < bestDistanceSqr))
                {
                    bestPriority = priority;
                    bestDistanceSqr = distanceSqr;
                    bestDetected = building;
                }
            }

            if (bestDetected != null)
            {
                knownEnemyTargetBuilding = bestDetected;
                hasScoutTarget = false;
            }
        }

        private bool TryGetRandomScoutPoint(out Vector3 point)
        {
            point = Vector3.zero;

            if (worldBoundsRoot == null)
                return false;

            if (!TryGetWorldBounds(out Bounds bounds))
                return false;

            float minX = bounds.min.x + scoutEdgePadding;
            float maxX = bounds.max.x - scoutEdgePadding;
            float minZ = bounds.min.z + scoutEdgePadding;
            float maxZ = bounds.max.z - scoutEdgePadding;

            if (minX >= maxX || minZ >= maxZ)
                return false;

            Vector3 fallbackPoint = Vector3.zero;
            bool hasFallback = false;

            for (int i = 0; i < scoutRandomPointAttempts; i++)
            {
                Vector3 rawPoint = new Vector3(
                    Random.Range(minX, maxX),
                    transform.position.y,
                    Random.Range(minZ, maxZ)
                );

                if (!NavMesh.SamplePosition(rawPoint, out NavMeshHit navHit, scoutNavMeshSampleDistance, NavMesh.AllAreas))
                    continue;

                Vector3 candidate = navHit.position;

                if (!IsFarEnoughFromRecentScoutTargets(candidate))
                    continue;

                if (!hasFallback)
                {
                    fallbackPoint = candidate;
                    hasFallback = true;
                }

                if (IsScoutPointExplored(candidate))
                    continue;

                point = candidate;
                return true;
            }

            if (hasFallback)
            {
                point = fallbackPoint;
                return true;
            }

            return false;
        }

        private bool TryGetWorldBounds(out Bounds combinedBounds)
        {
            combinedBounds = default;

            if (worldBoundsRoot == null)
                return false;

            BoxCollider[] colliders = worldBoundsRoot.GetComponentsInChildren<BoxCollider>();
            if (colliders == null || colliders.Length == 0)
                return false;

            bool initialized = false;

            foreach (BoxCollider box in colliders)
            {
                if (box == null) continue;

                Bounds worldSpaceBounds = GetWorldBounds(box);

                if (!initialized)
                {
                    combinedBounds = worldSpaceBounds;
                    initialized = true;
                }
                else
                {
                    combinedBounds.Encapsulate(worldSpaceBounds);
                }
            }

            return initialized;
        }

        private Bounds GetWorldBounds(BoxCollider box)
        {
            Vector3 worldCenter = box.transform.TransformPoint(box.center);
            Vector3 worldSize = Vector3.Scale(box.size, box.transform.lossyScale);
            worldSize = new Vector3(
                Mathf.Abs(worldSize.x),
                Mathf.Abs(worldSize.y),
                Mathf.Abs(worldSize.z)
            );

            return new Bounds(worldCenter, worldSize);
        }

        private bool IsFarEnoughFromRecentScoutTargets(Vector3 candidate)
        {
            foreach (Vector3 recent in recentScoutTargets)
            {
                if (Vector3.Distance(candidate, recent) < minDistanceFromLastScoutTarget)
                {
                    return false;
                }
            }

            return true;
        }

        private void RememberScoutTarget(Vector3 target)
        {
            recentScoutTargets.Enqueue(target);

            while (recentScoutTargets.Count > MAX_RECENT_SCOUT_TARGETS)
            {
                recentScoutTargets.Dequeue();
            }
        }

        private void RefreshKnownEnemyTarget()
        {
            CleanupKnownEnemyBuildings();

            if (knownEnemyTargetBuilding != null
                && knownEnemyTargetBuilding.Transform != null
                && knownEnemyTargetBuilding.CurrentHealth > 0)
            {
                return;
            }

            knownEnemyTargetBuilding = ChooseBestKnownEnemyBuilding();
        }

        private void CleanupKnownEnemyBuildings()
        {
            for (int i = knownEnemyBuildings.Count - 1; i >= 0; i--)
            {
                BaseBuilding building = knownEnemyBuildings[i];

                if (building == null || building.Transform == null || building.CurrentHealth <= 0)
                {
                    knownEnemyBuildings.RemoveAt(i);
                }
            }
        }

        private BaseBuilding ChooseBestKnownEnemyBuilding()
        {
            BaseBuilding best = null;
            float bestPriority = float.MinValue;
            float bestDistanceSqr = float.MaxValue;

            Vector3 referencePosition = completedCommandPost != null
                ? completedCommandPost.transform.position
                : transform.position;

            foreach (BaseBuilding building in knownEnemyBuildings)
            {
                if (building == null) continue;
                if (building.Transform == null) continue;
                if (building.CurrentHealth <= 0) continue;

                float priority = GetEnemyBuildingStrategicPriority(building);
                float distanceSqr = (building.transform.position - referencePosition).sqrMagnitude;

                if (priority > bestPriority || (Mathf.Approximately(priority, bestPriority) && distanceSqr < bestDistanceSqr))
                {
                    bestPriority = priority;
                    bestDistanceSqr = distanceSqr;
                    best = building;
                }
            }

            return best;
        }

        private float GetEnemyBuildingStrategicPriority(BaseBuilding building)
        {
            if (building == null || building.UnitSO == null)
                return -9999f;

            if (MatchesBuildingSO(building, barracksSO))
                return 100f;

            if (MatchesBuildingSO(building, infantrySchoolSO))
                return 95f;

            if (MatchesBuildingSO(building, supplyHutSO))
                return 85f;

            if (MatchesBuildingSO(building, commandPostSO))
                return 70f;

            return 50f;
        }

        private void RunDefense()
        {
            if (!enableDefense) return;

            CleanupDefenseState();
            TryFindDefenseTarget();

            if (currentDefenseTarget == null)
            {
                isDefending = false;
                return;
            }

            isDefending = true;

            if (Time.time < nextDefenseCommandTime)
                return;

            IssueDefenseOrder();
            nextDefenseCommandTime = Time.time + defenseCommandInterval;
        }

        private void CleanupDefenseState()
        {
            if (currentDefenseTarget == null)
                return;

            if (currentDefenseTarget.Transform == null || currentDefenseTarget.CurrentHealth <= 0)
            {
                currentDefenseTarget = null;
                isDefending = false;
            }
        }

        private void TryFindDefenseTarget()
        {
            if (completedCommandPost == null)
            {
                currentDefenseTarget = null;
                isDefending = false;
                return;
            }

            IDamageable bestTarget = null;
            float bestDistanceSqr = float.MaxValue;
            float defenseRadiusSqr = defenseRadius * defenseRadius;
            Vector3 defendCenter = completedCommandPost.transform.position;

            AbstractUnit[] enemyUnits = FindObjectsByType<AbstractUnit>(FindObjectsSortMode.None);
            foreach (AbstractUnit unit in enemyUnits)
            {
                if (unit == null) continue;
                if (unit.Owner != Owner.Player1) continue;
                if (unit.Transform == null) continue;
                if (unit.CurrentHealth <= 0) continue;
                if (!unit.IsVisibleTo(aiOwner)) continue;

                float distanceSqr = (unit.transform.position - defendCenter).sqrMagnitude;
                if (distanceSqr > defenseRadiusSqr) continue;

                if (distanceSqr < bestDistanceSqr)
                {
                    bestDistanceSqr = distanceSqr;
                    bestTarget = unit;
                }
            }

            BaseBuilding[] enemyBuildings = FindObjectsByType<BaseBuilding>(FindObjectsSortMode.None);
            foreach (BaseBuilding building in enemyBuildings)
            {
                if (building == null) continue;
                if (building.Owner != Owner.Player1) continue;
                if (building.Transform == null) continue;
                if (building.CurrentHealth <= 0) continue;
                if (!building.IsVisibleTo(aiOwner)) continue;

                float distanceSqr = (building.transform.position - defendCenter).sqrMagnitude;
                if (distanceSqr > defenseRadiusSqr) continue;

                if (distanceSqr < bestDistanceSqr)
                {
                    bestDistanceSqr = distanceSqr;
                    bestTarget = building;
                }
            }

            currentDefenseTarget = bestTarget;
        }

        private void IssueDefenseOrder()
        {
            if (currentDefenseTarget == null || currentDefenseTarget.Transform == null)
                return;

            Vector3 defendPoint = currentDefenseTarget.Transform.position;

            foreach (AbstractUnit ranged in rangedUnits)
            {
                if (ranged == null) continue;
                ranged.Attack(defendPoint);
            }

            foreach (AbstractUnit scout in scouts)
            {
                if (scout == null) continue;

                if (scout == activeScoutExplorer)
                    hasScoutTarget = false;

                scout.Attack(defendPoint);
            }

            if (workersHelpOnDefense)
            {
                foreach (Worker worker in workers)
                {
                    if (worker == null) continue;
                    if (worker.Transform == null) continue;
                    if (worker.CurrentHealth <= 0) continue;
                    if (worker.IsBuilding) continue;

                    workerAssignments.Remove(worker);

                    if (worker == activeResourceSearcher)
                    {
                        StopResourceSearch();
                    }

                    worker.Attack(defendPoint);
                }
            }
        }

        private void RunIdleWorkerDefensePositioning()
        {
            if (!sendIdleWorkersToCommandPost) return;
            if (completedCommandPost == null) return;
            if (isDefending) return;
            if (Time.time < nextIdleWorkerCommandTime) return;

            Vector3 defendCenter = completedCommandPost.transform.position;

            foreach (Worker worker in workers)
            {
                if (worker == null) continue;
                if (worker.Transform == null) continue;
                if (worker.CurrentHealth <= 0) continue;
                if (worker.IsBuilding) continue;
                if (worker.IsHealing) continue;
                if (worker.IsGathering) continue;
                if (worker == activeResourceSearcher) continue;
                if (workerAssignments.ContainsKey(worker)) continue;

                float distance = Vector3.Distance(worker.transform.position, defendCenter);
                if (distance <= idleWorkerReturnDistance)
                    continue;

                if (TryGetIdleWorkerStandPoint(defendCenter, out Vector3 standPoint))
                {
                    worker.MoveTo(standPoint);
                }
            }

            nextIdleWorkerCommandTime = Time.time + idleWorkerCommandInterval;
        }

        private bool TryGetIdleWorkerStandPoint(Vector3 center, out Vector3 point)
        {
            point = Vector3.zero;

            for (int i = 0; i < 12; i++)
            {
                Vector2 offset2D = Random.insideUnitCircle * idleWorkerStandRadius;
                Vector3 rawPoint = new Vector3(center.x + offset2D.x, center.y, center.z + offset2D.y);

                if (!NavMesh.SamplePosition(rawPoint, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
                    continue;

                point = hit.position;
                return true;
            }

            return false;
        }

        private void RunRetreating()
        {
            if (!enableRetreat) return;
            if (completedCommandPost == null) return;

            if (isDefending)
            {
                StopRetreat(false);
                return;
            }

            CleanupActiveAttackForce();

            if (isRetreating)
            {
                if (activeAttackForce.Count == 0 || AreRetreatingUnitsBackHome())
                {
                    StopRetreat(true);
                    return;
                }

                if (Time.time >= nextRetreatCommandTime)
                {
                    IssueRetreatOrder();
                    nextRetreatCommandTime = Time.time + retreatCommandInterval;
                }

                return;
            }

            if (ShouldStartRetreat())
            {
                isRetreating = true;
                nextRetreatCommandTime = 0f;

                if (Time.time >= nextRetreatCommandTime)
                {
                    IssueRetreatOrder();
                    nextRetreatCommandTime = Time.time + retreatCommandInterval;
                }
            }
        }

        private bool ShouldStartRetreat()
        {
            if (!hasLaunchedAttack)
                return false;

            CleanupActiveAttackForce();

            if (activeAttackForce.Count == 0)
                return false;

            if (activeAttackForce.Count <= retreatThreshold)
                return true;

            if (attackForceStartCount > 0)
            {
                float remainingRatio = (float)activeAttackForce.Count / attackForceStartCount;
                if (remainingRatio <= retreatWhenAttackForceRemainingRatio)
                    return true;
            }

            return false;
        }

        private void IssueRetreatOrder()
        {
            if (completedCommandPost == null)
                return;

            Vector3 retreatCenter = completedCommandPost.transform.position;

            foreach (AbstractUnit unit in activeAttackForce)
            {
                if (unit == null) continue;
                if (unit.Transform == null) continue;
                if (unit.CurrentHealth <= 0) continue;

                if (TryGetRetreatPoint(retreatCenter, out Vector3 retreatPoint))
                    unit.MoveTo(retreatPoint);
                else
                    unit.MoveTo(retreatCenter);
            }
        }

        private bool AreRetreatingUnitsBackHome()
        {
            if (completedCommandPost == null)
                return false;

            if (activeAttackForce.Count == 0)
                return true;

            Vector3 center = completedCommandPost.transform.position;
            float maxDistanceSqr = retreatRadiusFromCommandPost * retreatRadiusFromCommandPost;

            foreach (AbstractUnit unit in activeAttackForce)
            {
                if (unit == null) continue;
                if (unit.Transform == null) continue;
                if (unit.CurrentHealth <= 0) continue;

                float distanceSqr = (unit.transform.position - center).sqrMagnitude;
                if (distanceSqr > maxDistanceSqr)
                    return false;
            }

            return true;
        }

        private bool TryGetRetreatPoint(Vector3 center, out Vector3 point)
        {
            point = Vector3.zero;

            for (int i = 0; i < 10; i++)
            {
                Vector2 offset2D = Random.insideUnitCircle * retreatRadiusFromCommandPost;
                Vector3 rawPoint = new Vector3(center.x + offset2D.x, center.y, center.z + offset2D.y);

                if (!NavMesh.SamplePosition(rawPoint, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
                    continue;

                point = hit.position;
                return true;
            }

            return false;
        }

        private void StopRetreat(bool advanceWave)
        {
            isRetreating = false;
            hasLaunchedAttack = false;

            activeAttackForce.Clear();
            attackForceStartCount = 0;

            nextRetreatCommandTime = 0f;
            nextAttackCommandTime = 0f;

            if (advanceWave && useAttackWaves)
            {
                currentWaveIndex = Mathf.Min(currentWaveIndex + 1, maxWaveIndex);
            }

            attackBlockedUntilTime = Time.time + CurrentWaveAttackCooldownAfterRetreat;
        }

        private void CleanupActiveAttackForce()
        {
            activeAttackForce.RemoveWhere(unit =>
                unit == null ||
                unit.Transform == null ||
                unit.CurrentHealth <= 0 ||
                unit.Owner != aiOwner);
        }

        private void RunAttacking()
        {
            if (!enableAttacking) return;
            if (isDefending) return;
            if (isRetreating) return;
            if (Time.time < attackBlockedUntilTime) return;

            CleanupAttackState();

            if (knownEnemyTargetBuilding == null)
                return;

            if (MilitaryCount < CurrentWaveAttackThreshold)
                return;

            if (Time.time < nextAttackCommandTime)
                return;

            BuildActiveAttackForce();
            if (activeAttackForce.Count == 0)
                return;

            IssueAttackOrderToArmy();
            nextAttackCommandTime = Time.time + attackCommandInterval;
            hasLaunchedAttack = true;
        }

        private void BuildActiveAttackForce()
        {
            activeAttackForce.Clear();

            foreach (AbstractUnit ranged in rangedUnits)
            {
                if (ranged == null) continue;
                if (ranged.Transform == null) continue;
                if (ranged.CurrentHealth <= 0) continue;

                activeAttackForce.Add(ranged);
            }

            foreach (AbstractUnit scout in scouts)
            {
                if (scout == null) continue;
                if (scout.Transform == null) continue;
                if (scout.CurrentHealth <= 0) continue;

                activeAttackForce.Add(scout);
            }

            attackForceStartCount = activeAttackForce.Count;
        }

        private void CleanupAttackState()
        {
            RefreshKnownEnemyTarget();

            if (knownEnemyTargetBuilding == null)
            {
                hasLaunchedAttack = false;
            }
        }

        private void IssueAttackOrderToArmy()
        {
            if (knownEnemyTargetBuilding == null || knownEnemyTargetBuilding.transform == null)
                return;

            Vector3 attackPoint = knownEnemyTargetBuilding.transform.position;

            foreach (AbstractUnit unit in activeAttackForce)
            {
                if (unit == null) continue;
                if (unit.Transform == null) continue;
                if (unit.CurrentHealth <= 0) continue;

                if (unit == activeScoutExplorer)
                    hasScoutTarget = false;

                unit.Attack(attackPoint);
            }
        }

        private bool MatchesUnitSO(AbstractUnit unit, AbstractUnitSO referenceSO)
        {
            if (unit == null || unit.UnitSO == null || referenceSO == null)
                return false;

            return unit.UnitSO.Prefab == referenceSO.Prefab;
        }

        private bool MatchesBuildingSO(BaseBuilding building, BuildingSO referenceSO)
        {
            if (building == null || building.UnitSO == null || referenceSO == null)
                return false;

            return building.UnitSO.Prefab == referenceSO.Prefab;
        }

        private bool IsCompleted(BaseBuilding building)
        {
            return building != null
                   && building.Progress.State == BuildingProgress.BuildingState.Completed;
        }

        private void LogStateIfNeeded()
        {
            string snapshot = BuildDebugSnapshot();

            if (debugOnlyLogWhenChanged && snapshot == lastDebugSnapshot)
            {
                return;
            }

            lastDebugSnapshot = snapshot;
            Debug.Log(snapshot, this);
        }

        private string BuildDebugSnapshot()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine($"[EnemyAIController - {aiOwner}]");
            sb.AppendLine($"Workers: {WorkerCount}");
            sb.AppendLine($"Scouts: {ScoutCount}");
            sb.AppendLine($"Ranged: {RangedCount}");
            sb.AppendLine($"Military Total: {MilitaryCount}");
            sb.AppendLine($"All Units: {allUnits.Count}");
            sb.AppendLine($"Command Post: {(HasCommandPost ? "YES" : "NO")}");
            sb.AppendLine($"Completed Command Post: {(HasCompletedCommandPost ? "YES" : "NO")}");
            sb.AppendLine($"Barracks: {BarracksCount} (Completed: {CompletedBarracksCount})");
            sb.AppendLine($"Supply Huts: {SupplyHutCount} (Completed: {CompletedSupplyHutCount})");
            sb.AppendLine($"Infantry Schools: {InfantrySchoolCount} (Completed: {CompletedInfantrySchoolCount})");
            sb.AppendLine($"All Buildings: {allBuildings.Count}");
            sb.AppendLine($"Wood Assigned: {CountAssignedWorkers(SupplyType.Wood)}");
            sb.AppendLine($"Stone Assigned: {CountAssignedWorkers(SupplyType.Stone)}");
            sb.AppendLine($"Desired Workers: {desiredWorkerCount}");
            sb.AppendLine($"Command Post Queue Size: {(completedCommandPost != null ? completedCommandPost.QueueSize : 0)}");
            sb.AppendLine($"Population: {Supplies.Population[aiOwner]}/{Supplies.PopulationLimit[aiOwner]}");
            sb.AppendLine($"Pending Supply Hut: {(pendingSupplyHutConstruction != null ? "YES" : "NO")}");
            sb.AppendLine($"Pending Barracks: {(pendingBarracksConstruction != null ? "YES" : "NO")}");
            sb.AppendLine($"Pending Infantry School: {(pendingInfantrySchoolConstruction != null ? "YES" : "NO")}");
            sb.AppendLine($"Minerals: {Supplies.Minerals[aiOwner]}");
            sb.AppendLine($"Wood: {Supplies.Wood[aiOwner]}");
            sb.AppendLine($"Stone: {Supplies.Stone[aiOwner]}");
            sb.AppendLine($"Stone->Minerals Conversion Enabled: {(convertStoneToMinerals ? "YES" : "NO")}");
            sb.AppendLine($"Queued Ranged: {CountQueuedUnitsInCompletedBarrackses(rangedSO)}");
            sb.AppendLine($"Queued Scouts: {CountQueuedUnitsInCompletedBarrackses(scoutSO)}");
            sb.AppendLine($"Scouting Enabled: {(enableScouting ? "YES" : "NO")}");
            sb.AppendLine($"Active Scout Explorer: {(activeScoutExplorer != null ? activeScoutExplorer.name : "NONE")}");
            sb.AppendLine($"Has Scout Target: {(hasScoutTarget ? "YES" : "NO")}");
            sb.AppendLine($"Known Enemy Buildings Count: {knownEnemyBuildings.Count}");
            sb.AppendLine($"Known Enemy Target Building: {(knownEnemyTargetBuilding != null ? knownEnemyTargetBuilding.name : "NONE")}");
            sb.AppendLine($"Attacking Enabled: {(enableAttacking ? "YES" : "NO")}");
            sb.AppendLine($"Has Launched Attack: {(hasLaunchedAttack ? "YES" : "NO")}");
            sb.AppendLine($"Defense Enabled: {(enableDefense ? "YES" : "NO")}");
            sb.AppendLine($"Is Defending: {(isDefending ? "YES" : "NO")}");
            sb.AppendLine($"Defense Target: {(currentDefenseTarget != null ? currentDefenseTarget.Transform.name : "NONE")}");
            sb.AppendLine($"Retreat Enabled: {(enableRetreat ? "YES" : "NO")}");
            sb.AppendLine($"Is Retreating: {(isRetreating ? "YES" : "NO")}");
            sb.AppendLine($"Retreat Threshold: {retreatThreshold}");
            sb.AppendLine($"Attack Cooldown Remaining: {Mathf.Max(0f, attackBlockedUntilTime - Time.time):0.0}s");
            sb.AppendLine($"Use Attack Waves: {(useAttackWaves ? "YES" : "NO")}");
            sb.AppendLine($"Current Wave Index: {currentWaveIndex}");
            sb.AppendLine($"Clamped Wave Index: {ClampedWaveIndex}");
            sb.AppendLine($"Wave Attack Threshold: {CurrentWaveAttackThreshold}");
            sb.AppendLine($"Wave Desired Ranged: {CurrentWaveDesiredRangedCount}");
            sb.AppendLine($"Wave Desired Scout: {CurrentWaveDesiredScoutCount}");
            sb.AppendLine($"Wave Retreat Cooldown: {CurrentWaveAttackCooldownAfterRetreat:0.0}s");
            //Debug.Log("NavMesh vertices: " + UnityEngine.AI.NavMesh.CalculateTriangulation().vertices.Length);

            return sb.ToString();
        }
    }
}