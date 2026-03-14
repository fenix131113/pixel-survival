using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Editor.Localization
{
    public sealed class LocalizationComparisonWindow : EditorWindow
    {
        private const string LANGUAGES_FOLDER_PATH = "Assets/_Presentation/Resources/Configs/Localization/Languages";
        private const string PRESET_TYPE_NAME = "LocalizationLanguagePreset";

        private readonly List<LanguageContext> _languages = new();
        private readonly Dictionary<string, List<EntryRef>> _rowsByKey = new(StringComparer.Ordinal);

        private Vector2 _rowsScroll;
        private string _searchTerm = string.Empty;
        private string _newKey = string.Empty;
        private readonly List<bool> _editableLanguages = new();

        private GUIStyle _headerStyle;
        private GUIStyle _cardStyle;

        [MenuItem("Tools/Localization/Comparison")]
        public static void OpenWindow()
        {
            var window = GetWindow<LocalizationComparisonWindow>("Localization Comparison");
            window.minSize = new Vector2(1200, 650);
            window.RefreshLanguages();
            window.Show();
        }

        private void OnFocus()
        {
            RefreshLanguages();
        }

        private void OnGUI()
        {
            EnsureStyles();
            DrawToolbar();

            if (_languages.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    $"No {PRESET_TYPE_NAME} assets were found in:\n{LANGUAGES_FOLDER_PATH}",
                    MessageType.Warning);
                return;
            }

            UpdateSerializedObjects();
            BuildRows();

            DrawControlPanel();
            DrawRows();

            ApplySerializedObjects();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Refresh Languages", EditorStyles.toolbarButton, GUILayout.Width(130)))
                {
                    RefreshLanguages();
                }

                if (GUILayout.Button("Open Single-Language Editor", EditorStyles.toolbarButton, GUILayout.Width(190)))
                {
                    EditorApplication.ExecuteMenuItem("Tools/Localization/Editor");
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(LANGUAGES_FOLDER_PATH, EditorStyles.miniLabel, GUILayout.MaxWidth(460));
            }
        }

        private void DrawControlPanel()
        {
            using (new EditorGUILayout.VerticalScope(_cardStyle))
            {
                EditorGUILayout.LabelField("Languages and Filters", _headerStyle);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PrefixLabel("Editable languages");

                    if (GUILayout.Button(GetEditableLanguagesButtonLabel(), EditorStyles.popup, GUILayout.Width(240)))
                    {
                        ShowLanguageSelectionMenu();
                    }

                    if (GUILayout.Button("All", GUILayout.Width(50)))
                    {
                        SetAllEditable(true);
                    }

                    if (GUILayout.Button("None", GUILayout.Width(50)))
                    {
                        SetAllEditable(false);
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    _searchTerm = EditorGUILayout.TextField("Search by key", _searchTerm);
                    if (GUILayout.Button("Clear", GUILayout.Width(80)))
                    {
                        _searchTerm = string.Empty;
                        GUI.FocusControl(null);
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    _newKey = EditorGUILayout.TextField("New key", _newKey);
                    if (GUILayout.Button("Add to selected", GUILayout.Width(120)))
                    {
                        AddKeyToSelectedLanguages();
                    }
                }
            }
        }

        private void DrawRows()
        {
            var filteredKeys = GetFilteredSortedKeys();
            EditorGUILayout.LabelField($"Keys ({filteredKeys.Count}/{_rowsByKey.Count})", _headerStyle);

            var selectedIndexes = GetSelectedLanguageIndexes();
            if (selectedIndexes.Count == 0)
            {
                EditorGUILayout.HelpBox("Select at least one language in 'Editable languages'.", MessageType.Info);
                return;
            }

            var hasStructuralChanges = false;
            _rowsScroll = EditorGUILayout.BeginScrollView(_rowsScroll);

            try
            {
                foreach (var key in filteredKeys)
                {
                    using (new EditorGUILayout.VerticalScope(_cardStyle))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(key, EditorStyles.boldLabel);
                            GUILayout.FlexibleSpace();

                            GUI.backgroundColor = new Color(1f, 0.55f, 0.55f);
                            if (GUILayout.Button("✕", GUILayout.Width(24), GUILayout.Height(20)))
                            {
                                RemoveKeyFromSelectedLanguages(key, selectedIndexes);
                                hasStructuralChanges = true;
                            }

                            GUI.backgroundColor = Color.white;
                        }

                        if (hasStructuralChanges)
                        {
                            break;
                        }

                        foreach (var language in selectedIndexes.Select(languageIndex => _languages[languageIndex]))
                        {
                            if (DrawLanguageFieldForKey(language, key))
                            {
                                hasStructuralChanges = true;
                                break;
                            }
                        }
                    }

                    if (hasStructuralChanges)
                    {
                        break;
                    }
                }
            }
            finally
            {
                EditorGUILayout.EndScrollView();
            }

            if (hasStructuralChanges)
            {
                GUI.FocusControl(null);
                Repaint();
            }
        }

        private bool DrawLanguageFieldForKey(LanguageContext language, string key)
        {
            var entry = FindEntry(language, key);
            if (entry == null)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"{language.Code} (missing)", EditorStyles.miniBoldLabel,
                        GUILayout.Width(160));
                    if (GUILayout.Button("Create", GUILayout.Width(80)))
                    {
                        AddEntry(language, key, string.Empty);
                        return true;
                    }
                }

                return false;
            }

            if (entry.Text is not { propertyType: SerializedPropertyType.String })
            {
                EditorGUILayout.HelpBox($"{language.Code}: invalid translation field type.", MessageType.Warning);
                return false;
            }

            EditorGUILayout.LabelField(language.Code, EditorStyles.miniBoldLabel);
            entry.Text.stringValue =
                EditorGUILayout.TextArea(entry.Text.stringValue ?? string.Empty, GUILayout.MinHeight(44));
            return false;
        }

        private void ShowLanguageSelectionMenu()
        {
            var menu = new GenericMenu();
            for (var i = 0; i < _languages.Count; i++)
            {
                var index = i;
                menu.AddItem(new GUIContent(_languages[i].Code), _editableLanguages[i], () =>
                {
                    _editableLanguages[index] = !_editableLanguages[index];
                    Repaint();
                });
            }

            menu.ShowAsContext();
        }

        private string GetEditableLanguagesButtonLabel()
        {
            var count = _editableLanguages.Count(t => t);

            if (count == 0)
            {
                return "None selected";
            }

            return count == _languages.Count ? "All selected" : $"Selected: {count}";
        }

        private void SetAllEditable(bool enabled)
        {
            for (var i = 0; i < _editableLanguages.Count; i++)
            {
                _editableLanguages[i] = enabled;
            }
        }

        private void RefreshLanguages()
        {
            _languages.Clear();

            var guids = AssetDatabase.FindAssets($"t:{PRESET_TYPE_NAME}", new[] { LANGUAGES_FOLDER_PATH });
            foreach (var t in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(t);
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (!asset)
                {
                    continue;
                }

                var serialized = new SerializedObject(asset);
                var entries = FindEntriesArray(serialized);
                if (entries == null)
                {
                    continue;
                }

                _languages.Add(new LanguageContext
                {
                    Code = ReadLanguageCode(asset, path),
                    Serialized = serialized,
                    Entries = entries
                });
            }

            _languages.Sort((a, b) => string.Compare(a.Code, b.Code, StringComparison.OrdinalIgnoreCase));

            _editableLanguages.Clear();
            for (var i = 0; i < _languages.Count; i++)
            {
                _editableLanguages.Add(true);
            }
        }

        private void UpdateSerializedObjects()
        {
            foreach (var t in _languages)
            {
                t.Serialized.Update();
                t.Entries = FindEntriesArray(t.Serialized);
            }
        }

        private void ApplySerializedObjects()
        {
            foreach (var t in _languages)
            {
                t.Serialized.ApplyModifiedProperties();
            }
        }

        private void BuildRows()
        {
            _rowsByKey.Clear();

            foreach (var entries in _languages.Select(t => t.Entries).Where(entries => entries != null))
            {
                for (var i = 0; i < entries.arraySize; i++)
                {
                    var entry = entries.GetArrayElementAtIndex(i);
                    if (!TryGetEntryFields(entry, out var keyProperty, out var textProperty))
                    {
                        continue;
                    }

                    if (keyProperty is not { propertyType: SerializedPropertyType.String })
                    {
                        continue;
                    }

                    var key = keyProperty.stringValue;
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        continue;
                    }

                    if (!_rowsByKey.TryGetValue(key, out var refs))
                    {
                        refs = new List<EntryRef>();
                        _rowsByKey.Add(key, refs);
                    }

                    refs.Add(new EntryRef
                    {
                        Key = keyProperty,
                        Text = textProperty
                    });
                }
            }
        }

        private List<string> GetFilteredSortedKeys()
        {
            var keys = _rowsByKey.Keys.Where(key =>
                string.IsNullOrWhiteSpace(_searchTerm) ||
                key.IndexOf(_searchTerm, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            keys.Sort(StringComparer.OrdinalIgnoreCase);
            return keys;
        }

        private void AddKeyToSelectedLanguages()
        {
            var keyToAdd = _newKey.Trim();
            if (string.IsNullOrWhiteSpace(keyToAdd))
            {
                return;
            }

            var selected = GetSelectedLanguageIndexes();
            if (selected.Count == 0)
            {
                return;
            }

            foreach (var language in selected.Select(t => _languages[t])
                         .Where(language => FindEntry(language, keyToAdd) == null))
            {
                AddEntry(language, keyToAdd, string.Empty);
            }

            _newKey = string.Empty;
        }

        private void RemoveKeyFromSelectedLanguages(string key, IReadOnlyList<int> selectedIndexes)
        {
            if (!EditorUtility.DisplayDialog("Delete key",
                    $"Delete key '{key}' from selected languages?", "Delete", "Cancel"))
            {
                return;
            }

            foreach (var t in selectedIndexes)
            {
                var language = _languages[t];
                var entries = language.Entries;
                if (entries == null)
                {
                    continue;
                }

                for (var entryIndex = entries.arraySize - 1; entryIndex >= 0; entryIndex--)
                {
                    var entry = entries.GetArrayElementAtIndex(entryIndex);
                    if (!TryGetEntryFields(entry, out var keyProperty, out _))
                    {
                        continue;
                    }

                    if (keyProperty is not { propertyType: SerializedPropertyType.String })
                    {
                        continue;
                    }


                    if (!string.Equals(keyProperty.stringValue, key, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    entries.DeleteArrayElementAtIndex(entryIndex);
                }
            }
        }

        private static void AddEntry(LanguageContext language, string key, string text)
        {
            var entries = language.Entries;
            entries.InsertArrayElementAtIndex(entries.arraySize);
            var entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
            if (!TryGetEntryFields(entry, out var keyProperty, out var textProperty))
            {
                entries.DeleteArrayElementAtIndex(entries.arraySize - 1);
                return;
            }

            if (keyProperty == null || textProperty == null ||
                keyProperty.propertyType != SerializedPropertyType.String ||
                textProperty.propertyType != SerializedPropertyType.String)
            {
                entries.DeleteArrayElementAtIndex(entries.arraySize - 1);
                return;
            }

            keyProperty.stringValue = key;
            textProperty.stringValue = text;
        }

        private static EntryRef FindEntry(LanguageContext language, string key)
        {
            var entries = language.Entries;
            if (entries == null)
            {
                return null;
            }

            for (var i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                if (!TryGetEntryFields(entry, out var keyProperty, out var textProperty))
                {
                    continue;
                }

                if (keyProperty is not { propertyType: SerializedPropertyType.String })
                {
                    continue;
                }

                if (!string.Equals(keyProperty.stringValue, key, StringComparison.Ordinal))
                {
                    continue;
                }

                return new EntryRef
                {
                    Key = keyProperty,
                    Text = textProperty
                };
            }

            return null;
        }

        private List<int> GetSelectedLanguageIndexes()
        {
            var result = new List<int>();
            for (var i = 0; i < _languages.Count; i++)
            {
                if (i < _editableLanguages.Count && _editableLanguages[i])
                {
                    result.Add(i);
                }
            }

            return result;
        }

        private static SerializedProperty FindEntriesArray(SerializedObject serializedObject)
        {
            var named = serializedObject.FindProperty("entries") ??
                        serializedObject.FindProperty("keys") ??
                        serializedObject.FindProperty("translations") ??
                        serializedObject.FindProperty("localizations");

            if (named is { isArray: true } && named.propertyType != SerializedPropertyType.String)
            {
                if (named.arraySize == 0)
                {
                    return named;
                }

                var first = named.GetArrayElementAtIndex(0);
                if (TryGetEntryFields(first, out _, out _))
                {
                    return named;
                }
            }

            var iterator = serializedObject.GetIterator();
            if (!iterator.NextVisible(true))
            {
                return null;
            }

            do
            {
                if (!iterator.isArray || iterator.propertyType == SerializedPropertyType.String)
                {
                    continue;
                }

                if (iterator.arraySize == 0)
                {
                    return serializedObject.FindProperty(iterator.propertyPath);
                }

                var firstElement = iterator.GetArrayElementAtIndex(0);
                if (TryGetEntryFields(firstElement, out _, out _))
                {
                    return serializedObject.FindProperty(iterator.propertyPath);
                }
            } while (iterator.NextVisible(false));

            return null;
        }

        private static bool TryGetEntryFields(SerializedProperty entry, out SerializedProperty keyProperty,
            out SerializedProperty textProperty)
        {
            keyProperty = null;
            textProperty = null;

            if (entry == null || entry.propertyType == SerializedPropertyType.String)
            {
                return false;
            }

            var copy = entry.Copy();
            var end = copy.GetEndProperty();
            var strings = new List<SerializedProperty>(4);

            if (!copy.NextVisible(true))
            {
                return false;
            }

            while (!SerializedProperty.EqualContents(copy, end))
            {
                if (copy.propertyType == SerializedPropertyType.String)
                {
                    strings.Add(copy.Copy());
                }

                if (!copy.NextVisible(false))
                {
                    break;
                }
            }

            if (strings.Count < 2)
            {
                return false;
            }

            foreach (var t in strings)
            {
                var name = t.name.ToLowerInvariant();
                if (name.Contains("key") || name.Contains("id") || name.Contains("term") || name.Contains("name"))
                {
                    keyProperty = t;
                    break;
                }
            }

            foreach (var t in strings)
            {
                var name = t.name.ToLowerInvariant();
                if (name.Contains("text") || name.Contains("value") || name.Contains("translation") ||
                    name.Contains("localized") || name.Contains("content"))
                {
                    textProperty = t;
                    break;
                }
            }

            if (keyProperty == null)
            {
                for (var i = 0; i < strings.Count; i++)
                {
                    var propName = strings[i].name.ToLowerInvariant();
                    if (propName.Contains("text") || propName.Contains("value") || propName.Contains("translation") ||
                        propName.Contains("localized") || propName.Contains("content"))
                    {
                        continue;
                    }

                    var value = strings[i].stringValue ?? string.Empty;
                    if (value.Length <= 128 && value.IndexOf('\n') < 0 && value.IndexOf('\r') < 0)
                    {
                        keyProperty = strings[i];
                        break;
                    }
                }
            }

            if (keyProperty == null)
            {
                return false;
            }

            textProperty ??= strings[0] == keyProperty ? strings[1] : strings[0];

            if (SerializedProperty.EqualContents(keyProperty, textProperty))
            {
                return false;
            }

            return keyProperty.propertyType == SerializedPropertyType.String &&
                   textProperty.propertyType == SerializedPropertyType.String;
        }

        private static string ReadLanguageCode(ScriptableObject asset, string path)
        {
            var serialized = new SerializedObject(asset);
            var direct = serialized.FindProperty("language");
            if (direct is { propertyType: SerializedPropertyType.String } &&
                !string.IsNullOrWhiteSpace(direct.stringValue))
            {
                return direct.stringValue;
            }

            direct = serialized.FindProperty("languageCode");
            if (direct is { propertyType: SerializedPropertyType.String } &&
                !string.IsNullOrWhiteSpace(direct.stringValue))
            {
                return direct.stringValue;
            }

            return System.IO.Path.GetFileNameWithoutExtension(path);
        }

        private void EnsureStyles()
        {
            _headerStyle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13
            };

            _cardStyle ??= new GUIStyle("helpbox")
            {
                padding = new RectOffset(10, 10, 10, 10),
                margin = new RectOffset(0, 0, 4, 8)
            };
        }

        private sealed class LanguageContext
        {
            public string Code;
            public SerializedObject Serialized;
            public SerializedProperty Entries;
        }

        private sealed class EntryRef
        {
            public SerializedProperty Key;
            public SerializedProperty Text;
        }
    }
}