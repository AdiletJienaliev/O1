using System.Collections.Generic;
using Warlord.Core;
using Warlord.Gameplay.Match;
using Warlord.Gameplay.Players;

namespace Warlord.Gameplay.Economy
{
    /// <summary>Продвижение очередей постройки всех игроков (ГДД §5.1).</summary>
    public sealed class SpawnQueueSystem : IServerSystem
    {
        private readonly IMatchContext _context;

        public SpawnQueueSystem(IMatchContext context) => _context = context;

        public int Order => ServerSystemOrder.SpawnQueue;

        public void Tick(float deltaTime)
        {
            if (_context.Phase != MatchPhase.Running)
                return;

            IReadOnlyList<PlayerState> players = _context.Players.Active;

            for (int i = 0; i < players.Count; i++)
            {
                PlayerState player = players[i];
                if (player != null)
                    player.ServerTickBuildQueue(deltaTime);
            }
        }
    }
}
