using DG.Tweening;
using UnityEngine;

namespace GameAssembly.MainMenuSystem
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class MenuPanelAnimator : MonoBehaviour
    {
        [SerializeField] private RectTransform animatedTransform;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Animation")]
        [SerializeField] private Vector2 hiddenOffset = new(0f, 28f);
        [SerializeField, Range(0.7f, 1f)] private float hiddenScaleMultiplier = 0.97f;
        [SerializeField, Min(0f)] private float showDuration = 0.2f;
        [SerializeField, Min(0f)] private float hideDuration = 0.16f;
        [SerializeField] private Ease showEase = Ease.OutCubic;
        [SerializeField] private Ease hideEase = Ease.InCubic;
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField] private bool disableGameObjectOnHide = true;

        private Vector2 _shownAnchoredPosition;
        private Vector3 _shownScale;
        private Sequence _animationSequence;
        private bool _isInitialized;
        private bool _isVisible;

        public bool IsVisible => _isVisible;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            EnsureInitialized();
        }

        private void OnDisable()
        {
            KillAnimation();
        }

        private void OnDestroy()
        {
            KillAnimation();
        }

        public void Show(bool instant = false)
        {
            EnsureInitialized();

            if (instant)
            {
                SetVisibleImmediate(true);
                return;
            }

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
                ApplyHiddenState();
            }

            _isVisible = true;
            KillAnimation();
            SetInteractable(false);

            _animationSequence = DOTween.Sequence().SetUpdate(useUnscaledTime);
            _animationSequence.Join(canvasGroup.DOFade(1f, showDuration));
            _animationSequence.Join(animatedTransform.DOAnchorPos(_shownAnchoredPosition, showDuration).SetEase(showEase));
            _animationSequence.Join(animatedTransform.DOScale(_shownScale, showDuration).SetEase(showEase));
            _animationSequence.OnComplete(() =>
            {
                _animationSequence = null;
                SetInteractable(true);
            });
        }

        public void Hide(bool instant = false)
        {
            EnsureInitialized();

            if (instant)
            {
                SetVisibleImmediate(false);
                return;
            }

            if (!gameObject.activeSelf)
            {
                _isVisible = false;
                return;
            }

            _isVisible = false;
            KillAnimation();
            SetInteractable(false);

            _animationSequence = DOTween.Sequence().SetUpdate(useUnscaledTime);
            _animationSequence.Join(canvasGroup.DOFade(0f, hideDuration));
            _animationSequence.Join(animatedTransform
                .DOAnchorPos(GetHiddenAnchoredPosition(), hideDuration)
                .SetEase(hideEase));
            _animationSequence.Join(animatedTransform
                .DOScale(GetHiddenScale(), hideDuration)
                .SetEase(hideEase));
            _animationSequence.OnComplete(() =>
            {
                _animationSequence = null;
                if (disableGameObjectOnHide)
                    gameObject.SetActive(false);
            });
        }

        public void SetVisibleImmediate(bool isVisible)
        {
            EnsureInitialized();
            KillAnimation();

            _isVisible = isVisible;

            if (isVisible)
            {
                if (!gameObject.activeSelf)
                    gameObject.SetActive(true);

                canvasGroup.alpha = 1f;
                animatedTransform.anchoredPosition = _shownAnchoredPosition;
                animatedTransform.localScale = _shownScale;
                SetInteractable(true);
                return;
            }

            SetInteractable(false);
            ApplyHiddenState();

            if (disableGameObjectOnHide && gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        // ReSharper disable Unity.PerformanceAnalysis
        private void EnsureInitialized()
        {
            if (_isInitialized)
                return;

            animatedTransform ??= transform as RectTransform;

            if (!canvasGroup)
                canvasGroup = GetComponent<CanvasGroup>();
            if (!canvasGroup)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            _shownAnchoredPosition = animatedTransform != null
                ? animatedTransform.anchoredPosition
                : Vector2.zero;
            _shownScale = animatedTransform != null
                ? animatedTransform.localScale
                : Vector3.one;
            _isVisible = gameObject.activeSelf;
            _isInitialized = true;
        }

        private void ApplyHiddenState()
        {
            if (!animatedTransform || !canvasGroup)
                return;

            canvasGroup.alpha = 0f;
            animatedTransform.anchoredPosition = GetHiddenAnchoredPosition();
            animatedTransform.localScale = GetHiddenScale();
        }

        private Vector2 GetHiddenAnchoredPosition()
        {
            return _shownAnchoredPosition + hiddenOffset;
        }

        private Vector3 GetHiddenScale()
        {
            return _shownScale * Mathf.Clamp(hiddenScaleMultiplier, 0.01f, 1f);
        }

        private void SetInteractable(bool interactable)
        {
            if (!canvasGroup)
                return;

            canvasGroup.interactable = interactable;
            canvasGroup.blocksRaycasts = interactable;
        }

        private void KillAnimation()
        {
            _animationSequence?.Kill();
            _animationSequence = null;
        }
    }
}
