using GameAssembly.InventorySystem;

namespace GameAssembly.UiSystem
{
    public interface IUiInventory : IUiMenu
    {
        IInventory GetInventory();
    }
}