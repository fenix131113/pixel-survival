using System.Collections.Generic;
using System.Linq;
using GameAssembly.InventorySystem;
using GameAssembly.UiSystem.Data;
using PlayerSystem;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace GameAssembly.UiSystem
{
    public class UiManager : MonoBehaviour
    {
        public static UiManager Instance;

        [Inject] private InputSystem_Actions _input;

        private readonly List<IUiMenu> _openedMenus = new();

        private void Awake() => Instance = this;

        private void Start() => Bind();

        private void OnDestroy() => Expose();

        public void OpenRequest(IUiMenu menu)
        {
            if(_openedMenus.Contains(menu))
                return;
            
            menu.Open();
            _openedMenus.Add(menu);
        }
        
        public void CloseRequest(IUiMenu menu)
        {
            if(!_openedMenus.Contains(menu))
                return;
            
            menu.Close();
            _openedMenus.Remove(menu);
        }

        public bool IsMenuTypeOpened(MenuType menuType) => _openedMenus.Any(x => x.GetMenuType() == menuType);

        /// <returns>Max 2 inventories</returns>
        public List<IInventory> GetOpenedInventories()
        {
            if (_openedMenus.Count > 0)
                return new List<IInventory>();
            
            var openedInventories = new List<IInventory>();

            for (var i = _openedMenus.Count - 1; i >= 0 ; i++)
            {
                if (_openedMenus[i] is IUiInventory iInv)
                    openedInventories.Add(iInv.GetInventory());
                
                if(openedInventories.Count == 2)
                    break;
            }

            return openedInventories;
        }

        private void OnCancelClicked(InputAction.CallbackContext callbackContext)
        {
            if (_openedMenus.Count > 0)
            {
                CloseRequest(_openedMenus.Last());
                return;
            }
            
            //TODO: add open esc menu
        }

        private void Bind()
        {
            _input.Player.Cancel.performed += OnCancelClicked;
        }

        private void Expose()
        {
            _input.Player.Cancel.performed -= OnCancelClicked;
        }
    }
}