namespace Warlord.Core
{
    /// <summary>
    /// Превращает произвольный сетевой тик FishNet в фиксированный боевой такт (ГДД §12: 20 Гц).
    /// Боевой шаг всегда одинаковой длины — от этого зависит честность размена уроном.
    /// </summary>
    public sealed class FixedStepAccumulator
    {
        private const int MaxCatchUpSteps = 4;

        private float _accumulator;

        public FixedStepAccumulator(float stepDuration)
        {
            StepDuration = stepDuration > 0f ? stepDuration : 0.05f;
        }

        public float StepDuration { get; }

        /// <summary>Сколько целых боевых тактов накопилось. Догоняем не более MaxCatchUpSteps за кадр.</summary>
        public int Consume(float deltaTime)
        {
            _accumulator += deltaTime;

            int steps = 0;
            while (_accumulator >= StepDuration && steps < MaxCatchUpSteps)
            {
                _accumulator -= StepDuration;
                steps++;
            }

            // Провал по производительности не должен превращаться в лавину догоняющих тактов.
            if (_accumulator > StepDuration * MaxCatchUpSteps)
                _accumulator = 0f;

            return steps;
        }

        public void Reset() => _accumulator = 0f;
    }
}
