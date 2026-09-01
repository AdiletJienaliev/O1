using System.Collections.Generic;
using Warlord.Configs;
using Warlord.Core;
using Warlord.Domain.Economy;
using Warlord.Gameplay.Match;
using Warlord.Gameplay.Players;

namespace Warlord.Gameplay.Economy
{
    /// <summary>
    /// Доход, XP и время удержания флага (ГДД §4). Одно место, где начисляется всё,
    /// что капает со временем: доход зависит от того, кто держит центр, а держит его
    /// ровно один игрок, поэтому счёт и деньги считаются в одном проходе.
    /// </summary>
    public sealed class EconomySystem : IServerSystem
    {
        private readonly IMatchContext _context;

        public EconomySystem(IMatchContext context) => _context = context;

        public int Order => ServerSystemOrder.Economy;

        public void Tick(float deltaTime)
        {
            if (_context.Phase != MatchPhase.Running)
                return;

            GameModeConfig mode = _context.Config.GameMode;
            int flagOwner = _context.CentralFlagOwner;
            IReadOnlyList<PlayerState> players = _context.Players.Active;

            for (int i = 0; i < players.Count; i++)
            {
                PlayerState player = players[i];
                if (player == null || player.IsEliminated)
                    continue;

                bool holdsFlag = player.Slot == flagOwner;

                IncomeProfile income = IncomeProfile.Calculate(
                    mode,
                    _context.Settings,
                    holdsFlag,
                    player.CapturedBases);

                player.ServerTickEconomy(deltaTime, in income);

                // Время удержания центра — основной критерий победы (ГДД §2).
                if (holdsFlag)
                    _context.Scores.AddFlagHoldTime(player.Slot, deltaTime);
            }
        }
    }
}
