namespace Warlord.Configs
{
    /// <summary>
    /// Через что идёт соединение. Выбор живёт в <see cref="GameFlowConfig"/> рядом с остальными
    /// отладочными тумблерами: переключать его приходится по нескольку раз за сессию, а Steam
    /// в редакторе доступен не всегда.
    /// </summary>
    public enum NetworkBackend : byte
    {
        /// <summary>Прямое подключение по адресу и порту (Tugboat). Режим по умолчанию и для отладки.</summary>
        Localhost = 0,

        /// <summary>Steam P2P: лобби, приглашения друзей, обход NAT через реле Valve.</summary>
        Steam = 1,

        /// <summary>
        /// Steam, если он поднялся; иначе прямое подключение. Удобно, когда проект открывают
        /// и с запущенным Steam, и без него, а править конфиг под каждый запуск не хочется.
        /// </summary>
        Auto = 2
    }

    /// <summary>
    /// Кому видно созданное лобби. Своё перечисление, а не Steamworks.ELobbyType: конфиги лежат
    /// ниже сети по слоям и про сторонние SDK знать не должны — перевод делает уже слой Steam.
    /// </summary>
    public enum SteamLobbyVisibility : byte
    {
        /// <summary>Только по приглашению.</summary>
        Private = 0,

        /// <summary>Друзьям видно в их списке игр, остальным — только по приглашению.</summary>
        FriendsOnly = 1,

        /// <summary>Видно всем в поиске комнат.</summary>
        Public = 2
    }
}
