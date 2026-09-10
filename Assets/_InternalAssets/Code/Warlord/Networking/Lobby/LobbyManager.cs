using System;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using UnityEngine;
using Warlord.Configs.Bots;
using Warlord.Core;
using Warlord.Domain.Match;
using Warlord.Gameplay.Match;

namespace Warlord.Networking.Lobby
{
    /// <summary>
    /// Лобби комнаты (ГДД §14): список слотов, выбор цвета, готовность и настройки хоста.
    /// Настройки едут одной структурой и перекрывают дефолты режима при старте матча.
    ///
    /// Здесь же собирается стартовый состав: свободный слот хост может занять ботом,
    /// выбрать ему характер и сложность, а любому слоту назначить команду. Благодаря
    /// этому матч запускается и с одним живым человеком в комнате — против ботов
    /// или с ботом в союзниках.
    /// </summary>
    public sealed class LobbyManager : NetworkBehaviour
    {
        private readonly SyncList<LobbySlotInfo> _slots = new();
        private readonly SyncVar<MatchSettings> _settings = new();

        [SerializeField] private MatchManager matchManager;

        public SyncList<LobbySlotInfo> Slots => _slots;
        public MatchSettings Settings => _settings.Value;

        /// <summary>Состав лобби изменился — UI перерисовывает список.</summary>
        public event Action LobbyChanged;

        private void Awake() => matchManager ??= MatchManager.Instance;

        /// <summary>Набор ботов из конфига или null, если ботов в этой сборке контента нет.</summary>
        public BotSetConfig BotSet => matchManager != null && matchManager.Config != null ? matchManager.Config.Bots : null;

        /// <summary>Можно ли вообще добавлять ботов. UI по этому прячет кнопку, а не рисует мёртвую.</summary>
        public bool BotsAvailable => BotSet != null && BotSet.Count > 0;

        public override void OnStartServer()
        {
            base.OnStartServer();

            matchManager ??= MatchManager.Instance;
            _settings.Value = MatchSettings.FromConfig(matchManager.Config.GameMode);

            int count = matchManager.Config.GameMode.maxPlayers;
            _slots.Clear();
            for (int i = 0; i < count; i++)
                _slots.Add(LobbySlotInfo.Empty(i));

            ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            _slots.OnChange += OnSlotsChanged;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            _slots.OnChange -= OnSlotsChanged;
        }

        private void OnSlotsChanged(SyncListOperation op, int index, LobbySlotInfo previous, LobbySlotInfo next, bool asServer)
        {
            if (!asServer)
                LobbyChanged?.Invoke();
        }

        private void OnRemoteConnectionState(NetworkConnection connection, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState == RemoteConnectionState.Started)
                TryOccupySlot(connection);
            else if (args.ConnectionState == RemoteConnectionState.Stopped)
                ReleaseSlot(connection);
        }

        /// <summary>
        /// Живой игрок садится в первый по-настоящему пустой слот. Слот с ботом не занимается:
        /// хост поставил его сознательно, и молча выкидывать бота при каждом подключении
        /// значило бы ломать заранее собранный состав.
        /// </summary>
        private void TryOccupySlot(NetworkConnection connection)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                LobbySlotInfo info = _slots[i];
                if (info.Occupied)
                    continue;

