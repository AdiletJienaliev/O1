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
    }
}
