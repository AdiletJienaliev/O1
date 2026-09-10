using System.Collections.Generic;
using UnityEngine;

namespace Warlord.Presentation.Vfx
{
    /// <summary>
    /// Пул боевых эффектов. Instantiate/Destroy на каждое попадание в игре, где сходятся
    /// две армии по двадцать юнитов, даёт заметный мусор на каждом кадре сшибки — поэтому
    /// эффекты переиспользуются и ограничены счётчиком.
    ///
    /// Пул чисто клиентский и статический: эффекты ничего не решают, их незачем
    /// синхронизировать и незачем привязывать к жизненному циклу сцены.
    /// </summary>
    public static class VfxPool
    {
        private sealed class Entry
        {
            public GameObject Instance;
            public GameObject Prefab;
            public float ReleaseAt;
        }

        /// <summary>
        /// Временный рубильник: эффекты выключены целиком, ни один партикл не создаётся.
        /// Вернуть true, когда VFX снова понадобятся.
        /// </summary>
        private static readonly bool Enabled = false;

        private static readonly Dictionary<GameObject, Stack<GameObject>> Idle = new();
        private static readonly List<Entry> Live = new(64);

        private static Transform _root;
        private static int _liveLimit = 48;

        /// <summary>Сбрасывает пул между матчами: объекты сцены после выгрузки уже мертвы.</summary>
        public static void Reset()
        {
            Idle.Clear();
            Live.Clear();
            _root = null;
        }

        public static void Play(GameObject prefab, Vector3 position, Quaternion rotation, float lifetime, int limit)
        {
            if (!Enabled || prefab == null)
                return;

            _liveLimit = Mathf.Max(4, limit);

            Collect();

            if (Live.Count >= _liveLimit)
                return;

            GameObject instance = Rent(prefab);

            if (instance == null)
                return;

            instance.transform.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);

            foreach (ParticleSystem particles in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                particles.Clear(true);
                particles.Play(true);
            }

            Live.Add(new Entry
            {
                Instance = instance,
                Prefab = prefab,
                ReleaseAt = Time.time + Mathf.Max(0.1f, lifetime),
            });
        }

        private static GameObject Rent(GameObject prefab)
        {
            if (Idle.TryGetValue(prefab, out Stack<GameObject> stack))
            {
                while (stack.Count > 0)
                {
                    GameObject pooled = stack.Pop();

                    // Между матчами сцена выгружается, и в стеке остаются уничтоженные объекты.
                    if (pooled != null)
                        return pooled;
                }
            }

            GameObject created = Object.Instantiate(prefab, Root());
            created.SetActive(false);
            return created;
        }

        private static void Collect()
        {
            for (int i = Live.Count - 1; i >= 0; i--)
            {
                Entry entry = Live[i];

                if (Time.time < entry.ReleaseAt && entry.Instance != null)
                    continue;

                Live.RemoveAt(i);

                if (entry.Instance == null)
                    continue;

                entry.Instance.SetActive(false);
                entry.Instance.transform.SetParent(Root(), false);

                if (!Idle.TryGetValue(entry.Prefab, out Stack<GameObject> stack))
                {
                    stack = new Stack<GameObject>();
                    Idle[entry.Prefab] = stack;
                }

                stack.Push(entry.Instance);
            }
        }

        private static Transform Root()
        {
            if (_root != null)
                return _root;

            var holder = new GameObject("--- VFX Pool ---");
            Object.DontDestroyOnLoad(holder);
            _root = holder.transform;

            return _root;
        }
    }
}
