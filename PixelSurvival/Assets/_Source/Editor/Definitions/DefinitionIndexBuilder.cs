using System;
using System.Collections.Generic;
using GameAssembly.Core.Definitions;
using UnityEditor;
using UnityEngine;

namespace Editor.Definitions
{
    public static class DefinitionIndexBuilder
    {
        private const string DEFAULT_INDEX_PATH = "Assets/_Presentation/Resources/Configs/DefinitionIndex.asset";

        [MenuItem("Tools/Definitions/Rebuild Index")]
        public static void RebuildIndex()
        {
            var guids = AssetDatabase.FindAssets("t:ScriptableObject");
            var entries = new List<DefinitionIndexSO.Entry>(guids.Length);
            var usedIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var definition = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
                
                if (definition is not IDefinitionWithId definitionWithId)
                    continue;

                var id = definitionWithId.Id;

                if (string.IsNullOrWhiteSpace(id))
                {
                    id = Guid.NewGuid().ToString("N");
                    if (!TryWriteId(definition, id))
                    {
                        Debug.LogError(
                            $"Failed to auto-assign Id for {assetPath}. Add a writable string field named '_id' or 'id'.");
                        continue;
                    }

                    //Debug.Log($"Auto-assigned Id '{id}' to {assetPath}");
                }

                if (!usedIds.Add(id))
                {
                    var newId = Guid.NewGuid().ToString("N");
                    if (!TryWriteId(definition, newId))
                    {
                        Debug.LogError(
                            $"Duplicate Id '{id}' in {assetPath}, and failed to reassign. Add a writable string field named '_id' or 'id'.");
                        continue;
                    }

                    Debug.LogWarning($"Duplicate Id '{id}' found. Reassigned {assetPath} -> '{newId}'");
                    id = newId;
                    usedIds.Add(id);
                }

                entries.Add(new DefinitionIndexSO.Entry(id, definition));
            }

            entries.Sort((left, right) => string.Compare(left.Id, right.Id, StringComparison.Ordinal));

            var index = AssetDatabase.LoadAssetAtPath<DefinitionIndexSO>(DEFAULT_INDEX_PATH);
            if (!index)
            {
                index = ScriptableObject.CreateInstance<DefinitionIndexSO>();
                AssetDatabase.CreateAsset(index, DEFAULT_INDEX_PATH);
            }

            index.Rebuild(entries);
            EditorUtility.SetDirty(index);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Definition index rebuilt. Entries: {entries.Count}");
        }

        private static bool TryWriteId(ScriptableObject definition, string id)
        {
            var serializedObject = new SerializedObject(definition);
            var idProperty = serializedObject.FindProperty("_id") ?? serializedObject.FindProperty("id");
            if (idProperty is not { propertyType: SerializedPropertyType.String })
            {
                return false;
            }

            idProperty.stringValue = id;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            return true;
        }
    }
}