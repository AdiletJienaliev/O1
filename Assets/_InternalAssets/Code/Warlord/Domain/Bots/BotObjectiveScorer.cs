using UnityEngine;
using Warlord.Configs.Bots;
using Warlord.Core;

namespace Warlord.Domain.Bots
{
    /// <summary>
    /// Выбор цели бота. Утилитарный подход: каждая осмысленная цель получает оценку,
    /// побеждает лучшая. Правил вида «если центр захвачен, то иди на аванпост» здесь нет
    /// намеренно — такие цепочки и приходится переписывать при каждой правке карты.
    /// Оценка же складывается из ценности, достижимости и характера, и новая точка
    /// на карте просто добавляет ещё одного претендента в общий список.
    ///
    /// Здесь нет ни одного идентификатора юнита, ни одной координаты и ни одного
    /// названия точки: всё приходит числами в <see cref="BotSituation"/>.
    ///
    /// Чистый класс без состояния матча — его можно прогонять в тестах на выдуманных
    /// раскладах и смотреть, действительно ли «хитрый» уходит в рейд, а «жадный» копит.
    /// </summary>
    public sealed class BotObjectiveScorer
    {
        private BotObjective _best;
        private BotObjective _second;

        /// <summary>
        /// Выбрать цель. <paramref name="current"/> и <paramref name="committed"/> дают гистерезис:
        /// пока бот держится за цель, она получает прибавку и не сменяется от каждого шороха.
        /// Без этого бот на равных оценках дёргается между двумя точками и выглядит сломанным.
        /// </summary>
        public BotObjective Evaluate(
            BotSituation situation,
            BotPersonalityConfig personality,
            BotDifficultyConfig difficulty,
            in BotObjective current,
            bool committed,
            System.Random random)
        {
            _best = BotObjective.None;
            _second = BotObjective.None;

            if (situation == null || personality == null)
                return _best;

            float noise = difficulty != null ? difficulty.ScoreNoise : 0.2f;
            float stickiness = committed ? 1f + 0.4f * personality.persistence : 1f;

            ScorePoints(situation, personality, in current, stickiness, noise, random);
            ScoreHunt(situation, personality, in current, stickiness, noise, random);
            ScoreEconomy(situation, personality, in current, stickiness, noise, random);

            // Ошибка вместо оптимума. Живой игрок регулярно выбирает вторую по качеству цель,
            // и именно это отличает противника от решателя: идеальный выбор каждые полсекунды
            // читается как машина быстрее любой другой приметы.
            float mistake = difficulty != null ? difficulty.mistakeChance : 0f;

            if (_second.Score > 0f && random != null && random.NextDouble() < mistake)
                return _second;

            return _best;
        }

        #region Точки

        private void ScorePoints(
            BotSituation s,
            BotPersonalityConfig p,
            in BotObjective current,
            float stickiness,
            float noise,
            System.Random random)
        {
            for (int i = 0; i < s.PointCount; i++)
            {
                BotPointView point = s.Points[i];

                // Своя и союзная точка — это оборона. Чужая или ничья — захват или рейд.
                if (point.Mine || point.Allied)
                {
                    Offer(ScoreDefend(s, p, in point), in current, stickiness, noise, random);
                    continue;
                }

                if (point.Kind == CapturePointKind.BaseFlag && PlayerSlots.IsValid(point.ZoneOwnerSlot))
                {
                    Offer(ScoreRaid(s, p, in point), in current, stickiness, noise, random);
                    continue;
                }

                Offer(ScoreCapture(s, p, in point), in current, stickiness, noise, random);
            }
        }

        /// <summary>Взять чужую или ничью точку: ценность на достижимость на шансы удержать.</summary>
        private BotObjective ScoreCapture(BotSituation s, BotPersonalityConfig p, in BotPointView point)
        {
            float weight = point.Kind == CapturePointKind.CentralFlag ? p.centerWeight : p.outpostWeight;
            if (weight <= 0.001f)
                return BotObjective.None;

            float value = PointValue(s, in point);
            float winnable = Winnability(s.ArmyPower + point.FriendlyForce, point.EnemyForce, p.caution);
            float reach = Proximity(point.DistanceFromHero, s.MapScale);

            // Уже начатый захват дожимают охотнее, чем начинают новый: брошенная на середине
            // шкала — это чистый убыток по времени, и живой игрок это чувствует.
            float momentum = point.Neutral && point.OwnerProgress > 0.05f ? 1.15f : 1f;

            // В неразведанное идут чуть неохотнее — но идут: страх перед туманом
            // превратил бы бота в домоседа, который никогда не выходит с базы.
            float fog = point.Scouted ? 1f : 0.85f;

            float score = weight * value * winnable * reach * momentum * fog;

            return new BotObjective(BotGoalKind.Capture, point.Position, score, point.Index);
        }

