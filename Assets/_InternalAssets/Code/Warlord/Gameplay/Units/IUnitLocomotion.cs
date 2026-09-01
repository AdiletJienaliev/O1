using UnityEngine;

namespace Warlord.Gameplay.Units
{
    /// <summary>
    /// Передвижение юнита. Абстракция отделяет ИИ от способа навигации:
    /// сейчас это NavMeshAgent, завтра может быть флоу-филд или собственный steering,
    /// и переписывать поведения не придётся.
    /// </summary>
    public interface IUnitLocomotion
    {
        Vector3 Position { get; }

        /// <summary>Юнит стоит на месте (дошёл или остановлен).</summary>
        bool IsStopped { get; }

        void Configure(float moveSpeed, float turnSpeed);

        /// <param name="speedMultiplier">Ускорение при перестроении (ГДД §6, formationRebuildSpeed).</param>
        void MoveTo(Vector3 destination, float speedMultiplier = 1f);

        void Stop();

        /// <summary>Доворот корпуса к цели вручную — нужен при атаке с места.</summary>
        void FaceTowards(Vector3 worldPoint, float deltaTime);

        bool HasArrived(Vector3 destination, float tolerance);

        void Warp(Vector3 position);

        void SetActive(bool active);
    }
}
