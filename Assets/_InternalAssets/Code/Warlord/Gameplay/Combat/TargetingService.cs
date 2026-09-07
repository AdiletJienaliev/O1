using System.Collections.Generic;
using UnityEngine;
using Warlord.Core;
using Warlord.Domain.Combat;

namespace Warlord.Gameplay.Combat
{
    /// <summary>
    /// Реестр боевых целей и поиск по нему. Цели разложены по слотам владельцев,
    /// поэтому перебор идёт только по врагам. При 4 игроках по 20 юнитов это
    /// линейный скан, которого хватает с запасом; при росте лимита сюда добавляется
    /// пространственная сетка, не меняя вызывающий код.
    /// </summary>
    public sealed class TargetingService
    {
        private readonly List<ICombatTarget>[] _bySlot;
        private readonly Dictionary<ICombatTarget, int> _attackersThisTick = new(128);

        public TargetingService(int slotCount)
        {
            int count = slotCount > 0 ? slotCount : PlayerSlots.MaxSupported;
            _bySlot = new List<ICombatTarget>[count];
            for (int i = 0; i < count; i++)
                _bySlot[i] = new List<ICombatTarget>(32);
        }

        public void Register(ICombatTarget target)
        {
            if (target == null || !PlayerSlots.IsValid(target.OwnerSlot))
                return;

            List<ICombatTarget> bucket = _bySlot[target.OwnerSlot];
            if (!bucket.Contains(target))
                bucket.Add(target);
        }

        public void Unregister(ICombatTarget target)
        {
            if (target == null || !PlayerSlots.IsValid(target.OwnerSlot))
                return;

            _bySlot[target.OwnerSlot].Remove(target);
        }

        public IReadOnlyList<ICombatTarget> GetBySlot(int slot)
        {
            return PlayerSlots.IsValid(slot) && slot < _bySlot.Length ? _bySlot[slot] : System.Array.Empty<ICombatTarget>();
        }

        /// <summary>Сбрасывает счётчики атакующих. Вызывается один раз в начале боевого такта.</summary>
        public void BeginTick() => _attackersThisTick.Clear();

        /// <summary>Юнит сообщает, что взял цель: следующие выбирающие предпочтут менее облепленную.</summary>
        public void ReportAttackerOn(ICombatTarget target)
        {
            if (!target.Exists())
                return;

            _attackersThisTick.TryGetValue(target, out int current);
            _attackersThisTick[target] = current + 1;
        }

        /// <summary>
        /// Ближайшая живая вражеская цель в радиусе. При равном расстоянии выигрывает та,
        /// на которой уже висит меньше атакующих (ГДД §6).
        /// </summary>
        public ICombatTarget FindNearestEnemy(Vector3 origin, int mySlot, float radius, CombatantKind? kindFilter = null)
        {
            float sqrRadius = radius * radius;
            ICombatTarget best = null;
            float bestSqr = float.MaxValue;
            int bestAttackers = int.MaxValue;

            for (int slot = 0; slot < _bySlot.Length; slot++)
            {
                if (slot == mySlot)
                    continue;

                List<ICombatTarget> bucket = _bySlot[slot];
                for (int i = 0; i < bucket.Count; i++)
                {
                    ICombatTarget candidate = bucket[i];
                    if (!candidate.IsAliveTarget())
                        continue;
                    if (kindFilter.HasValue && candidate.Kind != kindFilter.Value)
                        continue;

                    float sqr = (candidate.Position - origin).sqrMagnitude;
                    if (sqr > sqrRadius)
                        continue;

                    _attackersThisTick.TryGetValue(candidate, out int attackers);

                    bool closer = sqr < bestSqr - 0.01f;
                    bool tiedButLessCrowded = Mathf.Abs(sqr - bestSqr) <= 0.01f && attackers < bestAttackers;

                    if (closer || tiedButLessCrowded)
                    {
                        best = candidate;
                        bestSqr = sqr;
                        bestAttackers = attackers;
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// Ближайшая к <paramref name="origin"/> вражеская цель среди тех, кто стоит внутри
        /// зоны боя. Зона задаётся отдельно от точки отсчёта намеренно (ГДД §6, приказ «В атаку»):
        /// драка идёт там, где полководец, а конкретную цель каждый юнит берёт ближайшую к себе —
        /// иначе вся армия ломится в одного противника, а половина стоит без дела.
        /// </summary>
        public ICombatTarget FindNearestEnemyInZone(Vector3 origin, Vector3 zoneCenter, float zoneRadius, int mySlot)
        {
            float sqrZone = zoneRadius * zoneRadius;
            ICombatTarget best = null;
            float bestSqr = float.MaxValue;
            int bestAttackers = int.MaxValue;

            for (int slot = 0; slot < _bySlot.Length; slot++)
            {
                if (slot == mySlot)
                    continue;

                List<ICombatTarget> bucket = _bySlot[slot];
                for (int i = 0; i < bucket.Count; i++)
                {
                    ICombatTarget candidate = bucket[i];
                    if (!candidate.IsAliveTarget())
                        continue;

                    if ((candidate.Position - zoneCenter).sqrMagnitude > sqrZone)
                        continue;

                    float sqr = (candidate.Position - origin).sqrMagnitude;
                    _attackersThisTick.TryGetValue(candidate, out int attackers);

                    bool closer = sqr < bestSqr - 0.01f;
                    bool tiedButLessCrowded = Mathf.Abs(sqr - bestSqr) <= 0.01f && attackers < bestAttackers;

                    if (closer || tiedButLessCrowded)
                    {
                        best = candidate;
                        bestSqr = sqr;
                        bestAttackers = attackers;
                    }
                }
            }

            return best;
        }

        /// <summary>Все живые цели в радиусе, включая своих. Используется зонами захвата и лечения.</summary>
        public void CollectInRadius(Vector3 origin, float radius, CombatantKind? kindFilter, List<ICombatTarget> results)
        {
            results.Clear();
            float sqrRadius = radius * radius;

            for (int slot = 0; slot < _bySlot.Length; slot++)
            {
                List<ICombatTarget> bucket = _bySlot[slot];
                for (int i = 0; i < bucket.Count; i++)
                {
                    ICombatTarget candidate = bucket[i];
                    if (!candidate.IsAliveTarget())
                        continue;
                    if (kindFilter.HasValue && candidate.Kind != kindFilter.Value)
                        continue;

                    if ((candidate.Position - origin).sqrMagnitude <= sqrRadius)
                        results.Add(candidate);
                }
            }
        }

        public void Clear()
        {
            for (int i = 0; i < _bySlot.Length; i++)
                _bySlot[i].Clear();

            _attackersThisTick.Clear();
        }
    }
}
