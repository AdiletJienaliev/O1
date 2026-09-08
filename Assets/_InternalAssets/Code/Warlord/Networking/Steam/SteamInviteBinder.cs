using HeathenEngineering.SteamworksIntegration.UI;
using UnityEngine;

namespace Warlord.Networking.Steam
{
    /// <summary>
    /// Связывает готовые виджеты приглашений из пакета Heathen с нашим лобби.
    /// </summary>
    /// <remarks>
    /// Виджеты Heathen (список друзей, выпадашка приглашения, кнопка на друге) умеют выбрать
    /// пользователя, но не знают, куда его звать: лобби заводит <see cref="SteamSession"/>.
    /// Этот компонент — единственное место, где они встречаются, поэтому чужие префабы можно
    /// бросать в сцену как есть, без правок их скриптов.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class SteamInviteBinder : MonoBehaviour
    {
        [Header("Сеанс")]
        [SerializeField] private SteamSession session;

        [Header("Виджеты Heathen")]
        [Tooltip("Выпадающий список друзей с кнопкой приглашения.")]
        [SerializeField] private FriendInviteDropDown inviteDropDown;

        [Tooltip("Отдельные кнопки «пригласить» на карточках друзей.")]
        [SerializeField] private UserInviteButton[] inviteButtons;

        private void Awake() => session ??= FindAnyObjectByType<SteamSession>();

        private void OnEnable()
        {
            if (session == null)
            {
                Debug.LogError("SteamInviteBinder: не найден SteamSession — приглашения работать не будут", this);
                return;
            }

            if (inviteDropDown != null)
                inviteDropDown.Invited.AddListener(session.Invite);

            if (inviteButtons == null)
                return;

            foreach (UserInviteButton button in inviteButtons)
            {
                if (button != null)
                    button.Click.AddListener(OnInviteButtonClicked);
            }
        }

        private void OnDisable()
        {
            if (session == null)
                return;

            if (inviteDropDown != null)
                inviteDropDown.Invited.RemoveListener(session.Invite);

            if (inviteButtons == null)
                return;

            foreach (UserInviteButton button in inviteButtons)
            {
                if (button != null)
                    button.Click.RemoveListener(OnInviteButtonClicked);
            }
        }

        private void OnInviteButtonClicked(UserAndPointerData data)
        {
            if (data != null)
                session.Invite(data.user);
        }
    }
}
