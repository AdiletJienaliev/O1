namespace Warlord.EditorTools.UI
{
    /// <summary>
    /// Единая сетка колонок экрана лобби. Живёт отдельно, потому что колонки ставят два
    /// разных сборщика: базовый экран собирает <c>WarlordUiBuilder</c>, а список участников
    /// и чат дописывает <c>WarlordSteamUi</c> из другой сборки. Пока ширины были зашиты
    /// в оба места по отдельности, панели вставали вплотную и налезали друг на друга.
    ///
    /// Расчёт для эталонных 1920×1080, начало координат в центре экрана:
    /// поля по 40, три промежутка по 24, остаток 1768 делится между четырьмя колонками.
    /// </summary>
    public static class LobbyColumns
    {
        public const float ScreenWidth = 1920f;
        public const float Margin = 40f;
        public const float Gutter = 24f;

        public const float MembersWidth = 300f;

        // Колонка слотов шире колонки настроек: в строке слота теперь живут ещё и
        // состав комнаты — команда, характер и сложность бота, — а настроек в боковой
        // панели всего две. Сумма ширин прежняя, иначе панели Steam встали бы внахлёст.
        public const float PlayersWidth = 720f;
        public const float SideWidth = 420f;
        public const float ChatWidth = 328f;

        /// <summary>Ширина самой строки слота. Меньше колонки на поля панели.</summary>
        public const float SlotWidth = PlayersWidth - 50f;

        public const float Height = 480f;
        public const float CenterY = 10f;

        const float Left = -ScreenWidth / 2f + Margin;

        /// <summary>
        /// Смещение колонки участников от левого края экрана (якорь Left).
        /// У якоря Left пивот стоит на левом краю прямоугольника, поэтому значение —
        /// это положение самого края панели, а не её центра.
        /// </summary>
        public const float MembersOffsetFromLeft = Margin;

        /// <summary>Центр колонки слотов в координатах от центра экрана.</summary>
        public const float PlayersX = Left + MembersWidth + Gutter + PlayersWidth / 2f;

        /// <summary>Центр колонки настроек в координатах от центра экрана.</summary>
        public const float SideX = Left + MembersWidth + Gutter + PlayersWidth + Gutter + SideWidth / 2f;

        /// <summary>
        /// Смещение колонки чата от правого края экрана (якорь Right, значение отрицательное).
        /// Пивот якоря Right стоит на правом краю прямоугольника — задаём положение края.
        /// </summary>
        public const float ChatOffsetFromRight = -Margin;
    }
}
