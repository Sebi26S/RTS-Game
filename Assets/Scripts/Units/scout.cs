using System.Collections;
using RTS.EventBus;
using RTS.Events;
using RTS.Utilities;
using UnityEngine;
using UnityEngine.AI;

namespace RTS.Units
{
    public class Scout : AbstractUnit, IHealer
    {
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
            if (isExecutingHealCommand)
                return;

            StopHeal();
        }

        public override void Stop()
        {
            base.Stop();
            StopHeal();
            SetCommandOverrides(null);
        }

        public override void Deselect()
        {
            if (decalProjector != null)
            {
                decalProjector.gameObject.SetActive(false);
            }

            IsSelected = false;
            Bus<UnitDeselectedEvent>.Raise(Owner, new UnitDeselectedEvent(this));
        }

        public void HealTarget(IDamageable target)
        {
            if (target == null || target.Transform == null)
                return;

            if (target.Owner != Owner)
                return;

            if (target.CurrentHealth >= target.MaxHealth)
                return;

            StopHeal();

            healTarget = target;
            healRoutine = StartCoroutine(HealRoutine());
        }

        private bool CanUseOwnAgent()
        {
            return Agent != null && Agent.isActiveAndEnabled && Agent.isOnNavMesh;
        }

        private void SetAgentStopped(bool stopped)
        {
            if (CanUseOwnAgent())
                Agent.isStopped = stopped;
        }

        private void StopHeal()
        {
            if (healRoutine != null)
            {
                StopCoroutine(healRoutine);
                healRoutine = null;
            }

            healTarget = null;
            SetAgentStopped(false);

            if (TryGetComponent(out Animator anim))
            {
                anim.SetBool("IsHealing", false);
                anim.SetBool(AnimationConstants.ATTACK, false);
                anim.SetFloat(AnimationConstants.SPEED, CanUseOwnAgent() ? Agent.velocity.magnitude : 0f);
            }

            isExecutingHealCommand = false;
        }

        private IEnumerator HealRoutine()
        {
            Animator anim = GetComponent<Animator>();

            while (healTarget != null && healTarget.Transform != null)
            {
                if (healTarget.Owner != Owner)
                    break;

                if (healTarget.CurrentHealth >= healTarget.MaxHealth)
                    break;

                Vector3 rawDestination = GetClosestPoint(healTarget.Transform.gameObject);
                Vector3 destination = rawDestination;

                if (!TryGetNavMeshPoint(rawDestination, 2f, out destination))
                    goto EndHeal;

                isExecutingHealCommand = true;
                MoveTo(destination);
                isExecutingHealCommand = false;

                while (healTarget != null && healTarget.Transform != null)
                {
                    if (healTarget.Owner != Owner)
                        goto EndHeal;

                    if (healTarget.CurrentHealth >= healTarget.MaxHealth)
                        goto EndHeal;

                    if (!CanUseOwnAgent())
                        goto EndHeal;

                    if (!Agent.pathPending &&
                        Agent.remainingDistance <= Mathf.Max(Agent.stoppingDistance, healReachDistance))
                    {
                        break;
                    }

                    yield return null;
                }

                if (healTarget == null || healTarget.Transform == null)
                    break;

                if (!CanUseOwnAgent())
                    goto EndHeal;

                SetAgentStopped(true);

                if (anim != null)
                {
                    anim.SetBool(AnimationConstants.ATTACK, false);
                    anim.SetBool("IsHealing", true);
                    anim.SetFloat(AnimationConstants.SPEED, 0f);
                }

                while (healTarget != null && healTarget.Transform != null)
                {
                    if (healTarget.Owner != Owner)
                        goto EndHeal;

                    if (healTarget.CurrentHealth >= healTarget.MaxHealth)
                        goto EndHeal;

                    if (!CanUseOwnAgent())
                        goto EndHeal;

                    float allowedDistance = Mathf.Max(Agent.stoppingDistance, healReachDistance);
                    float distance = Vector3.Distance(transform.position, healTarget.Transform.position);

                    if (distance > allowedDistance + 0.25f)
                    {
                        if (anim != null)
                            anim.SetBool("IsHealing", false);

                        SetAgentStopped(false);
                        break;
                    }

                    SetAgentStopped(true);

                    Vector3 dir = healTarget.Transform.position - transform.position;
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 0.001f)
                        transform.forward = dir.normalized;

                    healTarget.Heal(healPerTick);

                    yield return new WaitForSeconds(healTickRate);
                }

                yield return null;
            }

        EndHeal:
            if (anim != null)
            {
                anim.SetBool("IsHealing", false);
                anim.SetBool(AnimationConstants.ATTACK, false);
                anim.SetFloat(AnimationConstants.SPEED, 0f);
            }

            SetAgentStopped(false);

            healTarget = null;
            healRoutine = null;
            isExecutingHealCommand = false;
        }

        private Vector3 GetClosestPoint(GameObject go)
        {
            if (go != null && go.TryGetComponent(out Collider col))
                return col.ClosestPoint(transform.position);

            return go != null ? go.transform.position : transform.position;
        }

        private bool TryGetNavMeshPoint(Vector3 source, float radius, out Vector3 result)
        {
            if (NavMesh.SamplePosition(source, out NavMeshHit hit, radius, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }

            result = transform.position;
            return false;
        }
    }
}