using System.Collections.Generic;
using UnityEngine;

namespace Warlord.Presentation.Animation
{
    /// <summary>
    /// Реализация анимационных вызовов через Animator. Вешается на тот же объект, где лежит
    /// Animator персонажа (или на корень — контроллер найдётся в потомках).
    ///
    /// Каждый параметр проверяется на существование при старте: у разных типов юнитов набор
    /// параметров разный (щита у лучника нет), и без проверки Unity сыпала бы предупреждениями
    /// каждый кадр. Отсутствующий параметр просто не трогается — вызов остаётся холостым.
    /// </summary>
    public sealed class AnimatorCharacterAnimation : MonoBehaviour, ICharacterAnimation
    {
        [SerializeField] private Animator animator;

        [Header("Передвижение")]
        [Tooltip("Float: направление шага вбок, -1..1.")]
        [SerializeField] private string moveXParameter = "x";

        [Tooltip("Float: направление шага вперёд, -1..1.")]
        [SerializeField] private string moveYParameter = "y";

        [Tooltip("Float: доля от скорости бега, 0..1. По ней выбирается стойка, шаг или бег.")]
        [SerializeField] private string speedParameter = "run";

        [Tooltip("Bool: стоит ли на земле. Пусто — не используется.")]
        [SerializeField] private string groundedParameter = "grounded";

        [Tooltip("Bool: поднят ли щит. У юнитов без щита параметра нет — вызовы вхолостую.")]
        [SerializeField] private string defendParameter = "defend";

        [Header("Триггеры")]
        [SerializeField] private string attackTrigger = "attack";
        [SerializeField] private string jumpTrigger = "jump";
        [SerializeField] private string deathTrigger = "death";
        [SerializeField] private string respawnTrigger = "respawn";

        [Tooltip("Удар, принятый на щит.")]
        [SerializeField] private string blockTrigger = "block";

        [Header("Сглаживание")]
        [Tooltip("За сколько секунд параметр скорости догоняет новое значение. 0 — мгновенно.")]
        [Min(0f)] [SerializeField] private float speedDamp = 0.08f;

        private readonly List<int> _pendingHashes = new(4);
        private readonly List<int> _pendingFrames = new(4);

        private int _moveX;
        private int _moveY;
        private int _speed;
        private int _grounded;
        private int _defend;
        private int _attack;
        private int _block;
        private int _jump;
        private int _death;
        private int _respawn;

        private void Awake()
        {
            animator ??= GetComponent<Animator>();
            animator ??= GetComponentInChildren<Animator>(true);

            _moveX = ResolveParameter(moveXParameter, AnimatorControllerParameterType.Float);
            _moveY = ResolveParameter(moveYParameter, AnimatorControllerParameterType.Float);
            _speed = ResolveParameter(speedParameter, AnimatorControllerParameterType.Float);
            _grounded = ResolveParameter(groundedParameter, AnimatorControllerParameterType.Bool);
            _defend = ResolveParameter(defendParameter, AnimatorControllerParameterType.Bool);
            _attack = ResolveParameter(attackTrigger, AnimatorControllerParameterType.Trigger);
            _block = ResolveParameter(blockTrigger, AnimatorControllerParameterType.Trigger);
            _jump = ResolveParameter(jumpTrigger, AnimatorControllerParameterType.Trigger);
            _death = ResolveParameter(deathTrigger, AnimatorControllerParameterType.Trigger);
            _respawn = ResolveParameter(respawnTrigger, AnimatorControllerParameterType.Trigger);
        }

        /// <summary>
        /// Гашение триггеров, которые аниматор не забрал. Unity держит взведённый триггер
        /// до тех пор, пока его не съест переход, — и удар, пришедший во время предыдущего
        /// замаха, срабатывал бы сразу после его окончания. Юнит бьёт раз в секунду, замах
        /// длится дольше, и такие триггеры копились: персонаж дёргано перезапускал атаку
        /// вместо того, чтобы бежать. Триггер живёт один кадр — ровно один такт аниматора.
        /// </summary>
        private void LateUpdate()
        {
            if (animator == null || _pendingHashes.Count == 0)
                return;

            for (int i = _pendingHashes.Count - 1; i >= 0; i--)
            {
                if (_pendingFrames[i] >= Time.frameCount)
                    continue;

                animator.ResetTrigger(_pendingHashes[i]);
                _pendingHashes.RemoveAt(i);
                _pendingFrames.RemoveAt(i);
            }
        }

        /// <summary>
        /// Оси направления пишутся без сглаживания: их уже сгладил
        /// <see cref="CharacterAnimationDriver"/>, причём доворотом — так вектор остаётся
        /// единичным. Сгладить их ещё раз покомпонентно значит вернуть проход через центр
        /// двумерного дерева, где клипа нет и персонаж встаёт в T-позу.
        /// </summary>
        public void SetLocomotion(Vector2 localDirection, float speed01)
        {
            if (animator == null)
                return;

            if (_moveX != 0)
                animator.SetFloat(_moveX, localDirection.x);

            if (_moveY != 0)
                animator.SetFloat(_moveY, localDirection.y);

            if (_speed == 0)
                return;

            if (speedDamp > 0f)
                animator.SetFloat(_speed, speed01, speedDamp, Time.deltaTime);
            else
                animator.SetFloat(_speed, speed01);
        }

        public void SetGrounded(bool grounded)
        {
            if (animator != null && _grounded != 0)
                animator.SetBool(_grounded, grounded);
        }

        public void SetDefending(bool defending)
        {
            if (animator != null && _defend != 0)
                animator.SetBool(_defend, defending);
        }

        public void PlayBlockImpact() => Fire(_block);

        public void PlayJump() => Fire(_jump);

        public void PlayAttack() => Fire(_attack);

        public void PlayDeath()
        {
            // Замах, начатый в момент смерти, доигрывать незачем — и тем более нельзя
            // позволить ему выстрелить уже из позы смерти.
            DropPending();
            Fire(_death);
        }

        public void PlayRespawn()
        {
            // Триггер смерти мог остаться взведённым, если переход по нему так и не случился:
            // тогда после респавна персонаж свалился бы в позу смерти на первом же переходе.
            if (animator != null && _death != 0)
                animator.ResetTrigger(_death);

            DropPending();
            Fire(_respawn);
        }

        private void Fire(int hash)
        {
            if (animator == null || hash == 0)
                return;

            animator.SetTrigger(hash);

            _pendingHashes.Add(hash);
            _pendingFrames.Add(Time.frameCount);
        }

        private void DropPending()
        {
            if (animator == null)
                return;

            for (int i = 0; i < _pendingHashes.Count; i++)
                animator.ResetTrigger(_pendingHashes[i]);

            _pendingHashes.Clear();
            _pendingFrames.Clear();
        }

        /// <summary>
        /// Хеш параметра, если он есть в контроллере нужного типа, иначе 0.
        /// Ноль — валидный «пусто»: настоящий хеш Animator.StringToHash никогда его не даёт.
        /// </summary>
        private int ResolveParameter(string parameterName, AnimatorControllerParameterType type)
        {
            if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrEmpty(parameterName))
                return 0;

            AnimatorControllerParameter[] parameters = animator.parameters;

            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].type == type && parameters[i].name == parameterName)
                    return parameters[i].nameHash;
            }

            return 0;
        }
    }
}
