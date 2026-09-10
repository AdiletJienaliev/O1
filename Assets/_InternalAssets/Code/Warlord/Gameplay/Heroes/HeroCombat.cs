using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;
using Warlord.Configs;
using Warlord.Core;
using Warlord.Domain.Combat;
using Warlord.Gameplay.Heroes.Input;
using Warlord.Gameplay.Match;

namespace Warlord.Gameplay.Heroes
{
    /// <summary>
    /// Удар мечом полководца (ГДД §8, §12). Движение предсказывается, а удар — нет:
    /// он проходит серверную валидацию дальности и кулдауна с перемоткой позиций цели,
    /// потому что именно удар решает исход боя.
    /// </summary>
    public sealed class HeroCombat : NetworkBehaviour
    {
        [SerializeField] private HeroController hero;
        [SerializeField] private MonoBehaviour inputSource;

        private readonly List<ICombatTarget> _candidates = new(32);
        private readonly List<ICombatTarget> _picked = new(4);

        private IHeroInputSource _input;
        private IMatchContext _context;
        private HeroConfig _config;

        private float _serverCooldown;
        private float _localCooldown;
        private bool _botDriven;

        /// <summary>Оставшийся кулдаун для HUD. На владельце считается локально, без ожидания сервера.</summary>
        public float CooldownRemaining => _localCooldown;

        private void Awake()
        {
            hero ??= GetComponent<HeroController>();
            _input = inputSource as IHeroInputSource;
            _input ??= GetComponent<IHeroInputSource>();
        }

        public void ServerInitialize(IMatchContext context)
        {
            _context = context;
            _config = context.Config.Hero;
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            MatchManager manager = MatchManager.Instance;
            if (manager != null)
                _config = manager.Config.Hero;
        }

        private void Update()
        {
            if (_localCooldown > 0f)
                _localCooldown -= Time.deltaTime;

            if (!IsOwner || _input == null || _config == null)
                return;

            if (!_input.ConsumeAttack())
                return;

            if (_localCooldown > 0f || !hero.IsAlive)
                return;

            // Локальный отклик сразу, чтобы удар не «залипал» на пинге;
            // сервер всё равно проверит кулдаун у себя и может отказать.
            _localCooldown = _config.attackCooldown;
            CmdAttack(TimeManager.LocalTick);

            // Замах владелец проигрывает у себя, не дожидаясь подтверждения: ждать
            // круг по сети ради анимации — ровно та задержка, из-за которой удар
            // ощущается ватным. Остальным её принесёт ObserversPlayAttack.
            AttackPerformed?.Invoke();
        }

        /// <summary>
        /// Серверный отсчёт кулдауна и удар бота. Вызывается из боевого такта.
        /// У бота нет ни клиента, ни Update-ветки владельца, поэтому его нажатие
        /// считывается здесь — в том же такте, что и весь остальной бой.
        /// </summary>
        public void ServerTick(float deltaTime)
        {
            if (_serverCooldown > 0f)
                _serverCooldown -= deltaTime;

            if (!_botDriven || _input == null)
                return;

            if (_input.ConsumeAttack())
                ServerTryAttack();
        }

        /// <summary>
        /// Отдать удар боту. Кулдаун и дальность у него общие с игроком — меняется только
        /// то, откуда пришло нажатие.
        /// </summary>
        public void ServerAttachBot(IHeroInputSource botInput)
        {
            if (botInput == null)
                return;

            if (inputSource != null)
                inputSource.enabled = false;

            _input = botInput;
            _botDriven = true;
        }

        /// <summary>
        /// Удар полководца-бота. Перемотки нет и быть не может: бот живёт на сервере
        /// и бьёт ровно по тому миру, который сервер видит сейчас — никакого пинга,
        /// который надо было бы компенсировать, у него нет.
        /// </summary>
        public bool ServerTryAttack()
        {
            if (_context == null || _config == null)
                return false;

            if (_context.Phase != MatchPhase.Running || hero == null || !hero.IsAlive)
                return false;

            if (_serverCooldown > 0f)
                return false;

            _serverCooldown = _config.attackCooldown;
            ApplyMeleeHit(0f);

            // Замах разошлётся наблюдателям тем же путём, что и у живого игрока.
            // Отдельно поднимать событие здесь нельзя: на хосте оно пришло бы дважды.
            ObserversPlayAttack();
            return true;
        }

