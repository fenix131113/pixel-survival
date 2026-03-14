using System;
using System.Collections;
using System.Linq;
using GameAssembly.Core;
using Mirror;
using UnityEngine;
using VContainer;

namespace GameAssembly.WorldSystem
{
    public class WorldCreateManager : NetworkBehaviour
    {
        [Inject] private World _world;

        private async void Awake()
        {
            try
            {
                if (!NetworkServer.active)
                    return;

                await _world.GenerateWorldAsync();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        [TargetRpc]
        public void Target_LoadSeed(NetworkConnectionToClient target, int seed)
        {
            _world.SetupSeed(seed);
        }

        [TargetRpc]
        public void Target_LoadChunk(NetworkConnectionToClient target, Chunk chunk)
        {
            _world.SetupChunk(chunk);
        }

        [ClientRpc(includeOwner = false)]
        public void Rpc_SyncCell(ChunkCoord chunkCoord, int x, int y, CellData cell)
        {
            if(NetworkServer.active)
                return;
            
            var world = GameInstaller.Resolve<World>();
            var currentChunk = world.GetChunk(chunkCoord);

            currentChunk?.SetCell(x, y, cell);
        }

        [Server]
        public void Server_SendWorldToConn(NetworkConnectionToClient conn)
        {
            StartCoroutine(WorldSendCoroutine(conn));
        }

        [Server]
        private IEnumerator WorldSendCoroutine(NetworkConnectionToClient conn)
        {
            Debug.Log("WORLD");
            yield return new WaitUntil(() => conn.isReady);
            
            var world = GameInstaller.Resolve<World>();
            Debug.Log("WORLD 2");
            yield return new WaitUntil(() => world.IsLoaded.Value);
            Debug.Log("WORLD 3");
            Target_LoadSeed(conn, world.Seed);

            for (var i = 0; i < world.Chunks.Count; i++)
                Target_LoadChunk(conn, world.Chunks.Values.ElementAt(i));
        }
    }
}