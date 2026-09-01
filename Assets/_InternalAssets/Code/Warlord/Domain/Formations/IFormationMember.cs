using UnityEngine;

namespace Warlord.Domain.Formations
{
    /// <summary>
    /// Участник построения. Абстракция нужна, чтобы решатель слотов не зависел
    /// от сетевого типа юнита и легко тестировался.
    /// </summary>
    public interface IFormationMember
    {
        /// <summary>Меньше — ближе к первой шеренге (ГДД §7: легионеры вперёд, лучники назад).</summary>
        int FormationPriority { get; }

        /// <summary>Стабильный id для детерминированной сортировки при равном приоритете.</summary>
        int StableId { get; }

        void AssignFormationSlot(int slotIndex, Vector3 worldPosition);
    }
}
