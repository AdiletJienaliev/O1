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

        /// <summary>True, если два слота принадлежат разным живым игрокам (FFA: все друг другу враги).</summary>
        public static bool AreEnemies(int a, int b) => IsValid(a) && IsValid(b) && a != b;
    }
}
