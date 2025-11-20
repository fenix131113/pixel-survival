using System.Collections.Generic;
using GameAssembly.ItemsSystem.Data;

namespace GameAssembly.ItemsSystem
{
    public class ItemRegistry
    {
        public static ItemRegistry Instance { get; private set; }
        
        private readonly Dictionary<ItemDefinitionSO, IItemBehaviour> _itemBehaviours = new();

        public static void Initialize()
        {
            if(Instance != null)
                return;
            
            Instance = new ItemRegistry();
        }

        public bool ItemHasBehaviour(ItemDefinitionSO item, out IItemBehaviour behaviour) => _itemBehaviours.TryGetValue(item, out behaviour);

        public void RegisterItemBehaviour(ItemDefinitionSO item, IItemBehaviour behaviour) => _itemBehaviours.TryAdd(item, behaviour);
    }
}