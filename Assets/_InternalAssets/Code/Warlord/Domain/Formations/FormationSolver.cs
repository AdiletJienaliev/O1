using System;
using System.Collections.Generic;
using UnityEngine;
using Warlord.Configs.Formations;

namespace Warlord.Domain.Formations
{
    /// <summary>
    /// Раскладывает отряд по слотам построения (ГДД §7).
    /// Переиспользует внутренние буферы: пересборка строя идёт раз в несколько секунд,
    /// но происходит для каждого игрока, поэтому мусор здесь недопустим.
    /// </summary>
    public sealed class FormationSolver
    {
        private readonly List<Vector3> _localSlots = new(64);
        private readonly List<IFormationMember> _ordered = new(64);
        private bool[] _taken = new bool[64];

        private static readonly Comparison<IFormationMember> PriorityComparison = static (a, b) =>
        {
            int byPriority = a.FormationPriority.CompareTo(b.FormationPriority);
            return byPriority != 0 ? byPriority : a.StableId.CompareTo(b.StableId);
        };

        /// <param name="anchorYawDegrees">Куда смотрит строй. Слоты поворачиваются вместе с якорем.</param>
        public void Solve(
            FormationConfig formation,
            Vector3 anchorPosition,
            float anchorYawDegrees,
            IReadOnlyList<IFormationMember> members)
        {
            if (formation == null || members == null || members.Count == 0)
                return;

            _ordered.Clear();
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i] != null)
                    _ordered.Add(members[i]);
            }

            if (_ordered.Count == 0)
                return;

            _ordered.Sort(PriorityComparison);

            formation.GenerateSlots(_ordered.Count, _localSlots);

            Quaternion rotation = Quaternion.Euler(0f, anchorYawDegrees, 0f);
            int count = Mathf.Min(_ordered.Count, _localSlots.Count);

            for (int i = 0; i < count; i++)
            {
                Vector3 world = anchorPosition + rotation * _localSlots[i];
                _ordered[i].AssignFormationSlot(i, world);
            }
        }

        /// <summary>
        /// Раскладка по пользовательскому пресету. От обычной отличается тем, что клетка
        /// требует конкретный тип юнита: игрок расставлял мечников и лучников, а не
        /// абстрактные места в строю, и подменять одних другими нельзя.
        ///
        /// Клетки, под которые не нашлось юнита, просто пустуют — строй сохраняет форму
        /// по мере потерь. Лишние юниты дописываются шеренгами позади пресета.
        /// </summary>
        public void SolvePreset(
            ArmyPreset preset,
            float slotSpacing,
            Vector3 anchorPosition,
            float anchorYawDegrees,
            IReadOnlyList<IFormationMember> members)
        {
            if (preset == null || preset.IsEmpty || members == null || members.Count == 0)
                return;

            _ordered.Clear();
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i] != null)
                    _ordered.Add(members[i]);
            }

            if (_ordered.Count == 0)
                return;

            _ordered.Sort(PriorityComparison);
            EnsureTakenCapacity(_ordered.Count);

            Quaternion rotation = Quaternion.Euler(0f, anchorYawDegrees, 0f);
            IReadOnlyList<ArmyPresetSlot> slots = preset.Slots;
            int assigned = 0;

            for (int i = 0; i < slots.Count; i++)
            {
                ArmyPresetSlot slot = slots[i];
                int memberIndex = TakeNextOfType(slot.RosterIndex);

                if (memberIndex < 0)
                    continue;

                Vector3 local = new(slot.Column * slotSpacing, 0f, -slot.Row * slotSpacing);
                _ordered[memberIndex].AssignFormationSlot(i, anchorPosition + rotation * local);
                assigned++;
            }

            if (assigned < _ordered.Count)
                PlaceLeftovers(preset, slotSpacing, anchorPosition, rotation, slots.Count);
        }

        /// <summary>
        /// Юниты, которым не хватило клеток в пресете. Их нельзя бросить на месте:
        /// без слота они замирают там, где родились, и отряд теряет половину состава.
        /// </summary>
        private void PlaceLeftovers(
            ArmyPreset preset,
            float slotSpacing,
            Vector3 anchorPosition,
            Quaternion rotation,
            int slotIndexOffset)
        {
            int row = preset.DeepestRow() + 1;
            int columnsPerRow = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(_ordered.Count)));
            int placed = 0;

            for (int i = 0; i < _ordered.Count; i++)
            {
                if (_taken[i])
                    continue;

                int column = placed % columnsPerRow;
                int extraRow = placed / columnsPerRow;

                float x = (column - (columnsPerRow - 1) * 0.5f) * slotSpacing;
                float z = -(row + extraRow) * slotSpacing;

                _ordered[i].AssignFormationSlot(slotIndexOffset + placed, anchorPosition + rotation * new Vector3(x, 0f, z));

                _taken[i] = true;
                placed++;
            }
        }

        /// <summary>Первый ещё не расставленный юнит нужного типа или -1.</summary>
        private int TakeNextOfType(int rosterIndex)
        {
            for (int i = 0; i < _ordered.Count; i++)
            {
                if (_taken[i] || _ordered[i].UnitTypeIndex != rosterIndex)
                    continue;

                _taken[i] = true;
                return i;
            }

            return -1;
        }

        private void EnsureTakenCapacity(int count)
        {
            if (_taken.Length < count)
                _taken = new bool[Mathf.NextPowerOfTwo(count)];

            for (int i = 0; i < count; i++)
                _taken[i] = false;
        }
    }
}
