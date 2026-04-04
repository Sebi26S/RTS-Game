using System.Collections.Generic;
using RTS.EventBus;
using RTS.Events;
using RTS.TechTree;
using RTS.Utilities;
using UnityEngine;
using UnityEngine.AI;

namespace RTS.Units
{
    [RequireComponent(typeof(NavMeshAgent))]
    public abstract class AbstractUnit : AbstractCommandable, IMoveable, IAttacker
    {
        public float AgentRadius => Agent != null ? Agent.radius : 0f;

        [field: SerializeField] public ParticleSystem AttackingParticleSystem { get; private set; }
        [SerializeField] private DamageableSensor DamageableSensor;

        public NavMeshAgent Agent { get; private set; }
        public Sprite Icon => UnitSO.Icon;
        protected UnitSO unitSO;

        private Animator animator;

        private enum State { Idle, Moving, AttackingTarget, AttackingMove }
        private State state = State.Idle;

        private Vector3 moveTarget;

        private IDamageable attackTarget;
        private Vector3 attackMoveTarget;
        private float lastAttackTime;
        private bool isAttackMoveActive;

        protected override void Awake()
        {
            base.Awake();

            Agent = GetComponent<NavMeshAgent>();
            animator = GetComponent<Animator>();

            unitSO = UnitSO as UnitSO;
        }

        protected override void Start()
        {
            base.Start();

            CurrentHealth = UnitSO.Health;
            MaxHealth = UnitSO.Health;

            Bus<UnitSpawnEvent>.Raise(Owner, new UnitSpawnEvent(this));

            if (DamageableSensor != null)
            {
                DamageableSensor.OnUnitEnter += HandleUnitEnter;
                DamageableSensor.OnUnitExit += HandleUnitExit;
                DamageableSensor.Owner = Owner;
                DamageableSensor.SetupFrom(unitSO.AttackConfig);
            }

            if (this is AbstractCommandable commandable)
            {
                commandable.OnDamaged += HandleDamaged;
            }

            foreach (UpgradeSO upgrade in unitSO.Upgrades)
            {
                if (unitSO.TechTree.IsResearched(Owner, upgrade))
                {
                    upgrade.Apply(unitSO);
                }
            }

            Bus<PopulationEvent>.Raise(Owner, new PopulationEvent(
                Owner,
                0,
                unitSO.PopulationConfig.PopulationSupply
            ));
        }

        protected bool IsAliveAndActive()
        {
            return this != null
                   && gameObject != null
                   && gameObject.activeInHierarchy
                   && CurrentHealth > 0;
        }

        protected bool CanUseAgent()
        {
            return IsAliveAndActive()
                   && Agent != null
                   && Agent.isActiveAndEnabled
                   && Agent.isOnNavMesh;
        }

        protected void SafeStopAgent()
        {
            if (!CanUseAgent())
                return;

            Agent.isStopped = true;
            Agent.ResetPath();
        }

        private void Update()
        {
            if (!IsAliveAndActive())
                return;

            if (animator != null)
            {
                float speed = CanUseAgent() ? Agent.velocity.magnitude : 0f;
                animator.SetFloat(AnimationConstants.SPEED, speed);
            }

            if (!CanUseAgent())
                return;

            switch (state)
            {
                case State.Moving:
                    TickMove();
                    break;

                case State.AttackingMove:
                    TickAttackMove();
                    break;

                case State.AttackingTarget:
                    TickAttackTarget();
                    break;
            }

            OnUnitUpdate();
        }

        protected virtual void OnUnitUpdate() { }

        protected virtual void OnBeforeCommand() { }

        protected virtual bool CanAutoRetaliate()
        {
            return true;
        }

