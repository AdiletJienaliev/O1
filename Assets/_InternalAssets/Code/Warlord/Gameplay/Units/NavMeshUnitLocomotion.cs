using UnityEngine;
using UnityEngine.AI;
using Warlord.Core;

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

        private float _runSpeed = 1f;
        private float _walkSpeed = 1f;
        private float _speedMultiplier = 1f;
        private bool _facesMovement = true;
        private Gait _gait = Gait.Run;

        private void Awake() => agent ??= GetComponent<NavMeshAgent>();

        /// <summary>
        /// Разворот корпуса отдан самому агенту, пока юнит бежит: он и так ведёт его по пути,
        /// и доворачивать корпус ещё и руками значит драться с агентом за один и тот же поворот.
        /// На шаге агент от поворота отстраняется, и корпусом распоряжается вызывающий.
        /// </summary>
        public bool FacesMovement
        {
            get => _facesMovement;
            set
            {
                _facesMovement = value;

                if (agent != null && agent.enabled)
                    agent.updateRotation = value;
            }
        }

        public Vector3 Position => transform.position;

        public Vector3 Velocity => agent != null && agent.enabled && agent.isOnNavMesh
            ? agent.velocity
            : Vector3.zero;

        public bool IsStopped => agent == null || !agent.enabled || agent.isStopped;

        public Gait Gait
        {
            get => _gait;
            set
            {
                _gait = value;
                ApplySpeed();
            }
        }

        public void Configure(float runSpeed, float walkSpeed, float turnSpeed)
        {
            if (agent == null)
                return;

            _runSpeed = Mathf.Max(0.1f, runSpeed);
            _walkSpeed = Mathf.Clamp(walkSpeed, 0.1f, _runSpeed);

            agent.angularSpeed = turnSpeed;

            // Разгон считаем от бега: на шаге та же величина ощущается как рывок с места,
            // но шагом юнит и трогается с места редко — он им дохаживает последние метры.
            agent.acceleration = _runSpeed * 4f;
            agent.stoppingDistance = 0f;

            ApplySpeed();
        }

        public void MoveTo(Vector3 destination, float speedMultiplier = 1f)
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
                return;

            _speedMultiplier = Mathf.Max(0.1f, speedMultiplier);
            ApplySpeed();

            agent.isStopped = false;
            agent.SetDestination(destination);
        }

        private void ApplySpeed()
        {
            if (agent == null || !agent.enabled)
                return;

            float baseSpeed = _gait == Gait.Run ? _runSpeed : _walkSpeed;
            agent.speed = baseSpeed * _speedMultiplier;
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
            if (agent == null)
                return;

            agent.enabled = active;

            // updateRotation живёт на включённом агенте: выставленный до включения режим
            // сбрасывается обратно в дефолт, и юнит начинал бы жизнь с чужой походкой.
            if (!active)
                return;

            // updateRotation и speed живут на включённом агенте: выставленные до включения,
            // они сбрасываются в дефолт, и юнит начинал бы жизнь с чужой походкой.
            agent.updateRotation = _facesMovement;
            ApplySpeed();
        }
    }
}
