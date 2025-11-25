using GameAssembly.HealthSystem;
using GameAssembly.ItemsSystem;
using GameAssembly.ItemsSystem.Data;
using Mirror;
using UnityEngine;

namespace GameAssembly.ObjectsSystem
{
    public class BaseDropObject : NetworkBehaviour
    {
        [SerializeField] private AHealthObject healthObject;
        [SerializeField] private PickableObject droppingObject;
        [SerializeField] private ItemDefinitionSO itemDefinition;
        [SerializeField] private int minCount;
        [SerializeField] private int maxCount;

        private void Start()
        {
            if(!isServer)
                return;

            Bind();
        }

        private void OnDestroy()
        {
            if(!isServer)
                return;

            Expose();
        }
        
        private void OnDeath()
        {
            var spawned = Instantiate(droppingObject, transform.position, Quaternion.identity);
            spawned.Initialize(new ItemInstance(itemDefinition, Random.Range(minCount, maxCount + 1)));
            NetworkServer.Spawn(spawned.gameObject);
        }

        private void Bind() => healthObject.OnZeroHealth += OnDeath;

        private void Expose() => healthObject.OnZeroHealth -= OnDeath;
    }
}