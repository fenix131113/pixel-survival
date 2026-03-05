using GameAssembly.ItemsSystem;
using GameAssembly.Utils;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;

namespace GameAssembly.InventorySystem.View
{
    public class ItemCell : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler // TODO: Make new class for hot bar slot with PlayerSelector link
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text counter;
        [SerializeField] private GameObject selection;

        [Inject] private MovingItem _movingItem;

        private NetworkIdentity _inventoryIdentity;
        private IInventory _inventory;
        private bool _isExposed = true;

        public int CellIndex { get; private set; }
        private ItemInstance _lastItem;

        public void Initialize(NetworkIdentity inventoryIdentity, int cellIndex)
        {
            ObjectInjector.Inject(this);

            _inventoryIdentity = inventoryIdentity;
            _inventory = _inventoryIdentity.GetComponent<IInventory>();
            CellIndex = cellIndex;
            Bind();
            Draw();
        }

        private void OnDestroy() => Expose();

        protected virtual void CheckForChanges(int index)
        {
            if (CellIndex != index)
                return;

            var item = _inventory.GetItemByIndex(index);

            if (item == _lastItem)
            {
                Draw();
                return;
            }

            _lastItem = item;

            Draw();
        }

        protected virtual void Draw()
        {
            counter.gameObject.SetActive(_lastItem != null);
            icon.gameObject.SetActive(_lastItem != null);

            if (_lastItem == null)
            {
                icon.sprite = null;
                return;
            }

            icon.sprite = _lastItem.Definition.Icon;
            counter.text = _lastItem.Count.ToString();
        }

        protected void Client_CheckInventory()
        {
            if(!NetworkClient.active || NetworkServer.active)
                return;
            
            CheckForChanges(CellIndex);
        }

        public void SetSelectionActive() => selection.SetActive(true);

        public void SetSelectionInactive() => selection.SetActive(false);

        private void Bind()
        {
            if (!_isExposed)
                return;

            _isExposed = false;
            _inventory.OnItemChanged += CheckForChanges;
            _inventory.OnInventoryChanged += Client_CheckInventory;
        }

        private void Expose()
        {
            if (_isExposed)
                return;

            _isExposed = true;
            _inventory.OnItemChanged -= CheckForChanges;
            _inventory.OnInventoryChanged -= Client_CheckInventory;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _movingItem.StartDrag(_inventoryIdentity, CellIndex);
        }

        public void OnDrop(PointerEventData eventData)
        {
            _movingItem.TriggerDrop(_inventoryIdentity, CellIndex);
        }

        public void OnDrag(PointerEventData eventData)
        {
        }

        public void OnEndDrag(PointerEventData eventData)
        {
        }
    }
}
