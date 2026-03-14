using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Editor.Localization
{
    public sealed class LocalizationEditorWindow : EditorWindow
    {
        private const string LANGUAGES_FOLDER_PATH = "Assets/_Presentation/Resources/Configs/Localization/Languages";
        private const string PRESET_TYPE_NAME = "LocalizationLanguagePreset";

        private readonly List<LanguagePresetInfo> _languagePresets = new();

        private int _selectedLanguageIndex;
        private Vector2 _scrollPosition;
        private string _searchTerm = string.Empty;
        private string _newKey = string.Empty;

        private SerializedObject _activePreset;
        private SerializedProperty _entriesProperty;

        private GUIStyle _headerStyle;
        private GUIStyle _cardStyle;

        [MenuItem("Tools/Localization/Editor")]
        private static void Open()
        {
            var window = GetWindow<LocalizationEditorWindow>("Localization Editor");
            window.minSize = new Vector2(960, 600);
            window.RefreshLanguagePresets();
            window.Show();
        }

        private void OnFocus()
        {
            RefreshLanguagePresets();
        }

        private void OnGUI()
        {
            EnsureStyles();
            DrawToolbar();

            if (_languagePresets.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    $"No {PRESET_TYPE_NAME} assets were found in:\n{LANGUAGES_FOLDER_PATH}",
                    MessageType.Warning);
                return;
            }

            if (_activePreset == null || _entriesProperty == null)
            {
                EditorGUILayout.HelpBox(
                    "Could not read translations from selected language preset.",
                    MessageType.Error);
                return;
            }

            _activePreset.Update();

            DrawHeader();
            DrawEntries();

            _activePreset.ApplyModifiedProperties();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Refresh Languages", EditorStyles.toolbarButton, GUILayout.Width(130)))
                {
                    RefreshLanguagePresets();
                }

                if (GUILayout.Button("Open Comparison", EditorStyles.toolbarButton, GUILayout.Width(130)))
                {
                    LocalizationComparisonWindow.OpenWindow();
                }


                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(LANGUAGES_FOLDER_PATH, EditorStyles.miniLabel, GUILayout.MaxWidth(460));
            }
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.VerticalScope(_cardStyle))
            {
                EditorGUILayout.LabelField("Filters and Controls", _headerStyle);

                using (new EditorGUILayout.HorizontalScope())
                {
                    var labels = BuildLanguageLabels();
                    var updatedIndex = EditorGUILayout.Popup("Language", _selectedLanguageIndex, labels);
                    if (updatedIndex != _selectedLanguageIndex)
                    {
                        _selectedLanguageIndex = updatedIndex;
                        BindSelectedPreset();
                    }

                    if (GUILayout.Button("Ping Asset", GUILayout.Width(90)))
                    {
                        var preset = _languagePresets[_selectedLanguageIndex].Asset;
                        EditorGUIUtility.PingObject(preset);
                        Selection.activeObject = preset;
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
                    if (GUILayout.Button("Add Key", GUILayout.Width(90)))
                    {
                        AddKey();
                    }
                }
            }
        }

        private void DrawEntries()
        {
            EditorGUILayout.LabelField($"Keys ({_entriesProperty.arraySize})", _headerStyle);
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            for (var i = 0; i < _entriesProperty.arraySize; i++)
            {
                var entry = _entriesProperty.GetArrayElementAtIndex(i);
                if (!TryGetEntryFields(entry, out var keyProperty, out var textProperty))
                {
                    continue;
                }

                var key = keyProperty.stringValue;
                if (!string.IsNullOrWhiteSpace(_searchTerm) &&
                    key.IndexOf(_searchTerm, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                using (new EditorGUILayout.VerticalScope(_cardStyle))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(string.IsNullOrEmpty(key) ? $"Entry {i}" : key,
                            EditorStyles.boldLabel);
                        GUILayout.FlexibleSpace();

                        GUI.backgroundColor = new Color(1f, 0.55f, 0.55f);
                        if (GUILayout.Button("✕", GUILayout.Width(24), GUILayout.Height(20)))
                        {
                            RemoveEntryAt(i);
                            GUI.backgroundColor = Color.white;
                            break;
                        }

                        GUI.backgroundColor = Color.white;
                    }

                    textProperty.stringValue =
                        EditorGUILayout.TextArea(textProperty.stringValue, GUILayout.MinHeight(56));
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void AddKey()
        {
            var keyToAdd = _newKey.Trim();
            if (string.IsNullOrWhiteSpace(keyToAdd))
            {
                return;
            }

            if (HasKey(keyToAdd))
            {
                EditorUtility.DisplayDialog("Duplicate key", $"Key '{keyToAdd}' already exists.", "OK");
                return;
            }

            _entriesProperty.InsertArrayElementAtIndex(_entriesProperty.arraySize);
            var newEntry = _entriesProperty.GetArrayElementAtIndex(_entriesProperty.arraySize - 1);
            if (!TryGetEntryFields(newEntry, out var keyProperty, out var textProperty))
            {
                EditorUtility.DisplayDialog("Cannot add key",
                    "Failed to initialize a new entry. Check LocalizationLanguagePreset entry structure.", "OK");
                _entriesProperty.DeleteArrayElementAtIndex(_entriesProperty.arraySize - 1);
                return;
            }

            keyProperty.stringValue = keyToAdd;
            textProperty.stringValue = string.Empty;
            _newKey = string.Empty;

            EditorUtility.SetDirty(_activePreset.targetObject);
        }

        private bool HasKey(string key)
        {
            for (var i = 0; i < _entriesProperty.arraySize; i++)
            {
                var entry = _entriesProperty.GetArrayElementAtIndex(i);
                if (!TryGetEntryFields(entry, out var keyProperty, out _))
                {
                    continue;
                }

                if (string.Equals(keyProperty.stringValue, key, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void RemoveEntryAt(int index)
        {
            if (!EditorUtility.DisplayDialog("Delete key",
                    "Delete this translation key from current language preset?", "Delete", "Cancel"))
            {
                return;
            }

            _entriesProperty.DeleteArrayElementAtIndex(index);
            EditorUtility.SetDirty(_activePreset.targetObject);
        }

        private void RefreshLanguagePresets()
        {
            _languagePresets.Clear();

            var guids = AssetDatabase.FindAssets($"t:{PRESET_TYPE_NAME}", new[] { LANGUAGES_FOLDER_PATH });
            foreach (var t in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(t);
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (!asset)
                {
                    continue;
                }

                _languagePresets.Add(new LanguagePresetInfo
                {
                    Asset = asset,
                    Code = ReadLanguageCode(asset, path)
                });
            }

            _languagePresets.Sort((a, b) => string.Compare(a.Code, b.Code, StringComparison.OrdinalIgnoreCase));

            if (_languagePresets.Count == 0)
            {
                _activePreset = null;
                _entriesProperty = null;
                _selectedLanguageIndex = 0;
                return;
            }

            _selectedLanguageIndex = Mathf.Clamp(_selectedLanguageIndex, 0, _languagePresets.Count - 1);
            BindSelectedPreset();
        }

        private void BindSelectedPreset()
        {
            if (_languagePresets.Count == 0)
            {
                _activePreset = null;
                _entriesProperty = null;
                return;
            }

            var selectedAsset = _languagePresets[_selectedLanguageIndex].Asset;
            _activePreset = new SerializedObject(selectedAsset);
            _entriesProperty = FindEntriesArray(_activePreset);
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
                    var value = strings[i].stringValue ?? string.Empty;
                    if (value.Length <= 128 && value.IndexOf('\n') < 0 && value.IndexOf('\r') < 0)
                    {
                        keyProperty = strings[i];
                        break;
                    }
                }
            }

            keyProperty ??= strings[0];

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

        private string[] BuildLanguageLabels()
        {
            var labels = new string[_languagePresets.Count];
            for (var i = 0; i < _languagePresets.Count; i++)
            {
                labels[i] = _languagePresets[i].Code;
            }

            return labels;
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

        private sealed class LanguagePresetInfo
        {
            public string Code;
            public ScriptableObject Asset;
        }
    }
}