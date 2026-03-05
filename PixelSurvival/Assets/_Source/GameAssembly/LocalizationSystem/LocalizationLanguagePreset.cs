using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameAssembly.LocalizationSystem
{
    [CreateAssetMenu(fileName = "LocalizationLanguagePreset", menuName = "SO/Localization/Language Preset")]
    public class LocalizationLanguagePreset : ScriptableObject
    {
        [SerializeField] private string languageCode = "en";
        [SerializeField] private string languageName = "English";
        [SerializeField] private LocalizationEntry[] entries = Array.Empty<LocalizationEntry>();

        private Dictionary<string, string> _cachedEntries;

        public string LanguageCode => languageCode;
        public string LanguageName => languageName;

        public bool TryGetValue(string key, out string value)
        {
            EnsureCache();
            return _cachedEntries.TryGetValue(key, out value);
        }

        public IReadOnlyCollection<LocalizationEntry> Entries => entries;

        private void EnsureCache()
        {
            if (_cachedEntries != null)
            {
                return;
            }

            _cachedEntries = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.key))
                {
                    continue;
                }

                _cachedEntries[entry.key] = entry.value ?? string.Empty;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _cachedEntries = null;
        }
#endif
    }
}
