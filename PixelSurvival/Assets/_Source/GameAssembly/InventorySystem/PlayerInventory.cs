using System.Collections.Generic;
using System.Linq;
using GameAssembly.ItemsSystem;
using GameAssembly.ItemsSystem.Data;
using GameAssembly.PlayerSystem;
using Mirror;
using UnityEngine;

namespace GameAssembly.InventorySystem
{
    public class PlayerInventory : BaseInventory
    {
        private PlayerSelector _playerSelector;

        private void Awake()
        {
            _items ??= new ItemInstance[inventorySize];
            _playerSelector = GetComponent<PlayerSelector>();
        }

        [Server]
        public override bool TryAddItemFromInstance(ItemInstance instance, bool fullInsert, bool ignoreMeta = false)
        {
            if (instance == null)
                return false;

            CollectPrioritizedInsertIndexes(
                item => item.Definition == instance.Definition && (ignoreMeta || instance.MatchAllMeta(item.Meta)),
                out var hotBarExactItemsIndexes,
                out var hotBarEmptyItemsIndexes,
                out var inventoryExactItemsIndexes,
                out var inventoryEmptyItemsIndexes);

            var freeCountSpace = hotBarExactItemsIndexes.Concat(inventoryExactItemsIndexes)
                                     .Sum(x => instance.Definition.MaxCount - _items[x].Count) +
                                 (hotBarEmptyItemsIndexes.Count + inventoryEmptyItemsIndexes.Count) *
                                 instance.Definition.MaxCount;

            if (freeCountSpace < instance.Count && fullInsert) // Not enough space for given count
                return false;

            var prioritizeInventoryStacks =
                HasNotFullExactStack(inventoryExactItemsIndexes, instance.Definition.MaxCount);

            if (prioritizeInventoryStacks)
                TryAddItemToExactSlots(instance, inventoryExactItemsIndexes);
            else
                TryAddItemToExactSlots(instance, hotBarExactItemsIndexes);

            if (instance.Count <= 0)
            {
                SetDirty();
                return true;
            }

            if (prioritizeInventoryStacks)
                TryAddItemToExactSlots(instance, hotBarExactItemsIndexes);
            else
                TryAddItemToExactSlots(instance, inventoryExactItemsIndexes);

            if (instance.Count <= 0)
            {
                SetDirty();
                return true;
            }

            TryAddItemToEmptySlots(instance, hotBarEmptyItemsIndexes);

            if (instance.Count <= 0)
            {
                SetDirty();
                return true;
            }

            TryAddItemToEmptySlots(instance, inventoryEmptyItemsIndexes);

            SetDirty();
            return true;
        }

        [Server]
        public override bool TryAddNewItem(ItemDefinitionSO definition, int count)
        {
            if (!definition)
                return false;

            CollectPrioritizedInsertIndexes(item => item.Definition == definition,
                out var hotBarExactItemsIndexes,
                out var hotBarEmptyItemsIndexes,
                out var inventoryExactItemsIndexes,
                out var inventoryEmptyItemsIndexes);

            var freeCountSpace = hotBarExactItemsIndexes.Concat(inventoryExactItemsIndexes)
                                     .Sum(x => definition.MaxCount - _items[x].Count) +
                                 (hotBarEmptyItemsIndexes.Count + inventoryEmptyItemsIndexes.Count) *
                                 definition.MaxCount;

            if (freeCountSpace < count) // Not enough space for given count
                return false;

            var prioritizeInventoryStacks = HasNotFullExactStack(inventoryExactItemsIndexes, definition.MaxCount);

            if (prioritizeInventoryStacks)
                TryAddCountToExactSlots(definition, inventoryExactItemsIndexes, ref count);
            else
                TryAddCountToExactSlots(definition, hotBarExactItemsIndexes, ref count);

            if (count <= 0)
            {
                SetDirty();
                return true;
            }

            if (prioritizeInventoryStacks)
                TryAddCountToExactSlots(definition, hotBarExactItemsIndexes, ref count);
            else
                TryAddCountToExactSlots(definition, inventoryExactItemsIndexes, ref count);

            if (count <= 0)
            {
                SetDirty();
                return true;
            }

            TryAddCountToEmptySlots(definition, hotBarEmptyItemsIndexes, ref count);

            if (count <= 0)
            {
                SetDirty();
                return true;
            }

            TryAddCountToEmptySlots(definition, inventoryEmptyItemsIndexes, ref count);

            SetDirty();
            return true;
        }

