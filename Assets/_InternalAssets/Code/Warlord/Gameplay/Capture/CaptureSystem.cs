using System.Collections.Generic;
using Warlord.Core;
using Warlord.Gameplay.Match;

namespace Warlord.Gameplay.Capture
{
    /// <summary>
    /// Тик всех точек захвата и раздача наград (ГДД §9, §10).
    /// Стоит первым после часов матча: доход и счёт за текущий такт должны считаться
    /// уже с учётом того, кто владеет центром.
    /// </summary>
    public sealed class CaptureSystem : IServerSystem
    {
        private readonly List<CapturePointBehaviour> _points = new(8);
        private readonly Dictionary<CapturePointKind, ICaptureRewardHandler> _handlers = new();

        public int Order => ServerSystemOrder.Capture;

        /// <summary>Слот владельца центрального флага или <see cref="PlayerSlots.None"/>.</summary>
        public int CentralFlagOwner { get; private set; } = PlayerSlots.None;

        public IReadOnlyList<CapturePointBehaviour> Points => _points;

        public void RegisterHandler(ICaptureRewardHandler handler)
        {
            if (handler != null)
                _handlers[handler.Kind] = handler;
        }

        public void Register(CapturePointBehaviour point)
        {
            if (point == null || _points.Contains(point))
                return;

            _points.Add(point);
            point.Captured += OnCaptured;
            point.OwnershipLost += OnOwnershipLost;

            if (point.Kind == CapturePointKind.CentralFlag)
                CentralFlagOwner = point.OwnerSlot;
        }

        public void Clear()
        {
            for (int i = 0; i < _points.Count; i++)
            {
                _points[i].Captured -= OnCaptured;
                _points[i].OwnershipLost -= OnOwnershipLost;
            }

            _points.Clear();
            CentralFlagOwner = PlayerSlots.None;
        }

        public void Tick(float deltaTime)
        {
            for (int i = 0; i < _points.Count; i++)
                _points[i].ServerTick(deltaTime);
        }

        /// <summary>Точки, принадлежавшие выбывшему игроку, обнуляются (ГДД §10.5: крепость становится руиной).</summary>
        public void OnPlayerEliminated(int slot)
        {
            for (int i = 0; i < _points.Count; i++)
            {
                CapturePointBehaviour point = _points[i];
                if (point.OwnerSlot == slot && point.Kind == CapturePointKind.CentralFlag)
                    point.ServerForceNeutral();
            }
        }

        private void OnCaptured(CapturePointBehaviour point, int newOwnerSlot)
        {
            if (point.Kind == CapturePointKind.CentralFlag)
                CentralFlagOwner = newOwnerSlot;

            if (_handlers.TryGetValue(point.Kind, out ICaptureRewardHandler handler))
                handler.OnCaptured(point, newOwnerSlot);
        }

        private void OnOwnershipLost(CapturePointBehaviour point, int previousOwnerSlot)
        {
            if (point.Kind == CapturePointKind.CentralFlag)
                CentralFlagOwner = PlayerSlots.None;

            if (_handlers.TryGetValue(point.Kind, out ICaptureRewardHandler handler))
                handler.OnOwnershipLost(point, previousOwnerSlot);
        }
    }
}
