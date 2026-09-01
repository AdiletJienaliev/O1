using FishNet.Object;
using UnityEngine;
using Warlord.Core;
using Warlord.Gameplay.Heroes.Input;
using Warlord.Gameplay.Match;
using Warlord.Gameplay.Players;

namespace Warlord.Gameplay.Heroes
{
    /// <summary>
    /// Превращает ввод владельца в команды серверу (ГДД §6, §7, §12).
    /// Приказ не применяется мгновенно, а уходит на подтверждение — локальный отклик
    /// даётся событиями, чтобы задержка не ощущалась.
    /// </summary>
    public sealed class HeroCommandRouter : NetworkBehaviour
    {
        [SerializeField] private HeroController hero;
        [SerializeField] private MonoBehaviour inputSource;

        private IHeroInputSource _input;

        /// <summary>Локальный отклик на отданный приказ: звук, курсор, подсветка. Аргумент — тип приказа.</summary>
        public event System.Action<ArmyOrderType> OrderIssuedLocally;

        /// <summary>Локальный отклик на смену построения.</summary>
        public event System.Action<int> FormationIssuedLocally;

        private void Awake()
        {
            hero ??= GetComponent<HeroController>();
            _input = inputSource as IHeroInputSource;
            _input ??= GetComponent<IHeroInputSource>();
        }

        private void Update()
        {
            if (!IsOwner || _input == null)
                return;

            PlayerState player = PlayerState.Local;
            if (player == null)
                return;

            HandleOrders(player);
            HandleFormations(player);
        }

        private void HandleOrders(PlayerState player)
        {
            int requested = _input.ConsumeOrderRequest();
            bool hasPoint = _input.ConsumeOrderPoint(out Vector3 worldPoint);

            if (requested < 0)
                return;

            ArmyOrderType orderType = (ArmyOrderType)requested;

            // Якорь: точка под курсором, если она была указана, иначе позиция полководца.
            Vector3 anchor = hasPoint ? worldPoint : hero.Position;
            float yaw = hero.YawDegrees;

            player.CmdSetOrder((byte)orderType, anchor, yaw);
            OrderIssuedLocally?.Invoke(orderType);
        }

        private void HandleFormations(PlayerState player)
        {
            int requested = _input.ConsumeFormationRequest();
            if (requested < 0)
                return;

            player.CmdSetFormation((byte)requested);
            FormationIssuedLocally?.Invoke(requested);
        }

        /// <summary>Покупка юнита из панели базы. Вызывается UI, а не вводом.</summary>
        public void RequestPurchase(int rosterIndex)
        {
            PlayerState player = PlayerState.Local;
            if (player == null || !IsOwner)
                return;

            player.CmdPurchaseUnit((byte)Mathf.Clamp(rosterIndex, 0, byte.MaxValue));
        }

        /// <summary>Покупка перка из экрана древа. Не требует нахождения на базе (ГДД §11).</summary>
        public void RequestUpgrade(UpgradeBranch branch)
        {
            PlayerState player = PlayerState.Local;
            if (player == null || !IsOwner)
                return;

            player.CmdBuyUpgrade((byte)branch);
        }
    }
}
