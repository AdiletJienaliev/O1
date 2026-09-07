using System.Collections.Generic;
using UnityEngine;
using Warlord.Configs;
using Warlord.Configs.Formations;
using Warlord.Core;
using Warlord.Domain.Formations;
using Warlord.Domain.Stats;
using Warlord.Gameplay.Units;

namespace Warlord.Gameplay.Army
{
    /// <summary>
    /// Армия одного игрока: список живых юнитов, текущий приказ и построение.
    /// Чистый серверный объект — клиенту уходит только сводка через PlayerState.
    /// </summary>
    public sealed class ArmyController
    {
        private readonly List<UnitEntity> _units = new(32);
        private readonly List<IFormationMember> _members = new(32);
        private readonly FormationSolver _solver = new();
        private readonly ArmyPreset _preset = new();
        private readonly CommandConfig _command;
        private readonly FormationSetConfig _formations;

        private float _reflowTimer;
        private bool _formationDirty = true;

        public ArmyController(int ownerSlot, ArmyStatsCache stats, CommandConfig command, FormationSetConfig formations)
        {
            OwnerSlot = ownerSlot;
            Stats = stats;
            _command = command;
            _formations = formations;
            FormationIndex = formations != null ? formations.DefaultIndex : 0;
            Order = ArmyOrder.HoldAt(Vector3.zero, 0f);
        }

        public int OwnerSlot { get; }
        public ArmyStatsCache Stats { get; }
        public ArmyOrder Order { get; private set; }
        public int FormationIndex { get; private set; }
        public IArmyLeader Leader { get; set; }
        public IReadOnlyList<UnitEntity> Units => _units;
        public int AliveCount => _units.Count;

        public FormationConfig Formation => _formations != null ? _formations.Get(FormationIndex) : null;

        /// <summary>Пользовательская расстановка. Пустая, пока игрок ничего не задал.</summary>
        public ArmyPreset Preset => _preset;

        /// <summary>
        /// Принять пресет от владельца. Валидацию делает вызывающий: сюда данные
        /// доходят уже разобранными, а строй пересобирается со следующего такта.
        /// </summary>
        public void ApplyPreset(ArmyPreset source)
        {
            _preset.Clear();

            if (source != null)
            {
                for (int i = 0; i < source.Slots.Count; i++)
                    _preset.Add(source.Slots[i]);
            }

            _formationDirty = true;
        }

        /// <summary>
        /// Есть ли вокруг полководца кого бить. Считает <see cref="ArmyEngagementSystem"/>
        /// один раз на всю армию — юниты не ищут врагов сами, иначе передний край
        /// утаскивал бы за собой весь строй (ГДД §6).
        /// </summary>
        public bool IsEngaged { get; private set; }

        /// <summary>Центр зоны боя: позиция полководца, а при его смерти — точка приказа.</summary>
        public Vector3 EngagementCenter { get; private set; }

        public float EngagementRadius { get; private set; }

        public void SetEngagement(bool engaged, Vector3 center, float radius)
        {
            IsEngaged = engaged;
            EngagementCenter = center;
            EngagementRadius = radius;
        }

        public void Add(UnitEntity unit)
        {
            if (unit == null || _units.Contains(unit))
                return;

            _units.Add(unit);
            _formationDirty = true;
        }

        public void Remove(UnitEntity unit)
        {
            if (unit != null && _units.Remove(unit))
                _formationDirty = true;
        }

        public void SetOrder(in ArmyOrder order)
        {
            Order = order;
            _formationDirty = true;
        }

        public void SetFormation(int formationIndex)
        {
            if (_formations == null || !_formations.IsValidIndex(formationIndex))
                return;

            FormationIndex = formationIndex;
            _formationDirty = true;
        }

        /// <summary>Текущий якорь построения: полководец при «За мной», иначе точка приказа.</summary>
        public void ResolveAnchor(out Vector3 position, out float yaw)
        {
            if (Order.Type == ArmyOrderType.FollowLeader && Leader != null && Leader.IsAlive)
            {
                yaw = Leader.YawDegrees;

                // Строй встаёт позади полководца, чтобы не толкать его в спину.
                float offset = _command != null ? _command.followOffset : 3f;
                Vector3 back = Quaternion.Euler(0f, yaw, 0f) * Vector3.back * offset;
                position = Leader.Position + back;
                return;
            }

            position = Order.AnchorPosition;
            yaw = Order.AnchorYaw;
        }

        /// <summary>
        /// Пересборка слотов. При смерти юнита строй не перестраивается сразу —
        /// раз в formationReflowInterval, чтобы армия не «дышала» (ГДД §7).
        /// </summary>
        public void TickFormation(float deltaTime)
        {
            _reflowTimer -= deltaTime;

            bool followsLeader = Order.Type == ArmyOrderType.FollowLeader;
            bool needsReflow = _formationDirty || _reflowTimer <= 0f;

            // При «За мной» якорь едет вместе с полководцем, поэтому слоты нужны каждый такт.
            if (!followsLeader && !needsReflow)
                return;

            if (needsReflow)
            {
                float interval = _command != null ? _command.formationReflowInterval : 3f;
                _reflowTimer = interval;
                _formationDirty = false;
            }

            RebuildSlots();
        }

        private void RebuildSlots()
        {
            FormationConfig formation = Formation;
            if (formation == null || _units.Count == 0)
                return;

            ResolveAnchor(out Vector3 anchor, out float yaw);

            _members.Clear();
            for (int i = 0; i < _units.Count; i++)
            {
                UnitEntity unit = _units[i];
                if (unit != null && unit.IsAlive)
                    _members.Add(unit);
            }

            // Пресет — то же построение с точки зрения сети и HUD, но форму задаёт игрок,
            // а не ассет, поэтому раскладку считает отдельный проход решателя.
            if (formation is PresetFormationConfig && !_preset.IsEmpty)
                _solver.SolvePreset(_preset, formation.slotSpacing, anchor, yaw, _members);
            else
                _solver.Solve(formation, anchor, yaw, _members);
        }

        /// <summary>Уничтожение армии при выбывании игрока (ГДД §10.3). Возвращает копию списка для деспавна.</summary>
        public void CollectAndClear(List<UnitEntity> destination)
        {
            destination.Clear();
            destination.AddRange(_units);
            _units.Clear();
            _formationDirty = true;
        }

        public void PurgeDead()
        {
            for (int i = _units.Count - 1; i >= 0; i--)
            {
                UnitEntity unit = _units[i];
                if (unit == null || !unit.IsAlive)
                {
                    _units.RemoveAt(i);
                    _formationDirty = true;
                }
            }
        }
    }
}
