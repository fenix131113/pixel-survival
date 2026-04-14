using System;
using System.Collections.Generic;
using GameAssembly.HealthSystem;
using GameAssembly.InventorySystem;
using GameAssembly.InventorySystem.View;
using GameAssembly.ObjectsSystem.InteractiveSystem.InteractiveObjects;
using GameAssembly.PlayerSystem.Data;
using GameAssembly.PlayerSystem.Variables;
using GameAssembly.PlayerSystem.View;
using GameAssembly.UiSystem;
using GameAssembly.UiSystem.Data;
using GameAssembly.Utils.VariablesSystem;
using Mirror;
using UnityEngine;
using VContainer;

namespace GameAssembly.ObjectsSystem.View.ObjectsView
{
    public class EndGameChestView : MonoBehaviour, IUiInventory
    {
        [SerializeField] private ItemCell cellPrefab;
        [SerializeField] private GameObject chestMenu;
        [SerializeField] private Transform cellsParent;
        [SerializeField] private PlayerInventoryView playerInventoryView;
        [SerializeField] private MenuType menuType = MenuType.CHEST;
        [SerializeField] private MenuType[] allowedMenuTypesOnTop = Array.Empty<MenuType>();

        [Inject] private IVariablesResolver<PlayerVariableBlockerType, Action, Action> _variables;

        private EndGameChest _currentInventory;
        private IHealth _currentHealth;
        private readonly List<ItemCell> _cells = new();
        private bool _isBind;

        private readonly IVariableBlocker<PlayerVariableBlockerType> _chestBlocker =
            new PlayerVariableBlocker(PlayerVariableBlockerType.MOVEMENT, PlayerVariableBlockerType.INTERACT,
                PlayerVariableBlockerType.LOOK, PlayerVariableBlockerType.BUILD, PlayerVariableBlockerType.ATTACK);

        public event Action OnMenuCanceled;

        private void OnDestroy()
        {
            if (NetworkClient.active)
                ExposeInventory();
        }

        public void Initialize(EndGameChest inventory)
        {
            if (!NetworkClient.active)
                return;

            _currentInventory = inventory;
            _currentHealth = inventory ? inventory.GetComponent<IHealth>() : null;
            ActivateCells();
        }

        public void Open()
        {
            if (!_currentInventory || !NetworkClient.active)
                return;

            chestMenu.SetActive(true);
            _variables.RegisterBlocker(_chestBlocker);
            BindChestBreaking();
        }

        public void Close()
        {
            UiManager.Instance?.CloseRequest(playerInventoryView);
            chestMenu.SetActive(false);
            _chestBlocker.Dispose();
            ExposeChestBreaking();
            ExposeInventory();
        }

        public void Cancel()
        {
            Close();
            OnMenuCanceled?.Invoke();
        }

        public bool IsOpen() => chestMenu.activeSelf;

        public MenuType GetMenuType() => menuType;

        public IReadOnlyCollection<MenuType> GetAllowedMenuTypesOnTop() =>
            allowedMenuTypesOnTop ?? Array.Empty<MenuType>();

        public IInventory GetInventory() => _currentInventory;
        public NetworkIdentity GetNetworkIdentity() => _currentInventory?.netIdentity;

        private void ActivateCells()
        {
            if (!_currentInventory)
                return;

            _isBind = true;

            for (var i = 0; i < _currentInventory.GetInventorySize(); i++)
            {
                if (_cells.Count < i + 1)
                    SpawnNewCell();

                _cells[i].Initialize(_currentInventory.GetComponent<NetworkIdentity>(), i);
                _cells[i].gameObject.SetActive(true);
            }
        }

        private void OnCurrentChestBroken()
        {
            Close();
        }

        private void SpawnNewCell()
        {
            _cells.Add(Instantiate(cellPrefab, cellsParent));
        }

        private void ExposeInventory()
        {
            if (!_isBind)
                return;

            _isBind = false;
            _cells.ForEach(x =>
            {
                x.gameObject.SetActive(false);
                x.Expose();
            });
        }

        private void BindChestBreaking()
        {
            if (_currentHealth == null)
                return;

            _currentHealth.OnZeroHealth += OnCurrentChestBroken;
        }

        private void ExposeChestBreaking()
        {
            if (_currentHealth == null)
                return;

            _currentHealth.OnZeroHealth -= OnCurrentChestBroken;
        }
    }
}
