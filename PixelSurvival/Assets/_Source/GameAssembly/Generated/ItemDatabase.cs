// Auto-generated code

using GameAssembly.ItemsSystem.Data;
using GameAssembly.Utils;
using UnityEngine;

namespace GameAssembly.Generated
{
    public static class ItemDatabase
    {
        public static readonly ItemDefinitionSO Apple;
        public static readonly ItemDefinitionSO Pie;
        public static readonly ItemDefinitionSO RedMushroom;
        public static readonly ItemDefinitionSO SecondTestItem;
        public static readonly ItemDefinitionSO Stick;
        public static readonly ItemDefinitionSO Stump;
        public static readonly ItemDefinitionSO TestItem;

        static ItemDatabase()
        {
            Apple = Resources.Load<ItemDefinitionSO>(AssetsPaths.ITEMS_CONFIGS_PATH + "/Apple");
            Pie = Resources.Load<ItemDefinitionSO>(AssetsPaths.ITEMS_CONFIGS_PATH + "/Pie");
            RedMushroom = Resources.Load<ItemDefinitionSO>(AssetsPaths.ITEMS_CONFIGS_PATH + "/RedMushroom");
            SecondTestItem = Resources.Load<ItemDefinitionSO>(AssetsPaths.ITEMS_CONFIGS_PATH + "/SecondTestItem");
            Stick = Resources.Load<ItemDefinitionSO>(AssetsPaths.ITEMS_CONFIGS_PATH + "/Stick");
            Stump = Resources.Load<ItemDefinitionSO>(AssetsPaths.ITEMS_CONFIGS_PATH + "/Stump");
            TestItem = Resources.Load<ItemDefinitionSO>(AssetsPaths.ITEMS_CONFIGS_PATH + "/TestItem");
        }
    }
}