        private void CollectPrioritizedInsertIndexes(System.Func<ItemInstance, bool> exactMatcher,
            out List<int> hotBarExactItemsIndexes, out List<int> hotBarEmptyItemsIndexes,
            out List<int> inventoryExactItemsIndexes, out List<int> inventoryEmptyItemsIndexes)
        {
            hotBarExactItemsIndexes = new List<int>();
            hotBarEmptyItemsIndexes = new List<int>();
            inventoryExactItemsIndexes = new List<int>();
            inventoryEmptyItemsIndexes = new List<int>();

            var hotBarSize = GetHotBarSize();
            var hotBarStartIndex = _items.Length - hotBarSize;

            for (var index = hotBarStartIndex; index < _items.Length; index++)
                AddIndexes(index, exactMatcher, hotBarExactItemsIndexes, hotBarEmptyItemsIndexes);

            for (var index = 0; index < hotBarStartIndex; index++)
                AddIndexes(index, exactMatcher, inventoryExactItemsIndexes, inventoryEmptyItemsIndexes);
        }

        private void AddIndexes(int index, System.Func<ItemInstance, bool> exactMatcher, List<int> exactItemsIndexes,
            List<int> emptyItemsIndexes)
        {
            var item = _items[index];

            if (item == null)
            {
                emptyItemsIndexes.Add(index);
                return;
            }

            if (item.Count <= 0)
            {
                item.Dispose();
                _items[index] = null;
                InvokeOnItemChanged(index);
                emptyItemsIndexes.Add(index);
                return;
            }

            if (exactMatcher(item))
                exactItemsIndexes.Add(index);
        }

        private bool HasNotFullExactStack(IEnumerable<int> indexes, int maxCount)
        {
            foreach (var index in indexes)
            {
                var item = _items[index];

                if (item != null && item.Count > 0 && item.Count < maxCount)
                    return true;
            }

            return false;
        }

        private void TryAddItemToExactSlots(ItemInstance instance, IEnumerable<int> indexes)
        {
            if (instance.Count <= 0)
                return;

            foreach (var index in indexes)
            {
                var freeItemSpace = instance.Definition.MaxCount - _items[index].Count;

                if (freeItemSpace <= 0)
                    continue;

                var countToAdd = instance.Count > freeItemSpace ? freeItemSpace : instance.Count;

                if (!_items[index].TryAddCount(countToAdd))
                    continue;

                instance.TryRemoveCount(countToAdd);

                if (instance.Count <= 0)
                    return;
            }
        }

        private void TryAddItemToEmptySlots(ItemInstance instance, IEnumerable<int> indexes)
        {
            if (instance.Count <= 0)
                return;

            foreach (var index in indexes)
            {
                var newCount = instance.Count > instance.Definition.MaxCount
                    ? instance.Definition.MaxCount
                    : instance.Count;

                if (newCount <= 0)
                    return;

                instance.TryRemoveCount(newCount);

                _items[index] = new ItemInstance(instance.Definition,
                    instance.Meta.ToDictionary(x => x.Key, y => y.Value), newCount);

                BindNewItem(index);
                InvokeOnItemChanged(index);

                if (instance.Count <= 0)
                    return;
            }
        }

        private void TryAddCountToExactSlots(ItemDefinitionSO definition, IEnumerable<int> indexes, ref int count)
        {
            if (count <= 0)
                return;

            foreach (var index in indexes)
            {
                var freeItemSpace = definition.MaxCount - _items[index].Count;

                if (freeItemSpace <= 0)
                    continue;

                var countToAdd = count > freeItemSpace ? freeItemSpace : count;

                if (!_items[index].TryAddCount(countToAdd))
                    continue;

                count -= countToAdd;

                if (count <= 0)
                    return;
            }
        }

        private void TryAddCountToEmptySlots(ItemDefinitionSO definition, IEnumerable<int> indexes, ref int count)
        {
            if (count <= 0)
                return;

            foreach (var index in indexes)
            {
                var newCount = count > definition.MaxCount
                    ? definition.MaxCount
                    : count;

                if (newCount <= 0)
                    return;

                _items[index] = new ItemInstance(definition, newCount);
                count -= newCount;

                BindNewItem(index);
                InvokeOnItemChanged(index);

                if (count <= 0)
                    return;
            }
        }

        private int GetHotBarSize()
        {
            _playerSelector ??= GetComponent<PlayerSelector>();

            if (!_playerSelector)
                return 0;

            return Mathf.Clamp(_playerSelector.HotBarSize, 0, _items.Length);
        }
    }
}
