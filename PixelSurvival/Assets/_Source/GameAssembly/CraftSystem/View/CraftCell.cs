using GameAssembly.CraftSystem.Data;
using Mirror;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GameAssembly.CraftSystem.View
{
    public class CraftCell : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private CraftRecipeSO recipe;

        private CraftManager _craftManager;

        private void Start()
        {
            if(NetworkClient.active)
                _craftManager = NetworkClient.localPlayer.GetComponent<CraftManager>();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _craftManager.Cmd_TryCraftItem(recipe, 1);
        }
    }
}