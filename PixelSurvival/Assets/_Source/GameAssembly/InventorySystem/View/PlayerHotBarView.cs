using System.Collections;
using System.Linq;
using Mirror;
using UnityEngine;

namespace GameAssembly.InventorySystem.View
{
    public class PlayerHotBarView : MonoBehaviour
    {
        [SerializeField] private PlayerInventoryView playerInventoryView;

        private PlayerSelector _playerSelector;

        private IEnumerator Start()
        {
            if (!NetworkClient.active)
                yield break;

            while (playerInventoryView.HotBarCells == null)
                yield return null;
            
            _playerSelector = NetworkClient.localPlayer.GetComponent<PlayerSelector>();
            Bind();
        }

        private void OnDestroy()
        {
            if (NetworkClient.active)
                Expose();
        }

        private void OnSelectionChanged(int oldValue, int newValue)
        {
            if (oldValue != -1)
                playerInventoryView.HotBarCells.ElementAt(oldValue).SetSelectionInactive();
            
            if(_playerSelector.IsSelectedSomething)
                playerInventoryView.HotBarCells.ElementAt(newValue).SetSelectionActive();
        }

        private void Bind()
        {
            _playerSelector.OnSelectionChangedEvent += OnSelectionChanged;
        }

        private void Expose()
        {
            _playerSelector.OnSelectionChangedEvent -= OnSelectionChanged;
        }
    }
}