        private void TickAttackTarget()
        {
            if (!CanUseAgent())
                return;

            if (unitSO == null || unitSO.AttackConfig == null)
            {
                Stop();
                return;
            }

            if (attackTarget != null && attackTarget.Owner == Owner)
            {
                attackTarget = null;
            }

            if (attackTarget == null || attackTarget.Transform == null || attackTarget.CurrentHealth <= 0)
            {
                attackTarget = null;

                if (animator != null)
                    animator.SetBool(AnimationConstants.ATTACK, false);

                if (isAttackMoveActive)
                {
                    state = State.AttackingMove;

                    if (CanUseAgent())
                    {
                        Agent.isStopped = false;
                        Agent.SetDestination(attackMoveTarget);
                    }
                }
                else
                {
                    state = State.Idle;

                    if (CanUseAgent())
                        Agent.isStopped = true;
                }

                return;
            }

            Vector3 targetPos = attackTarget.Transform.position;
            float range = unitSO.AttackConfig.AttackRange;
            float dist = Vector3.Distance(transform.position, targetPos);

            if (dist > range)
            {
                if (animator != null)
                    animator.SetBool(AnimationConstants.ATTACK, false);

                if (!CanUseAgent())
                    return;

                Agent.isStopped = false;
                Agent.SetDestination(targetPos);
                return;
            }

            if (!CanUseAgent())
                return;

            Agent.isStopped = true;

            Vector3 dir = targetPos - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                transform.forward = dir.normalized;

            if (animator != null)
                animator.SetBool(AnimationConstants.ATTACK, true);

            if (Time.time >= lastAttackTime + unitSO.AttackConfig.AttackDelay)
            {
                lastAttackTime = Time.time;

                if (AttackingParticleSystem != null)
                    AttackingParticleSystem.Play();

                attackTarget.TakeDamage(unitSO.AttackConfig.Damage, this);
            }
        }

        private void TickAttackMove()
        {
            if (!CanUseAgent())
                return;

            if (animator != null)
                animator.SetBool(AnimationConstants.ATTACK, false);

            if (TryAcquireAttackMoveTarget())
            {
                state = State.AttackingTarget;
                return;
            }

            if (!Agent.pathPending && Agent.remainingDistance <= Agent.stoppingDistance)
            {
                Agent.isStopped = true;
                state = State.Idle;
            }
        }

        private bool TryAcquireAttackMoveTarget()
        {
            if (DamageableSensor == null || DamageableSensor.Damageables == null)
                return false;

            IDamageable bestTarget = null;
            float bestPriority = float.MinValue;
            float bestDistanceSqr = float.MaxValue;

            foreach (IDamageable damageable in DamageableSensor.Damageables)
            {
                if (damageable == null) continue;
                if (damageable.Transform == null) continue;
                if (damageable.CurrentHealth <= 0) continue;
                if (damageable.Owner == Owner) continue;

                float priority = GetTargetPriorityScore(damageable);
                float distanceSqr = (damageable.Transform.position - transform.position).sqrMagnitude;

                if (priority > bestPriority || (Mathf.Approximately(priority, bestPriority) && distanceSqr < bestDistanceSqr))
                {
                    bestPriority = priority;
                    bestDistanceSqr = distanceSqr;
                    bestTarget = damageable;
                }
            }

            if (bestTarget == null)
                return false;

            attackTarget = bestTarget;
            return true;
        }

        private float GetTargetPriorityScore(IDamageable target)
        {
            if (target == null || target.Transform == null)
                return -9999f;

            if (target is AbstractUnit unit)
            {
                if (unit is Worker)
                    return 80f;

                return 100f;
            }

            if (target is BaseBuilding building)
            {
                if (building.BuildingSO != null && building.QueueSize >= 0)
                {
                    if (IsProductionBuilding(building))
                        return 60f;
                }

                if (IsCommandPost(building))
                    return 40f;

                return 20f;
            }

            return 0f;
        }

        private bool IsProductionBuilding(BaseBuilding building)
        {
            if (building == null || building.UnitSO == null)
                return false;

            string buildingName = building.UnitSO.name.ToLowerInvariant();

            return buildingName.Contains("barracks")
                   || buildingName.Contains("infantry school")
                   || buildingName.Contains("supply hut");
        }

        private bool IsCommandPost(BaseBuilding building)
        {
            if (building == null || building.UnitSO == null)
                return false;

            string buildingName = building.UnitSO.name.ToLowerInvariant();
            return buildingName.Contains("command post");
        }

        private void TickMove()
        {
            if (!CanUseAgent())
                return;

            if (!Agent.pathPending && Agent.remainingDistance <= Agent.stoppingDistance)
            {
                Agent.isStopped = true;
                state = State.Idle;
            }
        }

