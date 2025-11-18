using System;
using GameAssembly.HealthSystem.Data;
using Mirror;
using UnityEngine;

namespace GameAssembly.HealthSystem
{
    public abstract class AHealthObject : NetworkBehaviour, IHealth
    {
        [SerializeField] protected HealthType healthType;
        [SerializeField] protected int maxHealth;

        [SyncVar(hook = nameof(InvokeOnHealthChanged))]
        protected int _health;

        /// <summary>
        /// Called on server and clients
        /// </summary>
        public event Action<int, int> OnHealthChanged;
        
        /// <summary>
        /// Called on server and clients
        /// </summary>
        public event Action OnZeroHealth;

        public HealthType GetHealthType() => healthType;

        public int GetMaxHealth() => maxHealth;

        public int GetHealth() => _health;

        protected void InvokeOnHealthChanged(int oldValue, int newValue) => OnHealthChanged?.Invoke(oldValue, newValue);

        [ClientRpc]
        protected void Rpc_InvokeOnZeroHealth() => OnZeroHealth?.Invoke();

        [Server]
        public virtual void ChangeHealth(int value)
        {
            var temp = _health;
            _health = Mathf.Clamp(_health + value, 0, maxHealth);
            OnHealthChanged?.Invoke(temp, _health);

            if (_health != 0)
                return;
            
            Rpc_InvokeOnZeroHealth();
            OnZeroHealth?.Invoke();
        }
    }
}