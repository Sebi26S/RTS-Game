using System.Collections;
using RTS.Commands;
using RTS.Environment;
using RTS.EventBus;
using RTS.Events;
using RTS.Player;
using RTS.Utilities;
using UnityEngine;

namespace RTS.Units
{
    public class Worker : AbstractUnit, IBuildingBuilder, IHealer
    {
        public bool IsBusyWithNonCombatAction => gatherRoutine != null || buildRoutine != null || healRoutine != null;

        [Header("Commands")]
        [SerializeField] private BaseCommand CancelBuildingCommand;

        // -------------------------
        // GATHER
        // -------------------------
        private GatherableSupply currentSupply;
        private Coroutine gatherRoutine;

        public bool IsGathering => gatherRoutine != null;
        public bool IsHealing => healRoutine != null;
        public bool HasGatherTarget => currentSupply != null;
        public SupplySO CurrentGatherType => gatherType;

        [Header("Gather Settings")]
        [SerializeField] private float gatherReachDistance = 1.5f;
        [SerializeField] private float supplySearchRadius = 7f;
        [SerializeField] private float retargetCooldown = 0.4f;

        private SupplySO gatherType;
        private float nextRetargetTime;
        private bool isExecutingGatherCommand;

        // -------------------------
        // BUILD
        // -------------------------
        public bool IsBuilding => buildRoutine != null;

        private Coroutine buildRoutine;
        private BaseBuilding buildingUnderConstruction;
        private BuildingSO buildingSO;

        private Renderer buildingRenderer;
        private Vector3 buildStartPos;
        private Vector3 buildEndPos;
        private float buildStartTime;
        private float buildTargetHealth;
        private bool isExecutingBuildCommand;

        [Header("Build Settings")]
        [SerializeField] private float buildReachDistance = 1.6f;

        [Header("Heal Settings")]
        [SerializeField] private int healPerTick = 2;
        [SerializeField] private float healTickRate = 0.25f;
        [SerializeField] private float healReachDistance = 1.8f;

        private Coroutine healRoutine;
        private IDamageable healTarget;

        private bool isExecutingHealCommand;

        protected override void Start()
        {
            base.Start();
        }

        protected override void OnBeforeCommand()
        {
            if (isExecutingHealCommand || isExecutingBuildCommand || isExecutingGatherCommand)
                return;

            if (healRoutine != null)
            {
                StopCoroutine(healRoutine);
                healRoutine = null;
            }

            healTarget = null;

            if (buildRoutine != null || buildingUnderConstruction != null)
            {
                CancelBuilding();
            }

            if (gatherRoutine != null)
            {
                StopCoroutine(gatherRoutine);
                gatherRoutine = null;
            }

            if (currentSupply != null)
            {
                currentSupply.AbortGather();
                currentSupply = null;
            }

            if (TryGetComponent(out Animator anim))
            {
                anim.SetBool(AnimationConstants.IS_GATHERING, false);
                anim.SetFloat(AnimationConstants.SPEED, CanUseAgent() ? Agent.velocity.magnitude : 0f);
            }

            if (CanUseAgent())
                Agent.isStopped = false;

            SetCommandOverrides(null);
        }

        protected override bool CanAutoRetaliate()
        {
            return !IsBusyWithNonCombatAction;
        }

        public void Gather(GatherableSupply supply)
        {
            if (supply == null) return;
            if (supply.Supply == null) return;
            if (Supplies.IsAtMax(Owner, supply.Supply)) return;

            if (gatherRoutine != null && currentSupply == supply && gatherType == supply.Supply)
                return;

            currentSupply = supply;
            gatherType = supply.Supply;
            nextRetargetTime = 0f;

            if (gatherRoutine != null)
            {
                StopCoroutine(gatherRoutine);
                gatherRoutine = null;
            }

            gatherRoutine = StartCoroutine(GatherRoutine());
        }

