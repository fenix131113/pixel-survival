using System;
using System.Collections.Generic;
using UnityEngine;

namespace LocalizationSystem
{
    [CreateAssetMenu(fileName = "LocalizationDatabase", menuName = "SO/Localization/Database")]
    public class LocalizationDatabase : ScriptableObject
    {
        [SerializeField] private LocalizationLanguagePreset[] languages = Array.Empty<LocalizationLanguagePreset>();

        private Dictionary<string, LocalizationLanguagePreset> cachedLanguages;

        public IReadOnlyCollection<LocalizationLanguagePreset> Languages => languages;

        public bool TryGetLanguage(string languageCode, out LocalizationLanguagePreset preset)
        {
            EnsureCache();
            return cachedLanguages.TryGetValue(languageCode, out preset);
        }

        private void EnsureCache()
        {
            if (cachedLanguages != null)
            {
                return;
            }

            cachedLanguages = new Dictionary<string, LocalizationLanguagePreset>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < languages.Length; i++)
            {
                var language = languages[i];
                if (language == null || string.IsNullOrWhiteSpace(language.LanguageCode))
                {
                    continue;
                }

                cachedLanguages[language.LanguageCode] = language;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            cachedLanguages = null;
        }
#endif
    }
}
