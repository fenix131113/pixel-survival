using System;
using System.Linq;
using GameAssembly.ItemsSystem;
using GameAssembly.ObjectsSystem;
using GameAssembly.Utils;
using Mirror;
using UnityEngine;
using VContainer;

namespace GameAssembly.InventorySystem
{
    public class PlayerLocalInventoryManager : NetworkBehaviour // For managing any inventory by player authority
    {
        [Inject] private ServerInventoryManager _serverInventoryManager;

        private void Start() => ObjectInjector.Inject(this);

        /// <summary>
        /// Called on the server and current client. Firstly on the server
        /// </summary>
        public event Action<NetworkIdentity, int, NetworkIdentity, int> OnCombiningCellsReplace;

        /// <summary>
        /// Called on the server and current client. Firstly on the server
        /// </summary>
        public event Action<NetworkIdentity, int, NetworkIdentity, int> OnCombiningCellsChangeAmount;

        /// <summary>
        /// Combine FIRST in SECOND! First item can not be null
        /// </summary>
        [Command]
        public void Cmd_CombineCells(NetworkIdentity firstInvIdentity, int firstIndex,
            NetworkIdentity secondInvIdentity,
            int secondIndex, bool ignoreMeta)
        {
            Server_CombineCells(firstInvIdentity, firstIndex, secondInvIdentity, secondIndex, ignoreMeta);
        }

        /// <summary>
        /// Combine FIRST in SECOND! First item can not be null
        /// </summary>
        [Server]
        public void Server_CombineCells(NetworkIdentity firstInvIdentity, int firstIndex,
            NetworkIdentity secondInvIdentity,
            int secondIndex, bool ignoreMeta)
        {
            var inv1 = firstInvIdentity.GetComponent<IInventory>();
            var inv2 = secondInvIdentity.GetComponent<IInventory>();
            var item1 = inv1.GetItemByIndex(firstIndex);
            var item2 = inv2.GetItemByIndex(secondIndex);

            if (item1 == null)
                return;

            if (item2 == null || item1.Definition != item2.Definition) // Change place between two of given
            {
                if (!inv2.TryRemoveItemByIndex(secondIndex, false))
                    return;

                inv2.TryAddItemInIndexFromInstance(item1.Copy(), secondIndex,
                    true); // Used Copy() for prevent disposing original item1. If you don't - that will be disposed in another cell
                inv1.TryRemoveItemByIndex(firstIndex, false);

                if (item2 != null)
                    inv1.TryAddItemInIndexFromInstance(item2, firstIndex, true);

                OnCombiningCellsReplace?.Invoke(firstInvIdentity, firstIndex, secondInvIdentity, secondIndex);
                Target_InvokeOnCombiningCellsReplace(connectionToClient, firstInvIdentity, firstIndex,
                    secondInvIdentity, secondIndex);
            }
            else if (item2.Count < item2.Definition.MaxCount) // Add count
            {
                if (!item2.TryAddFromAnotherItem(item1, ignoreMeta))
                    return;

                OnCombiningCellsChangeAmount?.Invoke(firstInvIdentity, firstIndex, secondInvIdentity, secondIndex);
                Target_InvokeOnCombiningCellsChangeAmount(connectionToClient, firstInvIdentity, firstIndex,
                    secondInvIdentity, secondIndex);
            }
        }

        [Command]
        public void DropItemFromInventory(NetworkIdentity inventoryIdentity, int cellIndex, Vector2 dropPosition,
            NetworkConnectionToClient sender = null)
        {
            _serverInventoryManager.Server_DropItemFromInventory(inventoryIdentity, cellIndex, dropPosition, sender);
        }

        [Command]
        public void Cmd_CombineItemWithInventory(NetworkIdentity firstInvIdentity, int firstIndex,
            NetworkIdentity secondInvIdentity)
        {
            Server_CombineItemWithInventory(firstInvIdentity, firstIndex, secondInvIdentity);
        }

