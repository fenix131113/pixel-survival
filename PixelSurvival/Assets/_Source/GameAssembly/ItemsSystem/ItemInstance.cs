using System;
using System.Collections.Generic;
using GameAssembly.ItemsSystem.Data;
using GameAssembly.Utils;
using Mirror;
using UnityEngine;

namespace GameAssembly.ItemsSystem
{
    public class ItemInstance : IDisposable
    {
        public readonly ItemDefinitionSO Definition;
        public int Count { get; private set; }

        private readonly Dictionary<string, string> _meta = new();

        public IReadOnlyDictionary<string, string> Meta => _meta;

        public event Action OnItemChanged;
        public event Action OnZeroItems;

        public ItemInstance(ItemDefinitionSO itemDefinition, int count = 1)
        {
            Definition = itemDefinition;
            Count = count;
        }

        public ItemInstance(ItemDefinitionSO definition, Dictionary<string, string> meta, int count = 1)
        {
            Definition = definition;
            _meta = meta;
            Count = count;
        }

        public ItemInstance Copy() => new(Definition, _meta, Count);

        public bool TryAddMeta(string key, string value)
        {
            if (!_meta.TryAdd(key, value))
                return false;
            
            OnItemChanged?.Invoke();
            return true;

        }

        public bool TryRemoveMeta(string key)
        {
            if (!_meta.ContainsKey(key))
                return false;

            _meta.Remove(key);
            OnItemChanged?.Invoke();
            return true;
        }

        public bool MatchAllMeta(IEnumerable<KeyValuePair<string, string>> meta)
        {
            if (_meta == null && meta == null)
                return true;
            if ((_meta == null && meta != null) || (_meta != null && meta == null))
                return false;

#if UNITY_EDITOR
            Debug.Assert(meta != null, nameof(meta) + " != null");
#endif

            foreach (var pair in meta)
            {
                if (_meta.TryGetValue(pair.Key, out var value) && value == pair.Value)
                    continue;

                return false;
            }

            return true;
        }

        public bool TryAddCount(int count)
        {
            if (Count + count > Definition.MaxCount)
                return false;

            Count += count;
            OnItemChanged?.Invoke();

            return true;
        }

        public bool TryRemoveCount(int count)
        {
            if (Count - count < 0)
                return false;

            Count -= count;
            OnItemChanged?.Invoke();

            if (Count == 0)
            {
                OnItemChanged = null;
                OnZeroItems?.Invoke();
            }

            return true;
        }

        public bool TryAddFromAnotherItem(ItemInstance instance, bool ignoreMeta = false)
        {
            if (Count == Definition.MaxCount || (!ignoreMeta && !MatchAllMeta(instance.Meta)))
                return false;

            var freeSpace = Definition.MaxCount - Count;

            if (instance.Count > freeSpace)
            {
                TryAddCount(freeSpace);
                instance.TryRemoveCount(freeSpace);
            }
            else
            {
                TryAddCount(instance.Count);
                instance.TryRemoveCount(instance.Count);
            }
            
            OnItemChanged?.Invoke();
            return true;
        }

        public void Dispose()
        {
            OnItemChanged = null;
            OnZeroItems = null;
        }
    }

    public static class ItemInstanceReaderWriter
    {
        public static void WriteItemInstance(this NetworkWriter writer, ItemInstance item)
        {
            writer.WriteString(item.Definition.name);
            writer.WriteInt(item.Count);

            writer.WriteInt(item.Meta.Count);

            foreach (var kv in item.Meta)
            {
                writer.WriteString(kv.Key);
                writer.WriteString(kv.Value);
            }
        }

        public static ItemInstance ReadItemInstance(this NetworkReader reader)
        {
            var defName = reader.ReadString();
            var count = reader.ReadInt();

            var metaCount = reader.ReadInt();
            var meta = new Dictionary<string, string>(metaCount);

            for (var i = 0; i < metaCount; i++)
            {
                var key = reader.ReadString();
                var value = reader.ReadString();
                meta[key] = value;
            }

            var def = Resources.Load<ItemDefinitionSO>(AssetsPaths.ITEMS_CONFIGS_PATH + "/" + defName);

            return new ItemInstance(def, meta, count);
        }
    }
}