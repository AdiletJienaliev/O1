using System;

namespace Warlord.Networking
{
    /// <summary>
    /// Сеанс на игровой платформе: комната, приглашения, адрес хоста. Игровой код знает только
    /// этот интерфейс, а реализация (Steam) живёт в отдельной сборке со своими ограничениями
    /// по платформам сборки. Поэтому <see cref="NetworkBootstrap"/> одинаково работает и без
    /// Steam вообще, и с ним — меняется только источник адреса.
    /// </summary>
    /// <remarks>
    /// Адрес здесь — строка, потому что для Tugboat это «127.0.0.1», а для Steam — SteamID64.
    /// Транспорт сам понимает, что ему передали: сеть — единственный слой, знающий про оба.
    /// </remarks>
    public interface IPlatformSession
    {
        /// <summary>Платформа поднялась и ей можно пользоваться.</summary>
        bool IsReady { get; }

        /// <summary>Текст для экрана подключения: почему ждём или почему не вышло.</summary>
        string Status { get; }

        /// <summary>Мы состоим в комнате платформы.</summary>
        bool InLobby { get; }

        /// <summary>Комната наша: только владелец публикует адрес и зовёт друзей.</summary>
        bool IsLobbyOwner { get; }

        /// <summary>Адрес, по которому к нам подключаются другие. Для Steam — наш SteamID64.</summary>
        string LocalAddress { get; }

        /// <summary>Сколько человек в комнате платформы (не в матче).</summary>
        int MemberCount { get; }

        /// <summary>Можно ли прямо сейчас позвать друга: комната есть и оверлей доступен.</summary>
        bool CanInvite { get; }

        /// <summary>
        /// Адрес хоста получен — друг принял приглашение или игра запущена по ссылке.
        /// Подписчик поднимает клиента; сам сеанс про FishNet ничего не знает.
        /// </summary>
        event Action<string> HostAddressReceived;

        /// <summary>Состав комнаты или статус изменились — интерфейсу пора перерисоваться.</summary>
        event Action Changed;

        /// <summary>Создать комнату и объявить себя хостом. Вызывает хост сразу после старта сервера.</summary>
        void HostLobby(int maxMembers);

        /// <summary>Выйти из комнаты. Безопасно вызывать, даже если её нет.</summary>
        void LeaveLobby();

        /// <summary>Открыть оверлей платформы со списком друзей и кнопкой приглашения.</summary>
        void OpenInviteOverlay();

        /// <summary>Ник участника комнаты для списка в лобби. Вне диапазона — пустая строка.</summary>
        string GetMemberName(int index);
    }
}
