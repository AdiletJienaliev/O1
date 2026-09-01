using UnityEngine;
using Warlord.Domain.Combat;

namespace Warlord.Networking.LagCompensation
{
    /// <summary>
    /// Перемотка позиций целей назад во времени при серверной валидации удара (ГДД §12).
    /// Абстракция намеренная: сейчас используется собственная история снимков,
    /// но её можно заменить на ColliderRollback из FishNet, не трогая боевой код.
    /// </summary>
    public interface ILagCompensator
    {
        void Track(ICombatTarget target);
        void Untrack(ICombatTarget target);

        /// <summary>Снимает позиции всех отслеживаемых целей. Вызывается раз в боевой такт.</summary>
        void CaptureSnapshot(float serverTime);

        /// <summary>Где цель находилась в указанный момент серверного времени.</summary>
        Vector3 GetPositionAt(ICombatTarget target, float serverTime);

        void Clear();
    }
}
