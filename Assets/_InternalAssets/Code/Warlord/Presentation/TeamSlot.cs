using UnityEngine;
using Warlord.Configs;
using Warlord.Core;
using Warlord.Gameplay.Heroes;
using Warlord.Gameplay.Match;
using Warlord.Gameplay.Units;

namespace Warlord.Presentation
{
    /// <summary>
    /// Кому принадлежит объект в сцене и какого он цвета. Полководец и юнит хранят слот
    /// каждый по-своему, а презентации — раскраске и полоске здоровья — нужно одно и то же,
    /// поэтому разбор владельца собран в одном месте.
    /// </summary>
    public static class TeamSlot
    {
        /// <summary>Слот владельца объекта или <see cref="PlayerSlots.None"/>, пока сеть его не прислала.</summary>
        public static int Resolve(GameObject target)
        {
            if (target == null)
                return PlayerSlots.None;

            if (target.TryGetComponent(out UnitEntity unit))
                return unit.OwnerSlot;

            if (target.TryGetComponent(out HeroController hero))
                return hero.Slot;

            return PlayerSlots.None;
        }

        /// <summary>Цвет слота из конфига матча. Серый — пока матча нет или слот неизвестен.</summary>
        public static Color ResolveColor(int slot)
        {
            if (!PlayerSlots.IsValid(slot))
                return Color.gray;

            MatchManager match = MatchManager.Instance;
            TeamColorConfig colors = match != null && match.Config != null ? match.Config.TeamColors : null;

            return colors != null ? colors.GetPrimary(slot) : Color.gray;
        }

        /// <summary>Готов ли матч отдать цвета. До этого красить нечем и пробовать незачем.</summary>
        public static bool ColorsReady
        {
            get
            {
                MatchManager match = MatchManager.Instance;
                return match != null && match.Config != null && match.Config.TeamColors != null;
            }
        }
    }
}