        private IEnumerator GatherRoutine()
        {
            Animator animator = GetComponent<Animator>();

            while (true)
            {
                if (!IsAliveAndActive())
                    yield break;

                if (gatherType == null || Supplies.IsAtMax(Owner, gatherType))
                    break;

                if (currentSupply == null || currentSupply.Amount <= 0)
                {
                    currentSupply = FindBestNearbySupply(gatherType);
                    if (currentSupply == null)
                        break;
                }

                Vector3 targetPos = GetClosestPoint(currentSupply.gameObject);

                isExecutingGatherCommand = true;
                MoveTo(targetPos);
                isExecutingGatherCommand = false;

                while (currentSupply != null)
                {
                    if (!IsAliveAndActive())
                        yield break;

                    if (gatherType == null || Supplies.IsAtMax(Owner, gatherType))
                        break;

                    if (!CanUseAgent())
                        yield break;

                    if (!Agent.pathPending && Agent.remainingDistance <= Mathf.Max(Agent.stoppingDistance, gatherReachDistance))
                        break;

                    yield return null;
                }

                if (gatherType == null || Supplies.IsAtMax(Owner, gatherType))
                    break;

                if (currentSupply == null)
                    continue;

                if (!currentSupply.BeginGather())
                {
                    if (Time.time >= nextRetargetTime)
                    {
                        nextRetargetTime = Time.time + retargetCooldown;
                        currentSupply = FindBestNearbySupply(gatherType, exclude: currentSupply);
                    }

                    yield return new WaitForSeconds(0.05f);
                    continue;
                }

                if (animator != null)
                    animator.SetBool(AnimationConstants.IS_GATHERING, true);

                float endTime = Time.time + currentSupply.Supply.BaseGatherTime;

                while (Time.time < endTime)
                {
                    if (!IsAliveAndActive())
                        yield break;

                    if (gatherType == null || Supplies.IsAtMax(Owner, gatherType))
                        break;

                    if (currentSupply == null)
                        break;

                    yield return null;
                }

                if (animator != null)
                    animator.SetBool(AnimationConstants.IS_GATHERING, false);

                if (gatherType == null || Supplies.IsAtMax(Owner, gatherType))
                    break;

                if (currentSupply == null)
                {
                    yield return null;
                    continue;
                }

                int remainingCapacity = Supplies.GetRemainingCapacity(Owner, currentSupply.Supply);
                int amount = currentSupply.EndGather(remainingCapacity);

                if (amount > 0)
                {
                    Bus<SupplyEvent>.Raise(
                        Owner,
                        new SupplyEvent(Owner, amount, currentSupply.Supply)
                    );
                }

                yield return null;
            }

            if (currentSupply != null)
            {
                currentSupply.AbortGather();
                currentSupply = null;
            }

            if (animator != null)
                animator.SetBool(AnimationConstants.IS_GATHERING, false);

            gatherRoutine = null;
            isExecutingGatherCommand = false;
        }

        private GatherableSupply FindBestNearbySupply(SupplySO type, GatherableSupply exclude = null)
        {
            if (type == null) return null;

            int mask = LayerMask.GetMask("Supplies");
            Collider[] cols = Physics.OverlapSphere(transform.position, supplySearchRadius, mask);

            GatherableSupply best = null;
            float bestDist = float.MaxValue;

            foreach (var col in cols)
            {
                if (!col.TryGetComponent(out GatherableSupply s)) continue;
                if (s == null) continue;
                if (exclude != null && s == exclude) continue;

                if (s.Supply != type) continue;
                if (s.Amount <= 0) continue;

                float dist = (s.transform.position - transform.position).sqrMagnitude;
                float busyPenalty = s.IsBusy ? 1000f : 0f;
                dist += busyPenalty;

                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = s;
                }
            }

            return best;
        }

        private Vector3 GetClosestPoint(GameObject go)
        {
            if (go != null && go.TryGetComponent(out Collider col))
                return col.ClosestPoint(transform.position);

            return go != null ? go.transform.position : transform.position;
        }

