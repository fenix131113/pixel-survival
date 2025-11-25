using UnityEngine;

namespace GameAssembly.ItemsSystem.Items
{
    public class DebugBehaviour : IItemBehaviour
    {
        public void OnAttack(ItemContext ctx)
        {
            Debug.Log("OnAttack");
        }

        public void OnAim(ItemContext ctx)
        {
            Debug.Log("OnAim");
        }

        public void OnDrop(ItemContext ctx)
        {
            Debug.Log("OnDrop");
        }

        public void OnPickup(ItemContext ctx)
        {
            Debug.Log("OnPickup");
        }

        public void OnEquip(ItemContext ctx)
        {
            Debug.Log("OnEquip");
        }

        public void OnUnequip(ItemContext ctx)
        {
            Debug.Log("OnUnequip");
        }

        public void OnSelect(ItemContext ctx)
        {
            Debug.Log("OnSelect");
        }

        public void OnDeselect(ItemContext ctx)
        {
            Debug.Log("OnDeselect");
        }

        public void OnItemInventoryMoved(ItemContext ctx)
        {
            Debug.Log("OnItemInventoryMoved");
        }

        public void OnItemInventoryTaken(ItemContext ctx)
        {
            Debug.Log("OnItemInventoryTaken");
        }

        public void OnTick(ItemContext ctx)
        {
            Debug.Log("OnTick");
        }
    }
}