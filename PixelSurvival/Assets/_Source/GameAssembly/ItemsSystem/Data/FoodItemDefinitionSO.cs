using UnityEngine;

namespace GameAssembly.ItemsSystem.Data
{
    [CreateAssetMenu(fileName = "New ToolItemDefinitionSO", menuName = "SO/New FoodItemDefinitionSO")]
    public class FoodItemDefinitionSO : ItemDefinitionSO
    {
        [field: SerializeField] public int HealthRecover { get; private set; }
    }
}