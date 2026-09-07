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

            /// <summary>Радиус накрытия в точке прилёта. 0 — обычная стрела по одной цели.</summary>
            public float SplashRadius;

            public float SplashFactor;
        }

        private readonly DamageQueue _damageQueue;
        private readonly TargetingService _targeting;
        private readonly List<Projectile> _inFlight = new(64);

        public ProjectileSystem(DamageQueue damageQueue, TargetingService targeting)
        {
            _damageQueue = damageQueue;
            _targeting = targeting;
        }

        public int Order => ServerSystemOrder.Projectiles;

        /// <summary>Снаряд выпущен: позиция старта, цель, время полёта. Для визуала на клиентах.</summary>
        public event Action<Vector3, ICombatTarget, float> ProjectileLaunched;

        /// <returns>Время полёта, с. Ноль — снаряд не выпущен. По нему клиенты заводят стрелу.</returns>
        public float Launch(
            ICombatTarget attacker,
            ICombatTarget target,
            int rawDamage,
            float projectileSpeed,
            float splashRadius = 0f,
            float splashFactor = 0f)
        {
            if (!attacker.Exists() || !target.IsAliveTarget() || projectileSpeed <= 0f)
                return 0f;

            float distance = Vector3.Distance(attacker.Position, target.Position);
            float flightTime = distance / projectileSpeed;

            _inFlight.Add(new Projectile
            {
                Attacker = attacker,
                Target = target,
                RawDamage = rawDamage,
                RemainingTime = flightTime,
                SplashRadius = splashRadius,
                SplashFactor = splashFactor
            });

            ProjectileLaunched?.Invoke(attacker.Position, target, flightTime);
            return flightTime;
        }

        public void Tick(float deltaTime)
        {
            for (int i = _inFlight.Count - 1; i >= 0; i--)
            {
                Projectile projectile = _inFlight[i];

                // Цель умерла или была уничтожена до прилёта — стрела уходит в пустоту.
                if (!projectile.Target.IsAliveTarget())
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

                SplashDamage.Apply(
                    _damageQueue,
                    _targeting,
                    projectile.Attacker.OrNull(),
                    projectile.Target,
                    projectile.RawDamage,
                    projectile.SplashRadius,
                    projectile.SplashFactor);

                _inFlight.RemoveAt(i);
            }
        }

        public void Clear() => _inFlight.Clear();
    }
}
