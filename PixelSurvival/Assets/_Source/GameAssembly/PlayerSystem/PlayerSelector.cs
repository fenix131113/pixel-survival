using System;
using GameAssembly.InventorySystem;
using GameAssembly.ItemsSystem;
using Mirror;
using PlayerSystem;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using Utils;
using VContainer;

namespace GameAssembly.PlayerSystem
{
    public class PlayerSelector : NetworkBehaviour
    {
        [SerializeField] private PlayerLocalInventoryManager playerLocalInventoryManager;

        [field: SerializeField] public int HotBarSize { get; private set; } = 10;

        [field: SyncVar(hook = nameof(OnSelectionChanged))]
        public int SelectedIndex { get; private set; } = -1;

        /// <returns>
        /// <b>True</b> - If selected one of the cells and this cell contains any item, otherwise <b>False</b>
        /// </returns>>
        public bool IsSelectedItem => IsSelectionActive &&
                                      _inventory.GetItemByIndex(GetInventoryIndexByHotBarIndex(SelectedIndex)) != null;

        /// <returns>
        /// <b>True</b> - If selected one of the cell, if not - <b>False</b>
        /// </returns>
        public bool IsSelectionActive => SelectedIndex > -1;

        [Inject] private InputSystem_Actions _input;

        private IInventory _inventory;

        /// <summary>
        /// Called on server and clients. Firstly on client who triggered the event
        /// </summary>
        public event Action<int, int> OnSelectionChangedEvent;

        /// <summary>
        /// Called on server and current client
        /// </summary>
        public event Action OnSelectedItemChanged;

        private void OnDestroy()
        {
            if (!isServerOnly && isLocalPlayer)
                Client_Expose();

            if (isServerOnly)
                Server_Expose();
        }

        public ItemInstance GetSelectedItem() =>
            _inventory.GetItemByIndex(GetInventoryIndexByHotBarIndex(SelectedIndex));

        // Called on current client and server
        // Here is no verification for double call cause it already exists in OnStartServer()
        private void OnInventoryChanged(int index)
        {
            if (index != GetInventoryIndexByHotBarIndex(SelectedIndex))
                return;

            CheckForBehaviour(-1);
        }

        // Called on server and OTHER clients (not on called client)
        private void OnSelectionChanged(int oldValue, int newValue)
        {
            if (!isLocalPlayer)
                OnSelectionChangedEvent?.Invoke(oldValue, newValue);

            if (!isServerOnly)
                return;

            OnSelectionChangedEvent?.Invoke(oldValue, newValue);
            CheckForBehaviour(oldValue);
        }

        private void CheckForBehaviour(int oldItemInvIndex)
        {
            if (oldItemInvIndex > -1 &&
                _inventory.GetItemByIndex(GetInventoryIndexByHotBarIndex(oldItemInvIndex)) != null &&
                ItemRegistry.Instance.ItemHasBehaviour(
                    _inventory.GetItemByIndex(GetInventoryIndexByHotBarIndex(oldItemInvIndex)).Definition,
                    out var behaviourOld))
            {
                behaviourOld.OnDeselect(new ItemContext(
                    _inventory.GetItemByIndex(GetInventoryIndexByHotBarIndex(oldItemInvIndex)), _inventory,
                    netIdentity));
            }

            if (IsSelectedItem &&
                ItemRegistry.Instance.ItemHasBehaviour(GetSelectedItem().Definition, out var behaviourNew))
                behaviourNew.OnSelect(new ItemContext(GetSelectedItem(), _inventory, netIdentity));
        }

        /// <summary>
        /// Called on server and current client
        /// </summary>
        private void OnCombiningItemsReplace(NetworkIdentity invIdentity1, int firstIndex, NetworkIdentity invIdentity2,
            int secondIndex)
        {
            if (invIdentity1 != netIdentity)
                return;

            if (firstIndex != GetInventoryIndexByHotBarIndex(SelectedIndex) &&
                secondIndex != GetInventoryIndexByHotBarIndex(SelectedIndex))
                return;

            var inv1 = invIdentity1.GetComponent<IInventory>();
            var inv2 = invIdentity2.GetComponent<IInventory>();


            if (firstIndex == GetInventoryIndexByHotBarIndex(SelectedIndex) &&
                inv1.GetItemByIndex(secondIndex) != null &&
                ItemRegistry.Instance.ItemHasBehaviour(inv1.GetItemByIndex(secondIndex).Definition, out var b1))
            {
                b1.OnDeselect(new ItemContext(inv1.GetItemByIndex(secondIndex), inv1, netIdentity));
            }
            else if (inv1.GetItemByIndex(firstIndex) != null &&
                     ItemRegistry.Instance.ItemHasBehaviour(inv1.GetItemByIndex(firstIndex).Definition, out var b2))
            {
                b2.OnDeselect(new ItemContext(inv1.GetItemByIndex(secondIndex), inv1, netIdentity));
            }
        }

        private int GetInventoryIndexByHotBarIndex(int hotBarIndex)
        {
            return hotBarIndex > -1 ? _inventory.GetInventorySize() - HotBarSize + hotBarIndex : -1;
        }

        #region Client

        public override void OnStartLocalPlayer()
        {
            if (isServerOnly || !isLocalPlayer)
                return;

            _inventory = GetComponent<IInventory>();

            ObjectInjector.Inject(this);
            Client_Bind();
        }

        /// <summary>Works only on client </summary>
        /// <param name="index">-1 = null</param>
        public void SetSelection(int index) // Only client method because script SyncDirection is Client To Server
        {
            var temp = SelectedIndex;

            if (index == SelectedIndex)
            {
                SelectedIndex = -1;
                OnSelectionChangedEvent?.Invoke(temp, SelectedIndex);
                CheckForBehaviour(temp);
                return;
            }

            SelectedIndex = index;
            CheckForBehaviour(temp);
            OnSelectionChangedEvent?.Invoke(temp, index);
        }

        private void OnSelectionClicked(InputAction.CallbackContext callbackContext)
        {
            if (callbackContext.ReadValue<float>() < 0.1f)
                return;

            var key = callbackContext.control as KeyControl;

            var index = int.Parse(key!.name);

            if (index == 0)
                index = 9;
            else
                index--;

            if (index < 0 || index >= HotBarSize)
                return;

            SetSelection(index);
        }

        private void Client_Bind()
        {
            _input.Player.Selection.performed += OnSelectionClicked;
            _inventory.OnItemChanged += OnInventoryChanged;
            playerLocalInventoryManager.OnCombiningCellsReplace += OnCombiningItemsReplace;
        }

        private void Client_Expose()
        {
            _input.Player.Selection.performed -= OnSelectionClicked;
            _inventory.OnItemChanged -= OnInventoryChanged;
            playerLocalInventoryManager.OnCombiningCellsReplace -= OnCombiningItemsReplace;
        }

        #endregion

        #region Server

        public override void OnStartServer()
        {
            _inventory = GetComponent<IInventory>();

            if (isServerOnly)
                Server_Bind();
        }

        private void Server_Bind()
        {
            _inventory.OnItemChanged += OnInventoryChanged;
            playerLocalInventoryManager.OnCombiningCellsReplace += OnCombiningItemsReplace;
        }

        private void Server_Expose()
        {
            _inventory.OnItemChanged -= OnInventoryChanged;
            playerLocalInventoryManager.OnCombiningCellsReplace -= OnCombiningItemsReplace;
        }

        #endregion
    }
}