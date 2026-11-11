using System;
using System.Collections.Generic;
using UnityEngine;

namespace Neonflow
{
    [Serializable]
    public sealed class CharacterProfile
    {
        public string id, nameKey, descriptionKey;
        public string[] poses, hands;
        public Movement[] Movements(Movement[] catalog)
        {
            var allowed = new HashSet<string>(poses, StringComparer.Ordinal);
            allowed.UnionWith(hands);
            return Array.FindAll(catalog, m => allowed.Contains(m.id));
        }
    }

    [Serializable]
    public sealed class CharacterCatalog
    {
        public CharacterProfile[] characters;
        public CharacterProfile Get(string id) => Array.Find(characters, c => c.id == id) ?? characters[0];
        public static CharacterCatalog Load() => JsonUtility.FromJson<CharacterCatalog>(Resources.Load<TextAsset>("Data/Characters").text);
        public void Validate(Movement[] moves)
        {
            if (characters == null || characters.Length != 2) throw new InvalidOperationException("Two characters are required");
            var ids = new HashSet<string>();
            var assigned = new HashSet<string>();
            foreach (var c in characters)
            {
                if (string.IsNullOrEmpty(c.id) || !ids.Add(c.id) || c.poses.Length != 6 || c.hands.Length != 5) throw new InvalidOperationException("Invalid character catalog");
                foreach (var id in c.poses)
                    if (!assigned.Add(id) || !Array.Exists(moves, m => m.id == id && m.category == "Poses")) throw new InvalidOperationException("Invalid pose assignment " + id);
                foreach (var id in c.hands)
                    if (!assigned.Add(id) || !Array.Exists(moves, m => m.id == id && m.category == "Hands")) throw new InvalidOperationException("Invalid hand assignment " + id);
            }
            if (assigned.Count != moves.Length) throw new InvalidOperationException("Character movement coverage is incomplete");
        }
    }
}
