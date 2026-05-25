namespace GameAssembly.InventorySystem
{
    public interface IInventoryExtractionLock
    {
        bool IsItemExtractionLocked { get; }
    }
}
