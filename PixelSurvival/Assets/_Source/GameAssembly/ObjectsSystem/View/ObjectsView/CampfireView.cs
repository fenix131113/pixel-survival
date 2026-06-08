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
    public class CampfireView : MonoBehaviour, IUiInventory
    {
        [SerializeField] private ItemCell cellPrefab;
        [SerializeField] private GameObject campfireMenu;
        [SerializeField] private Transform cellsParent;
        [SerializeField] private Image cookProgressFill;
        [SerializeField] private PlayerInventoryView playerInventoryView;
        [SerializeField] private MenuType menuType = MenuType.CAMPFIRE;
        [SerializeField] private MenuType[] allowedMenuTypesOnTop = Array.Empty<MenuType>();

        [Inject] private IVariablesResolver<PlayerVariableBlockerType, Action, Action> _variables;

        private Campfire _currentCampfire;
        private IHealth _currentHealth;
        private readonly List<ItemCell> _cells = new();
        private bool _isBind;

        private readonly IVariableBlocker<PlayerVariableBlockerType> _campfireBlocker =
            new PlayerVariableBlocker(PlayerVariableBlockerType.MOVEMENT, PlayerVariableBlockerType.INTERACT,
                PlayerVariableBlockerType.LOOK, PlayerVariableBlockerType.BUILD, PlayerVariableBlockerType.ATTACK);

        public event Action OnMenuCanceled;

        private void OnDestroy()
        {
            if (NetworkClient.active)
            {
                ExposeCookingProgress();
                ExposeInventory();
            }
        }

        public void Initialize(Campfire campfire)
        {
            if (!NetworkClient.active)
                return;

            ExposeCookingProgress();
            _currentCampfire = campfire;
            _currentHealth = campfire.GetComponent<IHealth>();
            BindCookingProgress();
            ActivateCells();
        }

        public void Open()
        {
            if (!_currentCampfire || !NetworkClient.active)
                return;

            campfireMenu.SetActive(true);
            _variables.RegisterBlocker(_campfireBlocker);
            BindCampfireBreaking();
        }

        public void Close()
        {
            UiManager.Instance?.CloseRequest(playerInventoryView);
            campfireMenu.SetActive(false);
            _campfireBlocker.Dispose();
            ExposeCampfireBreaking();
            ExposeCookingProgress();
            ExposeInventory();
        }

        public void Cancel()
        {
            Close();
            OnMenuCanceled?.Invoke();
        }

        public bool IsOpen() => campfireMenu.activeSelf;

        public MenuType GetMenuType() => menuType;

        public IReadOnlyCollection<MenuType> GetAllowedMenuTypesOnTop() => allowedMenuTypesOnTop ?? Array.Empty<MenuType>();

        public IInventory GetInventory() => _currentCampfire;
        public NetworkIdentity GetNetworkIdentity() => _currentCampfire?.netIdentity;

        private void ActivateCells()
        {
            if (!_currentCampfire)
                return;

            _isBind = true;

            for (var i = 0; i < _currentCampfire.GetInventorySize(); i++)
            {
                if (_cells.Count < i + 1)
                    SpawnNewCell();

                _cells[i].Initialize(_currentCampfire.GetComponent<NetworkIdentity>(), i);
                _cells[i].gameObject.SetActive(true);
            }
        }

        private void OnCurrentCampfireBroken()
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

        private void BindCampfireBreaking()
        {
            if (_currentHealth == null)
                return;

            _currentHealth.OnZeroHealth += OnCurrentCampfireBroken;
        }

        private void ExposeCampfireBreaking()
        {
            if (_currentHealth == null)
                return;

            _currentHealth.OnZeroHealth -= OnCurrentCampfireBroken;
        }

        private void BindCookingProgress()
        {
            if (_currentCampfire == null)
                return;

            _currentCampfire.OnCookProgressChanged += OnCookProgressChanged;
            OnCookProgressChanged(_currentCampfire.CookProgress01);
        }

        private void ExposeCookingProgress()
        {
            if (_currentCampfire != null)
                _currentCampfire.OnCookProgressChanged -= OnCookProgressChanged;

            OnCookProgressChanged(0f);
        }

        private void OnCookProgressChanged(float progress01)
        {
            if (cookProgressFill != null)
                cookProgressFill.fillAmount = Mathf.Clamp01(progress01);
        }
    }
}
