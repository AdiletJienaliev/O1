using UnityEngine;
using UnityEngine.AI;

namespace Warlord.Gameplay.Units
{
    /// <summary>
    /// Реализация передвижения через NavMeshAgent. Живёт только на сервере:
    /// на клиентах агент выключен, позиция приходит из <see cref="UnitTransformSync"/>.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class NavMeshUnitLocomotion : MonoBehaviour, IUnitLocomotion
    {
        [SerializeField] private NavMeshAgent agent;

        private float _baseSpeed;

        private void Awake() => agent ??= GetComponent<NavMeshAgent>();

        public Vector3 Position => transform.position;

        public bool IsStopped => agent == null || !agent.enabled || agent.isStopped;

        public void Configure(float moveSpeed, float turnSpeed)
        {
            if (agent == null)
                return;

            _baseSpeed = moveSpeed;
            agent.speed = moveSpeed;
            agent.angularSpeed = turnSpeed;
            agent.acceleration = moveSpeed * 4f;
            agent.stoppingDistance = 0f;
        }

        public void MoveTo(Vector3 destination, float speedMultiplier = 1f)
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
                return;

            agent.speed = _baseSpeed * Mathf.Max(0.1f, speedMultiplier);
            agent.isStopped = false;
            agent.SetDestination(destination);
        }

        public void Stop()
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
                return;

            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        public void FaceTowards(Vector3 worldPoint, float deltaTime)
        {
            Vector3 direction = worldPoint - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.0001f)
                return;

            float turnSpeed = agent != null ? agent.angularSpeed : 360f;
            Quaternion target = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * deltaTime);
        }

        public bool HasArrived(Vector3 destination, float tolerance)
        {
            Vector3 delta = destination - transform.position;
            delta.y = 0f;
            return delta.sqrMagnitude <= tolerance * tolerance;
        }

        public void Warp(Vector3 position)
        {
            if (agent != null && agent.enabled)
                agent.Warp(position);
            else
                transform.position = position;
        }

        public void SetActive(bool active)
        {
            if (agent != null)
                agent.enabled = active;
        }
    }
}
