// Auto-generated code

using GameAssembly.ItemsSystem.Data;

namespace GameAssembly.Generated
{
    public static class ItemDatabase
    {
        public readonly static ItemDefinitionSO SecondTestItem;
        public readonly static ItemDefinitionSO TestItem;

        static ItemDatabase()
        {
            SecondTestItem = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>("Assets/_Presentation/Resources/Configs/Items/SecondTestItem.asset");
            TestItem = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>("Assets/_Presentation/Resources/Configs/Items/TestItem.asset");
        }
    }
}
