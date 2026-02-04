using System.Collections;
using GameAssembly.Core.Network;
using GameAssembly.WorldSystem;
using GameAssembly.WorldSystem.Data;
using Mirror;
using VContainer;
using UnityEngine;

namespace GameAssembly.PlayerSystem
{
    public class PlayersSpawner : NetworkBehaviour
    {
        private NetManager _net;

        [Inject] private World _world;

        private void Awake()
        {
            if (isClientOnly)
                return;
            
            _net = NetworkManager.singleton as NetManager;
            Bind();
        }

        private void OnDestroy()
        {
            if (isClientOnly)
                return;
            
            StopAllCoroutines();
            Expose();
        }

        private void SpawnPlayer(NetworkConnectionToClient conn) // TODO: Make save/load position
        {
            StartCoroutine(WaitForWorldAndSpawnPlayer(conn));
        }

        private void Bind() => _net.ServerOnServerReadyInGame += SpawnPlayer;

        private void Expose() => _net.ServerOnServerReadyInGame -= SpawnPlayer;

        private IEnumerator WaitForWorldAndSpawnPlayer(NetworkConnectionToClient conn)
        {
            yield return new WaitUntil(() => _world.IsLoaded.Value);

            var spawnPos = Vector3.zero;
                //_world.FindRandomNearestBlockByType(_world.WorldCenterXY, _world.WorldCenterXY, BlockType.AIR, false);
            var spawnedPlayer = Instantiate(_net.playerPrefab, new Vector3(spawnPos.x, spawnPos.y, 0),
                Quaternion.identity);

            NetworkServer.AddPlayerForConnection(conn, spawnedPlayer);
        }
    }
}