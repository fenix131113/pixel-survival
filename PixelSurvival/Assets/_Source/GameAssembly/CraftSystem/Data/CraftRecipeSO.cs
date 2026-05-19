using System;
using System.Collections.Generic;
using GameAssembly.Core.Definitions;
using GameAssembly.ItemsSystem.Data;
using Mirror;
using UnityEngine;

namespace GameAssembly.CraftSystem.Data
{
    [CreateAssetMenu(fileName = "new CraftRecipeSO", menuName = "SO/CraftRecipeSO")]
    public class CraftRecipeSO : ScriptableObject, IDefinitionWithId
    {
        [field: SerializeField] private List<CraftGroup> craftGroups;
        [SerializeField] private List<CraftRecipeSO> requiredCrafts;
        
        [field: SerializeField] public ItemDefinitionSO ResultItem { get; private set; }
        [field: SerializeField] public int ResultCount { get; private set; }

        [SerializeField] private string id;
        
        public IReadOnlyList<CraftGroup> CraftGroups => craftGroups;
        public IReadOnlyList<CraftRecipeSO> RequiredCrafts => requiredCrafts;
        public string Id => id;

        private void OnValidate()
        {
            craftGroups ??= new List<CraftGroup>();
            requiredCrafts ??= new List<CraftRecipeSO>();

            var unique = new HashSet<CraftRecipeSO>();
            for (var index = 0; index < requiredCrafts.Count; index++)
            {
                var requiredRecipe = requiredCrafts[index];

                // Keep null slots: Unity creates them first when user presses '+' in inspector.
                if (!requiredRecipe)
                    continue;

                if (requiredRecipe == this || !unique.Add(requiredRecipe))
                    requiredCrafts[index] = null;
            }
        }

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
            writer.WriteString(data ? data.Id : string.Empty);

        public static CraftRecipeSO ReadCraftRecipe(this NetworkReader reader)
        {
            DefinitionResolverProvider.TryResolve<CraftRecipeSO>(reader.ReadString(), out var res);
            return res;
        }
    }
}
