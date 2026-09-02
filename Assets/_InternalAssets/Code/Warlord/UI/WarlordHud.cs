using FishNet;
using UnityEngine;
using Warlord.Core;
using Warlord.Gameplay.Match;
using Warlord.Presentation;
using Warlord.UI.Screens;

namespace Warlord.UI
{
    /// <summary>
    /// Корень интерфейса. Решает ровно одну задачу — какой экран сейчас на виду.
    /// Правило простое: нет соединения — экран подключения, есть матч — его фаза
    /// и определяет экран. Никакой логики матча здесь нет.
    /// </summary>
    public sealed class WarlordHud : MonoBehaviour
    {
        [Header("Экраны")]
        [SerializeField] private ConnectScreen connectScreen;
        [SerializeField] private LobbyScreen lobbyScreen;
        [SerializeField] private HudScreen hudScreen;
        [SerializeField] private ResultScreen resultScreen;

        private MatchManager _match;

        private void Start() => Apply(Resolve());

        private void Update() => Apply(Resolve());

        private void OnDisable() => InputFocus.Reset();

        /// <summary>
        /// MatchManager — сетевой объект сцены: до подключения он может быть ещё не готов,
        /// поэтому ссылку берём каждый кадр, а инициализацию экранов делаем один раз.
        /// </summary>
        private MatchPhase Resolve()
        {
            MatchManager match = MatchManager.Instance;

            if (match != _match)
            {
                _match = match;

                if (_match != null)
                {
                    hudScreen?.Initialize(_match);
                    resultScreen?.Initialize(_match);
                }
                else
                {
                    hudScreen?.Shutdown();
                }
            }

            if (!InstanceFinder.IsClientStarted)
                return MatchPhase.None;

            return _match != null ? _match.Phase : MatchPhase.Lobby;
        }

        private void Apply(MatchPhase phase)
        {
            // skipLobby: хост поднимается сам, а экраны подключения и лобби не показываются
            // вовсе — до старта матча игрок просто видит сцену без интерфейса.
            bool skipLobby = _match != null && _match.Config != null && _match.Config.SkipLobby;

            bool connect = phase == MatchPhase.None && !skipLobby;
            bool lobby = phase == MatchPhase.Lobby && !skipLobby;
            bool hud = phase == MatchPhase.Countdown || phase == MatchPhase.Running;
            bool result = phase == MatchPhase.Finished;

            Toggle(connectScreen, connect);
            Toggle(lobbyScreen, lobby);
            Toggle(hudScreen, hud);
            Toggle(resultScreen, result);

            // Меню требуют курсор целиком; в бою его отпускает только открытая панель HUD.
            InputFocus.UiCapturesCursor = connect || lobby || result || (hud && hudScreen != null && hudScreen.WantsCursor);
        }

        private static void Toggle(UiScreen screen, bool visible)
        {
            if (screen == null)
                return;

            if (visible)
                screen.Show();
            else
                screen.Hide();
        }
    }
}
