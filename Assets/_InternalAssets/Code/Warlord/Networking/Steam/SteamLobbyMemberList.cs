using System.Collections.Generic;
using HeathenEngineering.SteamworksIntegration;
using HeathenEngineering.SteamworksIntegration.UI;
using UnityEngine;

namespace Warlord.Networking.Steam
{
    /// <summary>
    /// Список участников комнаты Steam: аватар, ник и статус каждого.
    /// </summary>
    /// <remarks>
    /// Дополняет слоты матча, а не заменяет их: в слотах сидят те, кто уже подключился к
    /// серверу, а здесь видно и приглашённых, которые ещё грузятся. Карточку рисует готовый
    /// префаб Heathen — нам достаточно, что на нём есть <see cref="IUserProfile"/>.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class SteamLobbyMemberList : MonoBehaviour
    {
        [Header("Сеанс")]
        [SerializeField] private SteamSession session;

        [Header("Разметка")]
        [Tooltip("Куда складывать карточки. Обычно объект с VerticalLayoutGroup.")]
        [SerializeField] private RectTransform content;

        [Tooltip("Префаб карточки участника: подойдёт Heathen «Friend Profile».")]
        [SerializeField] private GameObject memberTemplate;

        [Tooltip("Показывать в списке себя.")]
        [SerializeField] private bool includeSelf = true;

        private readonly List<UserData> _shown = new();
        private readonly List<GameObject> _views = new();
        private readonly List<UserData> _current = new();

        private void Awake() => session ??= FindAnyObjectByType<SteamSession>();

        private void OnEnable()
        {
            if (session == null)
            {
                Debug.LogError("SteamLobbyMemberList: не найден SteamSession", this);
                return;
            }

            session.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (session != null)
                session.Changed -= Refresh;
        }

        private void Refresh()
        {
            if (content == null || memberTemplate == null || session == null)
                return;

            ReadMembers();

            // Пересобираем только когда состав действительно изменился: Changed приходит и на
            // каждое обновление метаданных, а перестройка списка сбрасывает загруженные аватары.
            if (SameAsShown())
                return;

            Clear();

            foreach (UserData user in _current)
            {
                GameObject instance = Instantiate(memberTemplate, content);

                if (instance.TryGetComponent(out IUserProfile profile))
                    profile.UserData = user;

                _views.Add(instance);
                _shown.Add(user);
            }
        }

        private void ReadMembers()
        {
            _current.Clear();

            if (!session.InLobby)
                return;

            LobbyMemberData[] members = session.Lobby.Members;

            if (members == null)
                return;

            foreach (LobbyMemberData member in members)
            {
                if (!includeSelf && member.user.IsMe)
                    continue;

                _current.Add(member.user);
            }
        }

        private bool SameAsShown()
        {
            if (_current.Count != _shown.Count)
                return false;

            for (int i = 0; i < _current.Count; i++)
            {
                if (_current[i] != _shown[i])
                    return false;
            }

            return true;
        }

        private void Clear()
        {
            foreach (GameObject view in _views)
            {
                if (view != null)
                    Destroy(view);
            }

            _views.Clear();
            _shown.Clear();
        }
    }
}
