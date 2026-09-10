namespace Warlord.Core
{
    /// <summary>
    /// Слот игрока в матче — сквозной идентификатор владения (юниты, флаги, база, цвет).
    /// Хранится как int, чтобы дёшево синкаться и не тащить обёртку в SyncVar.
    /// </summary>
    public static class PlayerSlots
    {
        /// <summary>Ничей / нейтральный владелец.</summary>
        public const int None = -1;

        /// <summary>Максимум слотов, поддерживаемых сетевым слоем (ГДД §2: 2–4 игрока).</summary>
        public const int MaxSupported = 4;

        public static bool IsValid(int slot) => slot >= 0 && slot < MaxSupported;

        // Проверки «враг или свой» здесь намеренно нет: с появлением команд ответ зависит
        // от раскладки матча, а не от одних номеров слотов. Спрашивать нужно
        // у <see cref="TeamLayout"/> — она же покрывает FFA как частный случай.
    }
}
