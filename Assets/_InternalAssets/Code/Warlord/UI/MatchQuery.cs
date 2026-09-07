using UnityEngine;
using Warlord.Configs;
using Warlord.Gameplay.Heroes;
using Warlord.Gameplay.Players;
using Warlord.Gameplay.World;

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

            PlayerBase playerBase = PlayerBase.Get(slot);
            if (playerBase == null)
                return false;

            Vector3 delta = hero.Position - playerBase.Center;
            delta.y = 0f;

            float radius = playerBase.BuyZoneRadius;
            return delta.sqrMagnitude <= radius * radius;
        }
    }
}
