using System.Collections.Generic;
using FishNet.Connection;
using Warlord.Core;
using Warlord.Gameplay.World;

namespace Warlord.Gameplay.Players
{
    /// <summary>
    /// Кто сейчас в матче: соответствие слот - состояние игрока и слот - соединение.
    /// Серверный объект; всё, что нужно клиентам, они получают через свои PlayerState.
    /// </summary>
    public sealed class PlayerRegistry
    {
        private readonly PlayerState[] _bySlot;
        private readonly List<PlayerState> _active = new(PlayerSlots.MaxSupported);
        private readonly List<int> _aliveSlots = new(PlayerSlots.MaxSupported);

        public PlayerRegistry(int slotCount)
        {
            int count = slotCount > 0 ? slotCount : PlayerSlots.MaxSupported;
            _bySlot = new PlayerState[count];
        }

        public int SlotCount => _bySlot.Length;

        /// <summary>Все подключённые игроки, включая выбывших (они остаются в таблице счёта).</summary>
        public IReadOnlyList<PlayerState> Active => _active;

        public PlayerState Get(int slot) => slot >= 0 && slot < _bySlot.Length ? _bySlot[slot] : null;

        /// <summary>
        /// Точки базы слота. Читаются из сцены каждый раз, а не кэшируются при старте:
        /// база — обычный объект сцены, и её можно двигать прямо во время отладки матча.
        /// </summary>
        public PlayerBaseAnchor GetBaseAnchor(int slot) => PlayerBase.GetAnchor(slot);

        public void Add(PlayerState player)
        {
            if (player == null || !PlayerSlots.IsValid(player.Slot) || player.Slot >= _bySlot.Length)
                return;

            _bySlot[player.Slot] = player;
            if (!_active.Contains(player))
                _active.Add(player);
        }

        public void Remove(PlayerState player)
        {
            if (player == null)
                return;

            if (player.Slot >= 0 && player.Slot < _bySlot.Length && _bySlot[player.Slot] == player)
                _bySlot[player.Slot] = null;

            _active.Remove(player);
        }

        /// <summary>Слот по соединению. Нужен, когда команда пришла не от объекта игрока.</summary>
        public int GetSlot(NetworkConnection connection)
        {
            if (connection == null)
                return PlayerSlots.None;

            for (int i = 0; i < _active.Count; i++)
            {
                PlayerState player = _active[i];
                if (player != null && player.Owner == connection)
                    return player.Slot;
            }

            return PlayerSlots.None;
        }

        /// <summary>Слоты игроков, ещё не выбывших из матча. Переиспользуемый буфер.</summary>
        public IReadOnlyList<int> GetAliveSlots()
        {
            _aliveSlots.Clear();

            for (int i = 0; i < _active.Count; i++)
            {
                PlayerState player = _active[i];
                if (player != null && !player.IsEliminated)
                    _aliveSlots.Add(player.Slot);
            }

            return _aliveSlots;
        }

        /// <summary>Первый свободный слот для нового подключения или None.</summary>
        public int FindFreeSlot()
        {
            for (int i = 0; i < _bySlot.Length; i++)
            {
                if (_bySlot[i] == null)
                    return i;
            }

            return PlayerSlots.None;
        }
    }
}
