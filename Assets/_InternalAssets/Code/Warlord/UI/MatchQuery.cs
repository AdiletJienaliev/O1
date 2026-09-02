using UnityEngine;
using Warlord.Configs;
using Warlord.Gameplay.Heroes;
using Warlord.Gameplay.Players;

namespace Warlord.UI
{
    /// <summary>
    /// Клиентские предпросмотры серверных правил. Сервер остаётся авторитетным —
    /// проверка нужна лишь для того, чтобы кнопка гасла до отправки команды,
    /// а не после отказа. Формула намеренно повторяет серверную (ГДД §5.1).
    /// </summary>
    public static class MatchQuery
    {
        /// <summary>Стоит ли полководец в зоне покупки своей базы.</summary>
        public static bool IsHeroInsideBuyZone(GameConfig config, HeroController hero, int slot)
        {
            if (config == null || hero == null || !hero.IsAlive)
                return false;

            MapConfig map = config.Map;
            HeroConfig heroConfig = config.Hero;

            if (map == null || heroConfig == null)
                return false;

            PlayerBaseAnchor anchor = PlayerBaseAnchor.FromMap(map, slot);
            float radius = heroConfig.buyZoneRadius;

            Vector3 delta = hero.Position - anchor.Center;
            delta.y = 0f;

            return delta.sqrMagnitude <= radius * radius;
        }
    }
}
