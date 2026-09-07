namespace Warlord.Domain.Combat
{
    /// <summary>
    /// Запас здоровья для показа. Отдельно от <see cref="ICombatTarget"/> намеренно:
    /// боевой цели максимум здоровья не нужен, а полоске над головой не нужны броня,
    /// радиус тела и участие в матрице типов.
    /// </summary>
    public interface IHealthSource
    {
        int Health { get; }
        int MaxHealth { get; }
        bool IsAlive { get; }
    }
}
