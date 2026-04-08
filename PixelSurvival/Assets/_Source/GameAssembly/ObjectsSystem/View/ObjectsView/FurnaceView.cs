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
using UnityEngine.UI;
using VContainer;

namespace GameAssembly.ObjectsSystem.View.ObjectsView
{
    public class FurnaceView : MonoBehaviour, IUiInventory
    {
        [SerializeField] private ItemCell cellPrefab;
        [SerializeField] private GameObject furnaceMenu;
        [SerializeField] private Transform cellsParent;
        [SerializeField] private Image smeltProgressFill;
        [SerializeField] private PlayerInventoryView playerInventoryView;
        [SerializeField] private MenuType menuType = MenuType.FURNACE;
        [SerializeField] private MenuType[] allowedMenuTypesOnTop = Array.Empty<MenuType>();

        [Inject] private IVariablesResolver<PlayerVariableBlockerType, Action, Action> _variables;

        private Furnace _currentFurnace;
        private IHealth _currentHealth;
        private readonly List<ItemCell> _cells = new();
        private bool _isBind;

        private readonly IVariableBlocker<PlayerVariableBlockerType> _furnaceBlocker =
            new PlayerVariableBlocker(PlayerVariableBlockerType.MOVEMENT, PlayerVariableBlockerType.INTERACT,
                PlayerVariableBlockerType.LOOK, PlayerVariableBlockerType.BUILD, PlayerVariableBlockerType.ATTACK);

        public event Action OnMenuCanceled;

        private void OnDestroy()
        {
            if (NetworkClient.active)
            {
                ExposeSmeltingProgress();
                ExposeInventory();
            }
        }

        public void Initialize(Furnace furnace)
        {
            if (!NetworkClient.active)
                return;

            ExposeSmeltingProgress();
            _currentFurnace = furnace;
            _currentHealth = furnace.GetComponent<IHealth>();
            BindSmeltingProgress();
            ActivateCells();
        }

        public void Open()
        {
            if (!_currentFurnace || !NetworkClient.active)
                return;

            furnaceMenu.SetActive(true);
            _variables.RegisterBlocker(_furnaceBlocker);
            BindFurnaceBreaking();
        }

        public void Close()
        {
            UiManager.Instance?.CloseRequest(playerInventoryView);
            furnaceMenu.SetActive(false);
            _furnaceBlocker.Dispose();
            ExposeFurnaceBreaking();
            ExposeSmeltingProgress();
            ExposeInventory();
        }

        public void Cancel()
        {
            Close();
            OnMenuCanceled?.Invoke();
        }

        public bool IsOpen() => furnaceMenu.activeSelf;

        public MenuType GetMenuType() => menuType;

        public IReadOnlyCollection<MenuType> GetAllowedMenuTypesOnTop() => allowedMenuTypesOnTop ?? Array.Empty<MenuType>();

        public IInventory GetInventory() => _currentFurnace;
        public NetworkIdentity GetNetworkIdentity() => _currentFurnace?.netIdentity;

        private void ActivateCells()
        {
            if (!_currentFurnace)
                return;

            _isBind = true;

            for (var i = 0; i < _currentFurnace.GetInventorySize(); i++)
            {
                if (_cells.Count < i + 1)
                    SpawnNewCell();

                _cells[i].Initialize(_currentFurnace.GetComponent<NetworkIdentity>(), i);
                _cells[i].gameObject.SetActive(true);
            }
        }

        private void OnCurrentFurnaceBroken()
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

        private void BindFurnaceBreaking()
        {
            if (_currentHealth == null)
                return;

            _currentHealth.OnZeroHealth += OnCurrentFurnaceBroken;
        }

        private void ExposeFurnaceBreaking()
        {
            if (_currentHealth == null)
                return;

            _currentHealth.OnZeroHealth -= OnCurrentFurnaceBroken;
        }

        private void BindSmeltingProgress()
        {
            if (_currentFurnace == null)
                return;

            _currentFurnace.OnSmeltProgressChanged += OnSmeltProgressChanged;
            OnSmeltProgressChanged(_currentFurnace.SmeltProgress01);
        }

        private void ExposeSmeltingProgress()
        {
            if (_currentFurnace != null)
                _currentFurnace.OnSmeltProgressChanged -= OnSmeltProgressChanged;

            OnSmeltProgressChanged(0f);
        }

        private void OnSmeltProgressChanged(float progress01)
        {
            if (smeltProgressFill != null)
                smeltProgressFill.fillAmount = Mathf.Clamp01(progress01);
        }
    }
}