                info.Kind = (byte)SlotKind.Human;
                info.ClientId = connection.ClientId;
                info.Ready = false;
                _slots[i] = info;
                return;
            }

            Debug.LogWarning($"LobbyManager: свободных слотов нет для клиента {connection.ClientId}. " +
                             "Уберите одного из ботов, чтобы освободить место.");
        }

        private void ReleaseSlot(NetworkConnection connection)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                LobbySlotInfo info = _slots[i];
                if (!info.IsHuman || info.ClientId != connection.ClientId)
                    continue;

                _slots[i] = LobbySlotInfo.Empty(i);
                return;
            }
        }

        #region Команды игрока

        [ServerRpc(RequireOwnership = false)]
        public void CmdSetReady(bool ready, NetworkConnection sender = null)
        {
            int index = FindSlotIndex(sender);
            if (index < 0)
                return;

            LobbySlotInfo info = _slots[index];
            info.Ready = ready;
            _slots[index] = info;
        }

        [ServerRpc(RequireOwnership = false)]
        public void CmdSelectColor(byte colorId, NetworkConnection sender = null)
        {
            int index = FindSlotIndex(sender);
            if (index < 0)
                return;

            if (!IsColorFree(colorId, index))
                return;

            LobbySlotInfo info = _slots[index];
            info.ColorId = colorId;
            _slots[index] = info;
        }

        /// <summary>Цвет привязан к слоту, занятый цвет выбрать нельзя (ГДД §13).</summary>
        private bool IsColorFree(byte colorId, int exceptIndex)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (i != exceptIndex && _slots[i].Occupied && _slots[i].ColorId == colorId)
                    return false;
            }

            return true;
        }

        #endregion

        #region Команды хоста: боты и команды

        /// <summary>
        /// Посадить бота в свободный слот. Все настройки бота — дело хоста: комната
        /// принадлежит ему, а бот не может сам выбрать себе характер.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void CmdAddBot(byte slot, byte difficulty, NetworkConnection sender = null)
        {
            if (!IsHost(sender) || !IsValidSlot(slot))
                return;

            BotSetConfig set = BotSet;
            if (set == null || set.Count == 0)
            {
                Debug.LogWarning("LobbyManager: у GameConfig не задан набор ботов — добавлять нечего. " +
                                 "Выполните пункт меню Warlord/Настройка/16.");
                return;
            }

            LobbySlotInfo info = _slots[slot];
            if (info.Occupied)
                return;

            int personality = set.DefaultPersonality;

            info.Kind = (byte)SlotKind.Bot;
            info.ClientId = -1;
            info.BotPersonality = (byte)personality;
            info.BotDifficulty = (byte)Mathf.Clamp(difficulty, 0, (int)Core.BotDifficulty.Brutal);
            info.BotNameIndex = NextNameIndex(personality);

            // Бот готов всегда: ждать от него нажатия «готов» некому.
            info.Ready = true;

            _slots[slot] = info;
        }

        [ServerRpc(RequireOwnership = false)]
        public void CmdRemoveBot(byte slot, NetworkConnection sender = null)
        {
            if (!IsHost(sender) || !IsValidSlot(slot) || !_slots[slot].IsBot)
                return;

            _slots[slot] = LobbySlotInfo.Empty(slot);
        }

        /// <summary>Следующая сложность по кругу. Кнопка в лобби ходит по списку этим вызовом.</summary>
        [ServerRpc(RequireOwnership = false)]
        public void CmdCycleBotDifficulty(byte slot, NetworkConnection sender = null)
        {
            if (!IsHost(sender) || !IsValidSlot(slot) || !_slots[slot].IsBot)
                return;

            LobbySlotInfo info = _slots[slot];
            int next = (info.BotDifficulty + 1) % ((int)Core.BotDifficulty.Brutal + 1);
            info.BotDifficulty = (byte)next;
            _slots[slot] = info;
        }

        /// <summary>Следующий характер по кругу. Имя меняется вместе с ним.</summary>
        [ServerRpc(RequireOwnership = false)]
        public void CmdCycleBotPersonality(byte slot, NetworkConnection sender = null)
        {
            BotSetConfig set = BotSet;

            if (!IsHost(sender) || !IsValidSlot(slot) || !_slots[slot].IsBot || set == null || set.Count == 0)
                return;

            LobbySlotInfo info = _slots[slot];
            int next = set.NextPersonality(info.BotPersonality);

            info.BotPersonality = (byte)next;
            info.BotNameIndex = NextNameIndex(next);
            _slots[slot] = info;
        }

        /// <summary>
        /// Команда слота по кругу: сам за себя, «А», «Б» и так далее по числу слотов.
        /// Пустая команда — это FFA, и она же значение по умолчанию.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void CmdCycleTeam(byte slot, NetworkConnection sender = null)
        {
            if (!IsHost(sender) || !IsValidSlot(slot) || !_slots[slot].Occupied)
                return;

            LobbySlotInfo info = _slots[slot];

            int teams = Mathf.Max(2, _slots.Count / 2);
            int next = info.TeamId + 1;

            info.TeamId = next >= teams ? (sbyte)-1 : (sbyte)next;
            _slots[slot] = info;
        }

        /// <summary>Номер имени, которого ещё нет среди ботов этого характера.</summary>
        private byte NextNameIndex(int personality)
        {
            int used = 0;

            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].IsBot && _slots[i].BotPersonality == personality)
                    used++;
            }

            return (byte)Mathf.Min(used, byte.MaxValue);
        }

        private bool IsHost(NetworkConnection sender) => sender != null && sender.IsHost;

        private bool IsValidSlot(int slot) => slot >= 0 && slot < _slots.Count;

        #endregion

        #region Настройки и старт

        /// <summary>Хост меняет настройки комнаты. Валидация — Sanitized при старте матча.</summary>
        [ServerRpc(RequireOwnership = false)]
        public void CmdUpdateSettings(MatchSettings settings, NetworkConnection sender = null)
        {
            if (!IsHost(sender))
                return;

            _settings.Value = settings.Sanitized(matchManager.Config.GameMode);
        }

        /// <summary>Хост запускает матч. Только хост — выход хоста завершает матч (ГДД §12, §16).</summary>
        [ServerRpc(RequireOwnership = false)]
        public void CmdStartMatch(NetworkConnection sender = null)
        {
            if (!IsHost(sender))
                return;

            if (!AllOccupiedReady())
                return;

            matchManager.ServerStartMatch(_settings.Value, BuildRoster());
        }

        /// <summary>
        /// Стартовый состав для матча. Собирается из тех же строк, что показаны в комнате, —
        /// второго источника правды о том, кто где сидит, в проекте нет.
        /// </summary>
        public MatchRoster BuildRoster()
        {
            MatchRoster roster = new();

            for (int i = 0; i < _slots.Count && i < roster.SlotCount; i++)
            {
                LobbySlotInfo info = _slots[i];

                if (!info.Occupied)
                {
                    roster.Clear(i);
                    continue;
                }

                roster.Set(i, new MatchSlotSetup
                {
                    Kind = info.SlotKind,
                    ClientId = info.IsHuman ? info.ClientId : -1,
                    ColorId = info.ColorId,
                    TeamId = info.TeamId,
                    Personality = info.BotPersonality,
                    Difficulty = (Core.BotDifficulty)info.BotDifficulty,
                    NameIndex = info.BotNameIndex
                });
            }

            return roster;
        }

        /// <summary>Готовность считается только по живым: бот всегда готов.</summary>
        private bool AllOccupiedReady()
        {
            int occupied = 0;

            for (int i = 0; i < _slots.Count; i++)
            {
                if (!_slots[i].Occupied)
                    continue;

                occupied++;

                if (_slots[i].IsHuman && !_slots[i].Ready)
                    return false;
            }

            return occupied >= 1;
        }

        private int FindSlotIndex(NetworkConnection connection)
        {
            if (connection == null)
                return -1;

            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].IsHuman && _slots[i].ClientId == connection.ClientId)
                    return i;
            }

            return -1;
        }

        #endregion
    }
}
