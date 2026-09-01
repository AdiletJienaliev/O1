using System;
using System.Collections.Generic;
using UnityEngine;
using Warlord.Core;
using Warlord.Domain.Combat;

namespace Warlord.Gameplay.Combat
{
    /// <summary>
    /// Полёт снарядов лучников (ГДД §5.2). Снаряд — не сетевой объект, а запись в списке:
    /// сервер считает время полёта, клиенту отдельно уходит визуальный «выстрел».
    /// Так 80 юнитов не превращаются в сотни лишних NetworkObject.
    /// </summary>
    public sealed class ProjectileSystem : IServerSystem
    {
        private struct Projectile
        {
            public ICombatTarget Attacker;
            public ICombatTarget Target;
            public int RawDamage;
            public float RemainingTime;
        }

        private readonly DamageQueue _damageQueue;
        private readonly List<Projectile> _inFlight = new(64);

        public ProjectileSystem(DamageQueue damageQueue) => _damageQueue = damageQueue;

        public int Order => ServerSystemOrder.Projectiles;

        /// <summary>Снаряд выпущен: позиция старта, цель, время полёта. Для визуала на клиентах.</summary>
        public event Action<Vector3, ICombatTarget, float> ProjectileLaunched;

        public void Launch(ICombatTarget attacker, ICombatTarget target, int rawDamage, float projectileSpeed)
        {
            if (attacker == null || target == null || !target.IsAlive || projectileSpeed <= 0f)
                return;

            float distance = Vector3.Distance(attacker.Position, target.Position);
            float flightTime = distance / projectileSpeed;

            _inFlight.Add(new Projectile
            {
                Attacker = attacker,
                Target = target,
                RawDamage = rawDamage,
                RemainingTime = flightTime
            });

            ProjectileLaunched?.Invoke(attacker.Position, target, flightTime);
        }

        public void Tick(float deltaTime)
        {
            for (int i = _inFlight.Count - 1; i >= 0; i--)
            {
                Projectile projectile = _inFlight[i];

                // Цель умерла до прилёта — стрела уходит в пустоту, урон не переносится.
                if (projectile.Target == null || !projectile.Target.IsAlive)
                {
                    _inFlight.RemoveAt(i);
                    continue;
                }

                projectile.RemainingTime -= deltaTime;

                if (projectile.RemainingTime > 0f)
                {
                    _inFlight[i] = projectile;
                    continue;
                }

                _damageQueue.Enqueue(projectile.Attacker, projectile.Target, projectile.RawDamage);
                _inFlight.RemoveAt(i);
            }
        }

        public void Clear() => _inFlight.Clear();
    }
}
