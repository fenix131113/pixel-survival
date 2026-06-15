using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameAssembly.InventorySystem;
using GameAssembly.InventorySystem.View;
using GameAssembly.PlayerSystem;
using GameAssembly.UiSystem.Data;
using Mirror;
using PlayerSystem;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
// ReSharper disable TailRecursiveCall

namespace GameAssembly.UiSystem
{
    public class UiManager : MonoBehaviour
    {
        public static UiManager Instance;

        [SerializeField] private GameObject inventoriesPanel;
        [SerializeField] private EscMenuView escMenuView;

        [Inject] private InputSystem_Actions _input;
        [Inject] private FloatingLabel _floatingLabel;

        private PlayerLocalInventoryManager _playerPlayerLocalInventoryManager;
        private PlayerSelector _playerSelector;

        private readonly List<IUiMenu> _openedMenus = new();

        private void Awake() => Instance = this;

        private void Start()
        {
            if (!NetworkClient.active)
                return;

            StartCoroutine(WaitForPlayer());
            Bind();
        }

        private void OnDestroy()
        {
            if (!NetworkClient.active)
                return;

            Expose();
        }

        public void OpenRequest(IUiMenu menu)
        {
            if (menu == null || _openedMenus.Contains(menu))
                return;

            if (!CanOpenMenuOverOpenedMenus(menu))
                return;

            menu.Open();
            _openedMenus.Add(menu);

            if (menu is IUiInventory)
                inventoriesPanel.SetActive(true);
        }

        public void CloseRequest(IUiMenu menu)
        {
            if (!_openedMenus.Contains(menu))
                return;

            menu.Close();
            _openedMenus.Remove(menu);
            
            if(!_openedMenus.Any(x => x is IUiInventory))
            {
                inventoriesPanel.SetActive(false);
                _floatingLabel.Hide();
            }

            if(menu.GetMenuType() == MenuType.PLAYER_INVENTORY)
            {
                var invMenu = _openedMenus.FirstOrDefault(x => x is IUiInventory);
                if(invMenu != null)
                    CloseRequest(invMenu);
            }
        }

        private void CancelRequest(IUiMenu menu)
        {
            if (!_openedMenus.Contains(menu))
                return;

            menu.Cancel();
            _openedMenus.Remove(menu);
        }

        public bool IsMenuTypeOpened(MenuType menuType) => _openedMenus.Any(x => x.GetMenuType() == menuType);

        public void Client_CloseAllOpenedMenus(bool includeEscMenu = false)
        {
            if (_openedMenus.Count == 0)
                return;

            while (true)
            {
                var menuToClose = _openedMenus.LastOrDefault(menu =>
                    includeEscMenu || menu.GetMenuType() != MenuType.ESC_MENU);

                if (menuToClose == null)
                    break;

                CancelRequest(menuToClose);
            }

            if (!_openedMenus.Any(x => x is IUiInventory))
                inventoriesPanel.SetActive(false);
        }
        
        private bool CanOpenMenuOverOpenedMenus(IUiMenu menuToOpen)
        {
            var menuTypeToOpen = menuToOpen.GetMenuType();
            return _openedMenus.All(openedMenu => IsMenuTypeAllowedOnTop(openedMenu, menuTypeToOpen));
        }

        private static bool IsMenuTypeAllowedOnTop(IUiMenu openedMenu, MenuType menuTypeToOpen)
        {
            var allowedMenuTypesOnTop = openedMenu.GetAllowedMenuTypesOnTop();
            return allowedMenuTypesOnTop != null && allowedMenuTypesOnTop.Contains(menuTypeToOpen);
        }

        /// <returns>Max 2 inventories</returns>
        public List<IUiInventory> GetOpenedInventoriesUi()
        {
            if (_openedMenus.Count == 0)
                return new List<IUiInventory>();

            var openedInventories = new List<IUiInventory>();

            for (var i = _openedMenus.Count - 1; i >= 0; i--)
            {
                if (_openedMenus[i] is IUiInventory iInv)
                    openedInventories.Add(iInv);

                if (openedInventories.Count == 2)
                    break;
            }

            return openedInventories;
        }

        public bool TryFastTransferBetweenOpenedInventories(NetworkIdentity sourceInventoryIdentity,
            int sourceCellIndex)
        {
            if (!NetworkClient.localPlayer)
                return false;
            
            if (!IsShiftPressed() || !sourceInventoryIdentity)
                return false;

            var sourceInventory = sourceInventoryIdentity.GetComponent<IInventory>();
            var sourceItem = sourceInventory?.GetItemByIndex(sourceCellIndex);

            if (sourceItem == null)
                return false;

            var openedInventories = GetOpenedInventoriesUi();

            if (openedInventories.Count == 1 &&
                openedInventories[0].GetMenuType() ==
                MenuType.PLAYER_INVENTORY) // Moving items between hot bar and inventory
            {
                var items = sourceInventory.GetItems().ToList();

                var hotBarStartIndex = sourceInventory.GetItems().Count() - _playerSelector.HotBarSize;

                if (sourceCellIndex < hotBarStartIndex)
                    for (var i = 0; i < _playerSelector.HotBarSize; i++)
                    {
                        var index = hotBarStartIndex + i;

                        if (items[index] != null && items[index].Definition != sourceItem.Definition)
                            continue;

                        if (items[index] != null && items[index].Definition.MaxCount - items[index].Count == 0)
                            continue;

                        _playerPlayerLocalInventoryManager.Cmd_PlaceFromOneCellToAnother(sourceInventoryIdentity, sourceCellIndex,
                            sourceInventoryIdentity, index, false);

                        return true;
                    }
                else // Move item from hot bar to inventory
                    _playerPlayerLocalInventoryManager.Cmd_AddInSameInventoryExceptGivenItemAndRange(sourceInventoryIdentity,
                        sourceCellIndex, hotBarStartIndex - 1);

                return true;
            }

            // Moving items between two opened inventories

            var targetInventory = openedInventories.FirstOrDefault(x => x.GetInventory() != sourceInventory);
            var targetIdentity = targetInventory?.GetNetworkIdentity();
            
            if (targetInventory == null || !targetIdentity)
                return false;
            
            var inventoryManager = NetworkClient.localPlayer.GetComponent<PlayerLocalInventoryManager>();

            if (!inventoryManager)
                return false;

            inventoryManager.Cmd_CombineItemWithInventory(sourceInventoryIdentity, sourceCellIndex, targetIdentity);
            return true;
        }

        private static bool IsShiftPressed() => Keyboard.current?.leftShiftKey.isPressed == true ||
                                                Keyboard.current?.rightShiftKey.isPressed == true;

        private void OnCancelClicked(InputAction.CallbackContext callbackContext)
        {
            if (_openedMenus.Count > 0)
            {
                CancelRequest(_openedMenus.Last());
                return;
            }

            if (escMenuView)
                OpenRequest(escMenuView);
        }

        private void Bind()
        {
            _input.Player.Cancel.performed += OnCancelClicked;
        }

        private void Expose()
        {
            _input.Player.Cancel.performed -= OnCancelClicked;
        }

        private IEnumerator WaitForPlayer()
        {
            while (!NetworkClient.localPlayer)
                yield return null;

            _playerSelector = NetworkClient.localPlayer.GetComponent<PlayerSelector>();
            _playerPlayerLocalInventoryManager = NetworkClient.localPlayer.GetComponent<PlayerLocalInventoryManager>();
        }
    }
}