        [Server]
        public void Server_CombineItemWithInventory(NetworkIdentity firstInvIdentity, int firstIndex,
            NetworkIdentity secondInvIdentity)
        {
            if (!firstInvIdentity || !secondInvIdentity)
                return;

            var inv = firstInvIdentity.GetComponent<IInventory>();
            var inv2 = secondInvIdentity.GetComponent<IInventory>();
            var item = inv?.GetItemByIndex(firstIndex);

            if (item == null || inv2 == null)
                return;

            inv2.TryAddItemFromInstance(item, false);
        }

        [Command]
        public void Cmd_PlaceFromOneCellToAnother(NetworkIdentity firstInvIdentity, int firstIndex,
            NetworkIdentity secondInvIdentity, int secondIndex, bool ignoreMeta)
        {
            Server_PlaceFromOneCellToAnother(firstInvIdentity, firstIndex, secondInvIdentity, secondIndex, ignoreMeta);
        }

        [Server]
        public void Server_PlaceFromOneCellToAnother(NetworkIdentity firstInvIdentity, int firstIndex,
            NetworkIdentity secondInvIdentity, int secondIndex, bool ignoreMeta)
        {
            var inv1 = firstInvIdentity.GetComponent<IInventory>();
            var inv2 = secondInvIdentity.GetComponent<IInventory>();
            var item1 = inv1.GetItemByIndex(firstIndex);
            var item2 = inv2.GetItemByIndex(secondIndex);

            if (item1 == null)
                return;

            if (item2 == null) // Place first item in empty second cell
            {
                inv2.TryAddItemInIndexFromInstance(item1.Copy(), secondIndex,
                    true); // Used Copy() for prevent disposing original item1. If you don't - that will be disposed in another cell
                inv1.TryRemoveItemByIndex(firstIndex, false);
            }
            else if (item2.Definition == item1.Definition && item2.Count < item2.Definition.MaxCount) // Add count
            {
                item2.TryAddFromAnotherItem(item1, ignoreMeta);
            }
        }

        /// <summary>
        /// Add item again. For example can move item from hot bar to main inventory at start (except hot bar range). You can select index after which items will be unable to put in
        /// </summary>
        [Command]
        public void Cmd_AddInSameInventoryExceptGivenItemAndRange(NetworkIdentity inventoryIdentity, int itemIndex,
            int ignoreAfterIndexExclude)
        {
            if (!inventoryIdentity)
                return;

            var inv = inventoryIdentity.GetComponent<IInventory>();

            if (inv == null)
                return;

            var item = inv.GetItemByIndex(itemIndex);
            var items = inv.GetItems().ToList();

            var firstAvailableCellIndex = -1;

            for (var i = 0; i < items.Count; i++)
            {
                if (i > ignoreAfterIndexExclude)
                    break;

                if (i == itemIndex)
                    continue;

                if (firstAvailableCellIndex == -1 && items[i] == null)
                    firstAvailableCellIndex = i;

                if (items[i] == null || items[i].Definition != item.Definition ||
                    items[i].Count >= items[i].Definition.MaxCount)
                    continue;

                firstAvailableCellIndex = i;
                break;
            }

            if (firstAvailableCellIndex == -1)
                return;

            Server_PlaceFromOneCellToAnother(inventoryIdentity, itemIndex, inventoryIdentity, firstAvailableCellIndex,
                false);
        }

        [TargetRpc]
        private void Target_InvokeOnCombiningCellsReplace(NetworkConnectionToClient target,
            NetworkIdentity firstInvIdentity,
            int firstIndex, NetworkIdentity secondInvIdentity, int secondIndex)
        {
            if (isServer && isClient)
                return;

            OnCombiningCellsReplace?.Invoke(firstInvIdentity, firstIndex, secondInvIdentity, secondIndex);
        }

        [TargetRpc]
        private void Target_InvokeOnCombiningCellsChangeAmount(NetworkConnectionToClient target,
            NetworkIdentity firstInvIdentity, int firstIndex, NetworkIdentity secondInvIdentity, int secondIndex)
        {
            if (isServer && isClient)
                return;

            OnCombiningCellsChangeAmount?.Invoke(firstInvIdentity, firstIndex, secondInvIdentity, secondIndex);
        }
    }
}