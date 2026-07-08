using System.IO;
using System.Linq;
using GameAssembly.ItemsSystem.Data;
using UnityEditor;

namespace Editor.Items
{
    public class ItemDatabaseGenerator : AssetPostprocessor
    {
        private static bool _needToRebuild = true;
        
        private static void OnPostprocessAllAssets(
            string[] imported,
            string[] deleted,
            string[] moved,
            string[] _)
        {
            if(!_needToRebuild)
                return;
            
            var needRegenerate =
                imported.Any(a => a.EndsWith(".asset")) ||
                deleted.Any(a => a.EndsWith(".asset")) ||
                moved.Any(a => a.EndsWith(".asset"));

            if (needRegenerate)
            {
                Generate();
            }
        }

        [MenuItem("Tools/Generate/Activate Auto Rebuild")]
        private static void ActivateAutoRebuild() => _needToRebuild = true;

        [MenuItem("Tools/Generate/Deactivate Auto Rebuild")]
        private static void DeactivateAutoRebuild() => _needToRebuild = false;

        [MenuItem("Tools/Generate/Regenerate ItemDatabase")]
        private static void Generate()
        {
            var assets = AssetDatabase.FindAssets("t:ItemDefinitionSO");
            var items = assets.Select(guid => AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(x => x)
                .ToList();

            var paths = assets.Select(AssetDatabase.GUIDToAssetPath).ToList();

            const string path = "Assets/_Source/GameAssembly/Generated/ItemDatabase.cs";
            Directory.CreateDirectory("Assets/_Source/GameAssembly/Generated");

            using var writer = new StreamWriter(path);
            writer.WriteLine("// Auto-generated code");
            writer.WriteLine();
            writer.WriteLine("using GameAssembly.ItemsSystem.Data;");
            writer.WriteLine("using GameAssembly.Utils;");
            writer.WriteLine("using UnityEngine;");
            writer.WriteLine();
            writer.WriteLine("namespace GameAssembly.Generated");
            writer.WriteLine("{");
            
            writer.WriteLine("    public static class ItemDatabase");
            writer.WriteLine("    {");

            foreach (var safeName in items.Select(item => item.name.Replace(" ", "_")))
            {
                writer.WriteLine($"        public static readonly ItemDefinitionSO {safeName};");
            }

            writer.WriteLine();
            writer.WriteLine("        static ItemDatabase()");
            writer.WriteLine("        {");

            for (var index = 0; index < items.Count; index++)
            {
                var item = items[index];
                var safeName = item.name.Replace(" ", "_");
                var assetPath = paths[index].Split("/Items/")[^1].Replace(".asset", "");
                writer.WriteLine(
                    $"            {safeName} = Resources.Load<ItemDefinitionSO>(AssetsPaths.ITEM_CONFIGS_PATH + \"/{assetPath}\");");
            }

            writer.WriteLine("        }");
            writer.WriteLine("    }");
            writer.WriteLine("}");

            AssetDatabase.Refresh();
        }
    }
}