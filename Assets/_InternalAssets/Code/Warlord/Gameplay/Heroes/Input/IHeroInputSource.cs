using UnityEngine;

namespace Warlord.Gameplay.Heroes.Input
{
    /// <summary>
    /// Источник ввода полководца. Абстракция отделяет предсказание движения от того,
    /// откуда пришли кнопки: сейчас это клавиатура, дальше могут быть геймпад,
    /// перебиндивание или запись реплея — сетевой код при этом не меняется.
    /// </summary>
    public interface IHeroInputSource
    {
        /// <summary>Направление движения в осях камеры, -1..1.</summary>
        Vector2 Move { get; }

        bool Sprint { get; }

        /// <summary>Куда смотрит полководец, град. Обычно берётся от камеры.</summary>
        float AimYaw { get; }

        /// <summary>Прыжок нажат хотя бы раз с прошлого такта.</summary>
        bool ConsumeJump();

        /// <summary>Удар мечом нажат хотя бы раз с прошлого такта (ПКМ).</summary>
        bool ConsumeAttack();

        /// <summary>Приказ, выбранный клавишами 1/2/3. -1, если ничего не нажато.</summary>
        int ConsumeOrderRequest();

        /// <summary>Построение, выбранное клавишами Q/W/E. -1, если ничего не нажато.</summary>
        int ConsumeFormationRequest();

        /// <summary>Точка под курсором для приказа (ЛКМ). false, если приказ не отдавался.</summary>
        bool ConsumeOrderPoint(out Vector3 worldPoint);
    }
}
