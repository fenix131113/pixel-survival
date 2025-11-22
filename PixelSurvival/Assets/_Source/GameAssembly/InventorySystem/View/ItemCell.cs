using System.Linq;
using GameAssembly.ItemsSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameAssembly.InventorySystem.View
{
    public class ItemCell : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text counter;
        
        private IInventory _inventory;
        private bool _isExposed = true;

        public int CellIndex { get; private set; }
        private ItemInstance _lastItem;

        public void Initialize(IInventory inventory, int cellIndex)
        {
            _inventory = inventory;
            CellIndex = cellIndex;
            Bind();
            Draw();
        }

        private void OnDestroy()
        {
            Expose();
        }

        protected virtual void CheckForChanges(int index)
        {
            var items = _inventory.GetItems().ToArray();

            if (items[index] == _lastItem)
                return;
            
            ExposeItem(_lastItem);
            _lastItem = items[index];
            BindItem(_lastItem);
                
            Draw();
        }

        protected virtual void Draw()
        {
            var items = _inventory.GetItems().ToArray();
            counter.gameObject.SetActive(items[CellIndex] != null);
            
            if(items[CellIndex] == null)
                return;
            
            icon.sprite = items[CellIndex].Definition.Icon;
            counter.text = items[CellIndex].Count.ToString();
        }

        protected virtual void BindItem(ItemInstance instance)
        {
            if (instance != null)
                instance.OnItemChanged += Draw;
        }

        protected virtual void ExposeItem(ItemInstance instance)
        {
            if (instance != null)
                instance.OnItemChanged -= Draw;
        }

        private void Bind()
        {
            if(!_isExposed)
                return;
            
            _isExposed = false;
            _inventory.OnItemChanged += CheckForChanges;
        }

        private void Expose()
        {
            if(_isExposed)
                return;
            
            _isExposed = true;
            _inventory.OnItemChanged -= CheckForChanges;
        }
    }
}