using Mirror;
using UnityEngine;

namespace GameAssembly.Core.Network
{
    public class NetManagerSpawner : MonoBehaviour
    {
        [SerializeField] private NetManager netManagerPrefab;

        private void Awake()
        {
            if(!NetworkManager.singleton)
                SpawnManager();
        }

        private void SpawnManager()
        {
            Instantiate(netManagerPrefab).name = netManagerPrefab.name;
        }
    }
}