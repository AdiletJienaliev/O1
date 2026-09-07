using System;
using System.Collections.Generic;
using Warlord.Core;
using Warlord.Domain.Combat;

namespace Warlord.Gameplay.Combat
{
    /// <summary>
    /// Применяет весь накопленный за такт урон одним пакетом (ГДД §12).
    /// Заявки, поставленные до смерти цели, не отменяются — именно поэтому двое,
    /// убивающие друг друга в одном такте, умирают оба, независимо от пинга.
    /// </summary>
    public sealed class CombatResolutionSystem : IServerSystem
    {
        private readonly DamageQueue _queue;
        private readonly DamageCalculator _calculator;
        private readonly List<KeyValuePair<ICombatTarget, ICombatTarget>> _deaths = new(32);

        public CombatResolutionSystem(DamageQueue queue, DamageCalculator calculator)
        {
            _queue = queue;
            _calculator = calculator;
        }

        public int Order => ServerSystemOrder.CombatResolution;

        /// <summary>Цель погибла. Первый аргумент — жертва, второй — тот, чей удар добил (может быть null).</summary>
        public event Action<ICombatTarget, ICombatTarget> TargetKilled;

        public void Tick(float deltaTime)
        {
            IReadOnlyList<DamageEvent> pending = _queue.Pending;
            if (pending.Count == 0)
                return;

            _deaths.Clear();

            for (int i = 0; i < pending.Count; i++)
            {
                DamageEvent damageEvent = pending[i];
                if (!damageEvent.IsValid)
                    continue;

                // Цель уже добита в этом же такте — оверкилл отбрасываем, но заявку не считаем ошибкой.
                if (!damageEvent.Target.IsAlive)
                    continue;

                // Атакующего могли уничтожить внутри того же такта: дальше он идёт как анонимный
                // источник урона, иначе обращение к его позиции или слоту упало бы.
                ICombatTarget attacker = damageEvent.LivingAttacker;

                int finalDamage = _calculator.Resolve(in damageEvent, out bool blocked);

                // Принятый на щит удар не проходит по здоровью, но событием остаётся:
                // без него юнит молча стоял бы под градом стрел, а в спину ему целиться
                // было бы некому — тот, кого он должен запомнить, так и не запомнился бы.
                if (blocked && damageEvent.Target is IShieldedTarget shielded)
                    shielded.NotifyBlocked(attacker);

                if (finalDamage <= 0)
                    continue;

                damageEvent.Target.ReceiveDamage(finalDamage, attacker);

                if (!damageEvent.Target.IsAlive)
                    _deaths.Add(new KeyValuePair<ICombatTarget, ICombatTarget>(damageEvent.Target, attacker));
            }

            _queue.Clear();

            for (int i = 0; i < _deaths.Count; i++)
                TargetKilled?.Invoke(_deaths[i].Key, _deaths[i].Value);

            _deaths.Clear();
        }
    }
}
