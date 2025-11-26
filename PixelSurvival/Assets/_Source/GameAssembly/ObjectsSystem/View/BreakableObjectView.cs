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
        
        private Tween _shake;

        protected override void OnDestroy()
        {
            _shake?.Kill();
            base.OnDestroy();
        }

        protected override void OnHealthChanged(int oldValue, int newValue)
        {
            _shake?.Kill();
            _shake = transform.DOShakePosition(shakeDuration, shakeStrength, shakeVibrato);
        }
    }
}