using GameAssembly.HealthSystem.Data;
using Mirror;
using UnityEngine;

namespace GameAssembly.HealthSystem
{
    public class HealthRecovery : NetworkBehaviour
    {
        [SerializeField] private AHealthObject health;
        [SerializeField, Min(0f)] private float startRecoveryTime = 5f;
        [SerializeField, Min(0)] private int healthPerSecond = 1;

        private float _timeWithoutDamage;
        private float _recoveryAccumulator;
        private DamageContext _recoveryContext;

        public override void OnStartServer()
        {
            base.OnStartServer();

            if (!health)
                TryGetComponent(out health);

            _recoveryContext = new DamageContext(gameObject, null);
            Server_Bind();
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            Server_Expose();
            _timeWithoutDamage = 0f;
            _recoveryAccumulator = 0f;
            _recoveryContext = null;
        }

        private void Update()
        {
            if (!isServer)
                return;

            Server_UpdateRecovery();
        }

        [Server]
        private void Server_Bind()
        {
            if (!health)
                return;

            health.OnHealthChanged += Server_OnHealthChanged;
        }

        [Server]
        private void Server_Expose()
        {
            if (!health)
                return;

            health.OnHealthChanged -= Server_OnHealthChanged;
        }

        [Server]
        private void Server_OnHealthChanged(int oldValue, int newValue)
        {
            if (newValue >= oldValue)
                return;

            _timeWithoutDamage = 0f;
            _recoveryAccumulator = 0f;
        }

        [Server]
        private void Server_UpdateRecovery()
        {
            if (!health || healthPerSecond <= 0)
                return;

            var currentHealth = health.GetHealth();
            var maxHealth = health.GetMaxHealth();
            if (currentHealth <= 0 || currentHealth >= maxHealth)
            {
                _recoveryAccumulator = 0f;
                return;
            }

            _timeWithoutDamage += Time.deltaTime;
            if (_timeWithoutDamage < Mathf.Max(0f, startRecoveryTime))
                return;

            _recoveryAccumulator += Time.deltaTime * healthPerSecond;
            var recoveryAmount = Mathf.FloorToInt(_recoveryAccumulator);
            if (recoveryAmount <= 0)
                return;

            _recoveryAccumulator -= recoveryAmount;
            health.ChangeHealth(recoveryAmount, _recoveryContext);
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            
            if (!health)
                TryGetComponent(out health);

            startRecoveryTime = Mathf.Max(0f, startRecoveryTime);
            healthPerSecond = Mathf.Max(0, healthPerSecond);
        }
    }
}
