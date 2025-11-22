using System.IO;
using System.Linq;
using GameAssembly.ItemsSystem.Data;
using UnityEditor;

namespace Editor.Items
{
    public class ItemDatabaseGenerator : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] imported,
            string[] deleted,
            string[] moved,
            string[] _)
        {
            var needRegenerate =
                imported.Any(a => a.EndsWith(".asset")) ||
                deleted.Any(a => a.EndsWith(".asset")) ||
                moved.Any(a => a.EndsWith(".asset"));

            if (needRegenerate)
            {
                Generate();
            }
        }

        [MenuItem("Tools/Generate/Regenerate ItemDatabase")]
        private static void Generate()
        {
            var items = AssetDatabase.FindAssets("t:ItemDefinitionSO")
                .Select(guid => AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(x => x)
                .ToList();

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

            foreach (var item in items)
            {
                var safeName = item.name.Replace(" ", "_");
                var assetPath = item.name;
                writer.WriteLine(
                    $"            {safeName} = Resources.Load<ItemDefinitionSO>(AssetsPaths.ITEMS_CONFIGS_PATH + \"/{assetPath}\");");
            }

            writer.WriteLine("        }");
            writer.WriteLine("    }");
            writer.WriteLine("}");

            AssetDatabase.Refresh();
        }
    }
}