using System;
using System.Collections.Generic;
using GameAssembly.Core.Definitions;
using GameAssembly.ItemsSystem.Data;
using GameAssembly.Utils;
using Mirror;
using UnityEngine;

namespace GameAssembly.CraftSystem.Data
{
    [CreateAssetMenu(fileName = "new CraftRecipeSO", menuName = "SO/CraftRecipeSO")]
    public class CraftRecipeSO : ScriptableObject, IDefinitionWithId
    {
        [field: SerializeField] private List<CraftGroup> craftGroups;
        
        [field: SerializeField] public ItemDefinitionSO ResultItem { get; private set; }
        [field: SerializeField] public int ResultCount { get; private set; }

        [SerializeField] private string id;
        
        public IReadOnlyList<CraftGroup> CraftGroups => craftGroups;
        public string Id => id;

        [Serializable]
        public class CraftGroup
        {
            [field: SerializeField] public ItemDefinitionSO ItemDefinition { get; private set; }
            [field: SerializeField] public int Count { get; private set; }
        }
    }
    
    public static class CraftRecipeSOSerializer
    {
        public static void WriteCraftRecipe(this NetworkWriter writer, CraftRecipeSO data) =>
            writer.WriteString(data.Id);

        public static CraftRecipeSO ReadCraftRecipe(this NetworkReader reader)
        {
            DefinitionResolverProvider.TryResolve<CraftRecipeSO>(reader.ReadString(), out var res);
            return res;
        }
    }
}