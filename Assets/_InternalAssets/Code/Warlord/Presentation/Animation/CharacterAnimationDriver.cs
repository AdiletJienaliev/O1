using UnityEngine;

namespace Warlord.Presentation.Animation
{
    /// <summary>
    /// Мост между геймплеем и <see cref="ICharacterAnimation"/>. Обычный класс, а не компонент:
    /// его заводит у себя тот, кто и так живёт на персонаже (полководец, юнит), поэтому
    /// добавлять что-то в префабы ради анимаций не нужно.
    ///
    /// Скорость драйвер умеет брать двумя способами. Там, где персонажа ведёт своя механика
    /// (мотор полководца у владельца, NavMeshAgent на сервере), скорость известна точно.
    /// У остальных персонажа двигает сеть, и единственный источник — смещение трансформа.
    /// </summary>
    public sealed class CharacterAnimationDriver
    {
        /// <summary>Смещение меньше этого считаем шумом, а не движением: около полсантиметра.</summary>
        private const float MinMoveSqr = 0.000025f;

        /// <summary>Сколько ждать нового снимка, прежде чем признать персонажа стоящим.</summary>
        private const float StopTimeout = 0.2f;

        private readonly ICharacterAnimation _animation;
        private readonly Transform _body;

        private Vector3 _lastSample;
        private Vector3 _networkVelocity;
        private float _sinceSample;
        private bool _hasSample;

        private bool _alive = true;
        private float _referenceSpeed = 1f;

        private Vector2 _direction = Vector2.up;
        private float _speed01;

        /// <summary>Скорость доворота осей blend tree, град/с.</summary>
        private const float DirectionTurnSpeed = 720f;

        /// <summary>За сколько секунд параметр скорости догоняет новое значение.</summary>
        private const float SpeedSmoothing = 0.12f;

        /// <param name="owner">Объект персонажа: реализация ищется на нём и в потомках.</param>
        /// <param name="body">Трансформ, в осях которого считается направление шага.</param>
        public CharacterAnimationDriver(GameObject owner, Transform body)
        {
            _animation = owner != null ? owner.GetComponentInChildren<ICharacterAnimation>(true) : null;
            _body = body;
        }

        /// <summary>Есть ли кому проигрывать анимации. Позволяет не считать скорость впустую.</summary>
        public bool IsBound => _animation != null;

        /// <summary>Скорость, которая считается «полным ходом» — по ней нормируются параметры.</summary>
        public void SetReferenceSpeed(float speed)
        {
            _referenceSpeed = speed > 0.01f ? speed : 1f;
        }

        /// <summary>Кадр персонажа, скорость которого известна точно.</summary>
        public void Tick(Vector3 planarVelocity, float deltaTime)
        {
            if (_animation == null || _body == null)
                return;

            _lastSample = _body.position;
            _sinceSample = 0f;
            _hasSample = true;

            Apply(planarVelocity, deltaTime);
        }

        /// <summary>
        /// Кадр персонажа, которого двигает сеть. Скорость считается между реальными
        /// изменениями трансформа, а не за каждый кадр: снимки приходят реже кадров, и
        /// покадровая дельта в промежутке равна нулю — от такой оценки параметр скорости
        /// мигал бы между бегом и стойкой на каждом втором кадре.
        /// </summary>
        public void TickFromTransform(float deltaTime)
        {
            if (_animation == null || _body == null)
                return;

            Vector3 position = _body.position;

            if (!_hasSample)
            {
                _lastSample = position;
                _sinceSample = 0f;
                _hasSample = true;

                Apply(Vector3.zero, deltaTime);
                return;
            }

            _sinceSample += deltaTime;

            Vector3 delta = position - _lastSample;
            delta.y = 0f;

            if (delta.sqrMagnitude > MinMoveSqr)
            {
                _networkVelocity = delta / Mathf.Max(_sinceSample, 0.0001f);
                _lastSample = position;
                _sinceSample = 0f;
            }
            else if (_sinceSample > StopTimeout)
            {
                // Снимков давно нет и позиция не менялась — персонаж действительно стоит.
                _networkVelocity = Vector3.zero;
            }

            Apply(_networkVelocity, deltaTime);
        }

        public void PlayAttack() => _animation?.PlayAttack();

        public void PlayBlockImpact() => _animation?.PlayBlockImpact();

        public void SetDefending(bool defending) => _animation?.SetDefending(defending);

        public void PlayJump() => _animation?.PlayJump();

        public void SetGrounded(bool grounded) => _animation?.SetGrounded(grounded);

        /// <summary>
        /// Смерть и возвращение в строй одним вызовом: драйвер сам ловит смену состояния,
        /// поэтому вызывающему достаточно каждый кадр отдавать текущее «жив».
        /// </summary>
        public void SetAlive(bool alive)
        {
            if (_animation == null || alive == _alive)
                return;

            _alive = alive;

            if (alive)
                _animation.PlayRespawn();
            else
                _animation.PlayDeath();
        }

        /// <summary>Сброс истории после телепорта: иначе рывок посчитается бегом на сотне м/с.</summary>
        public void ResetMotion()
        {
            _hasSample = false;
            _networkVelocity = Vector3.zero;
            _sinceSample = 0f;
            _speed01 = 0f;
        }

        /// <summary>
        /// Направление и величина разводятся здесь. Направление — единичный вектор в осях тела:
        /// им двумерный blend tree выбирает, каким боком персонаж переступает. Величина —
        /// доля от скорости бега: ею одномерный tree выбирает стойку, шаг или бег.
        ///
        /// Направление сглаживается доворотом, а не по каждой оси отдельно, и это принципиально.
        /// Покомпонентное сглаживание при развороте на 180° ведёт вектор из (0,1) в (0,-1)
        /// через (0,0) — то есть через центр двумерного дерева, где клипа нет. Аниматор в этой
        /// точке не может собрать ни одного веса, и персонаж на пару кадров встаёт в T-позу.
        /// Доворот держит вектор единичным всегда и через центр не проходит никогда.
        /// </summary>
        private void Apply(Vector3 planarVelocity, float deltaTime)
        {
            planarVelocity.y = 0f;

            float target01 = Mathf.Clamp01(planarVelocity.magnitude / _referenceSpeed);

            Vector3 local = _body.InverseTransformDirection(planarVelocity);
            Vector2 planar = new(local.x, local.z);

            // На остановке направление не пересчитывается, а замирает последнее: иначе в момент
            // торможения оси схлопывались бы и персонаж вставал бы лицом не туда, куда только что шёл.
            if (planar.sqrMagnitude > 0.0001f)
                _direction = RotateTowards(_direction, planar.normalized, DirectionTurnSpeed * deltaTime);

            _speed01 = SpeedSmoothing > 0f
                ? Mathf.Lerp(_speed01, target01, 1f - Mathf.Exp(-deltaTime / SpeedSmoothing))
                : target01;

            _animation.SetLocomotion(_direction, _speed01);
        }

        /// <summary>Доворот единичного вектора к цели не более чем на заданный угол.</summary>
        private static Vector2 RotateTowards(Vector2 from, Vector2 to, float maxDegrees)
        {
            float delta = Vector2.SignedAngle(from, to);
            float step = Mathf.Clamp(delta, -maxDegrees, maxDegrees) * Mathf.Deg2Rad;

            float cos = Mathf.Cos(step);
            float sin = Mathf.Sin(step);

            Vector2 rotated = new(
                from.x * cos - from.y * sin,
                from.x * sin + from.y * cos);

            return rotated.normalized;
        }
    }
}
