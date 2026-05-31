using System.Collections;
using DG.Tweening;
using GameAssembly.CraftSystem.Data;
using GameAssembly.WorldSystem;
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
        [SerializeField, Range(0f, 1f)] private float lockedIconAlpha = 0.35f;

        private CraftManager _craftManager;
        private WorldCreateManager _worldCreateManager;
        private Tween _animTween;
        private Vector3 _startScale;
        private Color _iconStartColor;
        private bool _isUnlocked = true;

        private void Start()
        {
            _startScale = transform.localScale;
            _iconStartColor = icon ? icon.color : Color.white;

            if (!NetworkClient.active)
            {
                Draw();
                return;
            }

            StartCoroutine(InitializeRoutine());
        }

        private IEnumerator InitializeRoutine()
        {
            while (NetworkClient.active && !NetworkClient.localPlayer)
                yield return null;

            if (!NetworkClient.active || !NetworkClient.localPlayer)
                yield break;

            _craftManager = NetworkClient.localPlayer.GetComponent<CraftManager>();

            TryRebindWorldCreateManager();
            Draw();
            RefreshUnlockVisualState();
        }

        private void OnDestroy()
        {
            if (_worldCreateManager)
                _worldCreateManager.ClientOnCraftUnlockStateChanged -= RefreshUnlockVisualState;

            _animTween?.Kill();
        }

        private void Draw()
        {
            if (!icon || !recipe || !recipe.ResultItem)
                return;

            icon.sprite = recipe.ResultItem.InventoryIcon ? recipe.ResultItem.InventoryIcon : recipe.ResultItem.Icon;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_craftManager || !recipe)
                return;

            RefreshUnlockVisualState();
            if (!_isUnlocked || !_craftManager.CanCraft(recipe))
                return;

            CraftEffect();
            _craftManager.Cmd_TryCraftItem(recipe, 1);
        }

        private void CraftEffect()
        {
            _animTween?.Kill();
            transform.localScale = _startScale;
            _animTween = transform.DOPunchScale(Vector3.Scale(_startScale, Vector3.one * effectMultiplier), effectDuration);
        }

        private void TryRebindWorldCreateManager()
        {
            var manager = WorldCreateManager.Instance;
            if (manager == _worldCreateManager)
                return;

            if (_worldCreateManager)
                _worldCreateManager.ClientOnCraftUnlockStateChanged -= RefreshUnlockVisualState;

            _worldCreateManager = manager;
            if (_worldCreateManager)
                _worldCreateManager.ClientOnCraftUnlockStateChanged += RefreshUnlockVisualState;
        }

        private void RefreshUnlockVisualState()
        {
            TryRebindWorldCreateManager();

            var isUnlocked = true;
            if (_worldCreateManager)
                isUnlocked = _worldCreateManager.HasCraftStateSnapshot && _worldCreateManager.IsRecipeUnlocked(recipe);

            _isUnlocked = isUnlocked;

            if (icon)
            {
                var color = _iconStartColor;
                color.a = isUnlocked ? _iconStartColor.a : _iconStartColor.a * lockedIconAlpha;
                icon.color = color;
            }
        }
    }
}