        /// <summary>
        /// Прикрыть свою точку. Своя база — особый случай: её потеря выбивает из матча (ГДД §10.3),
        /// поэтому она перевешивает любую наступательную мысль, а не соревнуется с ней на равных.
        /// </summary>
        private BotObjective ScoreDefend(BotSituation s, BotPersonalityConfig p, in BotPointView point)
        {
            bool home = point.Kind == CapturePointKind.BaseFlag && point.ZoneOwnerSlot == s.Slot;
            float weight = point.Allied && !point.Mine ? p.supportWeight : p.defenseWeight;

            if (weight <= 0.001f && !home)
                return BotObjective.None;

            float threat = 0f;

            if (point.UnderAttack)
                threat = 1f;
            else if (point.EnemyForce > 0.01f)
                threat = 0.55f;
            else if (point.Kind == CapturePointKind.Outpost && point.GuardCount == 0 && point.OwnerProgress < 0.99f)
                threat = 0.3f;

            if (threat <= 0f)
                return BotObjective.None;

            float value = PointValue(s, in point);
            float reach = Proximity(point.DistanceFromHero, s.MapScale);

            // Дом защищают всегда: сюда бот возвращается даже с выигранной атаки.
            float existential = home ? 3.5f : 1f;
            float effectiveWeight = home ? Mathf.Max(1f, weight) : weight;

            float score = effectiveWeight * value * threat * reach * existential;

            return new BotObjective(BotGoalKind.Defend, point.Position, score, point.Index, point.OwnerSlot);
        }

        /// <summary>
        /// Рейд на чужую базу — то самое «залезть на базу мимо главного флага».
        /// Отдельной цели «обойти» не существует: обход возникает сам, потому что
        /// флаг чужой базы оценивается наравне с центром, и при слабой обороне
        /// он просто выигрывает у центра по очкам.
        /// </summary>
        private BotObjective ScoreRaid(BotSituation s, BotPersonalityConfig p, in BotPointView point)
        {
            if (p.raidWeight <= 0.001f)
                return BotObjective.None;

            BotRivalView rival = s.Rival(point.ZoneOwnerSlot);

            if (rival.Eliminated || rival.Allied)
                return BotObjective.None;

            // С пустыми руками на базу не ходят: там гарнизон, лечение и респавн хозяина.
            // Кроме характеров, которые именно этим и живут — они ставят raidWithArmy в false.
            // Считаем полевую армию: охранники в рейд не идут.
            float readiness = p.raidWithArmy
                ? Mathf.InverseLerp(p.raidMinArmyShare * 0.5f, Mathf.Max(0.05f, p.raidMinArmyShare), s.FieldFill)
                : 1f;

            if (readiness <= 0.01f)
                return BotObjective.None;

            float defense = Mathf.Max(point.EnemyForce, rival.BaseDefense);
            float winnable = Winnability(s.ArmyPower, defense, p.caution);

            // Хищность: чем слабее и чем дальше впереди по очкам жертва, тем заманчивее.
            float weakness = 1f - Mathf.Clamp01(rival.ArmyPower / Mathf.Max(1f, s.ArmyPower + rival.ArmyPower));
            float leader = Mathf.Clamp01(rival.FlagHoldSeconds / 120f);
            float grudge = Mathf.Clamp01(rival.RecentAggression);

            float pick = 1f
                + p.opportunism * (weakness + leader)
                + p.vengeance * grudge;

            float reach = Proximity(point.DistanceFromHero, s.MapScale);

            // Рейд — вложение времени: половина карты туда и столько же обратно. Осторожный
            // характер поэтому ходит в него реже даже при равных силах.
            float commitment = Mathf.Lerp(1f, 0.6f, p.caution);

            float score = p.raidWeight * winnable * pick * reach * readiness * commitment;

            // Домашняя работа сначала: пока свою базу сбивают, чужую не берут.
            if (s.HomeUnderAttack)
                score *= 0.25f;

            return new BotObjective(BotGoalKind.Raid, point.Position, score, point.Index, point.ZoneOwnerSlot);
        }

        #endregion

        #region Охота и экономика

        private void ScoreHunt(
            BotSituation s,
            BotPersonalityConfig p,
            in BotObjective current,
            float stickiness,
            float noise,
            System.Random random)
        {
            if (p.huntWeight <= 0.001f || !s.HeroAlive)
                return;

            for (int i = 0; i < s.RivalCount; i++)
            {
                BotRivalView rival = s.Rivals[i];

                if (rival.Allied || rival.Eliminated || !rival.HeroKnown || !rival.HeroAlive)
                    continue;

                float reach = Proximity(Vector3.Distance(s.HeroPosition, rival.HeroPosition), s.MapScale);

                // Раненого добивают охотнее: смерть полководца стоит противнику
                // респавна и всей позиции разом (ГДД §8).
                float wounded = 1f + (1f - Mathf.Clamp01(rival.HeroHealthFraction));

                // В одиночку — цель, в окружении своей армии — ловушка.
                float escort = 1f - Mathf.Clamp01(rival.ArmyPower / Mathf.Max(1f, s.ArmyPower + rival.ArmyPower));
                float grudge = 1f + p.vengeance * Mathf.Clamp01(rival.RecentAggression);

                float score = p.huntWeight * reach * wounded * escort * grudge * s.HeroHealthFraction;

                Offer(new BotObjective(BotGoalKind.Hunt, rival.HeroPosition, score, -1, rival.Slot),
                    in current, stickiness, noise, random);
            }
        }

