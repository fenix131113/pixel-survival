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
    public class PlayerInventoryView : NetworkBehaviour, IUiInventory
    {
        [SerializeField] private ItemCell cellPrefab;
        [SerializeField] private GameObject inventoryPanel;
        [SerializeField] private Transform cellsParent;
        [SerializeField] private Transform hotBarParent;
        [SerializeField] private MenuType[] allowedMenuTypesOnTop = { MenuType.CHEST, MenuType.FURNACE, MenuType.CAMPFIRE };

        [Inject] private MovingItem _movingItem;
        [Inject] private InputSystem_Actions _input;
        [Inject] private IVariablesResolver<PlayerVariableBlockerType, Action, Action> _variablesResolver;

        private PlayerSelector _playerSelector;
        private IInventory _inventory;
        private ItemCell[] _cells;
        private ItemCell[] _hotBarCells;

        private readonly IVariableBlocker<PlayerVariableBlockerType> _inventoryBlocker =
            new PlayerVariableBlocker(PlayerVariableBlockerType.MOVEMENT, PlayerVariableBlockerType.BUILD,
                PlayerVariableBlockerType.ATTACK, PlayerVariableBlockerType.INTERACT, PlayerVariableBlockerType.LOOK);

        public IReadOnlyCollection<ItemCell> HotBarCells => _hotBarCells;

        public event Action OnMenuCanceled;

        public void Start()
        {
            if (!isServerOnly)
                StartCoroutine(WaitForPlayer());

            if (!isClient)
                return;

            ObjectInjector.Inject(this);
            _movingItem?.Prewarm();
            Bind();
        }

        private void OnDestroy()
        {
            if (isClient)
                Expose();
        }

        private void OnInventoryClicked(InputAction.CallbackContext callbackContext)
        {
            if (inventoryPanel.activeSelf)
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
            inventoryPanel.SetActive(true);

            _variablesResolver.RegisterBlocker(_inventoryBlocker);
        }

        public void Close()
        {
            inventoryPanel.SetActive(false);

            _movingItem.ForceClose();
            _inventoryBlocker.Dispose();
        }

        public void Cancel()
        {
            Close();
            OnMenuCanceled?.Invoke();
        }

        public bool IsOpen() => inventoryPanel.activeSelf;
        public MenuType GetMenuType() => MenuType.PLAYER_INVENTORY;
        public IReadOnlyCollection<MenuType> GetAllowedMenuTypesOnTop()
        {
            EnsureRequiredOverlayMenusAllowed();
            return allowedMenuTypesOnTop;
        }

        public IInventory GetInventory() => _inventory;
        public NetworkIdentity GetNetworkIdentity() => NetworkClient.localPlayer ? NetworkClient.localPlayer : null;

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

        private void EnsureRequiredOverlayMenusAllowed()
        {
            if (allowedMenuTypesOnTop == null || allowedMenuTypesOnTop.Length == 0)
            {
                allowedMenuTypesOnTop = new[] { MenuType.CHEST, MenuType.FURNACE, MenuType.CAMPFIRE };
                return;
            }

            var hasChest = false;
            var hasFurnace = false;
            var hasCampfire = false;

            foreach (var menuType in allowedMenuTypesOnTop)
            {
                hasChest |= menuType == MenuType.CHEST;
                hasFurnace |= menuType == MenuType.FURNACE;
                hasCampfire |= menuType == MenuType.CAMPFIRE;
            }

            if (hasChest && hasFurnace && hasCampfire)
                return;

            var missingCount = (hasChest ? 0 : 1) + (hasFurnace ? 0 : 1) + (hasCampfire ? 0 : 1);
            var result = new MenuType[allowedMenuTypesOnTop.Length + missingCount];
            allowedMenuTypesOnTop.CopyTo(result, 0);

            var index = allowedMenuTypesOnTop.Length;
            if (!hasChest)
                result[index++] = MenuType.CHEST;
            if (!hasFurnace)
                result[index++] = MenuType.FURNACE;
            if (!hasCampfire)
                result[index] = MenuType.CAMPFIRE;

            allowedMenuTypesOnTop = result;
        }
    }
}
