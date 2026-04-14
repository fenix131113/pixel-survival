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
        [SerializeField] protected AHealthObject healthObject;
        [SerializeField] protected SpriteRenderer chestRenderer;
        
        [Inject] private ChestView _view;
        [Inject] protected PlayerInventoryView playerInventoryView;
        [Inject] protected ServerInventoryManager serverInventoryManager;
        
        protected virtual void Start()
        {
            if(NetworkClient.active)
                ObjectInjector.Inject(this);
            
            if(NetworkServer.active)
                Server_Bind();
        }

        protected virtual void OnDestroy()
        {
            if(NetworkServer.active)
                Server_Expose();
        }

        public virtual void Interact()
        {
            if (_view == null || playerInventoryView == null || UiManager.Instance == null)
                return;

            _view.Initialize(this);
            OpenInventoryView(_view);
        }

        public Renderer GetRendererTarget() => chestRenderer;

        protected void OpenInventoryView(IUiInventory inventoryView)
        {
            if (UiManager.Instance == null || playerInventoryView == null || inventoryView == null)
                return;

            UiManager.Instance.OpenRequest(playerInventoryView);
            UiManager.Instance.OpenRequest(inventoryView);
        }

        [Server]
        protected virtual void DropEverythingFromChest()
        {
            for (var index = 0; index < _items.Length; index++)
            {
                var itemInstance = _items[index];

                if (itemInstance != null)
                    serverInventoryManager.Server_DropItemFromInventory(netIdentity, index, transform.position);
            }
        }

        [Server]
        protected virtual void Server_Bind()
        {
            healthObject.OnZeroHealth += DropEverythingFromChest;
        }

        [Server]
        protected virtual void Server_Expose()
        {
            healthObject.OnZeroHealth -= DropEverythingFromChest;
        }
    }
}
