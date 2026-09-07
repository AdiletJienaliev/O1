using System.Collections.Generic;
using Warlord.Gameplay.Units;

namespace Warlord.Gameplay.Capture
{
    /// <summary>
    /// Охранники одного игрока и их привязка к точкам (ГДД §1). Живёт на сервере рядом
    /// с <see cref="Warlord.Gameplay.Players.PlayerState"/>, как армия — но отдельным списком.
    ///
    /// Отдельный список, а не флаг внутри <see cref="Warlord.Gameplay.Army.ArmyController"/>,
    /// потому что охранник не выполняет приказов вообще. Лежи он в армии, каждое поведение
    /// приказа обязано было бы начинаться с проверки «а это не охранник ли» — и первое же
    /// новое поведение эту проверку забыло бы, а гарнизон ушёл бы в атаку через полкарты.
    /// Разделение на два списка делает такую ошибку невозможной по строению.
    ///
    /// В общий лимит армии охранники при этом входят (ГДД §1.1) — это считает сам PlayerState.
    /// </summary>
    public sealed class GarrisonRoster
    {
        /// <summary>Один охранник на своём месте: кто, где стоит и в каком слоте кольца.</summary>
        public readonly struct Post
        {
            public readonly UnitEntity Unit;
            public readonly CapturePointBehaviour Point;
            public readonly int SlotIndex;

            public Post(UnitEntity unit, CapturePointBehaviour point, int slotIndex)
            {
                Unit = unit;
                Point = point;
                SlotIndex = slotIndex;
            }
        }

        private readonly List<Post> _posts = new(16);

        public int Count => _posts.Count;

        public IReadOnlyList<Post> Posts => _posts;

        /// <summary>Сколько живых охранников игрок держит на этой точке. Для счётчика «3 / 6» в панели покупки.</summary>
        public int CountAt(CapturePointBehaviour point)
        {
            int count = 0;

            for (int i = 0; i < _posts.Count; i++)
            {
                if (_posts[i].Point == point && _posts[i].Unit != null && _posts[i].Unit.IsAlive)
                    count++;
            }

            return count;
        }

        /// <summary>
        /// Занять свободный слот кольца. Берётся наименьший свободный индекс: слот погибшего
        /// достаётся следующему купленному охраннику, а не сдвигает всех остальных (ГДД §1.7).
        /// </summary>
        public bool TryTakeSlot(CapturePointBehaviour point, int slotCount, out int slotIndex)
        {
            for (slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                if (!IsSlotTaken(point, slotIndex))
                    return true;
            }

            slotIndex = -1;
            return false;
        }

        public void Add(UnitEntity unit, CapturePointBehaviour point, int slotIndex)
        {
            if (unit == null || point == null)
                return;

            _posts.Add(new Post(unit, point, slotIndex));
        }

        public void Remove(UnitEntity unit)
        {
            for (int i = _posts.Count - 1; i >= 0; i--)
            {
                if (_posts[i].Unit == unit)
                    _posts.RemoveAt(i);
            }
        }

        /// <summary>Убирает погибших и уничтоженные объекты. Возвращает true, если состав изменился.</summary>
        public bool PurgeDead()
        {
            bool changed = false;

            for (int i = _posts.Count - 1; i >= 0; i--)
            {
                UnitEntity unit = _posts[i].Unit;

                if (unit == null || !unit.IsAlive)
                {
                    _posts.RemoveAt(i);
                    changed = true;
                }
            }

            return changed;
        }

        /// <summary>Гарнизон уничтожается вместе с остальной армией при выбывании игрока (ГДД §1.4).</summary>
        public void CollectAndClear(List<UnitEntity> destination)
        {
            for (int i = 0; i < _posts.Count; i++)
            {
                if (_posts[i].Unit != null)
                    destination.Add(_posts[i].Unit);
            }

            _posts.Clear();
        }

        private bool IsSlotTaken(CapturePointBehaviour point, int slotIndex)
        {
            for (int i = 0; i < _posts.Count; i++)
            {
                Post post = _posts[i];

                if (post.Point == point && post.SlotIndex == slotIndex && post.Unit != null && post.Unit.IsAlive)
                    return true;
            }

            return false;
        }
    }
}
