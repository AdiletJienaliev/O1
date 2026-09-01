using Warlord.Configs;
using Warlord.Domain.Match;

namespace Warlord.Domain.Economy
{
    /// <summary>
    /// Сколько игрок зарабатывает в секунду прямо сейчас (ГДД §4).
    /// Считается каждый боевой такт заново — источники дохода меняются вместе с флагами.
    /// </summary>
    public readonly struct IncomeProfile
    {
        public readonly float GoldPerSecond;
        public readonly float XpPerSecond;

        public IncomeProfile(float goldPerSecond, float xpPerSecond)
        {
            GoldPerSecond = goldPerSecond;
            XpPerSecond = xpPerSecond;
        }

        /// <param name="holdsCentralFlag">Флаг под контролем игрока прямо сейчас.</param>
        /// <param name="capturedBases">Сколько чужих баз игрок захватил.</param>
        public static IncomeProfile Calculate(
            GameModeConfig mode,
            in MatchSettings settings,
            bool holdsCentralFlag,
            int capturedBases)
        {
            if (mode == null)
                return default;

            float gold = mode.goldPerSecond;

            if (holdsCentralFlag)
                gold += mode.goldPerSecondPerFlag;

            if (capturedBases > 0)
                gold += mode.goldPerSecondPerCapturedBase * capturedBases;

            gold *= settings.IncomeMultiplier;

            float xp = holdsCentralFlag ? mode.xpPerSecondHoldingFlag : 0f;

            return new IncomeProfile(gold, xp);
        }
    }
}
