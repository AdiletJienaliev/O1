using UnityEngine;
using Warlord.Core;

namespace Warlord.Domain.Bots
{
    /// <summary>
    /// Память бота о том, что он видел. Нужна ровно затем, чтобы честный обзор не означал
    /// амнезию: увидев на аванпосте пять чужих юнитов, живой игрок будет считаться с ними
    /// и через десять секунд, даже перестав их видеть, — но и не будет верить своим данным
    /// вечно. Здесь это выражено сроком годности: чем старше запись, тем слабее она влияет.
    ///
    /// Без памяти бот с честным обзором ведёт себя как рыбка: отвернулся — забыл, вернулся —
    /// снова полез в ту же мясорубку. Именно это выдаёт машину сильнее всего.
    /// </summary>
    public sealed class BotMemory
    {
        private struct Trace
        {
            public bool Valid;
            public float Time;
            public int OwnerSlot;
            public float EnemyForce;
            public float FriendlyForce;
            public int GuardCount;
            public Vector3 Position;
            public float Health;
        }

        private readonly Trace[] _points;
        private readonly Trace[] _heroes;
        private readonly Trace[] _armies;
        private readonly float[] _aggression;

        public BotMemory(int pointCapacity, int slotCount)
        {
            _points = new Trace[Mathf.Max(1, pointCapacity)];
            _heroes = new Trace[Mathf.Max(1, slotCount)];
            _armies = new Trace[Mathf.Max(1, slotCount)];
            _aggression = new float[Mathf.Max(1, slotCount)];
        }

        #region Точки

        public void RememberPoint(int index, int ownerSlot, float enemyForce, float friendlyForce, int guards, float now)
        {
            if (index < 0 || index >= _points.Length)
                return;

            _points[index] = new Trace
            {
                Valid = true,
                Time = now,
                OwnerSlot = ownerSlot,
                EnemyForce = enemyForce,
                FriendlyForce = friendlyForce,
                GuardCount = guards
            };
        }

        /// <summary>
        /// Вспомнить обстановку на точке. Сила затухает со временем: старая запись
        /// не исчезает разом, а становится всё менее весомой — так же, как убеждённость
        /// живого игрока в том, что «там кто-то был».
        /// </summary>
        public bool TryRecallPoint(
            int index,
            float now,
            float memorySeconds,
            out int ownerSlot,
            out float enemyForce,
            out float friendlyForce,
            out int guards,
            out float staleSeconds)
        {
            ownerSlot = PlayerSlots.None;
            enemyForce = 0f;
            friendlyForce = 0f;
            guards = 0;
            staleSeconds = float.MaxValue;

            if (index < 0 || index >= _points.Length || !_points[index].Valid)
                return false;

            Trace trace = _points[index];
            staleSeconds = Mathf.Max(0f, now - trace.Time);

            float fade = Fade(staleSeconds, memorySeconds);
            if (fade <= 0f)
                return false;

            ownerSlot = trace.OwnerSlot;
            enemyForce = trace.EnemyForce * fade;
            friendlyForce = trace.FriendlyForce * fade;
            guards = trace.GuardCount;

            return true;
        }

        #endregion

        #region Полководцы и армии

        public void RememberHero(int slot, Vector3 position, float healthFraction, float now)
        {
            if (slot < 0 || slot >= _heroes.Length)
                return;

            _heroes[slot] = new Trace
            {
                Valid = true,
                Time = now,
                Position = position,
                Health = healthFraction
            };
        }

        public bool TryRecallHero(int slot, float now, float memorySeconds, out Vector3 position, out float health, out float staleSeconds)
        {
            position = Vector3.zero;
            health = 1f;
            staleSeconds = float.MaxValue;

            if (slot < 0 || slot >= _heroes.Length || !_heroes[slot].Valid)
                return false;

            staleSeconds = Mathf.Max(0f, now - _heroes[slot].Time);

            if (Fade(staleSeconds, memorySeconds) <= 0f)
                return false;

            position = _heroes[slot].Position;
            health = _heroes[slot].Health;
            return true;
        }

        public void RememberArmy(int slot, float power, int count, float now)
        {
            if (slot < 0 || slot >= _armies.Length)
                return;

            _armies[slot] = new Trace
            {
                Valid = true,
                Time = now,
                EnemyForce = power,
                GuardCount = count
            };
        }

        public bool TryRecallArmy(int slot, float now, float memorySeconds, out float power, out int count)
        {
            power = 0f;
            count = 0;

            if (slot < 0 || slot >= _armies.Length || !_armies[slot].Valid)
                return false;

            float fade = Fade(Mathf.Max(0f, now - _armies[slot].Time), memorySeconds);
            if (fade <= 0f)
                return false;

            power = _armies[slot].EnemyForce * fade;
            count = _armies[slot].GuardCount;
            return true;
        }

        #endregion

        #region Обиды

        /// <summary>Записать урон от конкретного соперника. Кормит злопамятность характера.</summary>
        public void NoteAggression(int slot, float amount)
        {
            if (slot >= 0 && slot < _aggression.Length)
                _aggression[slot] = Mathf.Clamp01(_aggression[slot] + amount);
        }

        public float Aggression(int slot)
        {
            return slot >= 0 && slot < _aggression.Length ? _aggression[slot] : 0f;
        }

        /// <summary>Обиды остывают. Иначе первая же стычка определяла бы поведение до конца матча.</summary>
        public void DecayAggression(float deltaTime, float halfLifeSeconds = 30f)
        {
            float factor = Mathf.Exp(-deltaTime / Mathf.Max(0.1f, halfLifeSeconds));

            for (int i = 0; i < _aggression.Length; i++)
                _aggression[i] *= factor;
        }

        #endregion

        /// <summary>
        /// Вес воспоминания: первую треть срока данные считаются свежими, дальше плавно
        /// теряют силу и к концу срока обнуляются.
        /// </summary>
        private static float Fade(float age, float memorySeconds)
        {
            float limit = Mathf.Max(1f, memorySeconds);

            if (age >= limit)
                return 0f;

            float fresh = limit * 0.35f;
            return age <= fresh ? 1f : 1f - (age - fresh) / (limit - fresh);
        }
    }
}
