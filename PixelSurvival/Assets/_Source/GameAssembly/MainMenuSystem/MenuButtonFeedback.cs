using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameAssembly.MainMenuSystem
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class MenuButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, ISubmitHandler
    {
        [Header("Scale")]
        [SerializeField] private float hoverScaleMultiplier = 1.05f;
        [SerializeField] private float pressScaleMultiplier = 0.95f;
        [SerializeField] private float hoverDuration = 0.12f;
        [SerializeField] private float pressDuration = 0.07f;
        [SerializeField] private float releaseDuration = 0.1f;

        [Header("Click")]
        [SerializeField] private float clickPunchScale = 0.05f;
        [SerializeField] private float clickPunchDuration = 0.12f;
        [SerializeField] private int clickPunchVibrato = 8;
        [SerializeField, Range(0f, 1f)] private float clickPunchElasticity = 0.8f;

        [Header("Disabled")]
        [SerializeField, Range(0f, 1f)] private float disabledAlphaMultiplier = 0.55f;
        [SerializeField] private float disabledFadeDuration = 0.12f;

        [Header("Tween")]
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField] private Ease hoverEase = Ease.OutQuad;
        [SerializeField] private Ease pressEase = Ease.OutQuad;
        [SerializeField] private Ease releaseEase = Ease.OutBack;
        [SerializeField] private Ease alphaEase = Ease.OutQuad;

        private Button _button;
        private RectTransform _rectTransform;
        private CanvasGroup _canvasGroup;
        private Vector3 _defaultScale;
        private float _defaultAlpha;
        private bool _isPointerOver;
        private bool _isPointerDown;
        private bool _isInteractable;

        private Tween _scaleTween;
        private Tween _alphaTween;
        private Tween _clickTween;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _rectTransform = transform as RectTransform;

            if (_rectTransform == null)
                return;

            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            _defaultScale = _rectTransform.localScale;
            _defaultAlpha = _canvasGroup.alpha;
        }

        private void OnEnable()
        {
            _isPointerOver = false;
            _isPointerDown = false;
            _isInteractable = IsButtonInteractable();
            SnapToCurrentState();
        }

        private void Update()
        {
            var interactableNow = IsButtonInteractable();
            if (_isInteractable == interactableNow)
                return;

            _isInteractable = interactableNow;
            if (!_isInteractable)
            {
                _isPointerOver = false;
                _isPointerDown = false;
                TweenScale(_defaultScale, releaseDuration, releaseEase);
            }

            TweenAlpha(GetTargetAlpha(), disabledFadeDuration, alphaEase);
        }

        private void OnDisable()
        {
            KillTweens();
            if (_rectTransform != null)
                _rectTransform.localScale = _defaultScale;
        }

        private void OnDestroy()
        {
            KillTweens();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isPointerOver = true;
            if (!IsButtonInteractable())
                return;

            _isInteractable = true;
            if (_isPointerDown)
                return;

            TweenScale(_defaultScale * hoverScaleMultiplier, hoverDuration, hoverEase);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isPointerOver = false;
            _isPointerDown = false;
            TweenScale(_defaultScale, releaseDuration, releaseEase);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !IsButtonInteractable())
                return;

            _isInteractable = true;
            _isPointerDown = true;
            TweenScale(_defaultScale * pressScaleMultiplier, pressDuration, pressEase);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            _isPointerDown = false;
            var targetScale = _isPointerOver && IsButtonInteractable()
                ? _defaultScale * hoverScaleMultiplier
                : _defaultScale;

            TweenScale(targetScale, releaseDuration, releaseEase);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !IsButtonInteractable())
                return;

            PlayClickFeedback();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (!IsButtonInteractable())
                return;

            PlayClickFeedback();
        }

        private void PlayClickFeedback()
        {
            var targetScale = GetCurrentTargetScale();

            _scaleTween?.Kill();
            _clickTween?.Kill();

            var normalizeDuration = Mathf.Min(releaseDuration, 0.08f);
            var needsNormalize = (_rectTransform.localScale - targetScale).sqrMagnitude > 0.0001f;

            var sequence = DOTween.Sequence().SetUpdate(useUnscaledTime);
            if (needsNormalize)
            {
                sequence.Append(_rectTransform
                    .DOScale(targetScale, normalizeDuration)
                    .SetEase(releaseEase));
            }

            sequence.Append(_rectTransform
                .DOPunchScale(Vector3.one * clickPunchScale, clickPunchDuration, clickPunchVibrato, clickPunchElasticity));

            sequence.OnComplete(() =>
            {
                _clickTween = null;
                _rectTransform.localScale = targetScale;
            });

            _clickTween = sequence;
        }

        private bool IsButtonInteractable()
        {
            return _button && _button.enabled && _button.IsInteractable() && isActiveAndEnabled;
        }

        private Vector3 GetCurrentTargetScale()
        {
            if (!_isInteractable)
                return _defaultScale;

            if (_isPointerDown)
                return _defaultScale * pressScaleMultiplier;

            return _isPointerOver
                ? _defaultScale * hoverScaleMultiplier
                : _defaultScale;
        }

        private float GetTargetAlpha()
        {
            return _isInteractable
                ? _defaultAlpha
                : _defaultAlpha * disabledAlphaMultiplier;
        }

        private void SnapToCurrentState()
        {
            if (_rectTransform == null || _canvasGroup == null)
                return;

            KillTweens();
            _rectTransform.localScale = GetCurrentTargetScale();
            _canvasGroup.alpha = GetTargetAlpha();
        }

        private void TweenScale(Vector3 targetScale, float duration, Ease ease)
        {
            if (!_rectTransform)
                return;

            _scaleTween?.Kill();
            _clickTween?.Kill();

            if (duration <= 0f)
            {
                _rectTransform.localScale = targetScale;
                return;
            }

            _scaleTween = _rectTransform
                .DOScale(targetScale, duration)
                .SetEase(ease)
                .SetUpdate(useUnscaledTime);
        }

        private void TweenAlpha(float targetAlpha, float duration, Ease ease)
        {
            if (!_canvasGroup)
                return;

            _alphaTween?.Kill();

            if (duration <= 0f)
            {
                _canvasGroup.alpha = targetAlpha;
                return;
            }

            _alphaTween = _canvasGroup
                .DOFade(targetAlpha, duration)
                .SetEase(ease)
                .SetUpdate(useUnscaledTime);
        }

        private void KillTweens()
        {
            _scaleTween?.Kill();
            _alphaTween?.Kill();
            _clickTween?.Kill();

            _scaleTween = null;
            _alphaTween = null;
            _clickTween = null;
        }
    }
}
