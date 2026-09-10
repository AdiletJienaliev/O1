using System;
using UnityEngine;
using Warlord.Configs.Bots;

namespace Warlord.Domain.Bots
{
    /// <summary>
    /// Что боту покупать. Ни одного названия юнита: кандидаты берутся из ростера,
    /// а решение складывается из четырёх чисел — сколько силы за золото, насколько
    /// хорош против того, что стоит напротив, чего не хватает своему строю и что
    /// любит характер.
    ///
    /// Это и есть ответ на «добавили юнита — переписывать ботов». Новый тип попадает
    /// в ростер и в тот же миг становится кандидатом: его сила считается из его статов,
    /// его контрность — из матрицы урона (а если она выключена, из честного соотношения
    /// урона и живучести). Переписывать нечего, потому что нигде не написано, что покупать.
    /// </summary>
    public sealed class BotArmyPlanner
    {
        /// <summary>Сколько тел каждого типа: индекс — позиция в ростере. Буферы живут с ботом.</summary>
        private readonly int[] _mine;
        private readonly int[] _enemy;

        public BotArmyPlanner(int rosterSize)
        {
            int size = Mathf.Max(1, rosterSize);
            _mine = new int[size];
            _enemy = new int[size];
        }

        public int[] MyComposition => _mine;
        public int[] EnemyComposition => _enemy;

        public void ClearComposition()
        {
            Array.Clear(_mine, 0, _mine.Length);
            Array.Clear(_enemy, 0, _enemy.Length);
        }

        public void CountMine(int rosterIndex)
        {
            if (rosterIndex >= 0 && rosterIndex < _mine.Length)
                _mine[rosterIndex]++;
        }

        public void CountEnemy(int rosterIndex)
        {
            if (rosterIndex >= 0 && rosterIndex < _enemy.Length)
                _enemy[rosterIndex]++;
        }

        /// <summary>
        /// Выбрать полевого юнита для покупки. <paramref name="canBuy"/> — серверная проверка
        /// покупки: планировщик не знает ни про золото, ни про лимит, ни про зону покупки,
        /// он только ранжирует то, что действительно доступно.
        /// Возвращает индекс в ростере или -1.
        /// </summary>
        public int ChooseFieldUnit(
            BotUnitCatalog catalog,
            BotPersonalityConfig personality,
            BotDifficultyConfig difficulty,
            Predicate<int> canBuy,
            System.Random random)
        {
            if (catalog == null || personality == null || canBuy == null)
                return -1;

            float rangedShareNow = CurrentRangedShare(catalog);
            float noise = difficulty != null ? 1f - difficulty.economySkill : 0.5f;

            int best = -1;
            float bestScore = 0f;

            for (int i = 0; i < catalog.Count; i++)
            {
                BotUnitRole role = catalog.Get(i);

                if (role.IsGarrison || role.Cost <= 0 || !canBuy(i))
                    continue;

                float score = ScoreUnit(catalog, personality, in role, rangedShareNow);

                // Чем хуже бот распоряжается деньгами, тем сильнее разброс: слабый противник
                // покупает не то, что нужно, а не то, что дешевле — и это разные ошибки.
                if (random != null)
                    score *= 1f + ((float)random.NextDouble() * 2f - 1f) * noise;

                if (score <= bestScore)
                    continue;

                bestScore = score;
                best = i;
            }

            return best;
        }

        /// <summary>Выбрать охранника на точку. Те же правила, только среди гарнизонных типов (ГДД §1.6).</summary>
        public int ChooseGuard(
            BotUnitCatalog catalog,
            BotPersonalityConfig personality,
            Predicate<int> canBuy,
            System.Random random)
        {
            if (catalog == null || personality == null || canBuy == null)
                return -1;

            int best = -1;
            float bestScore = 0f;

            for (int i = 0; i < catalog.Count; i++)
            {
                BotUnitRole role = catalog.Get(i);

                if (!role.IsGarrison || !canBuy(i))
                    continue;

                // Охраннику важнее выстоять, чем убить: он держит точку, а не берёт её.
                float score = role.Value * (0.5f + role.TankScore) * CounterScore(catalog, i);

                if (random != null)
                    score *= 1f + ((float)random.NextDouble() * 2f - 1f) * 0.15f;

                if (score <= bestScore)
                    continue;

                bestScore = score;
                best = i;
            }

            return best;
        }

        /// <summary>Оценка своей армии в тех же единицах, что и чужой. Нужна и планам, и оценщику целей.</summary>
        public float EstimateMyPower(BotUnitCatalog catalog) => EstimatePower(catalog, _mine);

        public static float EstimatePower(BotUnitCatalog catalog, int[] composition)
        {
            if (catalog == null || composition == null)
                return 0f;

            float total = 0f;

            for (int i = 0; i < composition.Length && i < catalog.Count; i++)
            {
                if (composition[i] > 0)
                    total += catalog.Get(i).Power * composition[i];
            }

            return total;
        }

        private float ScoreUnit(
            BotUnitCatalog catalog,
            BotPersonalityConfig personality,
            in BotUnitRole role,
            float rangedShareNow)
        {
            // 1. Сколько силы за золото. Базовая ценность, одинаковая для всех характеров.
            float score = role.Value;

            // 2. Насколько он хорош против того, что стоит напротив.
            score *= CounterScore(catalog, role.RosterIndex);

            // 3. Чего не хватает строю. Отклонение от желаемой доли стрелков тянет покупку
            //    в нужную сторону — без этого бот собирает армию из одного лучшего типа,
            //    а такая армия проигрывает первому же контрпику.
            float target = Mathf.Clamp01(personality.rangedShare);
            float need = role.IsRanged ? target - rangedShareNow : rangedShareNow - target;
            score *= 1f + Mathf.Clamp(need, -0.6f, 0.6f);

            // 4. Вкусы характера.
            score *= 1f + (personality.tankPreference - 1f) * role.TankScore;
            score *= 1f + (personality.splashPreference - 1f) * role.SplashScore;

            return Mathf.Max(0f, score);
        }

        /// <summary>
        /// Средняя эффективность типа против известного состава противника, взвешенная
        /// по числу тел. Пустой состав противника означает «ещё не разведано» — тогда
        /// множитель нейтральный, и покупка идёт по чистой ценности.
        /// </summary>
        private float CounterScore(BotUnitCatalog catalog, int rosterIndex)
        {
            float weighted = 0f;
            int total = 0;

            for (int i = 0; i < _enemy.Length && i < catalog.Count; i++)
            {
                if (_enemy[i] <= 0)
                    continue;

                weighted += catalog.Matchup(rosterIndex, i) * _enemy[i];
                total += _enemy[i];
            }

            return total > 0 ? weighted / total : 1f;
        }

        private float CurrentRangedShare(BotUnitCatalog catalog)
        {
            int ranged = 0;
            int total = 0;

            for (int i = 0; i < _mine.Length && i < catalog.Count; i++)
            {
                if (_mine[i] <= 0)
                    continue;

                total += _mine[i];
                if (catalog.Get(i).IsRanged)
                    ranged += _mine[i];
            }

            return total > 0 ? ranged / (float)total : 0f;
        }
    }
}
