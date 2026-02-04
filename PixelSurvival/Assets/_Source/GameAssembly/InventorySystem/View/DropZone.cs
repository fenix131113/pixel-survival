using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.EventSystems;
using VContainer;

namespace GameAssembly.InventorySystem.View
{
    public class DropZone : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        [Inject] private MovingItem _movingItem;

        private PlayerLocalInventoryManager _playerLocalInventoryManager;

        private void Start()
        {
            if (NetworkServer.active && !NetworkClient.active)
                return;

            StartCoroutine(WaitForPlayer());
        }

        public void OnDrop(PointerEventData eventData)
        {
            _playerLocalInventoryManager?.DropItemFromInventory(_movingItem.CurrentInventoryIdentity,
                _movingItem.CurrentCellIndex, NetworkClient.localPlayer.transform.position);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
        }

        public void OnDrag(PointerEventData eventData)
        {
        }

        public void OnEndDrag(PointerEventData eventData)
        {
        }

        private IEnumerator WaitForPlayer()
        {
            while (!NetworkClient.localPlayer)
                yield return null;

            _playerLocalInventoryManager = NetworkClient.localPlayer.GetComponent<PlayerLocalInventoryManager>();
        }
    }
}