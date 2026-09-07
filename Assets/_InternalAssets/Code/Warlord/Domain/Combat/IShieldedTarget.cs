using UnityEngine;

namespace Warlord.Domain.Combat
{
    /// <summary>
    /// Цель, которая умеет закрываться щитом (ГДД §6, приказ «Защита»).
    /// Отдельный интерфейс, а не поля в <see cref="ICombatTarget"/>: щит есть далеко не у всех,
    /// и таргетинг с полководцами не должен знать про него вообще ничего.
    ///
    /// Реализация обязана быть дешёвой на чтение — её дёргает разрешение урона на каждую заявку.
    /// </summary>
    public interface IShieldedTarget
    {
        /// <summary>Поднят ли щит прямо сейчас. Опущенный щит не защищает ни с какой стороны.</summary>
        bool IsBlocking { get; }

        /// <summary>Куда смотрит носитель щита. Единичный вектор в плоскости XZ.</summary>
        Vector3 BlockFacing { get; }

        /// <summary>Половина сектора блока, град. 90 закрывает всю переднюю полусферу.</summary>
        float BlockAngle { get; }

        /// <summary>Доля урона, проходящая сквозь щит спереди. 0 — фронт не пробивается.</summary>
        float BlockDamageFactor { get; }

        /// <summary>
        /// Удар пришёлся в щит. Вызывается разрешением урона даже когда урон обнулился:
        /// принятый на щит удар должно быть видно и слышно, иначе игрок решит, что промахнулся.
        /// </summary>
        void NotifyBlocked(ICombatTarget attacker);
    }

    /// <summary>Проверка сектора щита. Вынесена из интерфейса, чтобы правило было одно на всех.</summary>
    public static class ShieldBlock
    {
        /// <summary>
        /// Множитель входящего урона от <paramref name="attackerPosition"/>.
        /// 1 — щита нет, он опущен или удар пришёл мимо сектора.
        /// </summary>
        public static float ResolveMultiplier(ICombatTarget target, Vector3 attackerPosition)
        {
            if (target is not IShieldedTarget shield || !shield.IsBlocking)
                return 1f;

            Vector3 toAttacker = attackerPosition - target.Position;
            toAttacker.y = 0f;

            // Удар из той же точки, где стоит цель, направления не имеет — считаем, что щит не помог.
            if (toAttacker.sqrMagnitude < 0.0001f)
                return 1f;

            Vector3 facing = shield.BlockFacing;
            facing.y = 0f;

            if (facing.sqrMagnitude < 0.0001f)
                return 1f;

            float angle = Vector3.Angle(facing, toAttacker);

            return angle <= shield.BlockAngle ? Mathf.Clamp01(shield.BlockDamageFactor) : 1f;
        }
    }
}
