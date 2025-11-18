using System;
using GameAssembly.HealthSystem.Data;

namespace GameAssembly.HealthSystem
{
    public interface IHealth
    {
        event Action<int, int> OnHealthChanged; 
        event Action OnZeroHealth;
        
        HealthType GetHealthType();
        int GetMaxHealth();
        int GetHealth();
        void ChangeHealth(int value);
    }
}