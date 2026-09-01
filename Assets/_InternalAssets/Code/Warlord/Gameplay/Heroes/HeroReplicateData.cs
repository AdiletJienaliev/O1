using FishNet.Object.Prediction;
using UnityEngine;

namespace Warlord.Gameplay.Heroes
{
    /// <summary>
    /// Ввод полководца за один такт (ГДД §12: client-side prediction + reconciliation).
    /// Только то, что реально влияет на симуляцию движения — удар мечом идёт отдельным RPC,
    /// потому что он валидируется с лаг-компенсацией и не должен переигрываться при реконсиляции.
    /// </summary>
    public struct HeroReplicateData : IReplicateData
    {
        public Vector2 Move;
        public float AimYaw;
        public bool Sprint;
        public bool Jump;

        private uint _tick;

        public HeroReplicateData(Vector2 move, float aimYaw, bool sprint, bool jump)
        {
            Move = move;
            AimYaw = aimYaw;
            Sprint = sprint;
            Jump = jump;
            _tick = 0u;
        }

        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }

    /// <summary>Состояние, которым сервер выправляет предсказание клиента.</summary>
    public struct HeroReconcileData : IReconcileData
    {
        public Vector3 Position;
        public float VerticalVelocity;
        public bool Alive;

        private uint _tick;

        public HeroReconcileData(Vector3 position, float verticalVelocity, bool alive)
        {
            Position = position;
            VerticalVelocity = verticalVelocity;
            Alive = alive;
            _tick = 0u;
        }

        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }
}
