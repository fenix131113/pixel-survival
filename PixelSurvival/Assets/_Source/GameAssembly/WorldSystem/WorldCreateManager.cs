using System;
using System.Collections;
using System.Collections.Generic;
using GameAssembly.Core;
using Mirror;
using UnityEngine;
using VContainer;

namespace GameAssembly.WorldSystem
{
    public class WorldCreateManager : NetworkBehaviour
    {
        private const int INITIAL_CHUNK_RADIUS = 2;
        private const float CHUNK_SYNC_INTERVAL = 0.1f;

        private readonly Dictionary<int, Coroutine> _syncCoroutines = new();
        private readonly Dictionary<int, HashSet<ChunkCoord>> _sentChunksByConnection = new();
        private readonly HashSet<int> _seedSentConnections = new();

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
            if (NetworkServer.active)
                return;
            
            _world.SetupSeed(seed);
        }

        [TargetRpc]
        public void Target_LoadChunk(NetworkConnectionToClient target, Chunk chunk)
        {
            if (NetworkServer.active)
                return;

            if (_world.GetChunk(chunk.Coord) != null)
                return;

            _world.SetupChunk(chunk);
        }

        [ClientRpc(includeOwner = false)]
        public void Rpc_SyncCell(ChunkCoord chunkCoord, int x, int y, CellData cell)
        {
            if (NetworkServer.active)
                return;

            var world = GameInstaller.Resolve<World>();
            var currentChunk = world.GetChunk(chunkCoord);

            currentChunk?.SetCell(x, y, cell);
        }

        [Server]
        public void Server_SendWorldToConn(NetworkConnectionToClient conn)
        {
            if (conn == null)
                return;

            var connectionId = conn.connectionId;

            if (_syncCoroutines.ContainsKey(connectionId))
                return;

            _syncCoroutines[connectionId] = StartCoroutine(WorldSyncCoroutine(conn));
        }

        [Server]
        public void Server_StopSendingWorldToConn(NetworkConnectionToClient conn)
        {
            if (conn == null)
                return;

            var connectionId = conn.connectionId;

            if (_syncCoroutines.TryGetValue(connectionId, out var coroutine))
                StopCoroutine(coroutine);

            _syncCoroutines.Remove(connectionId);
            _sentChunksByConnection.Remove(connectionId);
            _seedSentConnections.Remove(connectionId);
        }

        [Server]
        private IEnumerator WorldSyncCoroutine(NetworkConnectionToClient conn)
        {
            var wait = new WaitForSeconds(CHUNK_SYNC_INTERVAL);
            var world = GameInstaller.Resolve<World>();

            yield return new WaitUntil(() => world.IsLoaded.Value);

            while (conn is { isAuthenticated: true })
            {
                if (!conn.isReady || !conn.identity)
                {
                    yield return wait;
                    continue;
                }

                var connectionId = conn.connectionId;

                if (!_seedSentConnections.Contains(connectionId))
                {
                    Target_LoadSeed(conn, world.Seed);
                    _seedSentConnections.Add(connectionId);
                }

                SendMissingNearbyChunks(conn, world, connectionId);

                yield return wait;
            }

            if (conn != null)
                Server_StopSendingWorldToConn(conn);
        }

        [Server]
        private void SendMissingNearbyChunks(NetworkConnectionToClient conn, World world, int connectionId)
        {
            if (!_sentChunksByConnection.TryGetValue(connectionId, out var sentChunks))
            {
                sentChunks = new HashSet<ChunkCoord>();
                _sentChunksByConnection[connectionId] = sentChunks;
            }

            var playerPosition = conn.identity.transform.position;
            var centerChunk = new ChunkCoord(
                Mathf.FloorToInt(playerPosition.x / Chunk.CHUNK_SIZE),
                Mathf.FloorToInt(playerPosition.y / Chunk.CHUNK_SIZE));

            var chunksToSend = new List<Chunk>();

            for (var x = centerChunk.X - INITIAL_CHUNK_RADIUS; x <= centerChunk.X + INITIAL_CHUNK_RADIUS; x++)
            {
                for (var y = centerChunk.Y - INITIAL_CHUNK_RADIUS; y <= centerChunk.Y + INITIAL_CHUNK_RADIUS; y++)
                {
                    var coord = new ChunkCoord(x, y);

                    if (sentChunks.Contains(coord))
                        continue;

                    var chunk = world.GetChunk(coord);
                    if (chunk == null)
                        continue;

                    chunksToSend.Add(chunk);
                }
            }

            chunksToSend.Sort((left, right) =>
            {
                var leftDx = left.Coord.X - centerChunk.X;
                var leftDy = left.Coord.Y - centerChunk.Y;
                var rightDx = right.Coord.X - centerChunk.X;
                var rightDy = right.Coord.Y - centerChunk.Y;

                return (leftDx * leftDx + leftDy * leftDy).CompareTo(rightDx * rightDx + rightDy * rightDy);
            });

            foreach (var chunk in chunksToSend)
            {
                Target_LoadChunk(conn, chunk);
                sentChunks.Add(chunk.Coord);
            }
        }
    }
}