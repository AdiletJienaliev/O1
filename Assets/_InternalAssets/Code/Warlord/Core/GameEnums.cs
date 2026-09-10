namespace Warlord.Core
{
    /// <summary>Фаза матча. Синкается как byte, поэтому порядок значений менять нельзя.</summary>
    public enum MatchPhase : byte
    {
        None = 0,
        Lobby = 1,
        Countdown = 2,
        Running = 3,
        Finished = 4
    }

    /// <summary>Приказ армии (ГДД §6). Значение = индекс клавиши 1/2/3.</summary>
    public enum ArmyOrderType : byte
    {
        /// <summary>«Стоять»: держат слоты, бьют вошедших в defendRadius, не уходят дальше leashDistance.</summary>
        HoldGround = 0,
        /// <summary>«За мной»: якорь построения привязан к полководцу, отвечают только в ответ на удар.</summary>
        FollowLeader = 1,
        /// <summary>«В атаку»: ищут ближайшую цель в attackAggroRadius, иначе идут к точке приказа.</summary>
        AttackMove = 2,
        /// <summary>
        /// «Защита»: встают в слоты, поднимают щиты и не сходят с места. Пока щит поднят,
        /// удар во фронтальный сектор гасится, зато во фланг и в спину проходит полностью.
        /// </summary>
        Defend = 3
    }

    /// <summary>
    /// Походка юнита. Определяет и скорость, и то, как он держит корпус: на бегу разворачивается
    /// по движению, на шаге сохраняет направление взгляда и переступает боком.
    /// </summary>
    public enum Gait : byte
    {
        Walk = 0,
        Run = 1
    }

    /// <summary>Как полководец разворачивается при движении (ГДД §8).</summary>
    public enum HeroRotationMode : byte
    {
        /// <summary>
        /// Разворачивается в сторону движения. Обычная схема от третьего лица:
        /// A и D — шаг влево и вправо, тело доворачивается следом.
        /// </summary>
        FaceMovement = 0,

        /// <summary>
        /// Всегда смотрит туда же, куда камера, A и D дают стрейф.
        /// Схема с постоянным прицеливанием.
        /// </summary>
        FaceCamera = 1
    }

    /// <summary>Ветки древа прокачки (ГДД §11). Hero зарезервирована на будущее.</summary>
    public enum UpgradeBranch : byte
    {
        Damage = 0,
        Health = 1,
        AttackRange = 2,
        UnitCap = 3,
        MoveSpeed = 4,
        Hero = 5
    }

    /// <summary>Способ применения модификатора прокачки к базовому стату.</summary>
    public enum ModifierType : byte
    {
        Multiplicative = 0,
        Flat = 1
    }

    /// <summary>Состояние точки захвата (ГДД §9).</summary>
    public enum CaptureStatus : byte
    {
        /// <summary>Ничья точка, никто не набирает.</summary>
        Neutral = 0,
        /// <summary>Ничья точка, претендент набирает свою шкалу.</summary>
        Capturing = 1,
        /// <summary>Точка захвачена, шкала владельца 100%.</summary>
        Owned = 2,
        /// <summary>Чужой полководец обнуляет шкалу владельца.</summary>
        Losing = 3,
        /// <summary>В зоне полководцы двух и более игроков — всё заморожено.</summary>
        Contested = 4
    }

    /// <summary>Тип награды точки захвата: непрерывный доход или разовое событие.</summary>
    public enum CaptureRewardType : byte
    {
        Continuous = 0,
        OneShot = 1
    }

    /// <summary>Правила тай-брейка при равном времени удержания (ГДД §2).</summary>
    public enum TiebreakRule : byte
    {
        FlagHoldTime = 0,
        FlagCaptures = 1,
        BasesCaptured = 2,
        UnitKills = 3
    }

    /// <summary>Кто именно является боевой сущностью — влияет на правила захвата и таргетинг.</summary>
    public enum CombatantKind : byte
    {
        Hero = 0,
        Unit = 1
    }

    /// <summary>Почему игрок выбыл из матча.</summary>
    public enum EliminationReason : byte
    {
        None = 0,
        BaseCaptured = 1,
        Disconnected = 2
    }

    /// <summary>
    /// Улучшение аванпоста (ГДД §2.5). Синкается как byte в паре с индексом в наборе,
    /// поэтому порядок значений менять нельзя.
    /// </summary>
    public enum OutpostUpgradeType : byte
    {
        /// <summary>«Снабжение»: доход и место под гарнизон. Выбор по умолчанию.</summary>
        Supply = 0,
        /// <summary>«Кузница»: быстрее постройка и лечение вокруг точки.</summary>
        Forge = 1,
        /// <summary>«Наёмники»: новый юнит в панели покупки и дешёвые живучие охранники.</summary>
        Mercenaries = 2
    }

    /// <summary>
    /// Кто занимает слот матча. Синкается как byte в составе строки лобби,
    /// поэтому порядок значений менять нельзя.
    /// </summary>
    public enum SlotKind : byte
    {
        Empty = 0,
        Human = 1,
        Bot = 2
    }

    /// <summary>
    /// Сложность бота. Задаёт только уровень исполнения — скорость реакции, качество
    /// микроконтроля, честность обзора. Стиль игры задаётся отдельно, характером:
    /// «сложный трус» и «лёгкий агрессор» обязаны быть выразимы.
    /// </summary>
    public enum BotDifficulty : byte
    {
        Easy = 0,
        Normal = 1,
        Hard = 2,
        Brutal = 3
    }

    /// <summary>Что бот знает о карте (ГДД §12: сервер видит всё, поэтому «незнание» нужно моделировать явно).</summary>
    public enum BotVisionMode : byte
    {
        /// <summary>Видит только то, что видят его юниты, полководец и удерживаемые точки. Остальное — по памяти.</summary>
        Honest = 0,

        /// <summary>Плюс всё, что видят союзники. Командная разведка.</summary>
        Shared = 1,

        /// <summary>Видит всё. Честная поблажка верхней сложности, а не тихий чит: включается только явно.</summary>
        Omniscient = 2
    }

    /// <summary>
    /// Что бот делает прямо сейчас. Список намеренно короткий и не привязан ни к карте,
    /// ни к конкретным юнитам: цель — это «зачем», а «куда именно» приходит отдельным
    /// параметром из живого состояния матча.
    /// </summary>
    public enum BotGoalKind : byte
    {
        /// <summary>Сидеть на базе и копить: покупки, прокачка, ожидание армии.</summary>
        Economy = 0,

        /// <summary>Взять или удержать точку захвата.</summary>
        Capture = 1,

        /// <summary>Прикрыть свою точку, к которой идёт враг.</summary>
        Defend = 2,

        /// <summary>Ударить по чужой базе, минуя центр (ГДД §10: захват базы выбивает игрока).</summary>
        Raid = 3,

        /// <summary>Догнать и убить чужого полководца.</summary>
        Hunt = 4,

        /// <summary>Отойти к своим и подлечиться.</summary>
        Retreat = 5,

        /// <summary>Прийти на помощь союзнику.</summary>
        Support = 6
    }

    /// <summary>Результат серверной валидации клиентской команды.</summary>
    public enum CommandRejection : byte
    {
        None = 0,
        MatchNotRunning = 1,
        PlayerEliminated = 2,
        NotInBuyZone = 3,
        NotEnoughGold = 4,
        ArmyLimitReached = 5,
        NotEnoughXp = 6,
        UpgradeUnavailable = 7,
        UnknownUnit = 8,
        UnknownFormation = 9,
        OutOfBounds = 10,
        /// <summary>На точке уже стоит максимум охранников этого игрока (ГДД §1.6).</summary>
        GarrisonFull = 11,
        /// <summary>Точка не принадлежит игроку: ни охранника купить, ни улучшение выбрать.</summary>
        PointNotOwned = 12,
        /// <summary>У точки нет слота улучшения или выбор уже сделан.</summary>
        UpgradeSlotUnavailable = 13
    }
}
