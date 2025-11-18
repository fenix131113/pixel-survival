using UnityEngine;

namespace GameAssembly.PlayerSystem.Data
{
    [CreateAssetMenu(fileName = "New PlayerData", menuName = "SO/New PlayerData")]
    public class PlayerDataSO : ScriptableObject
    {
        [field: SerializeField] public float MoveSpeed { get; private set; }
    }
}