        /// <summary>
        /// Сидеть дома и копить. Это полноценная цель, а не «нечего делать»: у зоны покупки
        /// стоят и деньги, и лечение, и очередь постройки, и живой игрок половину матча
        /// действительно проводит на базе.
        /// </summary>
        private void ScoreEconomy(
            BotSituation s,
            BotPersonalityConfig p,
            in BotObjective current,
            float stickiness,
            float noise,
            System.Random random)
        {
            // Недобор считается по полевой армии: гарнизон в бой не пойдёт, и мерить
            // им готовность значит считать себя готовым, никого при этом не набрав.
            float target = Mathf.Max(0.05f, p.massBeforePush);
            float deficit = Mathf.Clamp01((target - s.FieldFill) / target);

            // Хватает ли вообще на покупку: с пустым кошельком дома делать нечего.
            int cheapest = Mathf.Max(1, s.CheapestUnitCost);
            float funded = Mathf.Clamp01(s.Gold / (float)cheapest);

            // Сколько золота залежалось и есть ли куда его девать. Без этих слагаемых бот,
            // ушедший драться за точку, не возвращался бы домой никогда: точка на карте всегда
            // чего-то стоит, а «сходить за армией» стоило бы одинаково и в начале матча,
            // и с тысячей золота в кармане. Так бот и превращается в одинокого полководца.
            float hoard = Mathf.Clamp01(s.Gold / (float)(cheapest * 5));
            float room = 1f - s.ArmyFill;

            // Есть что потратить на прокачку — тоже повод заглянуть домой.
            float upgrades = s.Xp > 0 ? 0.15f : 0f;

            float score = p.economyWeight * (0.25f + 1.6f * deficit * funded + 1.3f * hoard * room + upgrades);

            // Раненый полководец лечится на базе (ГДД §5.1) — это часть той же цели.
            if (s.HeroHealthFraction < 0.5f)
                score *= 1f + (0.5f - s.HeroHealthFraction) * 2f;

            Offer(new BotObjective(BotGoalKind.Economy, s.BasePosition, score), in current, stickiness, noise, random);
        }

        #endregion

        #region Служебное

        /// <summary>Ценность точки: доход плюс вклад в победу. Центр к концу матча дорожает (ГДД §2).</summary>
        private static float PointValue(BotSituation s, in BotPointView point)
        {
            float income = point.IncomeValue * 0.1f;

            switch (point.Kind)
            {
                case CapturePointKind.CentralFlag:
                    // Время удержания — главный критерий победы, и чем ближе конец,
                    // тем дороже каждая его секунда.
                    return 1f + income + Mathf.Lerp(0.4f, 1.6f, s.MatchProgress);

                case CapturePointKind.BaseFlag:
                    return 1.4f + income;

                default:
                    return 0.55f + income;
            }
        }

        /// <summary>
        /// Шансы выиграть столкновение. Осторожность двигает точку перелома: осторожный
        /// характер требует перевеса, безрассудный лезет и в равный бой.
        /// </summary>
        private static float Winnability(float myForce, float enemyForce, float caution)
        {
            if (enemyForce <= 0.01f)
                return 1f;

            float ratio = myForce / Mathf.Max(0.01f, enemyForce);
            float required = Mathf.Lerp(0.6f, 1.6f, Mathf.Clamp01(caution));

            return Mathf.Clamp01(ratio / required);
        }

        /// <summary>Близость в долях карты. Дальняя цель не запрещена, она просто дешевле.</summary>
        private static float Proximity(float distance, float mapScale)
        {
            float scale = Mathf.Max(1f, mapScale);
            return 1f / (1f + distance / scale);
        }

        /// <summary>Принять претендента: шум, гистерезис и место в двойке лучших.</summary>
        private void Offer(
            in BotObjective candidate,
            in BotObjective current,
            float stickiness,
            float noise,
            System.Random random)
        {
            if (candidate.Score <= 0f)
                return;

            float jitter = random != null ? 1f + ((float)random.NextDouble() * 2f - 1f) * noise : 1f;
            float score = candidate.Score * jitter;

            if (candidate.SameAs(in current))
                score *= stickiness;

            BotObjective scored = new(candidate.Kind, candidate.Position, score, candidate.PointIndex, candidate.TargetSlot);

            if (score > _best.Score)
            {
                _second = _best;
                _best = scored;
                return;
            }

            if (score > _second.Score)
                _second = scored;
        }

        #endregion
    }
}
