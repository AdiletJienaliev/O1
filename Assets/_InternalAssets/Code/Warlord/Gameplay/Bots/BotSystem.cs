using System.Collections.Generic;
using UnityEngine;
using Warlord.Core;
using Warlord.Gameplay.Match;

namespace Warlord.Gameplay.Bots
{
    /// <summary>
    /// Боты в общем боевом такте (ГДД §12). Отдельная система, а не Update на объекте:
    /// решения ботов обязаны считаться в том же фиксированном шаге, что и весь остальной
    /// матч, иначе на просевшем кадре бот думал бы реже, чем идёт бой, и вёл бы себя
    /// по-разному на разных машинах.
    ///
    /// Стоит между экономикой и очередью постройки: бот принимает решение с уже начисленным
    /// золотом, и его покупка попадает в ту же очередь тем же тактом — как нажатие живого
    /// игрока, случившееся между двумя тактами.
    /// </summary>
    public sealed class BotSystem : IServerSystem, IServerSystemLifecycle
    {
        private readonly IMatchContext _context;
        private readonly List<BotPlayer> _bots = new(PlayerSlots.MaxSupported);

        public BotSystem(IMatchContext context) => _context = context;

        public int Order => ServerSystemOrder.Bots;

        public IReadOnlyList<BotPlayer> Bots => _bots;

        public bool HasBots => _bots.Count > 0;

        public void Add(BotPlayer bot)
        {
            if (bot != null && !_bots.Contains(bot))
                _bots.Add(bot);
        }

        public void Remove(int slot)
        {
            for (int i = _bots.Count - 1; i >= 0; i--)
            {
                if (_bots[i].Slot == slot)
                    _bots.RemoveAt(i);
            }
        }

        public BotPlayer Get(int slot)
        {
            for (int i = 0; i < _bots.Count; i++)
            {
                if (_bots[i].Slot == slot)
                    return _bots[i];
            }

            return null;
        }

        public void OnMatchStarted()
        {
            _context.Events.UnitKilled += OnUnitKilled;
            _context.Events.HeroKilled += OnHeroKilled;
        }

        public void OnMatchFinished()
        {
            _context.Events.UnitKilled -= OnUnitKilled;
            _context.Events.HeroKilled -= OnHeroKilled;
            _bots.Clear();
        }

        public void Tick(float deltaTime)
        {
            if (_context.Phase != MatchPhase.Running || _bots.Count == 0)
                return;

            float duration = Mathf.Max(1f, _context.Settings.MatchDuration);
            float progress = Mathf.Clamp01(_context.ServerTime / duration);

            for (int i = 0; i < _bots.Count; i++)
                _bots[i].Tick(deltaTime, progress, _context.ServerTime);
        }

        /// <summary>
        /// Потери кормят злопамятность характеров. Одна строка на убийство: копить обиды
        /// в самих ботах через опрос состояния было бы дороже и всё равно неточно —
        /// событие уже знает и жертву, и обидчика.
        /// </summary>
        private void OnUnitKilled(int ownerSlot, int killerSlot) => Blame(ownerSlot, killerSlot, 0.12f);

        private void OnHeroKilled(int victimSlot, int killerSlot) => Blame(victimSlot, killerSlot, 0.5f);

        private void Blame(int victimSlot, int killerSlot, float amount)
        {
            if (!PlayerSlots.IsValid(killerSlot))
                return;

            BotPlayer bot = Get(victimSlot);
            bot?.NoteAggression(killerSlot, amount);
        }
    }
}
