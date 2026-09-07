using UnityEngine;
using Warlord.Configs;
using Warlord.Core;

namespace Warlord.Gameplay.Heroes
{
    /// <summary>
    /// Перемещение полководца. Считается целиком локально, у владельца, с частотой кадров —
    /// поэтому движение ровно настолько плавное, насколько плавно рисуется игра.
    /// Остальным позиция уезжает готовой через NetworkTransform (ГДД §12): никакого
    /// пересчёта на чужой стороне нет, а значит нечему и дёргаться.
    ///
    /// Обычный класс, а не компонент: у полководца уже есть <see cref="HeroController"/>,
    /// который им владеет, и лишний MonoBehaviour в префабе ничего бы не дал.
    /// </summary>
    public sealed class HeroMotor
    {
        private readonly CharacterController _controller;
        private readonly Transform _body;

        private HeroConfig _config;
        private Vector3 _planarVelocity;
        private float _verticalVelocity;
        private bool _jumped;

        public HeroMotor(CharacterController controller, Transform body)
        {
            _controller = controller;
            _body = body;
        }

        /// <summary>Горизонтальная скорость, м/с. Её же читает анимация.</summary>
        public Vector3 PlanarVelocity => _planarVelocity;

        /// <summary>Полный ход: по нему нормируются параметры анимации.</summary>
        public float MaxSpeed => _config != null ? Mathf.Max(_config.moveSpeed, _config.sprintSpeed) : 1f;

        public bool IsGrounded => _controller != null && _controller.enabled && _controller.isGrounded;

        public bool IsConfigured => _config != null;

        public void Configure(HeroConfig config) => _config = config;

        /// <summary>Прыжок случился с прошлого кадра. Считывается ровно один раз — для анимации.</summary>
        public bool ConsumeJumped()
        {
            bool value = _jumped;
            _jumped = false;
            return value;
        }

        /// <summary>
        /// Шаг движения. <paramref name="cameraYaw"/> — азимут камеры: W уводит от камеры
        /// вглубь сцены, S ведёт на камеру, A и D дают строго вбок от направления обзора.
        /// </summary>
        public void Move(Vector2 input, float cameraYaw, bool sprint, bool jump, float deltaTime)
        {
            if (_config == null || _controller == null || !_controller.enabled || deltaTime <= 0f)
                return;

            ApplyGravity(jump, deltaTime);

            Vector3 wish = Quaternion.Euler(0f, cameraYaw, 0f) * new Vector3(input.x, 0f, input.y);

            // По диагонали скорость расти не должна, но короткие отклонения стика обязаны сохраниться.
            float amount = wish.magnitude;
            if (amount > 1f)
            {
                wish /= amount;
                amount = 1f;
            }

            Vector3 target = wish * (sprint ? _config.sprintSpeed : _config.moveSpeed);

            // Разгон считается по времени, а не по кадрам. Клавиатура даёт ровно 0 или 1,
            // и без этого каждое нажатие било бы полной скоростью в первый же кадр.
            float rate = target.sqrMagnitude >= _planarVelocity.sqrMagnitude
                ? _config.moveAcceleration
                : _config.moveDeceleration;

            _planarVelocity = rate > 0f
                ? Vector3.MoveTowards(_planarVelocity, target, rate * deltaTime)
                : target;

            Vector3 motion = _planarVelocity;
            motion.y = _verticalVelocity;
            _controller.Move(motion * deltaTime);

            ApplyRotation(wish, amount, cameraYaw, deltaTime);
        }

        /// <summary>
        /// Такт мёртвого полководца: только падение. Двигать CharacterController всё равно надо,
        /// иначе его коллайдер зависает в устаревшем состоянии до следующего Move.
        /// </summary>
        public void Fall(float deltaTime)
        {
            if (_controller == null || !_controller.enabled || deltaTime <= 0f)
                return;

            _planarVelocity = Vector3.zero;

            if (_config != null)
                ApplyGravity(false, deltaTime);
            else
                _verticalVelocity = -2f;

            _controller.Move(new Vector3(0f, _verticalVelocity, 0f) * deltaTime);
        }

        /// <summary>Перенос на респавне. Скорости обнуляются, чтобы тело не «донесло» старый шаг.</summary>
        public void Teleport(Vector3 position)
        {
            _planarVelocity = Vector3.zero;
            _verticalVelocity = 0f;

            if (_body == null)
                return;

            // CharacterController обязательно выключить перед переносом, иначе физика
            // останется в старой позиции до следующего Move и вернёт тело обратно.
            bool wasEnabled = _controller != null && _controller.enabled;

            if (wasEnabled)
                _controller.enabled = false;

            _body.position = position;

            if (wasEnabled)
                _controller.enabled = true;
        }

        private void ApplyGravity(bool jump, float deltaTime)
        {
            float gravity = Mathf.Abs(Physics.gravity.y) * _config.gravityScale;

            _verticalVelocity -= gravity * deltaTime;

            if (_verticalVelocity < -_config.terminalVelocity)
                _verticalVelocity = -_config.terminalVelocity;

            // Небольшой прижим к земле: с нулём CharacterController теряет контакт на склонах.
            if (_controller.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f;

            if (!jump || !_controller.isGrounded)
                return;

            // v = sqrt(2 * g * h) — прыжок задаётся высотой, а не силой: так проще балансировать.
            _verticalVelocity = Mathf.Sqrt(2f * gravity * _config.jumpHeight);
            _jumped = true;
        }

        /// <summary>
        /// Доворот тела. Отделён от перемещения намеренно: направление шага и направление
        /// взгляда — разные вещи, и режим их связи задаётся конфигом (ГДД §8).
        /// </summary>
        private void ApplyRotation(Vector3 direction, float inputAmount, float cameraYaw, float deltaTime)
        {
            Vector3 facing;

            if (_config.rotationMode == HeroRotationMode.FaceCamera)
            {
                facing = Quaternion.Euler(0f, cameraYaw, 0f) * Vector3.forward;
            }
            else
            {
                // Клавиши отпущены — сохраняем текущий разворот. Иначе полководец
                // дёргался бы к направлению последнего кадра при каждой остановке.
                if (inputAmount < 0.01f)
                    return;

                facing = direction;
            }

            facing.y = 0f;
            if (facing.sqrMagnitude < 0.0001f)
                return;

            Quaternion target = Quaternion.LookRotation(facing);

            _body.rotation = _config.turnSpeed > 0f
                ? Quaternion.RotateTowards(_body.rotation, target, _config.turnSpeed * deltaTime)
                : target;
        }
    }
}
