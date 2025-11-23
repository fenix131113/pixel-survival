using System.Collections;
using System.Collections.Generic;
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
        [SerializeField] private Transform hotBarParent;

        [Inject] private MovingItem _movingItem;

        private PlayerSelector _playerSelector;
        private IInventory _inventory;
        private ItemCell[] _cells;
        private ItemCell[] _hotBarCells;

        public IReadOnlyCollection<ItemCell> HotBarCells => _hotBarCells;

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

                if (!inventoryPanel.activeSelf)
                    _movingItem.ForceClose();
            }

            if (Keyboard.current.yKey.wasPressedThisFrame)
                Cmd_AddItem(NetworkClient.localPlayer, ItemDatabase.Apple, 5);

            if (Keyboard.current.tKey.wasPressedThisFrame)
                Cmd_AddItem(NetworkClient.localPlayer, ItemDatabase.TestItem, 5);

            if (Keyboard.current.gKey.wasPressedThisFrame)
                foreach (var inventory in FindObjectsByType<BaseInventory>(FindObjectsInactive.Include,
                             FindObjectsSortMode.None))
                {
                    Debug.Log(
                        $"[SERVER]: {inventory.GetComponent<NetworkIdentity>().netId} - {inventory.GetItems().Count(x => x != null)}");
                }
        }

        private void SpawnCells()
        {
            _cells = new ItemCell[_inventory.GetInventorySize()];
            _hotBarCells = new ItemCell[_playerSelector.HotBarSize];

            for (var i = 0; i < _inventory.GetInventorySize() - _playerSelector.HotBarSize; i++)
            {
                _cells[i] = Instantiate(cellPrefab, cellsParent);
                _cells[i].Initialize(NetworkClient.localPlayer, i);
            }

            for (var i = 0; i < _playerSelector.HotBarSize; i++)
            {
                _cells[i + _inventory.GetInventorySize() - _playerSelector.HotBarSize] =
                    Instantiate(cellPrefab, hotBarParent);
                _hotBarCells[i] = _cells[i + _inventory.GetInventorySize() - _playerSelector.HotBarSize];
                _cells[i + _inventory.GetInventorySize() - _playerSelector.HotBarSize].Initialize(
                    NetworkClient.localPlayer,
                    i + _inventory.GetInventorySize() - _playerSelector.HotBarSize);
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
            _playerSelector = NetworkClient.localPlayer.GetComponent<PlayerSelector>();
            
            SpawnCells();
        }
    }
}