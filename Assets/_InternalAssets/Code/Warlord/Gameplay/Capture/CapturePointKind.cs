namespace Warlord.Gameplay.Capture
{
    /// <summary>
    /// Роль точки захвата. Определяет, какой конфиг брать из GameConfig и какой обработчик
    /// награды подключать — механизм двух шкал у них общий (ГДД §9, §10).
    /// </summary>
    public enum CapturePointKind : byte
    {
        CentralFlag = 0,
        BaseFlag = 1
    }
}