        public GameObject Build(BuildingSO building, Vector3 targetLocation)
        {
            if (building == null || building.Prefab == null) return null;
            if (buildRoutine != null) return null;

            GameObject instance = Instantiate(building.Prefab, targetLocation, Quaternion.identity);

            if (!instance.TryGetComponent(out BaseBuilding baseBuilding) || baseBuilding.MainRenderer == null)
            {
                Debug.LogError($"Missing BaseBuilding or MainRenderer on Prefab for BuildingSO \"{building.name}\"! Cannot build!");
                Destroy(instance);
                return null;
            }

            baseBuilding.enabled = true;
            baseBuilding.SetStartAsCompleted(false);

            Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(Owner, -building.Cost.Minerals, building.Cost.MineralsSO));
            Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(Owner, -building.Cost.Wood, building.Cost.WoodSO));
            Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(Owner, -building.Cost.Stone, building.Cost.StoneSO));

            SetCommandOverrides(new BaseCommand[] { CancelBuildingCommand });

            buildingSO = building;
            buildingUnderConstruction = baseBuilding;

            buildRoutine = StartCoroutine(BuildRoutine(isResume: false));

            return instance;
        }

        public void ResumeBuilding(BaseBuilding building)
        {
            if (building == null || building.MainRenderer == null) return;
            if (buildRoutine != null) return;

            SetCommandOverrides(new BaseCommand[] { CancelBuildingCommand });

            buildingSO = building.BuildingSO;
            buildingUnderConstruction = building;

            buildRoutine = StartCoroutine(BuildRoutine(isResume: true));
        }

        public void CancelBuilding()
        {
            if (buildRoutine != null)
            {
                StopCoroutine(buildRoutine);
                buildRoutine = null;
            }

            if (buildingUnderConstruction != null)
            {
                BuildingSO so = buildingUnderConstruction.BuildingSO;
                Destroy(buildingUnderConstruction.gameObject);

                if (so != null)
                {
                    Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(
                        Owner,
                        Mathf.FloorToInt(0.75f * so.Cost.Minerals),
                        so.Cost.MineralsSO
                    ));
                    Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(
                        Owner,
                        Mathf.FloorToInt(0.75f * so.Cost.Wood),
                        so.Cost.WoodSO
                    ));
                    Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(
                        Owner,
                        Mathf.FloorToInt(0.75f * so.Cost.Stone),
                        so.Cost.StoneSO
                    ));
                }
            }

            buildingUnderConstruction = null;
            buildingSO = null;
            isExecutingBuildCommand = false;

            SetCommandOverrides(null);

            if (TryGetComponent(out Animator anim))
            {
                anim.SetBool(AnimationConstants.IS_GATHERING, false);
                anim.SetFloat(AnimationConstants.SPEED, 0);
            }
        }

        private IEnumerator BuildRoutine(bool isResume)
        {
            if (buildingUnderConstruction == null || buildingSO == null)
            {
                buildRoutine = null;
                yield break;
            }

            Vector3 destination = GetClosestPoint(buildingUnderConstruction.gameObject);

            isExecutingBuildCommand = true;
            MoveTo(destination);
            isExecutingBuildCommand = false;

            Animator anim = GetComponent<Animator>();

            while (buildingUnderConstruction != null)
            {
                if (!IsAliveAndActive())
                    yield break;

                if (anim != null)
                {
                    float speed = CanUseAgent() ? Agent.velocity.magnitude : 0f;
                    anim.SetFloat(AnimationConstants.SPEED, speed);
                }

                if (!CanUseAgent())
                    yield break;

                if (!Agent.pathPending && Agent.remainingDistance <= Mathf.Max(Agent.stoppingDistance, buildReachDistance))
                    break;

                yield return null;
            }

            if (anim != null)
                anim.SetFloat(AnimationConstants.SPEED, 0);

            if (buildingUnderConstruction == null || buildingSO == null)
            {
                buildRoutine = null;
                yield break;
            }

            buildingUnderConstruction.StartBuilding(this);

            buildStartTime = buildingUnderConstruction.Progress.StartTime;
            buildingRenderer = buildingUnderConstruction.MainRenderer;

            buildEndPos = buildingUnderConstruction.transform.position;
            buildStartPos = buildEndPos - Vector3.up * buildingRenderer.bounds.size.y;

            if (!isResume)
            {
                buildingRenderer.transform.position = buildStartPos;
            }

            buildTargetHealth = 0f;

            while (buildingUnderConstruction != null && buildingSO != null)
            {
                if (!IsAliveAndActive())
                    yield break;

                float normalizedTime = (Time.time - buildStartTime) / buildingSO.BuildTime;

                buildTargetHealth += Time.deltaTime * (buildingSO.Health / buildingSO.BuildTime);
                if (buildTargetHealth >= 1f)
                {
                    int healAmount = Mathf.FloorToInt(buildTargetHealth);
                    buildingUnderConstruction.Heal(healAmount);
                    buildTargetHealth -= healAmount;
                }

                buildingRenderer.transform.position = Vector3.Lerp(buildStartPos, buildEndPos, normalizedTime);

                if (normalizedTime >= 1f)
                    break;

                yield return null;
            }

            if (buildingUnderConstruction != null)
            {
                buildingUnderConstruction.CompleteConstruction();
                buildingUnderConstruction.SetObstacleActive(true);
            }

            buildRoutine = null;
            buildingSO = null;
            buildingUnderConstruction = null;
            isExecutingBuildCommand = false;

            SetCommandOverrides(null);
        }

        public override void Stop()
        {
            base.Stop();

            if (buildRoutine != null || buildingUnderConstruction != null)
            {
                CancelBuilding();
                return;
            }

            if (gatherRoutine != null)
            {
                StopCoroutine(gatherRoutine);
                gatherRoutine = null;
            }

            if (healRoutine != null)
            {
                StopCoroutine(healRoutine);
                healRoutine = null;
            }

            healTarget = null;

            if (currentSupply != null)
            {
                currentSupply.AbortGather();
                currentSupply = null;
            }

            if (TryGetComponent(out Animator anim))
            {
                anim.SetBool(AnimationConstants.IS_GATHERING, false);
                anim.SetFloat(AnimationConstants.SPEED, 0);
            }

            isExecutingGatherCommand = false;
            SetCommandOverrides(null);
        }

        public override void Deselect()
        {
            base.Deselect();
        }

        public void HealTarget(IDamageable target)
        {
            Debug.Log("HealTarget called");

            if (target == null || target.Transform == null)
            {
                Debug.Log("HealTarget failed: target null");
                return;
            }

            if (target.Owner != Owner)
            {
                Debug.Log("HealTarget failed: wrong owner");
                return;
            }

            if (target.CurrentHealth >= target.MaxHealth)
            {
                Debug.Log("HealTarget failed: target already full hp");
                return;
            }

            StopCurrentActionCoroutines();

            healTarget = target;
            isExecutingHealCommand = false;
            healRoutine = StartCoroutine(HealRoutine());
        }

        private void StopCurrentActionCoroutines()
        {
            if (gatherRoutine != null)
            {
                StopCoroutine(gatherRoutine);
                gatherRoutine = null;
            }

            if (buildRoutine != null)
            {
                StopCoroutine(buildRoutine);
                buildRoutine = null;
            }

            if (healRoutine != null)
            {
                StopCoroutine(healRoutine);
                healRoutine = null;
            }

            if (currentSupply != null)
            {
                currentSupply.AbortGather();
                currentSupply = null;
            }

            healTarget = null;
            isExecutingHealCommand = false;
            isExecutingGatherCommand = false;
            isExecutingBuildCommand = false;

            if (CanUseAgent())
            {
                Agent.isStopped = false;
                Agent.ResetPath();
            }

            if (TryGetComponent(out Animator anim))
            {
                anim.SetBool(AnimationConstants.IS_GATHERING, false);
                anim.SetFloat(AnimationConstants.SPEED, 0f);
            }

            SetCommandOverrides(null);
        }

        private IEnumerator HealRoutine()
        {
            Animator anim = GetComponent<Animator>();

            Debug.Log("HealRoutine started");

            while (healTarget != null && healTarget.Transform != null)
            {
                if (!IsAliveAndActive())
                    yield break;

                if (healTarget.Owner != Owner)
                    break;

                if (healTarget.CurrentHealth >= healTarget.MaxHealth)
                    break;

                if (!CanUseAgent())
                    yield break;

                float reachDistance = Mathf.Max(Agent.stoppingDistance, healReachDistance);
                float distance = Vector3.Distance(transform.position, healTarget.Transform.position);

                if (distance > reachDistance + 0.15f)
                {
                    Vector3 destination = GetClosestPoint(healTarget.Transform.gameObject);

                    isExecutingHealCommand = true;
                    MoveTo(destination);
                    isExecutingHealCommand = false;

                    if (anim != null)
                    {
                        anim.SetBool(AnimationConstants.IS_GATHERING, false);
                        anim.SetFloat(AnimationConstants.SPEED, Agent.velocity.magnitude);
                    }

                    yield return null;
                    continue;
                }

                Agent.isStopped = true;

                if (anim != null)
                {
                    anim.SetFloat(AnimationConstants.SPEED, 0f);
                    anim.SetBool(AnimationConstants.IS_GATHERING, true);
                }

                healTarget.Heal(healPerTick);

                yield return new WaitForSeconds(healTickRate);

                if (healTarget == null || healTarget.Transform == null)
                    break;

                if (healTarget.Owner != Owner)
                    break;

                if (healTarget.CurrentHealth >= healTarget.MaxHealth)
                    break;

                Agent.isStopped = false;
            }

            if (anim != null)
            {
                anim.SetBool(AnimationConstants.IS_GATHERING, false);
                anim.SetFloat(AnimationConstants.SPEED, 0f);
            }

            if (CanUseAgent())
            {
                Agent.isStopped = false;
                Agent.ResetPath();
            }

            healTarget = null;
            healRoutine = null;
            isExecutingHealCommand = false;
        }
    }
}