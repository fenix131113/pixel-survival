using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameAssembly.Core.Definitions
{
    [CreateAssetMenu(fileName = "DefinitionIndex", menuName = "Game/Definitions/Definition Index")]
    public sealed class DefinitionIndexSO : ScriptableObject
    {
        [SerializeField] private List<Entry> entries = new();

        private Dictionary<string, ScriptableObject> _cache;

        public bool TryGet(string id, out ScriptableObject definition)
        {
            EnsureCache();
            return _cache.TryGetValue(id, out definition);
        }

        public IReadOnlyList<Entry> Entries => entries;

        public void Rebuild(IReadOnlyList<Entry> entriesList)
        {
            entries = new List<Entry>(entriesList);
            _cache = null;
        }

        private void EnsureCache()
        {
            if (_cache != null)
            {
                return;
            }

            _cache = new Dictionary<string, ScriptableObject>(entries.Count, StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id) || entry.Definition == null)
                {
                    continue;
                }

                _cache[entry.Id] = entry.Definition;
            }
        }

        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private string id;
            [SerializeField] private ScriptableObject definition;

            public string Id => id;
            public ScriptableObject Definition => definition;

            public Entry(string id, ScriptableObject definition)
            {
                this.id = id;
                this.definition = definition;
            }
        }
    }
}