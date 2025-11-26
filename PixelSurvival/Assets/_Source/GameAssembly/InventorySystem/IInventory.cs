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
        ItemInstance GetItemByIndex(int index);
        bool TryAddItemFromInstance(ItemInstance instance,  bool fullInsert, bool ignoreMeta = false);
        bool TryAddNewItem(ItemDefinitionSO definition, int count);
        bool TryRemoveItem(ItemDefinitionSO item, int count);
        bool TryRemoveItemByIndex(int index, bool removeCount);
        bool TryAddItemInIndexFromInstance(ItemInstance item, int index, bool fullInsert);
        bool HasItem(ItemDefinitionSO item, int count = 1);
        bool CanAddNewItem(ItemDefinitionSO definition, int count = 1);
    }
}