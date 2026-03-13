using System;
using System.Collections.Generic;
using GameAssembly.InventorySystem;
using GameAssembly.InventorySystem.View;
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
    public class ChestView : MonoBehaviour, IUiInventory
    {
        [SerializeField] private ItemCell cellPrefab;
        [SerializeField] private GameObject chestMenu;
        [SerializeField] private Transform cellsParent;
        [SerializeField] private PlayerInventoryView playerInventoryView;
        [SerializeField] private MenuType menuType;

        [Inject] private IVariablesResolver<PlayerVariableBlockerType, Action, Action> _variables;

        private BaseInventory _currentInventory;
        private readonly List<ItemCell> _cells = new();
        private bool _isBind;

        private readonly IVariableBlocker<PlayerVariableBlockerType> _chestBlocker =
            new PlayerVariableBlocker(PlayerVariableBlockerType.MOVEMENT, PlayerVariableBlockerType.INTERACT,
                PlayerVariableBlockerType.LOOK, PlayerVariableBlockerType.BUILD, PlayerVariableBlockerType.ATTACK);

        public event Action OnMenuCanceled;

        private void OnDestroy()
        {
            if(NetworkClient.active)
                ExposeInventory();
        }

        public void Initialize(BaseInventory inventory)
        {
            if(!NetworkClient.active)
                return;
            
            _currentInventory = inventory;
            ActivateCells();
        }
        
        public void Open()
        {
            if(!_currentInventory || !NetworkClient.active)
                return;
            
            chestMenu.SetActive(true);
            _variables.RegisterBlocker(_chestBlocker);
        }

        public void Close()
        {
            UiManager.Instance.CloseRequest(playerInventoryView);
            chestMenu.SetActive(false);
            _chestBlocker.Dispose();
            ExposeInventory();
        }

        public void Cancel()
        {
            Close();
            OnMenuCanceled?.Invoke();
        }

        public bool IsOpen() => chestMenu.activeSelf;

        public MenuType GetMenuType() => menuType;

        public IInventory GetInventory() => _currentInventory;
        public NetworkIdentity GetNetworkIdentity() => _currentInventory?.netIdentity;

        private void ActivateCells()
        {
            if(!_currentInventory)
                return;
            
            for (var i = 0; i < _currentInventory.GetInventorySize(); i++)
            {
                if (_cells.Count < i + 1)
                    SpawnNewCell();
                
                _cells[i].Initialize(_currentInventory.GetComponent<NetworkIdentity>(), i);
                _cells[i].gameObject.SetActive(true);
            }
        }

        private void SpawnNewCell()
        {
            _cells.Add(Instantiate(cellPrefab, cellsParent));
        }

        private void ExposeInventory()
        {
            if(!_isBind)
                return;
            
            _isBind = false;
            _cells.ForEach(x =>
            {
                x.gameObject.SetActive(false);
                x.Expose();
            });
        }
    }
}