        /// <summary>Готов ли удар. Бот смотрит на это, чтобы не подходить вплотную впустую.</summary>
        public bool ServerAttackReady => _serverCooldown <= 0f;

        [ServerRpc]
        private void CmdAttack(uint clientTick, NetworkConnection sender = null)
        {
            if (_context == null || _config == null)
                return;
            if (_context.Phase != MatchPhase.Running || !hero.IsAlive)
                return;
            if (_serverCooldown > 0f)
                return;

            _serverCooldown = _config.attackCooldown;

            float rewind = ResolveRewindSeconds(clientTick);
            ApplyMeleeHit(rewind);

            ObserversPlayAttack();
        }

        /// <summary>
        /// Перемотка на RTT/2 с потолком из NetworkConfig (ГДД §12).
        /// Тик приходит от клиента, поэтому значение обязательно зажимается:
        /// иначе подделанный тик позволил бы бить по прошлому на произвольную глубину.
        /// </summary>
        private float ResolveRewindSeconds(uint clientTick)
        {
            uint serverTick = TimeManager.Tick;
            if (clientTick == 0u || clientTick > serverTick)
                return 0f;

            float seconds = (serverTick - clientTick) * (float)TimeManager.TickDelta * 0.5f;
            float cap = _context.Config.Network.LagCompensationCapSeconds;

            return Mathf.Clamp(seconds, 0f, cap);
        }

        private void ApplyMeleeHit(float rewindSeconds)
        {
            float reach = _config.attackRange + _config.attackRangeTolerance;
            float sampleTime = _context.ServerTime - rewindSeconds;

            _context.Targeting.CollectInRadius(hero.Position, reach + 2f, null, _candidates);
            _picked.Clear();

            Vector3 forward = transform.forward;
            float cosHalfAngle = Mathf.Cos(_config.attackHalfAngle * Mathf.Deg2Rad);

            for (int i = 0; i < _candidates.Count; i++)
            {
                ICombatTarget candidate = _candidates[i];
                if (!candidate.IsAliveTarget() || ReferenceEquals(candidate, hero))
                    continue;
                if (!_context.Teams.AreEnemies(candidate.OwnerSlot, hero.OwnerSlot))
                    continue;

                Vector3 targetPosition = _context.LagCompensation.GetPositionAt(candidate, sampleTime);

                Vector3 delta = targetPosition - hero.Position;
                delta.y = 0f;

                float distance = delta.magnitude - candidate.Radius;
                if (distance > reach)
                    continue;

                if (distance > 0.01f && Vector3.Dot(forward, delta.normalized) < cosHalfAngle)
                    continue;

                _picked.Add(candidate);
            }

            SortByDistance(_picked, hero.Position);

            int limit = Mathf.Min(_config.maxTargetsPerHit, _picked.Count);
            for (int i = 0; i < limit; i++)
                _context.Damage.Enqueue(hero, _picked[i], _config.attackDamage);

            _candidates.Clear();
            _picked.Clear();
        }

        private static void SortByDistance(List<ICombatTarget> targets, Vector3 origin)
        {
            targets.Sort((a, b) =>
            {
                float left = (a.Position - origin).sqrMagnitude;
                float right = (b.Position - origin).sqrMagnitude;
                return left.CompareTo(right);
            });
        }

        /// <summary>
        /// Визуальный отклик удара. Урон здесь не считается — только анимация и звук.
        /// Владелец исключён: он уже проиграл замах локально в момент нажатия.
        /// </summary>
        [ObserversRpc(ExcludeOwner = true)]
        private void ObserversPlayAttack() => AttackPerformed?.Invoke();

        /// <summary>Событие для аниматора и VFX. Презентация подписывается сама.</summary>
        public event System.Action AttackPerformed;
    }
}
