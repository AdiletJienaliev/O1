using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Warlord.Configs.Bots;
using Warlord.Core;
using Warlord.Domain.Bots;
using Warlord.Domain.Combat;
using Warlord.Gameplay.Capture;
using Warlord.Gameplay.Heroes;
using Warlord.Gameplay.Match;
using Warlord.Gameplay.Players;

namespace Warlord.Gameplay.Bots
{
    /// <summary>
    /// Один бот целиком: глаза (<see cref="BotSense"/>), решение (<see cref="BotObjectiveScorer"/>),
    /// руки (<see cref="BotHands"/>) и ноги (<see cref="BotHeroPilot"/>).
    ///
    /// Здесь живёт только связь между ними и то, что нельзя было отдать ни одному из них:
    /// темп размышления, задержка реакции, рефлекс отхода и превращение выбранной цели
    /// в конкретную точку на карте. Ни одного «если карта такая-то» и ни одного имени юнита
    /// в этом файле нет — и это не случайность, а условие, при котором систему ботов
    /// не придётся переписывать при следующей правке контента.
    /// </summary>
    public sealed class BotPlayer
    {
        private readonly IMatchContext _context;
        private readonly PlayerState _player;
        private readonly BotProfile _profile;
        private readonly BotHeroPilot _pilot;

        private readonly BotMemory _memory;
        private readonly BotUnitCatalog _catalog;
        private readonly BotArmyPlanner _planner;
        private readonly BotObjectiveScorer _scorer = new();
        private readonly BotSense _sense;
        private readonly BotHands _hands;
        private readonly System.Random _random;

        private BotObjective _goal = BotObjective.None;
        private BotObjective _pendingGoal = BotObjective.None;

        private float _decisionTimer;
        private float _reactionTimer;
        private float _commitmentTimer;
        private bool _hasPending;

        private Vector3 _approach;
        private bool _hasApproach;

        private bool _retreating;

        public BotPlayer(IMatchContext context, PlayerState player, HeroController hero, in BotProfile profile, BotHeroPilot pilot)
        {
            _context = context;
            _player = player;
            _profile = profile;
            _pilot = pilot;

            Hero = hero;

            // Своё зерно на бота: два бота одного характера обязаны ошибаться по-разному,
            // иначе пара одинаковых противников ходит строем и читается как один скрипт.
            _random = new System.Random(player.Slot * 7919 + profile.NameIndex * 104729 + System.Environment.TickCount);

            int points = context.CapturePoints != null ? context.CapturePoints.Count : 0;
            _memory = new BotMemory(Mathf.Max(1, points), PlayerSlots.MaxSupported);
            _catalog = new BotUnitCatalog(context.Config.Roster, context.Config.DamageMatrix);
            _planner = new BotArmyPlanner(context.Config.Roster != null ? context.Config.Roster.Count : 1);

            _sense = new BotSense(context, player, in profile, _memory, _catalog);
            _hands = new BotHands(context, player, in profile, _catalog, _planner, _sense, _random);

            // Первое решение принимается вразнобой с соседями: одновременный старт всех
            // ботов даёт синхронный рывок в центр, чего живые игроки никогда не делают.
            _decisionTimer = (float)_random.NextDouble() * profile.DecisionInterval;
        }

        public int Slot => _player.Slot;
        public PlayerState Player => _player;
        public HeroController Hero { get; }
        public BotProfile Profile => _profile;

        /// <summary>Текущая цель. Читается отладкой и HUD-ом наблюдателя.</summary>
        public BotObjective Goal => _goal;

        /// <summary>Сколько дохода получает бот сверх обычного. Гандикап сложности, обычно единица.</summary>
        public float IncomeMultiplier => _profile.IncomeMultiplier;

        /// <summary>Бота ударили. Кормит злопамятность характера: обидчик становится желаннее целью.</summary>
        public void NoteAggression(int fromSlot, float amount)
        {
            if (_context.Teams.AreEnemies(fromSlot, _player.Slot))
                _memory.NoteAggression(fromSlot, amount);
        }

        /// <summary>Один боевой такт бота. Зовётся из <see cref="BotSystem"/>.</summary>
        public void Tick(float deltaTime, float matchProgress, float now)
        {
            if (_player == null || _player.IsEliminated)
            {
                _pilot?.Halt();
                return;
            }

            _memory.DecayAggression(deltaTime);

            TickDecision(deltaTime, matchProgress, now);

            _hands.Tick(deltaTime, _sense.Situation);

            TickBody(deltaTime);
        }

        #region Решение

