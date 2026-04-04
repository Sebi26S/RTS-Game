using UnityEngine;

namespace RTS.Units
{
    public class Skeleton : AbstractUnit
    {
        [Header("Skeleton AI")]
        [SerializeField] private float aggroRadius = 3f;
        [SerializeField] private float maxChaseDistanceFromHome = 5f;
        [SerializeField] private float repathInterval = 0.25f;
        [SerializeField] private LayerMask targetLayers = ~0;

        private Vector3 homePosition;
        private IDamageable currentTarget;
        private float nextSearchTime;
        private bool returningHome;

        protected override void Start()
        {
            base.Start();

            homePosition = transform.position;
            Stop();

            OnDamaged += HandleSkeletonDamaged;

            if (maxChaseDistanceFromHome < aggroRadius)
            {
                maxChaseDistanceFromHome = aggroRadius;
            }
        }

        protected override void OnUnitUpdate()
        {
            HandleSkeletonBehaviour();
        }

        private void HandleSkeletonBehaviour()
        {
            if (!IsAliveAndActive())
                return;

            if (returningHome)
            {
                if (HasReachedHome())
                {
                    returningHome = false;
                    currentTarget = null;
                    Stop();
                    nextSearchTime = 0f;
                }

                return;
            }

            if (currentTarget != null)
            {
                if (!IsValidTarget(currentTarget))
                {
                    ReturnHome();
                    return;
                }

                float targetDistanceFromHome = Vector3.Distance(currentTarget.Transform.position, homePosition);

                if (targetDistanceFromHome > maxChaseDistanceFromHome)
                {
                    ReturnHome();
                    return;
                }

                Attack(currentTarget);
                return;
            }

            if (Time.time < nextSearchTime)
                return;

            nextSearchTime = Time.time + repathInterval;

            IDamageable foundTarget = FindClosestEnemyNearHome();
            if (foundTarget != null)
            {
                currentTarget = foundTarget;
                returningHome = false;
                Attack(currentTarget);
            }
        }

        private bool HasReachedHome()
        {
            if (!CanUseAgent())
                return Vector3.Distance(transform.position, homePosition) <= 0.5f;

            bool arrivedByPath =
                !Agent.pathPending &&
                Agent.remainingDistance <= Agent.stoppingDistance + 0.05f;

            bool arrivedByDistance =
                Vector3.Distance(transform.position, homePosition) <= Mathf.Max(0.5f, Agent.stoppingDistance + 0.05f);

            return arrivedByPath || arrivedByDistance;
        }

        private void HandleSkeletonDamaged(AbstractCommandable commandable, IDamageable attacker)
        {
            if (attacker == null)
                return;

            if (attacker.Transform == null)
                return;

            if (!IsValidTarget(attacker))
                return;

            float attackerDistanceFromHome = Vector3.Distance(attacker.Transform.position, homePosition);

            if (attackerDistanceFromHome > maxChaseDistanceFromHome)
                return;

            currentTarget = attacker;
            returningHome = false;
            Attack(currentTarget);
        }

        private IDamageable FindClosestEnemyNearHome()
        {
            Collider[] hits = Physics.OverlapSphere(homePosition, aggroRadius, targetLayers);

            IDamageable bestTarget = null;
            float bestDistanceSqr = float.MaxValue;

            foreach (Collider hit in hits)
            {
                if (hit == null)
                    continue;

                IDamageable damageable = hit.GetComponentInParent<IDamageable>();
                if (damageable == null)
                    continue;

                if (!IsValidTarget(damageable))
                    continue;

                float distanceFromHomeSqr = (damageable.Transform.position - homePosition).sqrMagnitude;

                if (distanceFromHomeSqr < bestDistanceSqr)
                {
                    bestDistanceSqr = distanceFromHomeSqr;
                    bestTarget = damageable;
                }
            }

            return bestTarget;
        }

        private bool IsValidTarget(IDamageable target)
        {
            if (target == null) return false;
            if (target.Transform == null) return false;
            if (target.CurrentHealth <= 0) return false;
            if (target.Transform == transform) return false;
            if (target.Owner == Owner) return false;
            if (target.Owner == Owner.Invalid) return false;
            if (target.Owner == Owner.Unowned) return false;

            return true;
        }

        private void ReturnHome()
        {
            currentTarget = null;
            returningHome = true;
            MoveTo(homePosition);
        }

        protected override bool CanAutoRetaliate()
        {
            return false;
        }

        protected override void OnDestroy()
        {
            OnDamaged -= HandleSkeletonDamaged;
            base.OnDestroy();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector3 center = Application.isPlaying ? homePosition : transform.position;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(center, aggroRadius);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(center, maxChaseDistanceFromHome);
        }
#endif
    }
}