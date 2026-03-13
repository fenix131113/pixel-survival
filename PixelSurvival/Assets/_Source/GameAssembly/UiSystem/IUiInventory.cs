using GameAssembly.InventorySystem;
using Mirror;

namespace GameAssembly.UiSystem
{
    public interface IUiInventory : IUiMenu
    {
        IInventory GetInventory();
        NetworkIdentity GetNetworkIdentity();
    }
}