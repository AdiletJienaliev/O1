using UnityEngine;

namespace Warlord.Presentation.Vfx
{
    /// <summary>
    /// Набор боевых эффектов одним ассетом. Отдельный конфиг, а не поля на каждом префабе:
    /// эффектов четыре на всю игру, и при правке их надо менять в одном месте, а не в
    /// восьми префабах юнитов.
    /// </summary>
    [CreateAssetMenu(menuName = "Warlord/VFX Library", fileName = "VfxLibrary")]
    public sealed class VfxLibrary : ScriptableObject
    {
        [Header("Бой")]
        [Tooltip("Попадание в ближнем бою.")]
        public GameObject meleeHit;

        [Tooltip("Замах: играется на самом атакующем.")]
        public GameObject swing;

        [Tooltip("Смерть юнита или полководца.")]
        public GameObject death;

        [Header("Точки")]
        [Tooltip("Вспышка в момент захвата точки.")]
        public GameObject captureBurst;

        [Header("Общее")]
        [Tooltip("Через сколько секунд снимать эффект, если он не самоуничтожается.")]
        public float lifetime = 2.5f;

        [Tooltip("Насколько выше корня объекта бить эффектом: попадание должно читаться на груди, а не в ногах.")]
        public float hitHeight = 1.1f;

        [Tooltip("Предел одновременных эффектов. На арене до восьмидесяти юнитов, и без предела "
                 + "массовая сшибка выдаёт сотни партиклов за кадр.")]
        public int maxConcurrent = 48;
    }
}
