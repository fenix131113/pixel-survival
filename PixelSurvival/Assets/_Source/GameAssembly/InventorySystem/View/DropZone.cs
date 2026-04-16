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
            if (_movingItem == null || !_movingItem.IsMoving || !_playerLocalInventoryManager || !NetworkClient.localPlayer)
                return;

            var sourceInventoryIdentity = _movingItem.CurrentInventoryIdentity;
            var sourceCellIndex = _movingItem.CurrentCellIndex;

            if (!sourceInventoryIdentity || sourceCellIndex < 0)
            {
                _movingItem.ForceClose();
                return;
            }

            _playerLocalInventoryManager.DropItemFromInventory(sourceInventoryIdentity,
                sourceCellIndex, NetworkClient.localPlayer.transform.position);
            _movingItem.ForceClose();
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
            while (isActiveAndEnabled && !NetworkClient.localPlayer)
                yield return null;

            if (!isActiveAndEnabled || !NetworkClient.localPlayer)
                yield break;

            _playerLocalInventoryManager = NetworkClient.localPlayer.GetComponent<PlayerLocalInventoryManager>();
        }
    }
}
