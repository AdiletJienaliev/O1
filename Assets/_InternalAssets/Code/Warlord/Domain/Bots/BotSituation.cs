using UnityEngine;
using Warlord.Core;

namespace Warlord.Domain.Bots
{
    /// <summary>
    /// Что бот знает об одной точке захвата. Никаких ссылок на сцену: только числа,
    /// собранные наблюдением. Поэтому оценщик целей тестируется без Unity, а карта
    /// может измениться как угодно — точек станет больше или меньше, и это всё.
    /// </summary>
    public struct BotPointView
    {
        /// <summary>Индекс в списке точек матча. По нему бот потом находит саму точку.</summary>
        public int Index;

        public CapturePointKind Kind;
        public Vector3 Position;

        /// <summary>Владелец по последним разведданным. Может отличаться от настоящего.</summary>
        public int OwnerSlot;

        /// <summary>Чья это база, если точка — флаг базы. Иначе <see cref="PlayerSlots.None"/>.</summary>
        public int ZoneOwnerSlot;

        public float OwnerProgress;

        public bool Mine;
        public bool Allied;
        public bool Enemy;
        public bool Neutral;

        public float DistanceFromHero;
        public float DistanceFromMyBase;

        /// <summary>Оценка вражеской силы у точки. По разведке и памяти, а не по всеведению.</summary>
        public float EnemyForce;

        /// <summary>Своя и союзная сила рядом с точкой.</summary>
        public float FriendlyForce;

        public int GuardCount;
        public bool HasUpgradeSlot;
        public bool UpgradeChosen;
        public bool AllowsRally;

        /// <summary>Точку прямо сейчас отбивают или она заморожена спором.</summary>
        public bool UnderAttack;

        /// <summary>Сколько золота в секунду приносит владение. Ноль — точка только за очки победы.</summary>
        public float IncomeValue;

        /// <summary>Разведана ли точка. Непроверенная не значит пустая — значит «неизвестно».</summary>
        public bool Scouted;

        /// <summary>Сколько секунд назад бот последний раз видел, что тут происходит.</summary>
        public float StaleSeconds;
    }

    /// <summary>Что бот знает о другом участнике матча — живом игроке или другом боте.</summary>
    public struct BotRivalView
    {
        public int Slot;
        public bool Active;
        public bool Eliminated;

        /// <summary>Союзник по команде. Против него цели не строятся, зато строится помощь.</summary>
        public bool Allied;

        /// <summary>Оценка боевой силы армии по последним данным.</summary>
        public float ArmyPower;
        public int ArmyCount;

        public bool HeroKnown;
        public bool HeroAlive;
        public Vector3 HeroPosition;
        public float HeroHealthFraction;

        public Vector3 BasePosition;

        /// <summary>Владелец флага его базы: не он сам — значит, база уже захвачена (ГДД §10).</summary>
        public int BaseFlagOwner;

        /// <summary>Оценка силы, стоящей у его базы. По ней считается заманчивость рейда.</summary>
        public float BaseDefense;

        /// <summary>Насколько он впереди по главному критерию победы.</summary>
        public float FlagHoldSeconds;

        /// <summary>Сколько урона он нанёс боту за последнее время. Кормит злопамятность характера.</summary>
        public float RecentAggression;
    }

    /// <summary>
    /// Полный снимок мира глазами одного бота. Переиспользуемый объект: массивы живут
    /// с ботом весь матч, на 20 Гц новый снимок не аллоцируется.
    /// </summary>
    public sealed class BotSituation
    {
        public BotSituation(int pointCapacity, int rivalCapacity)
        {
            Points = new BotPointView[Mathf.Max(1, pointCapacity)];
            Rivals = new BotRivalView[Mathf.Max(1, rivalCapacity)];
        }

        public BotPointView[] Points;
        public int PointCount;

        public BotRivalView[] Rivals;
        public int RivalCount;

        // --- Своё состояние ---

        public int Slot;
        public bool HasAllies;

        public Vector3 HeroPosition;
        public bool HeroAlive;
        public float HeroHealthFraction;

        public Vector3 BasePosition;
        public float BuyZoneRadius;
        public bool AtBase;

        /// <summary>Оценка своей полевой армии в тех же единицах, что и чужая.</summary>
        public float ArmyPower;

        public int ArmyCount;
        public int GarrisonCount;
        public int QueuedCount;
        public int UnitCap;

        public int Gold;
        public int Xp;

        public bool HoldsCenter;

        /// <summary>Доля пройденного матча, 0..1. К концу цена центрального флага растёт.</summary>
        public float MatchProgress;

        /// <summary>
        /// Характерный размер карты, м: расстояние от своей базы до самой дальней точки.
        /// Все оценки близости считаются в долях от него, поэтому оценщик одинаково работает
        /// и на дуэльной арене, и на карте вчетверо больше — правки при смене карты не нужны.
        /// </summary>
        public float MapScale = 60f;

        /// <summary>Цена самого дешёвого доступного юнита. По ней видно, «есть ли вообще деньги».</summary>
        public int CheapestUnitCost = 1;

        /// <summary>Своя база под ударом: её флаг сбивают прямо сейчас.</summary>
        public bool HomeUnderAttack;

        public void EnsurePointCapacity(int count)
        {
            if (Points.Length < count)
                Points = new BotPointView[count];
        }

        public void EnsureRivalCapacity(int count)
        {
            if (Rivals.Length < count)
                Rivals = new BotRivalView[count];
        }

        /// <summary>Сколько охранников уже заказано и едет на точки. Часть общей очереди.</summary>
        public int QueuedGuardCount;

        /// <summary>
        /// Доля лимита, занятая вообще всем: армией, гарнизоном и очередью.
        /// По ней видно, есть ли ещё место под покупку (ГДД §1.1).
        /// </summary>
        public float ArmyFill
        {
            get
            {
                int cap = Mathf.Max(1, UnitCap);
                return Mathf.Clamp01((ArmyCount + GarrisonCount + QueuedCount) / (float)cap);
            }
        }

        /// <summary>
        /// Доля лимита, занятая полевой армией — тем, что реально пойдёт в бой.
        /// Считается отдельно от <see cref="ArmyFill"/> намеренно: охранники занимают тот же
        /// лимит, но с точки не сходят и в атаке не участвуют. Мерить готовность к вылазке
        /// общим лимитом значит выпускать бота в рейд с четырьмя бойцами и полным гарнизоном
        /// за спиной — формально «армия набрана», фактически идти некому.
        /// </summary>
        public float FieldFill
        {
            get
            {
                int cap = Mathf.Max(1, UnitCap);
                int queuedField = Mathf.Max(0, QueuedCount - QueuedGuardCount);

                return Mathf.Clamp01((ArmyCount + queuedField) / (float)cap);
            }
        }

        /// <summary>Данные о сопернике по слоту или пустая запись.</summary>
        public BotRivalView Rival(int slot)
        {
            for (int i = 0; i < RivalCount; i++)
            {
                if (Rivals[i].Slot == slot)
                    return Rivals[i];
            }

            return default;
        }
    }
}
