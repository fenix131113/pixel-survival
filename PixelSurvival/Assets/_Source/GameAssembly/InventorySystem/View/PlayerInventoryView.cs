using System.Collections;
using System.Linq;
using GameAssembly.Generated;
using GameAssembly.ItemsSystem.Data;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using Utils;
using VContainer;

namespace GameAssembly.InventorySystem.View
{
    public class PlayerInventoryView : NetworkBehaviour
    {
        [SerializeField] private ItemCell cellPrefab;
        [SerializeField] private GameObject inventoryPanel;
        [SerializeField] private Transform cellsParent;

        [Inject] private MovingItem _movingItem;
        
        private IInventory _inventory;
        private ItemCell[] _cells;

        public void Start()
        {
            if (!isServerOnly)
            {
                StartCoroutine(WaitForPlayer());
            }

            if (isClient)
            {
                ObjectInjector.Inject(this);
            }
        }

        private void Update()
        {
            if (NetworkServer.active && !NetworkClient.active)
                return;

            if (Keyboard.current.cKey.wasPressedThisFrame)
            {
                inventoryPanel.SetActive(!inventoryPanel.activeSelf);
                
                if(!inventoryPanel.activeSelf)
                    _movingItem.ForceClose();
            }

            if (Keyboard.current.yKey.wasPressedThisFrame)
                Cmd_AddItem(NetworkClient.localPlayer, ItemDatabase.Apple, 5);

            if (Keyboard.current.tKey.wasPressedThisFrame)
                Cmd_AddItem(NetworkClient.localPlayer, ItemDatabase.TestItem, 5);

            if (Keyboard.current.gKey.wasPressedThisFrame)
                foreach (var inventory in FindObjectsByType<BaseInventory>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    Debug.Log($"[SERVER]: {inventory.GetComponent<NetworkIdentity>().netId} - {inventory.GetItems().Count(x => x != null)}");
                }
        }

        private void SpawnCells()
        {
            _cells = new ItemCell[_inventory.GetInventorySize()];

            for (var i = 0; i < _inventory.GetInventorySize(); i++)
            {
                _cells[i] = Instantiate(cellPrefab, cellsParent);
                _cells[i].Initialize(NetworkClient.localPlayer, i);
            }
        }

        [Command(requiresAuthority = false)]
        private void Cmd_AddItem(NetworkIdentity identity, ItemDefinitionSO item, int amount)
        {
            identity.GetComponent<BaseInventory>().TryAddNewItem(item, amount);
        }

        private IEnumerator WaitForPlayer()
        {
            while (!NetworkClient.localPlayer)
                yield return null;
            
            _inventory = NetworkClient.localPlayer.GetComponent<IInventory>();
            SpawnCells();
        }
    }
}