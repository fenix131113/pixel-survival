using System;
using System.Collections.Generic;
using GameAssembly.ObjectsSystem.InteractiveSystem.InteractiveObjects;
using GameAssembly.PlayerSystem.Data;
using GameAssembly.PlayerSystem.Variables;
using GameAssembly.UiSystem;
using GameAssembly.UiSystem.Data;
using GameAssembly.Utils.VariablesSystem;
using UnityEngine;
using VContainer;

namespace GameAssembly.ObjectsSystem.View.ObjectsView
{
    public class WorkbenchView : MonoBehaviour, IUiMenu
    {
        [SerializeField] private GameObject menu;
        [SerializeField] private List<GameObject> tiers;
        [SerializeField] private MenuType menuType;
        [SerializeField] private List<MenuType> allowedMenusOnTop;
        
        [Inject] private IVariablesResolver<PlayerVariableBlockerType, Action, Action> _variables;
        
        private readonly IVariableBlocker<PlayerVariableBlockerType> _workbenchBlocker =
            new PlayerVariableBlocker(PlayerVariableBlockerType.MOVEMENT, PlayerVariableBlockerType.INTERACT,
                PlayerVariableBlockerType.LOOK, PlayerVariableBlockerType.BUILD, PlayerVariableBlockerType.ATTACK);
        
        public event Action OnMenuCanceled;
     
        public void Initialize(Workbench workbench)
        {
            if(workbench.GetTier() > tiers.Count - 1 || workbench.GetTier() < 0)
                return;
            
            tiers.ForEach(x => x.SetActive(false));
            tiers[workbench.GetTier()].SetActive(true);
        }
        
        public void Open()
        {
            menu.SetActive(true);
            _variables.RegisterBlocker(_workbenchBlocker);
        }

        public void Close()
        {
            menu.SetActive(false);
            _workbenchBlocker.Dispose();
        }

        public void Cancel()
        {
            OnMenuCanceled?.Invoke();
            Close();
        }

        public bool IsOpen() => menu.activeSelf;

        public MenuType GetMenuType() => menuType;

        public IReadOnlyCollection<MenuType> GetAllowedMenuTypesOnTop() => allowedMenusOnTop;
    }
}