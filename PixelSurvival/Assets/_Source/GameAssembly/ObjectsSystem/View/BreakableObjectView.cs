using DG.Tweening;
using GameAssembly.HealthSystem.View;
using UnityEngine;

namespace GameAssembly.ObjectsSystem.View
{
    public class BreakableObjectView : ABaseHealthObjectView
    {
        [SerializeField] private float shakeDuration = 0.1f;
        [SerializeField] private float shakeStrength = 0.2f;
        [SerializeField] private int shakeVibrato = 10;

        [Header("Hit Flash")]
        [SerializeField] private bool enableHitFlash;
        [SerializeField] private SpriteRenderer[] targetRenderers;
        [SerializeField] private Color hitFlashColor = Color.red;
        [SerializeField, Min(0.01f)] private float hitFlashDuration = 0.15f;

        private Tween _shake;
        private Tween _hitFlash;
        private Color[] _baseColors;

        private void Awake()
        {
            EnsureRenderers();
            CacheBaseColors();
        }

        protected override void OnDestroy()
        {
            _shake?.Kill();
            _hitFlash?.Kill();
            RestoreBaseColors();
            base.OnDestroy();
        }

        protected override void OnHealthChanged(int oldValue, int newValue)
        {
            if (newValue >= oldValue)
                return;

            _shake?.Kill();
            if(shakeDuration > 0 && shakeVibrato > 0 && shakeStrength > 0)
                _shake = transform.DOShakePosition(shakeDuration, shakeStrength, shakeVibrato);

            if (enableHitFlash)
                PlayHitFlash();
        }

        private void EnsureRenderers()
        {
            if (targetRenderers is { Length: > 0 })
                return;

            targetRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        private void CacheBaseColors()
        {
            EnsureRenderers();

            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                _baseColors = null;
                return;
            }

            _baseColors = new Color[targetRenderers.Length];

            for (var i = 0; i < targetRenderers.Length; i++)
                _baseColors[i] = targetRenderers[i] ? targetRenderers[i].color : Color.white;
        }

        private void PlayHitFlash()
        {
            EnsureRenderers();
            if (targetRenderers == null || targetRenderers.Length == 0)
                return;

            if (_baseColors == null || _baseColors.Length != targetRenderers.Length)
                CacheBaseColors();

            _hitFlash?.Kill();
            ApplyFlashColor();

            _hitFlash = DOVirtual.DelayedCall(hitFlashDuration, RestoreBaseColors);
        }

        private void ApplyFlashColor()
        {
            if (targetRenderers == null)
                return;

            for (var i = 0; i < targetRenderers.Length; i++)
            {
                var renderer = targetRenderers[i];
                if (!renderer)
                    continue;

                var alpha = _baseColors != null && i < _baseColors.Length
                    ? _baseColors[i].a
                    : renderer.color.a;

                renderer.color = new Color(hitFlashColor.r, hitFlashColor.g, hitFlashColor.b, alpha);
            }
        }

        private void RestoreBaseColors()
        {
            if (targetRenderers == null || _baseColors == null)
                return;

            for (var i = 0; i < targetRenderers.Length; i++)
            {
                var renderer = targetRenderers[i];
                if (!renderer || i >= _baseColors.Length)
                    continue;

                renderer.color = _baseColors[i];
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsureRenderers();
        }
#endif
    }
}
