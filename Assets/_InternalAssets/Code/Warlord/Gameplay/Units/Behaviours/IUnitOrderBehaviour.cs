using Warlord.Core;
using Warlord.Gameplay.Army;
using Warlord.Gameplay.Match;

namespace Warlord.Gameplay.Units.Behaviours
{
    /// <summary>
    /// Поведение юнита под конкретным приказом (ГДД §6).
    /// Поведения не хранят состояние: всё, что помнит юнит, лежит в самом юните.
    /// Благодаря этому один экземпляр обслуживает всю армию, а новый приказ —
    /// это новый класс плюс регистрация в каталоге, без правок существующего кода.
    /// </summary>
    public interface IUnitOrderBehaviour
    {
        ArmyOrderType OrderType { get; }

        void Tick(UnitEntity unit, ArmyController army, IMatchContext context, float deltaTime);
    }
}
