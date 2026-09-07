using System.Collections.Generic;
using Warlord.Core;
using Warlord.Gameplay.Army;
using Warlord.Gameplay.Match;
using Warlord.Gameplay.Players;
using Warlord.Gameplay.Units.Behaviours;

namespace Warlord.Gameplay.Units
{
    /// <summary>
    /// Единый боевой такт для всех юнитов (ГДД §12). Порядок внутри такта фиксирован:
    /// сначала таймеры, потом решение и постановка заявок на урон. Сам урон применяется
    /// позже, в <see cref="Warlord.Gameplay.Combat.CombatResolutionSystem"/>.
    /// </summary>
    public sealed class UnitAiSystem : IServerSystem
    {
        private readonly IMatchContext _context;
        private readonly UnitOrderBehaviourCatalog _catalog;
        private readonly List<UnitEntity> _buffer = new(128);

        public UnitAiSystem(IMatchContext context, UnitOrderBehaviourCatalog catalog)
        {
            _context = context;
            _catalog = catalog;
        }

        public int Order => ServerSystemOrder.UnitAi;

        public void Tick(float deltaTime)
        {
            // Счётчики «сколько бьёт одну цель» обнуляются раз в такт, иначе приоритет
            // «менее облепленная цель» накапливал бы мусор с прошлых тактов.
            _context.Targeting.BeginTick();

            IReadOnlyList<PlayerState> players = _context.Players.Active;

            for (int a = 0; a < players.Count; a++)
            {
                ArmyController army = players[a] != null ? players[a].Army : null;
                if (army == null || army.AliveCount == 0)
                    continue;

                IUnitOrderBehaviour behaviour = _catalog.Get(army.Order.Type);
                if (behaviour == null)
                    continue;

                // Щит держится только под приказом «Защита»: опускаем его здесь, один раз
                // на смене приказа, а не в каждом из остальных поведений — забыть про это
                // в новом поведении было бы слишком легко, и армия ушла бы в атаку с блоком.
                bool defending = army.Order.Type == ArmyOrderType.Defend;

                _buffer.Clear();
                _buffer.AddRange(army.Units);

                for (int i = 0; i < _buffer.Count; i++)
                {
                    UnitEntity unit = _buffer[i];
                    if (unit == null || !unit.IsAlive)
                        continue;

                    unit.ServerTickTimers(deltaTime);

                    if (!defending)
                        unit.ServerSetShield(false);

                    behaviour.Tick(unit, army, _context, deltaTime);
                }
            }

            _buffer.Clear();
        }
    }
}
