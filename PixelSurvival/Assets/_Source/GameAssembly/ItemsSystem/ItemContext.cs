using GameAssembly.InventorySystem;
using Mirror;

namespace GameAssembly.ItemsSystem
{
    public class ItemContext
    {
        public ItemInstance Instance { get; private set; }
        public IInventory Inventory { get; private set; }
        public NetworkIdentity PlayerIdentity { get; private set; }

        public ItemContext(ItemInstance instance, IInventory inventory, NetworkIdentity playerIdentity)
        {
            Instance = instance;
            Inventory = inventory;
            PlayerIdentity = playerIdentity;
        }
    }
}