using System;

namespace Warlord.Core
{
    /// <summary>
    /// Кто с кем в команде (ГДД §2: FFA как частный случай). Упакована в четыре байта —
    /// по одному на слот, — поэтому спокойно едет по сети внутри настроек комнаты
    /// и копируется в системы значением, без ссылок и аллокаций.
    ///
    /// Ноль в байте означает «команда не назначена», и такой слот воюет сам за себя.
    /// Именно поэтому <c>default</c> — это честный FFA: система, до которой раскладку
    /// забыли протащить, ведёт себя как раньше, а не считает всех союзниками.
    /// </summary>
    [Serializable]
    public readonly struct TeamLayout : IEquatable<TeamLayout>
    {
        /// <summary>По байту на слот: 0 — без команды, иначе id команды + 1.</summary>
        private readonly uint _packed;

        /// <summary>Смещение для слотов без команды: их «личные» id не должны пересекаться с назначенными.</summary>
        private const int SoloTeamBase = 64;

        public TeamLayout(uint packed) => _packed = packed;

        /// <summary>Каждый сам за себя. То же, что <c>default</c>.</summary>
        public static TeamLayout Ffa => default;

        /// <summary>Упакованное значение. Только для переноса по сети и в настройках комнаты.</summary>
        public uint Packed => _packed;

        /// <summary>Копия с назначенной командой слота. Отрицательный id снимает команду.</summary>
        public TeamLayout With(int slot, int teamId)
        {
            if (!PlayerSlots.IsValid(slot))
                return this;

            uint value = teamId < 0 ? 0u : (uint)Math.Min(teamId + 1, byte.MaxValue);
            int shift = slot * 8;

            return new TeamLayout((_packed & ~(0xFFu << shift)) | (value << shift));
        }

        /// <summary>Назначена ли слоту команда явно. Нужно лобби, чтобы отличать «без команды» от нулевой.</summary>
        public bool HasTeam(int slot) => RawTeam(slot) != 0;

        /// <summary>Явно назначенная команда слота или -1.</summary>
        public int AssignedTeam(int slot)
        {
            int raw = RawTeam(slot);
            return raw == 0 ? -1 : raw - 1;
        }

        /// <summary>
        /// Команда слота для сравнений. У слота без назначения она своя собственная,
        /// поэтому обычный FFA-матч проходит через тот же код, что и командный.
        /// </summary>
        public int TeamOf(int slot)
        {
            if (!PlayerSlots.IsValid(slot))
                return PlayerSlots.None;

            int raw = RawTeam(slot);
            return raw == 0 ? SoloTeamBase + slot : raw - 1;
        }

        /// <summary>Разные живые слоты из разных команд. Единственная проверка «можно бить» во всём проекте.</summary>
        public bool AreEnemies(int a, int b)
        {
            return PlayerSlots.IsValid(a) && PlayerSlots.IsValid(b) && a != b && TeamOf(a) != TeamOf(b);
        }

        /// <summary>Разные слоты одной команды. Сам себе союзником не считается — см. <see cref="SameSide"/>.</summary>
        public bool AreAllies(int a, int b)
        {
            return PlayerSlots.IsValid(a) && PlayerSlots.IsValid(b) && a != b && TeamOf(a) == TeamOf(b);
        }

        /// <summary>Один и тот же слот или союзники. Так спрашивают лечение, миникарта и подсветка своих.</summary>
        public bool SameSide(int a, int b)
        {
            return PlayerSlots.IsValid(a) && PlayerSlots.IsValid(b) && (a == b || TeamOf(a) == TeamOf(b));
        }

        /// <summary>Есть ли в раскладке хоть одна команда из двух и более слотов.</summary>
        public bool HasAnyTeam
        {
            get
            {
                for (int a = 0; a < PlayerSlots.MaxSupported; a++)
                {
                    for (int b = a + 1; b < PlayerSlots.MaxSupported; b++)
                    {
                        if (RawTeam(a) != 0 && RawTeam(a) == RawTeam(b))
                            return true;
                    }
                }

                return false;
            }
        }

        private int RawTeam(int slot)
        {
            if (!PlayerSlots.IsValid(slot))
                return 0;

            return (int)((_packed >> (slot * 8)) & 0xFFu);
        }

        public bool Equals(TeamLayout other) => _packed == other._packed;
        public override bool Equals(object obj) => obj is TeamLayout other && Equals(other);
        public override int GetHashCode() => (int)_packed;

        public static bool operator ==(TeamLayout left, TeamLayout right) => left.Equals(right);
        public static bool operator !=(TeamLayout left, TeamLayout right) => !left.Equals(right);
    }
}
