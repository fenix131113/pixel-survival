using System;
using System.Collections.Generic;
using GameAssembly.PlayerSystem.Data;
using GameAssembly.PlayerSystem.Variables;
using GameAssembly.UiSystem;
using GameAssembly.UiSystem.Data;
using GameAssembly.Utils.VariablesSystem;
using PlayerSystem;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace GameAssembly.CraftSystem.View
{
    public class CraftPanelView : MonoBehaviour, IUiMenu
    {
        [SerializeField] private GameObject craftPanel;
        [SerializeField] private MenuType[] allowedMenuTypesOnTop = Array.Empty<MenuType>();

        [Inject] private InputSystem_Actions _input;
        [Inject] private IVariablesResolver<PlayerVariableBlockerType, Action, Action> _variablesResolver;
        
        private readonly IVariableBlocker<PlayerVariableBlockerType> _craftBlocker =
            new PlayerVariableBlocker(PlayerVariableBlockerType.MOVEMENT, PlayerVariableBlockerType.BUILD,
                PlayerVariableBlockerType.ATTACK, PlayerVariableBlockerType.INTERACT, PlayerVariableBlockerType.LOOK);

        public event Action OnMenuCanceled;

        private void Start() => Bind();

        private void OnDestroy() => Expose();

        private void OnCraftMenuClicked(InputAction.CallbackContext callbackContext)
        {
            if (IsOpen())
                UiManager.Instance.CloseRequest(this);
            else
                UiManager.Instance.OpenRequest(this);
        }

        private void Bind() => _input.Player.CraftMenu.performed += OnCraftMenuClicked;

        private void Expose() => _input.Player.CraftMenu.performed -= OnCraftMenuClicked;
        public void Open()
        {
            craftPanel.SetActive(true);
            
            _variablesResolver.RegisterBlocker(_craftBlocker);
        }

        public void Close()
        {
            craftPanel.SetActive(false);
            
            _craftBlocker.Dispose();
        }

        public void Cancel() => Close();

        public bool IsOpen() => craftPanel.activeSelf;

        public MenuType GetMenuType() => MenuType.CRAFT_MENU;

        public IReadOnlyCollection<MenuType> GetAllowedMenuTypesOnTop() => allowedMenuTypesOnTop ?? Array.Empty<MenuType>();
    }
}
