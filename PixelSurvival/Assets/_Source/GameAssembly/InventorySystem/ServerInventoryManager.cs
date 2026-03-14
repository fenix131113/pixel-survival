using GameAssembly.ItemsSystem;
using GameAssembly.ObjectsSystem;
using Mirror;
using UnityEngine;

namespace GameAssembly.InventorySystem
{
    public class ServerInventoryManager : NetworkBehaviour
    {
        [SerializeField] private PickableObject dropPrefab;
        
        private const float MAX_ITEM_DROP_DISTANCE = 3f;
        private const float ITEM_DROP_TAKE_PROTECTION_TIME = 2f;
        
        [Server]
        public void Server_DropItemFromInventory(NetworkIdentity inventoryIdentity, int cellIndex, Vector2 dropPosition,
            NetworkConnectionToClient sender = null)
        {
            if (!inventoryIdentity)
                return;

            var inv = inventoryIdentity.GetComponent<IInventory>();
            var item = inv?.GetItemByIndex(cellIndex);

            if (item == null)
                return;

            if (Vector2.Distance(inventoryIdentity.transform.position, dropPosition) > MAX_ITEM_DROP_DISTANCE)
            {
#if UNITY_EDITOR
                Debug.LogWarning("Trying to drop an item from too long distance");
#endif
                return;
            }

            var defTemp = item.Definition;
            var countTemp = item.Count;

            if (!inv.TryRemoveItemByIndex(cellIndex, false))
                return;

            var pickable = Instantiate(dropPrefab, dropPosition, Quaternion.identity);
            pickable.Initialize(new ItemInstance(defTemp, countTemp));
            NetworkServer.Spawn(pickable.gameObject);
            
            if(sender != null)
                pickable.SetTakeProtectionForPlayer(sender.identity, ITEM_DROP_TAKE_PROTECTION_TIME);
        }
    }
}