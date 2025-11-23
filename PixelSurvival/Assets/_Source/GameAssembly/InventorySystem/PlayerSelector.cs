using System;
using GameAssembly.ItemsSystem;
using Mirror;
using PlayerSystem;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using Utils;
using VContainer;

namespace GameAssembly.InventorySystem
{
    public class PlayerSelector : NetworkBehaviour
    {
        [field: SerializeField] public int HotBarSize { get; private set; } = 10;

        [field: SyncVar(hook = nameof(OnSelectionChanged))]
        public int SelectedIndex { get; private set; } = -1;

        public bool IsSelectedSomething => SelectedIndex > -1;

        [Inject] private InputSystem_Actions _input;

        private IInventory _inventory;

        /// <summary>
        /// Called on server and clients. Firstly on called client
        /// </summary>
        public event Action<int, int> OnSelectionChangedEvent;

        /// <summary>
        /// Called on server and clients
        /// </summary>
        public event Action OnSelectedItemChanged;

        private void OnDestroy()
        {
            if (!isServerOnly && isLocalPlayer)
                Client_Expose();

            Server_Expose();
        }

        public override void OnStartLocalPlayer()
        {
            if (isServerOnly || !isLocalPlayer)
                return;

            _inventory = GetComponent<IInventory>();

            ObjectInjector.Inject(this);
            Client_Bind();
        }

        public override void OnStartServer()
        {
            _inventory = GetComponent<IInventory>();
            Server_Bind();
        }

        public ItemInstance GetSelectedItem() =>
            _inventory.GetItemByIndex(GetInventoryIndexByHotBarIndex(SelectedIndex));

        /// <param name="index">-1 = null</param>
        public void SetSelection(int index)
        {
            var temp = SelectedIndex;

            if (index == SelectedIndex)
            {
                SelectedIndex = -1;
                OnSelectionChangedEvent?.Invoke(temp, SelectedIndex);
                return;
            }

            SelectedIndex = index;
            OnSelectionChangedEvent?.Invoke(temp, index);
        }

        [ClientRpc]
        private void Rpc_OnInventoryChanged(int index) => OnInventoryChanged(index);

        private void OnInventoryChanged(int index)
        {
            if (index != GetInventoryIndexByHotBarIndex(SelectedIndex))
                return;

            OnSelectedItemChanged?.Invoke();

            if (isServer)
            {
                Debug.Log("Check rpc for not recursive call on host!");
                Rpc_OnInventoryChanged(index);
            }
        }

        // Called on server and OTHER clients (not on called client)
        private void OnSelectionChanged(int oldValue, int newValue)
        {
            OnSelectionChangedEvent?.Invoke(oldValue, newValue);
        }

        private void OnSelectionClicked(InputAction.CallbackContext callbackContext)
        {
            if (callbackContext.ReadValue<float>() < 0.1f)
                return;

            var key = callbackContext.control as KeyControl;

            var idx = int.Parse(key!.name);

            if (idx == 0)
                idx = 9;
            else
                idx--;

            if (idx < 0 || idx >= HotBarSize)
                return;

            SetSelection(idx);
        }

        private int GetInventoryIndexByHotBarIndex(int hotBarIndex) =>
            IsSelectedSomething ? _inventory.GetInventorySize() + hotBarIndex : -1;

        private void Client_Bind()
        {
            _input.Player.Selection.performed += OnSelectionClicked;
        }

        private void Client_Expose()
        {
            _input.Player.Selection.performed -= OnSelectionClicked;
        }

        private void Server_Bind()
        {
            _inventory.OnItemChanged += OnInventoryChanged;
        }

        private void Server_Expose()
        {
            _inventory.OnItemChanged -= OnInventoryChanged;
        }
    }
}