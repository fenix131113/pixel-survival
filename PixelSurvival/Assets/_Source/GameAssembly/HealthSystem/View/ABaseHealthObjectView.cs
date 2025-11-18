using UnityEngine;

namespace GameAssembly.HealthSystem.View
{
    public abstract class ABaseHealthObjectView : MonoBehaviour
    {
        [SerializeField] private AHealthObject healthObject;

        protected virtual void Start() => Bind();

        protected virtual void OnDestroy() => Expose();

        protected virtual void OnHealthChanged(int oldValue, int newValue)
        {
        }

        protected virtual void OnZeroHealth()
        {
        }

        protected virtual void Bind()
        {
            healthObject.OnHealthChanged += OnHealthChanged;
            healthObject.OnZeroHealth += OnZeroHealth;
        }

        protected virtual void Expose()
        {
            healthObject.OnHealthChanged -= OnHealthChanged;
            healthObject.OnZeroHealth -= OnZeroHealth;
        }
    }
}