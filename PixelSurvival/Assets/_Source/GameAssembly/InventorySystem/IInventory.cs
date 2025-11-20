namespace GameAssembly.InventorySystem
{
    public interface IInventory
    {
        void AddItem();
        void RemoveItem();
        bool HasItem();
    }
}