        private void TickDecision(float deltaTime, float matchProgress, float now)
        {
            _decisionTimer -= deltaTime;
            _commitmentTimer -= deltaTime;

            if (_hasPending)
            {
                _reactionTimer -= deltaTime;

                if (_reactionTimer <= 0f)
                {
                    AdoptGoal(in _pendingGoal);
                    _hasPending = false;
                }
            }

            if (_decisionTimer > 0f)
                return;

            _decisionTimer = _profile.DecisionInterval;

            _sense.Refresh(now, matchProgress);
            BotSituation situation = _sense.Situation;

            // Рефлекс отхода сильнее любой стратегии и работает без задержки реакции:
            // раненый игрок разворачивается сам, не «обдумав ситуацию».
            if (ShouldRetreat(situation))
            {
                _retreating = true;
                AdoptGoal(new BotObjective(BotGoalKind.Retreat, situation.BasePosition, 99f));
                return;
            }

            _retreating = false;

            BotObjective chosen = _scorer.Evaluate(
                situation,
                _profile.Personality,
                _profile.Difficulty,
                in _goal,
                _commitmentTimer > 0f,
                _random);

            if (chosen.Score <= 0f || chosen.SameAs(in _goal))
                return;

            // Новая мысль доходит до рук не мгновенно.
            _pendingGoal = chosen;
            _reactionTimer = _profile.ReactionDelay;
            _hasPending = true;
        }

        private void AdoptGoal(in BotObjective goal)
        {
            _goal = goal;
            _commitmentTimer = _profile.CommitmentSeconds;
            _hasApproach = false;

            ApplyArmyOrder(in goal);
        }

        /// <summary>
        /// Пора ли уходить. Порог задаётся сложностью, но растягивается качеством микро:
        /// слабый бот замечает, что умирает, слишком поздно — и это честная разница
        /// в исполнении, а не урезанный урон.
        /// </summary>
        private bool ShouldRetreat(BotSituation situation)
        {
            if (!situation.HeroAlive)
                return false;

            float threshold = Mathf.Lerp(
                _profile.RetreatHealthFraction * 0.35f,
                _profile.RetreatHealthFraction,
                Mathf.Clamp01(_profile.Micro));

            if (_retreating)
            {
                // Обратно в бой — только заметно подлечившись, иначе бот дёргается
                // туда-сюда на границе порога.
                return situation.HeroHealthFraction < Mathf.Min(0.85f, threshold + 0.35f);
            }

            if (situation.HeroHealthFraction > threshold)
                return false;

            return NearestEnemyDistance(situation) < situation.MapScale * 0.35f;
        }

        private float NearestEnemyDistance(BotSituation situation)
        {
            ICombatTarget enemy = _context.Targeting.FindNearestEnemy(
                situation.HeroPosition,
                _player.Slot,
                situation.MapScale * 0.35f);

            return enemy.IsAliveTarget()
                ? Vector3.Distance(situation.HeroPosition, enemy.Position)
                : float.MaxValue;
        }

        #endregion

        #region Тело

        private void TickBody(float deltaTime)
        {
            if (_pilot == null)
                return;

            if (Hero == null || !Hero.IsAlive)
            {
                _pilot.Halt();
                _pilot.SetCombatTarget(null);
                return;
            }

            BotSituation situation = _sense.Situation;

            _pilot.SetCombatTarget(_retreating ? null : PickCombatTarget(situation));

            Vector3 destination = ResolveDestination(situation, in _goal, out float stopDistance);

            // Бег включается на дальних переходах: спринт вплотную к цели только
            // мешает точному выходу в круг захвата.
            bool sprint = (destination - Hero.Position).sqrMagnitude > 12f * 12f;

            _pilot.MoveTo(destination, stopDistance, sprint);
        }

        /// <summary>
        /// Кого бить. Бот не гоняется за всем подряд: цель берётся близкая, и только
        /// пока она рядом — иначе полководец уходит за одиноким лучником через полкарты
        /// и бросает то, зачем пришёл.
        /// </summary>
        private ICombatTarget PickCombatTarget(BotSituation situation)
        {
            float reach = _context.Config.Hero != null ? _context.Config.Hero.attackRange : 2.5f;

            // Радиус преследования растёт с качеством микро: сильный бот не боится
            // сделать три шага в сторону ради добивания, слабый бьёт только то, что само подошло.
            float chase = Mathf.Lerp(reach + 1.5f, reach + 9f, Mathf.Clamp01(_profile.Micro));

            if (_goal.Kind == BotGoalKind.Hunt && PlayerSlots.IsValid(_goal.TargetSlot))
            {
                PlayerState prey = _context.Players.Get(_goal.TargetSlot);

                if (prey != null && prey.Hero != null && prey.Hero.IsAlive
                    && Vector3.Distance(prey.Hero.Position, situation.HeroPosition) <= chase * 1.5f)
                {
                    return prey.Hero;
                }
            }

            return _context.Targeting.FindNearestEnemy(situation.HeroPosition, _player.Slot, chase);
        }

        /// <summary>Куда идти телом под текущую цель.</summary>
        private Vector3 ResolveDestination(BotSituation situation, in BotObjective goal, out float stopDistance)
        {
            switch (goal.Kind)
            {
                case BotGoalKind.Economy:
                case BotGoalKind.Retreat:
                    // В зону покупки, а не к её краю: покупка требует быть внутри (ГДД §5.1).
                    stopDistance = Mathf.Max(1.5f, situation.BuyZoneRadius * 0.4f);
                    return situation.BasePosition;

                case BotGoalKind.Hunt:
                    stopDistance = 2f;
                    return goal.Position;

                default:
                    stopDistance = PointStopDistance(goal.PointIndex);
                    return ResolveApproach(situation, in goal);
            }
        }

