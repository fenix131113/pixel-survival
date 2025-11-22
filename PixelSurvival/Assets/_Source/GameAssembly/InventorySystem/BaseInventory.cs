using System;
using System.Collections.Generic;
using System.Linq;
using GameAssembly.ItemsSystem;
using GameAssembly.ItemsSystem.Data;
using GameAssembly.Utils;
using Mirror;
using UnityEngine;

namespace GameAssembly.InventorySystem
{
    public class BaseInventory : NetworkBehaviour, IInventory
    {
        [SerializeField] protected int inventorySize;

        private ItemInstance[] _items;

        public event Action OnInventoryChanged;
        public event Action<int> OnItemChanged;

        private void Awake() => _items = new ItemInstance[inventorySize];

        public override void OnSerialize(NetworkWriter writer, bool initialState)
        {
            writer.WriteInt(_items.Length);

            foreach (var item in _items)
            {
                if (item == null)
                {
                    writer.WriteBool(false);
                    continue;
                }

                writer.WriteBool(true);
                writer.WriteString(item.Definition.name);
                writer.WriteInt(item.Count);

                writer.WriteInt(item.Meta.Count);
                foreach (var kv in item.Meta)
                {
                    writer.WriteString(kv.Key);
                    writer.WriteString(kv.Value);
                }
            }
            
            base.OnSerialize(writer, initialState);
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            foreach (var itemInstance in _items)
                itemInstance?.Dispose();
            
            var size = reader.ReadInt();
            _items = new ItemInstance[size];

            for (var i = 0; i < size; i++)
            {
                var hasItem = reader.ReadBool();
                if (!hasItem)
                {
                    _items[i] = null;
                    continue;
                }

                var defName = reader.ReadString();
                var count = reader.ReadInt();

                var metaCount = reader.ReadInt();
                var meta = new Dictionary<string, string>(metaCount);

                for (var m = 0; m < metaCount; m++)
                {
                    var k = reader.ReadString();
                    var v = reader.ReadString();
                    meta[k] = v;
                }

                var definition = Resources.Load<ItemDefinitionSO>(
                    AssetsPaths.ITEMS_CONFIGS_PATH + "/" + defName);

                _items[i] = new ItemInstance(definition, meta, count);
                BindNewItem(i);

                OnItemChanged?.Invoke(i);
            }

            OnInventoryChanged?.Invoke();
            
            base.OnDeserialize(reader, initialState);
        }

        public int GetInventorySize() => inventorySize;

        public IEnumerable<ItemInstance> GetItems() => _items;

        [Server]
        public virtual bool TryAddItemFromInstance(ItemInstance instance, bool ignoreMeta = false)
        {
            var exactItemsIndexes = new List<int>();
            var emptyItemsIndexes = new List<int>();

            for (var index = 0; index < _items.Length; index++)
            {
                var item = _items[index];

                if (item == null)
                    emptyItemsIndexes.Add(index);
                else if (item.Definition == instance.Definition &&
                         (ignoreMeta || instance.MatchAllMeta(_items[index].Meta)))
                    exactItemsIndexes.Add(index);
            }

            var freeCountSpace = exactItemsIndexes.Sum(x => instance.Definition.MaxCount - _items[x].Count) +
                                 emptyItemsIndexes.Count * instance.Definition.MaxCount;

            if (freeCountSpace < instance.Count) // Not enough space for given count
                return false;

            foreach (var index in exactItemsIndexes)
            {
                var freeItemSpace = instance.Definition.MaxCount - _items[index].Count;

                if (instance.Count > freeItemSpace && _items[index].TryAddCount(freeItemSpace))
                {
                    instance.TryRemoveCount(freeItemSpace);
                }
                else if (_items[index].TryAddCount(instance.Count))
                {
                    instance.TryRemoveCount(instance.Count);
                    break;
                }
            }

            if (instance.Count <= 0)
            {
                SetDirty();
                return true;
            }

            foreach (var index in emptyItemsIndexes)
            {
                var newCount = instance.Count > instance.Definition.MaxCount
                    ? instance.Definition.MaxCount
                    : instance.Count;

                instance.TryRemoveCount(newCount);

                _items[index] = new ItemInstance(instance.Definition,
                    instance.Meta.ToDictionary(x => x.Key, y => y.Value), newCount);

                BindNewItem(index);

                if (instance.Count <= 0)
                {
                    InvokeOnItemChanged(index);
                    SetDirty();
                    return true;
                }
            }

            SetDirty();
            return true;
        }

        [Server]
        public virtual bool TryAddNewItem(ItemDefinitionSO definition, int count)
        {
            var exactItemsIndexes = new List<int>();
            var emptyItemsIndexes = new List<int>();

            for (var index = 0; index < _items.Length; index++)
            {
                var item = _items[index];

                if (item == null)
                    emptyItemsIndexes.Add(index);
                else if (item.Definition == definition)
                    exactItemsIndexes.Add(index);
            }

            var freeCountSpace = exactItemsIndexes.Sum(x => definition.MaxCount - _items[x].Count) +
                                 emptyItemsIndexes.Count * definition.MaxCount;

            if (freeCountSpace < count) // Not enough space for given count
                return false;

            foreach (var index in exactItemsIndexes)
            {
                var freeItemSpace = definition.MaxCount - _items[index].Count;

                if (count > freeItemSpace && _items[index].TryAddCount(freeItemSpace))
                {
                    count -= freeItemSpace;
                }
                else if (_items[index].TryAddCount(count))
                {
                    count = 0;
                    break;
                }
            }

            if (count <= 0)
            {
                OnInventoryChanged?.Invoke();
                SetDirty();
                return true;
            }

            foreach (var index in emptyItemsIndexes)
            {
                var newCount = count > definition.MaxCount
                    ? definition.MaxCount
                    : count;

                _items[index] = new ItemInstance(definition, newCount);
                count -= newCount;

                BindNewItem(index);

                if (count <= 0)
                {
                    InvokeOnItemChanged(index);
                    SetDirty();
                    return true;
                }
            }

            OnInventoryChanged?.Invoke();
            SetDirty();
            return true;
        }

        [Server]
        public virtual bool TryRemoveItem(ItemDefinitionSO definition, int count)
        {
            var exactItemsIndexes = new List<int>();
            var existSum = 0;

            for (var index = 0; index < _items.Length; index++)
            {
                var item = _items[index];

                if (item.Definition != definition)
                    continue;

                exactItemsIndexes.Add(index);
                existSum += item.Count;
            }

            if (existSum < count)
                return false;

            foreach (var index in exactItemsIndexes)
            {
                if (_items[index].Count >= count)
                {
                    _items[index].TryRemoveCount(count);
                    count = 0;
                }
                else
                {
                    count -= _items[index].Count;
                    _items[index].TryRemoveCount(_items[index].Count);
                }

                if (count <= 0)
                {
                    SetDirty();
                    return true;
                }
            }

            SetDirty();
            return true;
        }

        public virtual bool HasItem(ItemDefinitionSO item, int count = 1)
        {
            if (count <= 1)
                return _items.Any(x => x.Definition == item);

            return _items.Sum(x => x.Count) >= count;
        }

        protected virtual void InvokeOnItemChanged(int index)
        {
            OnInventoryChanged?.Invoke();
            OnItemChanged?.Invoke(index);
        }

        protected virtual void BindNewItem(int index)
        {
            _items[index].OnItemChanged += () => InvokeOnItemChanged(index);
            _items[index].OnZeroItems += () => _items[index] = null;
        }
    }
}