        public void MoveTo(Vector3 position)
        {
            OnBeforeCommand();

            attackTarget = null;
            moveTarget = position;

            if (animator != null)
                animator.SetBool(AnimationConstants.ATTACK, false);

            if (!CanUseAgent())
                return;

            Agent.isStopped = false;
            Agent.SetDestination(moveTarget);

            state = State.Moving;
        }

        public void MoveTo(Transform transform)
        {
            if (transform == null)
                return;

            MoveTo(transform.position);
        }

        public virtual void Stop()
        {
            OnBeforeCommand();

            SetCommandOverrides(null);

            attackTarget = null;
            isAttackMoveActive = false;
            state = State.Idle;

            if (CanUseAgent())
            {
                Agent.isStopped = true;
                Agent.ResetPath();
                Agent.velocity = Vector3.zero;
            }

            if (TryGetComponent(out Animator foundAnimator))
            {
                foundAnimator.SetFloat(AnimationConstants.SPEED, 0f);
            }

            if (animator != null)
                animator.SetBool(AnimationConstants.ATTACK, false);
        }

        public void Attack(IDamageable damageable)
        {
            OnBeforeCommand();

            if (damageable == null || damageable.Transform == null)
            {
                Stop();
                return;
            }

            if (damageable.Owner == Owner)
                return;

            attackTarget = damageable;
            isAttackMoveActive = false;

            if (!CanUseAgent())
                return;

            Agent.isStopped = false;
            state = State.AttackingTarget;
        }

        public void Attack(Vector3 location)
        {
            OnBeforeCommand();

            attackTarget = null;
            attackMoveTarget = location;
            isAttackMoveActive = true;

            if (animator != null)
                animator.SetBool(AnimationConstants.ATTACK, false);

            if (!CanUseAgent())
                return;

            Agent.isStopped = false;
            Agent.SetDestination(attackMoveTarget);

            state = State.AttackingMove;
        }

        private void HandleDamaged(AbstractCommandable commandable, IDamageable attacker)
        {
            if (attacker == null) return;
            if (attacker.Transform == null) return;
            if (attacker.CurrentHealth <= 0) return;
            if (attacker.Owner == Owner) return;
            if (attacker.Owner == Owner.Invalid) return;
            if (attacker.Owner == Owner.Unowned) return;
            if (!CanAutoRetaliate()) return;

            if (state != State.Idle && state != State.Moving)
                return;

            Attack(attacker);
        }

        private void HandleUnitEnter(IDamageable damageable)
        {
            if (state != State.AttackingMove) return;
            if (attackTarget != null) return;

            if (damageable == null || damageable.Transform == null) return;
            if (damageable.Owner == Owner) return;

            attackTarget = damageable;
            state = State.AttackingTarget;
        }

        private void HandleUnitExit(IDamageable damageable)
        {
            if (attackTarget != damageable)
                return;

            attackTarget = null;

            if (animator != null)
                animator.SetBool(AnimationConstants.ATTACK, false);

            if (isAttackMoveActive)
            {
                state = State.AttackingMove;

                if (CanUseAgent())
                {
                    Agent.isStopped = false;
                    Agent.SetDestination(attackMoveTarget);
                }
            }
            else
            {
                state = State.Idle;

                if (CanUseAgent())
                    Agent.isStopped = true;
            }
        }

        private List<GameObject> SetNearbyEnemiesOnBlackboard()
        {
            if (DamageableSensor == null || DamageableSensor.Damageables == null)
            {
                return new List<GameObject>();
            }

            DamageableSensor.Damageables.RemoveAll(d => d == null || d.Transform == null);

            List<GameObject> nearbyEnemies = DamageableSensor.Damageables
                .ConvertAll(d => d.Transform.gameObject);

            if (this != null)
            {
                nearbyEnemies.Sort(new ClosestGameObjectComparer(transform.position));
            }

            return nearbyEnemies;
        }

        protected override void OnDestroy()
        {
            if (DamageableSensor != null)
            {
                DamageableSensor.OnUnitEnter -= HandleUnitEnter;
                DamageableSensor.OnUnitExit -= HandleUnitExit;
            }

            if (this is AbstractCommandable commandable)
            {
                commandable.OnDamaged -= HandleDamaged;
            }

            base.OnDestroy();
            Bus<UnitDeathEvent>.Raise(Owner, new UnitDeathEvent(this));
        }
    }
}