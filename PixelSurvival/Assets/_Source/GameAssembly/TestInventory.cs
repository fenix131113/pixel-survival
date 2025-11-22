using GameAssembly.Generated;
using GameAssembly.ItemsSystem;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameAssembly
{
    public class TestInventory : MonoBehaviour
    {
        private ItemInstance _firstItem;
        private ItemInstance _secondItem;
        
        public ItemInstance CurrentItem { get; private set; }

        private void Start()
        {
            _firstItem = new ItemInstance(ItemDatabase.Apple);
            CurrentItem = _firstItem;
        }

        private void Update()
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                CurrentItem = _firstItem;
            }
            else if (Keyboard.current.digit2Key.wasPressedThisFrame)
            {
                CurrentItem = _secondItem;
            }
            
            // Behaviour Listener NEED TO PLACE SOMEWHERE, Where we can correctly generate ItemContext
            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                if (CurrentItem != null && ItemRegistry.Instance.ItemHasBehaviour(CurrentItem.Definition, out var behaviour))
                {
                    behaviour.OnAim(new ItemContext(CurrentItem, null, null));
                }
            }
        }
    }
}