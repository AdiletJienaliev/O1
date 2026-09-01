using UnityEngine;
using Warlord.Core;

namespace Warlord.Domain.Combat
{
    /// <summary>
    /// Всё, во что можно целиться и чему можно нанести урон: юниты и полководцы.
    /// Единый контракт нужен, чтобы таргетинг и разрешение урона не знали, кто перед ними.
    /// </summary>
    public interface ICombatTarget
    {
        /// <summary>Слот владельца. FFA: разные слоты всегда враги.</summary>
        int OwnerSlot { get; }

        CombatantKind Kind { get; }

        /// <summary>Индекс типа в ростере. -1 для полководца — он не участвует в матрице урона.</summary>
        int UnitTypeIndex { get; }

        bool IsAlive { get; }

        Vector3 Position { get; }

        /// <summary>Радиус тела: дальность атаки считается до края, а не до центра.</summary>
        float Radius { get; }

        /// <summary>Плоское снижение входящего урона.</summary>
        int Armor { get; }

        /// <summary>Вызывается только системой разрешения урона в конце боевого такта.</summary>
        void ReceiveDamage(int amount, ICombatTarget source);
    }
}
