using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameAssembly.InventorySystem;
using GameAssembly.ItemsSystem;
using GameAssembly.ItemsSystem.Data;
using GameAssembly.Utils;
using Mirror;
using TMPro;
using UnityEngine;

namespace GameAssembly.ObjectsSystem
{
    public class PickableObject : NetworkBehaviour
    {
        [SerializeField] private ItemDefinitionSO itemDefinition;
        [SerializeField] private int itemCount;
        [SerializeField] private float despawnTime = 180f;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private TMP_Text counter;
        [SerializeField] private LayerMask triggerLayers;

        private Dictionary<NetworkIdentity, float> _takeProtections;

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
            SetDirty();
            Draw();
        }

        private void Update()
        {
            if (!isServer || _takeProtections == null || _takeProtections.Count == 0)
                return;

            foreach (var pair in _takeProtections.Where(pair => Time.time >= pair.Value))
            {
                _takeProtections.Remove(pair.Key);
                break;
            }
        }

        public override void OnStartServer()
        {
            StartCoroutine(DespawnCoroutine());
            
            if (!itemDefinition)
                return;

            Initialize(new ItemInstance(itemDefinition, itemCount));
        }

        [Server]
        public void SetTakeProtectionForPlayer(NetworkIdentity playerIdentity, float protectionTime)
        {
            _takeProtections ??= new Dictionary<NetworkIdentity, float>();

            _takeProtections.TryAdd(playerIdentity, Time.time + protectionTime);
        }

        private void Draw()
        {
            spriteRenderer.sprite = Item.Definition.Icon;
            counter.text = Item.Count.ToString();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!isServer || !LayerService.CheckLayersEquality(other.gameObject.layer, triggerLayers) ||
                !other.TryGetComponent<IInventory>(out var inventory) ||
                (_takeProtections != null && _takeProtections.ContainsKey(other.GetComponent<NetworkIdentity>())))
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

        [Server]
        private IEnumerator DespawnCoroutine()
        {
            yield return new WaitForSeconds(despawnTime);
            
            NetworkServer.Destroy(gameObject);
        }
    }
}