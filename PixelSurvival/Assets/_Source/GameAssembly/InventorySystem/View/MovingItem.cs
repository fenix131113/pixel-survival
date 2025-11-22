using System.Collections;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GameAssembly.InventorySystem.View
{
    public class MovingItem : MonoBehaviour
    {
        [SerializeField] private RectTransform movingRect;
        [SerializeField] private Image itemIcon;
        [SerializeField] private TMP_Text itemCountLabel;

        private bool _isMoving;
        private PlayerLocalInventoryManager _playerLocalInventoryManager;

        public NetworkIdentity CurrentInventoryIdentity { get; private set; }
        public IInventory CurrentInventory { get; private set; }
        public int CurrentCellIndex { get; private set; }

        private void Start()
        {
            StartCoroutine(WaitForPlayer());
        }

        private void Update()
        {
            if (!_isMoving)
                return;

            movingRect.position = GetMousePosition();
            
            if(Mouse.current.leftButton.wasReleasedThisFrame)
                ForceClose();
        }

        public void StartDrag(NetworkIdentity inventoryIdentity, int cellIndex)
        {
            if (!inventoryIdentity.TryGetComponent(out IInventory inventory))
                return;

            CurrentInventory = inventory;

            if (CurrentInventory.GetItemByIndex(cellIndex) == null)
                return;

            _isMoving = true;

            CurrentInventoryIdentity = inventoryIdentity;
            CurrentCellIndex = cellIndex;

            Draw();
            movingRect.position = GetMousePosition();
            movingRect.gameObject.SetActive(true);
        }

        public void TriggerDrop(NetworkIdentity inventoryIdentity, int cellIndex)
        {
            if(!_isMoving)
                return;
            
            movingRect.gameObject.SetActive(false);
            _isMoving = false;
            
            if(cellIndex == CurrentCellIndex)
                return;

            _playerLocalInventoryManager.CombineCells(CurrentInventoryIdentity, CurrentCellIndex, inventoryIdentity,
                cellIndex, false);
        }

        public void ForceClose()
        {
            movingRect.gameObject.SetActive(false);
            _isMoving = false;
        }

        private void Draw()
        {
            itemIcon.sprite = CurrentInventory.GetItemByIndex(CurrentCellIndex).Definition.Icon;
            itemCountLabel.text = CurrentInventory.GetItemByIndex(CurrentCellIndex).Count.ToString();
        }

        private Vector2 GetMousePosition() => Mouse.current.position.ReadValue();

        private IEnumerator WaitForPlayer()
        {
            while (!NetworkClient.localPlayer)
                yield return null;

            _playerLocalInventoryManager = NetworkClient.localPlayer.GetComponent<PlayerLocalInventoryManager>();
        }
    }
}