using System.Collections.Generic;
using GameAssembly.ItemsSystem;
using GameAssembly.ItemsSystem.Data;
using Mirror;
using UnityEngine;

namespace GameAssembly.ObjectsSystem
{
    public class ServerItemSpawner : NetworkBehaviour
    {
        [SerializeField] private PickableObject pickablePrefab;

        public static ServerItemSpawner Instance { get; private set; }

        public override void OnStartServer() => Instance = FindFirstObjectByType<ServerItemSpawner>();

        [Server]
        public static PickableObject Server_SpawnItem(Vector3 worldPosition, ItemDefinitionSO itemDefinition,
            int itemCount, Dictionary<string, string> meta = null) =>
            Instance.Server_SpawnItem_Internal(worldPosition, itemDefinition, itemCount, meta);
        
        [Server]
        public static PickableObject Server_SpawnItem(Vector3 worldPosition, ItemInstance item) =>
            Instance.Server_SpawnItem_Internal(worldPosition, item);

        [Server]
        public PickableObject Server_SpawnItem_Internal(Vector3 worldPosition, ItemDefinitionSO itemDefinition,
            int itemCount, Dictionary<string, string> meta = null)
        {
            var itemInstance = new ItemInstance(itemDefinition, meta, itemCount);
            var spawned = Instantiate(pickablePrefab, worldPosition, Quaternion.identity);
            spawned.Initialize(itemInstance);
            NetworkServer.Spawn(spawned.gameObject);
            return spawned;
        }
        
        [Server]
        public PickableObject Server_SpawnItem_Internal(Vector3 worldPosition, ItemInstance item)
        {
            var spawned = Instantiate(pickablePrefab, worldPosition, Quaternion.identity);
            spawned.Initialize(item);
            NetworkServer.Spawn(spawned.gameObject);
            return spawned;
        }
    }
}