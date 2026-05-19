using System.Collections;
using System.Linq;
using GameAssembly.CraftSystem.Data;
using GameAssembly.InventorySystem;
using GameAssembly.WorldSystem;
using Mirror;

namespace GameAssembly.CraftSystem
{
    public class CraftManager : NetworkBehaviour
    {
        private IInventory _inventory;

        #region Client

        private void Start()
        {
            if(!isClient || _inventory != null)
                return;

            StartCoroutine(WaitForPlayer());
        }

        private IEnumerator WaitForPlayer()
        {
            while(!NetworkClient.localPlayer)
                yield return null;

            _inventory = NetworkClient.localPlayer.GetComponent<IInventory>();
        }
        
        #endregion
        
        #region Server

        public override void OnStartServer() => _inventory = GetComponent<IInventory>();

        [Command]
        public void Cmd_TryCraftItem(CraftRecipeSO recipe, int amount) => Server_TryCraftItem(recipe, amount);

        [Server]
        public void Server_TryCraftItem(CraftRecipeSO recipe, int amount = 1)
        {
            if (recipe == null || amount <= 0 || _inventory == null)
                return;

            var worldCreateManager = WorldCreateManager.Instance;
            if (!worldCreateManager || !worldCreateManager.Server_IsRecipeUnlocked(recipe))
                return;

            if (!CanCraft(recipe, amount))
                return;

            foreach (var group in recipe.CraftGroups)
                _inventory.TryRemoveItem(group.ItemDefinition, group.Count * amount);

            _inventory.TryAddNewItem(recipe.ResultItem, recipe.ResultCount * amount);
            worldCreateManager.Server_RegisterCraftCompleted(recipe);
        }

        public bool CanCraft(CraftRecipeSO recipe, int amount = 1)
        {
            if (recipe == null || amount <= 0 || _inventory == null)
                return false;

            var worldCreateManager = WorldCreateManager.Instance;
            if (worldCreateManager)
            {
                if (!isServer && !worldCreateManager.HasCraftStateSnapshot)
                    return false;

                if (!worldCreateManager.IsRecipeUnlocked(recipe))
                    return false;
            }

            var canCraft = recipe.CraftGroups.All(group => _inventory.HasItem(group.ItemDefinition, group.Count * amount));
            
            return canCraft && _inventory.CanAddNewItem(recipe.ResultItem, recipe.ResultCount * amount);
        }

        #endregion
    }
}
