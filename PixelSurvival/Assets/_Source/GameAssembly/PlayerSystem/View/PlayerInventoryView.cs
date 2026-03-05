using System;
using System.Collections;
using System.Collections.Generic;
using GameAssembly.InventorySystem;
using GameAssembly.InventorySystem.View;
using GameAssembly.PlayerSystem.Data;
using GameAssembly.PlayerSystem.Variables;
using GameAssembly.UiSystem;
using GameAssembly.UiSystem.Data;
using GameAssembly.Utils;
using GameAssembly.Utils.VariablesSystem;
using Mirror;
using PlayerSystem;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace GameAssembly.PlayerSystem.View
{
    public class PlayerInventoryView : NetworkBehaviour, IUiInventory // TODO: Make blockers for player attack, build and etc. when open inventories
    {
        [SerializeField] private ItemCell cellPrefab;
        [SerializeField] private GameObject inventoryPanel;
        [SerializeField] private GameObject inventoriesContentPanel;
        [SerializeField] private Transform cellsParent;
        [SerializeField] private Transform hotBarParent;

        [Inject] private MovingItem _movingItem;
        [Inject] private InputSystem_Actions _input;
        [Inject] private IVariablesResolver<PlayerVariableBlockerType, Action, Action> _variablesResolver;

        private PlayerSelector _playerSelector;
        private IInventory _inventory;
        private ItemCell[] _cells;
        private ItemCell[] _hotBarCells;

        private readonly IVariableBlocker<PlayerVariableBlockerType> _inventoryBlocker =
            new PlayerVariableBlocker(PlayerVariableBlockerType.MOVEMENT, PlayerVariableBlockerType.BUILD,
                PlayerVariableBlockerType.ATTACK, PlayerVariableBlockerType.INTERACT);

        public IReadOnlyCollection<ItemCell> HotBarCells => _hotBarCells;

        public void Start()
        {
            if (!isServerOnly)
                StartCoroutine(WaitForPlayer());

            if (!isClient)
                return;

            ObjectInjector.Inject(this);
            Bind();
        }

        private void OnDestroy()
        {
            if (isClient)
                Expose();
        }

        private void OnInventoryClicked(InputAction.CallbackContext callbackContext)
        {
            if (inventoriesContentPanel.activeSelf)
                UiManager.Instance.CloseRequest(this);
            else
                UiManager.Instance.OpenRequest(this);
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

        public void Open()
        {
            inventoriesContentPanel.SetActive(true);
            inventoryPanel.SetActive(true);

            _variablesResolver.RegisterBlocker(_inventoryBlocker);
        }

        public void Close()
        {
            inventoriesContentPanel.SetActive(false);
            inventoryPanel.SetActive(false);

            _movingItem.ForceClose();
            _inventoryBlocker.Dispose();
        }

        public bool IsOpen() => inventoriesContentPanel.activeSelf;
        public MenuType GetMenuType() => MenuType.PLAYER_INVENTORY;

        public IInventory GetInventory() => _inventory;

        private void Bind() => _input.Player.Inventory.performed += OnInventoryClicked;

        private void Expose() => _input.Player.Inventory.performed -= OnInventoryClicked;

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