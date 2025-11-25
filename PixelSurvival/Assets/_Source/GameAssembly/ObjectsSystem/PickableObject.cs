using GameAssembly.InventorySystem;
using GameAssembly.ItemsSystem;
using GameAssembly.Utils;
using Mirror;
using TMPro;
using UnityEngine;

namespace GameAssembly.ObjectsSystem
{
    public class PickableObject : NetworkBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private TMP_Text counter;
        [SerializeField] private LayerMask triggerLayers;

        public ItemInstance Item { get; private set; }

        public override void OnSerialize(NetworkWriter writer, bool initialState) => writer.Write(Item);

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            Item = reader.Read<ItemInstance>();
            Draw();
        }
        
        public void Initialize(ItemInstance item)
        {
            Item = item;
            Draw();
        }

        private void Draw()
        {
            spriteRenderer.sprite = Item.Definition.Icon;
            counter.text = Item.Count.ToString();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!isServer || !LayerService.CheckLayersEquality(other.gameObject.layer, triggerLayers) ||
                !other.TryGetComponent<IInventory>(out var inventory))
                return;

            if (!inventory.TryAddItemFromInstance(Item, false))
                return;

            if (Item.Count == 0)
                NetworkServer.Destroy(gameObject);
            else
            {
                SetDirty();
                Draw();
            }
        }
    }
}