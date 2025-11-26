using System.Linq;
using GameAssembly.CraftSystem.Data;
using GameAssembly.InventorySystem;
using Mirror;

namespace GameAssembly.CraftSystem
{
    public class CraftManager : NetworkBehaviour
    {
        private IInventory _inventory;
        
        #region Server

        public override void OnStartServer() => _inventory = GetComponent<IInventory>();

        [Command]
        public void Cmd_TryCraftItem(CraftRecipeSO recipe, int amount) => Server_TryCraftItem(recipe, amount);

        [Server]
        public void Server_TryCraftItem(CraftRecipeSO recipe, int amount = 1)
        {
            var canCraft = recipe.CraftGroups.All(group => _inventory.HasItem(group.ItemDefinition, group.Count));
            
            if(!canCraft || !_inventory.CanAddNewItem(recipe.ResultItem, recipe.ResultCount))
                return;

            foreach (var group in recipe.CraftGroups)
                _inventory.TryRemoveItem(group.ItemDefinition, group.Count);

            _inventory.TryAddNewItem(recipe.ResultItem, recipe.ResultCount);
        }

        #endregion
    }
}