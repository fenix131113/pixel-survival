using System;
using System.Collections.Generic;
using GameAssembly.HealthSystem;
using GameAssembly.ItemsSystem;
using GameAssembly.ItemsSystem.Data;
using Mirror;
using UnityEngine;

namespace GameAssembly.ObjectsSystem
{
    public class BaseDropObject : NetworkBehaviour
    {
        [Serializable]
        private struct DropDefinition
        {
            [SerializeField] private ItemDefinitionSO itemDefinition;
            [SerializeField] private int minCount;
            [SerializeField] private int maxCount;

            public DropDefinition(ItemDefinitionSO itemDefinition, int minCount, int maxCount)
            {
                this.itemDefinition = itemDefinition;
                this.minCount = minCount;
                this.maxCount = maxCount;
            }

            public ItemDefinitionSO ItemDefinition => itemDefinition;
            public int MinCount => minCount;
            public int MaxCount => maxCount;
        }

        [SerializeField] private AHealthObject healthObject;
        [SerializeField] private PickableObject droppingObject;
        [SerializeField] private List<DropDefinition> drops = new();

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
            if(!isServer)
                return;

            foreach (var drop in EnumerateDrops())
                SpawnDrop(drop);
        }

        private IEnumerable<DropDefinition> EnumerateDrops()
        {
            if (drops is not { Count: > 0 })
                yield break;
            
            foreach (var drop in drops)
                yield return drop;
        }

        private void SpawnDrop(DropDefinition drop)
        {
            if (!drop.ItemDefinition || !droppingObject)
                return;

            var normalizedMin = Mathf.Max(0, drop.MinCount);
            var normalizedMax = Mathf.Max(normalizedMin, drop.MaxCount);
            var count = UnityEngine.Random.Range(normalizedMin, normalizedMax + 1);

            if (count <= 0)
                return;

            var spawned = Instantiate(droppingObject, transform.position, Quaternion.identity);
            spawned.Initialize(new ItemInstance(drop.ItemDefinition, count));
            NetworkServer.Spawn(spawned.gameObject);
        }

        private void Bind()
        {
            if (!healthObject)
                return;

            healthObject.OnZeroHealth += OnDeath;
        }

        private void Expose()
        {
            if (!healthObject)
                return;

            healthObject.OnZeroHealth -= OnDeath;
        }

        protected override void OnValidate()
        {
            if(!healthObject && TryGetComponent(out AHealthObject hp))
                healthObject = hp;
        }
    }
}
