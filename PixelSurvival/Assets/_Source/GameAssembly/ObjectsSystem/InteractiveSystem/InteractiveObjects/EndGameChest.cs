using System;
using System.Collections.Generic;
using System.Linq;
using GameAssembly.InventorySystem;
using GameAssembly.ItemsSystem;
using GameAssembly.ItemsSystem.Data;
using GameAssembly.ObjectsSystem.View.ObjectsView;
using Mirror;
using UnityEngine;
using VContainer;

namespace GameAssembly.ObjectsSystem.InteractiveSystem.InteractiveObjects
{
    public class EndGameChest : Chest, IInventoryExtractionLock
    {
        [SerializeField] private ItemDefinitionSO[] requiredItems = new ItemDefinitionSO[4];
        [SerializeField] private bool lockInsertAfterCompletion = true;
        [SerializeField] private bool lockItemsInChestAfterComplete = true;

        [Inject] private EndGameChestView _endGameChestView;

        [field: SyncVar(hook = nameof(OnCompletionChanged))]
        public bool IsCompleted { get; private set; }
        public bool IsItemExtractionLocked => lockItemsInChestAfterComplete && IsCompleted;

        private bool _completionRaisedLocal;

        public event Action<EndGameChest> OnCompleted;

        protected override void OnValidate()
        {
            base.OnValidate();
            inventorySize = requiredItems?.Length ?? 0;
        }

        public override void Interact()
        {
            if (!_endGameChestView)
                return;

            _endGameChestView.Initialize(this);
            OpenInventoryView(_endGameChestView);
        }

        public override bool TryRemoveItem(ItemDefinitionSO definition, int count) =>
            !IsItemExtractionLocked && base.TryRemoveItem(definition, count);

        public override bool TryRemoveItemByIndex(int index, bool countRemove) =>
            !IsItemExtractionLocked && base.TryRemoveItemByIndex(index, countRemove);

        [Server]
        public override bool TryAddItemFromInstance(ItemInstance instance, bool fullInsert, bool ignoreMeta = false)
        {
            if (instance is not { Count: > 0 })
                return false;

            if (lockInsertAfterCompletion && IsCompleted)
                return false;

            if (!IsRequiredItem(instance.Definition))
                return false;

            if (HasItemAlreadyPlaced(instance.Definition))
                return false;

            var targetIndex = GetFirstEmptySlotIndex();
            return targetIndex >= 0 && TryAddItemInIndexFromInstance(instance, targetIndex, fullInsert);
        }

        [Server]
        public override bool TryAddNewItem(ItemDefinitionSO definition, int count)
        {
            if (!definition || count <= 0)
                return false;

            if (lockInsertAfterCompletion && IsCompleted)
                return false;

            if (count > 1)
                return false;

            var temp = new ItemInstance(definition, count);
            return TryAddItemFromInstance(temp, true) && temp.Count == 0;
        }

        [Server]
        public override bool TryAddItemInIndexFromInstance(ItemInstance item, int index, bool fullInsert)
        {
            if (item is not { Count: > 0 } || index < 0 || index >= _items.Length)
                return false;

            if (lockInsertAfterCompletion && IsCompleted)
                return false;

            if (!IsRequiredItem(item.Definition))
                return false;

            if (_items[index] != null)
                return false;

            if (HasItemAlreadyPlaced(item.Definition))
                return false;

            var countToMove = 1;
            if (fullInsert && item.Count > countToMove)
                return false;

            if (item.Count < countToMove)
                return false;

            _items[index] = new ItemInstance(item.Definition, item.Meta.ToDictionary(x => x.Key, y => y.Value),
                countToMove);

            BindNewItem(index);
            InvokeOnItemChanged(index);
            item.TryRemoveCount(countToMove);
            SetDirty();
            Server_CheckForCompletion();
            return true;
        }

        [Server]
        public override bool CanAddNewItem(ItemDefinitionSO definition, int count = 1)
        {
            if (!definition || count != 1)
                return false;

            if (lockInsertAfterCompletion && IsCompleted)
                return false;

            if (!IsRequiredItem(definition))
                return false;

            if (HasItemAlreadyPlaced(definition))
                return false;

            return GetFirstEmptySlotIndex() >= 0;
        }

        [Server]
        protected override void Server_Bind()
        {
            base.Server_Bind();
            OnInventoryChanged += Server_CheckForCompletion;
            Server_CheckForCompletion();
        }

        [Server]
        protected override void Server_Expose()
        {
            OnInventoryChanged -= Server_CheckForCompletion;
            base.Server_Expose();
        }

        [Server]
        private void Server_CheckForCompletion()
        {
            var isCompleteNow = IsAssemblyComplete();
            if (IsCompleted == isCompleteNow)
                return;

            IsCompleted = isCompleteNow;

            if (isCompleteNow)
                RaiseCompletionEndpointOnce();
        }

        private void OnCompletionChanged(bool oldValue, bool newValue)
        {
            if (oldValue == newValue || !newValue)
                return;

            RaiseCompletionEndpointOnce();
        }

        private void RaiseCompletionEndpointOnce()
        {
            if (_completionRaisedLocal)
                return;

            _completionRaisedLocal = true;
            OnCompleted?.Invoke(this);
            EndGameEndpoint.Raise(this);
        }

        private bool IsAssemblyComplete()
        {
            if (_items == null || requiredItems == null || requiredItems.Length == 0)
                return false;

            var requiredSet = new HashSet<ItemDefinitionSO>();
            if (requiredItems.Any(required => !required || !requiredSet.Add(required)))
            {
                return false;
            }

            var placedItems = new HashSet<ItemDefinitionSO>();
            foreach (var itemInSlot in _items)
            {
                if (itemInSlot is not { Count: > 0 })
                    continue;

                if (!requiredSet.Contains(itemInSlot.Definition))
                    return false;

                if (!placedItems.Add(itemInSlot.Definition))
                    return false;
            }

            return placedItems.Count == requiredSet.Count;
        }

        private bool IsRequiredItem(ItemDefinitionSO definition)
        {
            if (!definition || requiredItems == null)
                return false;

            return requiredItems.Any(t => t == definition);
        }

        private int GetFirstEmptySlotIndex()
        {
            if (_items == null)
                return -1;

            for (var index = 0; index < _items.Length; index++)
            {
                if (_items[index] == null)
                    return index;
            }

            return -1;
        }

        private bool HasItemAlreadyPlaced(ItemDefinitionSO definition)
        {
            if (!definition || _items == null)
                return false;

            return _items.Any(slotItem => slotItem != null && slotItem.Definition == definition && slotItem.Count > 0);
        }
    }
}
