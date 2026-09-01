using System.Collections.Generic;
using Warlord.Core;
using Warlord.Gameplay.Match;
using Warlord.Gameplay.Players;

namespace Warlord.Gameplay.Heroes
{
    /// <summary>
    /// Регенерация вне боя, отсчёт респавна и кулдаун удара — всё в едином боевом такте (ГДД §12).
    /// Ничего из этого не делается в Update, чтобы результат не зависел от частоты кадров сервера.
    /// </summary>
    public sealed class HeroLifecycleSystem : IServerSystem
    {
        private readonly IMatchContext _context;

        public HeroLifecycleSystem(IMatchContext context) => _context = context;

        public int Order => ServerSystemOrder.Regeneration;

        public void Tick(float deltaTime)
        {
            IReadOnlyList<PlayerState> players = _context.Players.Active;

            for (int i = 0; i < players.Count; i++)
            {
                HeroController hero = players[i] != null ? players[i].Hero : null;
                if (hero == null)
                    continue;

                // Полководец сам тикает свой боевой компонент — системе не нужно знать его состав.
                hero.ServerTick(deltaTime);
            }
        }
    }
}
