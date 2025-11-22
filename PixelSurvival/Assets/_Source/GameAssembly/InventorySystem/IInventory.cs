using System;
using System.Collections.Generic;
using GameAssembly.ItemsSystem;
using GameAssembly.ItemsSystem.Data;

namespace GameAssembly.InventorySystem
{
    public interface IInventory
    {
        event Action OnInventoryChanged;
        event Action<int> OnItemChanged;
        int GetInventorySize();
        IEnumerable<ItemInstance> GetItems();
        bool TryAddItemFromInstance(ItemInstance instance, bool ignoreMeta = false);
        bool TryAddNewItem(ItemDefinitionSO definition, int count);
        bool TryRemoveItem(ItemDefinitionSO item, int count);
        bool HasItem(ItemDefinitionSO item, int count = 1);
    }
}