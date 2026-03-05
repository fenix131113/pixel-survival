using GameAssembly.UiSystem.Data;

namespace GameAssembly.UiSystem
{
    public interface IUiMenu
    {
        void Open();
        void Close();
        bool IsOpen();
        MenuType GetMenuType();
    }
}