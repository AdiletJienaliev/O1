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
        AttackMove = 2
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
        OutOfBounds = 10
    }
}
