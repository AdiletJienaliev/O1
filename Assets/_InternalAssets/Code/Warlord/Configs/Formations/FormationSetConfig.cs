using System.Collections.Generic;
using UnityEngine;

namespace Warlord.Configs.Formations
{
    /// <summary>
    /// Набор доступных построений. Индекс в списке — сетевой id построения (byte),
    /// клавиши Q/W/E мапятся на индексы 0/1/2.
    /// </summary>
    [CreateAssetMenu(menuName = "Warlord/Formations/Set", fileName = "FormationSetConfig")]
    public sealed class FormationSetConfig : ScriptableObject
    {
        [SerializeField] private List<FormationConfig> formations = new();
        [SerializeField] private int defaultIndex;

        public IReadOnlyList<FormationConfig> Formations => formations;
        public int Count => formations.Count;
        public int DefaultIndex => Mathf.Clamp(defaultIndex, 0, Mathf.Max(0, Count - 1));

        public bool IsValidIndex(int index) => index >= 0 && index < formations.Count;

        public FormationConfig Get(int index) => IsValidIndex(index) ? formations[index] : null;

        public int IndexOf(FormationConfig formation) => formation == null ? -1 : formations.IndexOf(formation);
    }
}
