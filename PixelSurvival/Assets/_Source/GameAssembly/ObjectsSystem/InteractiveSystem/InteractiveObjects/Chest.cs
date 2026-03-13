using GameAssembly.InventorySystem;
using GameAssembly.ObjectsSystem.View.ObjectsView;
using GameAssembly.PlayerSystem.View;
using GameAssembly.UiSystem;
using GameAssembly.Utils;
using Mirror;
using VContainer;

namespace GameAssembly.ObjectsSystem.InteractiveSystem.InteractiveObjects
{
    public class Chest : BaseInventory, IInteractiveObject
    {
        [Inject] private ChestView _view;
        [Inject] private PlayerInventoryView _playerInventoryView;
        
        private void Start()
        {
            if(NetworkClient.active)
                ObjectInjector.Inject(this);
        }

        public void Interact()
        {
            _view.Initialize(this);
            
            UiManager.Instance.OpenRequest(_playerInventoryView);
            UiManager.Instance.OpenRequest(_view);
        }
    }
}