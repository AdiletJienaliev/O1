using System.Collections.Generic;
using Warlord.Configs.Upgrades;
using Warlord.Core;
using Warlord.Gameplay.Match;
using Warlord.Gameplay.Players;

namespace Warlord.Gameplay.Capture
{
    /// <summary>
    /// Что происходит с аванпостом при смене владельца (ГДД §2.5).
    ///
    /// При захвате точке сразу ставится улучшение по умолчанию — «Снабжение». Игрок может
    /// сменить его коротким выбором, пока держит точку, но пустого состояния не возникает
    /// ни на кадр: иначе захват в бою, когда открывать меню некогда, оставлял бы точку
    /// без эффекта до конца матча.
    ///
    /// При потере эффект снимается немедленно, а при перезахвате выбирается заново —
    /// именно поэтому улучшение живёт на точке, а не в состоянии игрока.
    /// </summary>
    public sealed class OutpostRewardHandler : ICaptureRewardHandler
    {
        private readonly IMatchContext _context;
        private readonly List<OutpostUpgradeConfig> _buffer = new(4);

        public OutpostRewardHandler(IMatchContext context) => _context = context;

        public CapturePointKind Kind => CapturePointKind.Outpost;

        public void OnCaptured(CapturePointBehaviour point, int newOwnerSlot)
        {
            OutpostUpgradeSetConfig set = _context.Config.OutpostUpgrades;

            if (point.HasUpgradeSlot && set != null && set.Count > 0)
                point.ServerSetUpgrade(set.DefaultIndex);

            RebuildStack(newOwnerSlot);
        }

        public void OnOwnershipLost(CapturePointBehaviour point, int previousOwnerSlot)
        {
            point.ServerClearUpgrade();

            // Точка сбора, стоявшая здесь, автоматически возвращается на базу (ГДД §2.6):
            // юниты, идущие в уже потерянный аванпост, — худший вид подарка противнику.
            PlayerState owner = _context.Players.Get(previousOwnerSlot);
            if (owner != null)
                owner.ServerClearRallyPointIfAt(point);

            RebuildStack(previousOwnerSlot);
        }

        /// <summary>
        /// Пересобрать суммарный эффект улучшений игрока. Вызывается только на смене владельца:
        /// набор точек меняется единицы раз за матч, а считать стакинг каждый такт незачем.
        /// </summary>
        public void RebuildStack(int slot)
        {
            PlayerState player = _context.Players.Get(slot);

            if (player == null || !PlayerSlots.IsValid(slot))
                return;

            _buffer.Clear();

            IReadOnlyList<CapturePointBehaviour> points = _context.CapturePoints;

            for (int i = 0; i < points.Count; i++)
            {
                CapturePointBehaviour point = points[i];

                if (point.Kind != CapturePointKind.Outpost || point.OwnerSlot != slot)
                    continue;

                OutpostUpgradeConfig upgrade = point.Upgrade;
                if (upgrade != null)
                    _buffer.Add(upgrade);
            }

            player.ServerApplyOutpostUpgrades(_buffer);
        }
    }
}
