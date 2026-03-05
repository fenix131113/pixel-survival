using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameAssembly.LocalizationSystem
{
    public static class LocalizationService
    {
        public static event Action<string> LanguageChanged;

        public static bool IsInitialized => _database;
        public static string CurrentLanguageCode => _currentLanguageCode;

        private static LocalizationDatabase _database;
        private static string _currentLanguageCode;

        public static void Initialize(LocalizationDatabase localizationDatabase, string defaultLanguageCode = null)
        {
            _database = localizationDatabase;

            if (!_database)
            {
                Debug.LogError("LocalizationService.Initialize called with null database.");
                _currentLanguageCode = string.Empty;
                return;
            }

            if (!string.IsNullOrWhiteSpace(defaultLanguageCode) && SetLanguage(defaultLanguageCode))
                return;

            foreach (var preset in _database.Languages)
            {
                if (!preset)
                    continue;

                _currentLanguageCode = preset.LanguageCode;
                LanguageChanged?.Invoke(_currentLanguageCode);
                return;
            }

            _currentLanguageCode = string.Empty;
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
                return false;

            if (!_database.TryGetLanguage(languageCode, out _))
                return false;

            if (string.Equals(_currentLanguageCode, languageCode, StringComparison.OrdinalIgnoreCase))
                return true;

            _currentLanguageCode = languageCode;
            LanguageChanged?.Invoke(_currentLanguageCode);
            return true;
        }

        public static string Get(string key)
        {
            return TryGet(key, out var value) ? value : key;
        }

        public static bool TryGet(string key, out string value)
        {
            value = key;

            if (!IsInitialized || string.IsNullOrWhiteSpace(_currentLanguageCode))
            {
                return false;
            }

            if (!_database.TryGetLanguage(_currentLanguageCode, out var languagePreset))
            {
                return false;
            }

            return languagePreset && languagePreset.TryGetValue(key, out value);
        }

        public static IEnumerable<string> GetAvailableLanguageCodes()
        {
            if (!IsInitialized)
                yield break;

            foreach (var language in _database.Languages)
            {
                if (!language || string.IsNullOrWhiteSpace(language.LanguageCode))
                    continue;

                yield return language.LanguageCode;
            }
        }
    }
}
