using Mirror;

namespace GameAssembly.InventorySystem
{
    public class PlayerLocalInventoryManager : NetworkBehaviour // For managing any inventory by player authority
    {
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
            }
            else if (item2.Count < item2.Definition.MaxCount) // Add count
            {
                item2.TryAddFromAnotherItem(item1, ignoreMeta);
            }
        }
    }
}