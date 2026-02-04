using System;
using GameAssembly.ItemsSystem;
using GameAssembly.ObjectsSystem;
using Mirror;
using UnityEngine;

namespace GameAssembly.InventorySystem
{
    public class PlayerLocalInventoryManager : NetworkBehaviour // For managing any inventory by player authority
    {
        [SerializeField] private PickableObject dropPrefab;

        private const float MAX_ITEM_DROP_DISTANCE = 5f;
        private const float ITEM_DROP_TAKE_PROTECTION_TIME = 2f;
        
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
        public void CombineCells(NetworkIdentity firstInvIdentity, int firstIndex, NetworkIdentity secondInvIdentity,
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
        public void DropItemFromInventory(NetworkIdentity inventoryIdentity, int cellIndex, Vector2 dropPosition, NetworkConnectionToClient sender = null)
        {
            var inv = inventoryIdentity.GetComponent<IInventory>();
            var item = inv.GetItemByIndex(cellIndex);
            
            if(item == null)
                return;
            
            if(Vector2.Distance(inventoryIdentity.transform.position, dropPosition) > MAX_ITEM_DROP_DISTANCE)
            {
#if UNITY_EDITOR
               Debug.LogWarning("Trying to drop an item from too long distance"); 
#endif
                return;
            }

            var defTemp = item.Definition;
            var countTemp = item.Count;
            
            if(sender == null || !inv.TryRemoveItemByIndex(cellIndex, false))
                return;
            
            var pickable = Instantiate(dropPrefab, dropPosition, Quaternion.identity);
            pickable.Initialize(new ItemInstance(defTemp, countTemp));
            NetworkServer.Spawn(pickable.gameObject);
            pickable.SetTakeProtectionForPlayer(sender.identity, ITEM_DROP_TAKE_PROTECTION_TIME);
        }

        [TargetRpc]
        private void Target_InvokeOnCombiningCellsReplace(NetworkConnectionToClient target,
            NetworkIdentity firstInvIdentity,
            int firstIndex, NetworkIdentity secondInvIdentity, int secondIndex)
        {
            if(isServer && isClient)
                return;
            
            OnCombiningCellsReplace?.Invoke(firstInvIdentity, firstIndex, secondInvIdentity, secondIndex);
        }

        [TargetRpc]
        private void Target_InvokeOnCombiningCellsChangeAmount(NetworkConnectionToClient target,
            NetworkIdentity firstInvIdentity, int firstIndex, NetworkIdentity secondInvIdentity, int secondIndex)
        {
            if(isServer && isClient)
                return;
            
            OnCombiningCellsChangeAmount?.Invoke(firstInvIdentity, firstIndex, secondInvIdentity, secondIndex);
        }
    }
}