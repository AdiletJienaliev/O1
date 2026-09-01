using System.Collections.Generic;
using Warlord.Core;

namespace Warlord.Gameplay.Units.Behaviours
{
    /// <summary>
    /// Реестр поведений по типу приказа. Точка расширения: добавили приказ в enum,
    /// написали класс, зарегистрировали здесь — остальной код не меняется.
    /// </summary>
    public sealed class UnitOrderBehaviourCatalog
    {
        private readonly Dictionary<ArmyOrderType, IUnitOrderBehaviour> _behaviours = new();

        /// <summary>Каталог с поведениями из ГДД §6.</summary>
        public static UnitOrderBehaviourCatalog CreateDefault()
        {
            UnitOrderBehaviourCatalog catalog = new();
            catalog.Register(new HoldGroundBehaviour());
            catalog.Register(new FollowLeaderBehaviour());
            catalog.Register(new AttackMoveBehaviour());
            return catalog;
        }

        public void Register(IUnitOrderBehaviour behaviour)
        {
            if (behaviour != null)
                _behaviours[behaviour.OrderType] = behaviour;
        }

        public IUnitOrderBehaviour Get(ArmyOrderType orderType)
        {
            return _behaviours.TryGetValue(orderType, out IUnitOrderBehaviour behaviour) ? behaviour : null;
        }
    }
}
