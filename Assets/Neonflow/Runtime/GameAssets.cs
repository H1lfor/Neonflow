using System;
using UnityEngine;

namespace Neonflow
{
    [Serializable]
    public class AssetLink
    {
        public string key;
        public UnityEngine.Object value;
    }

    [Serializable]
    public class TrackDuration
    {
        public string file;
        public float seconds;
    }

    public class GameAssets : ScriptableObject
    {
        public AssetLink[] links;
        public TrackDuration[] durations;
        public float Duration(string file) => Array.Find(durations ?? Array.Empty<TrackDuration>(), d => d.file == file)?.seconds ?? 0;
        public T Get<T>(string key)
            where T : UnityEngine.Object
        {
            foreach (var l in links)
                if (l.key == key)
                    return l.value as T;
            return null;
        }
    }
}
