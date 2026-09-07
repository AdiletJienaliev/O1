using System.Collections.Generic;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;
using Warlord.Configs;
using Warlord.Gameplay.Match;
using Warlord.Gameplay.World;
using Warlord.Networking.Sync;

namespace Warlord.Gameplay.Units
{
    /// <summary>
    /// Репликация трансформа юнита (ГДД §12): сжатая позиция и поворот только вокруг Y,
    /// частота 15 Гц, ненадёжный канал. Юниты не предсказываются — клиент показывает
    /// интерполированное прошлое с задержкой interpolationDelay.
    /// </summary>
    public sealed class UnitTransformSync : NetworkBehaviour
    {
        private readonly struct Snapshot
        {
            public readonly float Time;
            public readonly Vector3 Position;
            public readonly float Yaw;

            public Snapshot(float time, Vector3 position, float yaw)
            {
                Time = time;
                Position = position;
                Yaw = yaw;
            }
        }

        private const int BufferCapacity = 24;

        private readonly List<Snapshot> _buffer = new(BufferCapacity);

        private TransformQuantizer _quantizer;
        private float _sendInterval = 1f / 15f;
        private float _interpolationDelay = 0.1f;
        private float _sendTimer;
        private bool _subscribed;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            NetworkConfig network = MatchManager.Instance != null ? MatchManager.Instance.Config.Network : null;

            _quantizer = new TransformQuantizer(MatchArena.Size, MatchArena.HeightRange);
            if (network != null)
            {
                _sendInterval = network.UnitSyncInterval;
                _interpolationDelay = network.interpolationDelay;
            }

        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            TimeManager.OnPostTick += ServerSendIfDue;
            _subscribed = true;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();

            if (_subscribed && TimeManager != null)
                TimeManager.OnPostTick -= ServerSendIfDue;

            _subscribed = false;
            _buffer.Clear();
        }

        private void ServerSendIfDue()
        {
            _sendTimer -= (float)TimeManager.TickDelta;
            if (_sendTimer > 0f)
                return;

            _sendTimer = _sendInterval;

            _quantizer.Encode(transform.position, out ushort x, out ushort y, out ushort z);
            byte yaw = TransformQuantizer.EncodeYaw(transform.eulerAngles.y);

            ObserversSyncTransform(x, y, z, yaw, Channel.Unreliable);
        }

        [ObserversRpc(ExcludeServer = true)]
        private void ObserversSyncTransform(ushort x, ushort y, ushort z, byte yaw, Channel channel = Channel.Unreliable)
        {
            Vector3 position = _quantizer.Decode(x, y, z);
            float yawDegrees = TransformQuantizer.DecodeYaw(yaw);

            if (_buffer.Count >= BufferCapacity)
                _buffer.RemoveAt(0);

            _buffer.Add(new Snapshot(Time.time, position, yawDegrees));
        }

        private void Update()
        {
            // На сервере (и на хосте) юнитом двигает навигация, интерполировать нечего.
            if (IsServerInitialized || _buffer.Count == 0)
                return;

            float renderTime = Time.time - _interpolationDelay;

            // Ищем пару снимков, между которыми лежит время отрисовки.
            for (int i = _buffer.Count - 1; i > 0; i--)
            {
                Snapshot newer = _buffer[i];
                Snapshot older = _buffer[i - 1];

                if (older.Time > renderTime)
                    continue;

                float span = newer.Time - older.Time;
                float t = span > 0.0001f ? Mathf.Clamp01((renderTime - older.Time) / span) : 1f;

                transform.position = Vector3.Lerp(older.Position, newer.Position, t);
                transform.rotation = Quaternion.Euler(0f, Mathf.LerpAngle(older.Yaw, newer.Yaw, t), 0f);

                // Всё, что старше использованной пары, больше не понадобится.
                if (i - 1 > 0)
                    _buffer.RemoveRange(0, i - 1);

                return;
            }

            // Снимков ещё не хватает или они все в будущем — держим последний известный.
            Snapshot latest = _buffer[_buffer.Count - 1];
            transform.position = latest.Position;
            transform.rotation = Quaternion.Euler(0f, latest.Yaw, 0f);
        }
    }
}
