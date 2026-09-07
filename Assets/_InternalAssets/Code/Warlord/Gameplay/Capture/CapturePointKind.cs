namespace Warlord.Gameplay.Capture
{
    /// <summary>
    /// Роль точки захвата. Определяет, какой конфиг брать из GameConfig и какой обработчик
    /// награды подключать — механизм двух шкал у них общий (ГДД §9, §10).
    /// </summary>
    public enum CapturePointKind : byte
    {
        CentralFlag = 0,
        BaseFlag = 1,

        /// <summary>
        /// Аванпост (ГДД §2.4). От центра отличается двумя вещами: откатывается без гарнизона
        /// и даёт владельцу улучшение на выбор.
        /// </summary>
        Outpost = 2
    }
}
