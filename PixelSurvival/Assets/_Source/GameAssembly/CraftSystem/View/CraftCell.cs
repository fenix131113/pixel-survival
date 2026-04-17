using DG.Tweening;
using GameAssembly.CraftSystem.Data;
using Mirror;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameAssembly.CraftSystem.View
{
    public class CraftCell : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private CraftRecipeSO recipe;
        [SerializeField] private Image icon;
        [SerializeField] private float effectMultiplier = 0.8f;
        [SerializeField] private float effectDuration = 0.2f;

        private CraftManager _craftManager;
        private Tween _animTween;
        private float _startScale;

        private void Start()
        {
            if (!NetworkClient.active)
                return;
            
            _craftManager = NetworkClient.localPlayer.GetComponent<CraftManager>();
            _startScale = transform.localScale.x;
            Draw();
        }

        private void Draw()
        {
            icon.sprite = recipe.ResultItem.InventoryIcon ? recipe.ResultItem.InventoryIcon : recipe.ResultItem.Icon;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_craftManager.CanCraft(recipe))
                return;
            
            CraftEffect();
            _craftManager.Cmd_TryCraftItem(recipe, 1);
        }

        private void CraftEffect()
        {
            _animTween?.Kill();
            transform.localScale = Vector3.one * _startScale;
            _animTween = transform.DOPunchScale(transform.localScale * effectMultiplier, effectDuration);
        }
    }
}