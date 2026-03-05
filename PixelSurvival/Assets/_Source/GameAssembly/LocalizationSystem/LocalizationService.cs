using System;
using System.Collections.Generic;
using UnityEngine;

namespace LocalizationSystem
{
    public static class LocalizationService
    {
        public static event Action<string> LanguageChanged;

        public static bool IsInitialized => database != null;
        public static string CurrentLanguageCode => currentLanguageCode;

        private static LocalizationDatabase database;
        private static string currentLanguageCode;

        public static void Initialize(LocalizationDatabase localizationDatabase, string defaultLanguageCode = null)
        {
            database = localizationDatabase;

            if (database == null)
            {
                Debug.LogError("LocalizationService.Initialize called with null database.");
                currentLanguageCode = string.Empty;
                return;
            }

            if (!string.IsNullOrWhiteSpace(defaultLanguageCode) && SetLanguage(defaultLanguageCode))
            {
                return;
            }

            foreach (var preset in database.Languages)
            {
                if (preset == null)
                {
                    continue;
                }

                currentLanguageCode = preset.LanguageCode;
                LanguageChanged?.Invoke(currentLanguageCode);
                return;
            }

            currentLanguageCode = string.Empty;
            Debug.LogWarning("Localization database does not contain any languages.");
        }

        public static bool SetLanguage(string languageCode)
        {
            if (!IsInitialized)
            {
                Debug.LogError("LocalizationService.SetLanguage called before Initialize.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(languageCode))
            {
                return false;
            }

            if (!database.TryGetLanguage(languageCode, out _))
            {
                return false;
            }

            if (string.Equals(currentLanguageCode, languageCode, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            currentLanguageCode = languageCode;
            LanguageChanged?.Invoke(currentLanguageCode);
            return true;
        }

        public static string Get(string key)
        {
            return TryGet(key, out var value) ? value : key;
        }

        public static bool TryGet(string key, out string value)
        {
            value = key;

            if (!IsInitialized || string.IsNullOrWhiteSpace(currentLanguageCode))
            {
                return false;
            }

            if (!database.TryGetLanguage(currentLanguageCode, out var languagePreset))
            {
                return false;
            }

            if (languagePreset == null)
            {
                return false;
            }

            return languagePreset.TryGetValue(key, out value);
        }

        public static IEnumerable<string> GetAvailableLanguageCodes()
        {
            if (!IsInitialized)
            {
                yield break;
            }

            foreach (var language in database.Languages)
            {
                if (language == null || string.IsNullOrWhiteSpace(language.LanguageCode))
                {
                    continue;
                }

                yield return language.LanguageCode;
            }
        }
    }
}
