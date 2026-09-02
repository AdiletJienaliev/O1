using UnityEngine;
using Warlord.Gameplay.Match;
using Warlord.Gameplay.Players;

namespace Warlord.UI
{
    /// <summary>
    /// Базовый виджет HUD. Виджеты не имеют собственного Update: их обновляет экран,
    /// поэтому за кадр все части HUD видят одно и то же состояние игрока.
    /// </summary>
    public abstract class HudWidget : MonoBehaviour
    {
        /// <summary>Матч. Проставляется один раз, когда <see cref="MatchManager"/> появился в сцене.</summary>
        protected MatchManager Match { get; private set; }

        /// <summary>Готов ли виджет к обновлению: без матча большинство данных не существует.</summary>
        protected bool HasMatch => Match != null;

        public void Initialize(MatchManager match)
        {
            Match = match;
            OnInitialized();
        }

        public void Shutdown()
        {
            OnShutdown();
            Match = null;
        }

        /// <summary>Разовая настройка: подписки на события матча, сборка списков из конфигов.</summary>
        protected virtual void OnInitialized() { }

        protected virtual void OnShutdown() { }

        /// <summary>Покадровое обновление. player может быть null — например, пока идёт лобби.</summary>
        public abstract void Refresh(PlayerState player);
    }
}
