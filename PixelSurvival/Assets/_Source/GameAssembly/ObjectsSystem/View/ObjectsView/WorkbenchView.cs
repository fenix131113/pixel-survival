using System;
using System.Collections.Generic;
using GameAssembly.ObjectsSystem.InteractiveSystem.InteractiveObjects;
using GameAssembly.UiSystem;
using GameAssembly.UiSystem.Data;
using UnityEngine;

namespace GameAssembly.ObjectsSystem.View.ObjectsView
{
    public class WorkbenchView : MonoBehaviour, IUiMenu
    {
        [SerializeField] private GameObject menu;
        [SerializeField] private List<GameObject> tiers;
        [SerializeField] private MenuType menuType;
        [SerializeField] private List<MenuType> allowedMenusOnTop;
        
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
        }

        public void Close()
        {
            menu.SetActive(false);   
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