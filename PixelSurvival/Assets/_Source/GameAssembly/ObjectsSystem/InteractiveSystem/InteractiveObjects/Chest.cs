using GameAssembly.HealthSystem;
using GameAssembly.InventorySystem;
using GameAssembly.ObjectsSystem.View.ObjectsView;
using GameAssembly.PlayerSystem.View;
using GameAssembly.UiSystem;
using GameAssembly.Utils;
using Mirror;
using UnityEngine;
using VContainer;

namespace GameAssembly.ObjectsSystem.InteractiveSystem.InteractiveObjects
{
    public class Chest : BaseInventory, IInteractiveObject
    {
        [SerializeField] private AHealthObject healthObject;
        
        [Inject] private ChestView _view;
        [Inject] private PlayerInventoryView _playerInventoryView;
        [Inject] private ServerInventoryManager _serverInventoryManager;
        
        private void Start()
        {
            if(NetworkClient.active)
                ObjectInjector.Inject(this);
            
            if(NetworkServer.active)
                Server_Bind();
        }

        private void OnDestroy()
        {
            if(NetworkServer.active)
                Server_Expose();
        }

        public void Interact()
        {
            _view.Initialize(this);
            
            UiManager.Instance.OpenRequest(_playerInventoryView);
            UiManager.Instance.OpenRequest(_view);
        }

        [Server]
        private void DropEverythingFromChest()
        {
            for (var index = 0; index < _items.Length; index++)
            {
                var itemInstance = _items[index];

                if (itemInstance != null)
                    _serverInventoryManager.Server_DropItemFromInventory(netIdentity, index, transform.position);
            }
        }

        [Server]
        private void Server_Bind()
        {
            healthObject.OnZeroHealth += DropEverythingFromChest;
        }

        [Server]
        private void Server_Expose()
        {
            healthObject.OnZeroHealth -= DropEverythingFromChest;
        }
    }
}