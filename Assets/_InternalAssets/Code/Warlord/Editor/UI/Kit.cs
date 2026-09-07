using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Warlord.EditorTools.UI
{
    /// <summary>
    /// Доступ к ассетам GUI PRO Kit — Simple Casual. Все пути собраны здесь, чтобы
    /// переезд набора или смена темы правились в одном файле, а не по всему билдеру.
    /// Отсутствующий ассет не роняет сборку: он логируется и заменяется пустышкой.
    /// </summary>
    public static class Kit
    {
        private const string Root = "Assets/_ExternalAssets/GUI PRO Kit - Simple Casual/";

        private const string FrameDark = Root + "Sprite/Component/Frame/Frame_Demo_Dark/";
        private const string FrameCommon = Root + "Sprite/Component/Frame/Frame_Demo_Common/";
        private const string ButtonDark = Root + "Sprite/Component/Button/Button_Demo_Dark/";
        private const string ButtonCommon = Root + "Sprite/Component/Button/Button_Demo_Common/";
        private const string SliderDark = Root + "Sprite/Component/Slider/Slider_Demo_Dark/";
        private const string PopupDark = Root + "Sprite/Component/Popup/Popup_Demo_Dark/";
        private const string LabelDir = Root + "Sprite/Component/Label/";
        private const string EtcDir = Root + "Sprite/Component/UI_Etc/";
        private const string IconDir = Root + "Sprite/Demo/Demo_Icon/";
        private const string ItemIconDir = Root + "Sprite/Demo/Demo_ItemIcon_(OriginalSize)/";
        private const string BackgroundDir = Root + "Sprite/Demo/Demo_Background/";
        private const string FontDir = Root + "Fonts/";

        private static readonly List<string> Missing = new();

        #region Панели и рамки

        public static Sprite PanelLarge => Sprite(FrameDark + "BasicFrame_Rectangle01_l.png");
        public static Sprite PanelSmall => Sprite(FrameDark + "BasicFrame_Rectangle02_s_Navy.png");
        public static Sprite PanelGray => Sprite(FrameDark + "BasicFrame_Rectangle02_s_Gray.png");
        public static Sprite Banner => Sprite(FrameDark + "BasicFrame_RectangleSlant01.Png");
        public static Sprite Pill => Sprite(FrameDark + "BasicFrame_Oval_63.png");
        public static Sprite PillSmall => Sprite(FrameDark + "BasicFrame_Oval_48_Navy.png");
        public static Sprite PillGreen => Sprite(FrameDark + "BasicFrame_Oval_48_Green.png");
        public static Sprite CircleLarge => Sprite(FrameDark + "BasicFrame_Circle_129.png");
        public static Sprite CircleSmall => Sprite(FrameDark + "BasicFrame_Circle_80.png");
        public static Sprite Square => Sprite(FrameDark + "BasicFrame_Square01.png");
        public static Sprite SquareTinted => Sprite(FrameCommon + "BasicFrame_Square02_Blue.png");
        public static Sprite ListRow => Sprite(FrameDark + "ListFrame04.png");
        public static Sprite ListRowFlat => Sprite(FrameDark + "ListFrame00.png");
        public static Sprite TableRow => Sprite(FrameDark + "TableFrame05.png");
        public static Sprite ItemSlot => Sprite(FrameDark + "ItemFrame01_Frame_n_Blue1.png");
        public static Sprite ItemSlotDim => Sprite(FrameDark + "ItemFrame01_Frame_d1.png");
        public static Sprite ItemSlotSmall => Sprite(FrameDark + "ItemFrame07_n.png");
        public static Sprite Diamond => Sprite(FrameCommon + "BasicFrame_Diamond01.png");

        #endregion

        #region Кнопки

        public static Sprite ButtonBlue => Sprite(ButtonCommon + "Btn_Rectangle00_n_Blue.png");
        public static Sprite ButtonGreen => Sprite(ButtonCommon + "Btn_Rectangle00_n_Green.png");
        public static Sprite ButtonRed => Sprite(ButtonCommon + "Btn_Rectangle00_n_Red.png");
        public static Sprite ButtonGray => Sprite(ButtonCommon + "Btn_Rectangle00_n_Gray.png");
        public static Sprite ButtonOrange => Sprite(ButtonCommon + "Btn_Rectangle00_n_Orange.png");
        public static Sprite ButtonNavy => Sprite(ButtonCommon + "Btn_Rectangle00_n_Navy.png");
        public static Sprite ButtonYellow => Sprite(ButtonCommon + "Btn_Rectangle00_n_Yellow.png");
        public static Sprite ButtonCircle => Sprite(ButtonDark + "Btn_IconButton_Circle_90.png");
        public static Sprite ButtonSquare => Sprite(ButtonDark + "Btn_IconButton_Square.png");
        public static Sprite ButtonMenu => Sprite(ButtonDark + "Btn_MenuButton_Rectangle01.png");

        #endregion

        #region Полосы

        public static Sprite BarFrame => Sprite(SliderDark + "Slider07_Frame.png");
        public static Sprite BarFillGreen => Sprite(SliderDark + "Slider07_Fill_Green.png");
        public static Sprite BarFillBlue => Sprite(SliderDark + "Slider07_Fill_Blue.png");
        public static Sprite BarFillRed => Sprite(SliderDark + "Slider07_Fill_Red.png");
        public static Sprite BarFillPurple => Sprite(SliderDark + "Slider07_Fill_Purple.png");
        public static Sprite ThinBarFrame => Sprite(SliderDark + "Slider01_Frame.png");
        public static Sprite ThinBarFill => Sprite(SliderDark + "Slider01_Fill.png");
        public static Sprite WideBarFrame => Sprite(SliderDark + "Slider09_Frame.png");
        public static Sprite WideBarFill => Sprite(SliderDark + "Slider09_Fill_Orange.png");

        #endregion

        #region Попапы, метки, прочее

        public static Sprite PopupBody => Sprite(PopupDark + "Popup01_Frame1.png");
        public static Sprite PopupHeader => Sprite(PopupDark + "Popup01_Frame2_navy.png");
        public static Sprite PopupSmall => Sprite(PopupDark + "MiddlePopup00.png");
        public static Sprite TagBlue => Sprite(LabelDir + "Label_Tag01_Color_Blue.png");
        public static Sprite TagRed => Sprite(LabelDir + "Label_Tag01_Color_Red.png");
        public static Sprite TagGreen => Sprite(LabelDir + "Label_Tag01_Color_Green.png");
        public static Sprite Ribbon => Sprite(LabelDir + "Label_Ribbon00_Color_Red.png");
        public static Sprite NotifyBadge => Sprite(EtcDir + "Notify_Count_l_Red.png");
        public static Sprite InputFrame => Sprite(EtcDir + "InputField_Frame.png");
        public static Sprite InputInner => Sprite(EtcDir + "InputField_Frame_Inner.png");

        public static Sprite BackgroundGradient => Sprite(BackgroundDir + "Background_Gradatient02_Dark.png");
        public static Sprite BackgroundGlow => Sprite(BackgroundDir + "Background_ScreenGlow.png");
        public static Sprite BackgroundPattern => Sprite(BackgroundDir + "Background_pattern.png");

        #endregion

        #region Иконки

        public static Sprite IconCoin => Sprite(IconDir + "Icon_ColorIcon_Coin_m.png");
        public static Sprite IconExp => Sprite(IconDir + "Icon_WhiteIcon_Exp.png");
        public static Sprite IconTimer => Sprite(IconDir + "Icon_ColorIcon_Timer.png");
        public static Sprite IconClose => Sprite(IconDir + "Icon_WhiteIcon_Close.png");
        public static Sprite IconCrown => Sprite(IconDir + "Icon_WhiteIcon_Crown.png");
        public static Sprite IconCheck => Sprite(IconDir + "Icon_WhiteIcon_Check_l.png");
        public static Sprite IconFlag => Sprite(IconDir + "IconGroup_MenuIcon01_Flag.png");
        public static Sprite IconFriends => Sprite(IconDir + "IconGroup_MenuIcon04_friends.png");
        public static Sprite IconAttack => Sprite(IconDir + "IconGroup_StatsIcon_Attack.png");
        public static Sprite IconLife => Sprite(IconDir + "IconGroup_StatsIcon_Life.png");
        public static Sprite IconDefense => Sprite(IconDir + "IconGroup_StatsIcon_Defense.png");
        public static Sprite IconSpeed => Sprite(IconDir + "IconGroup_StatsIcon_Speed.png");
        public static Sprite IconSword => Sprite(IconDir + "IconGroup_StatsIcon_Sword.png");
        public static Sprite IconMonster => Sprite(IconDir + "IconGroup_StatsIcon_Monster.png");
        public static Sprite IconStar => Sprite(IconDir + "Icon_RankIcon_Star01_m.png");

        public static Sprite ItemSword => Sprite(ItemIconDir + "itemicon_equipment_sword.png");
        public static Sprite ItemSpear => Sprite(ItemIconDir + "itemicon_equipment_weapon_spear.png");
        public static Sprite ItemBow => Sprite(ItemIconDir + "itemicon_equipment_weapon_bow.png");
        public static Sprite ItemShield => Sprite(ItemIconDir + "itemicon_equipment_shield.png");
        public static Sprite ItemHelmet => Sprite(ItemIconDir + "itemicon_equipment_helmet.png");
        public static Sprite ItemGold => Sprite(ItemIconDir + "itemicon_coin_gold_star.png");
        public static Sprite ItemXp => Sprite(ItemIconDir + "itemicon_star_1.png");
        public static Sprite ItemFlag => Sprite(ItemIconDir + "itemicon_flag_1_clan.png");
        public static Sprite ItemHp => Sprite(ItemIconDir + "itemicon_hp.png");
        public static Sprite ItemHourglass => Sprite(ItemIconDir + "itemicon_hourglass.png");
        public static Sprite ItemTarget => Sprite(ItemIconDir + "itemicon_target_red.png");
        public static Sprite ItemCrown => Sprite(ItemIconDir + "itemicon_crown_1.png");
        public static Sprite ItemCastle => Sprite(ItemIconDir + "itemicon_castle.png");
        public static Sprite ItemSkull => Sprite(ItemIconDir + "itemicon_skull.png");
        public static Sprite ItemTrophy => Sprite(ItemIconDir + "itemicon_trophy_gold.png");

        /// <summary>Иконки юнитов по индексу ростера: мечник, копейщик, лучник, легионер.</summary>
        public static Sprite[] UnitIcons => new[] { ItemSword, ItemSpear, ItemBow, ItemShield };

        /// <summary>Иконки веток прокачки в порядке UpgradeBranch.</summary>
        public static Sprite[] BranchIcons => new[]
        {
            IconAttack, IconLife, ItemTarget, IconMonster, IconSpeed, ItemCrown
        };

        /// <summary>Иконки приказов в порядке ArmyOrderType.</summary>
        public static Sprite[] OrderIcons => new[] { IconDefense, IconFriends, IconSword, ItemShield };

        #endregion

        #region Шрифты

        public static TMP_FontAsset FontTitle => Font(FontDir + "Quicksand-Bold SDF.asset");
        public static TMP_FontAsset FontBody => Font(FontDir + "Rubik-SemiBold SDF.asset");
        public static TMP_FontAsset FontNumbers => Font(FontDir + "MuseoModerno-CriticalNum_Transpar_46 SDF.asset");

        #endregion

        public static Sprite Sprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            if (sprite == null)
                Report(path);

            return sprite;
        }

        public static TMP_FontAsset Font(string path)
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);

            if (font == null)
                Report(path);

            return font;
        }

        /// <summary>Сводка по ненайденным ассетам. Печатается один раз в конце сборки.</summary>
        public static void FlushMissingReport()
        {
            if (Missing.Count == 0)
                return;

            Debug.LogWarning("Warlord UI: не найдено ассетов набора — " + Missing.Count + ":\n" + string.Join("\n", Missing));
            Missing.Clear();
        }

        private static void Report(string path)
        {
            if (!Missing.Contains(path))
                Missing.Add(path);
        }
    }
}
