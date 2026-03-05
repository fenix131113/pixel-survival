using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameAssembly.LocalizationSystem
{
    [CreateAssetMenu(fileName = "LocalizationDatabase", menuName = "SO/Localization/Database")]
    public class LocalizationDatabase : ScriptableObject
    {
        [SerializeField] private LocalizationLanguagePreset[] languages = Array.Empty<LocalizationLanguagePreset>();

        private Dictionary<string, LocalizationLanguagePreset> _cachedLanguages;

        public IReadOnlyCollection<LocalizationLanguagePreset> Languages => languages;

        public bool TryGetLanguage(string languageCode, out LocalizationLanguagePreset preset)
        {
            EnsureCache();
            return _cachedLanguages.TryGetValue(languageCode, out preset);
        }

        private void EnsureCache()
        {
            if (_cachedLanguages != null)
            {
                return;
            }

            _cachedLanguages = new Dictionary<string, LocalizationLanguagePreset>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < languages.Length; i++)
            {
                var language = languages[i];
                if (language == null || string.IsNullOrWhiteSpace(language.LanguageCode))
                {
                    continue;
                }

                _cachedLanguages[language.LanguageCode] = language;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _cachedLanguages = null;
        }
#endif
    }
}
