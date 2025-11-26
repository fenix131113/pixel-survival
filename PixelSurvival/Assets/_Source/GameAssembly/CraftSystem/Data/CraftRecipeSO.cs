using System;
using System.Collections.Generic;
using GameAssembly.ItemsSystem.Data;
using GameAssembly.Utils;
using Mirror;
using UnityEngine;

namespace GameAssembly.CraftSystem.Data
{
    [CreateAssetMenu(fileName = "new CraftRecipeSO", menuName = "SO/CraftRecipeSO")]
    public class CraftRecipeSO : ScriptableObject
    {
        [field: SerializeField] private List<CraftGroup> craftGroups;
        
        [field: SerializeField] public ItemDefinitionSO ResultItem { get; private set; }
        [field: SerializeField] public int ResultCount { get; private set; }

        public IReadOnlyList<CraftGroup> CraftGroups => craftGroups;

        [Serializable]
        public class CraftGroup
        {
            [field: SerializeField] public ItemDefinitionSO ItemDefinition { get; private set; }
            [field: SerializeField] public int Count { get; private set; }
        }
    }
    
    public static class CraftRecipeSOSerializer
    {
        public static void WriteArmor(this NetworkWriter writer, CraftRecipeSO data) =>
            writer.WriteString(data.name);

        public static CraftRecipeSO ReadArmor(this NetworkReader reader) =>
            Resources.Load<CraftRecipeSO>(AssetsPaths.RECIPES_CONFIGS_PATH + "/" + reader.ReadString());
    }
}