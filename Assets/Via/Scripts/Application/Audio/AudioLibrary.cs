using System;
using System.Collections.Generic;
using UnityEngine;

namespace Via.Application
{
    // Registry of every delivered clip, addressed by stable ids. The ids are the contract between
    // the AudioCues table, UI/minigame bindings and this scene asset.
    public sealed class AudioLibrary : MonoBehaviour
    {
        [Serializable]
        public sealed class Entry
        {
            public string id;
            public AudioClip clip;
            public bool loop;
        }

        [Header("Music")]
        [SerializeField] private AudioClip softMusic;
        [SerializeField] private AudioClip softEnding;
        [SerializeField] private AudioClip horrorLoop1;
        [SerializeField] private AudioClip horrorLoop2;
        [SerializeField] private AudioClip adMusic;
        [SerializeField] private AudioClip growthBed;

        [Header("Effects and ambience")]
        [SerializeField] private List<Entry> entries = new List<Entry>();

        private Dictionary<string, Entry> map;

        public AudioClip SoftMusic => softMusic;
        public AudioClip SoftEnding => softEnding;
        public AudioClip GrowthBed => growthBed;

        public AudioClip GetMusic(string id)
        {
            switch ((id ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "soft": return softMusic;
                case "soft_end": return softEnding;
                case "horror1": return horrorLoop1;
                case "horror2": return horrorLoop2;
                case "ad": return adMusic;
                case "stop": return null;
                default: return null;
            }
        }

        public bool TryGet(string id, out Entry entry)
        {
            EnsureMap();
            return map.TryGetValue(id ?? string.Empty, out entry);
        }

        public AudioClip Get(string id) => TryGet(id, out var entry) ? entry.clip : null;

        private void EnsureMap()
        {
            if (map != null) return;
            map = new Dictionary<string, Entry>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.id))
                {
                    Debug.LogError("音频库存在缺少 id 的条目。", this);
                    continue;
                }
                if (map.ContainsKey(entry.id))
                {
                    Debug.LogError("音频库 id 重复：" + entry.id, this);
                    continue;
                }
                if (entry.clip == null) Debug.LogWarning("音频库条目未绑定音频：" + entry.id, this);
                map[entry.id] = entry;
            }
        }
    }
}
