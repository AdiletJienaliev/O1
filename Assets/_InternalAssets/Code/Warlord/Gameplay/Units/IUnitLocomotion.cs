using UnityEngine;
using Warlord.Core;

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

        /// <summary>
        /// Текущая скорость в мировых осях. На сервере её знает сама навигация, и для анимации
        /// это куда честнее, чем смещение трансформа за кадр: агент тормозит и разгоняется
        /// плавно, а покадровая дельта дрожит на каждом пересчёте пути.
        /// </summary>
        Vector3 Velocity { get; }

        /// <summary>Юнит стоит на месте (дошёл или остановлен).</summary>
        bool IsStopped { get; }

        /// <summary>
        /// Разворачивается ли корпус сам, по вектору движения. Выключается на шаге:
        /// тогда направление взгляда задаёт вызывающий через <see cref="FaceTowards"/>,
        /// а юнит переступает вбок и назад, не теряя противника из виду.
        /// </summary>
        bool FacesMovement { get; set; }

        /// <summary>
        /// Бег или шаг. Скорость применяется сразу при записи, а не со следующей точкой
        /// назначения: путь перекладывается раз в repathInterval, и походка, привязанная
        /// к нему, отставала бы от намерения почти на полсекунды.
        /// </summary>
        Gait Gait { get; set; }

        void Configure(float runSpeed, float walkSpeed, float turnSpeed);

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
