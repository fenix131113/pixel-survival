using System;
using System.Collections.Generic;
using GameAssembly.UiSystem.Data;

namespace GameAssembly.UiSystem
{
    public interface IUiMenu
    {
        event Action OnMenuCanceled;
        void Open();
        void Close();
        void Cancel();
        bool IsOpen();
        MenuType GetMenuType();
        IReadOnlyCollection<MenuType> GetAllowedMenuTypesOnTop();
    }
}
