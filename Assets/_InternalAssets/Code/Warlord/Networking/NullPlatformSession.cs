using System;

namespace Warlord.Networking
{
    /// <summary>
    /// Заглушка на случай, когда в сцене нет ни одной платформы. Существует, чтобы вызывающий
    /// код не обрастал проверками на null: «нет Steam» — это не ошибка, а обычный режим игры
    /// по адресу и порту, и вести он себя должен как выключенная платформа, а не как пустая ссылка.
    /// </summary>
    public sealed class NullPlatformSession : IPlatformSession
    {
        public static readonly NullPlatformSession Instance = new();

        private NullPlatformSession() { }

        public bool IsReady => false;
        public string Status => string.Empty;
        public bool InLobby => false;
        public bool IsLobbyOwner => false;
        public string LocalAddress => string.Empty;
        public int MemberCount => 0;
        public bool CanInvite => false;

        // Событий заглушка не шлёт никогда, поэтому подписка и отписка — пустышки.
        public event Action<string> HostAddressReceived { add { } remove { } }
        public event Action Changed { add { } remove { } }

        public void HostLobby(int maxMembers) { }
        public void LeaveLobby() { }
        public void OpenInviteOverlay() { }
        public string GetMemberName(int index) => string.Empty;
    }
}