        /// <summary>Внутрь круга захвата, а не к его краю: снаружи точка не берётся вовсе (ГДД §9).</summary>
        private float PointStopDistance(int pointIndex)
        {
            IReadOnlyList<CapturePointBehaviour> points = _context.CapturePoints;

            if (pointIndex < 0 || pointIndex >= points.Count || points[pointIndex] == null)
                return 2f;

            return Mathf.Max(1.5f, points[pointIndex].CaptureRadius * 0.4f);
        }

        /// <summary>
        /// Обход. Характер с <see cref="BotPersonalityConfig.flanks"/> не прёт напролом через
        /// занятую врагом середину, а закладывает крюк по краю — то самое «залезть на базу
        /// мимо главного флага».
        ///
        /// Крюк не задан точками на карте и не привязан к арене: берётся середина пути,
        /// отводится вбок на половину дистанции в обе стороны, и выигрывает та сторона,
        /// где меньше чужой силы. Поменяется карта — поменяются и стороны, сами собой.
        /// </summary>
        private Vector3 ResolveApproach(BotSituation situation, in BotObjective goal)
        {
            BotPersonalityConfig personality = _profile.Personality;

            if (personality == null || !personality.flanks)
                return goal.Position;

            Vector3 from = situation.HeroPosition;
            Vector3 to = goal.Position;

            float distance = Vector3.Distance(from, to);

            // Вблизи обходить нечего, да и незачем.
            if (distance < situation.MapScale * 0.35f)
            {
                _hasApproach = false;
                return to;
            }

            if (_hasApproach)
            {
                if ((from - _approach).sqrMagnitude > 8f * 8f)
                    return _approach;

                _hasApproach = false;
                return to;
            }

            Vector3 direction = (to - from).normalized;
            Vector3 side = Vector3.Cross(direction, Vector3.up).normalized;
            Vector3 middle = (from + to) * 0.5f;
            float offset = distance * 0.5f;

            float straight = ThreatAlong(situation, middle);
            Vector3 left = middle - side * offset;
            Vector3 right = middle + side * offset;

            float leftThreat = ThreatAlong(situation, left);
            float rightThreat = ThreatAlong(situation, right);

            Vector3 candidate = leftThreat <= rightThreat ? left : right;
            float candidateThreat = Mathf.Min(leftThreat, rightThreat);

            // Крюк оправдан только если прямая дорога заметно опаснее: лишний круг
            // по карте стоит времени, и делать его просто так — не хитрость, а глупость.
            if (candidateThreat >= straight * 0.6f)
                return to;

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 12f, NavMesh.AllAreas))
                return to;

            _approach = hit.position;
            _hasApproach = true;
            return _approach;
        }

        /// <summary>Сколько чужой силы рядом с этой точкой пути — по известным боту данным.</summary>
        private float ThreatAlong(BotSituation situation, Vector3 position)
        {
            float threat = 0f;

            for (int i = 0; i < situation.PointCount; i++)
            {
                BotPointView point = situation.Points[i];

                if (point.Mine || point.Allied)
                    continue;

                float distance = Vector3.Distance(point.Position, position);
                float weight = 1f / (1f + distance / Mathf.Max(1f, situation.MapScale * 0.25f));

                threat += (point.EnemyForce + (point.Enemy ? 20f : 0f)) * weight;
            }

            return threat;
        }

        #endregion

        #region Армия

        /// <summary>
        /// Куда и с каким приказом идёт армия. Приказ один на всю армию (ГДД §6),
        /// поэтому выбор простой: в поход — походный приказ характера, на точке — оборонительный.
        /// </summary>
        private void ApplyArmyOrder(in BotObjective goal)
        {
            BotPersonalityConfig personality = _profile.Personality;

            if (personality == null || _player.Army == null)
                return;

            float yaw = Hero != null ? Hero.YawDegrees : 0f;

            switch (goal.Kind)
            {
                case BotGoalKind.Retreat:
                case BotGoalKind.Economy:
                    _hands.ApplyArmyOrder(personality.holdOrder, _context.Players.GetBaseAnchor(_player.Slot).UnitSpawnPoint, yaw);
                    return;

                case BotGoalKind.Defend:
                    _hands.ApplyArmyOrder(personality.holdOrder, goal.Position, yaw);
                    return;

                case BotGoalKind.Raid:
                    // На чужой базе стоять некогда: там респавн хозяина и лечение.
                    _hands.ApplyArmyOrder(ArmyOrderType.AttackMove, goal.Position, yaw);
                    return;

                default:
                    _hands.ApplyArmyOrder(personality.travelOrder, goal.Position, yaw);
                    return;
            }
        }

        #endregion
    }
}
