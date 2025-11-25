namespace GameAssembly.ItemsSystem
{
    /// <summary>
    /// All methods call on server and clients via RPC so it's wouldn't sync with new players
    /// </summary>
    public interface IItemBehaviour
    {
        /// <summary>
        /// All methods call on server and called client
        /// </summary>
        virtual void OnAttack(ItemContext ctx)
        {
        }

        /// <summary>
        /// All methods call on server and clients via RPC so it's wouldn't sync with new players
        /// </summary>
        virtual void OnAim(ItemContext ctx)
        {
        }

        /// <summary>
        /// All methods call on server and clients via RPC so it's wouldn't sync with new players
        /// </summary>
        virtual void OnDrop(ItemContext ctx)
        {
        }

        /// <summary>
        /// All methods call on server and clients via RPC so it's wouldn't sync with new players
        /// </summary>
        virtual void OnPickup(ItemContext ctx)
        {
        }

        /// <summary>
        /// All methods call on server and clients via RPC so it's wouldn't sync with new players
        /// </summary>
        virtual void OnEquip(ItemContext ctx)
        {
        }

        /// <summary>
        /// All methods call on server and clients via RPC so it's wouldn't sync with new players
        /// </summary>
        virtual void OnUnequip(ItemContext ctx)
        {
        }

        /// <summary>
        /// All methods call on server and called client.
        /// </summary>
        virtual void OnSelect(ItemContext ctx)
        {
        }

        /// <summary>
        /// All methods call on server and called client<br/>
        /// Works only when item was naturally deselected, not dropped or moved in inventory
        /// </summary>
        virtual void OnDeselect(ItemContext ctx)
        {
        }

        /// <summary>
        /// All methods call on server and clients via RPC so it's wouldn't sync with new players
        /// </summary>
        virtual void OnItemInventoryMoved(ItemContext ctx) // MAYBE
        {
        }

        /// <summary>
        /// All methods call on server and clients via RPC so it's wouldn't sync with new players
        /// </summary>
        virtual void OnItemInventoryTaken(ItemContext ctx) // MAYBE
        {
        }

        /// <summary>
        /// All methods call on server and clients via RPC so it's wouldn't sync with new players
        /// </summary>
        virtual void OnTick(ItemContext ctx)
        {
        }
    }
}