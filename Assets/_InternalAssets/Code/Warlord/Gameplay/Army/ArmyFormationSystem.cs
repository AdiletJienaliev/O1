using System.Collections.Generic;
using Warlord.Core;
using Warlord.Gameplay.Match;
using Warlord.Gameplay.Players;

namespace Warlord.Gameplay.Army
{
    /// <summary>
    /// Пересборка построений всех армий (ГДД §7). Стоит в такте до ИИ юнитов:
    /// сначала слоты, потом решения — иначе юниты бежали бы к прошлым позициям.
    /// </summary>
    public sealed class ArmyFormationSystem : IServerSystem
    {
        private readonly IMatchContext _context;

        public ArmyFormationSystem(IMatchContext context) => _context = context;

        public int Order => ServerSystemOrder.Formation;

        public void Tick(float deltaTime)
        {
            IReadOnlyList<PlayerState> players = _context.Players.Active;

            for (int i = 0; i < players.Count; i++)
            {
                PlayerState player = players[i];
                if (player == null || player.IsEliminated || player.Army == null)
                    continue;

                player.Army.TickFormation(deltaTime);
            }
        }
    }
}
