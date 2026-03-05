using System;
using System.Collections.Generic;
using UnityEngine;

namespace LocalizationSystem
{
    [CreateAssetMenu(fileName = "LocalizationLanguagePreset", menuName = "SO/Localization/Language Preset")]
    public class LocalizationLanguagePreset : ScriptableObject
    {
        [SerializeField] private string languageCode = "en";
        [SerializeField] private string languageName = "English";
        [SerializeField] private LocalizationEntry[] entries = Array.Empty<LocalizationEntry>();

        private Dictionary<string, string> cachedEntries;

        public string LanguageCode => languageCode;
        public string LanguageName => languageName;

        public bool TryGetValue(string key, out string value)
        {
            EnsureCache();
            return cachedEntries.TryGetValue(key, out value);
        }

        public IReadOnlyCollection<LocalizationEntry> Entries => entries;

        private void EnsureCache()
        {
            if (cachedEntries != null)
            {
                return;
            }

            cachedEntries = new Dictionary<string, string>(StringComparer.Ordinal);

            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (string.IsNullOrWhiteSpace(entry.Key))
                {
                    continue;
                }

                cachedEntries[entry.Key] = entry.Value ?? string.Empty;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            cachedEntries = null;
        }
#endif
    }
}
