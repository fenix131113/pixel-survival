namespace GameAssembly.ItemsSystem
{
    public interface IItemBehaviour
    {
        virtual void OnAttack(ItemContext ctx)
        {
        }

        virtual void OnAim(ItemContext ctx)
        {
        }

        virtual void OnDrop(ItemContext ctx)
        {
        }
        
        virtual void OnPickup(ItemContext ctx)
        {
        }

        virtual void OnEquip(ItemContext ctx)
        {
        }

        virtual void OnUnequip(ItemContext ctx)
        {
        }

        virtual void OnSelect(ItemContext ctx)
        {
        }

        virtual void OnDeselect(ItemContext ctx)
        {
        }
        
        virtual void OnItemInventoryMoved(ItemContext ctx) // MAYBE
        {
        }
        
        virtual void OnItemInventoryTaken(ItemContext ctx) // MAYBE
        {
        }

        virtual void OnTick(ItemContext ctx)
        {
        }
    }
}