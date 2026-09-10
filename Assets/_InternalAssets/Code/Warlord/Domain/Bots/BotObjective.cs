using UnityEngine;
using Warlord.Core;

namespace Warlord.Domain.Bots
{
    /// <summary>
    /// Одна цель бота: зачем он куда-то идёт. Точка на карте здесь — следствие,
    /// а не суть: «взять вон ту точку» и «прикрыть вон ту точку» — разные цели
    /// с одинаковыми координатами, и вести себя по дороге бот в них должен по-разному.
    /// </summary>
    public readonly struct BotObjective
    {
        public readonly BotGoalKind Kind;

        /// <summary>Индекс точки в списке матча или -1.</summary>
        public readonly int PointIndex;

        /// <summary>Слот соперника, к которому относится цель, или <see cref="PlayerSlots.None"/>.</summary>
        public readonly int TargetSlot;

        public readonly Vector3 Position;
        public readonly float Score;

        public BotObjective(BotGoalKind kind, Vector3 position, float score, int pointIndex = -1, int targetSlot = PlayerSlots.None)
        {
            Kind = kind;
            Position = position;
            Score = score;
            PointIndex = pointIndex;
            TargetSlot = targetSlot;
        }

        public static BotObjective None => new(BotGoalKind.Economy, Vector3.zero, 0f);

        /// <summary>Та же цель по смыслу: вид и адресат совпали. Оценка не в счёт — она меняется каждый такт.</summary>
        public bool SameAs(in BotObjective other)
        {
            return Kind == other.Kind && PointIndex == other.PointIndex && TargetSlot == other.TargetSlot;
        }

        /// <summary>Цель требует идти в поле, а не сидеть на базе.</summary>
        public bool IsField => Kind != BotGoalKind.Economy;

        public override string ToString()
        {
            return PointIndex >= 0
                ? $"{Kind}#{PointIndex} ({Score:0.00})"
                : $"{Kind}->{TargetSlot} ({Score:0.00})";
        }
    }
}
