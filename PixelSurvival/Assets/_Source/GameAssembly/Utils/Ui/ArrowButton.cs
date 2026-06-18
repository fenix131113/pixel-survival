using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GameAssembly.Utils.Ui
{
    public class ArrowButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private GameObject arrow;
        [SerializeField] private Transform buttonTransform;

        [Header("Floating")]
        [SerializeField] private float floatAmplitude = 5f;
        [SerializeField] private float floatSpeed = 2f;
        [SerializeField] private float upSize = 2f;
        [SerializeField] private float sizeUpTime = 0.25f;

        private bool _isSelected;
        private Vector3 _startPosition;
        private Vector3 _startScale;
        private Tween _tween;

        private void Awake()
        {
            _startPosition = buttonTransform.localPosition;
            _startScale = buttonTransform.localScale;
        }

        private void Update()
        {
            if (!_isSelected)
            {
                buttonTransform.localPosition = _startPosition;
                return;
            }

            var offsetY = Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;

            buttonTransform.localPosition = _startPosition + Vector3.up * offsetY;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            arrow.SetActive(true);
            _isSelected = true;
            _tween = buttonTransform.DOScale(Vector3.one * upSize, sizeUpTime);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            arrow.SetActive(false);
            _isSelected = false;
            buttonTransform.localPosition = _startPosition;
            _tween?.Kill();
            _tween = null;
            buttonTransform.localScale = _startScale;
        }
    }
}